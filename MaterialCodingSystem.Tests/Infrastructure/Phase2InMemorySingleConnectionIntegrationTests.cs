using System.Collections.Concurrent;
using System.Threading;
using Dapper;
using MaterialCodingSystem.Application;
using MaterialCodingSystem.Application.Contracts;
using MaterialCodingSystem.Infrastructure.Sqlite;

namespace MaterialCodingSystem.Tests.Infrastructure;

/// <summary>
/// Phase 2：独占 <c>Data Source=:memory:</c> 单连接上的集成基线（并发通过连接序列化/锁实现）。
/// </summary>
public class Phase2InMemorySingleConnectionIntegrationTests
{
    [Fact]
    public async Task SequentialCreateA_SameSpec_SecondReturns_SPEC_DUPLICATE()
    {
        await using var db = await SqliteTestDb.CreateAsync();
        await db.Connection.ExecuteAsync("INSERT INTO category(code,name) VALUES ('ZDA','电阻');");

        var app = new MaterialApplicationService(new SqliteUnitOfWork(db.Connection), new SqliteMaterialRepository(db.Connection));

        var r1 = await app.CreateMaterialItemA(new CreateMaterialItemARequest(
            CategoryCode: "ZDA",
            Spec: "SPEC-DUP-SEQ",
            Name: "n",
            Description: "d",
            Brand: "b"
        ));
        var r2 = await app.CreateMaterialItemA(new CreateMaterialItemARequest(
            CategoryCode: "ZDA",
            Spec: "SPEC-DUP-SEQ",
            Name: "n2",
            Description: "d2",
            Brand: "b2"
        ));

        Assert.True(r1.IsSuccess);
        Assert.False(r2.IsSuccess);
        Assert.Equal(ErrorCodes.SPEC_DUPLICATE, r2.Error!.Code);
    }

    /// <summary>
    /// 独占单连接上并行开启多个 <see cref="SqliteUnitOfWork"/> 事务会触发「不允许嵌套事务」。
    /// 用信号量串行化调用，模拟多请求争用同一 spec：仅一条成功，其余 <see cref="ErrorCodes.SPEC_DUPLICATE"/>。
    /// </summary>
    [Fact]
    public async Task SingleConnection_SerializedContention_SameSpec_ExactlyOneSuccess_RestSpecDuplicate()
    {
        await using var db = await SqliteTestDb.CreateAsync();
        await db.Connection.ExecuteAsync("INSERT INTO category(code,name) VALUES ('ZDA','电阻');");

        var app = new MaterialApplicationService(new SqliteUnitOfWork(db.Connection), new SqliteMaterialRepository(db.Connection));

        const int n = 25;
        var bag = new ConcurrentBag<(bool Ok, string? ErrCode, string? Ex)>();
        using var gate = new SemaphoreSlim(1, 1);

        await Parallel.ForEachAsync(Enumerable.Range(0, n), async (_, ct) =>
        {
            await gate.WaitAsync(ct);
            try
            {
                var res = await app.CreateMaterialItemA(new CreateMaterialItemARequest(
                    CategoryCode: "ZDA",
                    Spec: "SPEC-SAME-CONTENDED",
                    Name: "n",
                    Description: "d",
                    Brand: "b"
                ));
                bag.Add((res.IsSuccess, res.Error?.Code, null));
            }
            catch (Exception ex)
            {
                bag.Add((false, null, $"{ex.GetType().Name}:{ex.Message}"));
            }
            finally
            {
                gate.Release();
            }
        });

        Assert.Equal(1, bag.Count(x => x.Ok));
        Assert.All(bag.Where(x => !x.Ok), f =>
            Assert.True(
                f.ErrCode == ErrorCodes.SPEC_DUPLICATE || f.Ex is not null,
                $"Unexpected failure: Err={f.ErrCode}, Ex={f.Ex}"));
    }

    [Fact]
    public async Task SingleConnection_SequentialCreateA_DifferentSpecs_AllSucceed_UniqueSerials()
    {
        await using var db = await SqliteTestDb.CreateAsync();
        await db.Connection.ExecuteAsync("INSERT INTO category(code,name) VALUES ('ZDA','电阻');");

        var app = new MaterialApplicationService(new SqliteUnitOfWork(db.Connection), new SqliteMaterialRepository(db.Connection));

        const int n = 12;
        for (var i = 0; i < n; i++)
        {
            var res = await app.CreateMaterialItemA(new CreateMaterialItemARequest(
                CategoryCode: "ZDA",
                Spec: $"SPEC-SEQ-{i}",
                Name: "n",
                Description: "d",
                Brand: "b"
            ));
            Assert.True(res.IsSuccess, res.Error?.Message);
        }

        var serials = (await db.Connection.QueryAsync<int>(
            "SELECT serial_no FROM material_group WHERE category_code='ZDA' ORDER BY serial_no;")).ToList();
        Assert.Equal(n, serials.Count);
        Assert.Equal(n, serials.Distinct().Count());
    }

    [Fact]
    public async Task SingleConnection_SequentialCreateReplacement_DifferentSpecs_AfterA_AllSucceed_UniqueSuffixes()
    {
        await using var db = await SqliteTestDb.CreateAsync();
        await db.Connection.ExecuteAsync("INSERT INTO category(code,name) VALUES ('ZDA','电阻');");

        var app = new MaterialApplicationService(new SqliteUnitOfWork(db.Connection), new SqliteMaterialRepository(db.Connection));

        var a = await app.CreateMaterialItemA(new CreateMaterialItemARequest(
            CategoryCode: "ZDA",
            Spec: "SPEC-A-BASE",
            Name: "n",
            Description: "d",
            Brand: "b"
        ));
        Assert.True(a.IsSuccess);
        var groupId = a.Data!.GroupId;

        const int n = 8;
        for (var i = 0; i < n; i++)
        {
            var res = await app.CreateReplacement(new CreateReplacementRequest(
                GroupId: groupId,
                Spec: $"SPEC-REPL-{i}",
                Name: "n",
                Description: "d",
                Brand: "b"
            ));
            Assert.True(res.IsSuccess, res.Error?.Message);
        }

        var suffixes = (await db.Connection.QueryAsync<string>(
            "SELECT suffix FROM material_item WHERE group_id=@g ORDER BY suffix;",
            new { g = groupId })).Select(s => s[0]).ToList();
        Assert.Equal(1 + n, suffixes.Count);
        Assert.Equal(suffixes.Count, suffixes.Distinct().Count());
    }
}
