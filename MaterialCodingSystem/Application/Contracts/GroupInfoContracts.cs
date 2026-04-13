namespace MaterialCodingSystem.Application.Contracts;

public sealed record GroupInfoDto(
    int GroupId,
    string CategoryCode,
    string CategoryName,
    int SerialNo,
    string ExistingSuffixes,
    string NextSuffix
);

