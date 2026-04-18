# MCS 日志系统实施方案 V1.2.1（最终可施工版）

---

## 0. 规范优先级（冲突裁决）

当不同章节或清单条目含义冲突时，按下述优先级从高到低适用；**低优先级服从高优先级**。  
**若本文 V1.2 条款与 V1.2.1 修订冲突，以 V1.2.1 为准。**

| 优先级 | 名称 | 说明 |
|--------|------|------|
| 1 | 日志策略层（统一语义模型） | 本文第 5、7、12 节：双轴模型、EventLog、Failed 双模式、Exception 去重 |
| 2 | 字段 Schema 规范 | 本文第 7 节：字段集合、Start/结束态、`action` 命名 |
| 3 | 日志等级规范 | 本文第 8 节：`LogLevel`、查询类 IF/THEN |
| 4 | 埋点清单 V2（执行清单） | 本文第 11 节：方法定位 + `action_name`；与 1～3 冲突时以 1～3 为准 |

---

## 1. 文档目的

本方案定义 MaterialCodingSystem（WPF / .NET 8）**工程级**日志规范，使 Cursor 与开发人员能在**无歧义**前提下完成 Phase2～Phase4 改造与验收。

---

## 2. 实施原则

### 2.1 架构与边界

| 层级 | 职责 |
|------|------|
| Presentation | UI 生命周期、用户触发的入口；不写业务规则实现细节 |
| Application | 用例编排、事务边界、业务事件（Start / Success / Blocked / Failed） |
| Infrastructure | 存储、文件、外部 IO；**技术异常在此层记录 Error Log（含 stack）的首选责任** |
| Domain | **禁止**输出日志 |

### 2.2 禁止日志污染

以下**不得**出现在任何日志消息或结构化字段中：

- SQL 全文、连接串、密钥；文件路径遵守 PRD/脱敏规则
- 完整 DTO / Entity / 领域对象 dump
- `List` / `Dictionary` / 集合作为字段值（**允许**标量计数，如 `line_count`）
- 用户输入全文、超长 description
- 凭据与个人隐私

### 2.3 统一管道

业务侧统一使用 **Serilog + `ILogger<T>`**；配置与落盘见第 4 节。

---

## 3. 技术选型（强制）

| 项目 | 选型 |
|------|------|
| API | Microsoft.Extensions.Logging.Abstractions：`ILogger<T>` |
| 实现 | Serilog：结构化属性 + 文件滚动 |
| 注入 | 构造函数注入 `ILogger<T>` |
| 命名 | `Log.*` 区域由类名自然区分，禁止复制粘贴跨类 logger |

---

## 4. 日志目录与 Serilog 初始化（与实现对齐）

| 项 | 规则 |
|----|------|
| 根目录 | `%LocalAppData%\MaterialCodingSystem\logs` |
| 文本日志 | `mcs-.txt`，按日滚动，保留策略与代码中 `RollingInterval`、`retainedFileCountLimit` 一致 |
| 启动 | 使用 `Host.CreateApplicationBuilder` / `AddLogging` 时 **`ClearProviders()`** 后 **`AddSerilog(...)`** |
| 关闭 | 应用退出路径调用 **`Log.CloseAndFlush()`** |
| 初始化失败 | **不得**阻止应用启动；仅尽力写入启动失败信息 |

全局未处理异常（若已实现则保持）：`DispatcherUnhandledException`、`AppDomain.UnhandledException`、`TaskScheduler.UnobservedTaskException` — 记为 **技术失败路径**：**Failed Event Log** + **Error Log**（见第 5.4.3、5.4.4 节），与纯业务 Failed 区分。

---

## 5. 双轴日志语义模型（强制）

### 5.1 技术维度：`LogLevel`

仅使用：`Debug`、`Information`、`Warning`、`Error`。  
**禁止**使用 `Critical` / `Fatal` 作为常规业务事件等级。

### 5.2 业务维度：`EventState`

用于 **Event Log**，取值：`Success` | `Blocked` | `Failed`。  
生命周期（非 `LogLevel`）：**Start**（起点）与结束态 **Success / Blocked / Failed**。

