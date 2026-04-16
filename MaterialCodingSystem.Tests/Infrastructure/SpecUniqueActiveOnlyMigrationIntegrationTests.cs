using Dapper;
using MaterialCodingSystem.Application;
using MaterialCodingSystem.Application.Contracts;
using MaterialCodingSystem.Infrastructure.Sqlite;
using Microsoft.Data.Sqlite;
using Xunit;

namespace MaterialCodingSystem.Tests.Infrastructure;

public class SpecUniqueActiveOnlyMigrationIntegrationTests
{
    [Fact]
    public async Task EnsureCreated_MigratesLegacyUniqueCategorySpec_ToPartialUniqueIndex_ActiveOnly()
    {
        // Arrange: 建一个“旧结构”的内存库（表级 UNIQUE(category_code, spec)）
        var conn = new SqliteConnection("Data Source=:memory:");
        await conn.OpenAsync();

        await conn.ExecuteAsync("""
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
                                  UNIQUE(category_id, serial_no)
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
                                  UNIQUE(category_code, spec),
                                  CHECK(status IN (0,1))
                                );
                                """);

        await conn.ExecuteAsync("INSERT INTO category(code,name) VALUES ('ELC','电子料');");
        await conn.ExecuteAsync("""
                                INSERT INTO material_group(category_id, category_code, serial_no)
                                VALUES ((SELECT id FROM category WHERE code='ELC'), 'ELC', 123);
                                """);

        // 插入一条“废弃”记录，占用 spec（旧约束下会阻止复用）
        await conn.ExecuteAsync("""
                                INSERT INTO material_item(
                                  group_id, category_id, category_code, code, suffix, name, description, spec, spec_normalized, brand, status, is_structured
                                )
                                VALUES (
                                  (SELECT id FROM material_group LIMIT 1),
                                  (SELECT id FROM category WHERE code='ELC'),
                                  'ELC',
                                  'ELC0000123A',
                                  'A',
                                  '电子料',
                                  'd',
                                  'S1',
                                  'D',
                                  NULL,
                                  0,
                                  0
                                );
                                """);

        // Act: 执行 EnsureCreated（应触发迁移）
        SqliteSchema.EnsureCreated(conn);

        // Assert: 新建启用态(status=1)同 spec 应允许（不被旧 UNIQUE(category_code, spec) 阻止）
        var uow = new SqliteUnitOfWork(conn);
        var repo = new SqliteMaterialRepository(conn);
        var app = new MaterialApplicationService(uow, repo);

        var res = await app.CreateMaterialItemManual(new CreateMaterialItemManualRequest(
            CategoryCode: "ELC",
            Code: "ELC0000123B",
            Spec: "S1",
            Name: "ignored",
            Description: "d2",
            Brand: null
        ));

        Assert.True(res.IsSuccess);
    }
}

