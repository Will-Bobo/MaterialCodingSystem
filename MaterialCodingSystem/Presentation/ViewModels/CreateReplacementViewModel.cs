using System.Collections.ObjectModel;
using MaterialCodingSystem.Application;
using MaterialCodingSystem.Application.Contracts;
using MaterialCodingSystem.Presentation.UiSemantics;

namespace MaterialCodingSystem.Presentation.ViewModels;

public sealed class CreateReplacementViewModel : ViewModelBase
{
    private readonly MaterialApplicationService _app;
    private readonly IUiRenderer _uiRenderer;
    private readonly IUiDispatcher _uiDispatcher;

    private string _existingItemCode = "";
    public string ExistingItemCode { get => _existingItemCode; set => SetProperty(ref _existingItemCode, value); }

    private int _groupId;
    public int GroupId
    {
        get => _groupId;
        set
        {
            if (SetProperty(ref _groupId, value))
                CreateCommand.RaiseCanExecuteChanged();
        }
    }

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

    private string _replacementCreateHint = "";
    public string ReplacementCreateHint { get => _replacementCreateHint; set => SetProperty(ref _replacementCreateHint, value); }

    public RelayCommand CreateCommand { get; }
    public RelayCommand ResolveGroupCommand { get; }
    public RelayCommand LoadGroupInfoCommand { get; }
    public RelayCommand EmbeddedCodeSearchCommand { get; }
    public RelayCommand<MaterialItemSummary> PickEmbeddedCodeHitCommand { get; }

    public CreateReplacementViewModel(MaterialApplicationService app, IUiRenderer uiRenderer, IUiDispatcher uiDispatcher)
    {
        _app = app;
        _uiRenderer = uiRenderer;
        _uiDispatcher = uiDispatcher;
        CreateCommand = new RelayCommand(async () => await CreateAsync(), () => GroupId > 0);
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
        Result = UiResources.Get(UiResourceKeys.Info.ReplacementProcessing);
        var res = await _app.ResolveGroupIdByItemCode(ExistingItemCode);
        if (!res.IsSuccess)
        {
            var plan = _uiRenderer.BuildRenderPlan(res.Error!, ContextType.CreateReplacementResolveGroup);
            _uiDispatcher.Apply(plan, this);
            return;
        }

        GroupId = res.Data;
        Result = UiResources.Format(UiResourceKeys.Info.ReplacementResolvedGroup, GroupId);
        await LoadGroupInfoCoreAsync();
    }

    public async Task LoadGroupInfoAsync()
    {
        await LoadGroupInfoCoreAsync();
    }

    private async Task LoadGroupInfoCoreAsync()
    {
        GroupInfo = UiResources.Get(UiResourceKeys.Info.ReplacementLoadingGroupInfo);
        GroupCodeDisplay = "";
        ExistingSuffixDisplay = "";
        NextSuffixDisplay = "";
        ReplacementCreateHint = "";
        var res = await _app.GetGroupInfo(GroupId);
        if (!res.IsSuccess)
        {
            var plan = _uiRenderer.BuildRenderPlan(res.Error!, ContextType.CreateReplacementGroupInfo);
            _uiDispatcher.Apply(plan, this);
            return;
        }

        var d = res.Data!;
        GroupInfo = UiResources.Format(
            UiResourceKeys.Info.ReplacementGroupInfoSummary,
            d.CategoryCode,
            d.SerialNo,
            d.ExistingSuffixes,
            d.NextSuffix);
        GroupCodeDisplay = UiResources.Format(UiResourceKeys.Info.ReplacementGroupCodeDisplay, d.CategoryCode, d.SerialNo);
        var suffixParts = d.ExistingSuffixes.OrderBy(c => c).Select(c => c.ToString()).ToArray();
        ExistingSuffixDisplay = suffixParts.Length == 0
            ? UiResources.Get(UiResourceKeys.Info.ReplacementExistingSuffixNone)
            : UiResources.Format(UiResourceKeys.Info.ReplacementExistingSuffixList, string.Join(" / ", suffixParts));
        NextSuffixDisplay = UiResources.Format(UiResourceKeys.Info.ReplacementNextSuffixDisplay, d.NextSuffix);
        ReplacementCreateHint = UiResources.Format(UiResourceKeys.Info.ReplacementCreateHint, d.NextSuffix);
    }

    private async Task EmbeddedSearchByCodeAsync()
    {
        CodeSearchStatus = UiResources.Get(UiResourceKeys.Info.ReplacementEmbeddedSearchSearching);
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
                var plan = _uiRenderer.BuildRenderPlan(res.Error!, ContextType.CreateReplacementEmbeddedSearch);
                _uiDispatcher.Apply(plan, this);
                return;
            }

            foreach (var x in res.Data!.Items)
                EmbeddedCodeResults.Add(x);

            CodeSearchStatus = res.Data.Items.Count == 0
                ? UiResources.Get(UiResourceKeys.Info.ReplacementEmbeddedSearchEmpty)
                : UiResources.Format(UiResourceKeys.Info.ReplacementEmbeddedSearchCount, res.Data.Items.Count);
        }
        finally
        {
            CodeSearchLoading = false;
        }
    }

    private async Task CreateAsync()
    {
        SpecFieldError = "";
        Result = UiResources.Get(UiResourceKeys.Info.ReplacementProcessing);
        var res = await _app.CreateReplacement(new CreateReplacementRequest(
            GroupId: GroupId,
            Spec: Spec,
            Name: Name,
            Description: Description,
            Brand: string.IsNullOrWhiteSpace(Brand) ? null : Brand
        ));

        if (res.IsSuccess)
        {
            Result = UiResources.Format(UiResourceKeys.Info.ReplacementCreateSuccess, res.Data!.Code, res.Data.Suffix);
            await LoadGroupInfoCoreAsync();
            return;
        }

        var plan = _uiRenderer.BuildRenderPlan(res.Error!, ContextType.CreateReplacementCreate);
        _uiDispatcher.Apply(plan, this);
    }
}
