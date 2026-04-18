using MaterialCodingSystem.Application.Contracts;
using MaterialCodingSystem.Application.Interfaces;
using MaterialCodingSystem.Application.Logging;
using MaterialCodingSystem.Domain.Services;
using Microsoft.Extensions.Logging;

namespace MaterialCodingSystem.Application;

/// <summary>
/// 解析 BOM（只做解析编排）：Grid Parser -> Domain Rules -> Parsed DTO。
/// </summary>
public sealed class ParseBomUseCase
{
    private readonly IBomGridParser _gridParser;
    private readonly ILogger<ParseBomUseCase> _logger;

    public ParseBomUseCase(IBomGridParser gridParser, ILogger<ParseBomUseCase>? logger = null)
    {
        _gridParser = gridParser;
        _logger = logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<ParseBomUseCase>.Instance;
    }

    public Result<BomParsedDocument> Execute(string filePath)
    {
        return McsLoggingExtensions.RunUseCaseSync(_logger, McsActions.BomParse, McsLog.FileNameForLog(filePath),
            () =>
            {
        var gridRes = _gridParser.Parse(filePath);
        if (!gridRes.IsSuccess || gridRes.Data is null)
            return Result<BomParsedDocument>.Fail(gridRes.Error!.Code, gridRes.Error.Message);

        var parsed = BomParsingRules.Parse(gridRes.Data);
        if (!parsed.IsSuccess || parsed.Success is null)
            return MapFailure(parsed.Failure);

        var rows = parsed.Success.Rows
            .Select(r => new BomParsedRow(r.ExcelRowNo, r.Code, r.Name, r.Spec, r.Description, r.Brand))
            .ToList();

        return Result<BomParsedDocument>.Ok(new BomParsedDocument(
            new BomParsedHeader(parsed.Success.FinishedCode, parsed.Success.Version),
            rows));
            },
            r => r.IsSuccess && r.Data is not null ? ("line_count", r.Data.Rows.Count) : null);
    }

    private static Result<BomParsedDocument> MapFailure(MaterialCodingSystem.Domain.Services.Models.BomParsingFailure? f)
    {
        if (f is null)
            return Result<BomParsedDocument>.Fail(ErrorCodes.BOM_FILE_INVALID, "解析 BOM 失败。");

        return f.Kind switch
        {
            MaterialCodingSystem.Domain.Services.Models.BomParsingFailureKind.HeaderMissing
                => Result<BomParsedDocument>.Fail(ErrorCodes.BOM_HEADER_MISSING, "表头缺失：未识别到成品编码或版本号。"),
            MaterialCodingSystem.Domain.Services.Models.BomParsingFailureKind.DetailHeaderRowNotFound
                => Result<BomParsedDocument>.Fail(ErrorCodes.BOM_FILE_INVALID, "明细表头行未找到。"),
            MaterialCodingSystem.Domain.Services.Models.BomParsingFailureKind.MissingColumn
                => Result<BomParsedDocument>.Fail(ErrorCodes.BOM_FILE_INVALID, $"缺少必需列：{f.MissingColumnName}"),
            _ => Result<BomParsedDocument>.Fail(ErrorCodes.BOM_FILE_INVALID, "解析 BOM 失败。")
        };
    }
}

