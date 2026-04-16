# 物料编码系统 PRD（V1.3 规则冻结稿）

> **版本说明（V1.3）**：本章档与自动化校验规格 `MaterialCodingSystem.Validation/specs/PRD_V1.yaml` 中 `meta.prd_freeze` **对齐**。若正文其他小节与「V1.3 冻结口径」冲突，**以冻结口径为准**（实现与测试应随后续迭代收敛到冻结语义）。

## 一、项目背景

当前电子元件物料编码依赖人工管理，存在以下问题：

* 物料重复创建（同**规格号（spec）**多次录入）
* 替代关系不清晰（A-Z管理缺乏系统约束）
* 编码规则缺乏一致性与可追溯
* 数据分散，不易检索与导出

本系统旨在构建一个**统一、可控的物料编码管理平台**，实现物料标准化、编码治理与替代关系管理。

---

## 二、项目目标

构建面向电子元件的物料编码管理系统，实现：

1. 编码唯一生成
2. 替代料（A-Z）管理
3. 防重复录入

   * **同分类内规格号（spec）唯一（仅启用态）**：`UNIQUE(category_code, spec) WHERE status = 1`，冲突必须阻止创建（`SPEC_DUPLICATE`）
   * **废弃（status=0）记录不占用 spec**：允许后续通过“新建物料”创建 **新记录/新 code** 复用 spec；**不允许复活旧记录替代新建**（见 **10.3**）
   * `spec_normalized` 仅用于搜索辅助，**不参与唯一性**；与 spec 重复相关的提示为人工判断辅助，不替代唯一性规则
4. 快速检索（编码 / 规格号 / 规格描述）
5. Excel 导出
6. 【对齐PRD新增】数据库备份与恢复（手动导出 / 启动自动备份 / 覆盖式恢复）

---

## 三、系统范围

### ✔ 包含

* 物料创建（主物料）
* 替代料管理
* 分类管理
* 编码搜索（前缀/模糊匹配）
* 规格号 / 规格描述 的 **LIKE 子串搜索**（V1：相似=包含匹配，人工判断）
* Excel 导出
* 【对齐PRD新增】数据库手动导出（完整可恢复）
* 【对齐PRD新增】启动自动备份（静默、保留多版本）
* 【对齐PRD新增】数据恢复（覆盖当前数据、恢复到备份时刻、失败不破坏当前数据）

### ❌ 不包含

* OA 流程
* 邮件自动解析
* 结构化字段（V2）

---

## 四、核心概念

### 4.1 物料主档（Material Group）

一组完全可替代的物料集合（用于承载A-Z替代关系）：

```
ZDA0000001
```

说明：

* Group 不作为采购编码对外使用
* 对外展示与检索的最小单位为 Item（含A-Z后缀）

---

### 4.2 物料实例（Material Item）

具体可采购物料：

```
ZDA0000001A / ZDA0000001B / ZDA0000001C
```

---

### 4.3 替代规则

| 标识 | 含义 |
| --- | --- |
| A | 主物料 |
| B-Z | 完全替代 |

约束：

* 最大 26 个（A-Z）
* 必须连续（A → B → C），不允许跳字母
* **连续性判定（必须）**：对同一 `group_id` 下全部 `suffix`（单字符 `A`–`Z`）须同时满足：
  * `minSuffix = 'A'`
  * `ASCII(maxSuffix) - ASCII(minSuffix) + 1 == count`（`count` 为该组内 Item 条数）
  * 若不满足 → 判定为 **suffix 不连续**，**禁止创建**新替代料
* 超过 Z（26个）禁止创建
* suffix 生成必须在事务中完成；如遇并发冲突（`UNIQUE(group_id, suffix)`）须 **事务级重试**（默认最多 3 次），耗尽 → **`SUFFIX_ALLOCATION_FAILED`**（见 **10.2 / 15.5 / 15.6**）

---

## 五、编码规则

编码结构：

```
[分类编码] + [7位流水号] + [A-Z]
```

示例：

```
ZDA0000001A
```

### 5.1 分类编码（示例）

| 分类 | 编码 |
| --- | --- |
| 电阻 | ZDA |
| 电容 | ZDB |
| 电感 | ZDC |
| 功率电感 | ZDD |
| 二/三极管 | ZDE |

### 5.2 流水号规则

* 按分类独立递增
* 仅在新建“主物料（A）”时占用流水号
* 替代料（B-Z）不占用流水号

---

## 六、数据模型

V1.2 仍采用三表模型：`Category`（分类）/ `MaterialGroup`（主档）/ `MaterialItem`（实例）。

---

### 6.1 分类表（Category）

| 字段 | 类型 | 说明 |
| --- | --- | --- |
| id | int | 主键 |
| code | string | 分类编码（唯一） |
| name | string | 分类名称（唯一） |

---

### 6.2 物料主档（MaterialGroup）

| 字段 | 类型 | 说明 |
| --- | --- | --- |
| id | int | 主键 |
| category_id | int | 外键，关联 `Category.id`（唯一关系字段） |
| category_code | string | 冗余字段，用于展示 / 导出 / 查询优化（非关系字段） |
| serial_no | int | 流水号（数据库存整数，展示时补齐7位） |

说明：

* 系统内部关联以 `category_id` 为准；`category_code` 仅作冗余字段（见下方“设计原则”）
* `category_code + serial_no` 共同决定同一分类下的 Group 展示标识（例如展示为 `ZDA0000001`）
* 新建主物料（A）时创建 `MaterialGroup`

#### 设计原则：Category 关联规则

系统内部关系统一使用 `category_id` 作为唯一外键；`category_code` 仅作为冗余字段存在，用于：

* 查询性能优化
* 导出（Excel / 报表）
* UI 展示

约束与要求：

* 不允许任何业务逻辑以 `category_code` 作为关系主键（不得用于关联 `Category` 的 JOIN 条件）
* 数据一致性由 **数据库外键 + 应用层双重保证**
* 兼容性要求：保留 `category_code` 字段，允许短期双写（`category_id` + `category_code` 同步写入）；查询/流程默认以 `category_id` 为准；原有依赖 `category_code` 作为关系条件的逻辑需标记为 deprecated（不立即删除）

#### 数据一致性规则（必须）

* `MaterialGroup.category_id` 必须存在于 `Category.id`
* `MaterialGroup.category_code` 必须与 `category_id` 映射一致（由 Service 层维护）
* 禁止出现 `category_code` 单独作为关联条件参与 `JOIN Category` 或作为关系字段参与约束（关系约束以 `category_id` 为准；`category_code` 仅限冗余用途）

---

### 6.3 物料实例（MaterialItem）

| 字段 | 类型 | 说明 |
| --- | --- | --- |
| id | int | 主键 |
| group_id | int | 所属主档（对应 `MaterialGroup.id`） |
| code | string | 完整编码（唯一） |
| suffix | char | A-Z |
| name | string | **物料展示名称（快照）**：**不由用户输入**；创建时在与插入**同一事务、同一连接**内读取 `category.name` 并写入 `material_item.name`；**不随分类改名而变更**（历史行保持创建时快照） |
| description | string | **规格描述**：用户输入的完整规格字符串（不做结构化解析）；用于展示与搜索 |
| spec | string | **规格号（供应商型号）**：用户识别物料的核心字段，原样保存；示例：`CL10A106KP8NNNC` |
| spec_normalized | string | **基于 description 生成的搜索辅助字段**（V1 仅转大写/去首尾空格/多空格压一）；不参与唯一性约束 |
| brand | string | 品牌 |
| is_structured | int | 是否已结构化（0=否 1=是） |

#### 6.3.1 术语区分（必须）

- **`material_item.name`**：定义为 **“历史名称（快照）”**。创建时从 `category.name` 读取并写入；仅用于历史追溯与一致性，不保证与当前分类名称一致。
- **`display_name`（逻辑概念，不新增字段）**：定义为 **“当前分类名称”**（`category.name`），用于 **UI 展示与导出**。

强制规则（必须/禁止）：

- **UI 展示与导出场景，必须使用 `category.name`（display_name），不得使用 `material_item.name` 作为展示名称。**

说明：

* `code = category_code + serial_no + suffix`（例如 `ZDA0000001A`）
* 同一 `group_id` 下 `suffix` 必须连续（A→B→C），最多 26 个

---

### 6.4 评审结论：ER模型与表结构（最终建议）

#### 6.4.1 ER模型结论

必须采用“三层模型”：

```
Category（分类）
  ↓ 1:N
MaterialGroup（物料主档）
  ↓ 1:N
MaterialItem（物料实例 A-Z）
```

原因（评审要点）：

* 少一层会导致替代料（A-Z）难以被正确建模与约束
* 编码规则（分类流水号 + A-Z）无法稳定落库
* 搜索与展示无法按“替代组”进行分组/聚合

---

#### 6.4.2 关键约束口径（与PRD强一致）

* 编码唯一：`UNIQUE(code)`
* 同组内 A-Z 唯一：`UNIQUE(group_id, suffix)`
* 分类流水号唯一：`UNIQUE(category_id, serial_no)`（在 `material_group`）
* 规格唯一性（分类内，仅启用态）：`UNIQUE(category_code, spec) WHERE status = 1`（存在启用态重复 spec 必须禁止创建：`SPEC_DUPLICATE`）

**规格唯一性补充说明（工程口径）**

* **spec（规格号）视为供应商定义的唯一样号标识**（型号 Part Number）。
* 在电子元件领域中，**同一型号通常不会被多个品牌复用**；同时 **brand** 字段存在命名不规范（大小写、别名、多语言等）问题。
* 因此系统唯一性设计为 **“同分类 + 启用态 spec 唯一”**，并明确：
  * **同一 spec 在“正常状态（status=1）数据集”中只允许存在一条记录**（已废弃 `status=0` 不参与 spec 唯一性，占用可被释放，见 **10.1**）；
  * **即使录入时 brand 不同，只要 spec 相同，也视为同一物料**（不允许另建一条）；
  * **brand 不参与唯一性约束**。

说明：

* 物料编码体系唯一性仍以 `code` 为主唯一标识（等价 `category_code + serial_no + suffix`）
* **spec = 规格号（供应商型号）**（同分类在 `status=1` 数据集内不允许重复；`status=0` 仅保留历史，可被新建复用）
* **spec_normalized = 基于 description 生成的搜索辅助字符串**（不参与唯一性约束）
* `UNIQUE(category_code, spec) WHERE status = 1` 冲突（针对 `status=1`）→ 必须阻止创建（错误码：`SPEC_DUPLICATE`）
* 注意：spec 唯一性与编码/后缀唯一性不同；编码与后缀仍要求“废弃不释放”（见 **10.1**）

**category_code 冗余一致性（必须）**

