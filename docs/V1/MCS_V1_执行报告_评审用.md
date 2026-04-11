# 物料编码系统（MCS）V1 执行报告（评审用）

| 项 | 内容 |
|----|------|
| 文档版本 | 1.0 |
| 对应基线 | PRD：`docs/MCS_PRD_V1.md`（V1.2 修正版）；TDD：`docs/MCS_TDD_V1.md` |
| 工程范围 | 主程序 `MaterialCodingSystem`（多目标）、测试 `MaterialCodingSystem.Tests`、验收 CLI `MaterialCodingSystem.Validation` |
| 编写目的 | 汇总技术架构、设计、ER/流程及与 PRD 的对照结论，供评审决策 |

---

## 1. 技术架构总览

### 1.1 形态

- **运行时**：.NET 8；桌面端为 **WPF + MVVM**；业务与数据访问以 **类库形态** 同时产出 `net8.0`，供控制台版 `MaterialCodingSystem.Validation`（`net8.0`）引用，**避免 Validation 依赖 WPF**。
- **数据**：**SQLite** 本地文件（默认 `%LocalAppData%\MaterialCodingSystem\mcs.db`）；测试与 YAML 验收使用 **内存库 / 每用例独立连接**。
- **数据访问**：**Dapper + 显式 SQL**；**禁止 EF Core**（与架构决策一致）。
- **依赖注入**：`Microsoft.Extensions.DependencyInjection`，在 `App.xaml.cs` 中组装（WPF 入口）。

### 1.2 分层与依赖方向（Clean Architecture + 单仓库内分层）

```
Presentation（WPF / ViewModels / 视图桥接）
    → Application（用例编排、事务边界、错误码、重试策略）
        → Domain（实体、值对象、领域服务：规范化、suffix、编码生成）
        → （经接口）Infrastructure（SQLite、Excel、用户偏好 JSON 等）
```

**强制约束（与 PRD/TDD 一致）**：

- **Domain**：无 IO、无 DB、无事务；仅纯规则与领域异常语义。
- **Application**：不写 SQL；通过 `IUnitOfWork`、`IMaterialRepository` 等接口编排；统一 `Result<T>` / 错误码。
- **Infrastructure**：只实现接口与 SQL/文件；**不写业务规则**。
- **Presentation**：只绑定与命令转发；**禁止**在 ViewModel 中实现唯一性、suffix 连续性、编码生成等规则；允许 **视图级事件桥接**（如焦点切换 keyword 来源）。
- **Validation**：**仅调用 Application API**，不直接调用 Domain；YAML 为黑盒验收输入。

### 1.3 多目标工程策略

- `MaterialCodingSystem.csproj`：`net8.0-windows`（WPF `WinExe`）+ `net8.0`（`Library`，排除 WPF 与 Presentation 下依赖 Dispatcher 的程序集）。
- 目的：主业务与验收工具共享同一套 Application/Domain/Infrastructure（`net8.0`），UI 独占 `net8.0-windows`。

---

## 2. 设计要点

### 2.1 应用服务（Application）

- **`MaterialApplicationService`**：主料创建、替代料创建、废弃、按编码/规格搜索、分组信息查询、分类列表/创建、**活动物料 Excel 导出**等。
- **并发**：新建主料（流水号）与新建替代料（suffix）在 **数据库唯一约束冲突**时做 **事务级重试（最多 3 次）**，超限映射为 `CODE_CONFLICT_RETRY`（与 PRD 15.6 一致）。
- **错误码**：至少包含 `SPEC_DUPLICATE`、`SUFFIX_OVERFLOW`、`SUFFIX_SEQUENCE_BROKEN`、`CODE_CONFLICT_RETRY`、`VALIDATION_ERROR`、`NOT_FOUND` 等；UI 按 dev-wpf 约定做字段级/全局/弹窗分流。

### 2.2 领域（Domain）

- **值对象**：`CategoryCode`、`Spec`、`SpecNormalized`、`Suffix` 等，封装格式与不变式。
- **实体/聚合**：`MaterialGroup`、`MaterialItem` 承载创建主料 A、追加替代料时的编码与 `spec_normalized` 生成（**仅基于 description 的三步规范化**，与 PRD 6.4.3 / 15.2 一致）。
- **领域服务**：`SpecNormalizer`（V1 三步）、`SuffixAllocator`（连续性与 Z 溢出）、`CodeGenerator`（7 位流水 + 后缀）。

### 2.3 基础设施（Infrastructure）

- **SQLite**：`SqliteSchema.EnsureCreated` 建表；含 PRD 附录索引及 **`material_attribute` 空表预留**（V1 不写入业务数据，符合 PRD 第十四章）。
- **仓储**：`SqliteMaterialRepository` 实现 `IMaterialRepository`，将约束冲突映射为 `DbConstraintViolationException` 供 Application 重试或转错误码。
- **Excel**：`ClosedXmlMaterialExcelExporter` 实现 `IExcelMaterialExporter`；导出数据来自仓储只读查询（`status = 1`，排序与 PRD 7.4 一致）。
- **偏好**：`JsonExportPathPreferenceStore` 持久化上次导出目录（评审确认的需求）。

