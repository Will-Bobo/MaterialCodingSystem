using MaterialCodingSystem.Application;
using MaterialCodingSystem.Application.Interfaces;
using System.IO;

namespace MaterialCodingSystem.Tests.Application;

public sealed class DatabaseBackupServiceTests
{
    [Fact]
    public async Task ExportDatabase_WhenPathMissing_ReturnsValidationError()
    {
        var svc = new DatabaseBackupService(new FakeBackupRepository(), new FakeDatabasePathProvider(), new FakeDatabaseConnectionCloser(), new MaintenanceOperationGate());
        var r = await svc.ExportDatabase("");
        Assert.False(r.IsSuccess);
        Assert.Equal(ErrorCodes.VALIDATION_ERROR, r.Error!.Code);
    }

    [Fact]
    public async Task ExportDatabase_WhenTargetIsMainDbPath_Returns_DB_EXPORT_TARGET_IS_MAIN_DB()
    {
        var main = Path.Combine(Path.GetTempPath(), $"mcs_main_{Guid.NewGuid():N}.db");
        var svc = new DatabaseBackupService(new FakeBackupRepository(), new FakeDatabasePathProvider(mainDbPath: main), new FakeDatabaseConnectionCloser(), new MaintenanceOperationGate());

        var r = await svc.ExportDatabase(main);
        Assert.False(r.IsSuccess);
        Assert.Equal(ErrorCodes.DB_EXPORT_TARGET_IS_MAIN_DB, r.Error!.Code);
    }

    [Fact]
    public async Task ExportDatabase_WhenVacuumIntoSucceeds_ReturnsOk()
    {
        var repo = new FakeBackupRepository();
        var main = Path.Combine(Path.GetTempPath(), $"mcs_main_{Guid.NewGuid():N}.db");
        var svc = new DatabaseBackupService(repo, new FakeDatabasePathProvider(mainDbPath: main), new FakeDatabaseConnectionCloser(), new MaintenanceOperationGate());

        var target = Path.Combine(Path.GetTempPath(), $"mcs_export_{Guid.NewGuid():N}.db");
        var r = await svc.ExportDatabase(target);

        Assert.True(r.IsSuccess);
        Assert.Equal(Path.GetFullPath(target), r.Data!.TargetPath);
        Assert.Equal(Path.GetFullPath(target), repo.LastVacuumTarget);
    }

    [Fact]
    public async Task ExportDatabase_WhenTargetExists_DeletesThenSucceeds()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"mcs_export_{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        var target = Path.Combine(tempDir, "out.db");
        File.WriteAllText(target, "old");

