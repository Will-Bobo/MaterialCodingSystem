using Dapper;
using MaterialCodingSystem.Application;
using MaterialCodingSystem.Application.Contracts;
using MaterialCodingSystem.Infrastructure.Sqlite;

namespace MaterialCodingSystem.Tests.Infrastructure;

public sealed class ManualExistingCodeIntegrationTests
{
    [Fact]
    public async Task ManualExistingCode_GroupHasB_NoA_ThenInsertA_Succeeds_ReuseGroup()
    {
        await using var db = await SqliteTestDb.CreateAsync();
        await db.Connection.ExecuteAsync("INSERT INTO category(code,name,start_serial_no) VALUES ('ZDA','电阻',1);");

        // Create group and item B manually via SQL to simulate legacy data
        await db.Connection.ExecuteAsync("""
                                        INSERT INTO material_group(category_id, category_code, serial_no)
                                        VALUES ((SELECT id FROM category WHERE code='ZDA'), 'ZDA', 123);
                                        """);
        var gid = await db.Connection.ExecuteScalarAsync<int>(
            "SELECT id FROM material_group WHERE category_code='ZDA' AND serial_no=123;");

        await db.Connection.ExecuteAsync("""
                                        INSERT INTO material_item(group_id, category_id, category_code, code, suffix, name, description, spec, spec_normalized, brand, status, is_structured)
                                        VALUES (@gid, (SELECT id FROM category WHERE code='ZDA'), 'ZDA', 'ZDA0000123B', 'B', '电阻', 'd', 'S-B', 'D', 'b', 1, 0);
                                        """, new { gid });

        var app = new MaterialApplicationService(new SqliteUnitOfWork(db.Connection), new SqliteMaterialRepository(db.Connection));
        var res = await app.CreateMaterial(new CreateMaterialRequest(
            CategoryCode: "ZDA",
            Spec: "S-A",
            Name: "n",
            Description: "d",
            Brand: "b",
            CodeMode: CreateMaterialCodeMode.ManualExistingCode,
            ExistingCode: "ZDA0000123A",
            ForceConfirm: true
        ));

        Assert.True(res.IsSuccess);
        Assert.Equal(gid, res.Data!.GroupId);
        Assert.Equal("ZDA0000123A", res.Data.Code);
    }

    [Fact]
    public async Task ManualExistingCode_WhenAAlreadyExists_InsertAAgain_FailsValidationError()
    {
        await using var db = await SqliteTestDb.CreateAsync();
        await db.Connection.ExecuteAsync("INSERT INTO category(code,name,start_serial_no) VALUES ('ZDA','电阻',1);");

        var app = new MaterialApplicationService(new SqliteUnitOfWork(db.Connection), new SqliteMaterialRepository(db.Connection));
        var r1 = await app.CreateMaterial(new CreateMaterialRequest(
            CategoryCode: "ZDA",
            Spec: "S-A",
            Name: "n",
            Description: "d",
            Brand: "b",
            CodeMode: CreateMaterialCodeMode.ManualExistingCode,
            ExistingCode: "ZDA0000123A",
            ForceConfirm: true
        ));
        Assert.True(r1.IsSuccess);

        var r2 = await app.CreateMaterial(new CreateMaterialRequest(
            CategoryCode: "ZDA",
            Spec: "S-A2",
            Name: "n",
            Description: "d",
            Brand: "b",
            CodeMode: CreateMaterialCodeMode.ManualExistingCode,
            ExistingCode: "ZDA0000123A",
            ForceConfirm: true
        ));
        Assert.False(r2.IsSuccess);
        Assert.Equal(ErrorCodes.VALIDATION_ERROR, r2.Error!.Code);
    }
}

