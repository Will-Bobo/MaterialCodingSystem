using Dapper;
using MaterialCodingSystem.Application;
using MaterialCodingSystem.Application.Contracts;
using MaterialCodingSystem.Infrastructure.Sqlite;
using Xunit;

namespace MaterialCodingSystem.Tests.Infrastructure;

public class ManualCreateIntegrationTests
{
    [Fact]
    public async Task CreateManual_AllowsDifferentSuffixesInSameGroup_AndReusesGroupByCategoryIdAndSerialNo()
    {
        await using var db = await SqliteTestDb.CreateAsync();
        await db.Connection.ExecuteAsync("INSERT INTO category(code,name) VALUES ('ELC','电子料');");

        var uow = new SqliteUnitOfWork(db.Connection);
        var repo = new SqliteMaterialRepository(db.Connection);
        var app = new MaterialApplicationService(uow, repo);

        var r1 = await app.CreateMaterialItemManual(new CreateMaterialItemManualRequest(
            CategoryCode: "ELC",
            Code: "ELC0000123B",
            Spec: "S-B",
            Name: "ignored",
            Description: "d1",
            Brand: "b"
        ));
        Assert.True(r1.IsSuccess);

        var r2 = await app.CreateMaterialItemManual(new CreateMaterialItemManualRequest(
            CategoryCode: "ELC",
            Code: "ELC0000123A",
            Spec: "S-A",
            Name: "ignored",
            Description: "d2",
            Brand: "b"
        ));
        Assert.True(r2.IsSuccess);

        // 同 serial_no，应复用同一 group
        Assert.Equal(r1.Data!.GroupId, r2.Data!.GroupId);
        Assert.Equal(123, r1.Data.SerialNo);
        Assert.Equal(123, r2.Data.SerialNo);
    }

    [Fact]
    public async Task CreateManual_WhenGroupSuffixConflicts_Returns_CODE_DUPLICATE()
    {
        await using var db = await SqliteTestDb.CreateAsync();
        await db.Connection.ExecuteAsync("INSERT INTO category(code,name) VALUES ('ELC','电子料');");

        var uow = new SqliteUnitOfWork(db.Connection);
        var repo = new SqliteMaterialRepository(db.Connection);
        var app = new MaterialApplicationService(uow, repo);

        var r1 = await app.CreateMaterialItemManual(new CreateMaterialItemManualRequest(
            CategoryCode: "ELC",
            Code: "ELC0000123A",
            Spec: "S-A-1",
            Name: "ignored",
            Description: "d1",
            Brand: "b"
        ));
        Assert.True(r1.IsSuccess);

        var r2 = await app.CreateMaterialItemManual(new CreateMaterialItemManualRequest(
            CategoryCode: "ELC",
            Code: "ELC0000123A",
            Spec: "S-A-2",
            Name: "ignored",
            Description: "d2",
            Brand: "b"
        ));
        Assert.False(r2.IsSuccess);
        Assert.Equal(ErrorCodes.CODE_DUPLICATE, r2.Error!.Code);
    }
}

