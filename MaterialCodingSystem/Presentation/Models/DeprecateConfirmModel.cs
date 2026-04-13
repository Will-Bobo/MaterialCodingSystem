namespace MaterialCodingSystem.Presentation.Models;

public sealed class DeprecateConfirmModel
{
    public string Code { get; init; } = "";
    public string Spec { get; init; } = "";
    public string Description { get; init; } = "";
    public string Name { get; init; } = "";
    public string? Brand { get; init; }
}

