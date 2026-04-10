# 物料编码系统（MCS）工程级技术设计文档（TDD V1.3）

> 约束说明：本文是“可指导开发/可自动化验证”的技术设计文档。所有关键约束以数据库 DDL + Service 契约 + 自动化测试用例固化，禁止模糊实现。

---

## V1.3 变更说明（必须存在）

本版本 **不改变任何 PRD 已定义的业务规则与 V1 默认行为**，仅补充：

1. **状态扩展策略（Status Evolution Policy）**
2. **规则策略模型（Rule Policy）**：为未来演进预留扩展点，但 **V1 固定为 STRICT/固定策略且不可修改**
3. **spec 唯一性演进约束**：明确未来若改变“废弃占用唯一性”的语义，必须通过版本升级 + DB Migration + 全量测试更新

目的：

- 提升系统可演进性
- 避免未来规则变更导致架构重构

V1.3 最终校验（必须满足）：

- 所有原有测试语义不变
- 所有业务流程不变
- 未引入任何“可运行时切换”的配置/UI 开关
- 文档中所有 Policy 明确标注：“**V1 固定，不可修改**”

---

## 0. 评审建议对齐评估（PRD + TDD 一致性）

> 本节目的：把评审建议逐条映射为“是否可直接纳入 TDD”的结论，并明确若与 PRD 冲突，必须同步修订 PRD 的具体章节，否则会违反“禁止改变 PRD 逻辑”。

| 评审建议 | 结论 | 与 PRD 关系 | TDD 更新动作（文档） | 若要采纳但冲突，PRD 必须同步修订点 |
|---|---|---|---|---|
| 分层必须为 Presentation → Application → Domain → Infrastructure（禁止 Service 独立层） | **可纳入** | PRD 已要求分层边界，但 TDD 当前表述为“Service 层”独立存在 | 将“Service 层”归入 Domain（Domain Services），更新依赖方向、职责边界与术语 | 无（属表述与边界收敛，不改变业务） |
| spec 不可变，不提供 Update 接口 | **可纳入（已覆盖）** | PRD 已明确 spec 不允许修改 | 补充“契约层禁止 UpdateSpec 用例”与测试断言（接口不存在） | 无 |
| UNIQUE(category_code, spec) 必须 DB 强制 + Domain/Service 校验 + 错误码 | **可纳入（已覆盖）** | PRD 已明确 | 增加“统一错误返回结构 ErrorCode+Message”，将 SPEC_DUPLICATE 输出格式固化 | 无 |
| suffix 规则：suffix 不强制连续；suffix 冲突禁止；suffix 自动生成 | **与 PRD 冲突** | PRD 明确“suffix A-Z 必须连续，发现缺口禁止创建” | TDD **不能**单方面改为“不连续允许”，否则违反“禁止改变 PRD 逻辑” | 必须同步修订 PRD：`10.2 替代规则`、`15.1.2 CreateMaterialItemReplacement`、`附录A3` 的“连续性强校验/缺口禁止”条款 |
| spec_normalized 金样例要求小写：Normalize("  ABC-123 ") == "abc123" | **与 PRD 冲突** | PRD 规范与示例为“转大写”口径 | TDD 保持 PRD“转大写”输出，并提供 PRD 口径金样例（例如 "ABC123"） | 若要采纳“小写输出”，必须同步修订 PRD：`10.1 spec_normalized 生成规则`、`15.2 SpecNormalizationService` 伪代码与示例 |
| 相似度/编辑距离等算法 | **与 PRD 冲突** | PRD V1 明确：规格搜索“相似=包含（LIKE 子串包含）”，禁止相似度百分比、编辑距离、NLP/Embedding | TDD 必须移除“相似度计算/阈值/排序”要求，统一为 LIKE 候选列表（Top20） | 若要引入相似度百分比/编辑距离，必须先修订 PRD（V2 范围） |
| DB 必须包含：主键、唯一索引、status、创建/更新时间 | **部分缺失（可纳入）** | PRD DDL 仅含 created_at（无 updated_at） | 在 TDD 表结构补充 `updated_at` 字段要求与维护规则（由 Infra 负责） | 若要求 PRD 作为最终 DDL 源，则需同步修订 PRD DDL |
| 索引至少包含 (category_code, status) | **可纳入（补强）** | PRD 现有 status 与 category+spec_norm 索引，但无 category_code+status 复合索引 | 增加 `idx_item_category_status(category_code, status)` 并注明用于默认过滤 | 若要求 PRD DDL 同步，则需修订 PRD 索引清单 |
| 错误必须统一结构：ErrorCode + Message | **可纳入（补强）** | PRD 有错误码但未固化统一错误载体结构 | 增加统一错误响应 Envelope（Application 对 UI 返回） | 无 |
| TDD 红绿重构 + 每任务 build/test + 自检报告模板 | **可纳入（补强）** | PRD 有测试要求但未定义执行节奏与自检输出格式 | 增加“任务拆解/逐任务实现/每任务验证/自检报告模板” | 无 |

---

## PRD 规则优先级（强制）

1. **所有业务规则必须来源于 PRD**（`docs/MCS_PRD_V1.md` 为唯一真源）
2. TDD 仅允许：
   - 细化实现细节（不改变 PRD 行为）
   - 补充工程约束（例如：一致性约束、索引、错误结构）
   - 增加测试设计（使规则可验证）
3. 严格禁止：
   - 修改/覆盖 PRD 的业务规则与行为
   - 在 TDD 中引入 PRD 未定义的新业务规则
4. 若发现 PRD 与评审建议、或 PRD 与 TDD 存在冲突：
   - **必须在 TDD 中显式标记冲突**
   - **必须等待 PRD 修订**
   - **禁止在 TDD 中直接“改规则落地”**

---

## 1. 系统总体架构

### 1.1 架构图（文字描述即可）

```
WPF UI（Presentation）
  ↓ 只调用用例接口（禁止 SQL/业务规则）
Application / Controller（用例编排）
  ↓ 只编排流程 + 统一错误码（禁止 SQL/三方实现耦合）
Domain（核心业务：实体/值对象/领域规则 + Domain Services）
  ↓ 只依赖接口抽象（Repository/Exporter/Similarity/Logger）
Infrastructure（SQLite Repository + 导出适配器 + 相似度适配器 + 日志）
```

### 1.2 系统分层定义（强约束）

- **Presentation（WPF UI）**
  - **负责**：页面交互、输入基础校验（必填/长度/枚举）、调用 Application、展示数据与错误
  - **不负责**：SQL、编码生成、唯一性裁决、并发重试、状态流转规则、相似度算法选择
- **Application / Controller**
  - **负责**：用例编排（CreateA / CreateReplacement / Search / Export / Deprecate / Category CRUD）、**事务边界**、**并发重试策略**、输入校验、统一错误码映射与返回结构（ErrorCode + Message）
  - **不负责**：直接访问数据库实现、直接依赖第三方库实现（必须经接口抽象）
- **Domain**
  - **负责**：**纯业务规则（纯逻辑）**：编码规则、suffix 规则、唯一性业务判断、状态规则、spec 不可变、spec_normalized 归一化规则
  - **禁止**：事务、重试、DB 操作、Repository 调用、IO/第三方耦合
- **Infrastructure**
  - **负责**：Repository（SQLite 实现）、事务/连接管理、Excel 导出、相似度算法适配、日志实现
  - **不负责**：业务流程判断（Repository 禁止业务规则）

### 1.3 模块划分

