# MCS V1.3 技术执行方案汇总（实现版）

| 项 | 内容 |
|---|---|
| 版本 | 1.0（实现落地汇总） |
| 日期 | 2026-04-13 |
| 关联冻结方案 | `docs/V1/MCS_V1_规则冻结方案_V1.3_评审版.md` |
| 关联 PRD | `docs/MCS_PRD_V1.md`（V1.3 规则冻结稿） |
| 关联 Validation Spec | `MaterialCodingSystem.Validation/specs/PRD_V1.yaml` |
| 适用工程 | `MaterialCodingSystem`（WPF + 多目标）、`MaterialCodingSystem.Tests`、`MaterialCodingSystem.Validation` |

> 目的：把 **“V1.3 冻结契约（PRD+YAML）”** 对应到 **“工程实际实现（代码+测试+验收）”**，形成一份可评审、可回溯、可复跑的技术执行汇总。

---

## 1. 总体约束（执行期不变）

- **分层约束**（Clean Architecture）：
  - Presentation（WPF/MVVM）只做绑定与交互编排；不实现业务规则。
  - Application 负责编排、事务边界、错误码与重试策略；不写 SQL。
  - Domain 为纯规则（无 IO/DB/事务）。
  - Infrastructure 只实现接口（SQLite/Dapper/Excel/偏好存储），不写业务规则。
- **验收约束**：Validation **仅调用 Application API**；YAML 为黑盒事实源。
- **测试方法**：按 TDD 路径（先观察 FAIL，再做最小实现直到 PASS）。

---

## 2. 冻结语义 → 工程实现映射（V1.3 核心差异点）

### 2.1 Name 策略（“不可输入 + 快照持久化 + 展示/导出使用当前分类名”）

- **冻结要点**：
  - 创建时 `material_item.name` 写入 `category.name` 的快照；历史不随分类改名回写。
  - UI 展示与导出必须使用 `category.name`（逻辑 `display_name`），禁止使用 `material_item.name` 作为展示名。
- **工程落地**：
  - Application：创建主料/替代料时从 Repository 获取 `category.name` 注入 `MaterialItem.name`（快照写入）。
  - UI：创建页删除 Name 输入框，改为只读展示分类名。
  - 导出：通过 `JOIN category` 输出 `category.name` 作为导出 name。

### 2.2 Spec 唯一性口径（仅约束 status=1；status=0 释放 spec）

- **冻结要点**：
  - `UNIQUE(category_code, spec)` **仅约束 `status=1`**；`status=0` 允许复用 spec。
  - 当前 DB 未做 partial unique index，因此由 Application 层语义保证。
- **工程落地**：
  - Application：创建时的 spec 重复校验按冻结口径执行（过滤/口径以实现为准）。

### 2.3 Replacement（ByCode + 固定校验顺序 + 基准废弃禁止）

- **冻结要点**：
  - UI/VM 不暴露 `groupId`，替代料入口为 `CreateReplacementByCode(base_material_code, ...)`。
  - 校验顺序固定：base 存在 → base.status=1（否则 `ANCHOR_ITEM_DEPRECATED`）→ category 存在（否则 `CATEGORY_NOT_FOUND`）→ spec 唯一性 → suffix 分配与插入。
- **工程落地**：
  - Application：新增 `CreateReplacementByCode` 用例，并按冻结顺序实现。
  - UI：替代料页改为“基准编码”驱动，VM 内部缓存解析后的 groupId（不对外暴露）。

### 2.4 Suffix 分配并发与错误码（冲突重试耗尽区分）

- **冻结要点**：
  - suffix 并发冲突重试耗尽返回 `SUFFIX_ALLOCATION_FAILED`；
  - code 等其他冲突重试耗尽返回 `CODE_CONFLICT_RETRY`；
  - suffix 分配遵循“单次尝试单事务 + 外层事务级重试”。
- **工程落地**：
  - Application：`ExecuteWithRetry` 按约束类型映射错误码。
  - Infrastructure：`SqliteUnitOfWork` 为并发测试环境补 `BeginTransactionWithRetryAsync`（shared-cache 内存库 BeginTx 短暂竞争容错）。

### 2.5 Export（双 Sheet、列结构固定、name 来源、排序）

- **冻结要点**：
  - Sheet1=全量（含 `status=0`）；分类 Sheet 也包含全部数据，并以 `status` 区分。
  - 列顺序固定 7 列：`code, category_code, name, spec, description, brand, status`
  - name= `JOIN category` 的 `category.name`；排序：`status DESC, category_code, serial_no, suffix, code`
