using System.Diagnostics;
using MaterialCodingSystem.Application.Contracts;

namespace MaterialCodingSystem.Presentation.UiSemantics;

public interface IUiRenderer
{
    /// <summary>AppError + context → single <see cref="UiRenderPlan"/>; does not touch ViewModels.</summary>
    UiRenderPlan BuildRenderPlan(AppError error, ContextType context);

    void LogTechnicalFailure(AppError error);

    bool ConfirmDuplicateCreate();

    bool ConfirmDeprecate(string code);
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

    public bool ConfirmDeprecate(string code)
    {
        var body = UiResources.Format(UiResourceKeys.Confirm.DeprecateBody, code);
        var r = System.Windows.MessageBox.Show(
            body,
            UiResources.Get(UiResourceKeys.Confirm.DeprecateTitle),
            System.Windows.MessageBoxButton.YesNo,
            System.Windows.MessageBoxImage.Warning);
        return r == System.Windows.MessageBoxResult.Yes;
    }
}