- **M01 分类管理（Category）**
- **M02 物料建档（Create A / Create Replacement）**
- **M03 搜索（Code Search / Spec Search + 相似提示）**
- **M04 Excel 导出（Export）**
- **M05 状态管理（Deprecated）**
- **M06 规格归一化（SpecNormalization）**
- **M07 相似度提示（Similarity Hint，仅提示不阻断）**
- **M08 V2 结构化预留（material_attribute + is_structured）**

### 1.4 模块之间依赖关系（禁止跨层调用）

- 依赖方向：`UI → Application → Domain → Infrastructure`
- **禁止**：
  - UI 直接调用 Repository/SQLite
  - Domain 直接引用 Excel/相似度/日志具体实现（必须依赖接口）
  - Repository 写业务规则或跨库创建编码/重试逻辑

---

## 2. 模块拆解（逐模块）

### M01 分类管理模块（Category）

#### 2.1 模块职责（明确边界）

- 维护分类：新增、按 code 查询、列表展示
- 依赖数据库唯一约束保证分类稳定性：`category.code` 唯一、`category.name` 唯一

#### 2.2 不负责什么（必须写）

- 不负责物料编码生成
- 不负责 spec 唯一性、suffix 连续性与并发重试
- 不负责导出与相似度提示

#### 2.3 核心数据模型（字段 + 类型 + 约束）

- `Category`
  - `id: int`（主键，自增）
  - `code: string`（NOT NULL，UNIQUE，示例：ZDA）
  - `name: string`（NOT NULL，UNIQUE，示例：电阻）
  - `created_at: string(datetime)`（DEFAULT CURRENT_TIMESTAMP）

#### 2.4 核心接口（输入 / 输出 / 异常）

- `CreateCategory(input)`
  - **输入**：`{ code: string, name: string }`
  - **输出**：`{ id: int, code: string, name: string }`
  - **异常/错误码**
    - `CATEGORY_CODE_DUPLICATE`：触发 `UNIQUE(category.code)`
    - `CATEGORY_NAME_DUPLICATE`：触发 `UNIQUE(category.name)`
    - `VALIDATION_ERROR`：code/name 为空或超长
- `ListCategories() -> Category[]`
- `GetCategoryByCode(code: string) -> Category | null`

#### 2.5 核心业务流程（正常 + 异常）

- **正常流程**
  1. Application 校验 code/name（非空、长度、字符集）
  2. Service 调用 `ICategoryRepository.Insert`
  3. 返回新建分类
- **异常流程**
  - Repository 捕获 UNIQUE 冲突并映射为 `CATEGORY_CODE_DUPLICATE` / `CATEGORY_NAME_DUPLICATE`

#### 2.6 对外依赖（第三方 / 其他模块）

- `ICategoryRepository`（Infra 提供 SQLite 实现）
- SQLite UNIQUE 约束（可用集成测试验证）

---

### M02 物料建档模块（新建主物料 A / 新增替代料 B-Z）

#### 2.1 模块职责（明确边界）

- 新建主物料（A）：创建 `MaterialGroup` + `MaterialItem(A)`
- 新增替代料（B-Z）：在既有 `group_id` 下创建 `MaterialItem(B..Z)`
- 强制执行（硬约束，可自动化验证）：
  - 分类内 spec 唯一：`UNIQUE(category_code, spec)`（硬阻断）
  - 同组 suffix 唯一：`UNIQUE(group_id, suffix)`
  - 编码唯一：`UNIQUE(code)`
  - suffix 连续性强约束：A→B→C…（发现缺口禁止创建）
  - 并发冲突：事务级重试最多 3 次，失败 `CODE_CONFLICT_RETRY`

#### 2.2 不负责什么（必须写）

- 不负责“相似提示是否阻断创建”：**相似度仅提示，永不阻断**
- 不负责 spec 格式合法性拦截（不新增“格式非法”阻断类错误码）
- 不负责物理删除（仅允许 status=0 废弃）

#### 2.3 核心数据模型（字段 + 类型 + 约束）

- `MaterialGroup`
  - `id: int`（PK 自增）
  - `category_id: int`（NOT NULL，FK→`Category.id`；**唯一关系字段**）
  - `category_code: string`（NOT NULL，冗余字段：展示/导出/检索过滤；**非关系字段**）
  - `serial_no: int`（NOT NULL）
  - `created_at: string(datetime)`（DEFAULT）
  - 约束：`UNIQUE(category_id, serial_no)`
- `MaterialItem`
  - `id: int`（PK 自增）
  - `group_id: int`（NOT NULL，FK→`material_group.id`）
  - `category_id: int`（NOT NULL，FK→`Category.id`；与所属 Group 强一致）
  - `category_code: string`（NOT NULL，**冗余字段**，一致性强约束见下）
  - `code: string`（NOT NULL，UNIQUE，格式：`category_code + serial_no(7位补零) + suffix`）
  - `suffix: string(char)`（NOT NULL，A-Z，**只能系统生成**）
  - `name: string`（NOT NULL）
  - `description: string`（NOT NULL，完整规格描述；V1 必填）
  - `spec: string`（NOT NULL，原样保存，分类内唯一）
  - `spec_normalized: string`（NOT NULL，仅用于搜索辅助；**必须由 description 生成**）
  - `brand?: string`
  - `status: int`（NOT NULL，默认 1；1=active，0=deprecated；**不可物理删除**）
  - `is_structured: int`（默认 0；V2 预留）
  - `created_at: string(datetime)`（DEFAULT）
  - 约束：
    - `UNIQUE(code)`
    - `UNIQUE(group_id, suffix)`
    - `UNIQUE(category_code, spec)`（硬阻断；废弃仍占用）

### 状态扩展策略（Status Evolution Policy）

- V1（当前版本）：
  - `status` 固定为 **INTEGER**（0=deprecated，1=active）
  - DB 强制约束：`CHECK(status IN (0, 1))`

- V2+（未来扩展约束）：
  - 若新增状态（例如：archived、draft），必须：
    1. 修改 Domain 枚举 `MaterialStatus`
    2. 执行 DB Migration（重建表或调整 CHECK 约束）
    3. 更新所有相关测试用例（状态过滤/搜索/导出）

- 强制约束：
  - 禁止将 `status` 改为 TEXT 类型
  - 禁止 UI 文案直接入库
  - 所有状态语义必须由 Domain 枚举定义

- 结论：
  - 当前为“受控扩展模型”，允许演进但必须通过 Migration

**category_code 冗余字段一致性约束（必须补充）**

- 系统内部关联以 `category_id` 为准；`category_code` 仅作冗余字段（PRD 定稿）
- `material_group.category_id` 必须存在于 `Category.id`
- `material_group.category_code` 必须与 `category_id` 映射一致（由 Application 在写入路径保证）
- `material_item.category_id` **必须等于**其所属 `material_group.category_id`
- `material_item.category_code` **必须等于**其所属 `material_group.category_code`
- **禁止外部输入**：任何 Create 用例请求 DTO 均不得包含 `category_code`（Replacement 场景）
- 赋值责任：
  - Create A：Application 根据输入的 `category_code` 查询 `Category.id`，在单事务内写入 `material_group(category_id, category_code, serial_no)` 与 `material_item(category_id, category_code, ...)`
  - Create Replacement：Application 在事务内读取 `material_group(category_id, category_code)`，并写入 `material_item(category_id, category_code, ...)`
- 一致性要求：必须在**同一事务**内写入，保证强一致

#### 2.4 核心接口（输入 / 输出 / 异常）

