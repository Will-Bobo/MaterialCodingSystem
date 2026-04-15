using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using MaterialCodingSystem.Application.Contracts;
using MaterialCodingSystem.Application.Interfaces;
using Microsoft.Data.Sqlite;

namespace MaterialCodingSystem.Application;

public sealed class DatabaseBackupService
{
    private const int RetentionCount = 20;

    private readonly IBackupRepository _repo;
    private readonly IDatabasePathProvider _paths;
    private readonly IDatabaseConnectionCloser _dbCloser;
    private readonly MaintenanceOperationGate _gate;
    private readonly IRestoreReadOnlyLockNotifier? _restoreLock;

    public DatabaseBackupService(
        IBackupRepository repo,
        IDatabasePathProvider paths,
        IDatabaseConnectionCloser dbCloser,
        MaintenanceOperationGate gate,
        IRestoreReadOnlyLockNotifier? restoreLock = null)
    {
        _repo = repo;
        _paths = paths;
        _dbCloser = dbCloser;
        _gate = gate;
        _restoreLock = restoreLock;
    }

    public async Task<Result<DatabaseExportResponse>> ExportDatabase(string targetPath, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(targetPath))
            return Result<DatabaseExportResponse>.Fail(ErrorCodes.VALIDATION_ERROR, "target path is required.");

        var full = Path.GetFullPath(targetPath.Trim());
        if (!full.EndsWith(".db", StringComparison.OrdinalIgnoreCase))
            return Result<DatabaseExportResponse>.Fail(ErrorCodes.VALIDATION_ERROR, "target file must be .db.");

        var mainDbPath = Path.GetFullPath(_paths.GetMainDbPath());
        if (string.Equals(full, mainDbPath, StringComparison.OrdinalIgnoreCase))
            return Result<DatabaseExportResponse>.Fail(
                ErrorCodes.DB_EXPORT_TARGET_IS_MAIN_DB,
                "target path cannot be the main database file.");

