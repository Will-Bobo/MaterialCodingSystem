namespace MaterialCodingSystem.Application.Contracts;

public sealed record GroupInfoDto(
    int GroupId,
    string CategoryCode,
    int SerialNo,
    string ExistingSuffixes,
    string NextSuffix
);