### 5.3 强制映射（结束态 EventState → LogLevel）

| EventState | LogLevel |
|------------|----------|
| Success | Information |
| Blocked | Warning |
| Failed | Error |

**例外**：第 8 节对 `SEARCH_ACTIONS` 的 Start/Success 仅降低 `LogLevel`，**不改变** `EventState` 语义。

### 5.4 Error Log、Event Log 与 Failed 双模式

#### 5.4.1 定义

| 术语 | 判定 |
|------|------|
| **Event Log** | 结构化业务事件：`action`、`state` 或流水线阶段、计时与 `primary_id` / `error_code` / `extension` 等，**不含**异常栈 |
| **Error Log** | 调用带 `Exception` 的 `LogError`（或 Serilog 等价），输出 **stack trace** |

#### 5.4.2 Failed — 业务失败（Business Failed）

| 项 | 规则 |
|----|------|
| 特征 | **有** `error_code`；**无**待记录的 `Exception`（无 catch 技术异常） |
| 示例错误码 | `SPEC_DUPLICATE`、`NOT_FOUND`、`CATEGORY_NOT_FOUND`、`VALIDATION_ERROR` 及项目已有业务码 |
| 输出 | **仅** Failed Event Log（`error_code` 必填） |
| 禁止 | **禁止**输出 Error Log（**禁止**无异常的 stack） |

#### 5.4.3 Failed — 技术失败（Technical Failed）

| 项 | 规则 |
|----|------|
| 特征 | `catch (Exception)`、IO/DB/未预期异常等技术原因 |
| 输出 | **必须**：① Failed Event Log（`error_code` 必填）；② Error Log（`Exception` + stack trace） |
| 去重 | **必须**遵守第 12 节：整条调用链 stack trace **最多一次** |

#### 5.4.4 与 Event Log Schema 的关系

- 结束态 Event Log 的 **`duration_ms`** 必填（整段用例毫秒，不可用则填 `0` 并在第 14 节 helper 内单一策略，**禁止**在 Start 写 `duration_ms`，见第 7 节）。  
- **禁止**单独使用 Error Log 替代 Failed Event Log；技术失败必须 **Failed Event Log + Error Log**（在未被第 12 节免除栈的前提下）。

---

## 6. 分层日志职责（硬规则）

| 层级 | Event Log | Error Log（栈） |
|------|-----------|-----------------|
| Presentation | 入口 **Start**；结束态转发 | **业务 Failed**：不要栈。**技术 Failed**：仅当下层未记栈时记 **一次** Error Log |
| Application | **Start** / **Success** / **Blocked** / **Failed** | **业务 Failed**：不要栈。**技术 Failed**：遵循第 12 节决定是否在本层输出栈 |
| Infrastructure | 可选 **Start**；失败优先记栈 | **技术失败首选**在本层输出 Error Log（栈） |
| Domain | **禁止** | **禁止** |

**与第 5.4 节对齐**：业务 Failed 在任何层均 **仅** Event Log；技术 Failed 按层与第 12 节组合 **Event + Error**。

---

## 7. Event Log 结构化模型（强制）

### 7.1 `action` 命名（强制）

| 规则 | 内容 |
|------|------|
| 写入日志的字段名 | 固定为 **`action`** |
| 取值格式 | **`{module}.{verb_object}`**，全小写 **`snake_case`** |
| 第 11 节 `Method` / `Class.Method` | **仅用于定位源码**，**不得**作为 `action` 取值 |
| 取值来源 | **必须**与第 11 节 **`action_name`** 列**逐字一致**；**禁止**自定义或拼写变体 |

### 7.2 结束态事件（Success / Blocked / Failed）

```
必须：action，state（Success|Blocked|Failed），duration_ms
可选：primary_id，extension（≤1 个键值对，标量值）
Success：禁止 error_code
Blocked / Failed：error_code 必填
```

### 7.3 Start 事件（强制）

| 类别 | 规则 |
|------|------|
| **必须字段** | `action` |
| **可选字段** | `primary_id` |
| **禁止字段** | `duration_ms`，`error_code`，`extension` |

`duration_ms` **仅**出现在结束态（Success / Blocked / Failed）Event Log。

