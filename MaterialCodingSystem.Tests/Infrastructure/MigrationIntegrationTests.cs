using Dapper;
using MaterialCodingSystem.Infrastructure.Sqlite;
using Microsoft.Data.Sqlite;

namespace MaterialCodingSystem.Tests.Infrastructure;

public sealed class MigrationIntegrationTests
{
    [Fact]
    public async Task LegacyDb_WithoutStartSerialNo_IsUpgradedToV3_Idempotent()
    {
        var name = $"mcs_mig_{Guid.NewGuid():N}";
        var cs = $"Data Source=file:{name}?mode=memory&cache=shared";
        await using var conn = new SqliteConnection(cs);
        await conn.OpenAsync();

        // Legacy schema: category without start_serial_no, no app_meta.
        await conn.ExecuteAsync("""
                                CREATE TABLE category (
                                  id INTEGER PRIMARY KEY AUTOINCREMENT,
                                  code TEXT NOT NULL UNIQUE,
                                  name TEXT NOT NULL UNIQUE,
                                  created_at TEXT DEFAULT CURRENT_TIMESTAMP
                                );
                                """);

        SqliteSchema.EnsureCreated(conn);

        var hasStart = await conn.ExecuteScalarAsync<long>(
            "SELECT COUNT(1) FROM pragma_table_info('category') WHERE name='start_serial_no';");
        Assert.Equal(1, hasStart);

        var version = await conn.ExecuteScalarAsync<string>("SELECT v FROM app_meta WHERE k='schema_version' LIMIT 1;");
        Assert.Equal("3", version);

        // Second run should not change schema or error.
        SqliteSchema.EnsureCreated(conn);
        var version2 = await conn.ExecuteScalarAsync<string>("SELECT v FROM app_meta WHERE k='schema_version' LIMIT 1;");
        Assert.Equal("3", version2);
    }

    [Fact]
    public async Task DriftDb_VersionSaysV3_ButMissingStartSerialNo_ShouldThrow()
    {
        var name = $"mcs_mig_drift_{Guid.NewGuid():N}";
        var cs = $"Data Source=file:{name}?mode=memory&cache=shared";
        await using var conn = new SqliteConnection(cs);
        await conn.OpenAsync();

        await conn.ExecuteAsync("""
                                CREATE TABLE app_meta (
                                  k TEXT PRIMARY KEY,
                                  v TEXT NOT NULL
                                );
                                INSERT INTO app_meta(k,v) VALUES ('schema_version','3');

                                CREATE TABLE category (
                                  id INTEGER PRIMARY KEY AUTOINCREMENT,
                                  code TEXT NOT NULL UNIQUE,
                                  name TEXT NOT NULL UNIQUE,
                                  created_at TEXT DEFAULT CURRENT_TIMESTAMP
                                );
                                """);

        Assert.Throws<InvalidOperationException>(() => SqliteSchema.EnsureCreated(conn));
    }
}

