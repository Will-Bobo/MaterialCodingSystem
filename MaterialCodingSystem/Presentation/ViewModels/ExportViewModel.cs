using System.Diagnostics;
using System.IO;
using MaterialCodingSystem.Application.Contracts;
using MaterialCodingSystem.Application;
using MaterialCodingSystem.Application.Interfaces;
using MaterialCodingSystem.Presentation.Services;
using MaterialCodingSystem.Presentation.UiSemantics;

namespace MaterialCodingSystem.Presentation.ViewModels;

public sealed class ExportViewModel : ViewModelBase
{
    private readonly MaterialApplicationService _app;
    private readonly DatabaseBackupService _backup;
    private readonly IDatabasePathProvider _paths;
    private readonly IExportPathPreferenceStore _pathStore;
    private readonly IFileSaveDialog _saveDialog;
    private readonly IFileDbSaveDialog _dbSaveDialog;
    private readonly IFileOpenDialog _openDialog;
    private readonly IUiDialogService _dialogs;
    private readonly IUiRenderer _uiRenderer;
    private readonly IUiDispatcher _uiDispatcher;

    private string _result = "";
    public string Result { get => _result; set => SetProperty(ref _result, value); }

    public RelayCommand ExportCommand { get; }
    public RelayCommand ExportDbCommand { get; }
    public RelayCommand RestoreDbCommand { get; }

    public ExportViewModel(
        MaterialApplicationService app,
        DatabaseBackupService backup,
        IDatabasePathProvider paths,
        IExportPathPreferenceStore pathStore,
        IFileSaveDialog saveDialog,
        IFileDbSaveDialog dbSaveDialog,
        IFileOpenDialog openDialog,
        IUiDialogService dialogs,
        IUiRenderer uiRenderer,
        IUiDispatcher uiDispatcher)
    {
        _app = app;
        _backup = backup;
        _paths = paths;
        _pathStore = pathStore;
        _saveDialog = saveDialog;
        _dbSaveDialog = dbSaveDialog;
        _openDialog = openDialog;
        _dialogs = dialogs;
        _uiRenderer = uiRenderer;
        _uiDispatcher = uiDispatcher;
        ExportCommand = new RelayCommand(async () => await ExportAsync());
        ExportDbCommand = new RelayCommand(async () => await ExportDbAsync());
        RestoreDbCommand = new RelayCommand(async () => await RestoreDbAsync());
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

    private async Task ExportDbAsync()
    {
        Result = "正在导出数据库…";

        var initial = _pathStore.GetLastExportDirectory();
        var dbTarget = _dbSaveDialog.ShowSaveDb(initial);
        if (string.IsNullOrWhiteSpace(dbTarget))
        {
            Result = "已取消数据库导出。";
            return;
        }

        if (File.Exists(dbTarget))
        {
            if (!_dialogs.Confirm("确认覆盖", "文件已存在，是否覆盖？"))
            {
                Result = "已取消数据库导出。";
                return;
            }
        }

        var dir = Path.GetDirectoryName(dbTarget);
        if (!string.IsNullOrWhiteSpace(dir))
            _pathStore.SetLastExportDirectory(dir);

        var res = await _backup.ExportDatabase(dbTarget);
        if (res.IsSuccess)
        {
            Result = $"数据库导出成功：{res.Data!.TargetPath}";
            return;
        }

        var plan = _uiRenderer.BuildRenderPlan(res.Error!, ContextType.Export);
        _uiDispatcher.Apply(plan, this);
    }

    private async Task RestoreDbAsync()
    {
        Result = "请选择要恢复的数据库备份文件（.db）…";

        var initial = _pathStore.GetLastExportDirectory();
        var selected = _openDialog.ShowOpenDb(initial);
        if (string.IsNullOrWhiteSpace(selected))
        {
            Result = "已取消数据库恢复。";
            return;
        }

        var dir = Path.GetDirectoryName(selected);
        if (!string.IsNullOrWhiteSpace(dir))
            _pathStore.SetLastExportDirectory(dir);

        var mainDb = Path.GetFullPath(_paths.GetMainDbPath());
        var nowDbTime = File.Exists(mainDb) ? File.GetLastWriteTime(mainDb) : (DateTime?)null;
        var backupTime = File.Exists(selected) ? File.GetLastWriteTime(selected) : (DateTime?)null;

        var body =
            "恢复将覆盖当前数据，成功后会自动重启应用。\n\n"
            + $"当前 DB 时间：{(nowDbTime?.ToString("yyyy-MM-dd HH:mm:ss") ?? "（不可读取）")}\n"
            + $"备份文件时间：{(backupTime?.ToString("yyyy-MM-dd HH:mm:ss") ?? "（不可读取）")}\n\n"
            + "是否继续？";

        if (!_dialogs.Confirm("确认恢复数据库", body))
        {
            Result = "已取消数据库恢复。";
            return;
        }

        Result = "正在恢复数据库…";
        var res = await _backup.RestoreDatabase(selected);
        if (!res.IsSuccess)
        {
            var plan = _uiRenderer.BuildRenderPlan(res.Error!, ContextType.Export);
            _uiDispatcher.Apply(plan, this);
            return;
        }

        Result = "恢复成功，正在重启应用…";

        // 启动新进程并退出当前进程（按要求使用 Environment.Exit）
        var exe = Environment.ProcessPath;
        if (!string.IsNullOrWhiteSpace(exe))
        {
            Process.Start(new ProcessStartInfo(exe) { UseShellExecute = true });
        }

        Environment.Exit(0);
    }
}
