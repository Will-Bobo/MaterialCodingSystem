namespace MaterialCodingSystem.Presentation.Models;

public sealed record CreateMaterialConfirmModel
{
    public string WindowTitle { get; init; } = "确认创建主物料";
    public string HintText { get; init; } = "请确认以下信息后再创建";
    public string ConfirmButtonText { get; init; } = "确认创建";
    public string CancelButtonText { get; init; } = "取消";

    public string Code { get; init; } = "";
    public string Spec { get; init; } = "";
    public string Description { get; init; } = "";
    public string Name { get; init; } = "";
    public string Brand { get; init; } = "";
}

