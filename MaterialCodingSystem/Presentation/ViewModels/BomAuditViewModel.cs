using System.Collections.ObjectModel;
using MaterialCodingSystem.Application;
using MaterialCodingSystem.Application.Contracts;
using MaterialCodingSystem.Presentation.Services;
using MaterialCodingSystem.Presentation.UiSemantics;

namespace MaterialCodingSystem.Presentation.ViewModels;

public sealed class BomAuditRowViewModel : ViewModelBase
{
    public int ExcelRowNo { get; }
    public string Status { get; }
    public string Code { get; }
    public string Name { get; }
    public string Spec { get; }
    public string Description { get; }
    public string Brand { get; }
    public string ErrorReason { get; }
    public BomAuditStatus StatusRaw { get; }
    public bool ShowImportButton { get; }

    public BomAuditRowViewModel(BomAuditRowDto dto)
    {
        ExcelRowNo = dto.ExcelRowNo;
        Status = dto.Status.ToString();
        StatusRaw = dto.Status;
        Code = dto.Code;
        Name = dto.Name;
        Spec = dto.Spec;
        Description = dto.Description;
        Brand = dto.Brand;
        ErrorReason = dto.ErrorReason ?? "";
        // UI 规则：NEW 行显示导入按钮；若导入失败原因是“分类不存在…”，仍允许继续点击（用户新增分类后可重试）
        ShowImportButton = StatusRaw == BomAuditStatus.NEW
                           || (StatusRaw == BomAuditStatus.ERROR
                               && string.Equals(ErrorReason, "分类不存在，请新增物料分类后再尝试", StringComparison.Ordinal));
    }
}

public sealed class BomAuditViewModel : ViewModelBase
{
    private readonly AnalyzeBomUseCase _analyze;
    private readonly ImportBomNewMaterialsUseCase _importNew;
    private readonly CanArchiveBomUseCase _canArchiveUseCase;
    private readonly ArchiveBomUseCase _archive;
    private readonly GetBomArchiveListUseCase _history;
    private readonly IBomExcelOpenFileDialog _openDialog;
    private readonly IUiRenderer _ui;
    private readonly IUiDispatcher _dispatcher;

    private string? _filePath;
    public string? FilePath { get => _filePath; set => SetProperty(ref _filePath, value); }

    private string _finishedCode = "";
    public string FinishedCode { get => _finishedCode; set => SetProperty(ref _finishedCode, value); }

    private string _version = "";
    public string Version { get => _version; set => SetProperty(ref _version, value); }

    private int _total;
    public int Total { get => _total; set => SetProperty(ref _total, value); }
    private int _pass;
    public int Pass { get => _pass; set => SetProperty(ref _pass, value); }
    private int _new;
    public int New { get => _new; set => SetProperty(ref _new, value); }
    private int _error;
    public int Error { get => _error; set => SetProperty(ref _error, value); }
    private int _missingCodeError;
    public int MissingCodeError { get => _missingCodeError; set => SetProperty(ref _missingCodeError, value); }

    private bool _canArchiveAllowed;
    public bool CanArchive { get => _canArchiveAllowed; set => SetProperty(ref _canArchiveAllowed, value); }

    private string _cannotArchiveReason = "";
    public string CannotArchiveReason { get => _cannotArchiveReason; set => SetProperty(ref _cannotArchiveReason, value); }

    private string _message = "";
    public string Message { get => _message; set => SetProperty(ref _message, value); }

    private string _importSummary = "";
    public string ImportSummary { get => _importSummary; set => SetProperty(ref _importSummary, value); }

    public ObservableCollection<BomAuditRowViewModel> Rows { get; } = new();

    public RelayCommand PickFileCommand { get; }
    public RelayCommand AnalyzeCommand { get; }
    public RelayCommand ImportAllNewCommand { get; }
    public RelayCommand ArchiveCommand { get; }

    public RelayCommand<BomAuditRowViewModel> ImportRowCommand { get; }

    public BomAuditViewModel(
        AnalyzeBomUseCase analyze,
        ImportBomNewMaterialsUseCase importNew,
        CanArchiveBomUseCase canArchive,
        ArchiveBomUseCase archive,
        GetBomArchiveListUseCase history,
        IBomExcelOpenFileDialog openDialog,
        IUiRenderer ui,
        IUiDispatcher dispatcher)
    {
        _analyze = analyze;
        _importNew = importNew;
        _canArchiveUseCase = canArchive;
        _archive = archive;
        _history = history;
        _openDialog = openDialog;
        _ui = ui;
        _dispatcher = dispatcher;

        PickFileCommand = new RelayCommand(async () => await PickFileAsync());
        AnalyzeCommand = new RelayCommand(async () => await AnalyzeAsync(), () => !string.IsNullOrWhiteSpace(FilePath));
        ImportAllNewCommand = new RelayCommand(async () => await ImportAllNewAsync(), () => !string.IsNullOrWhiteSpace(FilePath));
        ArchiveCommand = new RelayCommand(async () => await ArchiveAsync(), () => !string.IsNullOrWhiteSpace(FilePath));
        ImportRowCommand = new RelayCommand<BomAuditRowViewModel>(async r => await ImportRowAsync(r), r => r is not null);
    }

    private void ClearAuditResultState()
    {
        FinishedCode = "";
        Version = "";
        Total = 0;
        Pass = 0;
        New = 0;
        Error = 0;
        MissingCodeError = 0;
        Rows.Clear();
        ImportSummary = "";
        CanArchive = false;
        CannotArchiveReason = "";
    }

