using MaterialCodingSystem.Application;
using MaterialCodingSystem.Application.Contracts;
using MaterialCodingSystem.Application.Interfaces;

namespace MaterialCodingSystem.Tests.Application;

public class DeprecateMaterialTests
{
    [Fact]
    public async Task Deprecate_WhenNotFound_ReturnsNotFound()
    {
        var app = new MaterialApplicationService(
            uow: new NoopUnitOfWork(),
            repo: new FakeMaterialRepository { ItemExistsByCode = false }
        );

        var res = await app.DeprecateMaterialItem(new DeprecateRequest(Code: "ZDA0000001A"));

        Assert.False(res.IsSuccess);
        Assert.Equal(ErrorCodes.NOT_FOUND, res.Error!.Code);
    }

    [Fact]
    public async Task Deprecate_Success_UpdatesStatusToDeprecated()
    {
        var repo = new FakeMaterialRepository { ItemExistsByCode = true, ItemStatusByCode = 1 };
        var app = new MaterialApplicationService(uow: new NoopUnitOfWork(), repo: repo);

        var res = await app.DeprecateMaterialItem(new DeprecateRequest(Code: "ZDA0000001A"));

        Assert.True(res.IsSuccess);
        Assert.Equal("ZDA0000001A", res.Data!.Code);
        Assert.Equal(0, res.Data.Status);
        Assert.Equal(1, repo.DeprecateCalled);
    }
}

