using System.Collections.ObjectModel;
using System.Windows.Media;
using MaterialCodingSystem.Application;
using MaterialCodingSystem.Application.Contracts;
using MaterialCodingSystem.Presentation.Models;
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
    public ObservableCollection<CandidateItemViewModel> CandidateItems { get; } = new();

    private bool _hasExactSpecMatch;
    public bool HasExactSpecMatch { get => _hasExactSpecMatch; private set => SetProperty(ref _hasExactSpecMatch, value); }

    private (string CategoryCode, string SpecTrim)? _allowedKey;

    private CreateMaterialCodeMode _codeMode = CreateMaterialCodeMode.Auto;
    public CreateMaterialCodeMode CodeMode
    {
        get => _codeMode;
        set
        {
            if (SetProperty(ref _codeMode, value))
            {
                NotifyPropertyChanged(nameof(IsManualExistingCodeMode));
                NotifyPropertyChanged(nameof(IsAutoMode));
                NotifyPropertyChanged(nameof(IsManualMode));

                // Manual mode: disable candidate logic and force idle
                if (_codeMode == CreateMaterialCodeMode.ManualExistingCode)
                {
                    KeywordSource = MaterialSearchKeywordSource.None;
                    ClearCandidatesAndDecisionUi();
                }
                RefreshDerivedState();
                CreateCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public bool IsManualExistingCodeMode => CodeMode == CreateMaterialCodeMode.ManualExistingCode;

    public bool IsAutoMode
    {
        get => CodeMode == CreateMaterialCodeMode.Auto;
        set
        {
            if (value)
                CodeMode = CreateMaterialCodeMode.Auto;
        }
    }

    public bool IsManualMode
    {
        get => CodeMode == CreateMaterialCodeMode.ManualExistingCode;
        set
        {
            if (value)
                CodeMode = CreateMaterialCodeMode.ManualExistingCode;
        }
    }

    private string _existingCode = "";
    public string ExistingCode
    {
        get => _existingCode;
        set
        {
            if (SetProperty(ref _existingCode, value))
                RefreshDerivedState();
        }
    }

    public bool IsForceCreateAllowed
    {
        get
        {
            var key = _allowedKey;
            if (key is null)
                return false;
            var category = SelectedCategory?.Code?.Trim() ?? "";
            var specTrim = Spec?.Trim() ?? "";
            return string.Equals(key.Value.CategoryCode, category, StringComparison.Ordinal)
                && string.Equals(key.Value.SpecTrim, specTrim, StringComparison.Ordinal);
        }
    }

    private CreateMaterialState _state = CreateMaterialState.Empty;
    public CreateMaterialState State { get => _state; private set => SetProperty(ref _state, value); }

    public bool HasCandidates => CandidateItems.Count > 0;

    public bool ShowAllowCreatePanel =>
        State == CreateMaterialState.CandidateConflict && !HasExactSpecMatch;

    private CategoryDto? _selectedCategory;
    public CategoryDto? SelectedCategory
    {
        get => _selectedCategory;
        set
        {
            if (SetProperty(ref _selectedCategory, value))
            {
                RefreshDerivedState();
                ScheduleCandidateRefresh();
            }
        }
    }

    private MaterialSearchKeywordSource _keywordSource = MaterialSearchKeywordSource.None;
    private bool _suppressCandidateRefresh;
    public MaterialSearchKeywordSource KeywordSource
    {
        get => _keywordSource;
        set
        {
            if (SetProperty(ref _keywordSource, value))
            {
                if (!_suppressCandidateRefresh)
                    ScheduleCandidateRefresh();
            }
        }
    }

    private CreateDecisionState _decisionState = CreateDecisionState.Idle;
    public CreateDecisionState DecisionState => _decisionState;

    public bool ShowDecisionBar => DecisionState == CreateDecisionState.HasCandidate;

    private CandidateItemViewModel? _selectedCandidate;
    public CandidateItemViewModel? SelectedCandidate
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
                if (!_suppressCandidateRefresh && KeywordSource == MaterialSearchKeywordSource.None)
                    KeywordSource = MaterialSearchKeywordSource.Spec;
                if (!_suppressCandidateRefresh)
                    ScheduleCandidateRefresh();
                UpdateSpecInputStateHint();
                RecomputeHasExactSpecMatch();
                RefreshDerivedState();
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
                RefreshDerivedState();
        }
    }

    private string _name = "";
    public string Name { get => _name; set => SetProperty(ref _name, value); }

    private string _brand = "";
    public string Brand
    {
        get => _brand;
        set
        {
            if (SetProperty(ref _brand, value))
                RefreshDerivedState();
        }
    }

    private CreateMaterialState ComputeState()
    {
        var specTrim = Spec?.Trim() ?? "";
        if (string.IsNullOrWhiteSpace(specTrim))
            return CreateMaterialState.Empty;

        if (IsManualExistingCodeMode)
        {
            if (string.IsNullOrWhiteSpace(ExistingCode?.Trim()))
                return CreateMaterialState.MissingRequiredFields;
            if (string.IsNullOrWhiteSpace(Description?.Trim()) || string.IsNullOrWhiteSpace(Brand?.Trim()))
                return CreateMaterialState.MissingRequiredFields;
            return CreateMaterialState.ReadyToCreate;
        }

        // 1) 完全匹配：最高优先级，硬阻断
        if (HasExactSpecMatch)
            return CreateMaterialState.CandidateConflict;

        // 2) 规格描述/品牌必填：第二优先级，硬阻断
        if (string.IsNullOrWhiteSpace(Description?.Trim()) || string.IsNullOrWhiteSpace(Brand?.Trim()))
            return CreateMaterialState.MissingRequiredFields;

        // 3) 软冲突：存在候选且未允许 → 冲突态（需要“允许新建”）
        if (HasCandidates && !IsForceCreateAllowed)
            return CreateMaterialState.CandidateConflict;

        // 4) 其他：可创建（唯一正向态）
        return CreateMaterialState.ReadyToCreate;
    }

    private void RefreshDerivedState()
    {
        // 单一真相：任何 UI 行为只依赖 State + 派生 flags
        State = ComputeState();
        NotifyPropertyChanged(nameof(IsForceCreateAllowed));
        NotifyPropertyChanged(nameof(HasCandidates));
        NotifyPropertyChanged(nameof(ShowAllowCreatePanel));
        CreateCommand.RaiseCanExecuteChanged();
    }

    private string _result = "";
    public string Result { get => _result; set => SetProperty(ref _result, value); }

    private bool _isSubmitting;
    public bool IsSubmitting
    {
        get => _isSubmitting;
        set
        {
            if (SetProperty(ref _isSubmitting, value))
                CreateCommand.RaiseCanExecuteChanged();
        }
    }

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
    public RelayCommand<CandidateItemViewModel> UseCandidateAsReplacementCommand { get; }
    public RelayCommand<CandidateItemViewModel> SelectCandidateCommand { get; }
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

        CreateCommand = new RelayCommand(async () => await CreateWithConfirmAsync(), CanExecuteCreate);
        RefreshCategoriesCommand = new RelayCommand(async () => await RefreshCategoriesAsync());
        OpenAddCategoryCommand = new RelayCommand(async () => await OpenAddCategoryAsync());
        UseCandidateAsReplacementCommand = new RelayCommand<CandidateItemViewModel>(
            async hit =>
            {
                if (hit is not null)
                    await _navigateToReplacementFromCandidate(hit.Source);
            },
            hit => hit is not null);
        SelectCandidateCommand = new RelayCommand<CandidateItemViewModel>(
            hit =>
            {
                if (hit is not null)
                    SelectedCandidate = hit;
            },
            hit => hit is not null);
        ForceCreateWithConfirmCommand = new RelayCommand(
            () =>
            {
                // 仅允许新建：记录 AllowedKey（分类+SpecTrim），不触发创建
                if (State != CreateMaterialState.CandidateConflict)
                    return;
                if (HasExactSpecMatch)
                    return;
                var category = SelectedCategory?.Code?.Trim() ?? "";
                var specTrim = Spec?.Trim() ?? "";
                if (string.IsNullOrWhiteSpace(category) || string.IsNullOrWhiteSpace(specTrim))
                    return;
                _allowedKey = (category, specTrim);
                RefreshDerivedState();
            },
            () => DecisionState == CreateDecisionState.HasCandidate && !HasExactSpecMatch);

        UpdateSpecInputStateHint();
        UpdateDecisionPresentation();
        RefreshDerivedState();
        _ = RefreshCategoriesAsync();
    }

    public void NotifySpecFieldFocused() => KeywordSource = MaterialSearchKeywordSource.Spec;

    // 候选收敛：描述框 focus 不应影响候选区（不切换 keyword source、不触发刷新/清空）
    public void NotifyDescriptionFieldFocused()
    {
    }

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
        if (DecisionState == CreateDecisionState.Success)
            return false;
        if (CandidateLoading)
            return false;
        if (IsSubmitting)
            return false;
        return State == CreateMaterialState.ReadyToCreate;
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
            case CreateDecisionState.Success:
                CandidateStateIcon = "✔";
                CandidateStateMessage = "创建成功";
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

    private void ClearCandidatesAndDecisionUi()
    {
        CandidateItems.Clear();
        SelectedCandidate = null;
        ShowCandidateCardList = false;
        ShowDecisionInfoBar = false;
        CandidateLoading = false;
        CandidateStatus = "";
        UpdateDecisionPresentation();
        UseCandidateAsReplacementCommand.RaiseCanExecuteChanged();
        RefreshDerivedState();
    }

    private void ScheduleCandidateRefresh()
    {
        if (_suppressCandidateRefresh)
            return;
        if (DecisionState == CreateDecisionState.ForcedCreate)
            SetDecisionState(CreateDecisionState.Searching);

        PrimeSearchingStateIfApplicable();
        _debouncer.Debounce(CandidateDebounceKey, TimeSpan.FromMilliseconds(300), RefreshCandidatesCoreAsync);
    }

    private void ClearInputsAfterSuccess()
    {
        _suppressCandidateRefresh = true;
        try
        {
            // 清空输入，便于再次录入；保留分类选择以提升录入效率
            Spec = "";
            Description = "";
            Brand = "";
            _allowedKey = null;
        }
        finally
        {
            _suppressCandidateRefresh = false;
        }
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
            HasExactSpecMatch = false;
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
            HasExactSpecMatch = false;
            TrySetIdleIfBothInputsEmpty();
            CreateCommand.RaiseCanExecuteChanged();
            UpdateDecisionPresentation();
            return;
        }

        var keyword = Spec;
        if (string.IsNullOrWhiteSpace(keyword))
        {
            CandidateItems.Clear();
            SelectedCandidate = null;
            CandidateLoading = false;
            CandidateStatus = "";
            HasExactSpecMatch = false;
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
        HasExactSpecMatch = false;

        if (KeywordSource == MaterialSearchKeywordSource.None)
        {
            CandidateLoading = false;
            TrySetIdleIfBothInputsEmpty();
            CreateCommand.RaiseCanExecuteChanged();
            UpdateDecisionPresentation();
            return;
        }

        var keyword = Spec;
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
            var res = await _app.SearchCandidatesBySpecOnlyAsync(
                categoryCode: categoryCode,
                keyword: keyword.Trim(),
                limit: 20,
                ct: ct);

            if (!res.IsSuccess)
            {
                var plan = _uiRenderer.BuildRenderPlan(res.Error!, ContextType.CreateMaterialCandidates);
                _uiDispatcher.Apply(plan, this);
                TrySetIdleIfBothInputsEmpty();
                CreateCommand.RaiseCanExecuteChanged();
                return;
            }

            foreach (var x in res.Data!.Items)
                CandidateItems.Add(new CandidateItemViewModel(x, keyword));

            RecomputeHasExactSpecMatch();
            RefreshDerivedState();

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
            ForceCreateWithConfirmCommand.RaiseCanExecuteChanged();
            UpdateDecisionPresentation();
        }
    }

    private void RecomputeHasExactSpecMatch()
    {
        var spec = Spec?.Trim() ?? "";
        HasExactSpecMatch =
            !string.IsNullOrWhiteSpace(spec)
            && CandidateItems.Any(x =>
                string.Equals(x.Spec?.Trim(), spec, StringComparison.OrdinalIgnoreCase));
        ForceCreateWithConfirmCommand.RaiseCanExecuteChanged();
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

    private async Task CreateWithConfirmAsync()
    {
        // 仅 UI 确认：不改业务逻辑
        var model = new CreateMaterialConfirmModel
        {
            Spec = Spec?.Trim() ?? "",
            Description = Description?.Trim() ?? "",
            Name = SelectedCategory?.Name ?? "",
            Brand = Brand?.Trim() ?? ""
        };

        if (!_uiRenderer.ConfirmCreateMaterial(model))
            return;

        await CreateAsync();
    }

    private async Task CreateAsync()
    {
        ClearCreateMaterialSubmitFeedback();
        Result = UiResources.Get(UiResourceKeys.Info.CreateMaterialProcessing);
        IsSubmitting = true;
        var categoryCode = SelectedCategory?.Code?.Trim() ?? "";
        if (string.IsNullOrWhiteSpace(categoryCode))
        {
            Result = UiResources.Get(UiResourceKeys.Hint.SelectCategory);
            IsSubmitting = false;
            return;
        }

        var res = await _app.CreateMaterial(new CreateMaterialRequest(
            CategoryCode: categoryCode,
            Spec: Spec,
            Name: Name,
            Description: Description,
            Brand: string.IsNullOrWhiteSpace(Brand) ? null : Brand,
            CodeMode: CodeMode,
            ExistingCode: IsManualExistingCodeMode ? ExistingCode : null,
            ForceConfirm: false
        ));

        if (res.IsSuccess)
        {
            // warning + confirm (manual-only)
            if (res.Data!.RequiresConfirmation)
            {
                var ok = _uiRenderer.ConfirmWarning("需要确认", res.Data.Message ?? "需要确认后继续。");
                if (!ok)
                {
                    Result = "已取消。";
                    IsSubmitting = false;
                    return;
                }

                // second submit with force_confirm
                res = await _app.CreateMaterial(new CreateMaterialRequest(
                    CategoryCode: categoryCode,
                    Spec: Spec,
                    Name: Name,
                    Description: Description,
                    Brand: string.IsNullOrWhiteSpace(Brand) ? null : Brand,
                    CodeMode: CodeMode,
                    ExistingCode: IsManualExistingCodeMode ? ExistingCode : null,
                    ForceConfirm: true
                ));

                if (!res.IsSuccess)
                {
                    var plan2 = _uiRenderer.BuildRenderPlan(res.Error!, ContextType.CreateMaterialCreate);
                    _uiDispatcher.Apply(plan2, this);
                    UpdateSpecInputStateHint();
                    IsSubmitting = false;
                    return;
                }
            }

            Result = UiResources.Format(
                UiResourceKeys.Info.CreateMaterialCreateSuccess,
                res.Data!.Code,
                res.Data.SpecNormalized);
            // 创建成功后：禁止自动触发候选搜索；清空候选区并强制进入 SUCCESS 态
            KeywordSource = MaterialSearchKeywordSource.None;
            ClearCandidatesAndDecisionUi();
            SetDecisionState(CreateDecisionState.Success);
            ClearInputsAfterSuccess();
            ExistingCode = "";
            RefreshDerivedState();
            IsSubmitting = false;
            return;
        }

        var plan = _uiRenderer.BuildRenderPlan(res.Error!, ContextType.CreateMaterialCreate);
        _uiDispatcher.Apply(plan, this);
        UpdateSpecInputStateHint();
        IsSubmitting = false;
    }

    private void ClearCreateMaterialSubmitFeedback()
    {
        SpecFieldError = "";
        GlobalError = "";
        Result = "";
    }
}