* 约束：`material_item.category_code` **必须与** 其所属 `material_group.category_code` **一致**。
* 该一致性**由 Service 层在创建/更新路径上保证**；**不依赖**数据库外键或触发器自动校验 `group_id` 与冗余字段的同步。

---

#### 6.4.3 推荐DDL（可直接执行，SQLite）

> 说明：为支持“同分类 spec 唯一”与检索过滤，建议在 `material_item` 冗余 `category_code`。

```sql
-- 分类表
CREATE TABLE category (
    id          INTEGER PRIMARY KEY AUTOINCREMENT,
    code        TEXT NOT NULL UNIQUE,   -- ZDA/ZDB...
    name        TEXT NOT NULL UNIQUE,   -- 电阻/电容...
    created_at  TEXT DEFAULT CURRENT_TIMESTAMP
);

-- 物料主档（替代组）
CREATE TABLE material_group (
    id            INTEGER PRIMARY KEY AUTOINCREMENT,
    category_id   INTEGER NOT NULL,     -- 外键：Category.id（唯一关系字段）
    category_code TEXT NOT NULL,        -- 冗余：用于展示/导出/查询优化（非关系字段）
    serial_no     INTEGER NOT NULL,     -- 1,2,3...
    created_at    TEXT DEFAULT CURRENT_TIMESTAMP,

    UNIQUE(category_id, serial_no),
    FOREIGN KEY (category_id) REFERENCES category(id)
);

-- 物料实例（可采购物料）
CREATE TABLE material_item (
    id            INTEGER PRIMARY KEY AUTOINCREMENT,
    group_id      INTEGER NOT NULL,
    category_id   INTEGER NOT NULL,     -- 外键：Category.id（唯一关系字段）
    category_code TEXT NOT NULL,        -- 冗余：用于“分类内spec唯一”等约束/检索过滤

    code          TEXT NOT NULL UNIQUE, -- ZDA0000001A
    suffix        TEXT NOT NULL,        -- A-Z

    name          TEXT NOT NULL,
    description   TEXT NOT NULL,        -- 完整规格描述（必填）
    spec          TEXT NOT NULL,        -- 规格号/供应商型号（必填；仅启用态 status=1 在同分类内唯一）
    spec_normalized TEXT NOT NULL,      -- 由 description 生成，仅搜索辅助
    brand         TEXT,

    status        INTEGER NOT NULL DEFAULT 1, -- 1=active（正常）0=deprecated（废弃），不可物理删除
    is_structured INTEGER DEFAULT 0,    -- 0=未结构化 1=已结构化
    created_at    TEXT DEFAULT CURRENT_TIMESTAMP,

    UNIQUE(group_id, suffix),

    FOREIGN KEY (group_id) REFERENCES material_group(id),
    FOREIGN KEY (category_id) REFERENCES category(id)
);

-- spec 唯一性（业务规则）：同分类 + 启用态(status=1) 唯一
-- SQLite 支持部分唯一索引：若目标数据库不支持，则必须由应用层校验保证，并保留普通索引用于查询加速
CREATE UNIQUE INDEX ux_material_item_category_spec_active
ON material_item(category_code, spec)
WHERE status = 1;

-- 结构化字段（V2预留，V1可先建表不强依赖）
CREATE TABLE material_attribute (
    id              INTEGER PRIMARY KEY AUTOINCREMENT,
    material_item_id INTEGER NOT NULL,
    attr_key         TEXT NOT NULL,
    attr_value       TEXT NOT NULL,
    created_at       TEXT DEFAULT CURRENT_TIMESTAMP,
    FOREIGN KEY (material_item_id) REFERENCES material_item(id)
);

-- 索引（为检索服务）
CREATE INDEX idx_material_item_spec ON material_item(spec);
CREATE INDEX idx_material_item_spec_normalized ON material_item(spec_normalized);
CREATE INDEX idx_material_item_code ON material_item(code);
CREATE INDEX idx_item_category_spec_norm ON material_item(category_code, spec_normalized);
CREATE INDEX idx_material_item_group_id ON material_item(group_id);
CREATE INDEX idx_material_item_status ON material_item(status);
```

可选优化字段（非必须）：

* `search_text TEXT`：用于汇总可检索文本（便于未来接入FTS/ES）

**spec_normalized 生成规则（V1 定稿，必须替换旧规则）**

输入来源：**仅允许基于 `description` 生成**（不得基于 `spec` 生成）。

V1 **仅允许**三步：

1. 转大写  
2. 去首尾空格  
3. 多空格压缩为 1 个空格  

**明确禁止写入实现/文档（V1）**：单位归一（µF/uF/UF）、数值合并、语义解析、复杂 token 处理、编辑距离/NLP/Embedding。

示例：

* description ` 10uF  16V  0603 ` → spec_normalized `10UF 16V 0603`

---

#### 6.4.4 编码生成逻辑（与DB约束配套）

新建主物料（A）：

* 在事务内按 `category_id` 查询最大 `serial_no`，+1 后插入 `material_group`
* 插入 `material_item`（suffix = 'A'），生成 `code = category_code + serial_no(7位补零) + 'A'`
* 若出现并发导致 `UNIQUE(category_id, serial_no)` 冲突，则重试生成

新增替代料（B-Z）：

* 查询 `material_item` 中同 `group_id` 的最大 `suffix`，取下一个字母
* 插入新 `material_item`，并由 `UNIQUE(group_id, suffix)` 保证不重复

---

#### 6.4.5 规格搜索落地（V1：子串包含）

目标：找到“可能相关的物料”（**相似=字符串包含**），供人工判断是否同料/是否做替代料。

实现（固定）：

```sql
WHERE category_code = ?
  AND status = 1
  AND (
        spec LIKE '%' || ? || '%'
     OR spec_normalized LIKE '%' || ? || '%'
      )
LIMIT 20;
```

规则：

* **必须**带 `category_code` 过滤
* **必须** `LIMIT 20`
* V1 **不实现**相似度百分比、编辑距离、NLP、Embedding
* spec 唯一性仍以 **`UNIQUE(category_code, spec) WHERE status = 1`** 为准；`spec_normalized` 相同仅作列表提示，**不阻止创建**

## 七、核心功能

### 7.1 新建主物料（增强版）

新增关键能力：**录入前候选列表（LIKE 子串，提示，不强制阻止）**

流程：

1. 选择物料种类（分类）

   * 分类下拉/选择器仅展示**系统内已存在的分类**
   * 若无目标分类，点击“新增分类”打开独立窗口新增（新增后可回填到选择器）
2. **分别填写（必填，禁止单框混写）**：

   * **规格号（spec）**：供应商型号（示例：`CL10A106KP8NNNC`）
   * **规格描述（description）**：完整规格字符串（示例：`10uF 16V 0603 X5R`）
3. 用户输入 **description** 或 **spec** 后触发（300ms 防抖）：按 **6.4.5** 执行规格搜索，返回 **Top 20** 候选（人工判断是否重复/是否走替代料）
4. 提交时系统执行：

   * 生成 `spec_normalized = Normalize(description)`（仅 V1 三步规则，见第 10 章/第 15 章）
   * **唯一性（仅启用态）**：若同分类存在 `status=1` 的同 `spec` → 禁止保存（`SPEC_DUPLICATE`）
   * **提示**：若仅 `spec_normalized` 与某条记录相同（但 spec 不同）→ **仅提示**，不阻止创建
   * 生成编码（如 `ZDA0000001A`）
   * 创建 Group + Item(A)

---

### 7.2 新增替代料（增强版）

新增能力：**编码搜索（快速定位主料/主档）**

流程：

1. 输入编码关键字（如：`ZDA00001`）
2. 系统返回候选（支持前缀匹配与模糊匹配）
3. 选择目标主物料（A项）或任一组内物料
4. 系统定位所属 Group，计算下一个可用后缀字母
5. 填写替代料信息并创建（B-Z）（**规格号 spec + 规格描述 description 必填**，规则同 **9.1**）

---

### 7.3 搜索功能（核心模块）

#### 7.3.1 编码搜索

目标：快速定位物料（尤其用于替代料添加）

**两阶段策略（必须实现）**：

1. **前缀匹配**：`code LIKE 'xxx%'`（优先返回，性能更好）
2. **结果不足时补充模糊匹配**：`code LIKE '%xxx%'`

返回字段：

```
编码 | 名称 | 规格号(spec) | 品牌
```

---

#### 7.3.2 规格号 / 规格描述搜索（V1）

目标：找到“可能相似的型号/描述”（**相似=子串包含**），用于人工判断与替代料引导。

固定 SQL 口径：见 **6.4.5**（必须带 `category_code` 且 `LIMIT 20`）。

重要规则（必须明确）：

* V1 **不实现**相似度阈值、编辑距离、NLP、Embedding
* `spec_normalized` 仅辅助 LIKE 命中，不参与唯一性

---

### 7.4 Excel 导出

**V1.3 冻结规则（双工作簿结构）**

1. **Sheet1（全量）**

   * **名称**：`全量`（或产品化等价命名）
   * **范围**：库内**全部**物料行（**含** `status = 0` 已废弃）
   * **字段（与分类 Sheet 完全一致，列顺序固定）**：

     1) 编码（`code`）  
     2) 分类编码（`category_code`）  
     3) 名称（`name`，**导出视图名称**，见下方“名称语义说明”）  
     4) 规格号（`spec`）  
     5) 规格描述（`description`）  
     6) 品牌（`brand`）  
     7) 状态（`status`：`1=正常`，`0=已废弃`；**必选列，不可省略**）

   * **排序（必须）**：按结构化键排序，**禁止仅按完整 `code` 字符串排序**；其中 `base_code（逻辑）= material_group.serial_no`，`suffix` 按字母顺序（A < B < ... < Z）。  
     **推荐排序（建议强制写入）**：正常物料优先展示，已废弃在后：

```text
ORDER BY mi.status DESC, mg.category_code, mg.serial_no, mi.suffix
```

补充（稳定排序，必须）：**当排序键相同时，以 `code` 作为最终稳定排序键**。

2. **分类 Sheet（仅有效数据）**

   * **范围**：**每个分类一个 Sheet**，包含该分类下**全部**物料（**含** `status = 0` 已废弃）；通过 `status` 列区分状态
   * **字段**：与 Sheet1 **完全相同**（列顺序固定；`status` 为必选列）
   * **排序**：与 Sheet1 保持一致（推荐同样以 status 优先）：

```text
ORDER BY mi.status DESC, mg.category_code, mg.serial_no, mi.suffix
```

补充（稳定排序，必须）：**当排序键相同时，以 `code` 作为最终稳定排序键**。

#### 7.4.1 名称语义说明（关键，避免歧义）

