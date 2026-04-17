using Dapper;
using Microsoft.Data.Sqlite;
using MaterialCodingSystem.Infrastructure.Sqlite;
using Xunit;

namespace MaterialCodingSystem.Tests.Infrastructure;

public sealed class MaterialItemDisplayNameMigrationTests
{
    [Fact]
    public void EnsureCreated_WhenLegacyDbMissingDisplayNameColumn_ShouldAutoMigrate()
    {
        using var conn = new SqliteConnection("Data Source=:memory:");
        conn.Open();

        // Legacy schema：无 display_name 列（模拟旧版 DB）
        conn.Execute("""
                     CREATE TABLE category (
                       id INTEGER PRIMARY KEY AUTOINCREMENT,
                       code TEXT NOT NULL UNIQUE,
                       name TEXT NOT NULL UNIQUE,
                       created_at TEXT DEFAULT CURRENT_TIMESTAMP
                     );
                     CREATE TABLE material_group (
                       id INTEGER PRIMARY KEY AUTOINCREMENT,
                       category_id INTEGER NOT NULL,
                       category_code TEXT NOT NULL,
                       serial_no INTEGER NOT NULL,
                       created_at TEXT DEFAULT CURRENT_TIMESTAMP,
                       UNIQUE(category_id, serial_no),
                       FOREIGN KEY(category_id) REFERENCES category(id)
                     );
                     CREATE TABLE material_item (
                       id INTEGER PRIMARY KEY AUTOINCREMENT,
                       group_id INTEGER NOT NULL,
                       category_id INTEGER NOT NULL,
                       category_code TEXT NOT NULL,
                       code TEXT NOT NULL UNIQUE,
                       suffix TEXT NOT NULL,
                       name TEXT NOT NULL,
                       description TEXT NOT NULL,
                       spec TEXT NOT NULL,
                       spec_normalized TEXT NOT NULL,
                       brand TEXT,
                       status INTEGER NOT NULL DEFAULT 1,
                       is_structured INTEGER DEFAULT 0,
                       created_at TEXT DEFAULT CURRENT_TIMESTAMP,
                       UNIQUE(group_id, suffix),
                       FOREIGN KEY(group_id) REFERENCES material_group(id),
                       FOREIGN KEY(category_id) REFERENCES category(id),
                       CHECK(status IN (0,1))
                     );
                     """);

        // Act
        SqliteSchema.EnsureCreated(conn);

        // Assert：列已存在
        var has = conn.ExecuteScalar<long>(
            """
            SELECT COUNT(1)
            FROM pragma_table_info('material_item')
            WHERE name = 'display_name';
            """);
        Assert.True(has > 0);
    }
}

