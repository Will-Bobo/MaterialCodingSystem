using MaterialCodingSystem.Application;
using MaterialCodingSystem.Application.Interfaces;
using MaterialCodingSystem.Infrastructure.Excel;
using NPOI.HSSF.UserModel;

namespace MaterialCodingSystem.Tests.Bom;

public sealed class BomFileFormatDetectorTests
{
    [Fact]
    public void Detect_Xls_OleSignature_Should_Return_Xls()
    {
        var path = Path.Combine(Path.GetTempPath(), $"mcs_fmt_{Guid.NewGuid():N}.xls");
        try
        {
            WriteXls(path);
            var detector = new BomFileFormatDetector();
            var res = detector.Detect(path);
            Assert.True(res.IsSuccess);
            Assert.Equal(BomExcelFileFormat.Xls, res.Data);
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }

    [Fact]
    public void Detect_TextFile_Should_Return_INVALID_EXCEL()
    {
        var path = Path.Combine(Path.GetTempPath(), $"mcs_fmt_{Guid.NewGuid():N}.txt");
        try
        {
            File.WriteAllText(path, "not excel");
            var detector = new BomFileFormatDetector();
            var res = detector.Detect(path);
            Assert.True(res.IsSuccess);
            Assert.Equal(BomExcelFileFormat.Unknown, res.Data);
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }

    private static void WriteXls(string path)
    {
        var wb = new HSSFWorkbook();
        wb.CreateSheet("BOM");
        using var fs = File.Create(path);
        wb.Write(fs);
        fs.Flush();
    }
}

