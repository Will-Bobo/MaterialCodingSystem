using System.IO;
using MaterialCodingSystem.Application;
using MaterialCodingSystem.Application.Interfaces;
using MaterialCodingSystem.Presentation.Services;
using MaterialCodingSystem.Presentation.UiSemantics;

namespace MaterialCodingSystem.Presentation.ViewModels;

public sealed class ExportViewModel : ViewModelBase
{
    private readonly MaterialApplicationService _app;
    private readonly IExportPathPreferenceStore _pathStore;
    private readonly IFileSaveDialog _saveDialog;
    private readonly IUiRenderer _uiRenderer;
    private readonly IUiDispatcher _uiDispatcher;

    private string _result = "";
    public string Result { get => _result; set => SetProperty(ref _result, value); }

    public RelayCommand ExportCommand { get; }

    public ExportViewModel(
        MaterialApplicationService app,
        IExportPathPreferenceStore pathStore,
        IFileSaveDialog saveDialog,
        IUiRenderer uiRenderer,
        IUiDispatcher uiDispatcher)
    {
        _app = app;
        _pathStore = pathStore;
        _saveDialog = saveDialog;
        _uiRenderer = uiRenderer;
        _uiDispatcher = uiDispatcher;
        ExportCommand = new RelayCommand(async () => await ExportAsync());
    }

    private async Task ExportAsync()
    {
        Result = UiResources.Get(UiResourceKeys.Info.ExportProcessing);
        var initial = _pathStore.GetLastExportDirectory();
        var path = _saveDialog.ShowSaveXlsx(initial);
        if (string.IsNullOrWhiteSpace(path))
        {
            Result = UiResources.Get(UiResourceKeys.Info.ExportCancelled);
            return;
        }

        var dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(dir))
            _pathStore.SetLastExportDirectory(dir);

        var res = await _app.ExportActiveMaterials(path);
        if (res.IsSuccess)
        {
            Result = UiResources.Format(
                UiResourceKeys.Info.ExportSuccess,
                path,
                res.Data!.RowCount,
                res.Data.SheetCount);
        }
        else
        {
            var plan = _uiRenderer.BuildRenderPlan(res.Error!, ContextType.Export);
            _uiDispatcher.Apply(plan, this);
        }
    }
}
