using System.Collections.Concurrent;
using Dapper;
using MaterialCodingSystem.Application;
using MaterialCodingSystem.Application.Contracts;
using MaterialCodingSystem.Infrastructure.Sqlite;
using Microsoft.Data.Sqlite;

namespace MaterialCodingSystem.Tests.Infrastructure;

/// <summary>
/// 真实 OS 级并发：SQLite 需 <c>cache=shared</c> 多连接访问同一内存库。
/// 与「独占 :memory: 单连接」不同；用于验证 group serial / 替代料在并行事务下的行为基线。
/// </summary>
public class Phase2InMemorySharedCacheConcurrencyIntegrationTests
{
    [Fact]
    public async Task ParallelCreateA_DifferentSpecs_AllSucceed_UniqueSerials()
    {
        var name = $"mcs_phase2_{Guid.NewGuid():N}";
        await using var db = await SqliteTestDb.CreateSharedAsync(name);
        await db.Connection.ExecuteAsync("INSERT INTO category(code,name) VALUES ('ZDA','电阻');");

        const int n = 12;
        var bag = new ConcurrentBag<bool>();

        await Parallel.ForEachAsync(Enumerable.Range(0, n), async (i, ct) =>
        {
            await using var conn = new SqliteConnection(db.ConnectionString);
            await conn.OpenAsync(ct);
            var app = new MaterialApplicationService(new SqliteUnitOfWork(conn), new SqliteMaterialRepository(conn));

            var res = await app.CreateMaterialItemA(new CreateMaterialItemARequest(
                CategoryCode: "ZDA",
                Spec: $"SPEC-PAR-{i}",
                Name: "n",
                Description: "d",
                Brand: "b"
            ));
            bag.Add(res.IsSuccess);
        });

        Assert.All(bag, Assert.True);

        var serials = (await db.Connection.QueryAsync<int>(
            "SELECT serial_no FROM material_group WHERE category_code='ZDA' ORDER BY serial_no;")).ToList();
        Assert.Equal(n, serials.Count);
        Assert.Equal(n, serials.Distinct().Count());
    }

    [Fact]
    public async Task ParallelCreateA_SameSpec_ExactlyOneSuccess_RestSpecDuplicateOrConflict()
    {
        var name = $"mcs_phase2_same_{Guid.NewGuid():N}";
        await using var db = await SqliteTestDb.CreateSharedAsync(name);
        await db.Connection.ExecuteAsync("INSERT INTO category(code,name) VALUES ('ZDA','电阻');");

        const int n = 20;
        var bag = new ConcurrentBag<(bool Ok, string? Err)>();

        await Parallel.ForEachAsync(Enumerable.Range(0, n), async (_, ct) =>
        {
            await using var conn = new SqliteConnection(db.ConnectionString);
            await conn.OpenAsync(ct);
            var app = new MaterialApplicationService(new SqliteUnitOfWork(conn), new SqliteMaterialRepository(conn));

            var res = await app.CreateMaterialItemA(new CreateMaterialItemARequest(
                CategoryCode: "ZDA",
                Spec: "SPEC-PARALLEL-SAME",
                Name: "n",
                Description: "d",
                Brand: "b"
            ));
            bag.Add((res.IsSuccess, res.Error?.Code));
        });

        Assert.Equal(1, bag.Count(x => x.Ok));
        Assert.All(bag.Where(x => !x.Ok), x =>
            Assert.True(
                x.Err == ErrorCodes.SPEC_DUPLICATE || x.Err == ErrorCodes.CODE_CONFLICT_RETRY,
                $"unexpected err={x.Err}"));
    }

    [Fact]
    public async Task ParallelCreateReplacement_DifferentSpecs_AfterA_AllSucceed()
    {
        var name = $"mcs_phase2_repl_{Guid.NewGuid():N}";
        await using var db = await SqliteTestDb.CreateSharedAsync(name);
        await db.Connection.ExecuteAsync("INSERT INTO category(code,name) VALUES ('ZDA','电阻');");

        var initConn = db.Connection;
        var initApp = new MaterialApplicationService(new SqliteUnitOfWork(initConn), new SqliteMaterialRepository(initConn));
        var a = await initApp.CreateMaterialItemA(new CreateMaterialItemARequest(
            CategoryCode: "ZDA",
            Spec: "SPEC-A-BASE",
            Name: "n",
            Description: "d",
            Brand: "b"
        ));
        Assert.True(a.IsSuccess);
        var groupId = a.Data!.GroupId;

        const int n = 8;
        var bag = new ConcurrentBag<bool>();

        await Parallel.ForEachAsync(Enumerable.Range(0, n), async (i, ct) =>
        {
            await using var conn = new SqliteConnection(db.ConnectionString);
            await conn.OpenAsync(ct);
            var app = new MaterialApplicationService(new SqliteUnitOfWork(conn), new SqliteMaterialRepository(conn));

            var res = await app.CreateReplacement(new CreateReplacementRequest(
                GroupId: groupId,
                Spec: $"SPEC-REPL-PAR-{i}",
                Name: "n",
                Description: "d",
                Brand: "b"
            ));
            bag.Add(res.IsSuccess);
        });

        Assert.All(bag, Assert.True);

        var suffixes = (await db.Connection.QueryAsync<string>(
            "SELECT suffix FROM material_item WHERE group_id=@g ORDER BY suffix;",
            new { g = groupId })).Select(s => s[0]).ToList();
        Assert.Equal(1 + n, suffixes.Count);
        Assert.Equal(suffixes.Count, suffixes.Distinct().Count());
    }
}
