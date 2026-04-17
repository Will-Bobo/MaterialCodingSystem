namespace MaterialCodingSystem.Domain.Services.Models;

public enum BomParsingFailureKind
{
    None = 0,
    HeaderMissing = 1,
    DetailHeaderRowNotFound = 2,
    MissingColumn = 3,
}

public sealed record BomParsingFailure(BomParsingFailureKind Kind, string? MissingColumnName);

public sealed record BomParsingSuccess(
    string FinishedCode,
    string Version,
    IReadOnlyList<BomParsedRowValue> Rows);

public sealed record BomParsingResult(
    BomParsingSuccess? Success,
    BomParsingFailure? Failure)
{
    public static BomParsingResult Ok(BomParsingSuccess s) => new(s, null);
    public static BomParsingResult Fail(BomParsingFailure f) => new(null, f);
    public bool IsSuccess => Success is not null;
}

