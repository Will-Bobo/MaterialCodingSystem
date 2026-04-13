using Dapper;
using MaterialCodingSystem.Application;
using MaterialCodingSystem.Application.Contracts;
using MaterialCodingSystem.Infrastructure.Sqlite;

namespace MaterialCodingSystem.Tests.Infrastructure;

public class SearchIntegrationTests
{
    [Fact]
    public async Task SearchBySpec_RequiresCategoryFilter_AndReturnsTop20Active()
    {
        await using var db = await SqliteTestDb.CreateAsync();
        await db.Connection.ExecuteAsync("INSERT INTO category(code,name) VALUES ('ZDA','电阻');");

        var app = new MaterialApplicationService(new SqliteUnitOfWork(db.Connection), new SqliteMaterialRepository(db.Connection));

        // seed 25 materials
        for (var i = 0; i < 25; i++)
        {
            var res = await app.CreateMaterialItemA(new CreateMaterialItemARequest(
                CategoryCode: "ZDA",
                Spec: $"SPEC-{i}",
                Name: "n",
                Description: i % 2 == 0 ? "10uF 16V" : "1uF 16V",
                Brand: "b"
            ));
            Assert.True(res.IsSuccess);
        }

        // deprecate one matching item
        var dep = await app.DeprecateMaterialItem(new DeprecateRequest("ZDA0000002A"));
        Assert.True(dep.IsSuccess);

        var search = await app.SearchBySpec(new SearchQuery(
            CodeKeyword: null,
            SpecKeyword: "UF",
            CategoryCode: "ZDA",
            IncludeDeprecated: false,
            Limit: 50,
            Offset: 0
        ));

        Assert.True(search.IsSuccess);
        Assert.True(search.Data!.Items.Count <= 20);
        Assert.DoesNotContain(search.Data.Items, x => x.Code == "ZDA0000002A");
    }

    [Fact]
    public async Task SearchByCode_PrefixThenFuzzy_ReturnsMatches()
    {
        await using var db = await SqliteTestDb.CreateAsync();
        await db.Connection.ExecuteAsync("INSERT INTO category(code,name) VALUES ('ZDA','电阻');");

        var app = new MaterialApplicationService(new SqliteUnitOfWork(db.Connection), new SqliteMaterialRepository(db.Connection));
        var a1 = await app.CreateMaterialItemA(new CreateMaterialItemARequest("ZDA", "S1", "n", "d", "b"));
        var a2 = await app.CreateMaterialItemA(new CreateMaterialItemARequest("ZDA", "S2", "n", "d", "b"));
        Assert.True(a1.IsSuccess && a2.IsSuccess);

        // keyword is middle substring: should be found by fuzzy stage if prefix insufficient
        var search = await app.SearchByCode(new SearchQuery(
            CodeKeyword: "000000",
            SpecKeyword: null,
            CategoryCode: null,
            IncludeDeprecated: false,
            Limit: 20,
            Offset: 0
        ));

        Assert.True(search.IsSuccess);
        Assert.True(search.Data!.Items.Count >= 2);
    }

    [Fact]
    public async Task SearchBySpecAll_ReturnsTop20_AndOrdersStably_AcrossCategories()
    {
        await using var db = await SqliteTestDb.CreateAsync();
        var conn = db.Connection;

        await conn.ExecuteAsync("INSERT INTO category(code,name) VALUES ('ZDA','A'),('ZDB','B'),('ZDC','C');");

        // Groups: serial_no controls stable ordering
        await conn.ExecuteAsync(@"
INSERT INTO material_group(id,category_id,category_code,serial_no) VALUES
 (1,1,'ZDA',2),
 (2,2,'ZDB',1),
 (3,3,'ZDC',3);");

        // Seed 30 rows that all match keyword via spec or spec_normalized, with mixed status
        // Make some direct spec hits ("UF") and others only normalized hits ("uF")
        for (var i = 0; i < 30; i++)
        {
            var groupId = i % 3 + 1;
            var cat = groupId == 1 ? "ZDA" : groupId == 2 ? "ZDB" : "ZDC";
            var serial = groupId == 1 ? 2 : groupId == 2 ? 1 : 3;
            var suffix = (char)('A' + (i / 3)); // ensure unique within group_id
            var code = $"{cat}{serial:D7}{suffix}";
            var spec = i % 2 == 0 ? $"X{ i }UF" : $"X{ i }uF";
            var desc = "10uF 16V";
            var status = i % 5 == 0 ? 0 : 1;

            await conn.ExecuteAsync(@"
INSERT INTO material_item(group_id,category_id,category_code,code,suffix,name,description,spec,spec_normalized,brand,status)
VALUES (@groupId, (SELECT category_id FROM material_group WHERE id=@groupId), @cat, @code, @suffix, 'n', @desc, @spec, @spec, 'b', @status);",
                new { groupId, cat, code, suffix = suffix.ToString(), desc, spec, status });
        }

        var app = new MaterialApplicationService(new SqliteUnitOfWork(conn), new SqliteMaterialRepository(conn));

        var r1 = await app.SearchBySpecAllAsync("UF", includeDeprecated: false, limit: 20);
        Assert.True(r1.IsSuccess);
        Assert.True(r1.Data!.Items.Count <= 20);
        Assert.DoesNotContain(r1.Data.Items, x => x.Status == 0);

        var r2 = await app.SearchBySpecAllAsync("UF", includeDeprecated: false, limit: 20);
        Assert.True(r2.IsSuccess);
        Assert.Equal(r1.Data.Items.Select(x => x.Code).ToArray(), r2.Data!.Items.Select(x => x.Code).ToArray());

        var r3 = await app.SearchBySpecAllAsync("UF", includeDeprecated: true, limit: 20);
        Assert.True(r3.IsSuccess);
        Assert.True(r3.Data!.Items.Count <= 20);
        Assert.Contains(r3.Data.Items, x => x.Status == 0);
    }
}

