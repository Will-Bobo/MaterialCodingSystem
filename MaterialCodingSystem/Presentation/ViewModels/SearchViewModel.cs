using System.Collections.ObjectModel;
using System.Linq;
using MaterialCodingSystem.Application;
using MaterialCodingSystem.Application.Contracts;
using MaterialCodingSystem.Presentation.Models;
using MaterialCodingSystem.Presentation.UiSemantics;

namespace MaterialCodingSystem.Presentation.ViewModels;

public sealed class SearchViewModel : ViewModelBase
{
    private readonly MaterialApplicationService _app;
    private readonly MainViewModel _main;
    private readonly IUiRenderer _uiRenderer;
    private readonly IUiDispatcher _uiDispatcher;

    private string _codeKeyword = "";
    public string CodeKeyword { get => _codeKeyword; set => SetProperty(ref _codeKeyword, value); }

    private string _specKeyword = "";
    public string SpecKeyword { get => _specKeyword; set => SetProperty(ref _specKeyword, value); }

    public ObservableCollection<CategoryDto> Categories { get; } = new();

    private CategoryDto? _selectedCategory;
    public CategoryDto? SelectedCategory
    {
        get => _selectedCategory;
        set => SetProperty(ref _selectedCategory, value);
    }

    private bool _includeDeprecated = true;
    public bool IncludeDeprecated { get => _includeDeprecated; set => SetProperty(ref _includeDeprecated, value); }

    public ObservableCollection<MaterialItemSummary> CodeResults { get; } = new();
    public ObservableCollection<MaterialItemSpecHit> SpecResults { get; } = new();

    private MaterialItemSpecHit? _selectedSpecResult;
    public MaterialItemSpecHit? SelectedSpecResult
    {
        get => _selectedSpecResult;
        set => SetProperty(ref _selectedSpecResult, value);
    }

    private string _message = "";
    public string Message { get => _message; set => SetProperty(ref _message, value); }

    public RelayCommand SearchByCodeCommand { get; }
    public RelayCommand SearchBySpecCommand { get; }
    public RelayCommand<MaterialItemSpecHit> AddSpecHitAsReplacementCommand { get; }
    public RelayCommand<MaterialItemSummary> AddCodeHitAsReplacementCommand { get; }
    public RelayCommand<MaterialItemSpecHit> DeprecateSpecHitCommand { get; }
    public RelayCommand<MaterialItemSummary> DeprecateCodeHitCommand { get; }
    public RelayCommand RefreshCategoriesCommand { get; }

    public SearchViewModel(MaterialApplicationService app, MainViewModel main, IUiRenderer uiRenderer, IUiDispatcher uiDispatcher)
    {
        _app = app;
        _main = main;
        _uiRenderer = uiRenderer;
        _uiDispatcher = uiDispatcher;
        SearchByCodeCommand = new RelayCommand(async () => await SearchByCodeAsync());
        SearchBySpecCommand = new RelayCommand(async () => await SearchBySpecAsync());
        AddSpecHitAsReplacementCommand = new RelayCommand<MaterialItemSpecHit>(async hit => await AddAsReplacementAsync(hit?.Code));
        AddCodeHitAsReplacementCommand = new RelayCommand<MaterialItemSummary>(async hit => await AddAsReplacementAsync(hit?.Code));
        DeprecateSpecHitCommand = new RelayCommand<MaterialItemSpecHit>(async hit => await DeprecateAsync(hit?.Code, hit?.Status));
        DeprecateCodeHitCommand = new RelayCommand<MaterialItemSummary>(async hit => await DeprecateAsync(hit?.Code, hit?.Status));
        RefreshCategoriesCommand = new RelayCommand(async () => await RefreshCategoriesAsync());

        _ = RefreshCategoriesAsync();
    }

    private async Task RefreshCategoriesAsync()
    {
        var res = await _app.ListCategories();
        if (!res.IsSuccess)
        {
            var plan = _uiRenderer.BuildRenderPlan(res.Error!, ContextType.SearchListCategories);
            _uiDispatcher.Apply(plan, this);
            return;
        }

        Categories.Clear();
        foreach (var c in res.Data!)
        {
            Categories.Add(c);
        }

        // UI: 默认“全部”，允许不选分类也能查
        Categories.Insert(0, new CategoryDto("*", "全部分类"));
        SelectedCategory = Categories.FirstOrDefault();
    }