- `CreateMaterialItemA(input)`
  - **输入**：`{ category_code: string, spec: string, name: string, description?: string, brand?: string }`
  - **输出**：
    - `group: { id: int, category_code: string, serial_no: int }`
    - `item: { id: int, code: string, suffix: "A", spec: string, spec_normalized: string, status: 1, is_structured: 0 }`
    - `candidates: MaterialCandidate[]`（Top 20，仅提示）
  - **错误码（阻断创建）**
    - `SPEC_DUPLICATE`：触发 `UNIQUE(category_code, spec)`
    - `CODE_CONFLICT_RETRY`：事务级重试超过 3 次
    - `VALIDATION_ERROR`：输入校验失败
- `CreateMaterialItemReplacement(group_id, input)`
  - **输入**：`group_id: int` + `{ spec: string, name: string, description?: string, brand?: string }`
  - **输出**：
    - `item: { id: int, code: string, suffix: "B".."Z", ... }`
    - `candidates: MaterialCandidate[]`
  - **错误码（阻断创建）**
    - `SUFFIX_OVERFLOW`：超过 Z（26个）禁止创建
    - `SUFFIX_SEQUENCE_BROKEN`：suffix 集合不连续（缺口存在）禁止创建
    - `SPEC_DUPLICATE`
    - `CODE_CONFLICT_RETRY`
    - `NOT_FOUND`：group 不存在
    - `VALIDATION_ERROR`

Replacement 输入约束（必须明确）：

- Replacement 请求 **不得包含** `category_code`（禁止外部输入冗余字段）
- Replacement 的 `material_item.category_code` 必须由 Application 在事务内从 `material_group.category_code` 推导并写入

（V1）候选提示以 `MaterialCandidate[]` 呈现（Top 20；LIKE 子串包含）

#### 2.5 核心业务流程（正常 + 异常）

##### 2.5.1 新建主物料（A）流程（必须严格实现）

- **正常流程**
  1. `spec_normalized = SpecNormalizationService.Normalize(description)`（V1 三步规则：转大写/去首尾空格/多空格压一）
  2. Application Begin Transaction（事务边界必须在 Application）
  3. 插入需依赖 `UNIQUE(category_code, spec)`（不得用相似度绕过）
  4. 读取 `category_id = CategoryRepository.GetIdByCode(category_code)`（关系以 category_id 为准）
  5. 读取 `maxSerialNo = MaterialGroupRepository.GetMaxSerialNo(category_id)`
  6. `serial_no = (maxSerialNo ?? 0) + 1`
  7. 插入 `material_group(category_id, category_code, serial_no)`
  8. `code = category_code + serial_no(7位补零) + "A"`
  9. 插入 `material_item`（写入 `group_id/category_id/category_code`，suffix="A"，spec/description/spec_normalized/status=1/is_structured=0）
  10. Application Commit
  11. 返回创建结果 + 候选提示（Top 20，仅提示）

- **异常流程**
  - `UNIQUE(category_code, spec)` 冲突 → 回滚 → `SPEC_DUPLICATE`（阻断）
  - `UNIQUE(category_id, serial_no)` 冲突（并发）→ 回滚事务 → 重试（最多 3 次）
    - 超过 3 次 → `CODE_CONFLICT_RETRY`（阻断）

serial_no 连续性口径（必须明确，消除并发歧义）：

- `serial_no` **不保证连续**（允许跳号）
- `serial_no` 仅保证：
  - 同 `category_id` 下唯一（由 `UNIQUE(category_id, serial_no)` 强制）
  - 单调递增趋势（基于 max+1 的生成策略；并发下可能产生间隔）

##### 2.5.2 新增替代料（B-Z）流程（必须严格实现）

- **正常流程**
  1. Application Begin Transaction（事务边界必须在 Application）
  2. 查询同组最大 suffix：
     - `SELECT suffix FROM material_item WHERE group_id=? ORDER BY suffix DESC LIMIT 1`
  3. `nextSuffix = maxSuffix + 1`
  4. 若 `nextSuffix > "Z"` → 回滚 → `SUFFIX_OVERFLOW`
  5. suffix 连续性校验（不改变 PRD，实施优化）：
     - 主路径：仅基于 `maxSuffix` 生成 `nextSuffix = maxSuffix + 1`
     - 防御性检查（避免 O(n) 全量扫描成为核心逻辑）：
       - 在同一事务内查询 `count = COUNT(*) WHERE group_id=?`
       - 计算 `expected = (maxSuffix - 'A' + 1)`
       - 若 `count != expected`：说明存在缺口或异常数据 → 回滚 → `SUFFIX_SEQUENCE_BROKEN`
     - 最终一致性：并发下以 `UNIQUE(group_id, suffix)` 为最终仲裁；冲突走重试
  6. `spec_normalized = Normalize(description)`（V1 三步规则）
  7. 插入 `material_item(nextSuffix)`
  8. 并发冲突：`UNIQUE(group_id, suffix)` → 回滚事务 → 重试（最多 3）
     - 超过 3 次 → `CODE_CONFLICT_RETRY`
  9. Application Commit
  10. 返回创建结果 + 相似提示（仅提示）

- **异常流程**
  - `UNIQUE(category_code, spec)` 冲突 → `SPEC_DUPLICATE`（阻断）
  - `group_id` 不存在 → `NOT_FOUND`

#### 2.6 对外依赖（第三方 / 其他模块）

- `ISpecNormalizationService`
- `IMaterialGroupRepository`、`IMaterialItemRepository`
- SQLite（唯一性/并发正确性可自动化验证）

---

### M03 搜索模块（编码搜索 / 规格搜索）

#### 2.1 模块职责（明确边界）

- 编码搜索：前缀/模糊匹配
- 规格搜索：固定为 `LIKE` 检索（禁止复杂分词/NLP/Embedding）
- 默认过滤：仅返回 `status=1`（除非 includeDeprecated=true）

#### 2.2 不负责什么（必须写）

- 不负责创建与唯一性裁决
- 不负责复杂分词/Embedding/NLP（明确禁止）

#### 2.3 核心数据模型（字段 + 类型 + 约束）

- `SearchQuery`
  - `CodeKeyword?: string`
  - `SpecKeyword?: string`
  - `CategoryCode?: string`
  - `IncludeDeprecated: bool`
  - `Limit: int`（默认 20，最大 50）
  - `Offset: int`（默认 0）

#### 2.4 核心接口（输入 / 输出 / 异常）

- `SearchByCode(query: SearchQuery) -> PagedResult<MaterialItemSummary>`
  - 规则：`LIKE 'xxx%'` 或 `LIKE '%xxx%'`
  - 默认返回 Top 20（受 Limit 上限控制）
- `SearchBySpec(query: SearchQuery) -> PagedResult<MaterialItemSpecHit>`
  - **必须实现固定方式**：
    1. `spec LIKE '%keyword%'`
    2. `spec_normalized LIKE '%keyword%'`（**必须带 category_code 过滤**且依赖索引）
  - **必须** `LIMIT 20`（V1 固定）
  - 返回字段（至少）：`{ code, spec, description, name, brand }`（与 PRD 15.3.2 一致；不返回 similarity）

- `MaterialItemSummary`
  - `code: string`
  - `name: string`
  - `spec: string`
  - `brand?: string`
- `MaterialItemSpecHit`
  - `code: string`
  - `spec: string`
  - `description: string`
  - `name: string`
  - `brand?: string`

- 错误码
  - `INVALID_QUERY`：limit/offset 非法（limit>50 等）
  - `VALIDATION_ERROR`：规格搜索未提供 category_code（强制要求）

