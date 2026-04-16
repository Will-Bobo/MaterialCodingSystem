using Dapper;
using MaterialCodingSystem.Domain.ValueObjects;
using MaterialCodingSystem.Infrastructure.Sqlite;
using Xunit;

namespace MaterialCodingSystem.Tests.Infrastructure;

public class SqliteRepositoryNewMethodsIntegrationTests
{
    [Fact]
    public async Task GetCategoryIdByCode_ReturnsId()
    {
        await using var db = await SqliteTestDb.CreateAsync();
        await db.Connection.ExecuteAsync("INSERT INTO category(code,name) VALUES ('ELC','电子料');");

        var repo = new SqliteMaterialRepository(db.Connection);
        var id = await repo.GetCategoryIdByCodeAsync(new CategoryCode("ELC"));

        Assert.NotNull(id);
        Assert.True(id > 0);
    }

    [Fact]
    public async Task GetGroupIdByCategoryAndSerialNo_UsesCategoryIdAndSerialNo()
    {
        await using var db = await SqliteTestDb.CreateAsync();
        await db.Connection.ExecuteAsync("INSERT INTO category(code,name) VALUES ('ELC','电子料');");

        var repo = new SqliteMaterialRepository(db.Connection);
        var categoryId = await repo.GetCategoryIdByCodeAsync(new CategoryCode("ELC"));
        Assert.NotNull(categoryId);

        // Insert group with serial_no=123（用 Dapper 直接插入，避免依赖 internal AmbientSqliteContext）
        await db.Connection.ExecuteAsync("""
                                        INSERT INTO material_group(category_id, category_code, serial_no)
                                        VALUES (@categoryId, 'ELC', 123);
                                        """, new { categoryId = categoryId!.Value });
        var gid = await db.Connection.ExecuteScalarAsync<long>("SELECT id FROM material_group WHERE category_id=@categoryId AND serial_no=123 LIMIT 1;",
            new { categoryId = categoryId!.Value });

        var found = await repo.GetGroupIdByCategoryAndSerialNoAsync(categoryId!.Value, 123);
        Assert.Equal((int)gid, found);
    }
}