1. **导出“名称”（本节字段 `name`）来源**：导出中的“名称”字段使用**当前分类名称**（`category.name` / `display_name`），**不使用** `material_item.name`（历史快照）。
2. **快照字段语义**：`material_item.name` 为创建时的分类名称快照，用于系统内历史数据一致性与可追溯性；后续分类改名不回写历史物料行。
3. **导出名称语义**：导出名称用于反映**当前分类体系**，与历史快照名称语义不同。
4. 消歧声明：**导出数据为当前分类视图，不保证与历史快照名称一致。**

**逻辑 base 说明（与排序键对齐）**

* 文档与规格中的 **base_code（逻辑）** **不单独落库**；在排序/导出语境下 **等价于 `material_group.serial_no`**（与 `category_code` 共同定位组；完整 `code` 仍由编码规则拼接生成）。

**导出 SQL 示例（Sheet1 全量，必须可对照实现）**

```sql
SELECT
  mi.code,
  mg.category_code,
  c.name AS name,
  mi.spec,
  mi.description,
  mi.brand,
  mi.status
FROM material_item mi
JOIN material_group mg ON mi.group_id = mg.id
JOIN category c ON mg.category_id = c.id
ORDER BY mi.status DESC, mg.category_code, mg.serial_no, mi.suffix, mi.code;
```

**导出 SQL 示例（分类 Sheet：仅有效，必须可对照实现）**

```sql
SELECT
  mi.code,
  mg.category_code,
  c.name AS name,
  mi.spec,
  mi.description,
  mi.brand,
  mi.status
FROM material_item mi
JOIN material_group mg ON mi.group_id = mg.id
JOIN category c ON mg.category_id = c.id
WHERE mg.category_code = :category_code
ORDER BY mi.status DESC, mg.category_code, mg.serial_no, mi.suffix, mi.code;
```

---

### 7.5 分类管理

支持：

* 新增分类
* 分类编码唯一
* 分类名称唯一

---

### 7.6 【对齐PRD新增】数据库备份与恢复（V1.3）

本节定义 V1.3 的“数据库手动导出 / 自动备份 / 数据恢复”能力边界。该能力仅覆盖**本地 SQLite 数据库文件**层面的备份与恢复；禁止引入云备份、分布式、增量备份等复杂机制。

#### 7.6.1 手动导出数据库（完整可恢复）

PRD 需求（冻结）：

* 用户可选择路径导出
* 导出结果必须是**完整可恢复数据库**（可用于恢复到导出时刻的数据状态）
* 导出失败不得影响主流程：导出为**非阻断功能**（失败仅提示/记录，不影响正常使用与数据写入）

一致性要求（冻结）：

* 导出必须保证一致性：导出文件必须对应某一确定时刻的数据库状态（不得出现“导出文件内部数据不一致”的情形）

#### 7.6.2 自动备份（启动后触发，静默）

PRD 需求（冻结）：

* 应用启动后自动执行一次备份（触发点为“启动后”，不得阻塞启动流程）
* 不打扰用户（静默执行；失败不弹出阻断式对话）
* 保留多版本（至少保留最近 N 份；N 为固定产品常量，V1.3 不引入复杂配置系统）
* 具备清理策略（超过保留数量的旧备份自动清理）

行为约束（冻结）：

* 自动备份失败不得影响系统：备份为**非阻断功能**（失败仅提示/记录，不影响正常使用与数据写入）

#### 7.6.3 数据恢复（覆盖当前数据，失败不破坏）

PRD 需求（冻结）：

* 恢复 = **覆盖当前数据**
* 恢复后数据必须**完全回到备份时刻**（数据库状态以备份文件为准）
* 恢复失败不得破坏当前数据（不得出现“恢复失败导致当前库不可用/部分损坏”的情形）
* 恢复完成后必须提示用户**重启应用**，且应用行为语义以“重启后进入已恢复的数据状态”为准

安全要求（冻结）：

* 必须采用“文件级替换”的恢复语义（以备份文件覆盖当前数据库文件），以保证恢复后的状态与备份时刻一致
* 【允许增强（轻量）】恢复前校验：至少校验备份文件存在、可读、文件大小非零；必要时可增加 SQLite 文件头/打开可用性校验（轻量）
* 必须具备“原子替换”语义：恢复过程不得在失败时破坏当前库文件（例如通过临时文件 + 原子重命名/替换等方式保证“要么成功切换到备份库，要么维持原库不变”）

---

## 八、相似度与检索（V1 闭环）

### 8.1 V1 定稿：不实现复杂相似度算法

* V1 中“相似/可能重复”的统一含义：**字符串包含**（`LIKE '%keyword%'`），见 **6.4.5**
* **禁止**：编辑距离、学习型相似度、NLP、向量检索（归入 V2 能力边界，见 **10.5**）

### 8.2 未来扩展（仅标注，不在 V1 实现）

* 结构化后可做更智能的推荐与相似度（见 **十三/十四**）

## 九、页面交互设计（重点）

### 9.0 建料模式切换（V1.3 新增，冻结口径）

系统支持两种建料模式（**模式切换**的产品语义，不删除既有自动编码能力）：

* **Manual（手工输入，默认启用）**
  * **UI 默认仅展示 Manual 入口**（本版本不在 UI 中提供 Auto 入口/开关）
  * 用户在创建物料时 **手动输入 `code`**
  * 提交时必须校验：
    * **`code` 全局唯一**：`UNIQUE(code)`（**包含已废弃**，`status=0` 仍占用编码不释放，见 **10.1**）
    * **`spec` 分类内唯一**：`UNIQUE(category_code, spec) WHERE status = 1`（仅约束 `status=1` 数据集，见 **10.1**）
  * **Manual 编码格式（冻结）**：
    * 固定格式：`[分类编码3位] + [7位流水号] + [A-Z]`（示例统一：`ELC0000123A`）
    * 输入标准化（提交前，冻结）：对用户输入 `code` 执行 `trim()` 并统一转为大写（`ToUpperInvariant()`）后，再进入后续校验与解析
    * 正则：`^[A-Z]{3}[0-9]{7}[A-Z]$`；不满足 → **`CODE_FORMAT_INVALID`**
    * 解析规则：
      * `category_code = code[0..2]`（前 3 位）
      * `serial_no = int(code[3..9])`（中间 7 位，按十进制转整数）
        * **合法范围（Manual）**：`serial_no >= 1`；`0000000` 视为非法 → **`CODE_FORMAT_INVALID`**
      * `suffix = code[10]`（最后 1 位；非 `A-Z` → **`SUFFIX_INVALID`**）
  * **分类一致性校验（Manual，冻结）**：
    * 解析出的 `category_code` 必须等于表单所选 `category_code`
    * 不一致 → **`CATEGORY_MISMATCH`**（提示“编码分类与当前选择分类不一致”）
  * **MaterialGroup 创建规则（Manual，冻结）**：
    * 提交时按 `(category_id, serial_no)` 查询 `material_group`
      * 存在 → **复用**该 `group`
      * 不存在 → **创建** `material_group(category_id, category_code, serial_no)`
    * **禁止**走 Auto 的 `max(serial_no)+1` 流水分配逻辑（Manual 必须使用用户输入 code 中解析出的 `serial_no`）

* **Auto（自动生成，保留能力，当前 UI 关闭/隐藏入口）**
  * 自动编码规则保持不变（见 **五 / 10.2 / 15.6**）：
    * **分类流水号生成**
    * **suffix 从 `A` 起始**（主物料为 A；替代料按 A→B→… 连续分配）

### 9.1 新建物料页面（核心改造）

**字段拆分（强制）**

* **规格号（spec）**：必填；填写供应商型号（示例：`CL10A106KP8NNNC`）
* **规格描述（description）**：必填；填写完整规格（示例：`10uF 16V 0603`）

**明确禁止**：使用**一个输入框**同时承载 `spec + description`。

**提示文案（必须展示在输入框旁）**

* 规格号：填写供应商型号（如 CL10A106KP8NNNC）
* 规格描述：填写完整规格（如 10uF 16V 0603）

UI 结构（示意）：

```
----------------------------------
建料模式：Manual（默认；Auto 入口隐藏）
----------------------------------
物料编码(code)    [            ]  （必填，手工输入）
----------------------------------
物料种类（分类）选择器 [ 下拉选择 ]  [新增分类]
----------------------------------
规格号(spec)     [            ]  （必填）
规格描述(desc)  [            ]  （必填）
----------------------------------
【候选物料列表（实时，LIKE，Top20）】
编码 | 规格号 | 规格描述 | 名称 | 品牌 | 状态
----------------------------------
名称（只读：当前所选分类的 category.name；提交后写入 item.name 快照，不随分类改名回写）
品牌
----------------------------------
[提交]
```

交互逻辑：

1. 分类选择器：

   * 默认从现有分类中选择
   * 点击“新增分类”弹出独立窗口新增分类（校验编码/名称唯一）
   * 新增成功后必须：

     * 自动关闭弹窗
     * 刷新分类列表
     * 自动选中新增分类（回填到选择器）
2. 用户编辑 **description** 或 **spec** → 触发搜索（300ms 防抖），按 **6.4.5** 召回候选（**LIMIT 20**）
3. 若列表非空，展示决策条（不阻断提交）：

```text
⚠ 检测到可能相关的物料（V1：子串包含），是否作为替代料？

[作为替代料加入]  [强制新建物料]
```

4. 复用已有物料闭环（必须）：

   * 当用户点击候选行（或在详情中点击操作）：

     * 弹出物料详情
     * 详情中提供操作按钮：`[作为替代料加入该组]`
     * 点击后行为：

       * 自动跳转到“新增替代料流程”
       * 自动定位 `group_id`（回填到替代料页面的已选中状态）

5. 点击提交时：

   * **Manual 模式下**提交校验顺序（必须固定）：
     0) **输入标准化**：`code = trim(code)` 且转大写；后续所有校验均基于标准化后的 `code`
     1) **code 格式校验**：`^[A-Z]{3}[0-9]{7}[A-Z]$`；不满足 → **禁止提交**（`CODE_FORMAT_INVALID`）
     2) **code 解析**：解析出 `category_code / serial_no / suffix`
        * `serial_no >= 1`；否则（如 `0000000`）→ **禁止提交**（`CODE_FORMAT_INVALID`）
        * 若 `suffix` 非 `A-Z` → **禁止提交**（`SUFFIX_INVALID`）
     3) **分类一致性**：解析出的 `category_code` 必须等于当前选择分类；不一致 → **禁止提交**（`CATEGORY_MISMATCH`）
     4) **code 唯一**：校验 `UNIQUE(code)`；若冲突 → **禁止提交**并提示“编码已存在”（`CODE_DUPLICATE`）
     5) **spec 唯一（仅启用态）**：见下条（`SPEC_DUPLICATE`）
   * 生成 `spec_normalized = Normalize(description)`（V1 三步规则）
   * 候选列表**不得**作为唯一性依据
   * **spec 唯一性（仅启用态）**：
     * 若存在 `status=1` 的同分类同 `spec` → **禁止提交**并提示“该规格型号已存在（启用中），禁止重复创建”（错误码：`SPEC_DUPLICATE`）
     * 若仅存在 `status=0` 的历史记录 → **允许创建新物料**（新记录 / 新 code）；可选弱提示“发现历史废弃规格，将创建新物料记录”
   * 若仅 `spec_normalized` 与某记录相同但 **spec 不同**：允许创建，并可提示“描述归一后相同，请确认是否升级料/替代料”（**不阻止创建**）

