using System.IO;
using MaterialCodingSystem.Application;
using MaterialCodingSystem.Application.Interfaces;
using MaterialCodingSystem.Presentation.Services;

namespace MaterialCodingSystem.Presentation.ViewModels;

public sealed class ExportViewModel : ViewModelBase
{
    private readonly MaterialApplicationService _app;
    private readonly IExportPathPreferenceStore _pathStore;
    private readonly IFileSaveDialog _saveDialog;

    private string _result = "";
    public string Result { get => _result; set => SetProperty(ref _result, value); }

    public RelayCommand ExportCommand { get; }

    public ExportViewModel(
        MaterialApplicationService app,
        IExportPathPreferenceStore pathStore,
        IFileSaveDialog saveDialog)
    {
        _app = app;
        _pathStore = pathStore;
        _saveDialog = saveDialog;
        ExportCommand = new RelayCommand(async () => await ExportAsync());
    }

    private async Task ExportAsync()
    {
        Result = "处理中...";
        var initial = _pathStore.GetLastExportDirectory();
        var path = _saveDialog.ShowSaveXlsx(initial);
        if (string.IsNullOrWhiteSpace(path))
        {
            Result = "已取消。";
            return;
        }

        var dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(dir))
            _pathStore.SetLastExportDirectory(dir);

        var res = await _app.ExportActiveMaterials(path);
        Result = res.IsSuccess
            ? $"导出成功：{path}（{res.Data!.RowCount} 行，{res.Data.SheetCount} 个分类 Sheet）"
            : $"失败：{res.Error!.Code} - {res.Error.Message}";
    }
}
