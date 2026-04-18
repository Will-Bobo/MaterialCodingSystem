using System.IO;
using System.Text;
using ClosedXML.Excel;
using ExcelDataReader;
using MaterialCodingSystem.Application;
using MaterialCodingSystem.Application.Contracts;
using MaterialCodingSystem.Application.Interfaces;
using MaterialCodingSystem.Domain.Services.Models;
using MaterialCodingSystem.Infrastructure.Excel.Adapters;
using Microsoft.Extensions.Logging;

namespace MaterialCodingSystem.Infrastructure.Excel;

/// <summary>
/// 统一网格解析器：只负责把文件转换成 <see cref="BomGrid"/>，不调用 BOM 规则。
/// </summary>
public sealed class UnifiedBomGridParser : IBomGridParser
{
    private readonly ILogger<UnifiedBomGridParser> _logger;
    private readonly ClosedXmlBomGridAdapter _xlsxAdapter;
    private readonly ExcelDataReaderBomGridAdapter _xlsAdapter;

    public UnifiedBomGridParser(
        ILogger<UnifiedBomGridParser> logger,
        ClosedXmlBomGridAdapter xlsxAdapter,
        ExcelDataReaderBomGridAdapter xlsAdapter)
    {
        _logger = logger;
        _xlsxAdapter = xlsxAdapter;
        _xlsAdapter = xlsAdapter;
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
    }

    public Result<BomGrid> Parse(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
            return Result<BomGrid>.Fail(ErrorCodes.VALIDATION_ERROR, "请提供文件路径。");

        if (!File.Exists(filePath))
            return Result<BomGrid>.Fail(ErrorCodes.NOT_FOUND, "文件不存在。");

        try
        {
            var ext = Path.GetExtension(filePath) ?? "";
            if (ext.Equals(".xlsx", StringComparison.OrdinalIgnoreCase))
                return Result<BomGrid>.Ok(ReadXlsxToGrid(filePath));
            if (ext.Equals(".xls", StringComparison.OrdinalIgnoreCase))
                return Result<BomGrid>.Ok(ReadXlsToGrid(filePath));
            return Result<BomGrid>.Fail(ErrorCodes.BOM_FILE_INVALID, "不支持的 Excel 扩展名。");
        }
        catch (IOException ex) when (IsFileInUse(ex))
        {
            _logger.LogInformation(ex, "BOM file locked: {filePath}", filePath);
            return Result<BomGrid>.Fail(ErrorCodes.BOM_FILE_LOCKED, "文件正在使用中，请关闭后重试。");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "BOM grid parse failed: {filePath}", filePath);
            return Result<BomGrid>.Fail(ErrorCodes.BOM_FILE_INVALID, "解析 Excel 失败。");
        }
    }

    private BomGrid ReadXlsxToGrid(string filePath)
    {
        using var wb = new XLWorkbook(filePath);
        var ws = wb.Worksheets.FirstOrDefault();
        if (ws is null) return new BomGrid(Array.Empty<BomGridRow>());
        return _xlsxAdapter.ToGrid(ws);
    }

    private BomGrid ReadXlsToGrid(string filePath)
    {
        using var stream = File.Open(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
        using var reader = ExcelReaderFactory.CreateReader(stream, new ExcelReaderConfiguration
        {
            // 兼容旧 xls 中文字段（如 BOM 模板）在非 Unicode codepage 下的读取
            FallbackEncoding = Encoding.GetEncoding(936) // GBK
        });
        var ds = reader.AsDataSet(new ExcelDataSetConfiguration
        {
            ConfigureDataTable = _ => new ExcelDataTableConfiguration { UseHeaderRow = false }
        });
        if (ds.Tables.Count == 0) return new BomGrid(Array.Empty<BomGridRow>());
        return _xlsAdapter.ToGrid(ds.Tables[0]);
    }

    private static bool IsFileInUse(IOException ex)
    {
        const int sharingViolationHResult = unchecked((int)0x80070020);
        if (ex.HResult == sharingViolationHResult)
            return true;
        return ex.Message.Contains("another process", StringComparison.OrdinalIgnoreCase)
               || ex.Message.Contains("另一个程序", StringComparison.OrdinalIgnoreCase)
               || ex.Message.Contains("正由另一进程", StringComparison.OrdinalIgnoreCase);
    }
}

