using MaterialCodingSystem.Application.Contracts;

namespace MaterialCodingSystem.Application.Interfaces;

public sealed record BomParsedHeader(string FinishedCode, string Version);

public sealed record BomParsedRow(
    int ExcelRowNo,
    string Code,
    string Name,
    string Spec,
    string Description,
    string Brand
);

public sealed record BomParsedDocument(
    BomParsedHeader Header,
    IReadOnlyList<BomParsedRow> Rows
);

public interface IBomExcelParser
{
    Result<BomParsedDocument> Parse(string filePath);
}

