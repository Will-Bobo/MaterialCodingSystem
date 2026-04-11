namespace MaterialCodingSystem.Application.Contracts;

public sealed record CreateCategoryRequest(string Code, string Name);

public sealed record CreateCategoryResponse(string Code, string Name);

public sealed record CategoryDto(string Code, string Name);

