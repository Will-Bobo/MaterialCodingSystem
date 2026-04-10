# 物料编码系统 PRD（V1.2 修正版）

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

   * **同分类内规格号（spec）唯一**：`UNIQUE(category_code, spec)`，冲突必须阻止创建（`SPEC_DUPLICATE`）
   * `spec_normalized` 仅用于搜索辅助，**不参与唯一性**；与 spec 重复相关的提示为人工判断辅助，不替代唯一性规则
4. 快速检索（编码 / 规格号 / 规格描述）
5. Excel 导出

---

## 三、系统范围

### ✔ 包含

* 物料创建（主物料）
* 替代料管理
* 分类管理
* 编码搜索（前缀/模糊匹配）
* 规格号 / 规格描述 的 **LIKE 子串搜索**（V1：相似=包含匹配，人工判断）
* Excel 导出

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
* suffix 生成必须在事务中完成；如遇并发冲突（唯一约束冲突）需重试

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
| name | string | 名称 |
| description | string | **规格描述**：用户输入的完整规格字符串（不做结构化解析）；用于展示与搜索 |
| spec | string | **规格号（供应商型号）**：用户识别物料的核心字段，原样保存；示例：`CL10A106KP8NNNC` |
| spec_normalized | string | **基于 description 生成的搜索辅助字段**（V1 仅转大写/去首尾空格/多空格压一）；不参与唯一性约束 |
| brand | string | 品牌 |
| is_structured | int | 是否已结构化（0=否 1=是） |

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
* 规格唯一性（分类内）：`UNIQUE(category_code, spec)`（完全重复 spec 禁止创建）

**规格唯一性补充说明（工程口径）**

* **spec（规格号）视为供应商定义的唯一样号标识**（型号 Part Number）。
* 在电子元件领域中，**同一型号通常不会被多个品牌复用**；同时 **brand** 字段存在命名不规范（大小写、别名、多语言等）问题。
* 因此系统唯一性设计为 **`UNIQUE(category_code, spec)`**，并明确：
  * **同一 spec 在系统中只允许存在一条记录**（`status=1` 或 `status=0` 均占用该约束，见废弃规则）；
  * **即使录入时 brand 不同，只要 spec 相同，也视为同一物料**（不允许另建一条）；
  * **brand 不参与唯一性约束**。

说明：

* 物料编码体系唯一性仍以 `code` 为主唯一标识（等价 `category_code + serial_no + suffix`）
* **spec = 规格号（供应商型号）**（同分类不允许重复）
* **spec_normalized = 基于 description 生成的搜索辅助字符串**（不参与唯一性约束）
* `UNIQUE(category_code, spec)` 冲突 → 必须阻止创建（错误码：`SPEC_DUPLICATE`）
* 由于数据不可物理删除：废弃数据仍占用唯一性（见“核心约束”）

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
    spec          TEXT NOT NULL,        -- 规格号/供应商型号（必填，分类内唯一）
    spec_normalized TEXT NOT NULL,      -- 由 description 生成，仅搜索辅助
    brand         TEXT,

    status        INTEGER NOT NULL DEFAULT 1, -- 1=active（正常）0=deprecated（废弃），不可物理删除
    is_structured INTEGER DEFAULT 0,    -- 0=未结构化 1=已结构化
    created_at    TEXT DEFAULT CURRENT_TIMESTAMP,

    UNIQUE(group_id, suffix),
    UNIQUE(category_code, spec),

    FOREIGN KEY (group_id) REFERENCES material_group(id),
    FOREIGN KEY (category_id) REFERENCES category(id)
);

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
* 唯一性仍以 **`UNIQUE(category_code, spec)`** 为准；`spec_normalized` 相同仅作列表提示，**不阻止创建**

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
   * **唯一性**：若同分类 **spec 已存在** → 禁止保存（`SPEC_DUPLICATE`）
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

规则：

* 每个分类一个 Sheet
* 字段：

```
编码 | 名称 | 规格描述 | 规格号 | 品牌
```

排序规则（必须修正）：

```text
ORDER BY category_code, serial_no, suffix
```

导出SQL（必须给出完整SQL，不可省略）：

```sql
SELECT mi.code, mi.name, mi.description, mi.spec, mi.brand
FROM material_item mi
JOIN material_group mg ON mi.group_id = mg.id
WHERE mi.status = 1
ORDER BY mg.category_code, mg.serial_no, mi.suffix
```

---

### 7.5 分类管理

支持：

* 新增分类
* 分类编码唯一
* 分类名称唯一