**实现路径（全项目统一其一）**：

| 路径 | 规则 |
|------|------|
| A | 独立 **LogStart**（见第 14 节）：只打 `action`、可选 `primary_id` |
| B | 单一结构体：`pipeline_event=Start` 时 **不得** 出现 `duration_ms` / `error_code` / `extension` |

### 7.4 `extension`

最多 **1** 个键；值为 bool / int / long / string 等标量；禁止嵌套与集合。

### 7.5 禁止字段（全局）

禁止在日志参数中出现：SQL 全文、DTO/Entity 整体 dump、集合整体、description/大段用户输入全文。

---

## 8. 高频路径：`LogLevel` IF/THEN（不改变 EventState）

**常量**：`QUERY_SLOW_MS = 100`

**集合 `SEARCH_ACTIONS`（取值必须与第 11 节 `action` 字符串一致）**：

- `material.search_by_code`
- `material.search_by_spec`
- `material.search_candidates_by_spec_only`
- `material.search_by_spec_all`

| IF | THEN |
|----|------|
| `action` ∈ `SEARCH_ACTIONS` **且** **Start** | `LogLevel = Debug` |
| `action` ∈ `SEARCH_ACTIONS` **且** **Success** **且** `duration_ms < QUERY_SLOW_MS` | `LogLevel = Debug` |
| `action` ∈ `SEARCH_ACTIONS` **且** **Success** **且** `duration_ms >= QUERY_SLOW_MS` | `LogLevel = Information` |
| `action` ∉ `SEARCH_ACTIONS` **且** **Start** | `LogLevel = Information` |
| `action` ∉ `SEARCH_ACTIONS` **且** **Success** | `LogLevel = Information` |
| **Blocked** | `LogLevel = Warning` |
| **Failed** | `LogLevel = Error` |

---

## 9. 验收标准（强制）

| 编号 | 条件 |
|------|------|
| AC-01 | `%LocalAppData%\MaterialCodingSystem\logs` 下存在文本日志文件 |
| AC-02 | 正常关闭后 `Log.CloseAndFlush()`（或等价），日志落盘完整 |
| AC-03 | Application 对外用例具备 **Start + 结束态**（第 11 节） |
| AC-04 | Start **无** `duration_ms` / `error_code` / `extension`；结束态符合第 7.2 节 |
| AC-05 | `action` 与第 11 节 `action_name` **完全一致** |
| AC-06 | 禁止内容（第 2.2、7.5 节）抽检通过 |
| AC-07 | 业务 Failed **无** Error Log；技术 Failed **有** Failed Event Log，栈去重符合第 12 节 |

---

## 10. 实施前评估（Cursor 自检）

- [ ] Serilog、目录、滚动符合第 4 节  
- [ ] 全局异常挂钩符合第 5.4.3 节  
- [ ] 已读第 0、5、7、8、12、14 节并可实现  
- [ ] 第 11 节与当前 `Application` 代码一致  
- [ ] 第 14 节 helper 已落地或列入 Phase 2 首包

---

## 11. 埋点清单 V2（执行清单）

**列说明**：

| 列 | 含义 |
|----|------|
| **Method** | **仅**用于定位源码（PascalCase / `Class.Method`），**不作为**日志 `action` |
| **action_name** | 写入日志字段 **`action`** 的唯一合法值，**禁止**改写 |
| **Start / Success / Blocked / Failed** | 是否打点 |

**共用规则**：`primary_id` 若填写，UTF-8 字节长度 **≤ 128**；标量拼接用 `|`；**Blocked** 列 `-` 表示不适用。

### 11.1 `MaterialApplicationService`

