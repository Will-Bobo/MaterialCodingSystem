using Dapper;
using MaterialCodingSystem.Application;
using MaterialCodingSystem.Infrastructure.Excel;
using MaterialCodingSystem.Infrastructure.Sqlite;
using MaterialCodingSystem.Tests.Infrastructure;
using Microsoft.Extensions.Logging.Abstractions;

namespace MaterialCodingSystem.Tests.Bom;

public sealed class Phase3_CanArchiveAndHistoryTests
{
    [Fact]
    public async Task CanArchive_When_New_Not_Zero_Should_Disallow()
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
        var archiveRepo = new SqliteBomArchiveRepository(conn);
        var can = new CanArchiveBomUseCase(analyze, archiveRepo);

        var path = Phase2_BomArchiveAndImportTests_WriteBomWithOneNew();
        try
        {
            var res = await can.ExecuteAsync(new CanArchiveBomRequest(path));
            Assert.True(res.IsSuccess, res.Error?.Message);
            Assert.False(res.Data!.IsAllowed);
            Assert.Contains("NEW", res.Data.Reason);
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }

    [Fact]
    public async Task GetBomArchiveList_Filter_By_FinishedCode_Should_Work()
    {
        await using var db = await SqliteTestDb.CreateAsync();
        var conn = db.Connection;
        await conn.ExecuteAsync("INSERT INTO bom_archive(finished_code,version,file_path) VALUES ('A','v1','p1'),('B','v2','p2');");

        var repo = new SqliteBomArchiveRepository(conn);
        var use = new GetBomArchiveListUseCase(repo);
        var res = await use.ExecuteAsync(new GetBomArchiveListRequest("A"));
        Assert.True(res.IsSuccess);
        Assert.Single(res.Data!.Items);
        Assert.Equal("A", res.Data.Items[0].FinishedCode);
    }

    private static string Phase2_BomArchiveAndImportTests_WriteBomWithOneNew()
    {
        // minimal xlsx for parser: header + required columns + one NEW row
        var path = Path.Combine(Path.GetTempPath(), $"mcs_bom_{Guid.NewGuid():N}.xlsx");
        using var wb = new ClosedXML.Excel.XLWorkbook();
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

        wb.SaveAs(path);
        return path;
    }
}

