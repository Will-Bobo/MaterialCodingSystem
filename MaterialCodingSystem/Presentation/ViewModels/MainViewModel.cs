using MaterialCodingSystem;
using MaterialCodingSystem.Application;
using MaterialCodingSystem.Application.Contracts;
using MaterialCodingSystem.Application.Interfaces;
using MaterialCodingSystem.Presentation.Scheduling;
using MaterialCodingSystem.Presentation.Services;
using MaterialCodingSystem.Presentation.UiSemantics;

namespace MaterialCodingSystem.Presentation.ViewModels;

public sealed class MainViewModel : ViewModelBase
{
    private readonly MaterialApplicationService _app;
    private readonly IUiRenderer _uiRenderer;
    private readonly IUiDispatcher _uiDispatcher;

    private int _selectedTabIndex;
    public int SelectedTabIndex { get => _selectedTabIndex; set => SetProperty(ref _selectedTabIndex, value); }

    public CreateMaterialViewModel CreateMaterial { get; }
    public CreateReplacementViewModel CreateReplacement { get; }
    public SearchViewModel Search { get; }
    public DeprecateViewModel Deprecate { get; }
    public ExportViewModel Export { get; }
    public BomAuditViewModel BomAudit { get; }
    public BomArchiveHistoryViewModel BomArchiveHistory { get; }

    public MainViewModel(
        MaterialApplicationService app,
        DatabaseBackupService backup,
        IDatabasePathProvider paths,
        IDebouncer debouncer,
        IUiRenderer uiRenderer,
        IUiDispatcher uiDispatcher,
        IExportPathPreferenceStore exportPathStore,
        IFileSaveDialog saveDialog,
        IFileDbSaveDialog dbSaveDialog,
        IFileOpenDialog openDialog,
        IUiDialogService dialogs,
        AnalyzeBomUseCase analyzeBom,
        ImportBomNewMaterialsUseCase importBomNew,
        CanArchiveBomUseCase canArchiveBom,
        ArchiveBomUseCase archiveBom,
        BomArchiveService bomArchive,
        GetBomArchiveListUseCase bomHistory,
        ConfigureBomArchiveRootPathUseCase configureBomArchiveRoot,
        IBomExcelOpenFileDialog bomOpenDialog)
    {
        _app = app;
        _uiRenderer = uiRenderer;
        _uiDispatcher = uiDispatcher;

        CreateMaterial = new CreateMaterialViewModel(
            app,
            debouncer,
            uiRenderer,
            uiDispatcher,
            NavigateToReplacementFromCandidateAsync,
            OpenAddCategoryDialog);

        CreateReplacement = new CreateReplacementViewModel(app, uiRenderer, uiDispatcher);
        Search = new SearchViewModel(app, this, uiRenderer, uiDispatcher);
        Deprecate = new DeprecateViewModel(app, uiRenderer, uiDispatcher);
        Export = new ExportViewModel(app, backup, paths, exportPathStore, saveDialog, dbSaveDialog, openDialog, dialogs, uiRenderer, uiDispatcher);
        BomAudit = new BomAuditViewModel(analyzeBom, importBomNew, canArchiveBom, archiveBom, bomHistory, configureBomArchiveRoot, bomOpenDialog, uiRenderer, uiDispatcher);
        BomArchiveHistory = new BomArchiveHistoryViewModel(bomHistory, uiRenderer, uiDispatcher);
    }

    private Task OpenAddCategoryDialog()
    {
        var dlg = new CategoryDialogWindow(_app, _uiRenderer, _uiDispatcher);
        if (global::System.Windows.Application.Current.MainWindow != null)
            dlg.Owner = global::System.Windows.Application.Current.MainWindow;
        dlg.ShowDialog();
        return Task.CompletedTask;
    }

    public async Task NavigateToReplacementFromExistingCodeAsync(string existingCode)
    {
        SelectedTabIndex = 1;
        await CreateReplacement.LoadFromCodeAsync(existingCode);
    }

    public async Task NavigateToReplacementFromDtoAsync(MaterialItemSummary dto)
    {
        SelectedTabIndex = 1;
        await CreateReplacement.LoadFromDtoAsync(dto);
    }

    public async Task NavigateToReplacementFromDtoAsync(MaterialItemSpecHit dto)
    {
        SelectedTabIndex = 1;
        await CreateReplacement.LoadFromDtoAsync(dto);
    }

    public async Task NavigateToReplacementFromCandidateAsync(MaterialItemSpecHit hit)
    {
        SelectedTabIndex = 1;
        await CreateReplacement.LoadFromDtoAsync(hit);
    }
}