        return await _gate.RunAsync(async () =>
        {
            try
            {
                if (File.Exists(full))
                {
                    try
                    {
                        File.Delete(full);
                    }
                    catch (Exception ex)
                    {
                        return Result<DatabaseExportResponse>.Fail(
                            ErrorCodes.DB_EXPORT_TARGET_DELETE_FAILED,
                            $"failed to delete existing target file: {ex.Message}");
                    }
                }

                await _repo.VacuumIntoAsync(full, ct);
                return Result<DatabaseExportResponse>.Ok(new DatabaseExportResponse(full));
            }
            catch (Exception ex)
            {
                return Result<DatabaseExportResponse>.Fail(ErrorCodes.INTERNAL_ERROR, $"export database failed: {ex.Message}");
            }
        }, ct);
    }

    public async Task<Result<AutoBackupResponse>> CreateAutoBackup(CancellationToken ct = default)
    {
        return await _gate.RunAsync(async () =>
        {
            try
            {
                var backupDir = _paths.GetBackupDirectory();
                var fileName = $"mcs_{DateTime.Now:yyyyMMdd_HHmmss}.db";
                var backupPath = Path.Combine(backupDir, fileName);

                await _repo.VacuumIntoAsync(backupPath, ct);

                // Cleanup: keep newest N
                var all = await _repo.ListBackupFilesAsync(ct);
                foreach (var old in all
                             .OrderByDescending(ExtractTimestamp)
                             .Skip(RetentionCount))
                {
                    try { await _repo.DeleteFileAsync(old, ct); } catch { /* ignore cleanup */ }
                }

                return Result<AutoBackupResponse>.Ok(new AutoBackupResponse(backupPath, RetentionCount));
            }
            catch (Exception ex)
            {
                // auto backup must not impact main flow; caller may ignore this result, but we still return failure.
                return Result<AutoBackupResponse>.Fail(ErrorCodes.INTERNAL_ERROR, $"auto backup failed: {ex.Message}");
            }
        }, ct);
    }

    public async Task<Result<DatabaseRestoreResponse>> RestoreDatabase(string path, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(path))
            return Result<DatabaseRestoreResponse>.Fail(ErrorCodes.VALIDATION_ERROR, "path is required.");

        var sourcePath = Path.GetFullPath(path.Trim());
        var mainDbPath = Path.GetFullPath(_paths.GetMainDbPath());

        // Step 1: validate
        if (string.Equals(sourcePath, mainDbPath, StringComparison.OrdinalIgnoreCase))
            return Result<DatabaseRestoreResponse>.Fail(ErrorCodes.DB_RESTORE_SOURCE_IS_CURRENT_DB, "selected path cannot be the current main database file.");

        if (!File.Exists(sourcePath))
            return Result<DatabaseRestoreResponse>.Fail(ErrorCodes.DB_RESTORE_SOURCE_INVALID, "restore source file does not exist.");

        if (!sourcePath.EndsWith(".db", StringComparison.OrdinalIgnoreCase))
            return Result<DatabaseRestoreResponse>.Fail(ErrorCodes.DB_RESTORE_SOURCE_INVALID, "restore source file must be .db.");

        if (!CanOpenSqliteDb(sourcePath))
            return Result<DatabaseRestoreResponse>.Fail(ErrorCodes.DB_RESTORE_SOURCE_INVALID, "restore source file is not a valid sqlite database.");

        // Step 2: global maintenance mutex (Export/AutoBackup/Restore)
        return await _gate.RunAsync(async () =>
        {
            var mainDir = Path.GetDirectoryName(mainDbPath) ?? ".";
            Directory.CreateDirectory(mainDir);

            var ts = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            var tmp = Path.Combine(mainDir, $"mcs.restore.{ts}.tmp");
            var bak = Path.Combine(mainDir, $"mcs.restore.{ts}.bak");

            // Step 3: close db connection (release file handle)
            try
            {
                await _dbCloser.CloseAsync(ct);
                // Ensure pooled connections (if any) release file handles before file replace.
                SqliteConnection.ClearAllPools();
            }
            catch (Exception ex)
            {
                return Result<DatabaseRestoreResponse>.Fail(ErrorCodes.DB_RESTORE_CLOSE_CONNECTION_FAILED, $"close database connection failed: {ex.Message}");
            }

            // Step 4: atomic replace
            try
            {
                // 1) copy to tmp (never write to main db directly)
                if (File.Exists(tmp)) File.Delete(tmp);
                File.Copy(sourcePath, tmp);

                // 2) validate tmp can be opened
                if (!CanOpenSqliteDb(tmp))
                {
                    try { File.Delete(tmp); } catch { /* ignore */ }
                    return Result<DatabaseRestoreResponse>.Fail(ErrorCodes.DB_RESTORE_SOURCE_INVALID, "restore temp file is not a valid sqlite database.");
                }

                // 3) main -> bak (keep one)
                // 4) tmp -> main (atomic replace)
                const int maxAttempts = 40;
                var replaced = false;
                for (var attempt = 1; attempt <= maxAttempts; attempt++)
                {
                    try
                    {
                        File.Replace(tmp, mainDbPath, bak, ignoreMetadataErrors: true);
                        replaced = true;
                        break;
                    }
                    catch (IOException ex) when (attempt < maxAttempts && ex.HResult == unchecked((int)0x80070020))
                    {
                        await Task.Delay(50, ct);
                    }
                }

                if (!replaced)
                    return Result<DatabaseRestoreResponse>.Fail(ErrorCodes.DB_RESTORE_REPLACE_FAILED, "restore replace failed: file replace retries exhausted.");

                _restoreLock?.EnterReadOnlyLock();
                return Result<DatabaseRestoreResponse>.Ok(new DatabaseRestoreResponse(
                    MainDbPath: mainDbPath,
                    BackupPath: bak,
                    RestartRequired: true
                ));
            }
            catch (Exception ex)
            {
                // Step 5: protection
                try { if (File.Exists(tmp)) File.Delete(tmp); } catch { /* ignore */ }
                return Result<DatabaseRestoreResponse>.Fail(ErrorCodes.DB_RESTORE_REPLACE_FAILED, $"restore replace failed: {ex.Message}");
            }
        }, ct);
    }

    private static DateTime ExtractTimestamp(string path)
    {
        try
        {
            var file = Path.GetFileNameWithoutExtension(path);
            if (string.IsNullOrWhiteSpace(file)) return DateTime.MinValue;
            if (!file.StartsWith("mcs_", StringComparison.OrdinalIgnoreCase)) return DateTime.MinValue;

            // mcs_yyyyMMdd_HHmmss
            var ts = file.Substring("mcs_".Length);
            if (ts.Length != 15) return DateTime.MinValue;
            if (ts[8] != '_') return DateTime.MinValue;

            var y = int.Parse(ts.Substring(0, 4));
            var m = int.Parse(ts.Substring(4, 2));
            var d = int.Parse(ts.Substring(6, 2));
            var hh = int.Parse(ts.Substring(9, 2));
            var mm = int.Parse(ts.Substring(11, 2));
            var ss = int.Parse(ts.Substring(13, 2));

            return new DateTime(y, m, d, hh, mm, ss, DateTimeKind.Unspecified);
        }
        catch
        {
            return DateTime.MinValue;
        }
    }

    private static bool CanOpenSqliteDb(string dbPath)
    {
        try
        {
            using var conn = new SqliteConnection($"Data Source={dbPath};Pooling=False");
            conn.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "PRAGMA quick_check;";
            var result = cmd.ExecuteScalar()?.ToString();
            return string.Equals(result, "ok", StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }

    // RestoreDatabase 已实现（Phase 3）；必须通过同一运维互斥入口执行。
}

