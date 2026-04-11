using Dapper;
using MaterialCodingSystem.Application;
using MaterialCodingSystem.Application.Contracts;
using MaterialCodingSystem.Infrastructure.Sqlite;
using Microsoft.Data.Sqlite;

namespace MaterialCodingSystem.Tests.Infrastructure;

public class ConcurrencyIntegrationTests
{
    [Fact]
    public async Task ConcurrentCreateA_NoDuplicateCodes_AndNoDuplicateSerialPerCategory()
    {
        await using var db = await SqliteTestDb.CreateSharedAsync("mcs_conc_createa");
        await db.Connection.ExecuteAsync("INSERT INTO category(code,name) VALUES ('ZDA','电阻');");

        const int threads = 10;
        const int perThread = 20;

        async Task<string?> Worker(int t, int i)
        {
            await using var conn = new SqliteConnection(db.ConnectionString);
            await conn.OpenAsync();
            var app = new MaterialApplicationService(new SqliteUnitOfWork(conn), new SqliteMaterialRepository(conn));

            var res = await app.CreateMaterialItemA(new CreateMaterialItemARequest(
                CategoryCode: "ZDA",
                Spec: $"SPEC-{t}-{i}",
                Name: "n",
                Description: " 10uF  16V ",
                Brand: "b"
            ));

            return res.IsSuccess ? res.Data!.Code : null;
        }

        var tasks = new List<Task<string?>>(threads * perThread);
        for (var t = 0; t < threads; t++)
        for (var i = 0; i < perThread; i++)
            tasks.Add(Worker(t, i));

        var codes = (await Task.WhenAll(tasks)).Where(x => x is not null).Select(x => x!).ToList();

        // DB 作为最终仲裁：不允许重复 code
        Assert.Equal(codes.Count, codes.Distinct().Count());

        // 串号唯一：material_group(category_id, serial_no) 不重复
        var serialPairs = (await db.Connection.QueryAsync<(long category_id, long serial_no)>(
            "SELECT category_id, serial_no FROM material_group WHERE category_code='ZDA';"
        )).ToList();
        Assert.Equal(serialPairs.Count, serialPairs.Distinct().Count());
    }

    [Fact]
    public async Task ConcurrentCreateReplacement_NoDuplicateSuffixPerGroup()
    {
        await using var db = await SqliteTestDb.CreateSharedAsync("mcs_conc_repl");
        await db.Connection.ExecuteAsync("INSERT INTO category(code,name) VALUES ('ZDA','电阻');");

        // Given：先创建 A（在一个连接中）
        var initApp = new MaterialApplicationService(new SqliteUnitOfWork(db.Connection), new SqliteMaterialRepository(db.Connection));
        var a = await initApp.CreateMaterialItemA(new CreateMaterialItemARequest(
            CategoryCode: "ZDA",
            Spec: "SPEC-A",
            Name: "n",
            Description: "d",
            Brand: "b"
        ));
        Assert.True(a.IsSuccess);

        var groupId = a.Data!.GroupId;

        const int threads = 10;
        const int perThread = 5; // replacement 并发量先控制在 50，避免 in-memory shared 的锁抖动

        async Task<string?> Worker(int t, int i)
        {
            await using var conn = new SqliteConnection(db.ConnectionString);
            await conn.OpenAsync();
            var app = new MaterialApplicationService(new SqliteUnitOfWork(conn), new SqliteMaterialRepository(conn));

            var res = await app.CreateReplacement(new CreateReplacementRequest(
                GroupId: groupId,
                Spec: $"SPEC-R-{t}-{i}",
                Name: "n",
                Description: "d",
                Brand: "b"
            ));

            return res.IsSuccess ? res.Data!.Suffix : null;
        }

        var tasks = new List<Task<string?>>(threads * perThread);
        for (var t = 0; t < threads; t++)
        for (var i = 0; i < perThread; i++)
            tasks.Add(Worker(t, i));

        await Task.WhenAll(tasks);

        var suffixes = (await db.Connection.QueryAsync<string>(
            "SELECT suffix FROM material_item WHERE group_id=@groupId ORDER BY suffix;",
            new { groupId }
        )).ToList();

        Assert.Equal(suffixes.Count, suffixes.Distinct().Count());
        Assert.Contains("A", suffixes);
    }
}