#### 2.5 核心业务流程（正常 + 异常）

- **正常流程**
  1. Application 校验 limit/offset 上限
  2. Service 构造查询（默认 status=1）
  3. Repository 执行 SQL，返回 DTO
  4. 返回候选列表（LIKE 命中；Top20）
- **异常流程**
  - limit>50 → `INVALID_QUERY`
  - 规格搜索缺 category_code → `VALIDATION_ERROR`

#### 2.6 对外依赖（第三方 / 其他模块）

- `IMaterialItemRepository`
（V1）不依赖相似度算法接口（PRD 禁止相似度百分比/编辑距离）；仅依赖 Repository 进行 LIKE 候选提示查询。

索引使用与性能口径（必须明确，消除实现歧义）：

- `spec_normalized` 检索必须同时满足：
  - `category_code = ?`
  - `spec_normalized LIKE '%keyword%'`
  - 依赖索引：`idx_item_category_spec_norm(category_code, spec_normalized)`（用于缩小扫描范围；SQLite 对 `%keyword%` 不保证走索引，因此必须通过 category_code 先过滤）
- `spec LIKE '%keyword%'`：
  - 仅作为“辅助命中/召回补充”，不保证性能

V1 禁止项（与 PRD 一致）：

- 禁止相似度百分比、编辑距离、NLP/Embedding、复杂分词

---

### M04 Excel 导出模块

#### 2.1 模块职责（明确边界）

- 将搜索结果导出为 Excel（默认导出 active；可选包含废弃）
- 导出列结构必须固定（列名/顺序/类型可自动化验证）

#### 2.2 不负责什么（必须写）

- 不负责复杂查询逻辑（必须复用 M03 的 Query 口径）
- 不负责业务规则判断

#### 2.3 核心数据模型（字段 + 类型 + 约束）

- `ExportRequest`
  - `CategoryCode?: string`
  - `Keyword?: string`
  - `IncludeDeprecated: bool`
- `ExportRow`
  - `code: string`
  - `name: string`
  - `spec: string`
  - `brand?: string`
  - `status: int`
  - `created_at: string(datetime)`

#### 2.4 核心接口（输入 / 输出 / 异常）

- `ExportToExcel(request) -> { filePath: string }`（或固定为 `byte[]`，二选一后禁止漂移）
- 错误码
  - `EXPORT_LIMIT_EXCEEDED`：导出行数超过上限（建议 10000）
  - `EXPORT_FAILED`：导出组件失败/IO 失败

#### 2.5 核心业务流程（正常 + 异常）

- **正常流程**
  1. 调用 `MaterialSearchService` 获取数据（与 UI 搜索口径一致）
  2. 若数据量 > 10000 → `EXPORT_LIMIT_EXCEEDED`
  3. 调用 `IExcelExporter.Export(rows)` 输出文件
- **异常流程**
  - Exporter 抛错 → `EXPORT_FAILED`（必须记录日志）

#### 2.6 对外依赖（第三方 / 其他模块）

- `IExcelExporter`（第三方库适配器）
- `MaterialSearchService`

---

### M05 状态管理模块（Deprecated）

#### 2.1 模块职责（明确边界）

- 将 `material_item.status` 从 1 更新为 0（废弃）
- 禁止物理删除
- 默认搜索/导出过滤 `status=1`

#### 2.2 不负责什么（必须写）

- 不负责“废弃释放唯一性”：明确 **不释放**（废弃仍占用 spec 唯一）
- 不负责修改 spec：明确 **spec 不允许修改**

#### 2.3 核心数据模型（字段 + 类型 + 约束）

- `status: int`（**强制最终口径：INTEGER**；1=active，0=deprecated）

status 语义口径（必须固定，消除实现歧义）：

- DB：`INTEGER`（仅允许 0/1，禁止 TEXT 状态）
- Domain：枚举 `MaterialStatus`（`Active=1`，`Deprecated=0`）
- UI：仅做展示文案映射（例如“正常/废弃”），不得把展示文案反写入 DB

#### 2.4 核心接口（输入 / 输出 / 异常）

- `DeprecateMaterialItem(code: string) -> { code: string, status: 0 }`
- 错误码
  - `NOT_FOUND`
  - `ALREADY_DEPRECATED`

#### 2.5 核心业务流程（正常 + 异常）

- **正常流程**：按 code 更新 status=0
- **异常流程**：不存在/重复废弃映射错误码

#### 2.6 对外依赖（第三方 / 其他模块）

- `IMaterialItemRepository`

---

### M06 规格归一化模块（SpecNormalizationService）

#### 2.1 模块职责（明确边界）

- 生成 `spec_normalized`（宽松归一化）
- 行为必须与 PRD 的伪代码一致，使用单元测试金样例固定行为

#### 2.2 不负责什么（必须写）

- 不负责输入合法性拦截（允许不规范输入）
- 不负责唯一性裁决（唯一性由 `UNIQUE(category_code, spec)` 强制）

#### 2.3 核心数据模型（字段 + 类型 + 约束）

- 输入：`description: string`（完整规格描述）
- 输出：`spec_normalized: string`

#### 2.4 核心接口（输入 / 输出 / 异常）

- `Normalize(description: string) -> string`

#### 2.5 核心业务流程（正常 + 异常）

- V1 归一化规则（**唯一允许**，必须与 PRD 一致）
  1. 转大写
  2. 去首尾空格（trim）
  3. 多空格压缩为 1 个空格（含 Tab 等空白）

禁止（V1，必须写入实现与测试约束）：

- 禁止单位归一、数值合并、语义解析、复杂 token
- 禁止基于 `spec_normalized` 做唯一性判断

#### 2.6 对外依赖（第三方 / 其他模块）

- 无（纯函数）

---

### M07 候选提示模块（LIKE 子串包含，V1 固定）

#### 2.1 模块职责（明确边界）

- 提供“录入前/提交时”的候选提示列表（用于人工判断是否重复、是否加入替代组）
- **V1 固定：相似=包含**，仅允许 **LIKE 子串包含**（`spec` 或 `spec_normalized` 命中）
- 明确：候选提示 **不参与唯一性判断**，不阻断创建

#### 2.2 不负责什么（必须写）

- 不负责阻断创建（唯一性由 `UNIQUE(category_code, spec)` 决定）
- 不负责相似度百分比、编辑距离、复杂分词、NLP/Embedding、外部服务（PRD V1 禁止）

#### 2.3 核心数据模型（字段 + 类型 + 约束）

- 输入：`category_code: string`、`keyword: string`、`include_deprecated: bool`
- 输出：候选列表 `MaterialCandidate[]`（Top 20）

#### 2.4 核心接口（输入 / 输出 / 异常）

- `GetCandidatesByLike(query) -> MaterialCandidate[]`
  - 约束：**必须带** `category_code` 过滤，且 **必须 LIMIT 20**

#### 2.5 核心业务流程（正常 + 异常）

- 候选列表获取（V1 固定，必须与 PRD 一致）
  - SQL 口径（逻辑等价）：
    - `WHERE category_code = ? AND status = 1 AND (spec LIKE '%keyword%' OR spec_normalized LIKE '%keyword%') LIMIT 20`
  - 规则：
    - 必须带 `category_code`
    - 必须 `LIMIT 20`
    - 返回结果仅用于提示/人工判断，不参与唯一性
- 异常：候选查询异常 → `CANDIDATE_QUERY_FAILED`（仅影响提示，不影响创建），记录日志

#### 2.6 对外依赖（第三方 / 其他模块）

