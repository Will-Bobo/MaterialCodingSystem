using MaterialCodingSystem.Application;
using MaterialCodingSystem.Application.Contracts;

namespace MaterialCodingSystem.Tests.Application;

public sealed class CreateReplacementByCodeTests
{
    [Fact]
    public async Task CreateReplacementByCode_WhenBaseNotFound_ReturnsNotFound_AndDoesNotQueryGroup()
    {
        var repo = new FakeMaterialRepository
        {
            ItemExistsByCode = false,
            GroupExists = true
        };
        var app = new MaterialApplicationService(uow: new NoopUnitOfWork(), repo: repo);

        var res = await app.CreateReplacementByCode(new CreateReplacementByCodeRequest(
            BaseMaterialCode: "ZDA0000001A",
            Spec: "S1",
            Description: "d",
            Brand: "B"
        ));

        Assert.False(res.IsSuccess);
        Assert.Equal(ErrorCodes.NOT_FOUND, res.Error!.Code);
        Assert.Equal(0, repo.GroupSnapshotCalls);
    }

    [Fact]
    public async Task CreateReplacementByCode_WhenBaseDeprecated_ReturnsAnchorItemDeprecated()
    {
        var repo = new FakeMaterialRepository
        {
            ItemExistsByCode = true,
            ItemStatusByCode = 0,
            GroupExists = true
        };
        var app = new MaterialApplicationService(uow: new NoopUnitOfWork(), repo: repo);

        var res = await app.CreateReplacementByCode(new CreateReplacementByCodeRequest(
            BaseMaterialCode: "ZDA0000001A",
            Spec: "S1",
            Description: "d",
            Brand: "B"
        ));

        Assert.False(res.IsSuccess);
        Assert.Equal(ErrorCodes.ANCHOR_ITEM_DEPRECATED, res.Error!.Code);
    }

    [Fact]
    public async Task CreateReplacementByCode_WhenBrandMissing_ReturnsValidationError_WithFieldErrors()
    {
        var repo = new FakeMaterialRepository
        {
            ItemExistsByCode = true,
            ItemStatusByCode = 1,
            GroupExists = true
        };
        var app = new MaterialApplicationService(uow: new NoopUnitOfWork(), repo: repo);

        var res = await app.CreateReplacementByCode(new CreateReplacementByCodeRequest(
            BaseMaterialCode: "ZDA0000001A",
            Spec: "S1",
            Description: "d",
            Brand: null
        ));

        Assert.False(res.IsSuccess);
        Assert.Equal(ErrorCodes.VALIDATION_ERROR, res.Error!.Code);
        Assert.NotNull(res.Error.ValidationErrors);
        Assert.True(res.Error.ValidationErrors!.ContainsKey("brand"));
    }
}

