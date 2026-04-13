using MaterialCodingSystem.Application;
using MaterialCodingSystem.Application.Contracts;

namespace MaterialCodingSystem.Tests.Application;

public sealed class SearchBySpecAllTests
{
    [Fact]
    public async Task SearchBySpecAll_WhenKeywordEmpty_ReturnsValidationError()
    {
        var repo = new FakeMaterialRepository();
        var app = new MaterialApplicationService(new NoopUnitOfWork(), repo);

        var res = await app.SearchBySpecAllAsync("   ", includeDeprecated: false, limit: 20);

        Assert.False(res.IsSuccess);
        Assert.Equal(ErrorCodes.VALIDATION_ERROR, res.Error!.Code);
    }

    [Fact]
    public async Task SearchBySpecAll_Limit_IsCappedAt20()
    {
        var repo = new FakeMaterialRepository();
        var app = new MaterialApplicationService(new NoopUnitOfWork(), repo);

        await app.SearchBySpecAllAsync("UF", includeDeprecated: false, limit: 999);

        Assert.Equal(20, repo.LastSearchBySpecAllLimit);
    }

    [Fact]
    public async Task SearchBySpecAll_PassesIncludeDeprecatedToRepository()
    {
        var repo = new FakeMaterialRepository();
        var app = new MaterialApplicationService(new NoopUnitOfWork(), repo);

        await app.SearchBySpecAllAsync("UF", includeDeprecated: true, limit: 20);

        Assert.True(repo.LastSearchBySpecAllIncludeDeprecated);
    }

    [Fact]
    public async Task SearchBySpecAll_ReturnsTop20()
    {
        var repo = new FakeMaterialRepository();
        for (var i = 0; i < 30; i++)
        {
            repo.SpecSearchHits.Add(new MaterialItemSpecHit(
                Code: $"ZDA000000{i:D2}A",
                Spec: "UF",
                Description: "D",
                Name: "N",
                Brand: null,
                Status: 1,
                GroupId: i));
        }

        var app = new MaterialApplicationService(new NoopUnitOfWork(), repo);
        var res = await app.SearchBySpecAllAsync("UF", includeDeprecated: false, limit: 20);

        Assert.True(res.IsSuccess);
        Assert.True(res.Data!.Items.Count <= 20);
    }

    [Fact]
    public async Task SearchBySpecAll_OrderIsStable()
    {
        var repo = new FakeMaterialRepository();
        repo.SpecSearchHits.Add(new MaterialItemSpecHit("C2", "UF", "D", "N", null, 1, 1));
        repo.SpecSearchHits.Add(new MaterialItemSpecHit("C1", "UF", "D", "N", null, 1, 2));
        var app = new MaterialApplicationService(new NoopUnitOfWork(), repo);

        var r1 = await app.SearchBySpecAllAsync("UF", includeDeprecated: false, limit: 20);
        var r2 = await app.SearchBySpecAllAsync("UF", includeDeprecated: false, limit: 20);

        Assert.True(r1.IsSuccess && r2.IsSuccess);
        Assert.Equal(r1.Data!.Items.Select(x => x.Code).ToArray(), r2.Data!.Items.Select(x => x.Code).ToArray());
    }
}

