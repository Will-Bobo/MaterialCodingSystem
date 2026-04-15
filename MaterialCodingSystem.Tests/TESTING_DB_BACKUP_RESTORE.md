# DB 备份 / 导出 / 恢复 — 测试说明（Phase 5）

本说明用于配合 Phase 5 测试交付，覆盖：

- 单元测试（Mock/Fake）
- 集成测试（真实 SQLite）
- 手动测试场景清单

---

## 1. 单元测试（Mock/Fake）

文件：`MaterialCodingSystem.Tests/Application/DatabaseBackupServiceTests.cs`

覆盖点：

- **ExportDatabase**
  - 路径缺失 → `VALIDATION_ERROR`
  - 目标路径等于主库 → `DB_EXPORT_TARGET_IS_MAIN_DB`
  - **成功**：调用 `VacuumIntoAsync(targetPath)`，返回 `TargetPath`
  - **失败**：`VacuumIntoAsync` 抛异常 → 返回 `INTERNAL_ERROR`
- **CreateAutoBackup**
  - **Cleanup**：超过 20 份时，删除最旧的多余备份（已有用例覆盖）
  - **失败**：`VacuumIntoAsync` 抛异常 → 返回 `INTERNAL_ERROR`
- **RestoreDatabase（轻量校验）**
  - 选择路径等于主库 → `DB_RESTORE_SOURCE_IS_CURRENT_DB`

说明：

- 单元测试不做真实 IO/SQLite 行为验证，遵循当前阶段 `meta.testing_scope_note` 的“轻量契约/规则冻结”边界。

---

## 2. 集成测试（真实 SQLite）

文件：`MaterialCodingSystem.Tests/Application/DatabaseBackupServiceIntegrationTests.cs`

覆盖点：

- **VACUUM INTO 生成 DB**
  - 在源库创建表/写入数据
  - 调用 `ExportDatabase` 生成导出库文件
  - 打开导出库并读取数据校验
- **Restore 覆盖后数据正确**
  - 生成 main.db（内容 before）
  - 生成 source.db（内容 after）
  - 调用 `RestoreDatabase(source.db)`
  - 打开 main.db 校验数据为 after
  - 校验返回 `RestartRequired=true`，且产生 `.bak` 文件

说明：

- 集成测试只使用本地临时目录，不依赖固定路径。
- 校验连接使用 `Pooling=False`，避免文件句柄残留影响替换。

---

## 3. 手动测试场景（必须走一轮）

### 3.1 正常备份/导出

- 点击“导出数据库”
- 选择保存路径
- 期望：生成 `.db` 文件可用（可用 DB Browser for SQLite 打开；或再次“恢复数据库”验证）

### 3.2 多次启动（自动备份）

- 连续启动应用 3 次
- 期望：备份目录新增 3 个备份文件（命名 `mcs_yyyyMMdd_HHmmss.db`）
- 期望：超过保留数量后旧文件被清理（保持最近 20 份）

### 3.3 Restore 成功

- 点击“恢复数据库”
- 选择一个备份 `.db`
- 二次确认对话框出现（显示当前 DB 时间、备份文件时间）
- 确认后执行恢复
- 期望：提示恢复成功并自动重启；重启后数据回到备份时刻

### 3.4 Restore 失败（权限/占用）

- 让主库被外部进程占用（例如用 DB Browser 打开并保持连接）
- 触发恢复
- 期望：恢复失败可感知（出现失败提示），不出现 silent success
- 期望：当前库文件保持可用（不会被破坏）

### 3.5 磁盘不足

- 将备份/导出目标目录指向空间不足的磁盘或配额目录
- 触发导出/自动备份
- 期望：失败不影响主流程（导出失败仅提示；自动备份失败仅记录/不阻断）