| Method | action_name | Start | Success | Blocked | Failed | primary_id | extension |
|--------|---------------|-------|---------|---------|--------|------------|-----------|
| CreateCategory | `material.create_category` | ✓ | ✓ | - | ✓ | `category_code` | - |
| ListCategories | `material.list_categories` | ✓ | ✓ | - | ✓ | - | `count`（Success） |
| ResolveGroupIdByItemCode | `material.resolve_group_id_by_item_code` | ✓ | ✓ | - | ✓ | `item_code` | - |
| GetGroupInfo | `material.get_group_info` | ✓ | ✓ | - | ✓ | `group_id` | - |
| AllocateNextGroupSerial | `material.allocate_next_group_serial` | ✓ | ✓ | - | ✓ | `category_code` | `allocated_serial`（Success） |
| CreateMaterialItemA | `material.create_material_item_a` | ✓ | ✓ | - | ✓ | `category_code` | - |
| CreateMaterialItemManual | `material.create_material_item_manual` | ✓ | ✓ | - | ✓ | `category_code` | - |
| CreateReplacement | `material.create_replacement` | ✓ | ✓ | - | ✓ | `source_item_code` | - |
| CreateReplacementByCode | `material.create_replacement_by_code` | ✓ | ✓ | - | ✓ | `source_item_code` | - |
| DeprecateMaterialItem | `material.deprecate_material_item` | ✓ | ✓ | - | ✓ | `item_code` | - |
| SearchByCode | `material.search_by_code` | ✓ | ✓ | - | ✓ | `category_code\|keyword` | `page_size` |
| SearchBySpec | `material.search_by_spec` | ✓ | ✓ | - | ✓ | `category_code` | `page_size` |
| SearchCandidatesBySpecOnlyAsync | `material.search_candidates_by_spec_only` | ✓ | ✓ | - | ✓ | `category_code` | `page_size` |
| SearchBySpecAllAsync | `material.search_by_spec_all` | ✓ | ✓ | - | ✓ | `category_code` | `page_size` |
| ExportActiveMaterials | `material.export_active_materials` | ✓ | ✓ | - | ✓ | `file_path`（脱敏） | `exported_row_count` |

### 11.2 BOM 与分析

| Class.Method | action_name | Start | Success | Blocked | Failed | primary_id | extension |
|--------------|-------------|-------|---------|---------|--------|------------|-----------|
| AnalyzeBomUseCase.ExecuteAsync | `bom.analyze` | ✓ | ✓ | - | ✓ | `bom_file_path`（脱敏） | `line_count` |
| ParseBomUseCase.Execute | `bom.parse` | ✓ | ✓ | - | ✓ | `bom_file_path`（脱敏） | `line_count` |
| ImportBomNewMaterialsUseCase.ExecuteAsync | `bom.import_new_materials` | ✓ | ✓ | ✓ | ✓ | `bom_file_path`（脱敏） | `inserted_count`（Success） |
| CanArchiveBomUseCase.ExecuteAsync | `bom.can_archive` | ✓ | ✓ | - | ✓ | `bom_file_path`（脱敏） | `is_allowed`（Success） |
| ArchiveBomUseCase.ExecuteAsync | `bom.archive` | ✓ | ✓ | ✓ | ✓ | `bom_file_path`（脱敏） | - |
| BomArchiveService.ArchiveAsync | `bom.archive_service` | ✓ | ✓ | - | ✓ | `bom_file_path`（脱敏） | - |
| GetBomArchiveListUseCase.ExecuteAsync | `bom.list_archive` | ✓ | ✓ | - | ✓ | `archive_root`（脱敏） | `entry_count` |
| ValidateBomArchiveIntegrityUseCase.ExecuteAsync | `bom.validate_archive_integrity` | ✓ | ✓ | - | ✓ | `archive_root`（脱敏） | `invalid_count` |
| ConfigureBomArchiveRootPathUseCase.ExecuteAsync | `bom.configure_archive_root` | ✓ | ✓ | - | ✓ | `archive_root`（脱敏） | - |

### 11.3 数据库备份 / 启动 / 门禁

| Class.Method | action_name | Start | Success | Blocked | Failed | primary_id | extension |
|--------------|-------------|-------|---------|---------|--------|------------|-----------|
| DatabaseBackupService.ExportDatabase | `backup.export_database` | ✓ | ✓ | - | ✓ | `target_path`（脱敏） | - |
| DatabaseBackupService.CreateAutoBackup | `backup.create_auto_backup` | ✓ | ✓ | - | ✓ | - | `backup_path`（脱敏，Success） |
| DatabaseBackupService.RestoreDatabase | `backup.restore_database` | ✓ | ✓ | - | ✓ | `path`（脱敏） | - |
| StartupOrchestrationService.OnAppStartedAsync | `system.app_started` | ✓ | ✓ | - | ✓ | - | `db_version`（可得时） |
| MaintenanceOperationGate.RunAsync | `system.maintenance_gate` | ✓ | ✓ | ✓ | ✓ | `gate_name` | - |