### 2.4 表示层（Presentation）

- **MVVM**：`MainViewModel` 聚合各 Tab 子 ViewModel；命令使用 `RelayCommand` / `RelayCommand<T>`。
- **PRD 9.x 相关交互（已实现部分）**：新建页 **300ms 防抖** + **当前编辑字段**作为规格搜索 keyword；候选 Top20；决策条；分类 **独立对话框**；字段级 `SPEC_DUPLICATE` 与全局/弹窗 `CODE_CONFLICT_RETRY`；替代料页 **本页编码搜索**；导出 Tab + 保存对话框与路径记忆。

### 2.5 与 PRD 第十六章「Repository 拆分」的关系

- PRD 文档按 **Category / MaterialGroup / MaterialItem** 多仓储描述；当前实现为 **单一 `IMaterialRepository`** 集中 SQL。  
- **评审说明**：属 **工程组织差异**，不改变对外行为与约束；若组织规范要求与 PRD 章节逐字对齐，可后续重构拆分，**非功能缺失项**。

---

## 3. ER 模型与数据约束（与 PRD 第六章对齐）

### 3.1 概念 ER

```
Category（分类）
  1 ──< N  MaterialGroup（物料主档 / 替代组）
              1 ──< N  MaterialItem（可采购物料 A–Z）
```

- **对外最小检索单位**：`MaterialItem`（含后缀的完整 `code`）。
- **Group**：承载同组 A–Z；**不单独作为对外采购编码**（PRD 4.1）。

### 3.2 核心表与约束（逻辑）

| 表 | 要点 |
|----|------|
| `category` | `code`、`name` 唯一 |
| `material_group` | `UNIQUE(category_id, serial_no)`；关联 `category_id` |
| `material_item` | `UNIQUE(code)`；`UNIQUE(group_id, suffix)`；`UNIQUE(category_code, spec)`；`status ∈ {0,1}`；`spec_normalized` 不参与唯一性 |
| `material_attribute`（预留） | V1 可建表，**V1 业务不写入** |

### 3.3 冗余字段

- `material_item.category_code` 与所属 `material_group.category_code` 一致由 **创建路径** 保证（PRD 6.4.2）；**不以 `category_code` 作为关系主键**（关系以 `category_id` 为准）。

---

## 4. 领域与用例流程（摘要）

### 4.1 新建主料（A）

1. 校验分类存在；校验 `spec` 在分类内未占用（含已废弃行，仍占唯一——PRD 10.1）。
2. 事务内：`MAX(serial_no)+1` → 插入 `material_group` → 生成 A 件 `MaterialItem`（`spec_normalized = Normalize(description)`）→ 插入行。
3. 并发下 `UNIQUE(category_id, serial_no)` 冲突 → 事务回滚并重试（最多 3 次）。

### 4.2 新建替代料（B–Z）

1. 加载组快照与已有 suffix；**Domain** 校验连续性与下一后缀；溢出 → `SUFFIX_OVERFLOW`。
2. 校验同分类 `spec` 未重复；事务内插入新行；`UNIQUE(group_id, suffix)` 冲突 → 重试策略同主料思路。

### 4.3 废弃

- 将 `status` 置 0；**禁止物理删除**；废弃行仍占用 `UNIQUE(category_code, spec)`（PRD 10.1）。

### 4.4 搜索

- **编码**：前缀 `LIKE` 优先，不足再模糊 `LIKE`（PRD 7.3.1 / 15.3.1）。
- **规格**：`category_code` + `spec` / `spec_normalized` 子串 `LIKE`，**LIMIT 20**（PRD 6.4.5）。

### 4.5 Excel 导出（PRD 7.4）

- 仅 `status = 1`；`JOIN material_group`；按 `category_code` 分 Sheet；列顺序与 PRD 一致；排序 `category_code, serial_no, suffix`。

### 4.6 分类

- 新增分类；依赖 DB 唯一约束区分编码重复与名称重复（Application 映射为相应错误码）。

---

## 5. 验证与测试

### 5.1 自动化测试

- **Domain**：规范化、suffix、编码、实体行为等单元测试。
- **Application**：Mock 仓储的用例与重试行为。
- **Infrastructure**：SQLite 内存集成测试（创建、并发、搜索、导出等）。
- **Presentation**：关键 ViewModel 行为（如 keyword 来源、防抖配合下的搜索参数）在可测环境下用同步 Debouncer 验证。

### 5.2 YAML 黑盒验收

