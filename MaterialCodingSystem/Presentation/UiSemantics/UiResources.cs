using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Markup;

namespace MaterialCodingSystem.Presentation.UiSemantics;

/// <summary>
/// Resolves UI strings from WPF <see cref="ResourceDictionary"/> (<c>Application.Current.TryFindResource</c> or test merge).
/// Templates and literals live only in XAML; C# supplies keys and optional format arguments for resource templates.
/// </summary>
public static class UiResources
{
    private static ResourceDictionary? _testRoot;

    /// <summary>Unit tests: merge one or more loose .xaml dictionaries (e.g. UiErrors + UiStrings copied to output).</summary>
    public static void LoadDictionariesForTests(params string[] absolutePaths)
    {
        var root = new ResourceDictionary();
        foreach (var p in absolutePaths)
        {
            if (!File.Exists(p))
                continue;
            using var stream = File.OpenRead(p);
            var d = (ResourceDictionary)XamlReader.Load(stream);
            root.MergedDictionaries.Add(d);
        }

        _testRoot = root.MergedDictionaries.Count > 0 ? root : null;
    }

    public static string Get(object resourceKey)
    {
        if (TryGetString(resourceKey, out var s))
            return s;
        return resourceKey?.ToString() ?? "";
    }

    public static string Format(object resourceKey, params object?[] args) =>
        string.Format(CultureInfo.CurrentCulture, Get(resourceKey), args ?? Array.Empty<object?>());

    public static bool TryGetString(object? resourceKey, out string value)
    {
        value = "";
        if (resourceKey is null)
            return false;

        if (_testRoot is not null && TryFindInTree(_testRoot, resourceKey, out value))
            return true;

        if (global::System.Windows.Application.Current?.TryFindResource(resourceKey) is string appS)
        {
            value = appS;
            return true;
        }

        return false;
    }

    private static bool TryFindInTree(ResourceDictionary root, object key, out string value)
    {
        if (root.Contains(key))
        {
            value = root[key] as string ?? "";
            return !string.IsNullOrEmpty(value) || root[key] is string;
        }

        foreach (var m in root.MergedDictionaries)
        {
            if (TryFindInTree(m, key, out value))
                return true;
        }

        value = "";
        return false;
    }
}

/// <summary>Canonical resource keys (match <c>x:Key</c> in UiErrors.xaml / UiStrings.xaml).</summary>
public static class UiResourceKeys
{
    public static class Error
    {
        public const string SpecDuplicate = "Error.SpecDuplicate";
        public const string CodeConflictRetry = "Error.CodeConflictRetry";
        public const string SuffixOverflow = "Error.SuffixOverflow";
        public const string SuffixSequenceBroken = "Error.SuffixSequenceBroken";
        public const string NotFound = "Error.NotFound";
        public const string ValidationError = "Error.ValidationError";
        public const string InvalidQuery = "Error.InvalidQuery";
        public const string CategoryCodeDuplicate = "Error.CategoryCodeDuplicate";
        public const string CategoryNameDuplicate = "Error.CategoryNameDuplicate";
        public const string InternalError = "Error.InternalError";
        public const string GenericFailure = "Error.GenericFailure";
        public const string CandidateLoadFailed = "Error.CandidateLoadFailed";
        public const string ListCategoriesFailed = "Error.ListCategoriesFailed";
        public const string ReplacementResolveFailed = "Error.Replacement.ResolveFailed";
        public const string ReplacementGroupInfoFailed = "Error.Replacement.GroupInfoFailed";
        public const string ReplacementEmbeddedSearchFailed = "Error.Replacement.EmbeddedSearchFailed";
        public const string ExportFileInUse = "Error.Export.FileInUse";
    }

    public static class Dialog
    {
        public const string TitleWarning = "Dialog.Title.Warning";
        public const string TitleConfirm = "Dialog.Title.Confirm";
    }

