namespace MaterialCodingSystem.Application.Contracts;

public sealed record CreateCategoryRequest(string Code, string Name, int StartSerialNo = 1);

public sealed record CreateCategoryResponse(string Code, string Name, int StartSerialNo);

public sealed record CategoryDto(string Code, string Name, int StartSerialNo);

