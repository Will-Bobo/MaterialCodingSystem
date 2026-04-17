using System.Collections.ObjectModel;
using System.Diagnostics;
using MaterialCodingSystem.Application;
using MaterialCodingSystem.Application.Interfaces;
using MaterialCodingSystem.Presentation.UiSemantics;

namespace MaterialCodingSystem.Presentation.ViewModels;

public sealed class BomArchiveHistoryItemViewModel : ViewModelBase
{
    public string FinishedCode { get; }
    public string Version { get; }
    public string FilePath { get; }
    public string CreatedAt { get; }

    public BomArchiveHistoryItemViewModel(BomArchiveRecord r)
    {
        FinishedCode = r.FinishedCode;
        Version = r.Version;
        FilePath = r.FilePath;
        CreatedAt = r.CreatedAt;
    }
}

public sealed class BomArchiveHistoryViewModel : ViewModelBase
{
    private readonly GetBomArchiveListUseCase _useCase;
    private readonly IUiRenderer _ui;
    private readonly IUiDispatcher _dispatcher;

    private string _finishedCodeFilter = "";
    public string FinishedCodeFilter { get => _finishedCodeFilter; set => SetProperty(ref _finishedCodeFilter, value); }

    private string _message = "";
    public string Message { get => _message; set => SetProperty(ref _message, value); }

    public ObservableCollection<BomArchiveHistoryItemViewModel> Items { get; } = new();

    public RelayCommand RefreshCommand { get; }
    public RelayCommand<BomArchiveHistoryItemViewModel> OpenPathCommand { get; }

    public BomArchiveHistoryViewModel(GetBomArchiveListUseCase useCase, IUiRenderer ui, IUiDispatcher dispatcher)
    {
        _useCase = useCase;
        _ui = ui;
        _dispatcher = dispatcher;

        RefreshCommand = new RelayCommand(async () => await RefreshAsync());
        OpenPathCommand = new RelayCommand<BomArchiveHistoryItemViewModel>(OpenPath, x => x is not null);
    }

    public async Task RefreshAsync()
    {
        var res = await _useCase.ExecuteAsync(new GetBomArchiveListRequest(string.IsNullOrWhiteSpace(FinishedCodeFilter) ? null : FinishedCodeFilter.Trim()));
        if (!res.IsSuccess || res.Data is null)
        {
            Message = res.Error?.Message ?? "load failed.";
            return;
        }

        Items.Clear();
        foreach (var i in res.Data.Items)
            Items.Add(new BomArchiveHistoryItemViewModel(i));

        Message = $"共 {Items.Count} 条归档记录。";
    }

    private void OpenPath(BomArchiveHistoryItemViewModel? item)
    {
        if (item is null) return;
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = item.FilePath,
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            Message = "打开失败：" + ex.Message;
        }
    }
}

