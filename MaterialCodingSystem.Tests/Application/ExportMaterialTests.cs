using System.IO;
using MaterialCodingSystem.Application;
using MaterialCodingSystem.Application.Contracts;
using MaterialCodingSystem.Application.Interfaces;

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

    [Fact]
    public async Task ExportActiveMaterials_WhenTargetFileIsLocked_Returns_ExportFileInUse()
    {
        var locked = new IOException(
            "The process cannot access the file because it is being used by another process.",
            unchecked((int)0x80070020));
        var exporter = new ThrowingExcelExporter(locked);
        var app = new MaterialApplicationService(new NoopUnitOfWork(), new FakeMaterialRepository(), exporter);

        var res = await app.ExportActiveMaterials(@"F:\test\test.xlsx");

        Assert.False(res.IsSuccess);
        Assert.Equal(ErrorCodes.EXPORT_FILE_IN_USE, res.Error!.Code);
    }

    private sealed class ThrowingExcelExporter : IExcelMaterialExporter
    {
        private readonly Exception _ex;
        public ThrowingExcelExporter(Exception ex) => _ex = ex;
        public Task WriteAsync(string filePath, IReadOnlyList<MaterialExportRow> rows, CancellationToken ct = default) =>
            Task.FromException(_ex);
    }
}
