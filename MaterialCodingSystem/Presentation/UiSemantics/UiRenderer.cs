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

    bool Confirm(string title, string body);

    bool ConfirmDuplicateCreate();

    bool ConfirmCreateMaterial(CreateMaterialConfirmModel model);

    bool ConfirmImportMaterial(CreateMaterialConfirmModel model);

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

    public bool Confirm(string title, string body) => _dialogs.Confirm(title, body);

    public bool ConfirmDuplicateCreate()
    {
        return _dialogs.Confirm(
            UiResources.Get(UiResourceKeys.Confirm.DuplicateTitle),
            UiResources.Get(UiResourceKeys.Confirm.DuplicateBody));
    }

    public bool ConfirmCreateMaterial(CreateMaterialConfirmModel model)
    {
        model = model with
        {
            WindowTitle = "确认创建主物料",
            HintText = "请确认以下信息后再创建",
            ConfirmButtonText = "确认创建",
            CancelButtonText = "取消"
        };
        var dlg = new CreateMaterialConfirmWindow(model)
        {
            Owner = System.Windows.Application.Current?.MainWindow
        };
        var r = dlg.ShowDialog();
        return r == true;
    }

    public bool ConfirmImportMaterial(CreateMaterialConfirmModel model)
    {
        model = model with
        {
            WindowTitle = "确认导入物料库",
            HintText = "请确认以下信息后再导入",
            ConfirmButtonText = "确认导入",
            CancelButtonText = "取消"
        };
        var dlg = new CreateMaterialConfirmWindow(model)
        {
            Owner = System.Windows.Application.Current?.MainWindow
        };
        var r = dlg.ShowDialog();
        return r == true;
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