        try
        {
            var repo = new FakeBackupRepository { WriteFileOnVacuum = true };
            var main = Path.Combine(tempDir, "mcs.db");
            var svc = new DatabaseBackupService(repo, new FakeDatabasePathProvider(mainDbPath: main), new FakeDatabaseConnectionCloser(), new MaintenanceOperationGate());

            var r = await svc.ExportDatabase(target);
            Assert.True(r.IsSuccess);
            Assert.True(File.Exists(target));
            Assert.Equal("new", File.ReadAllText(target));
        }
        finally
        {
            try { Directory.Delete(tempDir, recursive: true); } catch { /* ignore */ }
        }
    }

    [Fact]
    public async Task ExportDatabase_WhenTargetFileLocked_Returns_DB_EXPORT_TARGET_DELETE_FAILED()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"mcs_export_lock_{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        var target = Path.Combine(tempDir, "out.db");
        File.WriteAllText(target, "old");

        try
        {
            using var fs = new FileStream(target, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
            var repo = new FakeBackupRepository { WriteFileOnVacuum = true };
            var main = Path.Combine(tempDir, "mcs.db");
            var svc = new DatabaseBackupService(repo, new FakeDatabasePathProvider(mainDbPath: main), new FakeDatabaseConnectionCloser(), new MaintenanceOperationGate());

            var r = await svc.ExportDatabase(target);
            Assert.False(r.IsSuccess);
            Assert.Equal(ErrorCodes.DB_EXPORT_TARGET_DELETE_FAILED, r.Error!.Code);
        }
        finally
        {
            try { Directory.Delete(tempDir, recursive: true); } catch { /* ignore */ }
        }
    }

    [Fact]
    public async Task ExportDatabase_WhenVacuumIntoThrows_ReturnsInternalError()
    {
        var repo = new FakeBackupRepository { ThrowOnVacuum = true };
        var main = Path.Combine(Path.GetTempPath(), $"mcs_main_{Guid.NewGuid():N}.db");
        var svc = new DatabaseBackupService(repo, new FakeDatabasePathProvider(mainDbPath: main), new FakeDatabaseConnectionCloser(), new MaintenanceOperationGate());

        var target = Path.Combine(Path.GetTempPath(), $"mcs_export_{Guid.NewGuid():N}.db");
        var r = await svc.ExportDatabase(target);

        Assert.False(r.IsSuccess);
        Assert.Equal(ErrorCodes.INTERNAL_ERROR, r.Error!.Code);
    }

    [Fact]
    public async Task CreateAutoBackup_DeletesOldBackups_KeepingLatest20()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"mcs_backup_test_{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);

        try
        {
            var repo = new FakeBackupRepository();
            repo.BackupFiles = new List<string>();

            // Create 25 backup files with distinct timestamps in file names (oldest -> newest)
            var start = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Unspecified);
            for (var i = 1; i <= 25; i++)
            {
                var t = start.AddSeconds(i);
                var p = Path.Combine(tempDir, $"mcs_{t:yyyyMMdd_HHmmss}.db");
                File.WriteAllText(p, "x");
                repo.BackupFiles.Add(p);
            }

            var svc = new DatabaseBackupService(repo, new FakeDatabasePathProvider(backupDir: tempDir), new FakeDatabaseConnectionCloser(), new MaintenanceOperationGate());
            var r = await svc.CreateAutoBackup();

            Assert.True(r.IsSuccess);
            Assert.Equal(5, repo.Deleted.Count);

            // Should delete the oldest 5 by timestamp in file name
            static DateTime ExtractTimestamp(string path)
            {
                try
                {
                    var file = Path.GetFileNameWithoutExtension(path);
                    if (string.IsNullOrWhiteSpace(file)) return DateTime.MinValue;
                    if (!file.StartsWith("mcs_", StringComparison.OrdinalIgnoreCase)) return DateTime.MinValue;

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

            var expectedOldest = repo.BackupFiles
                .OrderBy(ExtractTimestamp)
                .Take(5)
                .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
                .ToList();
            var actual = repo.Deleted
                .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
                .ToList();
            Assert.Equal(expectedOldest, actual);
        }
        finally
        {
            try { Directory.Delete(tempDir, recursive: true); } catch { /* ignore */ }
        }
    }

    [Fact]
    public async Task RestoreDatabase_WhenPathEqualsMainDb_Returns_DB_RESTORE_SOURCE_IS_CURRENT_DB()
    {
        var p = Path.Combine(Path.GetTempPath(), $"mcs_{Guid.NewGuid():N}.db");
        var svc = new DatabaseBackupService(new FakeBackupRepository(), new FakeDatabasePathProvider(mainDbPath: p), new FakeDatabaseConnectionCloser(), new MaintenanceOperationGate());
        var r = await svc.RestoreDatabase(p);
        Assert.False(r.IsSuccess);
        Assert.Equal(ErrorCodes.DB_RESTORE_SOURCE_IS_CURRENT_DB, r.Error!.Code);
    }

    [Fact]
    public async Task CreateAutoBackup_WhenVacuumIntoThrows_ReturnsInternalError_AndDoesNotThrow()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"mcs_backup_test_{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);

        try
        {
            var repo = new FakeBackupRepository { ThrowOnVacuum = true };
            var svc = new DatabaseBackupService(repo, new FakeDatabasePathProvider(backupDir: tempDir), new FakeDatabaseConnectionCloser(), new MaintenanceOperationGate());
            var r = await svc.CreateAutoBackup();

            Assert.False(r.IsSuccess);
            Assert.Equal(ErrorCodes.INTERNAL_ERROR, r.Error!.Code);
        }
        finally
        {
            try { Directory.Delete(tempDir, recursive: true); } catch { /* ignore */ }
        }
    }

    private sealed class FakeDatabaseConnectionCloser : IDatabaseConnectionCloser
    {
        public Task CloseAsync(CancellationToken ct = default) => Task.CompletedTask;
    }

    private sealed class FakeDatabasePathProvider : IDatabasePathProvider
    {
        private readonly string _mainDbPath;
        private readonly string _backupDir;

        public FakeDatabasePathProvider(string? mainDbPath = null, string? backupDir = null)
        {
            _mainDbPath = mainDbPath ?? @"D:\data\mcs.db";
            _backupDir = backupDir ?? @"D:\b";
        }

        public string GetMainDbPath() => _mainDbPath;
        public string GetBackupDirectory() => _backupDir;
    }

    private sealed class FakeBackupRepository : IBackupRepository
    {
        public string? LastVacuumTarget { get; private set; }
        public bool ThrowOnVacuum { get; set; }
        public bool WriteFileOnVacuum { get; set; }
        public List<string> BackupFiles { get; set; } = new();
        public List<string> Deleted { get; } = new();

        public Task VacuumIntoAsync(string targetPath, CancellationToken ct = default)
        {
            if (ThrowOnVacuum)
                throw new InvalidOperationException("boom");
            LastVacuumTarget = targetPath;
            if (WriteFileOnVacuum)
            {
                var dir = Path.GetDirectoryName(targetPath);
                if (!string.IsNullOrWhiteSpace(dir)) Directory.CreateDirectory(dir);
                File.WriteAllText(targetPath, "new");
            }
            return Task.CompletedTask;
        }

        public Task<List<string>> ListBackupFilesAsync(CancellationToken ct = default)
            => Task.FromResult(BackupFiles.ToList());

        public Task DeleteFileAsync(string path, CancellationToken ct = default)
        {
            Deleted.Add(path);
            return Task.CompletedTask;
        }
    }
}

