using MaterialCodingSystem.Application.Contracts;
using MaterialCodingSystem.Application.Interfaces;
using MaterialCodingSystem.Domain.Services;

namespace MaterialCodingSystem.Application;

/// <summary>
/// 解析 BOM（只做解析编排）：Grid Parser -> Domain Rules -> Parsed DTO。
/// </summary>
public sealed class ParseBomUseCase
{
    private readonly IBomGridParser _gridParser;

    public ParseBomUseCase(IBomGridParser gridParser)
    {
        _gridParser = gridParser;
    }

    public Result<BomParsedDocument> Execute(string filePath)
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
    }

    private static Result<BomParsedDocument> MapFailure(MaterialCodingSystem.Domain.Services.Models.BomParsingFailure? f)
    {
        if (f is null)
            return Result<BomParsedDocument>.Fail(ErrorCodes.BOM_FILE_INVALID, "failed to parse excel.");

        return f.Kind switch
        {
            MaterialCodingSystem.Domain.Services.Models.BomParsingFailureKind.HeaderMissing
                => Result<BomParsedDocument>.Fail(ErrorCodes.BOM_HEADER_MISSING, "finished_code/version missing."),
            MaterialCodingSystem.Domain.Services.Models.BomParsingFailureKind.DetailHeaderRowNotFound
                => Result<BomParsedDocument>.Fail(ErrorCodes.BOM_FILE_INVALID, "detail header row not found."),
            MaterialCodingSystem.Domain.Services.Models.BomParsingFailureKind.MissingColumn
                => Result<BomParsedDocument>.Fail(ErrorCodes.BOM_FILE_INVALID, $"missing column: {f.MissingColumnName}"),
            _ => Result<BomParsedDocument>.Fail(ErrorCodes.BOM_FILE_INVALID, "failed to parse excel.")
        };
    }
}

