using System.Collections.ObjectModel;
using System.Windows.Media;
using MaterialCodingSystem.Application;
using MaterialCodingSystem.Application.Contracts;
using MaterialCodingSystem.Presentation.Scheduling;
using MaterialCodingSystem.Presentation.UiSemantics;

namespace MaterialCodingSystem.Presentation.ViewModels;

public sealed class CreateMaterialViewModel : ViewModelBase
{
    private const string CandidateDebounceKey = "create_material_candidates";

    private static readonly Brush BrushGray = new SolidColorBrush(Color.FromRgb(0x6B, 0x72, 0x80));
    private static readonly Brush BrushGreen = new SolidColorBrush(Color.FromRgb(0x16, 0xA3, 0x4A));
    private static readonly Brush BrushOrange = new SolidColorBrush(Color.FromRgb(0xEA, 0x58, 0x0C));
    private static readonly Brush BrushRed = new SolidColorBrush(Color.FromRgb(0xDC, 0x26, 0x26));
    private static readonly Brush BrushStateStripBgIdle = new SolidColorBrush(Color.FromRgb(0xF3, 0xF4, 0xF6));
    private static readonly Brush BrushStateStripBgOk = new SolidColorBrush(Color.FromRgb(0xEC, 0xFD, 0xF5));
    private static readonly Brush BrushStateStripBgWarn = new SolidColorBrush(Color.FromRgb(0xFF, 0xFB, 0xEB));
    private static readonly Brush BrushStateStripBgRisk = new SolidColorBrush(Color.FromRgb(0xFE, 0xE2, 0xE2));
    private static readonly Brush BrushInfoBarSurface = new SolidColorBrush(Color.FromRgb(0xFF, 0xFB, 0xEB));
    private static readonly Brush BrushInfoBarBorder = new SolidColorBrush(Color.FromRgb(0xFD, 0xBA, 0x74));

