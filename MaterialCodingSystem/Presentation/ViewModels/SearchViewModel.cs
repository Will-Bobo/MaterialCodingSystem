using System.Collections.ObjectModel;
using MaterialCodingSystem.Application;
using MaterialCodingSystem.Application.Contracts;

namespace MaterialCodingSystem.Presentation.ViewModels;

public sealed class SearchViewModel : ViewModelBase
{
    private readonly MaterialApplicationService _app;
    private readonly MainViewModel _main;

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

    private bool _includeDeprecated;
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
    public RelayCommand AddSelectedAsReplacementCommand { get; }
    public RelayCommand<MaterialItemSpecHit> AddSpecHitAsReplacementCommand { get; }
    public RelayCommand<MaterialItemSummary> AddCodeHitAsReplacementCommand { get; }
    public RelayCommand RefreshCategoriesCommand { get; }

    public SearchViewModel(MaterialApplicationService app, MainViewModel main)
    {
        _app = app;
        _main = main;
        SearchByCodeCommand = new RelayCommand(async () => await SearchByCodeAsync());
        SearchBySpecCommand = new RelayCommand(async () => await SearchBySpecAsync());
        AddSelectedAsReplacementCommand = new RelayCommand(async () => await AddSelectedAsReplacementAsync());
        AddSpecHitAsReplacementCommand = new RelayCommand<MaterialItemSpecHit>(async hit => await AddAsReplacementAsync(hit?.Code));
        AddCodeHitAsReplacementCommand = new RelayCommand<MaterialItemSummary>(async hit => await AddAsReplacementAsync(hit?.Code));
        RefreshCategoriesCommand = new RelayCommand(async () => await RefreshCategoriesAsync());

        _ = RefreshCategoriesAsync();
    }

    private async Task RefreshCategoriesAsync()
    {
        var res = await _app.ListCategories();
        if (!res.IsSuccess)
        {
            Message = $"分类加载失败：{res.Error!.Code} - {res.Error.Message}";
            return;
        }

        Categories.Clear();
        foreach (var c in res.Data!)
        {
            Categories.Add(c);
        }

        var prev = SelectedCategory?.Code;
        SelectedCategory = Categories.FirstOrDefault(x => x.Code == prev) ?? Categories.FirstOrDefault();
    }

    private async Task SearchByCodeAsync()
    {
        Message = "搜索中...";
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
            Message = $"失败：{res.Error!.Code} - {res.Error.Message}";
            return;
        }

        foreach (var item in res.Data!.Items) CodeResults.Add(item);
        Message = $"完成：{res.Data.Items.Count} 条";
    }

    private async Task SearchBySpecAsync()
    {
        Message = "搜索中...";
        SpecResults.Clear();
        SelectedSpecResult = null;

        var categoryCode = SelectedCategory?.Code?.Trim() ?? "";
        if (string.IsNullOrWhiteSpace(categoryCode))
        {
            Message = "请先选择分类。";
            return;
        }

        var res = await _app.SearchBySpec(new SearchQuery(
            CodeKeyword: null,
            SpecKeyword: SpecKeyword,
            CategoryCode: categoryCode,
            IncludeDeprecated: IncludeDeprecated,
            Limit: 20,
            Offset: 0
        ));

        if (!res.IsSuccess)
        {
            Message = $"失败：{res.Error!.Code} - {res.Error.Message}";
            return;
        }

        foreach (var item in res.Data!.Items) SpecResults.Add(item);
        Message = $"完成：{res.Data.Items.Count} 条（Top20）";
    }

    private async Task AddSelectedAsReplacementAsync()
    {
        if (SelectedSpecResult is null)
        {
            Message = "请先在 Top20 结果中选择一条。";
            return;
        }

        // 只负责“跳转与带参”，不在 UI 里做任何业务推断
        await AddAsReplacementAsync(SelectedSpecResult.Code);
    }

    private async Task AddAsReplacementAsync(string? code)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            Message = "缺少编码，无法跳转。";
            return;
        }

        await _main.NavigateToReplacementFromExistingCodeAsync(code);
    }
}

