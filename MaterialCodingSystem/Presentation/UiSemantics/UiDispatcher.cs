using System.Windows;
using System.Windows.Threading;
using MaterialCodingSystem.Presentation.ViewModels;

namespace MaterialCodingSystem.Presentation.UiSemantics;

public interface IUiDispatcher
{
    void Apply(UiRenderPlan plan, object host);
}

public sealed class WpfUiDispatcher : IUiDispatcher
{
    private DispatcherTimer? _toastTimer;
    private object? _toastHost;
    private string? _toastProperty;

    public void Apply(UiRenderPlan plan, object host)
    {
        ArgumentNullException.ThrowIfNull(plan);
        ArgumentNullException.ThrowIfNull(host);

        ApplyClearStrategy(host, plan.ClearSlots);

        if (plan.Presentation == UiPresentation.Dialog)
        {
            if (plan.Modal is { } m)
                ShowModal(m);
            return;
        }

        var text = ResolveText(plan.TextResourceKey);
        foreach (var key in plan.TargetBindings)
            SetBound(host, key, text);

        if (plan.Presentation == UiPresentation.Toast && plan.TargetBindings.Count > 0)
            ScheduleToastClear(host, plan.TargetBindings[0]);
    }

    private static string ResolveText(string resourceKey) => UiResources.Get(resourceKey);

    private static void ShowModal(UiModalPlan m)
    {
        var body = ResolveText(m.BodyResourceKey);
        var title = ResolveText(m.TitleResourceKey);
        MessageBox.Show(body, title, MessageBoxButton.OK, MapIcon(m.Severity));
    }

    private static MessageBoxImage MapIcon(UiSeverity s) =>
        s switch
        {
            UiSeverity.Error => MessageBoxImage.Error,
            UiSeverity.Warning => MessageBoxImage.Warning,
            _ => MessageBoxImage.Information
        };

    private static void ApplyClearStrategy(object host, UiClearStrategy s)
    {
        if (s == UiClearStrategy.None)
            return;

        switch (host)
        {
            case CreateMaterialViewModel cm:
                ClearCreateMaterial(cm, s);
                break;
            case CreateReplacementViewModel cr:
                ClearCreateReplacement(cr, s);
                break;
            case SearchViewModel sv:
                ClearSearch(sv, s);
                break;
            case CategoryDialogViewModel cd:
                ClearCategoryDialog(cd, s);
                break;
            case DeprecateViewModel dv:
                ClearDeprecate(dv, s);
                break;
            case ExportViewModel ev:
                ClearExport(ev, s);
                break;
        }
    }

    private static void ClearCreateMaterial(CreateMaterialViewModel cm, UiClearStrategy s)
    {
        switch (s)
        {
            case UiClearStrategy.CreateMaterialSubmit:
                cm.SpecFieldError = "";
                cm.GlobalError = "";
                cm.Result = "";
                break;
            case UiClearStrategy.CreateMaterialResultOnly:
                cm.Result = "";
                break;
            case UiClearStrategy.CreateMaterialCandidateStatus:
                cm.CandidateStatus = "";
                break;
        }
    }

    private static void ClearCreateReplacement(CreateReplacementViewModel cr, UiClearStrategy s)
    {
        switch (s)
        {
            case UiClearStrategy.CreateReplacementSubmit:
                cr.SpecFieldError = "";
                cr.Result = "";
                break;
            case UiClearStrategy.CreateReplacementResultOnly:
                cr.Result = "";
                break;
            case UiClearStrategy.CreateReplacementGroupInfo:
                cr.GroupInfo = "";
                break;
            case UiClearStrategy.CreateReplacementEmbeddedStatus:
                cr.CodeSearchStatus = "";
                break;
        }
    }

    private static void ClearSearch(SearchViewModel sv, UiClearStrategy s)
    {
        if (s == UiClearStrategy.SearchMessage)
            sv.Message = "";
    }

    private static void ClearCategoryDialog(CategoryDialogViewModel cd, UiClearStrategy s)
    {
        if (s == UiClearStrategy.CategoryDialogError)
            cd.Error = "";
    }

    private static void ClearDeprecate(DeprecateViewModel dv, UiClearStrategy s)
    {
        if (s == UiClearStrategy.DeprecateResult)
            dv.Result = "";
    }

    private static void ClearExport(ExportViewModel ev, UiClearStrategy s)
    {
        if (s == UiClearStrategy.ExportResult)
            ev.Result = "";
    }

    private static void SetBound(object host, string field, string value)
    {
        switch (host)
        {
            case CreateMaterialViewModel cm:
                SetCreateMaterial(cm, field, value);
                break;
            case CreateReplacementViewModel cr:
                SetCreateReplacement(cr, field, value);
                break;
            case SearchViewModel sv:
                if (field == UiBindings.Message)
                    sv.Message = value;
                break;
            case CategoryDialogViewModel cd:
                if (field == UiBindings.Error)
                    cd.Error = value;
                break;
            case DeprecateViewModel dv:
                if (field == UiBindings.Result)
                    dv.Result = value;
                break;
            case ExportViewModel ev:
                if (field == UiBindings.Result)
                    ev.Result = value;
                break;
        }
    }

    private static void SetCreateMaterial(CreateMaterialViewModel vm, string propertyName, string text)
    {
        switch (propertyName)
        {
            case UiBindings.SpecFieldError:
                vm.SpecFieldError = text;
                break;
            case UiBindings.GlobalError:
                vm.GlobalError = text;
                break;
            case UiBindings.Result:
                vm.Result = text;
                break;
            case UiBindings.CandidateStatus:
                vm.CandidateStatus = text;
                break;
        }
    }

    private static void SetCreateReplacement(CreateReplacementViewModel vm, string propertyName, string text)
    {
        switch (propertyName)
        {
            case UiBindings.SpecFieldError:
                vm.SpecFieldError = text;
                break;
            case UiBindings.Result:
                vm.Result = text;
                break;
            case UiBindings.CodeSearchStatus:
                vm.CodeSearchStatus = text;
                break;
            case UiBindings.GroupInfo:
                vm.GroupInfo = text;
                break;
        }
    }

    private void ScheduleToastClear(object host, string propertyName)
    {
        _toastTimer?.Stop();
        _toastHost = host;
        _toastProperty = propertyName;
        _toastTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(4) };
        _toastTimer.Tick += (_, _) =>
        {
            _toastTimer!.Stop();
            if (_toastHost is not null && _toastProperty is not null)
                SetBound(_toastHost, _toastProperty, "");
            _toastHost = null;
            _toastProperty = null;
        };
        _toastTimer.Start();
    }
}
