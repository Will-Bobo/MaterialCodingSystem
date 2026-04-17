using MaterialCodingSystem.Application;
using MaterialCodingSystem.Infrastructure.Excel;
using Microsoft.Extensions.Logging.Abstractions;
using NPOI.HSSF.UserModel;
using NPOI.SS.UserModel;

namespace MaterialCodingSystem.Tests.Bom;

public sealed class BomExcelParser_XlsTests
{
    [Fact]
    public void Parse_Xls_Should_Read_Header_And_Detail_And_RowNo()
    {
        var path = Path.Combine(Path.GetTempPath(), $"mcs_bom_{Guid.NewGuid():N}.xls");
        try
        {
            WriteXls(path);
            var parseBom = new ParseBomUseCase(new UnifiedBomGridParser(
                NullLogger<UnifiedBomGridParser>.Instance,
                new MaterialCodingSystem.Infrastructure.Excel.Adapters.ClosedXmlBomGridAdapter(),
                new MaterialCodingSystem.Infrastructure.Excel.Adapters.ExcelDataReaderBomGridAdapter()));
            var res = parseBom.Execute(path);
            Assert.True(res.IsSuccess, res.Error?.Message);
            Assert.Equal("CP00A1062A", res.Data!.Header.FinishedCode);
            Assert.Equal("KC001_RS_MB_V1.2_A_V1.0-20260414", res.Data.Header.Version);
            Assert.Single(res.Data.Rows);
            Assert.Equal(6, res.Data.Rows[0].ExcelRowNo);
            Assert.Equal("ZDA0000009A", res.Data.Rows[0].Code);
            Assert.Equal("S-NEW", res.Data.Rows[0].Spec);
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }

    private static void WriteXls(string path)
    {
        var wb = new HSSFWorkbook();
        var sheet = wb.CreateSheet("BOM");

        sheet.CreateRow(0).CreateCell(0).SetCellValue("成品编码");
        sheet.GetRow(0).CreateCell(1).SetCellValue("CP00A1062A");

        sheet.CreateRow(1).CreateCell(0).SetCellValue("PCBA版本号");
        sheet.GetRow(1).CreateCell(1).SetCellValue("KC001_RS_MB_V1.2_A_V1.0-20260414");

        var header = sheet.CreateRow(4); // Excel row 5
        header.CreateCell(0).SetCellValue("编码");
        header.CreateCell(1).SetCellValue("名称");
        header.CreateCell(2).SetCellValue("描述");
        header.CreateCell(3).SetCellValue("规格");
        header.CreateCell(4).SetCellValue("品牌");

        var row = sheet.CreateRow(5); // Excel row 6
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

