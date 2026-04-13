# MCS V1.3 规则冻结方案（评审版）

> 目的：将“产品语义”冻结为可测试、无歧义的确定性契约，作为后续代码重构唯一依据。  
> 约束：本轮仅 PRD + Validation Spec 对齐，不修改任何 C# 业务代码，不修改数据库结构。  
> 依据文件：`docs/MCS_PRD_V1.md`（V1.3 冻结稿）、`MaterialCodingSystem.Validation/specs/PRD_V1.yaml`（meta.prd_freeze）。

---

## 一、冻结产物（交付物）

- **PRD（唯一业务标准）**：`docs/MCS_PRD_V1.md`
- **Validation Spec（自动化依据）**：`MaterialCodingSystem.Validation/specs/PRD_V1.yaml`
- **实现改造（后续迭代，不在本轮执行）**：Application/Repository/Exporter/UI 按契约落地

---

## 二、核心语义冻结（必须一致）

### 2.1 状态（Domain 唯一语义源）

- **字段**：`material_item.status`
- **取值**：
  - `1` = 正常
  - `0` = 已废弃
- **一致性要求**：UI / 搜索 / 导出必须完全一致（同一语义、同一映射、同一文案）

### 2.2 Name（名称）策略（核心变更）

- **用户不可输入**：所有创建入口均禁止用户手输 name
- **持久化**：`material_item.name` 继续存储
- **值来源**：创建时取 `category.name` 快照写入 `material_item.name`
- **不可变**：分类改名不回写历史物料 name（快照保持创建时值）
- **一致性约束（开发前确认）**：
  - `category.name` 必须在**同一事务内读取**
  - 读取与插入必须使用**同一连接**
  - 快照语义以事务一致性为基准（见 spec `meta.prd_freeze.name_policy.transaction`）

术语区分（必须）：

- **`material_item.name`**：历史名称（快照，仅追溯，不保证与当前分类名一致）
- **`display_name`（逻辑概念，不新增字段）**：当前分类名称（`category.name`），用于 UI 展示与导出

强制规则（必须/禁止）：

- **UI 展示与导出场景，必须使用 `category.name`（display_name），不得使用 `material_item.name` 作为展示名称。**

### 2.3 唯一性范围（分三类策略，必须区分）

必须写死：

- `UNIQUE(code)`：**全局唯一**，包含已废弃（`status=0` 不释放编码）
- `UNIQUE(group_id, suffix)`：**同组唯一**，包含已废弃（`status=0` 不释放 suffix 槽位）
- `UNIQUE(category_code, spec)`：**仅约束 `status=1` 数据**；已废弃（`status=0`）**释放 spec 占用**，允许同分类复用 spec

补充说明：

- **“废弃不释放”仅适用于 `code` 与 `(group_id, suffix)`**：包含已废弃数据（status=0 不释放），以保证编码体系的历史一致性与可追溯性
- **spec 唯一性采用“仅约束正常、废弃释放”**：废弃用于修正错误录入；废弃后的规格允许重新创建，以提升业务可用性

工程实现口径补充（必须）：

- `UNIQUE(category_code, spec)` **仅约束 status=1** 属于**业务逻辑约束**。
- 当前数据库未实现 **partial unique index（部分唯一索引）**，因此该规则必须由 **Application 层在创建时保证**（在校验时过滤 `status=0` 数据）。
- 本轮不允许修改数据库结构（不得新增索引/触发器等）。

---

## 三、替代料（ByCode）冻结（P0）

### 3.1 入口模式

- **推荐 Application API**：`CreateReplacementByCode(base_material_code, input)`
- **Presentation/UI/VM 禁止**：出现 `group/groupId` 字样或暴露内部 id

### 3.2 基准约束

- 基准物料必须 `status = 1`
- 基准已废弃（`status = 0`）→ 错误码：`ANCHOR_ITEM_DEPRECATED`
- **不级联废弃**：废弃不影响同组其他替代料

### 3.3 事务与并发

- **单次尝试单事务**包含：基准解析（组解析）→ suffix 分配 → 插入物料
- **suffix 分配策略**：依赖 `UNIQUE(group_id, suffix)` + 事务级重试
- **重试上限**：3 次
- **重试耗尽错误码**：`SUFFIX_ALLOCATION_FAILED`

语义澄清（必须）：

- **CreateReplacementByCode 的“单事务”指单次分配尝试**；并发冲突时允许**跨事务外层重试**（重新开启新事务）以获得最终成功
- **禁止**在一个事务内循环尝试多个 suffix；必须依赖唯一约束冲突回滚后再重试

事务规则补充（必须）：

- **每一次事务仅允许尝试一个候选 suffix**；若发生 `UNIQUE(group_id, suffix)` 冲突，必须回滚当前事务，并在新事务中重新计算并重试。
- **禁止**在单一事务内循环尝试多个 suffix（该行为会导致不可控副作用与偏离冻结语义）。

### 3.4 校验顺序（必须写死顺序）

`CreateReplacementByCode(base_material_code, ...)` 固定顺序：

1. base_material_code 是否存在
2. base item.status == 1（否则 `ANCHOR_ITEM_DEPRECATED`）
3. category 是否存在（否则 `CATEGORY_NOT_FOUND`；优先级高于 suffix 分配）
4.（可选）规格合法性
5. 全部业务校验通过后才允许进入：suffix 分配（唯一约束 + 重试）