- `IMaterialItemRepository`（候选集查询）

---

### M08 V2 结构化预留模块（material_attribute + is_structured）

#### 2.1 模块职责（明确边界）

- V1 必须落库：`material_item.is_structured`（0/1）
- V1 可先建表：`material_attribute`（不强依赖 V1 流程）
- 为 V2 预留升级链路：spec → 解析候选 attribute → 用户确认 → 写入 attribute → is_structured=1

#### 2.2 不负责什么（必须写）

- V1 不实现结构化解析与回填流程
- 不改变 V1 的 spec 唯一性与不可修改约束

#### 2.3 核心数据模型（字段 + 类型 + 约束）

- `material_attribute`
  - `id: int`（PK 自增）
  - `material_item_id: int`（NOT NULL，FK）
  - `attr_key: string`（NOT NULL）
  - `attr_value: string`（NOT NULL）
  - `created_at: string(datetime)`（DEFAULT）
  - **必须前置定义的唯一约束**：`UNIQUE(material_item_id, attr_key)`

#### 2.4 核心接口（输入 / 输出 / 异常）

- V1：仅 Repository CRUD（不对 UI 暴露用例）
- V2 预留：
  - `UpsertAttributes(materialItemId: int, attrs: {key:string,value:string}[])`
  - `ATTRIBUTE_KEY_DUPLICATE`（同一 item 同 key 重复）

#### 2.5 核心业务流程（正常 + 异常）

- V1：无业务流程
- V2：按升级链路实现（独立版本）

#### 2.6 对外依赖（第三方 / 其他模块）

- `IMaterialAttributeRepository`

---

## 3. 数据模型设计

### 3.1 所有表结构（字段、类型、约束）

> 数据库设计前置定义：以 PRD V1.2 “SQLite 推荐 DDL（可直接执行）”为准，并补充 V2 预留唯一约束（material_attribute）。

### 3.1.0 updated_at 自动维护规则（必须定义）

问题：已引入 `updated_at` 但若无维护规则，会导致工程不可测与实现歧义。

强制规则：

- 任何对行的 UPDATE（例如：废弃 `status`、修改 name/description/brand、结构化标记等）都必须更新 `updated_at = CURRENT_TIMESTAMP`
- 责任层：**Infrastructure（Repository）负责**（Domain 禁止 DB 操作，Application 仅编排）
- SQLite 实现方式（二选一，必须固定后不可漂移）：
  1. **触发器（推荐）**：为每张包含 `updated_at` 的表建立 BEFORE/AFTER UPDATE 触发器自动写入
  2. Repository 显式更新：所有 UPDATE 语句必须包含 `updated_at = CURRENT_TIMESTAMP`

可测试性要求（必须）：

- 任意 UPDATE（至少覆盖：`DeprecateMaterialItem`）后，`updated_at` 必须发生变化（集成测试断言）

#### 3.1.1 category

- `id INTEGER PRIMARY KEY AUTOINCREMENT`
- `code TEXT NOT NULL UNIQUE`
- `name TEXT NOT NULL UNIQUE`
- `created_at TEXT DEFAULT CURRENT_TIMESTAMP`
- `updated_at TEXT DEFAULT CURRENT_TIMESTAMP`

#### 3.1.2 material_group

- `id INTEGER PRIMARY KEY AUTOINCREMENT`
- `category_id INTEGER NOT NULL`（外键：Category.id；唯一关系字段）
- `category_code TEXT NOT NULL`（冗余：展示/导出/检索过滤；非关系字段）
- `serial_no INTEGER NOT NULL`
- `created_at TEXT DEFAULT CURRENT_TIMESTAMP`
- `updated_at TEXT DEFAULT CURRENT_TIMESTAMP`
- `UNIQUE(category_id, serial_no)`
- `FOREIGN KEY (category_id) REFERENCES category(id)`

#### 3.1.3 material_item

- `id INTEGER PRIMARY KEY AUTOINCREMENT`
- `group_id INTEGER NOT NULL`（FK→`material_group.id`）
- `category_id INTEGER NOT NULL`（外键：Category.id；唯一关系字段）
- `category_code TEXT NOT NULL`（冗余，用于唯一性与检索）
- `code TEXT NOT NULL UNIQUE`
- `suffix TEXT NOT NULL`（A-Z，系统生成）
- `name TEXT NOT NULL`
- `description TEXT NOT NULL`（完整规格描述；V1 必填）
- `spec TEXT NOT NULL`（原样保存，分类内唯一；长度约束见下）
- `spec_normalized TEXT NOT NULL`（仅搜索辅助；必须由 description 生成；不参与唯一性）
- `brand TEXT`
- `status INTEGER NOT NULL DEFAULT 1`（1=active，0=deprecated；不可物理删除）
- `is_structured INTEGER DEFAULT 0`
- `created_at TEXT DEFAULT CURRENT_TIMESTAMP`
- `updated_at TEXT DEFAULT CURRENT_TIMESTAMP`
- `CHECK(status IN (0, 1))`（强制：禁止 TEXT 状态与非法值）
- `UNIQUE(group_id, suffix)`
- `UNIQUE(category_code, spec)`
- `FOREIGN KEY (category_id) REFERENCES category(id)`
- `CHECK(length(spec) <= 512)`（工程约束：降低 SQLite 在 UNIQUE 长字段下的性能风险；与 Application 校验一致）

### 状态扩展策略（Status Evolution Policy）

- V1（当前版本）：
  - `status` 固定为 **INTEGER**（0=deprecated，1=active）
  - DB 强制约束：`CHECK(status IN (0, 1))`

- V2+（未来扩展约束）：
  - 若新增状态（例如：archived、draft），必须：
    1. 修改 Domain 枚举 `MaterialStatus`
    2. 执行 DB Migration（重建表或调整 CHECK 约束）
    3. 更新所有相关测试用例（状态过滤/搜索/导出）

- 强制约束：
  - 禁止将 `status` 改为 TEXT 类型
  - 禁止 UI 文案直接入库
  - 所有状态语义必须由 Domain 枚举定义

- 结论：
  - 当前为“受控扩展模型”，允许演进但必须通过 Migration

#### 3.1.4 material_attribute（V2 预留）

- `id INTEGER PRIMARY KEY AUTOINCREMENT`
- `material_item_id INTEGER NOT NULL`（FK→`material_item.id`）
- `attr_key TEXT NOT NULL`
- `attr_value TEXT NOT NULL`
- `created_at TEXT DEFAULT CURRENT_TIMESTAMP`
- `updated_at TEXT DEFAULT CURRENT_TIMESTAMP`
- **预留唯一约束（必须）**：`UNIQUE(material_item_id, attr_key)`

### 3.2 主键策略

- 全表使用 `INTEGER PRIMARY KEY AUTOINCREMENT`
- 业务主键：
  - `category.code`
  - `material_item.code`

### 3.3 唯一性约束（必须明确到字段组合）

- `category(code)` 唯一
- `category(name)` 唯一
- `material_group(category_id, serial_no)` 唯一
- `material_item(code)` 唯一
- `material_item(group_id, suffix)` 唯一
- **关键强约束**：`material_item(category_code, spec)` 唯一（废弃仍占用）
- `material_attribute(material_item_id, attr_key)` 唯一（V2）

### spec 唯一性策略补充说明

当前实现（V1）：

- 使用 DB 强制约束：`UNIQUE(category_code, spec)`
- 废弃数据（status=0）仍占用唯一性（同分类同 spec 永远禁止重复创建）

设计意图：

