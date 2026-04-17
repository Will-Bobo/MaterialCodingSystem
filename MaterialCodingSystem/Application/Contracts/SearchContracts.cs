namespace MaterialCodingSystem.Application.Contracts;

public sealed record SearchQuery(
    string? CodeKeyword,
    string? SpecKeyword,
    string? CategoryCode,
    bool IncludeDeprecated,
    int Limit,
    int Offset
);

public sealed record MaterialItemSummary(string Code, string Name, string? DisplayName, string Spec, string Description, string? Brand, long Status)
{
    public string DisplayNameForUi => string.IsNullOrWhiteSpace(DisplayName) ? Name : DisplayName!;
}

public sealed record MaterialItemSpecHit(string Code, string Spec, string Description, string Name, string? DisplayName, string? Brand, long Status, long GroupId)
{
    public string DisplayNameForUi => string.IsNullOrWhiteSpace(DisplayName) ? Name : DisplayName!;
}

public sealed record PagedResult<T>(int Total, IReadOnlyList<T> Items);

