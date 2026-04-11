using MaterialCodingSystem.Application;

namespace MaterialCodingSystem.Tests.Application;

public sealed class ExportMaterialTests
{
    [Fact]
    public async Task ExportActiveMaterials_WithoutExporter_Returns_InternalError()
    {
        var repo = new FakeMaterialRepository();
        var app = new MaterialApplicationService(new NoopUnitOfWork(), repo, excelExporter: null);

        var res = await app.ExportActiveMaterials(@"C:\temp\out.xlsx");

        Assert.False(res.IsSuccess);
        Assert.Equal(ErrorCodes.INTERNAL_ERROR, res.Error!.Code);
    }
}
