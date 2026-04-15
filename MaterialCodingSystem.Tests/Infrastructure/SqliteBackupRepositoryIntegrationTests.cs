using System.IO;
using Dapper;
using MaterialCodingSystem.Infrastructure.Sqlite;
using Microsoft.Data.Sqlite;

namespace MaterialCodingSystem.Tests.Infrastructure;

public sealed class SqliteBackupRepositoryIntegrationTests
{
    [Fact]
    public async Task VacuumIntoAsync_CreatesDbFile_AndCanBeOpened()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"mcs_backup_{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);

        var sourcePath = Path.Combine(tempDir, "src.db");
        var targetPath = Path.Combine(tempDir, "out.db");

        try
        {
            await using (var src = new SqliteConnection($"Data Source={sourcePath}"))
            {
                await src.OpenAsync();
                await src.ExecuteAsync("CREATE TABLE t(id INTEGER PRIMARY KEY, v TEXT);");
                await src.ExecuteAsync("INSERT INTO t(v) VALUES ('x');");

                var repo = new SqliteBackupRepository(src, new TestPaths(tempDir));
                await repo.VacuumIntoAsync(targetPath);
            }

            Assert.True(File.Exists(targetPath));
            Assert.True(new FileInfo(targetPath).Length > 0);

            await using var verify = new SqliteConnection($"Data Source={targetPath}");
            await verify.OpenAsync();
            var name = await verify.ExecuteScalarAsync<string>("SELECT name FROM sqlite_master WHERE type='table' AND name='t' LIMIT 1;");
            Assert.Equal("t", name);
        }
        finally
        {
            try { Directory.Delete(tempDir, recursive: true); } catch { /* ignore */ }
        }
    }

    private sealed class TestPaths : MaterialCodingSystem.Application.Interfaces.IDatabasePathProvider
    {
        private readonly string _dir;
        public TestPaths(string dir) { _dir = dir; }
        public string GetMainDbPath() => Path.Combine(_dir, "src.db");
        public string GetBackupDirectory() => _dir;
    }
}

