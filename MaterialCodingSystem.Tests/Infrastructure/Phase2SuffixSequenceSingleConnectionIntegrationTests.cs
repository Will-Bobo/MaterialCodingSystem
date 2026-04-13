using Dapper;
using MaterialCodingSystem.Application;
using MaterialCodingSystem.Application.Contracts;
using MaterialCodingSystem.Infrastructure.Sqlite;

namespace MaterialCodingSystem.Tests.Infrastructure;

/// <summary>
/// 单连接 InMemory：后缀连续性 / 「缺口不可补」（与 PRD 禁止复用空位语义一致）在真实 DB 快照下的行为。
/// </summary>
public class Phase2SuffixSequenceSingleConnectionIntegrationTests
{
    [Fact]
    public async Task CreateReplacement_WhenDbHasSuffixGap_A_To_C_Returns_SUFFIX_SEQUENCE_BROKEN()
    {
        await using var db = await SqliteTestDb.CreateAsync();
        await db.Connection.ExecuteAsync("INSERT INTO category(code,name) VALUES ('ZDA','电阻');");

        var app = new MaterialApplicationService(new SqliteUnitOfWork(db.Connection), new SqliteMaterialRepository(db.Connection));

        var a = await app.CreateMaterialItemA(new CreateMaterialItemARequest(
            CategoryCode: "ZDA",
            Spec: "SPEC-A-ONLY",
            Name: "n",
            Description: "d",
            Brand: "b"
        ));
        Assert.True(a.IsSuccess);
        var groupId = a.Data!.GroupId;

        var categoryId = await db.Connection.ExecuteScalarAsync<long>(
            "SELECT category_id FROM material_group WHERE id=@groupId;",
            new { groupId });

        await db.Connection.ExecuteAsync(
            """
            INSERT INTO material_item(
              group_id, category_id, category_code, code, suffix,
              name, description, spec, spec_normalized, brand, status, is_structured
            )
            VALUES(
              @groupId, @categoryId, 'ZDA', 'ZDA0000001C', 'C',
              'n3', 'd3', 'SPEC-SEED-C', 'D3', NULL, 1, 0
            );
            """,
            new { groupId, categoryId });

        var res = await app.CreateReplacement(new CreateReplacementRequest(
            GroupId: groupId,
            Spec: "SPEC-NEW-AFTER-GAP",
            Name: "n",
            Description: "d",
            Brand: "b"
        ));

        Assert.False(res.IsSuccess);
        Assert.Equal(ErrorCodes.SUFFIX_SEQUENCE_BROKEN, res.Error!.Code);
    }
}