    private Task PickFileAsync()
    {
        var path = _openDialog.ShowOpenBomExcel(null);
        if (!string.IsNullOrWhiteSpace(path))
        {
            FilePath = path;
            // 切换文件后必须清空上一轮结果，避免“报错但仍显示旧 BOM 数据”的误解
            ClearAuditResultState();
            Message = "";
            AnalyzeCommand.RaiseCanExecuteChanged();
            ImportAllNewCommand.RaiseCanExecuteChanged();
            ArchiveCommand.RaiseCanExecuteChanged();
        }
        return Task.CompletedTask;
    }

    private async Task AnalyzeAsync()
    {
        try
        {
            if (string.IsNullOrWhiteSpace(FilePath)) return;
            var res = await _analyze.ExecuteAsync(new AnalyzeBomRequest(FilePath));
            if (!res.IsSuccess || res.Data is null)
            {
                Message = $"{res.Error?.Code} {res.Error?.Message}".Trim();
                // 解析失败：清空主数据区，避免残留上一轮解析结果
                ClearAuditResultState();
                return;
            }

            Apply(res.Data);
        }
        catch (Exception ex)
        {
            Message = "分析失败：" + ex.Message;
            ClearAuditResultState();
        }
    }

    private async Task ImportAllNewAsync()
    {
        try
        {
            if (string.IsNullOrWhiteSpace(FilePath)) return;
            if (!ConfirmBatchImport()) return;
            var res = await _importNew.ExecuteAsync(new ImportBomNewMaterialsRequest(FilePath));
            if (!res.IsSuccess || res.Data is null)
            {
                Message = $"{res.Error?.Code} {res.Error?.Message}".Trim();
                return;
            }

            ImportSummary = BuildImportSummary(res.Data);
            Apply(res.Data.AnalyzeResult);
        }
        catch (Exception ex)
        {
            Message = "导入失败：" + ex.Message;
        }
    }

    private async Task ArchiveAsync()
    {
        try
        {
            if (string.IsNullOrWhiteSpace(FilePath)) return;
            var res = await _archive.ExecuteAsync(new ArchiveBomRequest(FilePath));
            Message = res.IsSuccess ? $"已归档：{res.Data?.FilePath}" : $"{res.Error?.Code} {res.Error?.Message}".Trim();
        }
        catch (Exception ex)
        {
            Message = "归档失败：" + ex.Message;
        }
    }

    private bool ConfirmBatchImport()
    {
        var count = New;
        if (count <= 0) return true;
        var title = "确认批量导入新物料";
        var body = $"即将批量导入新物料：{count} 条。\n\n是否继续？";
        return _ui.Confirm(title, body);
    }

    private async Task ImportRowAsync(BomAuditRowViewModel? row)
    {
        try
        {
            if (row is null) return;
            if (string.IsNullOrWhiteSpace(FilePath)) return;

            if (string.IsNullOrWhiteSpace(row.Code) || row.Code.Trim().Length < 3)
                return;

            var model = new MaterialCodingSystem.Presentation.Models.CreateMaterialConfirmModel
            {
                Code = row.Code,
                Spec = row.Spec,
                Description = row.Description,
                Name = row.Name,
                Brand = row.Brand
            };
            if (!_ui.ConfirmImportMaterial(model))
                return;

            var res = await _importNew.ExecuteAsync(new ImportBomNewMaterialsRequest(FilePath, ExcelRowNo: row.ExcelRowNo));
            if (!res.IsSuccess || res.Data is null)
            {
                Message = $"{res.Error?.Code} {res.Error?.Message}".Trim();
                return;
            }

            ImportSummary = BuildImportSummary(res.Data);
            Apply(res.Data.AnalyzeResult);
        }
        catch (Exception ex)
        {
            Message = "导入失败：" + ex.Message;
        }
    }

    private void Apply(AnalyzeBomResponse data)
    {
        FinishedCode = data.FinishedCode;
        Version = data.Version;
        Total = data.TotalCount;
        Pass = data.PassCount;
        New = data.NewCount;
        Error = data.ErrorCount;
        MissingCodeError = data.MissingCodeErrorCount;

        Rows.Clear();
        foreach (var r in data.Rows)
            Rows.Add(new BomAuditRowViewModel(r));

        if (data.FirstErrorRowNo is not null)
            Message = $"首个异常行：第{data.FirstErrorRowNo}行";
        else
            Message = "审核完成。";

        _ = RefreshCanArchiveAsync();
    }

    private async Task RefreshCanArchiveAsync()
    {
        if (string.IsNullOrWhiteSpace(FilePath))
        {
            CanArchive = false;
            CannotArchiveReason = "";
            return;
        }

        var res = await _canArchiveUseCase.ExecuteAsync(new CanArchiveBomRequest(FilePath));
        if (!res.IsSuccess || res.Data is null)
        {
            CanArchive = false;
            CannotArchiveReason = res.Error?.Message ?? "";
            return;
        }

        CanArchive = res.Data.IsAllowed;
        CannotArchiveReason = res.Data.Reason;
    }

    private static string BuildImportSummary(ImportBomNewMaterialsResponse res)
    {
        if (res.FailureCount == 0 && res.SuccessCount == 0) return "";
        if (res.FailureCount == 0) return $"导入完成：成功 {res.SuccessCount}，失败 0。";
        var top = string.Join("；", res.TopFailureReasons.Select(x => $"{x.Reason}×{x.Count}"));
        return $"导入完成：成功 {res.SuccessCount}，失败 {res.FailureCount}。失败原因：{top}";
    }
}

