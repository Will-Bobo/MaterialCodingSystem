using Dapper;
using MaterialCodingSystem.Application;
using MaterialCodingSystem.Application.Contracts;
using MaterialCodingSystem.Infrastructure.Sqlite;
using Xunit;

namespace MaterialCodingSystem.Tests.Infrastructure;

public class DapperIntegrationTests
{
    [Fact]
    public async Task CreateA_PersistsGroupAndItem_AndEnforcesSpecUniqueness()
    {
        await using var db = await SqliteTestDb.CreateAsync();

        // Given：初始化分类（PRD：分类必须已存在）
        await db.Connection.ExecuteAsync("INSERT INTO category(code,name) VALUES ('ZDA','电阻');");

        var uow = new SqliteUnitOfWork(db.Connection);
        var repo = new SqliteMaterialRepository(db.Connection);
        var app = new MaterialApplicationService(uow, repo);

        var r1 = await app.CreateMaterialItemA(new CreateMaterialItemARequest(
            CategoryCode: "ZDA",
            Spec: "CL10A106KP8NNNC",
            Name: "n",
            Description: " 10uF  16V ",
            Brand: "b"
        ));

        Assert.True(r1.IsSuccess);

        var r2 = await app.CreateMaterialItemA(new CreateMaterialItemARequest(
            CategoryCode: "ZDA",
            Spec: "CL10A106KP8NNNC",
            Name: "n2",
            Description: "x",
            Brand: "b2"
        ));

        Assert.False(r2.IsSuccess);
        Assert.Equal(ErrorCodes.SPEC_DUPLICATE, r2.Error!.Code);
    }
}

