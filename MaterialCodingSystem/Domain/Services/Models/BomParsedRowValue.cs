namespace MaterialCodingSystem.Domain.Services.Models;

public sealed record BomParsedRowValue(
    int ExcelRowNo,
    string Code,
    string Name,
    string Spec,
    string Description,
    string Brand);