- 防止历史物料重复编码
- 保证编码体系稳定性与可追溯

未来扩展（仅允许通过版本升级实现）：

- 若需支持“废弃后可复用 spec”，必须：
  1. 移除或调整 `UNIQUE(category_code, spec)`
  2. 引入状态参与唯一性（例如：仅对 active 生效的唯一性约束；SQLite 层面可通过重建表/调整约束实现）
  3. 重写相关测试（`SPEC_DUPLICATE` 行为会改变）

V1 强制：

- 不允许任何形式的“唯一性释放”
- 不允许通过逻辑绕过 DB UNIQUE 约束（必须以 DB 为最终仲裁）

### 3.4 索引设计

- `idx_material_item_code(code)`
- `idx_material_item_spec(spec)`
- `idx_material_item_spec_normalized(spec_normalized)`
- **关键索引**：`idx_item_category_spec_norm(category_code, spec_normalized)`
- `idx_material_item_group_id(group_id)`
- `idx_material_item_status(status)`
- **必备复合索引（评审补强）**：`idx_item_category_status(category_code, status)`（用于默认过滤 `status=1` 且限定分类时的检索/导出）

### 3.5 是否需要冗余字段（说明原因）

- `material_item.category_code`：**需要**
  - 原因：必须在同表表达 `UNIQUE(category_code, spec)` 及 `idx_item_category_spec_norm`，避免联表导致约束不可表达/查询不稳定。
- `search_text`：V1 **不需要**
  - 原因：V1 固定 LIKE 检索，后续再扩展 FTS/ES。

---

## 4. API / 服务契约

> 本系统为本地 WPF 应用，契约以 Application Service DTO 固化（同样可迁移为 HTTP）。所有字段明确类型，错误码可自动化断言。

### 4.0 统一返回结构（强制）

> 评审要求所有错误必须统一结构：`ErrorCode + Message`。本系统所有 Application 用例必须返回以下二选一结构，禁止散落“异常字符串/不规则对象”。

- 成功返回：
  - `SuccessResponse<T>`
    - `data: T`
- 失败返回：
  - `ErrorResponse`
    - `error: { code: string, message: string }`

约束：

- `error.code` 必须来自本章定义的错误码表（禁止临时字符串）
- `error.message` 必须可面向用户展示（禁止堆栈/内部 SQL 文本）
- Domain/Infrastructure 内部异常不得直接穿透到 UI；必须在 Application 层映射为 `ErrorResponse`
- `INTERNAL_ERROR` 只能由 Application 层返回，且必须记录日志（包含 operation、error_code、异常摘要）

### 4.1 错误码总表（V1.2 必须包含）

| 错误码 | 触发场景 | 是否阻断创建 |
|---|---|---:|
| SPEC_DUPLICATE | 同分类 `spec` 完全重复（触发 `UNIQUE(category_code, spec)`） | 是 |
| SUFFIX_OVERFLOW | 替代料 suffix 超过 Z | 是 |
| SUFFIX_SEQUENCE_BROKEN | 替代料发现 suffix 集合不连续（缺口） | 是 |
| CODE_CONFLICT_RETRY | 并发/冲突导致事务级重试超过 3 次 | 是 |
| INTERNAL_ERROR | 未知异常（非预期异常） | 是 |
| NOT_FOUND | group 或 item 不存在 | 视用例 |
| ALREADY_DEPRECATED | 重复废弃 | 否（但返回错误） |
| INVALID_QUERY | limit/offset 超界或非法 | 否 |
| VALIDATION_ERROR | 必填缺失/超长/不满足输入约束 | 否 |
| EXPORT_LIMIT_EXCEEDED | 导出行数超过上限 | 否 |
| EXPORT_FAILED | 导出失败/IO 失败 | 否 |
| CANDIDATE_QUERY_FAILED | 候选提示查询失败（仅提示链路） | 否（不得阻断创建） |

### 4.2 分类管理契约

- `CreateCategoryRequest`
  - `code: string`
  - `name: string`
- `CreateCategoryResponse`
  - `id: int`
  - `code: string`
  - `name: string`

### 4.3 新建主物料（A）契约

- `CreateMaterialItemARequest`
  - `category_code: string`
  - `spec: string`
  - `name: string`
  - `description: string`（必填，完整规格描述）
  - `brand?: string`

输入校验（必须可测试）：

- `spec` 长度必须 `<= 512`，否则返回 `VALIDATION_ERROR`
- `MaterialCandidate`（V1 候选提示项）
  - `code: string`
  - `spec: string`
  - `description: string`
- `CreateMaterialItemAResponse`
  - `group_id: int`
  - `category_code: string`
  - `serial_no: int`
  - `item_id: int`
  - `code: string`
  - `suffix: string`（固定 "A"）
  - `spec: string`
  - `spec_normalized: string`
  - `status: int`（固定 1）
  - `is_structured: int`（固定 0）
  - `candidates: MaterialCandidate[]`（Top 20，仅提示，不阻断创建）

### 4.4 新增替代料（B-Z）契约

- `CreateReplacementRequest`
  - `group_id: int`
  - `spec: string`
  - `name: string`
  - `description: string`（必填，完整规格描述）
  - `brand?: string`
- `CreateReplacementResponse`
  - `item_id: int`
  - `group_id: int`
  - `code: string`
  - `suffix: string`（"B".."Z"）
  - `spec: string`
  - `spec_normalized: string`
  - `status: int`
  - `candidates: MaterialCandidate[]`（Top 20，仅提示，不阻断创建）

### 4.5 搜索契约

- `SearchByCodeRequest`
  - `code_keyword: string`
  - `category_code?: string`
  - `include_deprecated: bool`
  - `limit: int`
  - `offset: int`
- `MaterialItemSummary`
  - `code: string`
  - `name: string`
  - `spec: string`
  - `brand?: string`
- `SearchByCodeResponse`
  - `total: int`
  - `items: MaterialItemSummary[]`

- `SearchBySpecRequest`
  - `spec_keyword: string`
  - `category_code: string`（强制）
  - `include_deprecated: bool`
  - `limit: int`
  - `offset: int`
- `MaterialItemSpecHit`
  - `code: string`
  - `spec: string`
  - `description: string`
  - `name: string`
  - `brand?: string`
- `SearchBySpecResponse`
  - `total: int`
  - `items: MaterialItemSpecHit[]`

### 4.6 导出契约

- `ExportExcelRequest`
  - `category_code?: string`
  - `keyword?: string`
  - `include_deprecated: bool`
- `ExportExcelResponse`
  - `file_path: string`

### 4.7 状态管理契约

- `DeprecateRequest`
  - `code: string`
- `DeprecateResponse`
  - `code: string`
  - `status: int`（固定 0）

---

## 5. 业务流程设计

### 5.1 新建主物料（A）

#### 5.1.1 正常流程

1. UI 选择 `category_code`（仅展示已存在分类；无则先创建分类）
2. UI 输入 `spec/name/description/brand`
3. 可选：录入前候选提示（仅提示，V1 固定为 LIKE 子串包含，Top 20）
4. 提交创建 → `CreateMaterialItemA`
5. Application：`spec_normalized = Normalize(description)`（V1 三步规则）
6. 单事务内完成：
   - 读取 maxSerialNo → serial_no=max+1
   - 插入 `material_group`
   - 插入 `material_item(A)`（生成 code）
7. 返回结果 + 候选列表（Top 20，仅提示）

#### 5.1.2 异常流程