明确禁止：

- 在 suffix 分配前执行任何插入尝试
- 在 suffix 分配前开启重试逻辑

冻结句：**所有业务校验必须在 suffix 分配前完成。**

---

## 四、错误码冻结（归属清晰）

### 4.1 新增/强化错误码

- `CATEGORY_NOT_FOUND`：`category_code` 不存在或已失效（创建主物料/替代料 ByCode 分类解析失败；优先级高于 suffix 分配）
- `SUFFIX_ALLOCATION_FAILED`：suffix 槽位并发重试耗尽（仅用于 `UNIQUE(group_id, suffix)` 场景）
- `ANCHOR_ITEM_DEPRECATED`：基准物料已废弃，禁止创建替代料

### 4.2 区分规则（防混用）

- **suffix 槽位重试耗尽** → `SUFFIX_ALLOCATION_FAILED`
- **全局 code 等其他唯一约束重试耗尽** → `CODE_CONFLICT_RETRY`

---

## 五、废弃（Deprecation）冻结

- **交互**：必须列表行级操作 + 确认弹窗
- **状态联动**：已废弃不可再次操作
- **规则补充**：
  - 已废弃物料不可作为新增替代料基准（由 ByCode 校验保障）
  - 替代关系不级联废弃

---

## 六、导出（Excel）冻结（含 PRD 导出补丁）

> 注意：导出语义与系统内快照语义存在“刻意不同”，必须写明，避免评审歧义。

### 6.1 Sheet 范围

- **Sheet1（全量）**：全库所有物料（含 `status=0`）
- **分类 Sheet**：每分类一个 Sheet，包含该分类下**全部物料（含 `status=0`）**，通过 `status` 区分

### 6.2 列结构（强制统一、顺序固定、status 必选）

Sheet1 与分类 Sheet 列结构必须一致，列顺序固定：

1. `code`
2. `category_code`
3. `name`（导出视图名称，见 6.4）
4. `spec`
5. `description`
6. `brand`
7. `status`（必选：`1=正常`，`0=已废弃`）

### 6.3 排序（推荐强制 + 稳定键）

推荐排序（正常在前）：

```text
ORDER BY status DESC, category_code, serial_no, suffix
```

其中：

- `base_code（逻辑） = material_group.serial_no`（用于排序语义，不单独落库）
- `suffix` 按字母顺序（A < B < ... < Z）
- 稳定排序：当排序键相同时，以 `code` 作为最终稳定排序键

### 6.4 导出“名称”语义（关键消歧）

- **导出字段 name 来源**：使用**当前分类名称** `category.name`
- **不使用** `material_item.name`（历史快照）
- 语义解释：
  - `material_item.name`：创建时分类名快照，用于历史一致性与追溯
  - 导出 name：反映当前分类体系，用于当前视图报表
- 消歧声明：**导出数据为当前分类视图，不保证与历史快照名称一致。**

数据来源约束补充（必须）：

- 导出 `name` 必须通过 **`JOIN category`** 获取 `category.name`，不得使用 `material_item.name`（历史快照）或任何缓存值替代，以保证导出始终反映当前分类体系。

---

## 七、UI 收敛点（后续实现，不在本轮执行）

- ComboBox：禁止 `DisplayMemberPath`；必须 `ItemTemplate`：`{Code} - {Name}`
- Name 输入框：全局删除，仅只读展示分类名
- 替代料页：不出现 group/groupId；流程“搜索 → 选中 → 自动加载 → 提交”
- 替代料页状态机：
  - 基准加载态四态：未选择/加载中/成功/失败
  - 提交态独立（禁止混用）
- DataGrid：必须有 Status 列（正常/已废弃灰色）
- 废弃：行级按钮 + 确认弹窗；已废弃禁用

---

## 八、Validation Spec 现状与策略

### 8.1 Spec 冻结与标注

- 冻结口径写入 `PRD_V1.yaml` 的 `meta.prd_freeze`
- `meta.testing_scope_note`：当前阶段不新增复杂测试
- `meta.case_tags` + `meta.case_tags_note`：采用“caseId → tags映射”标注 `contract_only/echo`

### 8.2 运行结果（允许 FAIL 存在）

当前 runner 结果：

- 总计 30 例：PASS=28 / FAIL=2
- 预期 FAIL（因契约已冻结但尚未改代码）：
  - `NAME_SNAPSHOT_001`
  - `REPLACEMENT_ANCHOR_001`

---

## 九、后续实现清单（仅列出，不执行）

- Application：
  - name 快照注入（同事务同连接读取 category.name）
  - `CreateReplacementByCode`（固定校验顺序、单事务、并发重试）
  - 错误码分流：`SUFFIX_ALLOCATION_FAILED` vs `CODE_CONFLICT_RETRY`
  - `CATEGORY_NOT_FOUND` 触发点与优先级
- Repository/Exporter：
  - 导出 name = category.name
  - 分类 Sheet 包含废弃
  - 排序：status DESC + category_code + serial_no + suffix + code
  - 列结构固定 7 列（含 status）
- Presentation：
  - 状态列与样式
  - 废弃行级操作与禁用
  - ComboBox ItemTemplate
  - 替代料双状态机与 ByCode 流程