- `MaterialCodingSystem.Validation` 解析 `PRD_V1.yaml`，每 case 独立 SQLite；**`then.error.code` 与 YAML 不一致时视为冲突**（以 YAML 为验收事实源时的团队约定）。
- **当前状态（执行期结论）**：全量 Case 曾以 CLI 跑通且无 FAIL（具体 case 数以仓库内 YAML 为准）。

---

## 6. 与 `MCS_PRD_V1.md` 对照：完成度结论

### 6.1 已对齐（V1 主干与约束）

- 三表 ER、关键 UNIQUE、流水号仅 A 占用、替代料不占流水、suffix 连续与 Z 溢出、废弃与唯一性关系、`spec_normalized` 定义与三步规则、规格/编码搜索策略与 LIMIT、Excel 导出规则、分类唯一性、并发重试语义、V1 不做的相似度/NLP/结构化写入等。
- **10.6 V1 成功标准**（用 spec 搜索可定位、可从候选/搜索加入替代流程、可创建主料并生成编码）在能力层面**已覆盖**。
- **7.5 分类管理**：支持新增 + 唯一性（无强制要求独立「分类运营后台」时，可视为满足）。

### 6.2 部分对齐（交互或呈现与 PRD 字面存在差距）

| PRD 要点 | 说明 |
|----------|------|
| **9.1 新增分类成功后自动选中** | PRD 要求关窗后刷新并**自动选中新建分类**；若实现未保证 ComboBox 选中最新 code，则属**收尾级差距**。 |
| **9.1 候选行 → 物料详情弹窗** | PRD 示意详情窗 + 内嵌「作为替代料加入」；当前多为列表 + 决策条/行操作，**无独立详情窗**，属**交互形态差异**。 |
| **7.1 / 10.1：spec 不同但 spec_normalized 相同 → 仅提示** | PRD 要求**可提示、不阻止**；仅靠 LIKE 候选不一定等价于「归一化全等 + spec 不同」的**专门提示**；若产品坚持字面，需补 **Application 查询或 Hint DTO + UI 文案**。 |
| **9.2 编码搜索「实时」** | PRD 写输入即搜；若仍为按钮触发，则与**「实时」**措辞有差异，需 PRD 放宽或补防抖实时。 |
| **9.2 `SUFFIX_OVERFLOW` 展示位置** | PRD 写**表单顶部**；若实现为**弹窗**，与正文不完全一致（可与 dev-wpf 的弹窗建议统一口径）。 |
| **15.3.1 编码搜索 Top 20** | PRD 正文强调 Top 20；若 API 仍允许更大 limit，需统一**产品对外口径**（UI 固定 20 通常可接受）。 |

### 6.3 待澄清（非必然缺口）

- **10.3 状态管理**：除废弃外，V1 是否必须提供 **恢复 active（1）** 的用例与界面，PRD 未写死为独立功能；建议产品确认后定是否纳入 V1.1。
- **纠错流程 UI**：PRD 10.3 描述业务规则（废弃 + 新建）；是否要做**向导式界面**属体验增强，非约束性 DDL 缺口。

### 6.4 明确不在 V1（不记入未完成）

- OA、邮件解析、复杂相似度、向量检索、`material_attribute` 业务写入、Web 化等（PRD 10.5 / 十三 / 十四）。

---

## 7. 风险与维护建议（简）

- **SQLite 单写者模型**：仅适合低并发写入；高并发批量写入不在设计承诺内（PRD 第十一章）。
- **PRD 与 YAML**：若 YAML 为验收事实源，业务代码与 YAML 期望冲突时，应**显式升级 YAML 或修正实现**，避免静默改期望。
- **单仓储 vs 多仓储**：随代码量增长可考虑按 PRD 第十六章拆分，降低 SQL 维护成本。

---

## 8. 附录：关键路径索引

| 类别 | 路径（仓库内） |
|------|----------------|
| PRD | `docs/MCS_PRD_V1.md` |
| TDD | `docs/MCS_TDD_V1.md` |
| 本报告 | `docs/V1/MCS_V1_执行报告_评审用.md` |
| 验收规格 | `MaterialCodingSystem.Validation/specs/PRD_V1.yaml` |
| 主程序分层 | `MaterialCodingSystem/Application`、`Domain`、`Infrastructure`、`Presentation` |
| Schema | `MaterialCodingSystem/Infrastructure/Sqlite/SqliteSchema.cs` |
| 应用服务 | `MaterialCodingSystem/Application/MaterialApplicationService.cs` |

---

**评审结论建议**：

- 若以 **「业务约束 + YAML 全绿 + 核心用例可走通」** 为 V1 关门标准：**可通过**，剩余为 **9.x 体验与文案级** 对齐项。  
- 若以 **「9.1/9.2 与 PRD 示意图逐条一致」** 为关门标准：应将 **第六节「部分对齐」** 列为 **必须在 V1 收尾或 V1.1 排期** 的条目，并在 PRD 或验收清单中写清优先级。