错误提示机制（必须结构化）：

* `CODE_FORMAT_INVALID`：显示在 **code（物料编码）** 输入框下（红色提示，“编码格式不正确”）
* `CATEGORY_MISMATCH`：显示在 **code（物料编码）** 输入框下（红色提示，“编码分类与当前选择分类不一致”）
* `CODE_DUPLICATE`：显示在 **code（物料编码）** 输入框下（红色提示）
* `SPEC_DUPLICATE`：显示在 **spec（规格号）** 输入框下（红色提示）
* `SUFFIX_INVALID`：显示在 **code（物料编码）** 输入框下（红色提示，“编码后缀无效”）
* `CODE_CONFLICT_RETRY`：全局提示（弹窗或顶部）

状态设计（必须补齐）：

* 搜索（输入后300ms触发）：

  * loading：候选列表区域显示加载中
  * 空状态：未发现匹配项

**description 字段定位（V1）**

* V1：用户手动输入完整规格字符串（展示 + 搜索辅助字段生成）
* V2：可由结构化 `material_attribute` 生成展示文本（见 **十四**，V1 不启用解析）

---

### 9.2 替代料添加页面

**V1.3 冻结：创建入口为 ByCode（基准物料编码）**

* **输入**：`base_material_code`（用户从搜索结果选中一行即等价于提供基准编码）
* **行为**：系统根据基准编码解析所属组并完成 **单事务** 编排：`组解析 → suffix 计算 → 插入新物料`；**Presentation 层禁止暴露 `group` / `groupId` 等内部标识**（用户向文案不得出现 *group*）
* **基准约束**：基准物料必须 `status = 1`；若基准已废弃（`status = 0`）→ 返回 **`ANCHOR_ITEM_DEPRECATED`**，禁止创建
* **废弃级联**：对某一物料执行废弃 **不级联** 废弃同组其他替代料（见 **10.3** 补充）

**UI 状态机（冻结）**

* **基准加载态**（四态，仅描述基准解析与上下文展示）：`未选择 | 加载中 | 成功 | 失败`
* **提交态**（独立）：例如 `提交中 / 成功 / 失败`，**禁止**与基准加载四态混用同一枚举或同一套 UI 绑定

UI 结构（示意）：

```
----------------------------------
编码搜索框 [          ]
----------------------------------
候选列表：
编码 | 名称 | 规格号 | 状态
----------------------------------
选中物料后（必须展示，用户向）：
主档展示标识（如 ZDA0000001，不含内部 id）
当前已有 suffix 列表（A/B/C）
明确提示：
将创建下一个替代料：D
----------------------------------
填写替代料信息（名称只读规则同 9.1：来自分类名快照逻辑在服务端完成）
----------------------------------
```

交互逻辑：

1. 输入编码 → 实时搜索（前缀/模糊）
2. 选择主料/组内任一 **正常** 物料 → 自动加载基准上下文（进入基准加载状态机）
3. 展示主档/后缀预测信息（见上方“必须展示”）
4. 创建替代料（B-Z）：调用 **`CreateReplacementByCode`（命名示意）** 或等价 Application API，保证与 **15.4** 事务口径一致

状态设计（必须补齐）：

* 编码搜索：

  * loading：候选列表区域显示加载中
  * 无结果提示：未找到匹配的编码

错误提示机制（必须结构化）：

* `SUFFIX_OVERFLOW`：表单顶部错误提示
* `CODE_CONFLICT_RETRY`：全局提示（弹窗或顶部）

---

### 9.3 独立搜索页面（可选）

支持：

* 编码搜索
* 规格号/规格描述搜索（LIKE 候选列表）

---

## 十、核心约束

### 10.1 唯一性

* 编码唯一
* 物料唯一性以编码体系为准：`code` 唯一（等价于 `category_code + serial_no + suffix`）
* **唯一性范围（关键规则，必须写死）**：
  * `UNIQUE(code)`：**全局唯一**，且**包含已废弃**（`status=0` 仍占用编码，不释放）
  * `UNIQUE(group_id, suffix)`：**同组唯一**，且**包含已废弃**（`status=0` 仍占用 suffix 槽位，不释放）
  * 说明：**包含已废弃数据（status=0 不释放），以保证编码体系的历史一致性与可追溯性**
* **规格号唯一性（分类内）**：

  * **spec = 规格号（供应商型号）**，为用户识别物料的核心字段
  * **唯一性口径（必须修正）**：`UNIQUE(category_code, spec) WHERE status = 1`（已废弃 `status = 0` 不参与 spec 唯一性）
  * `spec_normalized` **不参与**唯一性约束（仅搜索辅助）
  * **spec 相同（在 status=1 数据集中）** → 禁止创建（`SPEC_DUPLICATE`）
  * **spec_normalized 相同但 spec 不同** → **仅提示**，不阻止创建

**规格唯一性补充说明（与 6.4.2 一致）**

* spec 视为供应商唯一样号；在 **正常状态（status=1）数据集**中，同一分类下 **同一 spec 仅允许一条记录**；**brand 不参与唯一性**；不同品牌不得通过不同 `brand` 绕过同一 spec 的唯一性。

废弃数据与唯一性（关键决策，必须定稿）：

* 方案（采用，V1.3 裁决）：**废弃数据不参与 spec 唯一性（允许复用 spec）**

唯一性范围说明：

* 已废弃数据（`status=0`）**不参与** `spec` 唯一性约束
* 废弃后允许在同一 `category_code` 下重新创建相同 `spec` 的**新物料**（`status=1`）
* 废弃操作视为“**释放 spec 占用**”
* 产品语义（必须）：**废弃用于修正错误录入；废弃后的规格允许重新创建，以提升业务可用性。**
* 注意：以上变更**仅作用于 spec 唯一性**；`code` 与 `(group_id, suffix)` 唯一性仍为“废弃不释放”，不得改变

**spec_normalized（V1）**

* **输入来源**：仅允许基于 **`description`** 生成
* **职责**：搜索辅助（配合 `spec` 一起做 LIKE）；**不得**用于唯一性判断/数据库唯一约束/业务强制拦截
* **生成规则**：仅允许 **转大写 / 去首尾空格 / 多空格压一**（见 **6.4.3** 与 **15.2**）
* **禁止**：单位归一、数值合并、语义解析、复杂 token、把 `spec_normalized` 当“归一化后唯一”

---

### 【spec / description / spec_normalized 规则（V1 闭环）】

#### 1. spec（规格号）

* **定义**：规格号（供应商型号），示例：`CL10A106KP8NNNC`
* **定位**：用户识别物料的核心字段；**分类内启用态唯一**（`UNIQUE(category_code, spec) WHERE status = 1`）
* **存储**：原样保存

#### 2. description（规格描述）

* **定义**：用户输入的完整规格字符串，示例：`10uF 16V 0603 X5R`
* **定位**：展示与搜索；**不做结构化解析（V1）**
* **存储**：原样保存

#### 3. spec_normalized（搜索辅助）

* **定义**：基于 `description` 生成的辅助字符串，仅用于搜索辅助
* **禁止**：参与任何唯一性约束；禁止作为“输入合法性”唯一依据（不替代业务对 spec 的校验）

#### 4. 唯一性边界（表格口径）

| 场景 | 是否允许 |
| --- | --- |
| spec 完全相同 | ❌ 禁止（`SPEC_DUPLICATE`） |
| spec 不同但 spec_normalized 相同 | ✅ 允许（可提示，不阻止） |
| 仅 LIKE 命中（包含） | ✅ 允许（提示/人工判断） |

### 10.2 替代规则

* A-Z 连续
* 最大 26 个
* **连续性判定（必须）**：同一 `group_id` 下须满足 `minSuffix = 'A'` 且 `ASCII(maxSuffix) - ASCII(minSuffix) + 1 == count`；否则视为 suffix 不连续，**禁止创建**新替代料
* 超过 Z 禁止创建（**`SUFFIX_OVERFLOW`**）
* **创建模式（V1.3）**：以 **基准物料编码** 驱动（`CreateReplacementByCode`）；基准物料 **`status` 必须为 1**，否则 **`ANCHOR_ITEM_DEPRECATED`**
* suffix 生成必须在**单事务**中完成；依赖 **`UNIQUE(group_id, suffix)`** 保证唯一；遇并发冲突 **事务级重试**，**最大 3 次**；若仍失败 → **`SUFFIX_ALLOCATION_FAILED`**（与全局 `code` 冲突重试失败 **`CODE_CONFLICT_RETRY`** 区分）
* **替代关系不级联废弃**：废弃某一物料 **不** 自动废弃同组其他物料

#### 10.2.1 替代料（ByCode）校验顺序（必须写死顺序）

`CreateReplacementByCode(base_material_code, ...)` 的校验顺序必须固定为（**顺序不可变更**）：

1. **base_material_code 是否存在**
2. **base item.status == 1**（否则 → **`ANCHOR_ITEM_DEPRECATED`**）
3. **category 是否存在**（`category_code` 不存在或已失效 → **`CATEGORY_NOT_FOUND`**；该校验**优先级高于 suffix 分配**）
4. （可选）规格合法性（格式/必填等）
5. **全部业务校验通过后**，才允许进入：**suffix 分配**

明确禁止：

* **在 suffix 分配前执行任何插入尝试**
* **在 suffix 分配前开启重试逻辑**

一句话冻结：**所有业务校验必须在 suffix 分配前完成。**

---

### 10.3 状态管理（必须实现）

字段定义：

* `status = 1`：active（正常）
* `status = 0`：deprecated（废弃）

行为约束：

* 禁止物理删除（仅允许把 `status` 置为 0）
* 默认搜索/检索/导出只返回 `status = 1` 的数据（除非显式选择“包含废弃”）
* **展示一致性（冻结）**：UI 列表、搜索结果、导出 Sheet 中 **`status` 语义必须一致**：`1` 展示为 **正常**，`0` 展示为 **已废弃**（同一套映射，禁止各层各写一套文案）
* **导出一致性**：Sheet1（全量）**含**废弃行；分类 Sheet **仅** `status = 1`；均须带状态列或与 **7.4** 一致

