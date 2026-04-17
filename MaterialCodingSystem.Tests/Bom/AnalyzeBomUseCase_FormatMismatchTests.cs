using MaterialCodingSystem.Application;
using MaterialCodingSystem.Application.Contracts;
using MaterialCodingSystem.Infrastructure.Excel;
using MaterialCodingSystem.Infrastructure.Sqlite;
using MaterialCodingSystem.Tests.Application;
using MaterialCodingSystem.Tests.Infrastructure;
using Microsoft.Extensions.Logging.Abstractions;
using NPOI.HSSF.UserModel;
using NPOI.SS.UserModel;

namespace MaterialCodingSystem.Tests.Bom;

public sealed class AnalyzeBomUseCase_FormatMismatchTests
{
    [Fact]
    public async Task Execute_When_Ext_Is_Xlsx_But_Actual_Is_Xls_Should_Not_Be_Blocked_By_FormatDetector()
    {
        var path = Path.Combine(Path.GetTempPath(), $"mcs_bom_{Guid.NewGuid():N}.xlsx"); // 伪装后缀
        try
        {
            WriteMinimalBomXls(path);

            var gridParser = new UnifiedBomGridParser(
                NullLogger<UnifiedBomGridParser>.Instance,
                new MaterialCodingSystem.Infrastructure.Excel.Adapters.ClosedXmlBomGridAdapter(),
                new MaterialCodingSystem.Infrastructure.Excel.Adapters.ExcelDataReaderBomGridAdapter());
            var parseBom = new ParseBomUseCase(gridParser);
            var detector = new BomFileFormatDetector();

            await using var db = await SqliteTestDb.CreateAsync();
            var repo = new SqliteMaterialRepository(db.Connection);
            var uow = new SqliteUnitOfWork(db.Connection);

            var useCase = new AnalyzeBomUseCase(parseBom, detector, repo, uow, NullLogger<AnalyzeBomUseCase>.Instance);
            var res = await useCase.ExecuteAsync(new AnalyzeBomRequest(path));

            Assert.False(res.IsSuccess);
            // 不应被 “真实性检测层” 以“后缀不一致”阻断；失败应来自解析器（ClosedXML 读 xls 会 BOM_FILE_INVALID）
            Assert.Equal(ErrorCodes.BOM_FILE_INVALID, res.Error!.Code);
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }

    private static void WriteMinimalBomXls(string path)
    {
        var wb = new HSSFWorkbook();
        var sheet = wb.CreateSheet("BOM");

        sheet.CreateRow(0).CreateCell(0).SetCellValue("成品编码");
        sheet.GetRow(0).CreateCell(1).SetCellValue("CP00A1062A");

        sheet.CreateRow(1).CreateCell(0).SetCellValue("PCBA版本号");
        sheet.GetRow(1).CreateCell(1).SetCellValue("KC001_RS_MB_V1.2_A_V1.0-20260414");

        var header = sheet.CreateRow(4);
        header.CreateCell(0).SetCellValue("编码");
        header.CreateCell(1).SetCellValue("名称");
        header.CreateCell(2).SetCellValue("描述");
        header.CreateCell(3).SetCellValue("规格");
        header.CreateCell(4).SetCellValue("品牌");

        var row = sheet.CreateRow(5);
        row.CreateCell(0).SetCellValue("ZDA0000009A");
        row.CreateCell(1).SetCellValue("n9");
        row.CreateCell(2).SetCellValue("d9");
        row.CreateCell(3).SetCellValue("S-NEW");
        row.CreateCell(4).SetCellValue("B");

        using var fs = File.Create(path);
        wb.Write(fs);
        fs.Flush();
    }
}

