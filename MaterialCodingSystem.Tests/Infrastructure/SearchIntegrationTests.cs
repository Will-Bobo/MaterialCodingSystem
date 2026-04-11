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
}