废弃语义（必须明确，冻结）：

* **废弃 = 生命周期终止**，不代表记录删除；废弃后记录仍可查询用于追溯
* **不允许“复活/恢复”旧记录** 来替代新建（即不允许把 `status=0` 直接改回 `status=1` 以绕过新建流程）
* 废弃后：
  * **`code` 永不释放**（全局唯一包含废弃）
  * **`spec` 可再次使用**：仅允许通过“新建物料”产生 **新记录/新 code**；历史废弃记录保持不变不覆盖

#### 【废弃与替代（V1.3 补充）】

* **不级联**：废弃仅影响当前 `material_item` 行，不级联修改同组其他行
* **基准限制**：**已废弃物料不得作为新增替代料的基准**（返回 **`ANCHOR_ITEM_DEPRECATED`**）

#### 【错误数据处理规则】

当 **spec** 或 **description** 录入错误需要纠正时：

1. **不允许修改 spec**（规格号不可变，见 **10.4**）
2. **不允许删除物料**（禁止物理删除）
3. **必须**执行：
   * 将原物料标记为 `status = 0`（废弃）
   * **新建**正确物料（生成**新**编码，新流水号/新组按业务走主料或替代料流程）

补充说明：

* **原物料编码（code）不可复用**（纠正须用新编码）
* **废弃数据不参与 spec 唯一性**：`SPEC_DUPLICATE` 仅由 `status=1` 数据触发；历史废弃记录仅用于追溯与提示（见 **10.1 / 9.1**）

---

### 10.4 spec 是否允许修改（必须定稿）

* `spec` **不允许修改**
* 修改规格号 = 新建物料
* 原物料可按需设置 `status = 0（废弃）` 保留追溯

### 10.5 V1 与 V2 能力边界（闭环优先）

#### V1（必须交付）

* 创建物料（A / B-Z）与替代料体系（A-Z 连续规则保持不变）
* **规格号 spec 唯一（仅启用态）**：`UNIQUE(category_code, spec) WHERE status = 1`
* **检索**：编码两阶段（前缀→模糊，见 **7.3.1 / 15.3.1**）；规格检索为 **LIKE 子串包含**（见 **6.4.5**），**LIMIT 20**
* **人工判断替代关系**：系统只提供候选列表与跳转替代料流程，不做自动判定

#### V2（标注，不在 V1 实现）

* 规格结构化落库（`material_attribute`）与参数级计算
* 单位归一、语义解析、复杂 token
* 相似度算法、智能推荐替代料、向量检索等

---

### 10.6 V1 成功标准（验收）

1. 输入**规格号（spec）**可通过搜索定位到物料（LIKE 命中即可）
2. 可通过搜索/候选列表将物料加入替代组（人工确认）
3. 可创建新主料（A 件）并生成唯一编码

## 十一、技术实现建议

### 11.1 数据库

* SQLite（本地）

**SQLite 并发能力说明（必须知晓）**

* SQLite 采用**单写者**模型（写操作串行化，实质为单写锁语义）
* 本系统仅支持**低并发写入**；并发冲突通过 **`UNIQUE` 约束 + 事务级重试**兜底
* **重试仅用于偶发冲突**，**不适用于**高并发批量写入场景

### 11.2 相似度实现

V1 约束：

* **不实现**复杂相似度；统一使用 **LIKE 子串包含**（见 **6.4.5**）
* 候选列表仅辅助人工判断；spec 唯一性以 **启用态 `status=1` 数据集**为准（`SPEC_DUPLICATE` 仅由启用态重复触发）

### 11.3 性能建议

* 数据量 < 1 万：SQLite LIKE + 索引通常可接受
* 规格搜索固定 **LIMIT 20**；避免全表扫描过大结果集
* 后续扩展（V2）：FTS/向量等（不在 V1 范围）

---

### 11.4 【对齐PRD新增】备份与恢复的实现约束（对齐 7.6）

本节为 **7.6** 的落地约束说明，目的仅为保证 PRD 语义可实现，不引入 PRD 未要求的复杂机制。

#### 11.4.1 PRD 约束 → 技术落地映射（数据安全三原则）

* **数据必须可恢复** → 多版本备份（自动备份 + 手动导出）+ 文件级恢复（覆盖式替换）
* **备份必须可用** → 一致性导出机制（导出得到某一确定时刻的完整数据库文件）+ 轻量校验（文件存在/可读/非零；必要时 SQLite 可打开性校验）
* **恢复不得破坏系统** → 原子替换语义（失败不动当前库）+ 恢复前校验 + 恢复后强制重启语义（确保进程内连接/缓存状态与已恢复数据一致）

#### 11.4.2 行为语义对齐（必须）

* **手动导出**：用户选择路径输出；失败不阻断；导出文件必须“完整可恢复”
* **启动自动备份**：启动后触发、静默、失败不阻断、保留多版本并按固定策略清理
* **数据恢复**：覆盖当前库；成功后必须提示并要求重启；失败不得破坏当前库

---

## 十二、风险与对策

### 风险1：误判候选命中（LIKE）

对策：

* 仅提示，不强制阻止创建

### 风险2：性能问题

对策：

* 固定 **LIMIT 20** 与 `category_code` 过滤
* 必要索引见 DDL（`code`、`category_code+spec_normalized` 等）

### 风险3：规格号/描述不规范

对策：

* V1：依赖人工录入与检索；V2 通过结构化字段解决

---

## 十三、未来扩展（V2）

* 参数结构化
* 智能相似度（Embedding）
* 自动推荐替代料
* Web 系统

---

## 十四、V2结构化字段预留设计（必须新增）

本章目标：在不推翻 V1 数据模型的前提下，为 V2 “规格结构化”能力预留可平滑升级的落库方案。

**V1 边界（强制）**：允许**建表**，但 **V1 业务不启用** `material_attribute` 写入与结构化解析；V1 仅使用 `spec` + `description` + `spec_normalized(搜索辅助)`。

### 14.1 新表：结构化属性表（material_attribute）

引入新表（用于“规格结构化存储”）：

```sql
material_attribute (
    id,
    material_item_id,
    attr_key,
    attr_value
)
```

关键说明（评审要点）：

* 该表**不是** `description` 的扩展
* 该表用于 V2 把规格**结构化**后的键值落库（系统可理解、可计算、可检索）；**V1 不写入**

### 14.2 三类字段的职责边界（必须明确）

* `spec`：**规格号（供应商型号）**（用户输入，保留原貌；分类内唯一）
* `description`：**完整规格字符串**（V1 手动输入；展示与生成 `spec_normalized` 的来源）
* `material_attribute`：**V2 结构化键值**（V1 不启用写入）

description 的未来定位：

* V1：手动输入（完整规格字符串）
* V2：可由 `material_attribute` 自动生成展示文本（可选）

### 14.3 V1.2阶段的结构化标识（必须落库）

在 `material_item` 中保留字段：

* `spec_normalized`：**仅 V1 搜索辅助**（基于 `description` 生成；**不参与唯一性**）
* `is_structured`：标识是否已完成结构化（0=否 1=是；**V1 默认 0**）

当前版本（V1）范围（必须明确）：

* 不实现 spec/ description 的结构化解析
* 不向 `material_attribute` 写入业务数据（表可空建）

补充说明（必须写）：

* `spec_normalized` 与 `material_attribute` **无混用关系**：前者是 V1 搜索辅助；后者是 V2 语义结构化

### 14.4 V1 → V2 数据升级策略（必须新增）

原则：

* 不允许全自动转换（避免误解析污染数据）
* 必须采用：解析 + 人工确认

示例流程（工程可实现）：

```text
spec → 系统解析（生成候选attribute） → 用户确认/修改 → 写入 material_attribute → is_structured=1 →（可选）重新生成description
```

落地要点：

* V1 数据（`spec`、`description`）保持不变可用
* V2 在同一条 `material_item` 上补齐 `material_attribute`，实现平滑升级

---

## 十五、Service层设计（核心实现约束）

本章目标：把 V1.2 的核心流程固化为可直接编码的 Service 规格，避免开发“二次设计”，并保证唯一性/并发一致性。

---

### 15.1 MaterialCodeService（编码生成服务）

#### 15.1.1 新建主物料（A）

方法定义：

```text
CreateMaterialItemA(input)
```

输入字段：

* category_code
* spec
* name
* description
* brand

处理流程（必须严格按此实现）：

校验顺序（必须写死顺序）：

1. **category 是否存在**（`category_code` 不存在或已失效 → **`CATEGORY_NOT_FOUND`**；不得进入事务内创建流程）
2. （可选）spec/description/name 等入参合法性（必填/格式等）
3. 通过后才允许进入事务与后续插入（见下方“处理流程”）

1. 调用 `SpecNormalizationService.Normalize(description)` 生成 `spec_normalized`（V1 仅三步规则）
2. 开启事务（必须）
3. 执行 INSERT（不允许用相似度绕过唯一性）
4. 若触发 `UNIQUE(category_code, spec) WHERE status = 1` 冲突：返回错误 `SPEC_DUPLICATE`（阻止创建；历史废弃不触发）
5. 获取流水号（同分类）：

   ```text
   SELECT MAX(serial_no) FROM material_group WHERE category_id=?
   ```
6. `serial_no = (max_serial_no ?? 0) + 1`
7. 插入 `material_group`（`category_id`,`category_code`,`serial_no`）
8. 插入 `material_item`（suffix = 'A'）：

   * `code = category_code + serial_no(7位补零) + 'A'`
   * `material_item.category_id` **等于** 本事务内新建 `material_group.category_id`
   * `material_item.category_code` **等于** 本事务内新建 `material_group.category_code`（冗余字段一致性，见 **6.2 / 6.4.2**）
   * 写入 `spec`、`spec_normalized`、`is_structured=0` 及其它展示字段
9. 若插入阶段发生 `UNIQUE(category_id, serial_no)` 冲突（并发导致）：

   ```text
   回滚本次事务 → 重试（最多3次）
   ```
10. 提交事务
11. 若重试仍失败：返回错误 `CODE_CONFLICT_RETRY`

#### 15.1.1.1 新建主物料（A，Manual 手工输入 code）

适用范围（冻结）：

* 仅适用于 **Manual 模式**的“新建物料页面”
* **不修改** Auto 自动编码规则（Auto 仍按 **15.1.1** 执行）

输入字段（新增/差异）：

* `category_code`（表单选择）
* `code`（用户手工输入，必须符合格式）
* 其余字段同 **15.1.1**（`spec/name/description/brand`）

处理流程（必须严格按此实现，禁止复用 Auto 的 max(serial_no)+1）：

