using System.Collections.ObjectModel;
using MaterialCodingSystem.Application;
using MaterialCodingSystem.Application.Contracts;
using MaterialCodingSystem.Presentation.Scheduling;
using MaterialCodingSystem.Presentation.Services;

namespace MaterialCodingSystem.Presentation.ViewModels;

public sealed class CreateMaterialViewModel : ViewModelBase
{
    private const string CandidateDebounceKey = "create_material_candidates";

    private readonly MaterialApplicationService _app;
    private readonly IDebouncer _debouncer;
    private readonly IDialogService _dialogService;
    private readonly Func<string, Task> _navigateToReplacementByCode;
    private readonly Func<MaterialItemSpecHit, Task> _navigateToReplacementFromCandidate;
    private readonly Func<Task> _openAddCategoryDialog;

    public ObservableCollection<CategoryDto> Categories { get; } = new();
    public ObservableCollection<MaterialItemSpecHit> CandidateItems { get; } = new();

    private CategoryDto? _selectedCategory;
    public CategoryDto? SelectedCategory
    {
        get => _selectedCategory;
        set
        {
            if (SetProperty(ref _selectedCategory, value))
                ScheduleCandidateRefresh();
        }
    }

    private MaterialSearchKeywordSource _keywordSource = MaterialSearchKeywordSource.None;
    public MaterialSearchKeywordSource KeywordSource
    {
        get => _keywordSource;
        set => SetProperty(ref _keywordSource, value);
    }

    private string _spec = "";
    public string Spec
    {
        get => _spec;
        set
        {
            if (SetProperty(ref _spec, value))
            {
                if (KeywordSource == MaterialSearchKeywordSource.Spec)
                    ScheduleCandidateRefresh();
            }
        }
    }

    private string _description = "";
    public string Description
    {
        get => _description;
        set
        {
            if (SetProperty(ref _description, value))
            {
                if (KeywordSource == MaterialSearchKeywordSource.Description)
                    ScheduleCandidateRefresh();
            }
        }
    }

    private string _name = "";
    public string Name { get => _name; set => SetProperty(ref _name, value); }

    private string _brand = "";
    public string Brand { get => _brand; set => SetProperty(ref _brand, value); }

    private string _result = "";
    public string Result { get => _result; set => SetProperty(ref _result, value); }

    private string _specFieldError = "";
    public string SpecFieldError { get => _specFieldError; set => SetProperty(ref _specFieldError, value); }

    private string _globalError = "";
    public string GlobalError { get => _globalError; set => SetProperty(ref _globalError, value); }

    private bool _candidateLoading;
    public bool CandidateLoading { get => _candidateLoading; set => SetProperty(ref _candidateLoading, value); }

    private string _candidateStatus = "";
    public string CandidateStatus { get => _candidateStatus; set => SetProperty(ref _candidateStatus, value); }

    private bool _decisionBarVisible;
    public bool DecisionBarVisible { get => _decisionBarVisible; set => SetProperty(ref _decisionBarVisible, value); }

    private MaterialItemSpecHit? _selectedCandidate;
    public MaterialItemSpecHit? SelectedCandidate
    {
        get => _selectedCandidate;
        set => SetProperty(ref _selectedCandidate, value);
    }

    public RelayCommand CreateCommand { get; }
    public RelayCommand RefreshCategoriesCommand { get; }
    public RelayCommand OpenAddCategoryCommand { get; }
    public RelayCommand<MaterialItemSpecHit> AddCandidateAsReplacementCommand { get; }
    public RelayCommand<MaterialItemSpecHit> UseCandidateAsReplacementCommand { get; }
    public RelayCommand ForceNewMaterialCommand { get; }

    public CreateMaterialViewModel(
        MaterialApplicationService app,
        IDebouncer debouncer,
        IDialogService dialogService,
        Func<string, Task> navigateToReplacementByCode,
        Func<MaterialItemSpecHit, Task> navigateToReplacementFromCandidate,
        Func<Task> openAddCategoryDialog)
    {
        _app = app;
        _debouncer = debouncer;
        _dialogService = dialogService;
        _navigateToReplacementByCode = navigateToReplacementByCode;
        _navigateToReplacementFromCandidate = navigateToReplacementFromCandidate;
        _openAddCategoryDialog = openAddCategoryDialog;

        CreateCommand = new RelayCommand(async () => await CreateAsync());
        RefreshCategoriesCommand = new RelayCommand(async () => await RefreshCategoriesAsync());
        OpenAddCategoryCommand = new RelayCommand(async () => await OpenAddCategoryAsync());
        AddCandidateAsReplacementCommand = new RelayCommand<MaterialItemSpecHit>(async hit =>
        {
            if (hit is not null)
                await _navigateToReplacementByCode(hit.Code);
        });
        UseCandidateAsReplacementCommand = new RelayCommand<MaterialItemSpecHit>(async hit =>
        {
            if (hit is not null)
                await _navigateToReplacementFromCandidate(hit);
        });
        ForceNewMaterialCommand = new RelayCommand(() =>
        {
            DecisionBarVisible = false;
        });

        _ = RefreshCategoriesAsync();
    }