---

## 八、相似度与检索（V1 闭环）

### 8.1 V1 定稿：不实现复杂相似度算法

* V1 中“相似/可能重复”的统一含义：**字符串包含**（`LIKE '%keyword%'`），见 **6.4.5**
* **禁止**：编辑距离、学习型相似度、NLP、向量检索（归入 V2 能力边界，见 **10.5**）

### 8.2 未来扩展（仅标注，不在 V1 实现）

* 结构化后可做更智能的推荐与相似度（见 **十三/十四**）

## 九、页面交互设计（重点）

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
物料种类（分类）选择器 [ 下拉选择 ]  [新增分类]
----------------------------------
规格号(spec)     [            ]  （必填）
规格描述(desc)  [            ]  （必填）
----------------------------------
【候选物料列表（实时，LIKE，Top20）】
编码 | 规格号 | 规格描述 | 名称 | 品牌
----------------------------------
名称（展示名）
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

   * 生成 `spec_normalized = Normalize(description)`（V1 三步规则）
   * 候选列表**不得**作为唯一性依据
   * 若触发 `UNIQUE(category_code, spec)` 冲突：**禁止提交**并提示“规格号重复”（错误码：`SPEC_DUPLICATE`）
   * 若仅 `spec_normalized` 与某记录相同但 **spec 不同**：允许创建，并可提示“描述归一后相同，请确认是否升级料/替代料”（**不阻止创建**）

错误提示机制（必须结构化）：

* `SPEC_DUPLICATE`：显示在 **spec（规格号）** 输入框下（红色提示）
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

UI 结构（示意）：

```
----------------------------------
编码搜索框 [          ]
----------------------------------
候选列表：
编码 | 名称 | 规格号
----------------------------------
选中物料后（必须展示）：
Group编码（如 ZDA0000001）
当前已有 suffix 列表（A/B/C）
明确提示：
将创建下一个替代料：D
----------------------------------
填写替代料信息
----------------------------------
```

交互逻辑：

1. 输入编码 → 实时搜索（前缀/模糊）
2. 选择主料/组内任一物料 → 自动定位 Group
3. 展示 Group 信息与替代料预测信息（见上方“必须展示”）
4. 创建替代料（B-Z）

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
* **规格号唯一性（分类内）**：

  * **spec = 规格号（供应商型号）**，为用户识别物料的核心字段
  * 数据库唯一约束：**`UNIQUE(category_code, spec)`**
  * `spec_normalized` **不参与**唯一性约束（仅搜索辅助）
  * **spec 相同** → 禁止创建（`SPEC_DUPLICATE`）
  * **spec_normalized 相同但 spec 不同** → **仅提示**，不阻止创建

**规格唯一性补充说明（与 6.4.2 一致）**

* spec 视为供应商唯一样号；**`UNIQUE(category_code, spec)`** 表示同一分类下 **同一 spec 仅允许一条记录**；**brand 不参与唯一性**；不同品牌不得通过不同 `brand` 绕过同一 spec 的唯一性。

废弃数据与唯一性（关键决策，必须定稿）：

* 方案A（采用）：**废弃数据仍占用唯一性（不允许重复创建）**

唯一性范围说明：

* 因为“数据不可物理删除”，即使 `status = 0（废弃）`，其 `spec` 仍然占用唯一性（同分类 spec 禁止重复）
* 录入错误的处理方式是：废弃（status=0）保留追溯，而不是物理删除后重建

**spec_normalized（V1）**

* **输入来源**：仅允许基于 **`description`** 生成
* **职责**：搜索辅助（配合 `spec` 一起做 LIKE）；**不得**用于唯一性判断/数据库唯一约束/业务强制拦截
* **生成规则**：仅允许 **转大写 / 去首尾空格 / 多空格压一**（见 **6.4.3** 与 **15.2**）
* **禁止**：单位归一、数值合并、语义解析、复杂 token、把 `spec_normalized` 当“归一化后唯一”

---

### 【spec / description / spec_normalized 规则（V1 闭环）】

#### 1. spec（规格号）

* **定义**：规格号（供应商型号），示例：`CL10A106KP8NNNC`
* **定位**：用户识别物料的核心字段；**分类内唯一**（`UNIQUE(category_code, spec)`）
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
* 超过 Z 禁止创建
* suffix 生成必须在事务中完成；并发冲突需重试

---

### 10.3 状态管理（必须实现）

字段定义：

* `status = 1`：active（正常）
* `status = 0`：deprecated（废弃）

