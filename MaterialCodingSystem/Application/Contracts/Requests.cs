namespace MaterialCodingSystem.Application.Contracts;

public sealed record CreateMaterialItemARequest(
    string CategoryCode,
    string Spec,
    string Name,
    string Description,
    string? Brand
);

public sealed record CreateMaterialItemManualRequest(
    string CategoryCode,
    string Code,
    string Spec,
    string Name,
    string Description,
    string? Brand
);

public sealed record CreateReplacementRequest(
    int GroupId,
    string Spec,
    string Name,
    string Description,
    string? Brand
);

public sealed record CreateReplacementByCodeRequest(
    string BaseMaterialCode,
    string Spec,
    string Description,
    string? Brand
);

public sealed record DeprecateRequest(string Code);