校验顺序（必须写死顺序）：

1. **category 是否存在**（`category_code` 不存在或已失效 → **`CATEGORY_NOT_FOUND`**）
2. **输入标准化**：对用户输入 `code` 执行 `trim()` 并统一转为大写后，再进入后续校验与解析
3. **code 格式校验**：必须匹配 `^[A-Z]{3}[0-9]{7}[A-Z]$`；否则 → **`CODE_FORMAT_INVALID`**
4. **code 解析**：
   * `parsed_category_code = code[0..2]`
   * `serial_no = int(code[3..9])`（**必须 >= 1**；`0000000` 视为非法 → **`CODE_FORMAT_INVALID`**）
   * `suffix = code[10]`（非 `A-Z` → **`SUFFIX_INVALID`**）
5. **分类一致性**：`parsed_category_code` 必须等于表单选择 `category_code`；否则 → **`CATEGORY_MISMATCH`**
6. **suffix 限制（新建主物料A）**：`suffix` 必须为 `'A'`；否则 → **`SUFFIX_INVALID`**
7. 生成 `spec_normalized = Normalize(description)`（V1 三步规则）
8. 开启事务（必须）
9. **code 唯一**：若触发 `UNIQUE(code)` → **`CODE_DUPLICATE`**
10. **spec 唯一（仅启用态）**：若触发 `UNIQUE(category_code, spec) WHERE status = 1` → **`SPEC_DUPLICATE`**（历史废弃不触发）
11. **按指定 serial_no 查找/创建 group（冻结）**：
    * 查询 `material_group`：`WHERE category_id = ? AND serial_no = ?`
      * 若存在 → 复用该 `group_id`
      * 若不存在 → 尝试插入 `material_group(category_id, category_code, serial_no)`（serial_no 取自 code 解析结果）
        * **并发规则（Manual，冻结）**：若命中 `UNIQUE(category_id, serial_no)`：
          * 重新查询该 `(category_id, serial_no)` 对应的 `material_group`
          * 取其 `group_id` 并继续后续流程（不视为业务失败）
12. 插入 `material_item`（使用用户输入 `code`，且 suffix='A'）
13. 提交事务

---

#### 15.1.2 新增替代料（B-Z）

方法定义：

```text
CreateMaterialItemReplacement(group_id, input)
```

输入字段：

* spec
* name
* description
* brand

处理流程（必须严格按此实现）：

校验顺序（ByCode 推荐入口，必须写死顺序；不得混写主物料校验）：

1. **base_material_code 是否存在**
2. **base item.status == 1**（否则 → **`ANCHOR_ITEM_DEPRECATED`**）
3. **category 是否存在**（`category_code` 不存在或已失效 → **`CATEGORY_NOT_FOUND`**；该校验**优先级高于 suffix 分配**）
4. （可选）规格合法性（格式/必填等）
5. **全部业务校验通过后**，才允许进入：suffix 分配与插入（事务内）

1. 开启事务（必须）
2. 查询当前最大 suffix（同 group）：

   ```text
   SELECT suffix
   FROM material_item
   WHERE group_id = ?
   ORDER BY suffix DESC
   LIMIT 1
   ```
3. 计算 `nextSuffix = (char)(maxSuffix + 1)`（ASCII+1）
4. 若 `nextSuffix > 'Z'`：回滚并返回错误 `SUFFIX_OVERFLOW`（禁止创建）
5. 调用 `SpecNormalizationService.Normalize(description)` 生成 `spec_normalized`（V1 仅三步规则）
6. 执行 INSERT（不允许用相似度绕过唯一性）
7. 若触发 `UNIQUE(category_code, spec) WHERE status = 1` 冲突：返回错误 `SPEC_DUPLICATE`（阻止创建；历史废弃不触发）
8. 插入 `material_item`（suffix = nextSuffix，code按规则生成；`category_code` 与所属 Group 一致，见 **6.4.2**）
9. 若发生 `UNIQUE(group_id, suffix)` 冲突：

   ```text
   回滚本次事务 → 重试（最多3次）
   ```
10. 提交事务
11. 若重试仍失败：返回错误 `CODE_CONFLICT_RETRY`

实现约束：

* suffix 生成与插入必须位于同一事务内，避免并发下跳号/重复

suffix 连续性强约束（必须补充）：

* suffix 只能由系统生成，禁止前端/调用方传入
* `nextSuffix = 当前最大 suffix + 1`
* **连续性判定（与 4.3 / 10.2 一致）**：在创建替代料前，对目标 `group_id` 查询 `min_suffix`、`max_suffix`、`count`，须满足 **`min_suffix = 'A'`** 且 **`ASCII(max_suffix) - ASCII(min_suffix) + 1 == count`**；若不满足 → **suffix 不连续**，必须禁止创建（不允许跳号/补洞）

---

### 15.2 SpecNormalizationService（规格归一化服务）

方法定义：

```text
Normalize(description) → spec_normalized
```

V1 规则（**唯一允许**，必须一致）：

1. 转大写  
2. 去首尾空格  
3. 多空格压缩为 1 个空格  

**禁止（V1）**：单位归一、数值合并、语义解析、复杂 token、基于 `spec_normalized` 的唯一性判断。

输出要求：

* `spec_normalized` **仅**用于搜索辅助（LIKE），不参与唯一性
* spec 唯一性的硬阻断仅来自 **`UNIQUE(category_code, spec) WHERE status = 1`**（`status=0` 仅保留历史，不触发 `SPEC_DUPLICATE`）

#### 15.2.1 伪代码（可实现级）

```text
Normalize(descriptionRaw):
  if descriptionRaw is null:
    return ""
  s = trim(descriptionRaw)
  s = ToUpperInvariant(s)
  s = CollapseSpaces(s)   # 将连续空白（含Tab）压缩为单个空格
  return s
```

---

### 15.3 MaterialSearchService（搜索服务）

#### 15.3.1 编码搜索

方法定义：

```text
SearchByCode(query)
```

Query Object（必须使用）：

```text
SearchQuery {
  CodeKeyword
  CategoryCode (optional)
  Limit
  Offset
}
```

实现约束（两阶段，必须）：

1. **前缀匹配**：`code LIKE 'xxx%'`
2. **补充模糊匹配**（当前缀结果不足时）：`code LIKE '%xxx%'`

* 返回 Top 20

返回字段（至少）：

* code
* name
* spec
* brand

---

#### 15.3.2 规格搜索（核心，V1）

方法定义：

```text
SearchBySpec(query)
```

实现方式固定为（必须实现，**V1：相似=包含**）：

```sql
WHERE category_code = ?
  AND status = 1
  AND (
        spec LIKE '%' || ? || '%'
     OR spec_normalized LIKE '%' || ? || '%'
      )
LIMIT 20;
```

禁止：

* 相似度百分比、编辑距离、NLP/Embedding、复杂分词

返回字段（至少）：

* code
* spec（规格号）
* description（规格描述）
* name
* brand

重要约束（必须强调）：

* 检索结果仅辅助人工判断；spec 唯一性仍以 **`UNIQUE(category_code, spec) WHERE status = 1`** 为准
* `spec_normalized` 相同：**仅提示**，不阻止创建（当 spec 不同）

---

### 15.4 事务与一致性约束

必须明确：

* `CreateMaterialItemA`：单事务（包含流水号生成、Group插入、Item插入）；**`name` 在与插入同一事务、同一连接内**从 `category` 读取并写入 `material_item.name` 快照
* `CreateMaterialItemReplacement` / **`CreateReplacementByCode`（V1.3 推荐入口）**：单事务（包含组解析、suffix 计算与插入）；须满足 **10.2** 并发与错误码口径
* 搜索：不使用事务

---

### 15.5 错误码定义

**口径说明（必须）**：错误码分为两类，实现与文档需区分使用场景：

* **业务错误码**：与物料创建（A）、替代料（B-Z）、`spec` 唯一性、`suffix` 连续性/上限、编码并发冲突重试等 **PRD V1 核心物料业务**直接相关；UI 应优先按业务码映射专项提示（见下表「UI建议」列）。
* **工程错误码**：见 **§15.5.2**，用于入参校验、资源不存在、分类重复、未预期内部失败等 **API/工程边界**；**不替代**业务错误码的语义，亦不要求与物料业务表同一套 UI 文案。

必须包含（**业务错误码**）：

* `CODE_FORMAT_INVALID`：Manual 手工输入 `code` 格式不满足 `^[A-Z]{3}[0-9]{7}[A-Z]$`
* `CATEGORY_MISMATCH`：Manual 手工输入 `code` 解析出的 `category_code` 与表单所选分类不一致
* `SUFFIX_INVALID`：Manual 手工输入 `code` 的后缀非法，或在“新建主物料A”场景后缀不为 `A`
* `CODE_DUPLICATE`：编码重复（Manual 手工输入 `code` 冲突，触发 `UNIQUE(code)`；该场景为确定性冲突，不适用重试）
* `SPEC_DUPLICATE`：规格号重复（同分类下 `status=1` 的 `spec` 冲突，触发 `UNIQUE(category_code, spec) WHERE status = 1`；**历史废弃记录不触发**）
* `CATEGORY_NOT_FOUND`：`category_code` 不存在或已失效（创建主物料时 category_code 无效；CreateReplacementByCode 过程中解析分类失败；**优先级高于 suffix 分配，必须先校验**）
* `SUFFIX_OVERFLOW`：**A-Z 后缀耗尽**（逻辑上无法再分配下一后缀，非并发重试问题）
* `SUFFIX_ALLOCATION_FAILED`：**后缀分配失败**（依赖 `UNIQUE(group_id, suffix)` + 事务级重试后仍冲突，**重试次数达到上限**，与全局 `code` 冲突区分）
* `SUFFIX_SEQUENCE_BROKEN`：**suffix 不连续（存在缺口）**；同一 `group_id` 下不满足 PRD 连续性判定时，**禁止创建替代料**（见 **4.3 / 10.2 / 15.1.2**）
* `CODE_CONFLICT_RETRY`：**全局编码 `code` 唯一**等冲突，事务级重试仍失败（超过 3 次）；**不用于**替代料 suffix 槽位重试耗尽（该场景使用 `SUFFIX_ALLOCATION_FAILED`）
* `ANCHOR_ITEM_DEPRECATED`：基准物料已废弃（`status = 0`），禁止以此创建替代料

业务错误码完整列表（**V1.3 冻结**）：