    private async Task SearchByCodeAsync()
    {
        Message = UiResources.Get(UiResourceKeys.Info.SearchSearchingCode);
        CodeResults.Clear();

        var res = await _app.SearchByCode(new SearchQuery(
            CodeKeyword: CodeKeyword,
            SpecKeyword: null,
            CategoryCode: null,
            IncludeDeprecated: IncludeDeprecated,
            Limit: 20,
            Offset: 0
        ));

        if (!res.IsSuccess)
        {
            var plan = _uiRenderer.BuildRenderPlan(res.Error!, ContextType.SearchByCode);
            _uiDispatcher.Apply(plan, this);
            return;
        }

        foreach (var item in res.Data!.Items) CodeResults.Add(item);
        Message = UiResources.Format(UiResourceKeys.Info.SearchCodeDone, res.Data.Items.Count);
    }

    private async Task SearchBySpecAsync()
    {
        Message = UiResources.Get(UiResourceKeys.Info.SearchSearchingSpec);
        SpecResults.Clear();
        SelectedSpecResult = null;

        var categoryCode = SelectedCategory?.Code?.Trim() ?? "*";

        if (!string.IsNullOrWhiteSpace(SpecKeyword) && SpecKeyword.Trim().Length < 2)
            Message = "关键词过短，结果可能不稳定";

        var res = categoryCode == "*"
            ? await _app.SearchBySpecAllAsync(SpecKeyword, IncludeDeprecated, limit: 20)
            : await _app.SearchBySpec(new SearchQuery(
                CodeKeyword: null,
                SpecKeyword: SpecKeyword,
                CategoryCode: categoryCode,
                IncludeDeprecated: IncludeDeprecated,
                Limit: 20,
                Offset: 0
            ));

        if (!res.IsSuccess)
        {
            var plan = _uiRenderer.BuildRenderPlan(res.Error!, ContextType.SearchBySpec);
            _uiDispatcher.Apply(plan, this);
            return;
        }

        foreach (var item in res.Data!.Items) SpecResults.Add(item);
        Message = UiResources.Format(UiResourceKeys.Info.SearchSpecDone, res.Data.Items.Count);
    }

    private async Task AddAsReplacementAsync(string? code)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            Message = UiResources.Get(UiResourceKeys.Info.SearchMissingCodeForJump);
            return;
        }

        // 仅传 code 会丢失 Anchor 展示信息；这里使用 DTO 注入
        if (SelectedSpecResult is not null && SelectedSpecResult.Code == code)
        {
            await _main.NavigateToReplacementFromDtoAsync(SelectedSpecResult);
            return;
        }

        var codeHit = CodeResults.FirstOrDefault(x => x.Code == code);
        if (codeHit is not null)
        {
            await _main.NavigateToReplacementFromDtoAsync(codeHit);
            return;
        }

        await _main.NavigateToReplacementFromExistingCodeAsync(code);
    }

    private async Task DeprecateAsync(string? code, long? status)
    {
        if (string.IsNullOrWhiteSpace(code))
            return;
        if (status == 0)
            return;

        var model = BuildDeprecateConfirmModel(code);
        if (!await _uiRenderer.ConfirmDeprecateAsync(model))
            return;

        Message = UiResources.Get(UiResourceKeys.Info.DeprecateProcessing);
        var res = await _app.DeprecateMaterialItem(new DeprecateRequest(code));
        if (res.IsSuccess)
        {
            Message = UiResources.Format(UiResourceKeys.Info.DeprecateDone, res.Data!.Code);
            if (!string.IsNullOrWhiteSpace(CodeKeyword))
                await SearchByCodeAsync();
            if (!string.IsNullOrWhiteSpace(SpecKeyword) && !string.IsNullOrWhiteSpace(SelectedCategory?.Code))
                await SearchBySpecAsync();
            return;
        }

        var plan = _uiRenderer.BuildRenderPlan(res.Error!, ContextType.Deprecate);
        _uiDispatcher.Apply(plan, this);
    }

    private DeprecateConfirmModel BuildDeprecateConfirmModel(string code)
    {
        var specHit = SpecResults.FirstOrDefault(x => x.Code == code);
        if (specHit is not null)
        {
            return new DeprecateConfirmModel
            {
                Code = specHit.Code,
                Spec = specHit.Spec,
                Description = specHit.Description,
                Name = specHit.Name,
                Brand = specHit.Brand
            };
        }

        var codeHit = CodeResults.FirstOrDefault(x => x.Code == code);
        if (codeHit is not null)
        {
            return new DeprecateConfirmModel
            {
                Code = codeHit.Code,
                Spec = codeHit.Spec,
                Description = codeHit.Description,
                Name = codeHit.Name,
                Brand = codeHit.Brand
            };
        }

        return new DeprecateConfirmModel { Code = code };
    }
}
