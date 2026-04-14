namespace MaterialCodingSystem.Presentation.Models;

public sealed class CreateReplacementConfirmModel
{
    public string BaseMaterialCode { get; init; } = "";
    public string BaseSpec { get; init; } = "";
    public string BaseDescription { get; init; } = "";
    public string BaseBrand { get; init; } = "";

    public string Spec { get; init; } = "";
    public string Description { get; init; } = "";
    public string Brand { get; init; } = "";
}

