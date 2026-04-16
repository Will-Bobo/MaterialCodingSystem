using System.Diagnostics;
using MaterialCodingSystem.Application.Contracts;
using MaterialCodingSystem.Presentation.Models;
using MaterialCodingSystem.Presentation.Services;

namespace MaterialCodingSystem.Presentation.UiSemantics;

public interface IUiRenderer
{
    /// <summary>AppError + context → single <see cref="UiRenderPlan"/>; does not touch ViewModels.</summary>
    UiRenderPlan BuildRenderPlan(AppError error, ContextType context);

    void LogTechnicalFailure(AppError error);

    bool ConfirmDuplicateCreate();

    bool ConfirmCreateMaterial(CreateMaterialConfirmModel model);

    bool ConfirmWarning(string title, string body);

    bool ConfirmCreateReplacement(CreateReplacementConfirmModel model);

    Task<bool> ConfirmDeprecateAsync(DeprecateConfirmModel model);
}

public sealed class WpfUiRenderer : IUiRenderer
{
    private readonly IUiDialogService _dialogs;

    public WpfUiRenderer(IUiDialogService dialogs)
    {
        _dialogs = dialogs;
    }

    public void LogTechnicalFailure(AppError error) =>
        Trace.WriteLine($"[MCS] {error.Code}: {error.Message}");

    public UiRenderPlan BuildRenderPlan(AppError error, ContextType context)
    {
        LogTechnicalFailure(error);
        return UiPolicy.Resolve(error, context);
    }

    public bool ConfirmDuplicateCreate()
    {
        return _dialogs.Confirm(
            UiResources.Get(UiResourceKeys.Confirm.DuplicateTitle),
            UiResources.Get(UiResourceKeys.Confirm.DuplicateBody));
    }

    public bool ConfirmCreateMaterial(CreateMaterialConfirmModel model)
    {
        var body =
            $"规格号：{model.Spec}\n" +
            $"规格描述：{model.Description}\n" +
            $"名称：{model.Name}\n" +
            $"品牌：{model.Brand}";

        return _dialogs.Confirm("确认创建主物料", body);
    }

    public bool ConfirmWarning(string title, string body)
    {
        return _dialogs.Confirm(title, body);
    }

    public bool ConfirmCreateReplacement(CreateReplacementConfirmModel model)
    {
        var body =
            $"基准编码：{model.BaseMaterialCode}\n" +
            $"基准规格号：{model.BaseSpec}\n" +
            $"基准规格描述：{model.BaseDescription}\n" +
            $"基准品牌：{model.BaseBrand}\n" +
            "\n" +
            $"替代料规格号：{model.Spec}\n" +
            $"替代料规格描述：{model.Description}\n" +
            $"替代料品牌：{model.Brand}";

        return _dialogs.Confirm("确认创建替代料", body);
    }

    public Task<bool> ConfirmDeprecateAsync(DeprecateConfirmModel model)
    {
        var dlg = new DeprecateConfirmWindow(model)
        {
            Owner = System.Windows.Application.Current?.MainWindow
        };
        var r = dlg.ShowDialog();
        return Task.FromResult(r == true);
    }
}
