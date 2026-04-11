using System.Collections.ObjectModel;
using MaterialCodingSystem.Application;
using MaterialCodingSystem.Application.Contracts;
using MaterialCodingSystem.Presentation.Services;

namespace MaterialCodingSystem.Presentation.ViewModels;

public sealed class CreateReplacementViewModel : ViewModelBase
{
    private readonly MaterialApplicationService _app;
    private readonly IDialogService _dialogService;

    private string _existingItemCode = "";
    public string ExistingItemCode { get => _existingItemCode; set => SetProperty(ref _existingItemCode, value); }

    private int _groupId = 1;
    public int GroupId { get => _groupId; set => SetProperty(ref _groupId, value); }

    private string _groupInfo = "";
    public string GroupInfo { get => _groupInfo; set => SetProperty(ref _groupInfo, value); }

    private string _groupCodeDisplay = "";
    public string GroupCodeDisplay { get => _groupCodeDisplay; set => SetProperty(ref _groupCodeDisplay, value); }

    private string _existingSuffixDisplay = "";
    public string ExistingSuffixDisplay { get => _existingSuffixDisplay; set => SetProperty(ref _existingSuffixDisplay, value); }

    private string _nextSuffixDisplay = "";
    public string NextSuffixDisplay { get => _nextSuffixDisplay; set => SetProperty(ref _nextSuffixDisplay, value); }

    private string _embeddedCodeKeyword = "";
    public string EmbeddedCodeKeyword { get => _embeddedCodeKeyword; set => SetProperty(ref _embeddedCodeKeyword, value); }

    public ObservableCollection<MaterialItemSummary> EmbeddedCodeResults { get; } = new();

    private string _codeSearchStatus = "";
    public string CodeSearchStatus { get => _codeSearchStatus; set => SetProperty(ref _codeSearchStatus, value); }

    private bool _codeSearchLoading;
    public bool CodeSearchLoading { get => _codeSearchLoading; set => SetProperty(ref _codeSearchLoading, value); }

    private string _spec = "";
    public string Spec { get => _spec; set => SetProperty(ref _spec, value); }

    private string _description = "";
    public string Description { get => _description; set => SetProperty(ref _description, value); }

    private string _name = "";
    public string Name { get => _name; set => SetProperty(ref _name, value); }

    private string _brand = "";
    public string Brand { get => _brand; set => SetProperty(ref _brand, value); }

    private string _result = "";
    public string Result { get => _result; set => SetProperty(ref _result, value); }

    private string _specFieldError = "";
    public string SpecFieldError { get => _specFieldError; set => SetProperty(ref _specFieldError, value); }

    public RelayCommand CreateCommand { get; }
    public RelayCommand ResolveGroupCommand { get; }
    public RelayCommand LoadGroupInfoCommand { get; }
    public RelayCommand EmbeddedCodeSearchCommand { get; }
    public RelayCommand<MaterialItemSummary> PickEmbeddedCodeHitCommand { get; }

    public CreateReplacementViewModel(MaterialApplicationService app, IDialogService dialogService)
    {
        _app = app;
        _dialogService = dialogService;
        CreateCommand = new RelayCommand(async () => await CreateAsync());
        ResolveGroupCommand = new RelayCommand(async () => await ResolveGroupAndReportAsync());
        LoadGroupInfoCommand = new RelayCommand(async () => await LoadGroupInfoCoreAsync());
        EmbeddedCodeSearchCommand = new RelayCommand(async () => await EmbeddedSearchByCodeAsync());
        PickEmbeddedCodeHitCommand = new RelayCommand<MaterialItemSummary>(async hit =>
        {
            if (hit is null) return;
            ExistingItemCode = hit.Code;
            await ResolveGroupAndReportAsync();
        });
    }

    public async Task ResolveGroupAndReportAsync()
    {
        Result = "处理中...";
        var res = await _app.ResolveGroupIdByItemCode(ExistingItemCode);
        if (!res.IsSuccess)
        {
            Result = $"定位失败：{res.Error!.Code} - {res.Error.Message}";
            return;
        }

        GroupId = res.Data;
        Result = $"已定位：GroupId={GroupId}";
        await LoadGroupInfoCoreAsync();
    }

    public async Task LoadGroupInfoAsync()
    {
        await LoadGroupInfoCoreAsync();
    }

    private async Task LoadGroupInfoCoreAsync()
    {
        GroupInfo = "加载组信息中...";
        GroupCodeDisplay = "";
        ExistingSuffixDisplay = "";
        NextSuffixDisplay = "";
        var res = await _app.GetGroupInfo(GroupId);
        if (!res.IsSuccess)
        {
            GroupInfo = $"组信息失败：{res.Error!.Code} - {res.Error.Message}";
            return;
        }

        var d = res.Data!;
        GroupInfo = $"category={d.CategoryCode} serial={d.SerialNo} 已用suffix=[{d.ExistingSuffixes}] 下一个={d.NextSuffix}";
        GroupCodeDisplay = $"Group: {d.CategoryCode}{d.SerialNo:D7}";
        var suffixParts = d.ExistingSuffixes.OrderBy(c => c).Select(c => c.ToString()).ToArray();
        ExistingSuffixDisplay = suffixParts.Length == 0
            ? "已有替代料: （无）"
            : $"已有替代料: {string.Join(" / ", suffixParts)}";
        NextSuffixDisplay = $"将创建: {d.NextSuffix}";
    }

    private async Task EmbeddedSearchByCodeAsync()
    {
        CodeSearchStatus = "搜索中...";
        EmbeddedCodeResults.Clear();
        CodeSearchLoading = true;
        try
        {
            var res = await _app.SearchByCode(new SearchQuery(
                CodeKeyword: EmbeddedCodeKeyword,
                SpecKeyword: null,
                CategoryCode: null,
                IncludeDeprecated: false,
                Limit: 20,
                Offset: 0));

            if (!res.IsSuccess)
            {
                CodeSearchStatus = $"失败：{res.Error!.Code}";
                return;
            }

            foreach (var x in res.Data!.Items)
                EmbeddedCodeResults.Add(x);

            CodeSearchStatus = res.Data.Items.Count == 0 ? "未找到匹配的编码。" : $"共 {res.Data.Items.Count} 条。";
        }
        finally
        {
            CodeSearchLoading = false;
        }
    }

    private async Task CreateAsync()
    {
        SpecFieldError = "";
        Result = "处理中...";
        var res = await _app.CreateReplacement(new CreateReplacementRequest(
            GroupId: GroupId,
            Spec: Spec,
            Name: Name,
            Description: Description,
            Brand: string.IsNullOrWhiteSpace(Brand) ? null : Brand
        ));

        if (res.IsSuccess)
        {
            Result = $"创建成功：{res.Data!.Code}（suffix={res.Data.Suffix}）";
            await LoadGroupInfoCoreAsync();
            return;
        }

        if (res.Error!.Code == ErrorCodes.SPEC_DUPLICATE)
            SpecFieldError = "规格号重复。";
        else if (res.Error.Code == ErrorCodes.SUFFIX_OVERFLOW || res.Error.Code == ErrorCodes.SUFFIX_SEQUENCE_BROKEN)
            _dialogService.ShowWarning("替代料", res.Error.Message);
        else if (res.Error.Code == ErrorCodes.CODE_CONFLICT_RETRY)
            _dialogService.ShowWarning("提示", "系统繁忙，请稍后重试。");
        else
            Result = $"失败：{res.Error.Code} - {res.Error.Message}";
    }
}
