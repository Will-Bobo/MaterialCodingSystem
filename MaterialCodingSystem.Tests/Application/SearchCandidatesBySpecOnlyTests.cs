using MaterialCodingSystem.Application;

namespace MaterialCodingSystem.Tests.Application;

public sealed class SearchCandidatesBySpecOnlyTests
{
    [Fact]
    public async Task SearchCandidatesBySpecOnly_WhenKeywordEmpty_ReturnsValidationError()
    {
        var repo = new FakeMaterialRepository();
        var app = new MaterialApplicationService(new NoopUnitOfWork(), repo);

        var res = await app.SearchCandidatesBySpecOnlyAsync("ZDA", "   ", limit: 20);

        Assert.False(res.IsSuccess);
        Assert.Equal(ErrorCodes.VALIDATION_ERROR, res.Error!.Code);
    }

    [Fact]
    public async Task SearchCandidatesBySpecOnly_Limit_IsCappedAt20()
    {
        var repo = new FakeMaterialRepository();
        var app = new MaterialApplicationService(new NoopUnitOfWork(), repo);

        await app.SearchCandidatesBySpecOnlyAsync("ZDA", "CL10A106", limit: 999);

        Assert.Equal(20, repo.LastSearchCandidatesBySpecOnlyLimit);
    }
}