---

## 12. Exception 去重规则（强制）

**定义「已记录异常栈」**：本调用链中已执行带 `Exception` 的 `LogError`（或等价）并输出 stack trace。

**优先级（从高到低）**：Infrastructure → Application → Presentation。

| 条件 | 行为 |
|------|------|
| 下层已记录异常栈 | 上层 **仅** Failed Event Log（`error_code`），**禁止**再次输出同一异常的栈 |
| 仅 Presentation 捕获且下层无栈 | Presentation：**Failed Event Log** + **Error Log（一次）** |

**与第 5.4 节关系**：

- **业务 Failed**：全层 **禁止** Error Log，**不适用**本条栈去重（无栈）。  
- **技术 Failed**：本条约束 **Error Log** 次数；Failed Event Log 仍按层输出，**除非**架构约定由一层统一收口（须在代码中一致，且仍须满足「栈一次」）。

**判定**：同一 `exception` 引用链上 stack **只打印一次**。

---

## 13. 实施顺序（可直接执行）

### Phase 1（已完成）

- Serilog 接入、启动日志、全局异常、`Log.CloseAndFlush()`

### Phase 2

1. 落地第 **14** 节 **Event Log / Error Log 分离的 helper**（单一实现源）  
2. Application：按第 11 节 **`action_name`** 为全部方法接入 **Start + 结束态**  
3. 字段与错误码对齐第 7 节、`Result` / 项目错误码枚举

### Phase 3

- Infrastructure：技术失败路径补齐 Error Log；与 Application 满足第 12 节

### Phase 4

- 第 8 节 `SEARCH_ACTIONS` 验证、重复日志与字段违规扫描、第 9 节验收

---

## 14. 推荐实现方式（Cursor 执行模板）

**目的**：单一实现点；**禁止**在各 UseCase 内复制粘贴日志拼接。

**任选一种承载形式**：静态类 **`McsLoggingExtensions`**（扩展 `ILogger`）或 **`IMcsLoggingService`**（封装下列行为）；项目内 **只保留一种**，全 Application 统一调用。

### 14.1 最小 API 形状（签名可与下述等价，语义不得删减）

```
LogStart(ILogger logger, string action, string? primaryId = null)

LogSuccess(ILogger logger, string action, long durationMs, string? primaryId = null, (string key, object value)? extension = null)

LogBlocked(ILogger logger, string action, string errorCode, long durationMs, string? primaryId = null)

LogFailed(ILogger logger, string action, string errorCode, long durationMs, string? primaryId = null)

LogException(ILogger logger, Exception ex, string action, string errorCode)
```

### 14.2 语义绑定

| 方法 | 用途 |
|------|------|
| `LogStart` | Start：仅 `action`、可选 `primary_id`；**不得**写入 `duration_ms` / `error_code` / `extension` |
| `LogSuccess` | Success Event Log；`extension` 最多一组键值，标量 |
| `LogBlocked` | Blocked Event Log |
| `LogFailed` | **所有** Failed Event Log（业务 + 技术均**先**打此条，带 `error_code`） |
| `LogException` | **仅技术失败**：在 `LogFailed` **之后**调用（若本层允许输出栈）；**业务失败禁止调用** |

### 14.3 技术失败调用序

1. `LogFailed`（Failed Event Log）  
2. 若本层有权记栈且下层未记（第 12 节）→ `LogException`  
3. 若下层已记栈 → **跳过** `LogException`

### 14.4 查询类 `LogLevel`

`LogSuccess` / `LogStart` 内部按第 8 节对 `SEARCH_ACTIONS` 的 `action` 调整 `LogLevel`，**不得**在各方法内手写分支。

---

**文档版本**：V1.2.1（最终可施工版）  
**替代**：替代 V1.2 及更早版本；以第 0 节优先级为准。