行为约束：

* 禁止物理删除（仅允许把 `status` 置为 0）
* 默认搜索/检索/导出只返回 `status = 1` 的数据（除非显式选择“包含废弃”）

#### 【错误数据处理规则】

当 **spec** 或 **description** 录入错误需要纠正时：

1. **不允许修改 spec**（规格号不可变，见 **10.4**）
2. **不允许删除物料**（禁止物理删除）
3. **必须**执行：
   * 将原物料标记为 `status = 0`（废弃）
   * **新建**正确物料（生成**新**编码，新流水号/新组按业务走主料或替代料流程）

补充说明：

* **原物料编码（code）不可复用**（纠正须用新编码）
* **废弃数据仍参与** `UNIQUE(category_code, spec)`（见 **10.1**）

---

### 10.4 spec 是否允许修改（必须定稿）

* `spec` **不允许修改**
* 修改规格号 = 新建物料
* 原物料可按需设置 `status = 0（废弃）` 保留追溯

### 10.5 V1 与 V2 能力边界（闭环优先）

#### V1（必须交付）

* 创建物料（A / B-Z）与替代料体系（A-Z 连续规则保持不变）
* **规格号 spec 唯一**：`UNIQUE(category_code, spec)`
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
* 候选列表仅辅助人工判断；唯一性仍以 **`UNIQUE(category_code, spec)`** 为准

### 11.3 性能建议

* 数据量 < 1 万：SQLite LIKE + 索引通常可接受
* 规格搜索固定 **LIMIT 20**；避免全表扫描过大结果集
* 后续扩展（V2）：FTS/向量等（不在 V1 范围）

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

1. 调用 `SpecNormalizationService.Normalize(description)` 生成 `spec_normalized`（V1 仅三步规则）
2. 开启事务（必须）
3. 执行 INSERT（不允许用相似度绕过唯一性）
4. 若触发 `UNIQUE(category_code, spec)` 冲突：返回错误 `SPEC_DUPLICATE`（阻止创建）
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
7. 若触发 `UNIQUE(category_code, spec)` 冲突：返回错误 `SPEC_DUPLICATE`（阻止创建）
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
* 唯一性冲突仅来自 **`UNIQUE(category_code, spec)`**

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

* 检索结果仅辅助人工判断；唯一性仍以 **`UNIQUE(category_code, spec)`** 为准
* `spec_normalized` 相同：**仅提示**，不阻止创建（当 spec 不同）

---

### 15.4 事务与一致性约束

必须明确：

* `CreateMaterialItemA`：单事务（包含流水号生成、Group插入、Item插入）
* `CreateMaterialItemReplacement`：单事务（包含suffix计算与插入）
* 搜索：不使用事务

---

### 15.5 错误码定义

必须包含：

* `SPEC_DUPLICATE`：规格号重复（同分类下 `spec` 冲突，触发 `UNIQUE(category_code, spec)`）
* `SUFFIX_OVERFLOW`：超过 Z（26个）禁止创建
* `CODE_CONFLICT_RETRY`：编码/唯一约束冲突重试失败（超过3次）

错误码完整列表（最终版，V1.2 收敛后）：

| 错误码 | 触发场景 | 是否阻断创建 | UI建议 |
|---|---|---:|---|
| SPEC_DUPLICATE | 同分类 `spec` 完全重复（触发 `UNIQUE(category_code, spec)`） | 是 | spec（规格号）输入框红字提示“规格号重复” |
| SUFFIX_OVERFLOW | 替代料 suffix 超过 Z | 是 | 弹窗/全局提示“替代料已达上限” |
| CODE_CONFLICT_RETRY | 并发/冲突导致重试超过 3 次 | 是 | 弹窗/全局提示“系统繁忙，请重试” |

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
   * 重试（最多3次）
   * 超过返回 `CODE_CONFLICT_RETRY`

适用场景（必须按此实现）：

* `serial_no` 生成（新建主物料A）
* `suffix` 生成（新增替代料B-Z）

实现要点：

* 重试必须以“事务级重试”为单位（回滚后重新读取最大值并再插入）

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
    UNIQUE(category_code, spec),

    FOREIGN KEY (group_id) REFERENCES material_group(id),
    FOREIGN KEY (category_id) REFERENCES category(id)
);

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

- **唯一性校验**：仅以 `UNIQUE(category_code, spec)` 为硬阻断；删除任何 `spec_normalized` 唯一性阻断。
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
   - 保留：`UNIQUE(category_code, spec)`
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