- `SPEC_DUPLICATE`：同分类 spec 完全重复 → 阻断保存
- `CODE_CONFLICT_RETRY`：并发导致 serial_no 冲突重试失败 → 阻断保存
- `VALIDATION_ERROR`：必填缺失/超长 → 阻断提交

#### 5.1.3 边界情况

- `status=0` 的历史数据仍占用 `UNIQUE(category_code, spec)`：重复创建必须 `SPEC_DUPLICATE`
- `spec_normalized` 相同但 `spec` 不同：允许创建，但仅提示（不阻止创建）

spec 唯一性策略说明（V1 强制）：

- 废弃不释放唯一性：`status=0` 仍然占用 `UNIQUE(category_code, spec)`
- 若未来需要“废弃后可复用 spec”，必须走版本升级 + DB Migration + 全量测试更新（见 3.3 “spec 唯一性策略补充说明”）

### 5.2 新增替代料（B-Z）

#### 5.2.1 正常流程

1. UI 通过编码搜索定位目标主料/组
2. 获取 `group_id`
3. 输入替代料字段并提交 → `CreateMaterialItemReplacement`
4. 单事务内：
   - 查询 maxSuffix → nextSuffix
   - 校验 nextSuffix≤Z
   - 校验 suffix 集合连续（无缺口）
   - 插入新 item
5. 返回结果 + 候选列表（Top 20，仅提示，V1 固定为 LIKE 子串包含）

#### 5.2.2 异常流程

- `SUFFIX_OVERFLOW`：已到 Z → 阻断创建
- `SUFFIX_SEQUENCE_BROKEN`：发现缺口 → 阻断创建
- `SPEC_DUPLICATE`：同分类 spec 冲突 → 阻断创建
- `CODE_CONFLICT_RETRY`：并发冲突重试失败 → 阻断创建

#### 5.2.3 边界情况

- suffix 只能系统生成：任何外部输入 suffix 的行为必须在契约层禁止（请求 DTO 不含 suffix）
- group 不存在：`NOT_FOUND`

spec 唯一性策略说明（V1 强制）：

- Replacement 亦受 `UNIQUE(category_code, spec)` 约束：同分类同 spec 必须 `SPEC_DUPLICATE`（与 status 无关）

## 5.X Rule Policy（规则策略模型）

> 目的：在不改变 V1 行为的前提下，为未来规则变更提供可控扩展路径。
>
> **V1 强制约束（必须）**：
>
> - 所有 Policy 在 V1 必须固定为：
>   - `SuffixPolicy = STRICT`
>   - `StatusPolicy = FIXED_BINARY`
>   - `SpecUniquenessPolicy = STRICT`
> - 禁止通过配置/UI 修改 Policy
> - Policy 仅作为“架构扩展点”，不得影响当前行为

### 1. SuffixPolicy

- `STRICT`（V1 默认）：
  - suffix 必须连续（A→B→C）
  - 存在缺口 → `SUFFIX_SEQUENCE_BROKEN`

- `RELAXED`（预留，V1 禁止启用）：
  - 允许跳号（A→C）
  - 仍然必须满足 `UNIQUE(group_id, suffix)`

### 2. StatusPolicy（预留）

- `FIXED_BINARY`（V1 默认）：
  - 仅允许 0/1（与 DB：`CHECK(status IN (0, 1))` 一致）

- `EXTENSIBLE`（V2+）：
  - 支持多状态（必须配合 DB Migration 调整 CHECK 约束）

### 3. SpecUniquenessPolicy（新增）

- `STRICT`（V1 默认）：
  - `UNIQUE(category_code, spec)`
  - 废弃（status=0）仍占用唯一性

- `REUSABLE`（预留）：
  - 废弃后允许复用 spec（需移除或调整 UNIQUE 约束，属于版本升级行为）

### 5.3 搜索（编码 / 规格）

#### 5.3.1 正常流程

- 编码搜索：前缀/包含 LIKE，limit 默认 20（最大 50）
- 规格搜索：必须带 category_code，固定 SQL：
  - `spec LIKE '%keyword%'`
  - `spec_normalized LIKE '%keyword%'`
  - 候选集可控后计算 similarity

#### 5.3.2 异常流程

- 规格搜索缺 category_code → `VALIDATION_ERROR`
- limit>50/offset<0 → `INVALID_QUERY`

#### 5.3.3 边界情况

- includeDeprecated=false：必须过滤 `status=1`
- includeDeprecated=true：必须包含 `status=0`

### 5.4 Excel 导出

#### 5.4.1 正常流程

1. 发起导出（可选条件）
2. 复用搜索口径拿数据
3. 行数<=10000 时导出并返回文件路径

#### 5.4.2 异常流程

- 超上限 → `EXPORT_LIMIT_EXCEEDED`
- 导出失败 → `EXPORT_FAILED`

#### 5.4.3 边界情况

- 列结构必须固定（可通过自动化测试断言）

### 5.5 状态废弃

#### 5.5.1 正常流程

1. 输入或选择 code
2. 更新 `status=0`
3. 默认搜索/导出不可见（除非 includeDeprecated=true）

#### 5.5.2 异常流程

- code 不存在 → `NOT_FOUND`
- 已废弃 → `ALREADY_DEPRECATED`

#### 5.5.3 边界情况

- 废弃后不释放唯一性：同分类同 spec 仍禁止创建（必须测试）

### 状态扩展策略（Status Evolution Policy）

- V1（当前版本）：
  - `status` 固定为 **INTEGER**（0=deprecated，1=active）
  - DB 强制约束：`CHECK(status IN (0, 1))`

- V2+（未来扩展约束）：
  - 若新增状态（例如：archived、draft），必须：
    1. 修改 Domain 枚举 `MaterialStatus`
    2. 执行 DB Migration（重建表或调整 CHECK 约束）
    3. 更新所有相关测试用例（状态过滤/搜索/导出）

- 强制约束：
  - 禁止将 `status` 改为 TEXT 类型
  - 禁止 UI 文案直接入库
  - 所有状态语义必须由 Domain 枚举定义

- 结论：
  - 当前为“受控扩展模型”，允许演进但必须通过 Migration

---

## 6. 测试设计（重点）

> 所有关键设计必须可用于自动化验证；测试即规范。必须覆盖 PRD 第 17 章：spec_normalized、相似度、并发。

### 6.1 单元测试

#### 6.1.1 SpecNormalizationService.Normalize（金样例必须固定）

- `10uF`、`10UF`、`10 µF` → `spec_normalized` 必须一致
- `1 0 u F` → **不得**等同 `10UF`（禁止合并分隔数字）
- 评审提供的小写金样例（`abc123`）与 PRD“转大写”口径冲突；在 PRD 未修订前，必须采用 PRD 口径金样例：
  - `Normalize("  ABC-123 ") == "ABC123"`
  - `Normalize("A-1") == "A1"`
- 单位别名最小集：PF/NF/UF/OHM/KOHM/MOHM/V/W/A（每类至少 1 用例）
- 全角/半角、符号替换、字符集过滤、数字+单位黏连的边界用例

#### 6.1.3 输入校验金样例（必须新增）

- 超长 spec（长度 513）→ `VALIDATION_ERROR`

#### 6.1.2 suffix 连续性校验

- `["A","B","C"]` → 通过
- `["A","C"]` → `SUFFIX_SEQUENCE_BROKEN`
- `[]`（空集合）→ 对 Replacement 必须先存在 A，否则 `NOT_FOUND` 或流程拒绝（由实现选择并固定）

### 6.2 集成测试（SQLite + DDL + Repository + Service）