| 错误码 | 触发场景 | 是否阻断创建 | UI建议 |
|---|---|---:|---|
| CODE_FORMAT_INVALID | Manual 手工输入 `code` 不匹配 `^[A-Z]{3}[0-9]{7}[A-Z]$` | 是 | code 输入框红字提示“编码格式不正确” |
| CATEGORY_MISMATCH | Manual `code` 解析分类与当前选择分类不一致 | 是 | code 输入框红字提示“编码分类与当前选择分类不一致” |
| SUFFIX_INVALID | Manual `code` 后缀非法，或新建主物料A但后缀不为 `A` | 是 | code 输入框红字提示“编码后缀无效” |
| CODE_DUPLICATE | Manual 手工输入 `code` 已存在（触发 `UNIQUE(code)`） | 是 | code（物料编码）输入框红字提示“编码已存在” |
| SPEC_DUPLICATE | 同分类存在 `status=1` 的同 `spec`（触发 `UNIQUE(category_code, spec) WHERE status = 1`） | 是 | spec（规格号）输入框红字提示“该规格型号已存在（启用中），禁止重复创建” |
| CATEGORY_NOT_FOUND | `category_code` 不存在或已失效（创建主物料 / 替代料 ByCode 分类解析失败） | 是 | 弹窗/全局提示“分类不存在或已失效” |
| SUFFIX_OVERFLOW | 同组已连续占满 A–Z，无法再分配下一后缀 | 是 | 弹窗/全局提示“替代料已达上限” |
| SUFFIX_ALLOCATION_FAILED | 同组 suffix 并发分配，事务级重试（默认最多 3 次）仍失败 | 是 | 弹窗/全局提示“后缀分配失败，请重试”（文案可产品化） |
| SUFFIX_SEQUENCE_BROKEN | 同组 suffix 不连续（存在跳号/缺口），禁止新增替代料 | 是（仅替代料创建） | 弹窗/全局提示“suffix 不连续，禁止创建替代料”（文案可产品化） |
| CODE_CONFLICT_RETRY | 全局 `code` 等唯一约束冲突，重试超过 3 次 | 是 | 弹窗/全局提示“系统繁忙，请重试” |
| ANCHOR_ITEM_DEPRECATED | 基准物料已废弃仍尝试创建替代料 | 是 | 弹窗/全局提示“基准物料已废弃，无法添加替代料”（文案可产品化） |

**suffix 连续性错误码收敛（必须）**：凡属于「同组 suffix 不连续、不允许补洞、删除中间 suffix 后形成缺口」等 **同一 PRD 连续性判定** 的情形，Application **仅** 返回 `SUFFIX_SEQUENCE_BROKEN`（与 Domain `SuffixAllocator` 一致）。**不得**再使用或对外承诺下列曾用于校验 YAML/说明的别名：`SUFFIX_GAP_FORBIDDEN`、`SUFFIX_NO_GAP_FILL`、`SUFFIX_NO_REUSE`（上述语义一律并入 `SUFFIX_SEQUENCE_BROKEN`）。黑盒 spec（如 `PRD_V1.yaml`）应 **直接断言 Application 返回码**，**禁止**在 Validation 动作层对 suffix 相关错误做二次映射。与「已满 26 个后缀」相关的上限场景仍单独使用 **`SUFFIX_OVERFLOW`**；与「并发 suffix 槽位重试耗尽」相关场景使用 **`SUFFIX_ALLOCATION_FAILED`**（见 **15.6**）。

#### 15.5.2 工程错误码附录（非核心业务）

以下码用于 **参数校验、资源定位、分类维护、内部失败** 等，**不属于**上表「业务错误码」的创建阻断语义；实现层可统一返回，由 UI 按场景映射通用提示。

| 错误码 | 典型触发场景 | 说明 |
|---|---|---|
| VALIDATION_ERROR | 必填项缺失/格式不合法、分类编码不存在等 | 可字段级或全局提示，具体由调用场景决定 |
| NOT_FOUND | 目标资源不存在（如 group/item 查询为空） | 通用“未找到”类提示 |
| INTERNAL_ERROR | 未预期的约束/系统失败（不应向终端用户暴露堆栈） | 全局提示“系统错误，请稍后重试”等 |
| CATEGORY_CODE_DUPLICATE | 新建分类时 `category.code` 唯一冲突 | 分类对话框等场景 |
| CATEGORY_NAME_DUPLICATE | 新建分类时 `category.name` 唯一冲突 | 分类对话框等场景 |

---

### 15.6 并发控制模型（必须新增）

系统并发控制策略：采用“乐观并发 + 数据库唯一约束 + 重试”模型。

**SQLite 并发模型说明（必须写入实现说明）**

* SQLite 为**单写锁**导向的并发模型，多写易序列化等待
* 本系统仅支持**低并发写入**；冲突通过 **唯一约束 + 重试**处理
* **重试仅为冲突兜底**，不承诺高并发场景下的吞吐与实时性

并发能力声明（必须明确降级）：

* 本系统不支持高并发写入（仅轻量/低并发）

规则（必须遵循）：

1. 不使用应用层锁
2. 不使用 `SELECT FOR UPDATE`
3. 所有唯一性由数据库 `UNIQUE` 约束保证
4. 冲突处理：

   * 捕获 `UNIQUE` 冲突
   * **事务级重试**（回滚后重来；默认 **最多 3 次**）
   * 超过上限时按场景返回：
     * **替代料 suffix 槽位**争用（`UNIQUE(group_id, suffix)`）→ **`SUFFIX_ALLOCATION_FAILED`**
     * **全局 `code` 唯一**冲突：
       * **Manual（手工输入）**：确定性重复 → **`CODE_DUPLICATE`**
       * **Auto（系统生成）**：事务级重试耗尽 → **`CODE_CONFLICT_RETRY`**
     * 其他唯一约束 → **`CODE_CONFLICT_RETRY`**

适用场景（必须按此实现）：

* `serial_no` 生成（新建主物料A）
* `suffix` 生成（新增替代料B-Z）

实现要点：

* 重试必须以“事务级重试”为单位（回滚后重新读取快照并再插入）
* **禁止**将 suffix 重试耗尽与全局编码冲突混用同一错误码（见 **15.5**）

#### 15.6.1 suffix 分配的事务与重试语义（必须新增）

强制规则（必须/禁止）：

1. **单次尝试必须在单事务内完成**：
   - suffix 计算
   - 插入 `material_item`
2. 若发生唯一约束冲突（`UNIQUE(group_id, suffix)`），**允许进行外层重试**（重新开启新事务）。
3. 重试模型定义（必须按此实现）：

```text
for retry in N:
  BEGIN TRANSACTION
    计算 suffix
    尝试插入 material_item
  COMMIT
  若冲突 → ROLLBACK 并重试
```

4. 最大重试次数：当前冻结为 **3 次**；超过最大重试次数 → 返回 **`SUFFIX_ALLOCATION_FAILED`**；**不得**无限重试。
5. 禁止行为：
   - **不得在一个事务内循环尝试多个 suffix**
   - **不得绕过唯一约束进行手动分配**
6. 语义澄清（必须）：**CreateReplacementByCode 的“单事务”指单次分配尝试；在并发冲突场景下，允许通过多次事务重试实现最终成功。**

## 十六、Repository层设计（数据访问规范）

本章目标：消除 SQL 分散问题，明确数据访问职责边界，使 Service 层稳定调用并可直接生成 Repository 代码。

### 16.0 设计原则（必须遵循）

1. Repository 只负责数据访问（CRUD/查询），不包含业务流程与规则判断
2. 所有 SQL 必须集中在 Repository（可内置SQL常量/独立SQL文件，但不可散落在Service/UI）
3. Service 不允许直接写 SQL，只能调用 Repository 方法
4. Repository 必须支持事务协作（连接/事务对象由 Service 传入并控制 Begin/Commit/Rollback）

---

### 16.1 CategoryRepository

方法（必须提供）：

```text
GetByCode(code)
Exists(code)
Insert(category)
ListAll()
```

说明：

* `Insert` 需依赖数据库唯一约束（`code`/`name`），由Repository统一将唯一冲突映射为可识别异常/错误码（由Service决定如何提示）

---

### 16.2 MaterialGroupRepository

方法（必须提供）：

```text
GetMaxSerialNo(category_id)
Insert(group)
GetById(id)
```

关键SQL（必须实现）：

```sql
SELECT MAX(serial_no)
FROM material_group
WHERE category_id = ?
```

说明：

* `GetMaxSerialNo` 只做查询，不做“+1”等业务处理（由Service完成）

---

### 16.3 MaterialItemRepository（核心）

#### 16.3.1 写操作

```text
Insert(item)
```

约束：

* 仅负责执行 INSERT 与映射结果（如返回新id），不负责 suffix 计算、`description→spec_normalized`、检索策略等业务逻辑

---

#### 16.3.2 基础查询（必须拆清）

```text
GetByCode(code)

GetMaxSuffix(group_id)

GetBySpecNormalized(category_code, spec_normalized, limit, offset)

ExistsSpec(category_code, spec)  -- 精确唯一性校验（仅 spec）
```

关键SQL（必须给出并集中维护）：

获取最大 suffix（用于替代料生成）：

```sql
SELECT MAX(suffix)
FROM material_item
WHERE group_id = ?
```

suffix 连续性校验（必须新增，供 Service 调用）：

```sql
-- 目的：检查同一 group_id 下 suffix 是否从 A 开始连续无缺口
-- 约束：suffix 必须为单字符 A-Z
-- 判定：须满足 min_suffix = 'A' 且 (ASCII(max_suffix) - ASCII(min_suffix) + 1) = cnt
-- 若不满足 → suffix 不连续，禁止创建新替代料
SELECT
  MIN(suffix) AS min_suffix,
  MAX(suffix) AS max_suffix,
  COUNT(1)    AS cnt
FROM material_item
WHERE group_id = ?
  AND suffix >= 'A' AND suffix <= 'Z';
```

Service 用 `min_suffix`、`max_suffix`、`cnt` 在应用层按 **6.4.2 / 4.3 / 15.1.2** 公式验算（SQLite 无标准 `ASCII()` 时用字符序等价实现）。

---

#### 16.3.3 搜索（用于Service）

```text
SearchByCode(query)

SearchBySpecKeyword(category_code, keyword, limit, offset)
```

职责说明（必须严格分离）：

* `SearchBySpecKeyword` 只负责 DB 召回（LIKE 子串包含），不负责相似度算法
* `MaterialSearchService` 负责参数组装、两阶段编码搜索策略、以及 UI 需要的轻量排序（可选，仍不得引入相似度算法）

关键SQL（必须给出并集中维护）：

编码搜索（前缀/模糊均可复用同一SQL，通过参数决定pattern）：

```sql
SELECT code, name, spec, brand
FROM material_item
WHERE status = 1
  AND code LIKE ?
ORDER BY code
LIMIT ?
```

spec召回（V1定稿：相似=包含；keyword 同时匹配 spec 与 spec_normalized；必须带 category_code；必须 LIMIT=20）：

