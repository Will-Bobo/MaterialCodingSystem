using ClosedXML.Excel;
using Dapper;
using MaterialCodingSystem.Application;
using MaterialCodingSystem.Application.Contracts;
using MaterialCodingSystem.Infrastructure.Excel;
using MaterialCodingSystem.Infrastructure.Sqlite;
using MaterialCodingSystem.Tests.Infrastructure;
using Microsoft.Extensions.Logging.Abstractions;

namespace MaterialCodingSystem.Tests.Bom;

public sealed class Hardening_ImportInProgressTests
{
    [Fact]
    public async Task Import_Same_FinishedCode_Version_Concurrent_Should_Block_Second()
    {
        await using var db = await SqliteTestDb.CreateAsync();
        var conn = db.Connection;
        await conn.ExecuteAsync("INSERT INTO category(code,name) VALUES ('ZDA','R');");

        var repo = new SqliteMaterialRepository(conn);
        var uow = new SqliteUnitOfWork(conn);
        var gridParser = new UnifiedBomGridParser(
            NullLogger<UnifiedBomGridParser>.Instance,
            new MaterialCodingSystem.Infrastructure.Excel.Adapters.ClosedXmlBomGridAdapter(),
            new MaterialCodingSystem.Infrastructure.Excel.Adapters.ExcelDataReaderBomGridAdapter());
        var parseBom = new ParseBomUseCase(gridParser);
        var detector = new MaterialCodingSystem.Infrastructure.Excel.BomFileFormatDetector();
        var analyze = new AnalyzeBomUseCase(parseBom, detector, repo, uow, NullLogger<AnalyzeBomUseCase>.Instance);
        var app = new MaterialApplicationService(uow, repo);
        var gate = new BomImportInProgressGate();
        var import = new ImportBomNewMaterialsUseCase(analyze, app, gate, NullLogger<ImportBomNewMaterialsUseCase>.Instance);

        var path = WriteBomTempFile(wb =>
        {
            var ws = wb.AddWorksheet("BOM");
            ws.Cell(1, 1).Value = "成品编码";
            ws.Cell(1, 2).Value = "CP";
            ws.Cell(2, 1).Value = "PCBA版本号";
            ws.Cell(2, 2).Value = "V";

            ws.Cell(5, 1).Value = "编码";
            ws.Cell(5, 2).Value = "名称";
            ws.Cell(5, 3).Value = "描述";
            ws.Cell(5, 4).Value = "规格";
            ws.Cell(5, 5).Value = "品牌";

            ws.Cell(6, 1).Value = "ZDA0000009A";
            ws.Cell(6, 2).Value = "n9";
            ws.Cell(6, 3).Value = "d9";
            ws.Cell(6, 4).Value = "S-NEW";
            ws.Cell(6, 5).Value = "";
        });

        try
        {
            var analyzed = await analyze.ExecuteAsync(new AnalyzeBomRequest(path));
            Assert.True(analyzed.IsSuccess);
            var key = $"{analyzed.Data!.FinishedCode}||{analyzed.Data.Version}";

            // hold the lock to simulate "import in progress"
            Assert.True(gate.TryEnter(key, out var sem));
            try
            {
                var res = await import.ExecuteAsync(new ImportBomNewMaterialsRequest(path));
                Assert.False(res.IsSuccess);
                Assert.Equal(ErrorCodes.BOM_IMPORT_IN_PROGRESS, res.Error!.Code);
            }
            finally
            {
                gate.Exit(key, sem);
            }
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }

    private static string WriteBomTempFile(Action<XLWorkbook> build)
    {
        var path = Path.Combine(Path.GetTempPath(), $"mcs_bom_{Guid.NewGuid():N}.xlsx");
        using var wb = new XLWorkbook();
        build(wb);
        wb.SaveAs(path);
        return path;
    }
}

