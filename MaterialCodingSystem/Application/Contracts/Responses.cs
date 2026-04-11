namespace MaterialCodingSystem.Application.Contracts;

public sealed record CreateMaterialItemAResponse(
    int GroupId,
    string CategoryCode,
    int SerialNo,
    string Code,
    string Suffix,
    string Spec,
    string SpecNormalized
);

public sealed record CreateReplacementResponse(
    int ItemId,
    int GroupId,
    string Code,
    string Suffix,
    string Spec,
    string SpecNormalized
);

public sealed record DeprecateResponse(string Code, int Status);

