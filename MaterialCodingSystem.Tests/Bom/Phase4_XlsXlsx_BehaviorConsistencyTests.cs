using ClosedXML.Excel;
using MaterialCodingSystem.Application;
using MaterialCodingSystem.Infrastructure.Excel;
using Microsoft.Extensions.Logging.Abstractions;
using NPOI.HSSF.UserModel;
using NPOI.SS.UserModel;

namespace MaterialCodingSystem.Tests.Bom;

public sealed class Phase4_XlsXlsx_BehaviorConsistencyTests
{
    [Fact]
    public void Parse_SameContent_Xls_And_Xlsx_Should_Output_Exactly_Same()
    {
        var xlsx = Path.Combine(Path.GetTempPath(), $"mcs_bom_{Guid.NewGuid():N}.xlsx");
        var xls = Path.Combine(Path.GetTempPath(), $"mcs_bom_{Guid.NewGuid():N}.xls");

        try
        {
            WriteXlsx(xlsx);
            WriteXls(xls);

            var parseBom = new ParseBomUseCase(new UnifiedBomGridParser(
                NullLogger<UnifiedBomGridParser>.Instance,
                new MaterialCodingSystem.Infrastructure.Excel.Adapters.ClosedXmlBomGridAdapter(),
                new MaterialCodingSystem.Infrastructure.Excel.Adapters.ExcelDataReaderBomGridAdapter()));

            var a = parseBom.Execute(xlsx);
            var b = parseBom.Execute(xls);

            Assert.True(a.IsSuccess, a.Error?.Message);
            Assert.True(b.IsSuccess, b.Error?.Message);

            Assert.Equal(a.Data!.Header.FinishedCode, b.Data!.Header.FinishedCode);
            Assert.Equal(a.Data.Header.Version, b.Data.Header.Version);
            Assert.Equal(a.Data.Rows.Count, b.Data.Rows.Count);

            for (var i = 0; i < a.Data.Rows.Count; i++)
            {
                var ra = a.Data.Rows[i];
                var rb = b.Data.Rows[i];
                Assert.Equal(ra.ExcelRowNo, rb.ExcelRowNo);
                Assert.Equal(ra.Code, rb.Code);
                Assert.Equal(ra.Name, rb.Name);
                Assert.Equal(ra.Description, rb.Description);
                Assert.Equal(ra.Spec, rb.Spec);
                Assert.Equal(ra.Brand, rb.Brand);
            }
        }
        finally
        {
            if (File.Exists(xlsx)) File.Delete(xlsx);
            if (File.Exists(xls)) File.Delete(xls);
        }
    }

    private static void WriteXlsx(string path)
    {
        using var wb = new XLWorkbook();
        var ws = wb.AddWorksheet("BOM");

        ws.Cell(1, 1).Value = "成品编码";
        ws.Cell(1, 2).Value = "CP00A1062A";
        ws.Cell(2, 1).Value = "PCBA版本号";
        ws.Cell(2, 2).Value = "KC001_RS_MB_V1.2_A_V1.0-20260414";

        ws.Cell(5, 1).Value = "编码";
        ws.Cell(5, 2).Value = "名称";
        ws.Cell(5, 3).Value = "描述";
        ws.Cell(5, 4).Value = "规格";
        ws.Cell(5, 5).Value = "品牌";

        ws.Cell(6, 1).Value = "ZDA0000009A";
        ws.Cell(6, 2).Value = "n9";
        ws.Cell(6, 3).Value = "d9";
        ws.Cell(6, 4).Value = "S-NEW";
        ws.Cell(6, 5).Value = "B";

        wb.SaveAs(path);
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

