namespace MaterialCodingSystem.Application.Contracts;

public sealed record AnalyzeBomRequest(string FilePath);

public enum BomAuditStatus
{
    PASS = 1,
    NEW = 2,
    ERROR = 3
}

public sealed record BomAuditRowDto(
    int ExcelRowNo,
    BomAuditStatus Status,
    string Code,
    string Name,
    string Spec,
    string Description,
    string Brand,
    string? ErrorReason
);

public sealed record AnalyzeBomResponse(
    string FinishedCode,
    string Version,
    IReadOnlyList<BomAuditRowDto> Rows,
    int TotalCount,
    int PassCount,
    int NewCount,
    int ErrorCount,
    int MissingCodeErrorCount,
    int? FirstErrorRowNo
);

