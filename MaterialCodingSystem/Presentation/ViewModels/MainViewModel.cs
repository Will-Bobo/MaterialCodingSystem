using MaterialCodingSystem;
using MaterialCodingSystem.Application;
using MaterialCodingSystem.Application.Interfaces;
using MaterialCodingSystem.Presentation.Scheduling;
using MaterialCodingSystem.Presentation.Services;

namespace MaterialCodingSystem.Presentation.ViewModels;

public sealed class MainViewModel : ViewModelBase
{
    private readonly MaterialApplicationService _app;

    private int _selectedTabIndex;
    public int SelectedTabIndex { get => _selectedTabIndex; set => SetProperty(ref _selectedTabIndex, value); }

    public CreateMaterialViewModel CreateMaterial { get; }
    public CreateReplacementViewModel CreateReplacement { get; }
    public SearchViewModel Search { get; }
    public DeprecateViewModel Deprecate { get; }
    public ExportViewModel Export { get; }

    public MainViewModel(
        MaterialApplicationService app,
        IDebouncer debouncer,
        IDialogService dialogService,
        IExportPathPreferenceStore exportPathStore,
        IFileSaveDialog saveDialog)
    {
        _app = app;

        CreateMaterial = new CreateMaterialViewModel(
            app,
            debouncer,
            dialogService,
            NavigateToReplacementFromExistingCodeAsync,
            OpenAddCategoryDialog);

        CreateReplacement = new CreateReplacementViewModel(app, dialogService);
        Search = new SearchViewModel(app, this);
        Deprecate = new DeprecateViewModel(app);
        Export = new ExportViewModel(app, exportPathStore, saveDialog);
    }

    private Task OpenAddCategoryDialog()
    {
        var dlg = new CategoryDialogWindow(_app);
        if (global::System.Windows.Application.Current.MainWindow != null)
            dlg.Owner = global::System.Windows.Application.Current.MainWindow;
        dlg.ShowDialog();
        return Task.CompletedTask;
    }

    public async Task NavigateToReplacementFromExistingCodeAsync(string existingCode)
    {
        // 0=A, 1=替代料, 2=搜索/废弃, 3=导出
        SelectedTabIndex = 1;
        CreateReplacement.ExistingItemCode = existingCode;
        await CreateReplacement.ResolveGroupAndReportAsync();
    }
}