    public static class Confirm
    {
        public const string DuplicateTitle = "Confirm.DuplicateTitle";
        public const string DuplicateBody = "Confirm.DuplicateBody";
        public const string DeprecateTitle = "Confirm.DeprecateTitle";
        public const string DeprecateBody = "Confirm.DeprecateBody";
    }

    public static class Info
    {
        public const string CreateMaterialProcessing = "Info.CreateMaterial.Processing";
        public const string CreateMaterialCreateSuccess = "Info.CreateMaterial.CreateSuccess";
        public const string CreateMaterialCandidateSimilarityPlaceholder = "Info.CreateMaterial.CandidateSimilarityPlaceholder";
        public const string SpecStateDuplicate = "Info.SpecState.Duplicate";
        public const string SpecStatePending = "Info.SpecState.Pending";
        public const string SpecStateNormal = "Info.SpecState.Normal";
        public const string DecisionIdleMessage = "Info.Decision.IdleMessage";
        public const string DecisionSearchingMessage = "Info.Decision.SearchingMessage";
        public const string DecisionNoCandidateMessage = "Info.Decision.NoCandidateMessage";
        public const string DecisionHasCandidateMessage = "Info.Decision.HasCandidateMessage";
        public const string DecisionForcedCreateMessage = "Info.Decision.ForcedCreateMessage";
        public const string DecisionHighRiskMessage = "Info.Decision.HighRiskMessage";
        public const string CandidatePickCategoryAndKeyword = "Info.Candidate.PickCategoryAndKeyword";
        public const string CandidateSearching = "Info.Candidate.Searching";
        public const string CandidatePossibleDuplicateTop20 = "Info.Candidate.PossibleDuplicateTop20";
        public const string CandidateNoMatchCanCreate = "Info.Candidate.NoMatchCanCreate";
        public const string ReplacementProcessing = "Info.Replacement.Processing";
        public const string ReplacementLoadingGroupInfo = "Info.Replacement.LoadingGroupInfo";
        public const string ReplacementGroupInfoSummary = "Info.Replacement.GroupInfoSummary";
        public const string ReplacementGroupCodeDisplay = "Info.Replacement.GroupCodeDisplay";
        public const string ReplacementExistingSuffixNone = "Info.Replacement.ExistingSuffixNone";
        public const string ReplacementExistingSuffixList = "Info.Replacement.ExistingSuffixList";
        public const string ReplacementNextSuffixDisplay = "Info.Replacement.NextSuffixDisplay";
        public const string ReplacementCreateHint = "Info.Replacement.CreateHint";
        public const string ReplacementResolvedGroup = "Info.Replacement.ResolvedGroup";
        public const string ReplacementCreateSuccess = "Info.Replacement.CreateSuccess";
        public const string ReplacementEmbeddedSearchSearching = "Info.Replacement.EmbeddedSearchSearching";
        public const string ReplacementEmbeddedSearchEmpty = "Info.Replacement.EmbeddedSearchEmpty";
        public const string ReplacementEmbeddedSearchCount = "Info.Replacement.EmbeddedSearchCount";
        public const string SearchSearchingCode = "Info.Search.SearchingCode";
        public const string SearchSearchingSpec = "Info.Search.SearchingSpec";
        public const string SearchSelectSpecRowFirst = "Info.Search.SelectSpecRowFirst";
        public const string SearchMissingCodeForJump = "Info.Search.MissingCodeForJump";
        public const string SearchCodeDone = "Info.Search.CodeDone";
        public const string SearchSpecDone = "Info.Search.SpecDone";
        public const string DeprecateProcessing = "Info.Deprecate.Processing";
        public const string DeprecateDone = "Info.Deprecate.Done";
        public const string ExportProcessing = "Info.Export.Processing";
        public const string ExportCancelled = "Info.Export.Cancelled";
        public const string ExportSuccess = "Info.Export.Success";
    }

    public static class Hint
    {
        public const string SelectCategory = "Hint.SelectCategory";
    }
}