```sql
SELECT code, spec, description, name, brand
FROM material_item
WHERE status = 1
  AND category_code = ?
  AND (
        spec LIKE '%' || ? || '%'
     OR spec_normalized LIKE '%' || ? || '%'
      )
LIMIT 20;
```

spec 精确校验（唯一性检查，必须提供）：

```sql
SELECT 1
FROM material_item
WHERE category_code = ?
  AND spec = ?
LIMIT 1;
```

---

### 16.4 SQL职责边界（必须写清）

Repository 负责：

* SQL 执行
* 数据映射（Row → Entity/DTO）
* 基础CRUD与基础查询

Service 负责：

* 业务流程编排（事务、重试、编码生成）
* **`material_item.category_code` 与所属 `material_group.category_code` 写入时一致**（冗余字段由 Service 赋值校验，不依赖 DB 自动同步）
* spec_normalized 生成（调用 `SpecNormalizationService`，输入为 `description`）
* 规则校验（唯一性、suffix边界、重试次数）
* 搜索策略（编码两阶段、规格 LIKE Top20），**不实现**相似度算法

状态字段约束（必须统一口径）：

* 数据库字段：`status INTEGER NOT NULL DEFAULT 1`
* 语义：`1 = ACTIVE（正常）`，`0 = DEPRECATED（废弃）`
* Repository 层不得散落 `status = 1/0` magic number，必须使用语义封装（枚举或常量）

代码层枚举（规范，供实现对齐）：

```text
enum MaterialStatus {
  ACTIVE = 1,
  DEPRECATED = 0
}
```

---

### 16.5 事务协作（必须定义）

规则：

* Repository 不开启事务
* Service 控制事务（Begin / Commit / Rollback）
* Repository 方法需要支持接收（connection, transaction）或等效上下文对象

示例（工程级）：

```text
Service:
  BeginTransaction
    → MaterialGroupRepository.Insert(group)
    → MaterialItemRepository.Insert(item)
  Commit
```

---

### 16.6 返回对象规范（必须定义）

Repository 返回对象应满足：

* 内部写操作：返回 `id` 或完整实体（含 `id`）
* 搜索类方法：返回“轻量DTO”（避免返回大对象）

DTO示例：

```json
{
  "code": "ZDA0000001A",
  "spec": "xxx",
  "name": "xxx",
  "brand": "xxx"
}
```

---

### 16.7 扩展性要求（V2预留）

为 V2 预留：

* `material_attribute` 相关 Repository（后续扩展）
* 支持批量写入（用于“结构化升级”：用户确认后批量写入 attribute）

---

### 16.8 查询对象规范（强烈建议）

所有“搜索类接口”必须支持 Query Object，禁止仅用单一参数（如只传 `keyword`）导致未来扩展破坏接口。

要求：

* Repository 搜索方法与 Service 搜索方法均应接受查询对象（或等效结构）
* 必须支持分类过滤、分页与可控limit

Query Object（示例）：

```text
SearchQuery {
  CodeKeyword
  SpecKeyword
  CategoryCode (optional)
  Limit
  Offset
}
```

落地约束：

* `limit` 必须有上限（例如最大 50，默认 20）
* `offset` 用于分页（默认 0）

---

## 十七、测试要求（必须新增）

本章目标：定义必须具备的自动化测试，确保“spec（规格号）分类内唯一、spec_normalized（description 辅助）可用于 LIKE、轻量并发下不冲突”。

### 17.1 spec_normalized 测试（必须）

要求：

* 输入 description ` 10uF  16V ` → `spec_normalized` 为 `10UF 16V`（仅三步规则）
* 输入 description `abc` → `ABC`

### 17.2 规格搜索测试（必须，V1）

输入：

* keyword 命中 spec 子串或 description 归一后子串

期望：

* SQL 命中 **LIKE** 规则（见 **6.4.5**），`LIMIT 20`
* **不断言**相似度分数（V1 无相似度算法）

### 17.3 并发测试（必须）

并发创建：

* 主料（A）
* 替代料（B-Z）

验证：

* 不重复（依赖 `UNIQUE(category_id, serial_no)`、`UNIQUE(group_id, suffix)`、`UNIQUE(code)`）
* 冲突时能重试，超过3次返回 `CODE_CONFLICT_RETRY`

---

## 附录A：本次“工程一致性修正评估”最终输出（V1.2 修订补丁）

> 本附录用于评审与“进入 Cursor 自动编码阶段”的实现对齐，强制约束已全部收敛，无冲突。

### A1. 修正后的 DDL（完整SQL，SQLite 可直接执行）

```sql
-- 分类表
CREATE TABLE category (
    id          INTEGER PRIMARY KEY AUTOINCREMENT,
    code        TEXT NOT NULL UNIQUE,   -- ZDA/ZDB...
    name        TEXT NOT NULL UNIQUE,   -- 电阻/电容...
    created_at  TEXT DEFAULT CURRENT_TIMESTAMP
);

-- 物料主档（替代组）
CREATE TABLE material_group (
    id            INTEGER PRIMARY KEY AUTOINCREMENT,
    category_id   INTEGER NOT NULL,     -- 外键：Category.id（唯一关系字段）
    category_code TEXT NOT NULL,        -- 冗余：用于展示/导出/查询优化（非关系字段）
    serial_no     INTEGER NOT NULL,     -- 1,2,3...
    created_at    TEXT DEFAULT CURRENT_TIMESTAMP,

    UNIQUE(category_id, serial_no),
    FOREIGN KEY (category_id) REFERENCES category(id)
);

-- 物料实例（可采购物料）
CREATE TABLE material_item (
    id            INTEGER PRIMARY KEY AUTOINCREMENT,
    group_id      INTEGER NOT NULL,
    category_id   INTEGER NOT NULL,     -- 外键：Category.id（唯一关系字段）
    category_code TEXT NOT NULL,        -- 冗余：用于“分类内spec唯一”等约束/检索过滤

    code          TEXT NOT NULL UNIQUE, -- ZDA0000001A
    suffix        TEXT NOT NULL,        -- A-Z（系统生成）

    name          TEXT NOT NULL,
    description   TEXT NOT NULL,        -- 完整规格描述（必填）
    spec          TEXT NOT NULL,        -- 规格号/供应商型号（必填，分类内唯一）
    spec_normalized TEXT NOT NULL,      -- 由 description 生成，仅搜索辅助（不参与唯一性）
    brand         TEXT,

    status        INTEGER NOT NULL DEFAULT 1, -- 1=active（正常）0=deprecated（废弃），不可物理删除
    is_structured INTEGER DEFAULT 0,    -- 0=未结构化 1=已结构化（V2预留）
    created_at    TEXT DEFAULT CURRENT_TIMESTAMP,

    UNIQUE(group_id, suffix),

    FOREIGN KEY (group_id) REFERENCES material_group(id),
    FOREIGN KEY (category_id) REFERENCES category(id)
);

-- spec 唯一性（业务规则）：同分类 + 启用态(status=1) 唯一
CREATE UNIQUE INDEX ux_material_item_category_spec_active
ON material_item(category_code, spec)
WHERE status = 1;

-- 结构化字段（V2预留，V1可先建表不强依赖）
CREATE TABLE material_attribute (
    id              INTEGER PRIMARY KEY AUTOINCREMENT,
    material_item_id INTEGER NOT NULL,
    attr_key         TEXT NOT NULL,
    attr_value       TEXT NOT NULL,
    created_at       TEXT DEFAULT CURRENT_TIMESTAMP,
    FOREIGN KEY (material_item_id) REFERENCES material_item(id)
);

-- 索引（为检索服务）
CREATE INDEX idx_material_item_code ON material_item(code);
CREATE INDEX idx_material_item_spec ON material_item(spec);
CREATE INDEX idx_material_item_spec_normalized ON material_item(spec_normalized);
CREATE INDEX idx_item_category_spec_norm ON material_item(category_code, spec_normalized);
CREATE INDEX idx_material_item_group_id ON material_item(group_id);
CREATE INDEX idx_material_item_status ON material_item(status);
```

### A2. 修正后的 SpecNormalizationService（伪代码/可实现级）

以第 15.2.1 小节为准（宽松归一化：不用于输入合法性校验，不拦截用户输入）。

### A3. 修正后的 CreateMaterialItemA / Replacement 流程（仅关键差异）

**CreateMaterialItemA（A件）差异点：**

- **唯一性校验（仅启用态）**：仅以 `UNIQUE(category_code, spec) WHERE status = 1` 为硬阻断；删除任何 `spec_normalized` 唯一性阻断。
- **候选提示（V1）**：当 **LIKE 召回**命中或 `spec_normalized` 与某条相同但 **spec 不同**时：
  - 返回候选列表（Top20）
  - UI 提示：“是否作为替代料？”
  - **不阻止创建**

**CreateMaterialItemReplacement（B-Z）差异点：**

- **suffix 连续性强校验（必须）**：
  - `nextSuffix = maxSuffix + 1`
  - 须满足 `minSuffix='A'` 且 `ASCII(maxSuffix)-ASCII(minSuffix)+1 == count`，否则 **禁止创建**
  - suffix 只能系统生成，**不允许跳号/补洞**

### A4. 错误码完整列表（最终版）

见第 15.5 节“错误码完整列表（最终版，V1.2 收敛后）”。

### A5. 与原方案差异点（用于评审）

1. **唯一性规则调整（强制）**
   - 保留：同分类 + 启用态 spec 唯一（建议使用部分唯一索引 `UNIQUE(category_code, spec) WHERE status = 1`）
   - 删除：`UNIQUE(category_code, spec_normalized)`（从 DDL/约束口径/流程/提示文案中全部移除）
2. **spec_normalized 定位调整**
   - 从“唯一性字段”降级为“基于 description 的搜索辅助字段”；创建流程中候选提示**不阻断**
3. **搜索提示行为（V1）**
   - 统一口径：**LIKE 子串包含**（spec 或 spec_normalized）→ 候选列表（Top20）+ UI 二选一（作为替代料加入 / 强制新建物料），且不阻止创建
4. **suffix 连续性校验补齐（强制）**
   - 在替代料创建中增加“发现缺口即禁止创建”的硬规则，并补充了用于校验的 SQL 聚合口径（见 16.3.2）
5. **错误码补充（强制）**
   - 本轮收敛：spec_normalized 不承担合法性校验职责，不新增“格式非法拦截”类错误码
6. **SQL 优化（强制）**
   - 新增索引 `idx_item_category_spec_norm(category_code, spec_normalized)`
   - 规格查询口径收敛为：`WHERE status=1 AND category_code=? AND (spec LIKE ... OR spec_normalized LIKE ...)` 且 **LIMIT 20**