    public void NotifySpecFieldFocused() => KeywordSource = MaterialSearchKeywordSource.Spec;

    public void NotifyDescriptionFieldFocused() => KeywordSource = MaterialSearchKeywordSource.Description;

    private void ScheduleCandidateRefresh()
    {
        _debouncer.Debounce(CandidateDebounceKey, TimeSpan.FromMilliseconds(300), RefreshCandidatesCoreAsync);
    }

    private async Task RefreshCandidatesCoreAsync(CancellationToken ct)
    {
        CandidateItems.Clear();
        DecisionBarVisible = false;
        CandidateStatus = "";
        if (KeywordSource == MaterialSearchKeywordSource.None)
        {
            CandidateStatus = "请先点击规格号或规格描述输入框，再输入以查看候选（Top20）。";
            return;
        }

        var keyword = KeywordSource == MaterialSearchKeywordSource.Spec ? Spec : Description;
        var categoryCode = SelectedCategory?.Code?.Trim() ?? "";
        if (string.IsNullOrWhiteSpace(categoryCode) || string.IsNullOrWhiteSpace(keyword))
        {
            CandidateStatus = "请选择分类并输入关键字。";
            return;
        }

        CandidateLoading = true;
        try
        {
            var res = await _app.SearchBySpec(new SearchQuery(
                CodeKeyword: null,
                SpecKeyword: keyword.Trim(),
                CategoryCode: categoryCode,
                IncludeDeprecated: false,
                Limit: 20,
                Offset: 0));

            if (!res.IsSuccess)
            {
                CandidateStatus = $"候选加载失败：{res.Error!.Code}";
                return;
            }

            foreach (var x in res.Data!.Items)
                CandidateItems.Add(x);

            CandidateStatus = res.Data.Items.Count == 0
                ? "未发现匹配项。"
                : $"找到 {res.Data.Items.Count} 条候选（子串包含，仅供参考）。";

            if (res.Data.Items.Count > 0)
                DecisionBarVisible = true;
        }
        finally
        {
            CandidateLoading = false;
        }
    }

    private async Task OpenAddCategoryAsync()
    {
        await _openAddCategoryDialog();
        await RefreshCategoriesAsync();
    }

    private async Task RefreshCategoriesAsync()
    {
        var res = await _app.ListCategories();
        if (!res.IsSuccess)
        {
            Result = $"分类加载失败：{res.Error!.Code} - {res.Error.Message}";
            return;
        }

        Categories.Clear();
        foreach (var c in res.Data!)
            Categories.Add(c);

        var prev = SelectedCategory?.Code;
        SelectedCategory = Categories.FirstOrDefault(x => x.Code == prev) ?? Categories.FirstOrDefault();
    }

    private async Task CreateAsync()
    {
        SpecFieldError = "";
        GlobalError = "";
        Result = "处理中...";
        var categoryCode = SelectedCategory?.Code?.Trim() ?? "";
        if (string.IsNullOrWhiteSpace(categoryCode))
        {
            Result = "请先选择分类。";
            return;
        }

        var res = await _app.CreateMaterialItemA(new CreateMaterialItemARequest(
            CategoryCode: categoryCode,
            Spec: Spec,
            Name: Name,
            Description: Description,
            Brand: string.IsNullOrWhiteSpace(Brand) ? null : Brand
        ));

        if (res.IsSuccess)
        {
            Result = $"创建成功：{res.Data!.Code}（spec_normalized={res.Data.SpecNormalized}）";
            return;
        }

        if (res.Error!.Code == ErrorCodes.SPEC_DUPLICATE)
            SpecFieldError = "规格号重复（同分类内 spec 已存在）。";
        else if (res.Error.Code == ErrorCodes.CODE_CONFLICT_RETRY)
        {
            GlobalError = "系统繁忙，请稍后重试。";
            _dialogService.ShowWarning("提示", GlobalError);
        }
        else
            Result = $"失败：{res.Error.Code} - {res.Error.Message}";
    }
}
