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

    // Internal only: selected base material code (not user-editable on page)
    private string _baseMaterialCode = "";

    // Internal only (Presentation must not expose groupId)
    private int _resolvedGroupId;

    private string _categoryDisplayName = "";
    public string CategoryDisplayName { get => _categoryDisplayName; set => SetProperty(ref _categoryDisplayName, value); }

    private string _anchorSpec = "";
    public string AnchorSpec { get => _anchorSpec; private set => SetProperty(ref _anchorSpec, value); }

    private string _anchorDescription = "";
    public string AnchorDescription { get => _anchorDescription; private set => SetProperty(ref _anchorDescription, value); }

    private string _anchorBrand = "";
    public string AnchorBrand { get => _anchorBrand; private set => SetProperty(ref _anchorBrand, value); }

    private string _masterCodeDisplay = "";
    public string MasterCodeDisplay { get => _masterCodeDisplay; set => SetProperty(ref _masterCodeDisplay, value); }

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

    private string _anchorLoadState = "未选择";
    public string AnchorLoadState { get => _anchorLoadState; set => SetProperty(ref _anchorLoadState, value); }

    private bool _isSubmitting;
    public bool IsSubmitting { get => _isSubmitting; set => SetProperty(ref _isSubmitting, value); }

    private string _spec = "";
    public string Spec { get => _spec; set => SetProperty(ref _spec, value); }

    private string _description = "";
    public string Description { get => _description; set => SetProperty(ref _description, value); }

    private string _brand = "";
    public string Brand { get => _brand; set => SetProperty(ref _brand, value); }

    private string _result = "";
    public string Result { get => _result; set => SetProperty(ref _result, value); }

    private string _specFieldError = "";
    public string SpecFieldError { get => _specFieldError; set => SetProperty(ref _specFieldError, value); }

    private string _replacementCreateHint = "";
    public string ReplacementCreateHint { get => _replacementCreateHint; set => SetProperty(ref _replacementCreateHint, value); }

    public RelayCommand CreateCommand { get; }
    public RelayCommand EmbeddedCodeSearchCommand { get; }
    public RelayCommand<MaterialItemSummary> PickEmbeddedCodeHitCommand { get; }

    public CreateReplacementViewModel(MaterialApplicationService app, IUiRenderer uiRenderer, IUiDispatcher uiDispatcher)
    {
        _app = app;
        _uiRenderer = uiRenderer;
        _uiDispatcher = uiDispatcher;
        CreateCommand = new RelayCommand(async () => await CreateAsync(), () => _resolvedGroupId > 0 && !IsSubmitting);
        EmbeddedCodeSearchCommand = new RelayCommand(async () => await EmbeddedSearchByCodeAsync());
        PickEmbeddedCodeHitCommand = new RelayCommand<MaterialItemSummary>(async hit =>
        {
            if (hit is null) return;
            await LoadFromDtoAsync(hit);
        });
    }

    public async Task LoadFromDtoAsync(MaterialItemSummary dto)
    {
        await LoadAnchorFromSummaryAsync(dto);
        await LoadFromCodeAsync(dto.Code, clearAnchorDetails: false);
    }

    public async Task LoadFromDtoAsync(MaterialItemSpecHit dto)
    {
        await LoadAnchorFromSpecHitAsync(dto);
        await LoadFromCodeAsync(dto.Code, clearAnchorDetails: false);
    }

    private Task LoadAnchorFromSummaryAsync(MaterialItemSummary dto)
    {
        AnchorSpec = dto.Spec;
        AnchorDescription = dto.Description;
        AnchorBrand = dto.Brand ?? "";
        CategoryDisplayName = dto.Name;
        MasterCodeDisplay = dto.Code;
        return Task.CompletedTask;
    }

    private Task LoadAnchorFromSpecHitAsync(MaterialItemSpecHit dto)
    {
        AnchorSpec = dto.Spec;
        AnchorDescription = dto.Description;
        AnchorBrand = dto.Brand ?? "";
        CategoryDisplayName = dto.Name;
        MasterCodeDisplay = dto.Code;
        return Task.CompletedTask;
    }

    public async Task LoadFromCodeAsync(string code, bool clearAnchorDetails = true)
    {
        Result = UiResources.Get(UiResourceKeys.Info.ReplacementProcessing);
        AnchorLoadState = "加载中";
        _resolvedGroupId = 0;
        _baseMaterialCode = code?.Trim() ?? "";
        if (clearAnchorDetails)
        {
            AnchorSpec = "";
            AnchorDescription = "";
            AnchorBrand = "";
        }
        CreateCommand.RaiseCanExecuteChanged();

        var res = await _app.ResolveGroupIdByItemCode(_baseMaterialCode);
        if (!res.IsSuccess)
        {
            AnchorLoadState = "失败";
            var plan = _uiRenderer.BuildRenderPlan(res.Error!, ContextType.CreateReplacementResolveGroup);
            _uiDispatcher.Apply(plan, this);
            return;
        }

        _resolvedGroupId = res.Data;
        await LoadAnchorInfoCoreAsync();
    }

    private async Task LoadAnchorInfoCoreAsync()
    {
        // 不覆盖 Anchor 注入的字段（仅补充 suffix 视图信息）
        ExistingSuffixDisplay = "";
        NextSuffixDisplay = "";
        ReplacementCreateHint = "";
        var res = await _app.GetGroupInfo(_resolvedGroupId);
        if (!res.IsSuccess)
        {
            AnchorLoadState = "失败";
            var plan = _uiRenderer.BuildRenderPlan(res.Error!, ContextType.CreateReplacementGroupInfo);
            _uiDispatcher.Apply(plan, this);
            return;
        }

        var d = res.Data!;
        // 仍然允许用 group 信息刷新展示名/主档编码（与冻结口径一致）
        CategoryDisplayName = d.CategoryName;
        MasterCodeDisplay = UiResources.Format(UiResourceKeys.Info.ReplacementGroupCodeDisplay, d.CategoryCode, d.SerialNo);
        var suffixParts = d.ExistingSuffixes.OrderBy(c => c).Select(c => c.ToString()).ToArray();
        ExistingSuffixDisplay = suffixParts.Length == 0
            ? UiResources.Get(UiResourceKeys.Info.ReplacementExistingSuffixNone)
            : UiResources.Format(UiResourceKeys.Info.ReplacementExistingSuffixList, string.Join(" / ", suffixParts));
        NextSuffixDisplay = UiResources.Format(UiResourceKeys.Info.ReplacementNextSuffixDisplay, d.NextSuffix);
        ReplacementCreateHint = UiResources.Format(UiResourceKeys.Info.ReplacementCreateHint, d.NextSuffix);
        AnchorLoadState = "成功";
        CreateCommand.RaiseCanExecuteChanged();
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
        IsSubmitting = true;
        CreateCommand.RaiseCanExecuteChanged();
        try
        {
            var res = await _app.CreateReplacementByCode(new CreateReplacementByCodeRequest(
                BaseMaterialCode: _baseMaterialCode,
                Spec: Spec,
                Description: Description,
                Brand: string.IsNullOrWhiteSpace(Brand) ? null : Brand
            ));

            if (res.IsSuccess)
            {
                Result = UiResources.Format(UiResourceKeys.Info.ReplacementCreateSuccess, res.Data!.Code, res.Data.Suffix);
                await LoadAnchorInfoCoreAsync();
                return;
            }

            var plan = _uiRenderer.BuildRenderPlan(res.Error!, ContextType.CreateReplacementCreate);
            _uiDispatcher.Apply(plan, this);
        }
        finally
        {
            IsSubmitting = false;
            CreateCommand.RaiseCanExecuteChanged();
        }
    }
}
