using System.IO;
using Dapper;
using MaterialCodingSystem.Application;
using MaterialCodingSystem.Application.Interfaces;
using MaterialCodingSystem.Infrastructure.Sqlite;
using Microsoft.Data.Sqlite;

namespace MaterialCodingSystem.Tests.Application;

public sealed class DatabaseBackupServiceIntegrationTests
{
    [Fact]
    public async Task ExportDatabase_VacuumInto_GeneratesRestorableDb()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"mcs_dbops_{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);

        var mainDbPath = Path.Combine(tempDir, "main.db");
        var exportPath = Path.Combine(tempDir, "export.db");

        try
        {
            await using (var conn = new SqliteConnection($"Data Source={mainDbPath}"))
            {
                await conn.OpenAsync();
                await conn.ExecuteAsync("CREATE TABLE t(id INTEGER PRIMARY KEY, v TEXT);");
                await conn.ExecuteAsync("INSERT INTO t(v) VALUES ('main');");

                var paths = new TestPaths(mainDbPath, backupDir: tempDir);
                var repo = new SqliteBackupRepository(conn, paths);
                var svc = new DatabaseBackupService(repo, paths, new NoopCloser(), new MaintenanceOperationGate());

                var res = await svc.ExportDatabase(exportPath);
                Assert.True(res.IsSuccess);
            }

            Assert.True(File.Exists(exportPath));
            await using var verify = new SqliteConnection($"Data Source={exportPath};Pooling=False");
            await verify.OpenAsync();
            var v = await verify.ExecuteScalarAsync<string>("SELECT v FROM t LIMIT 1;");
            Assert.Equal("main", v);
        }
        finally
        {
            try { Directory.Delete(tempDir, recursive: true); } catch { /* ignore */ }
        }
    }

    [Fact]
    public async Task RestoreDatabase_ReplacesMainDb_AndDataMatchesBackup()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"mcs_dbops_{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);

        var mainDbPath = Path.Combine(tempDir, "main.db");
        var sourcePath = Path.Combine(tempDir, "source.db");

        try
        {
            // main db seed
            await using (var main = new SqliteConnection($"Data Source={mainDbPath}"))
            {
                await main.OpenAsync();
                await main.ExecuteAsync("CREATE TABLE t(id INTEGER PRIMARY KEY, v TEXT);");
                await main.ExecuteAsync("INSERT INTO t(v) VALUES ('before');");
            }

            // source/backup db seed
            await using (var src = new SqliteConnection($"Data Source={sourcePath}"))
            {
                await src.OpenAsync();
                await src.ExecuteAsync("CREATE TABLE t(id INTEGER PRIMARY KEY, v TEXT);");
                await src.ExecuteAsync("INSERT INTO t(v) VALUES ('after');");
            }

            var paths = new TestPaths(mainDbPath, backupDir: tempDir);
            await using var opConn = new SqliteConnection($"Data Source={mainDbPath}");
            var repo = new SqliteBackupRepository(opConn, paths);
            var restoreLock = new TestRestoreLock();
            var svc = new DatabaseBackupService(repo, paths, new NoopCloser(), new MaintenanceOperationGate(), restoreLock);

            var res = await svc.RestoreDatabase(sourcePath);
            Assert.True(res.IsSuccess);
            Assert.True(res.Data!.RestartRequired);
            Assert.True(restoreLock.Locked);
            Assert.True(File.Exists(res.Data.BackupPath));

            await using var verify = new SqliteConnection($"Data Source={mainDbPath};Pooling=False");
            await verify.OpenAsync();
            var v = await verify.ExecuteScalarAsync<string>("SELECT v FROM t LIMIT 1;");
            Assert.Equal("after", v);
        }
        finally
        {
            try { Directory.Delete(tempDir, recursive: true); } catch { /* ignore */ }
        }
    }

    private sealed class NoopCloser : IDatabaseConnectionCloser
    {
        public Task CloseAsync(CancellationToken ct = default) => Task.CompletedTask;
    }

    private sealed class TestPaths : IDatabasePathProvider
    {
        private readonly string _main;
        private readonly string _backupDir;
        public TestPaths(string mainDbPath, string backupDir) { _main = mainDbPath; _backupDir = backupDir; }
        public string GetMainDbPath() => _main;
        public string GetBackupDirectory() => _backupDir;
    }

    private sealed class TestRestoreLock : IRestoreReadOnlyLockNotifier
    {
        public bool Locked { get; private set; }
        public void EnterReadOnlyLock() => Locked = true;
    }
}

