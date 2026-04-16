namespace MaterialCodingSystem.Application.Contracts;

public enum CreateMaterialCodeMode
{
    Auto = 0,
    ManualExistingCode = 1
}

public sealed record CreateMaterialRequest(
    string CategoryCode,
    string Spec,
    string Name,
    string Description,
    string? Brand,
    CreateMaterialCodeMode CodeMode = CreateMaterialCodeMode.Auto,
    string? ExistingCode = null,
    bool ForceConfirm = false,
    string? RequestId = null
);

// Backward compatible alias for existing call sites (will be removed in future)
public sealed record CreateMaterialItemARequest(
    string CategoryCode,
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

