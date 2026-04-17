using MaterialCodingSystem.Application;
using MaterialCodingSystem.Infrastructure.Excel;
using Microsoft.Extensions.Logging.Abstractions;

namespace MaterialCodingSystem.Tests.Bom;

public sealed class Template_BOM_Xls_HeaderParseTests
{
    [Fact]
    public void Parse_Template_BOM_Xls_Should_Read_FinishedCode_And_Version()
    {
        var src = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "docs", "template", "BOM.xls"));
        Assert.True(File.Exists(src), $"template file not found: {src}");

        var tmp = Path.Combine(Path.GetTempPath(), $"mcs_template_{Guid.NewGuid():N}.xls");
        try
        {
            File.Copy(src, tmp, overwrite: true);

            var gridParser = new UnifiedBomGridParser(
                NullLogger<UnifiedBomGridParser>.Instance,
                new MaterialCodingSystem.Infrastructure.Excel.Adapters.ClosedXmlBomGridAdapter(),
                new MaterialCodingSystem.Infrastructure.Excel.Adapters.ExcelDataReaderBomGridAdapter());

            var gridRes = gridParser.Parse(tmp);
            Assert.True(gridRes.IsSuccess, gridRes.Error?.Message);

            // 帮助定位模板解析失败：若后续断言失败，将输出前若干个非空单元格文本
            var sample = new List<string>();
            var grid = gridRes.Data!;
            for (var r = 0; r < grid.RowCount && sample.Count < 60; r++)
            {
                for (var c = 0; c < grid.ColCount && sample.Count < 60; c++)
                {
                    var t = grid.GetCell(r, c)?.Trim() ?? "";
                    if (t.Length == 0) continue;
                    sample.Add($"r{grid.GetRowIndex(r)}c{c + 1}:{t}");
                }
            }
            var sampleText = string.Join(" | ", sample);

            var parseBom = new ParseBomUseCase(new UnifiedBomGridParser(
                NullLogger<UnifiedBomGridParser>.Instance,
                new MaterialCodingSystem.Infrastructure.Excel.Adapters.ClosedXmlBomGridAdapter(),
                new MaterialCodingSystem.Infrastructure.Excel.Adapters.ExcelDataReaderBomGridAdapter()));

            var res = parseBom.Execute(tmp);
            Assert.True(res.IsSuccess, (res.Error?.Message ?? "parse failed") + " || sample=" + sampleText);
            Assert.Equal("CP00A1062A", res.Data!.Header.FinishedCode);
            Assert.Equal("KC001_RS_MB_V1.2_A_V1.0-20260414", res.Data.Header.Version);
        }
        finally
        {
            if (File.Exists(tmp)) File.Delete(tmp);
        }
    }
}