- **工程落地**：
  - Repository：提供全量导出查询（`JOIN category`，带 status，含排序）。
  - Exporter：`ClosedXmlMaterialExcelExporter` 写入 Sheet1 + 分类 Sheet，列结构与排序与冻结一致。
  - Tests：补/更导出集成测试覆盖 sheet 数量、列位、name 来源、排序口径。

---

## 3. 三阶段执行清单（Phase 1~3）与关键产物

### 3.1 Phase 1：Application 语义（用例与错误码收敛）

- **目标**：把冻结语义落到 Application 编排与错误码，确保 Validation 语义可过。
- **主要内容**：
  - Name：创建时注入分类名快照；不再要求用户输入 name。
  - 新增 `CreateReplacementByCode` 并固定校验顺序；基准废弃返回 `ANCHOR_ITEM_DEPRECATED`。
  - 错误码：`CATEGORY_NOT_FOUND` / `SUFFIX_ALLOCATION_FAILED` / `ANCHOR_ITEM_DEPRECATED`。
  - 事务重试映射：suffix 冲突与 code 冲突区分返回码。

### 3.2 Phase 2：Repository + Export（数据一致性）

- **目标**：导出与 PRD/冻结契约一致（双 Sheet、列结构、name、排序、包含废弃）。
- **主要内容**：
  - Repository：导出全量查询（`ListAllItemsForExportAsync`）。
  - Exporter：写 Sheet1“全量” + 分类 Sheet；列结构 7 列；排序按冻结口径。
  - Tests：导出集成测试覆盖关键断言。

### 3.3 Phase 3：UI 收敛（交互与呈现一致）

- **目标**：UI 与冻结语义一致（Name 不可输入、ComboBox 显示、Replacement ByCode、状态列、行级废弃）。
- **主要内容**：
  - 创建页：删除 Name 输入，改只读展示分类名。
  - ComboBox：统一 ItemTemplate `"{Code} - {Name}"`。
  - 替代料：ByCode 流程；VM 不暴露 groupId；基准加载与提交分离（双状态机语义）。
  - DataGrid：新增状态列（“正常/已废弃”）与禁用策略。
  - 废弃：改为 DataGrid 行级按钮 + 确认弹窗；已废弃禁用（并移除底部“废弃编码”输入区）。

---

## 4. 关键代码索引（按能力域）

### 4.1 Application

- `MaterialCodingSystem/Application/MaterialApplicationService.cs`
- `MaterialCodingSystem/Application/ErrorCodes.cs`
- `MaterialCodingSystem/Application/Contracts/*`

### 4.2 Infrastructure（SQLite/Excel）

- `MaterialCodingSystem/Infrastructure/Sqlite/SqliteMaterialRepository.cs`
- `MaterialCodingSystem/Infrastructure/Sqlite/SqliteUnitOfWork.cs`
- `MaterialCodingSystem/Infrastructure/Excel/ClosedXmlMaterialExcelExporter.cs`

### 4.3 Presentation（WPF/MVVM）

- `MaterialCodingSystem/MainWindow.xaml`
- `MaterialCodingSystem/Presentation/ViewModels/*`
- `MaterialCodingSystem/Presentation/UiSemantics/*`
- `MaterialCodingSystem/Presentation/Resources/UiStrings.xaml`
- `MaterialCodingSystem/Presentation/Converters/*`

### 4.4 Tests / Validation

- `MaterialCodingSystem.Tests/*`
- `MaterialCodingSystem.Validation/specs/PRD_V1.yaml`

---

## 5. 验收口径与复跑命令（最终结果基线）

### 5.1 编译

- `build/build.cmd`：成功（退出码 0）

### 5.2 单元/集成测试

- `dotnet test -c Debug`：通过（当前基线 98/98）

### 5.3 YAML 黑盒验收

- `dotnet run --project MaterialCodingSystem.Validation -- specs/PRD_V1.yaml`
- 结果：`PASS=30, FAIL=0`

---

## 6. 与历史文档的关系说明

- `docs/V1/MCS_V1_规则冻结方案_V1.3_评审版.md`：**冻结契约（评审基准）**，描述“应该是什么”。
- 本文档：**实现版汇总（落地对照）**，描述“已经做成什么 + 如何验收复跑”。
- `docs/V1/MCS_V1_执行报告_评审用.md`：偏“体系化技术报告”，覆盖架构/ER/对照与风险建议；其部分内容反映较早阶段口径，本文以 **V1.3 冻结与当前实现** 为准。

