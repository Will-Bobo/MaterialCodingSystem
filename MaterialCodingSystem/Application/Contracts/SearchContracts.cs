namespace MaterialCodingSystem.Application.Contracts;

public sealed record SearchQuery(
    string? CodeKeyword,
    string? SpecKeyword,
    string? CategoryCode,
    bool IncludeDeprecated,
    int Limit,
    int Offset
);

public sealed record MaterialItemSummary(string Code, string Name, string Spec, string? Brand);

public sealed record MaterialItemSpecHit(string Code, string Spec, string Description, string Name, string? Brand, long GroupId);

public sealed record PagedResult<T>(int Total, IReadOnlyList<T> Items);