#### 6.2.1 CreateMaterialItemA

- 成功创建：group+item(A) 落库正确，code 7 位补零正确
- `SPEC_DUPLICATE`：同分类同 spec 冲突（包含 status=0 的历史记录也必须冲突）
- 冗余字段一致性（必须新增断言）：
  - `material_item.category_code == material_group.category_code`
  - `material_item.category_id == material_group.category_id`
- 并发创建（轻量并发）：多线程同分类创建 A，断言：
  - `material_group(category_id, serial_no)` 不重复
  - `material_item(code)` 不重复
  - 冲突时最多重试 3 次，失败返回 `CODE_CONFLICT_RETRY`
  - serial_no 允许跳号：例如允许出现 `1,2,4`（不做“必须连续”的断言）

#### 6.2.2 CreateMaterialItemReplacement

- suffix 递增：B、C…正确
- `SUFFIX_OVERFLOW`：创建到 Z 后再建必须失败
- `SUFFIX_SEQUENCE_BROKEN`：人为制造缺口数据后创建必须失败
- 冗余字段一致性（必须新增断言）：
  - `material_item.category_code == material_group.category_code`
  - `material_item.category_id == material_group.category_id`

#### 6.2.3 SearchByCode / SearchBySpec

- 默认过滤 status=1
- includeDeprecated=true 包含 status=0
- 规格搜索必须带 category_code，否则 `VALIDATION_ERROR`

#### 6.2.4 DeprecateMaterialItem

- 成功置 0 后默认搜索不可见
- 置 0 后仍占用 spec 唯一性：重复创建必须 `SPEC_DUPLICATE`
- updated_at 维护（必须新增断言）：
  - Deprecate（UPDATE status）后 `updated_at` 必须发生变化

#### 6.2.5 ExportToExcel

- 导出列结构断言（列名/顺序/类型）
- 注入 Exporter 失败 → `EXPORT_FAILED`
- 导出量超上限 → `EXPORT_LIMIT_EXCEEDED`

### 6.3 自动化接口测试（用例黑盒契约）

- 对每个 Application 用例输入固定 DTO，断言：
  - 返回 DTO 字段完整
  - 错误码精准
  - 数据库副作用正确（行数、字段、唯一性约束触发）

### 6.4 支持 Cursor 自检机制（工程落地要求）

- 必须提供一键自检入口：`dotnet test` 可跑完全部测试
- 自检必须覆盖：
  - spec_normalized 金样例（PRD 17.1）
  - 相似提示排序/阈值/仅提示（PRD 17.2）
  - 并发创建不重复 + 冲突重试 + 超过 3 次错误码（PRD 17.3）

### 6.5 State Replay（状态回放）测试模型（建议但推荐执行）

目的：覆盖复杂流程的自动验证闭环能力（Create / Deprecate / Create 等组合），确保最终状态可断言、可复现。

测试结构（强制三段式）：

- Given：初始数据状态（DB 种子数据）
- When：一系列操作（Application 用例序列）
- Then：最终状态断言（suffix/唯一性/status/返回错误码）

最小覆盖用例（必须可自动化验证）：

1. Given：空分类 + 空物料
   - When：CreateA(spec=X) → Deprecate(code=X-A) → CreateA(spec=X)
   - Then：第二次 CreateA 必须 `SPEC_DUPLICATE`（废弃仍占用唯一性）
   - And：明确断言“废弃不释放唯一性”（V1 强制；若未来变更必须版本升级 + DB Migration + 重写测试）
2. Given：同 group 已存在 A、B、C
   - When：手工制造缺口（例如删除 B 的行在本系统禁止物理删除，因此以“直接写测试 DB 种子”为准）→ 再 CreateReplacement
   - Then：必须 `SUFFIX_SEQUENCE_BROKEN`（符合 PRD）
3. Given：同 group 已存在 A、B、C（且均为 active）
   - When：并发执行（使用 6.6 的固定并发模型）：
     - 操作1：CreateReplacement(group_id)
     - 操作2：Deprecate(code=B)
   - Then：
     - 不出现 suffix 重复（`UNIQUE(group_id, suffix)` 永不破坏）
     - 不出现状态异常（B 最终为 deprecated；新增替代料为 active）
     - 若发生冲突重试失败，必须返回 `CODE_CONFLICT_RETRY`（而非静默失败/部分提交）

### 6.6 并发测试执行模型（必须完全确定化）

目标：消除“多线程但不可复现”的歧义，确保 Cursor 生成的并发测试稳定可重复。

固定模型（强制）：

- 线程数：10
- 每线程请求数：20
- 总请求数：200

断言（强制）：

- 无重复 `material_item.code`
- 无重复 `material_group(category_id, serial_no)`
- 无重复 `material_item(group_id, suffix)`
- 冲突路径：
  - 唯一性冲突可发生但必须被重试吸收；超过 3 次必须 `CODE_CONFLICT_RETRY`

---

## 7. 非功能设计

### 7.1 性能目标

- 数据规模：< 10,000 条
- 搜索（limit=20）：
  - 编码搜索：P95 < 200ms
  - 规格搜索：P95 < 300ms（V1 固定 `LIMIT 20`；LIKE 子串包含；不计算 similarity）
- 导出：<= 10,000 行 < 3s（需基准测试验证）

### 7.2 并发控制策略（必须遵循）

- 采用：**乐观并发 + 数据库唯一约束 + 事务级重试（最多 3 次）**
- 明确禁止：
  - 应用层锁
  - `SELECT FOR UPDATE`
- 适用场景：
  - `serial_no` 生成（Create A）
  - `suffix` 生成（Create Replacement）
- 超过 3 次：返回 `CODE_CONFLICT_RETRY`

### 7.3 日志与追踪

- 统一接口：`IAppLogger`
- 必须记录字段（结构化）：
  - `operation`（CreateA/CreateReplacement/Search/Export/Deprecate）
  - `category_code`、`group_id`、`code`
  - `error_code`
  - `retry_count`
  - `elapsed_ms`
- `CANDIDATE_QUERY_FAILED`：必须记录但不得阻断创建

### 7.4 异常处理机制（工程可落地）

- Repository：
  - 捕获 SQLite 约束异常（UNIQUE/FK）→ 统一转 `DbConstraintViolation`（包含约束/字段组合）
- Service/Application：
  - `UNIQUE(category_code, spec)` → `SPEC_DUPLICATE`
  - `UNIQUE(category_id, serial_no)` / `UNIQUE(group_id, suffix)` → 触发事务级重试
  - 非预期异常 → `INTERNAL_ERROR`（记录日志，UI 统一提示）

---

## 关键设计点为什么这样设计（必须说明）

- **唯一性采用 `UNIQUE(category_code, spec)` 而非 `spec_normalized`**
  - 原因：PRD 定稿 `spec` 为业务唯一输入；`spec_normalized` 仅用于检索/相似提示，不能作为阻断依据。DB 约束强制不可绕过，可被集成测试验证。
- **`material_item` 冗余 `category_code`**
  - 原因：同表表达分类内 spec 唯一与索引优化，避免联表导致约束不可表达/性能不稳定。
- **并发采用 UNIQUE + 事务级重试**
  - 原因：PRD 明确不使用应用锁与 `SELECT FOR UPDATE`，本地 SQLite 场景用约束驱动的乐观重试更可控且可通过并发测试验证。
- **suffix 缺口直接禁止创建**
  - 原因：PRD 强约束“必须连续且不允许跳号/补洞”，禁止缺口消除替代组歧义，且规则可单测/集成测验证。