    private readonly MaterialApplicationService _app;
    private readonly IDebouncer _debouncer;
    private readonly IUiRenderer _uiRenderer;
    private readonly IUiDispatcher _uiDispatcher;
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
        set
        {
            if (SetProperty(ref _keywordSource, value))
                ScheduleCandidateRefresh();
        }
    }

    private CreateDecisionState _decisionState = CreateDecisionState.Idle;
    public CreateDecisionState DecisionState => _decisionState;

    public bool ShowDecisionBar => DecisionState == CreateDecisionState.HasCandidate;

    private MaterialItemSpecHit? _selectedCandidate;
    public MaterialItemSpecHit? SelectedCandidate
    {
        get => _selectedCandidate;
        set
        {
            if (SetProperty(ref _selectedCandidate, value))
                UseCandidateAsReplacementCommand.RaiseCanExecuteChanged();
        }
    }

    private string _spec = "";
    public string Spec
    {
        get => _spec;
        set
        {
            if (SetProperty(ref _spec, value))
            {
                ScheduleCandidateRefresh();
                UpdateSpecInputStateHint();
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
                ScheduleCandidateRefresh();
        }
    }

    private string _name = "";
    public string Name { get => _name; set => SetProperty(ref _name, value); }

    private string _brand = "";
    public string Brand { get => _brand; set => SetProperty(ref _brand, value); }

    private string _result = "";
    public string Result { get => _result; set => SetProperty(ref _result, value); }

    private string _specFieldError = "";
    public string SpecFieldError
    {
        get => _specFieldError;
        set
        {
            if (SetProperty(ref _specFieldError, value))
                UpdateSpecInputStateHint();
        }
    }

    private string _globalError = "";
    public string GlobalError { get => _globalError; set => SetProperty(ref _globalError, value); }

    private bool _candidateLoading;
    public bool CandidateLoading
    {
        get => _candidateLoading;
        set
        {
            if (SetProperty(ref _candidateLoading, value))
                UpdateDecisionPresentation();
        }
    }

    private string _candidateStatus = "";
    public string CandidateStatus { get => _candidateStatus; set => SetProperty(ref _candidateStatus, value); }

    private string _candidateStateIcon = "🔍";
    public string CandidateStateIcon { get => _candidateStateIcon; private set => SetProperty(ref _candidateStateIcon, value); }

    private string _candidateStateMessage = "";
    public string CandidateStateMessage { get => _candidateStateMessage; private set => SetProperty(ref _candidateStateMessage, value); }

    private string _candidateStateSubMessage = "";
    public string CandidateStateSubMessage { get => _candidateStateSubMessage; private set => SetProperty(ref _candidateStateSubMessage, value); }

    private Brush _candidateStateForeground = BrushGray;
    public Brush CandidateStateForeground { get => _candidateStateForeground; private set => SetProperty(ref _candidateStateForeground, value); }

    private Brush _candidateStateStripBackground = BrushStateStripBgIdle;
    public Brush CandidateStateStripBackground { get => _candidateStateStripBackground; private set => SetProperty(ref _candidateStateStripBackground, value); }

    private Brush _decisionInfoBarBackground = BrushInfoBarSurface;
    public Brush DecisionInfoBarBackground { get => _decisionInfoBarBackground; private set => SetProperty(ref _decisionInfoBarBackground, value); }

    private Brush _decisionInfoBarBorderBrush = BrushInfoBarBorder;
    public Brush DecisionInfoBarBorderBrush { get => _decisionInfoBarBorderBrush; private set => SetProperty(ref _decisionInfoBarBorderBrush, value); }

    private bool _showDecisionInfoBar;
    public bool ShowDecisionInfoBar { get => _showDecisionInfoBar; private set => SetProperty(ref _showDecisionInfoBar, value); }

    private bool _showCandidateEmptyState;
    public bool ShowCandidateEmptyState { get => _showCandidateEmptyState; private set => SetProperty(ref _showCandidateEmptyState, value); }

    private bool _showCandidateCardList;
    public bool ShowCandidateCardList { get => _showCandidateCardList; private set => SetProperty(ref _showCandidateCardList, value); }

    /// <summary>Placeholder: set true when high-similarity risk logic is wired.</summary>
    public bool ShowHighRiskDuplicateState { get; private set; }

    private string _specInputStateHint = "";
    public string SpecInputStateHint { get => _specInputStateHint; private set => SetProperty(ref _specInputStateHint, value); }

    public string CandidateSimilarityPlaceholder =>
        UiResources.Get(UiResourceKeys.Info.CreateMaterialCandidateSimilarityPlaceholder);

    public RelayCommand CreateCommand { get; }
    public RelayCommand RefreshCategoriesCommand { get; }
    public RelayCommand OpenAddCategoryCommand { get; }
    public RelayCommand<MaterialItemSpecHit> UseCandidateAsReplacementCommand { get; }
    public RelayCommand<MaterialItemSpecHit> SelectCandidateCommand { get; }
    public RelayCommand ForceCreateWithConfirmCommand { get; }

    public CreateMaterialViewModel(
        MaterialApplicationService app,
        IDebouncer debouncer,
        IUiRenderer uiRenderer,
        IUiDispatcher uiDispatcher,
        Func<MaterialItemSpecHit, Task> navigateToReplacementFromCandidate,
        Func<Task> openAddCategoryDialog)
    {
        BrushGray.Freeze();
        BrushGreen.Freeze();
        BrushOrange.Freeze();
        BrushRed.Freeze();
        BrushStateStripBgIdle.Freeze();
        BrushStateStripBgOk.Freeze();
        BrushStateStripBgWarn.Freeze();
        BrushStateStripBgRisk.Freeze();
        BrushInfoBarSurface.Freeze();
        BrushInfoBarBorder.Freeze();

        _app = app;
        _debouncer = debouncer;
        _uiRenderer = uiRenderer;
        _uiDispatcher = uiDispatcher;
        _navigateToReplacementFromCandidate = navigateToReplacementFromCandidate;
        _openAddCategoryDialog = openAddCategoryDialog;

        CreateCommand = new RelayCommand(async () => await CreateAsync(), CanExecuteCreate);
        RefreshCategoriesCommand = new RelayCommand(async () => await RefreshCategoriesAsync());
        OpenAddCategoryCommand = new RelayCommand(async () => await OpenAddCategoryAsync());
        UseCandidateAsReplacementCommand = new RelayCommand<MaterialItemSpecHit>(
            async hit =>
            {
                if (hit is not null)
                    await _navigateToReplacementFromCandidate(hit);
            },
            hit => hit is not null);
        SelectCandidateCommand = new RelayCommand<MaterialItemSpecHit>(
            hit =>
            {
                if (hit is not null)
                    SelectedCandidate = hit;
            },
            hit => hit is not null);
        ForceCreateWithConfirmCommand = new RelayCommand(
            () =>
            {
                if (!_uiRenderer.ConfirmDuplicateCreate())
                    return;
                SetDecisionState(CreateDecisionState.ForcedCreate);
            },
            () => DecisionState == CreateDecisionState.HasCandidate);

        UpdateSpecInputStateHint();
        UpdateDecisionPresentation();
        _ = RefreshCategoriesAsync();
    }

    public void NotifySpecFieldFocused() => KeywordSource = MaterialSearchKeywordSource.Spec;

    public void NotifyDescriptionFieldFocused() => KeywordSource = MaterialSearchKeywordSource.Description;

    private void UpdateSpecInputStateHint()
    {
        if (!string.IsNullOrEmpty(SpecFieldError))
            SpecInputStateHint = UiResources.Get(UiResourceKeys.Info.SpecStateDuplicate);
        else if (string.IsNullOrWhiteSpace(Spec))
            SpecInputStateHint = UiResources.Get(UiResourceKeys.Info.SpecStatePending);
        else
            SpecInputStateHint = UiResources.Get(UiResourceKeys.Info.SpecStateNormal);
    }

    private static bool AreSpecAndDescriptionBothEmpty(string spec, string description) =>
        string.IsNullOrWhiteSpace(spec) && string.IsNullOrWhiteSpace(description);

    private bool CanExecuteCreate()
    {
        if (DecisionState == CreateDecisionState.Searching || DecisionState == CreateDecisionState.HasCandidate)
            return false;
        if (DecisionState == CreateDecisionState.NoCandidate || DecisionState == CreateDecisionState.ForcedCreate)
            return true;
        return DecisionState == CreateDecisionState.Idle && KeywordSource == MaterialSearchKeywordSource.None;
    }

    private bool SetDecisionState(CreateDecisionState value)
    {
        if (!SetProperty(ref _decisionState, value, nameof(DecisionState)))
            return false;
        NotifyPropertyChanged(nameof(ShowDecisionBar));
        CreateCommand.RaiseCanExecuteChanged();
        ForceCreateWithConfirmCommand.RaiseCanExecuteChanged();
        UpdateDecisionPresentation();
        return true;
    }

    private void UpdateDecisionPresentation()
    {
        var count = CandidateItems.Count;
        ShowCandidateCardList = count > 0;
        ShowCandidateEmptyState = !CandidateLoading && count == 0 && DecisionState == CreateDecisionState.Idle;
        CandidateStateSubMessage = CandidateStatus;

        if (ShowHighRiskDuplicateState && !CandidateLoading)
        {
            CandidateStateIcon = "🚨";
            CandidateStateMessage = UiResources.Get(UiResourceKeys.Info.DecisionHighRiskMessage);
            CandidateStateForeground = BrushRed;
            CandidateStateStripBackground = BrushStateStripBgRisk;
            ShowDecisionInfoBar = count > 0;
            DecisionInfoBarBackground = new SolidColorBrush(Color.FromRgb(0xFE, 0xE2, 0xE2));
            DecisionInfoBarBorderBrush = BrushRed;
            return;
        }

        if (CandidateLoading)
        {
            CandidateStateIcon = "⏳";
            CandidateStateMessage = UiResources.Get(UiResourceKeys.Info.DecisionSearchingMessage);
            CandidateStateForeground = BrushGray;
            CandidateStateStripBackground = BrushStateStripBgIdle;
            ShowDecisionInfoBar = false;
            return;
        }

        switch (DecisionState)
        {
            case CreateDecisionState.Idle:
                CandidateStateIcon = "🔍";
                CandidateStateMessage = UiResources.Get(UiResourceKeys.Info.DecisionIdleMessage);
                CandidateStateForeground = BrushGray;
                CandidateStateStripBackground = BrushStateStripBgIdle;
                ShowDecisionInfoBar = false;
                break;
            case CreateDecisionState.Searching:
                CandidateStateIcon = "⏳";
                CandidateStateMessage = UiResources.Get(UiResourceKeys.Info.DecisionSearchingMessage);
                CandidateStateForeground = BrushGray;
                CandidateStateStripBackground = BrushStateStripBgIdle;
                ShowDecisionInfoBar = false;
                break;
            case CreateDecisionState.NoCandidate:
                CandidateStateIcon = "✔";
                CandidateStateMessage = UiResources.Get(UiResourceKeys.Info.DecisionNoCandidateMessage);
                CandidateStateForeground = BrushGreen;
                CandidateStateStripBackground = BrushStateStripBgOk;
                ShowDecisionInfoBar = false;
                break;
            case CreateDecisionState.HasCandidate:
                CandidateStateIcon = "⚠";
                CandidateStateMessage = UiResources.Get(UiResourceKeys.Info.DecisionHasCandidateMessage);
                CandidateStateForeground = BrushOrange;
                CandidateStateStripBackground = BrushStateStripBgWarn;
                ShowDecisionInfoBar = count > 0;
                DecisionInfoBarBackground = BrushInfoBarSurface;
                DecisionInfoBarBorderBrush = BrushInfoBarBorder;
                break;
            case CreateDecisionState.ForcedCreate:
                CandidateStateIcon = "✔";
                CandidateStateMessage = UiResources.Get(UiResourceKeys.Info.DecisionForcedCreateMessage);
                CandidateStateForeground = BrushGreen;
                CandidateStateStripBackground = BrushStateStripBgOk;
                ShowDecisionInfoBar = false;
                break;
            default:
                CandidateStateIcon = "🔍";
                CandidateStateMessage = "";
                CandidateStateForeground = BrushGray;
                CandidateStateStripBackground = BrushStateStripBgIdle;
                ShowDecisionInfoBar = false;
                break;
        }
    }

    private void ScheduleCandidateRefresh()
    {
        if (DecisionState == CreateDecisionState.ForcedCreate)
            SetDecisionState(CreateDecisionState.Searching);

        PrimeSearchingStateIfApplicable();
        _debouncer.Debounce(CandidateDebounceKey, TimeSpan.FromMilliseconds(300), RefreshCandidatesCoreAsync);
    }

    private void TrySetIdleIfBothInputsEmpty()
    {
        if (AreSpecAndDescriptionBothEmpty(Spec, Description))
            SetDecisionState(CreateDecisionState.Idle);
    }

    private void PrimeSearchingStateIfApplicable()
    {
        if (KeywordSource == MaterialSearchKeywordSource.None)
        {
            CandidateItems.Clear();
            SelectedCandidate = null;
            CandidateLoading = false;
            CandidateStatus = "";
            TrySetIdleIfBothInputsEmpty();
            CreateCommand.RaiseCanExecuteChanged();
            UpdateDecisionPresentation();
            return;
        }

        if (string.IsNullOrWhiteSpace(SelectedCategory?.Code))
        {
            CandidateItems.Clear();
            SelectedCandidate = null;
            CandidateLoading = false;
            CandidateStatus = UiResources.Get(UiResourceKeys.Info.CandidatePickCategoryAndKeyword);
            TrySetIdleIfBothInputsEmpty();
            CreateCommand.RaiseCanExecuteChanged();
            UpdateDecisionPresentation();
            return;
        }

        var keyword = KeywordSource == MaterialSearchKeywordSource.Spec ? Spec : Description;
        if (string.IsNullOrWhiteSpace(keyword))
        {
            CandidateItems.Clear();
            SelectedCandidate = null;
            CandidateLoading = false;
            CandidateStatus = "";
            TrySetIdleIfBothInputsEmpty();
            CreateCommand.RaiseCanExecuteChanged();
            UpdateDecisionPresentation();
            return;
        }

        CandidateStatus = UiResources.Get(UiResourceKeys.Info.CandidateSearching);
        SetDecisionState(CreateDecisionState.Searching);
        CandidateLoading = true;
        CreateCommand.RaiseCanExecuteChanged();
    }

    private async Task RefreshCandidatesCoreAsync(CancellationToken ct)
    {
        CandidateItems.Clear();
        SelectedCandidate = null;
        CandidateStatus = "";

        if (KeywordSource == MaterialSearchKeywordSource.None)
        {
            CandidateLoading = false;
            TrySetIdleIfBothInputsEmpty();
            CreateCommand.RaiseCanExecuteChanged();
            UpdateDecisionPresentation();
            return;
        }

        var keyword = KeywordSource == MaterialSearchKeywordSource.Spec ? Spec : Description;
        var categoryCode = SelectedCategory?.Code?.Trim() ?? "";
        if (string.IsNullOrWhiteSpace(categoryCode) || string.IsNullOrWhiteSpace(keyword))
        {
            CandidateLoading = false;
            CandidateStatus = string.IsNullOrWhiteSpace(categoryCode)
                ? UiResources.Get(UiResourceKeys.Info.CandidatePickCategoryAndKeyword)
                : "";
            TrySetIdleIfBothInputsEmpty();
            CreateCommand.RaiseCanExecuteChanged();
            UpdateDecisionPresentation();
            return;
        }

        CandidateStatus = UiResources.Get(UiResourceKeys.Info.CandidateSearching);
        CandidateLoading = true;
        SetDecisionState(CreateDecisionState.Searching);
        CreateCommand.RaiseCanExecuteChanged();

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
                var plan = _uiRenderer.BuildRenderPlan(res.Error!, ContextType.CreateMaterialCandidates);
                _uiDispatcher.Apply(plan, this);
                TrySetIdleIfBothInputsEmpty();
                CreateCommand.RaiseCanExecuteChanged();
                return;
            }

            foreach (var x in res.Data!.Items)
                CandidateItems.Add(x);

            if (res.Data.Items.Count > 0)
            {
                CandidateStatus = UiResources.Format(UiResourceKeys.Info.CandidatePossibleDuplicateTop20, res.Data.Items.Count);
                SetDecisionState(CreateDecisionState.HasCandidate);
                SelectedCandidate = CandidateItems[0];
            }
            else
            {
                CandidateStatus = UiResources.Get(UiResourceKeys.Info.CandidateNoMatchCanCreate);
                SetDecisionState(CreateDecisionState.NoCandidate);
            }
        }
        finally
        {
            CandidateLoading = false;
            CreateCommand.RaiseCanExecuteChanged();
            UseCandidateAsReplacementCommand.RaiseCanExecuteChanged();
            UpdateDecisionPresentation();
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
            var plan = _uiRenderer.BuildRenderPlan(res.Error!, ContextType.CreateMaterialListCategories);
            _uiDispatcher.Apply(plan, this);
            return;
        }

        Categories.Clear();
        foreach (var c in res.Data!)
            Categories.Add(c);

        var prev = SelectedCategory?.Code;
        SelectedCategory = Categories.FirstOrDefault(x => x.Code == prev) ?? Categories.FirstOrDefault();
        UpdateDecisionPresentation();
    }

    private async Task CreateAsync()
    {
        ClearCreateMaterialSubmitFeedback();
        Result = UiResources.Get(UiResourceKeys.Info.CreateMaterialProcessing);
        var categoryCode = SelectedCategory?.Code?.Trim() ?? "";
        if (string.IsNullOrWhiteSpace(categoryCode))
        {
            Result = UiResources.Get(UiResourceKeys.Hint.SelectCategory);
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
            Result = UiResources.Format(
                UiResourceKeys.Info.CreateMaterialCreateSuccess,
                res.Data!.Code,
                res.Data.SpecNormalized);
            ScheduleCandidateRefresh();
            return;
        }

        var plan = _uiRenderer.BuildRenderPlan(res.Error!, ContextType.CreateMaterialCreate);
        _uiDispatcher.Apply(plan, this);
        UpdateSpecInputStateHint();
    }

    private void ClearCreateMaterialSubmitFeedback()
    {
        SpecFieldError = "";
        GlobalError = "";
        Result = "";
    }
}
