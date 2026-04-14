using System.Diagnostics;
using MaterialCodingSystem.Application.Contracts;
using MaterialCodingSystem.Presentation.Models;

namespace MaterialCodingSystem.Presentation.UiSemantics;

public interface IUiRenderer
{
    /// <summary>AppError + context → single <see cref="UiRenderPlan"/>; does not touch ViewModels.</summary>
    UiRenderPlan BuildRenderPlan(AppError error, ContextType context);

    void LogTechnicalFailure(AppError error);

    bool ConfirmDuplicateCreate();

    bool ConfirmCreateMaterial(CreateMaterialConfirmModel model);

    Task<bool> ConfirmDeprecateAsync(DeprecateConfirmModel model);
}

public sealed class WpfUiRenderer : IUiRenderer
{
    public void LogTechnicalFailure(AppError error) =>
        Trace.WriteLine($"[MCS] {error.Code}: {error.Message}");

    public UiRenderPlan BuildRenderPlan(AppError error, ContextType context)
    {
        LogTechnicalFailure(error);
        return UiPolicy.Resolve(error, context);
    }

    public bool ConfirmDuplicateCreate()
    {
        var r = System.Windows.MessageBox.Show(
            UiResources.Get(UiResourceKeys.Confirm.DuplicateBody),
            UiResources.Get(UiResourceKeys.Confirm.DuplicateTitle),
            System.Windows.MessageBoxButton.YesNo,
            System.Windows.MessageBoxImage.Question);
        return r == System.Windows.MessageBoxResult.Yes;
    }

    public bool ConfirmCreateMaterial(CreateMaterialConfirmModel model)
    {
        var body =
            $"规格号：{model.Spec}\n" +
            $"规格描述：{model.Description}\n" +
            $"名称：{model.Name}\n" +
            $"品牌：{model.Brand}";

        var r = System.Windows.MessageBox.Show(
            body,
            "确认创建主物料",
            System.Windows.MessageBoxButton.YesNo,
            System.Windows.MessageBoxImage.Question);
        return r == System.Windows.MessageBoxResult.Yes;
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
