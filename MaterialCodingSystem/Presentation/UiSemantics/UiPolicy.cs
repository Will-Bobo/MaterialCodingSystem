using MaterialCodingSystem.Application;
using MaterialCodingSystem.Application.Contracts;

namespace MaterialCodingSystem.Presentation.UiSemantics;

/// <summary>Single decision source: (AppError, ContextType) → <see cref="UiRenderPlan"/> (resource keys only; no localized text).</summary>
public static class UiPolicy
{
    public static UiRenderPlan Resolve(AppError error, ContextType context) =>
        (error.Code, context) switch
        {
            (ErrorCodes.SPEC_DUPLICATE, ContextType.CreateMaterialCreate) => Plan(
                error.Code, context, UiSeverity.Warning, UiPresentation.Inline,
                UiResourceKeys.Error.SpecDuplicate, UiBindings.SpecFieldError),

            (ErrorCodes.SPEC_DUPLICATE, ContextType.CreateReplacementCreate) => Plan(
                error.Code, context, UiSeverity.Warning, UiPresentation.Inline,
                UiResourceKeys.Error.SpecDuplicate, UiBindings.SpecFieldError),

            (ErrorCodes.CODE_CONFLICT_RETRY, ContextType.CreateMaterialCreate) => Plan(
                error.Code, context, UiSeverity.Warning, UiPresentation.Banner,
                UiResourceKeys.Error.CodeConflictRetry, UiBindings.GlobalError),

            (ErrorCodes.CODE_CONFLICT_RETRY, ContextType.CreateReplacementCreate) => Plan(
                error.Code, context, UiSeverity.Warning, UiPresentation.Banner,
                UiResourceKeys.Error.CodeConflictRetry, UiBindings.Result),

            (ErrorCodes.SUFFIX_OVERFLOW, ContextType.CreateReplacementCreate) => Plan(
                error.Code, context, UiSeverity.Error, UiPresentation.Dialog,
                UiResourceKeys.Error.SuffixOverflow, null),

            (ErrorCodes.SUFFIX_SEQUENCE_BROKEN, ContextType.CreateReplacementCreate) => Plan(
                error.Code, context, UiSeverity.Error, UiPresentation.Dialog,
                UiResourceKeys.Error.SuffixSequenceBroken, null),

            (ErrorCodes.SUFFIX_OVERFLOW, ContextType.CreateReplacementGroupInfo) => Plan(
                error.Code, context, UiSeverity.Error, UiPresentation.Dialog,
                UiResourceKeys.Error.SuffixOverflow, null),

            (ErrorCodes.SUFFIX_SEQUENCE_BROKEN, ContextType.CreateReplacementGroupInfo) => Plan(
                error.Code, context, UiSeverity.Error, UiPresentation.Dialog,
                UiResourceKeys.Error.SuffixSequenceBroken, null),

            (ErrorCodes.NOT_FOUND, ContextType.CreateReplacementResolveGroup) => Plan(
                error.Code, context, UiSeverity.Warning, UiPresentation.Banner,
                UiResourceKeys.Error.ReplacementResolveFailed, UiBindings.Result),

            (ErrorCodes.NOT_FOUND, ContextType.CreateReplacementGroupInfo) => Plan(
                error.Code, context, UiSeverity.Warning, UiPresentation.Banner,
                UiResourceKeys.Error.ReplacementGroupInfoFailed, UiBindings.GroupInfo),

            (ErrorCodes.VALIDATION_ERROR, ContextType.CreateReplacementResolveGroup) => Plan(
                error.Code, context, UiSeverity.Warning, UiPresentation.Banner,
                UiResourceKeys.Error.ValidationError, UiBindings.Result),

            (ErrorCodes.VALIDATION_ERROR, ContextType.CreateReplacementGroupInfo) => Plan(
                error.Code, context, UiSeverity.Warning, UiPresentation.Banner,
                UiResourceKeys.Error.ValidationError, UiBindings.GroupInfo),

            (ErrorCodes.CATEGORY_CODE_DUPLICATE, ContextType.CategoryDialogSave) => Plan(
                error.Code, context, UiSeverity.Warning, UiPresentation.Inline,
                UiResourceKeys.Error.CategoryCodeDuplicate, UiBindings.Error),

            (ErrorCodes.CATEGORY_NAME_DUPLICATE, ContextType.CategoryDialogSave) => Plan(
                error.Code, context, UiSeverity.Warning, UiPresentation.Inline,
                UiResourceKeys.Error.CategoryNameDuplicate, UiBindings.Error),

            (ErrorCodes.VALIDATION_ERROR, ContextType.CategoryDialogSave) => Plan(
                error.Code, context, UiSeverity.Warning, UiPresentation.Inline,
                UiResourceKeys.Error.ValidationError, UiBindings.Error),

            (ErrorCodes.INTERNAL_ERROR, _) => Plan(
                error.Code, context, UiSeverity.Error, UiPresentation.Banner,
                UiResourceKeys.Error.InternalError, DefaultBindProperty(context)),

            (ErrorCodes.INVALID_QUERY, ContextType.SearchByCode) => Plan(
                error.Code, context, UiSeverity.Warning, UiPresentation.Banner,
                UiResourceKeys.Error.InvalidQuery, UiBindings.Message),

            (ErrorCodes.INVALID_QUERY, ContextType.SearchBySpec) => Plan(
                error.Code, context, UiSeverity.Warning, UiPresentation.Banner,
                UiResourceKeys.Error.InvalidQuery, UiBindings.Message),

            (ErrorCodes.VALIDATION_ERROR, ContextType.SearchByCode) => Plan(
                error.Code, context, UiSeverity.Warning, UiPresentation.Banner,
                UiResourceKeys.Error.ValidationError, UiBindings.Message),

            (ErrorCodes.VALIDATION_ERROR, ContextType.SearchBySpec) => Plan(
                error.Code, context, UiSeverity.Warning, UiPresentation.Banner,
                UiResourceKeys.Error.ValidationError, UiBindings.Message),

            (ErrorCodes.VALIDATION_ERROR, ContextType.CreateReplacementEmbeddedSearch) => Plan(
                error.Code, context, UiSeverity.Warning, UiPresentation.Banner,
                UiResourceKeys.Error.ReplacementEmbeddedSearchFailed, UiBindings.CodeSearchStatus),

            (ErrorCodes.INVALID_QUERY, ContextType.CreateReplacementEmbeddedSearch) => Plan(
                error.Code, context, UiSeverity.Warning, UiPresentation.Banner,
                UiResourceKeys.Error.InvalidQuery, UiBindings.CodeSearchStatus),

            (_, ContextType.CreateMaterialListCategories) => Plan(
                error.Code, context, UiSeverity.Error, UiPresentation.Banner,
                UiResourceKeys.Error.ListCategoriesFailed, UiBindings.Result),

            (_, ContextType.SearchListCategories) => Plan(
                error.Code, context, UiSeverity.Error, UiPresentation.Banner,
                UiResourceKeys.Error.ListCategoriesFailed, UiBindings.Message),

            (_, ContextType.CreateMaterialCandidates) => Plan(
                error.Code, context, UiSeverity.Error, UiPresentation.Banner,
                UiResourceKeys.Error.CandidateLoadFailed, UiBindings.CandidateStatus),

            (_, ContextType.CreateReplacementEmbeddedSearch) => Plan(
                error.Code, context, UiSeverity.Error, UiPresentation.Banner,
                UiResourceKeys.Error.ReplacementEmbeddedSearchFailed, UiBindings.CodeSearchStatus),

            (_, ContextType.CategoryDialogSave) => Plan(
                error.Code, context, UiSeverity.Error, UiPresentation.Inline,
                MapGenericKey(error.Code), UiBindings.Error),

            (_, ContextType.Export) => Plan(
                error.Code, context, UiSeverity.Error, UiPresentation.Banner,
                MapGenericKey(error.Code), UiBindings.Result),

            (_, ContextType.Deprecate) => Plan(
                error.Code, context, UiSeverity.Error, UiPresentation.Banner,
                MapGenericKey(error.Code), UiBindings.Result),

            (_, ContextType.CreateMaterialCreate) => Plan(
                error.Code, context, UiSeverity.Error, UiPresentation.Banner,
                MapGenericKey(error.Code), UiBindings.Result),

            (_, ContextType.CreateReplacementCreate) => Plan(
                error.Code, context, UiSeverity.Error, UiPresentation.Banner,
                MapGenericKey(error.Code), UiBindings.Result),

            (_, ContextType.CreateReplacementResolveGroup) => Plan(
                error.Code, context, UiSeverity.Error, UiPresentation.Banner,
                MapGenericKey(error.Code), UiBindings.Result),

            _ => Plan(
                error.Code, context, UiSeverity.Error, UiPresentation.Banner,
                MapGenericKey(error.Code), DefaultBindProperty(context))
        };

    private static UiRenderPlan Plan(
        string errorCode,
        ContextType context,
        UiSeverity severity,
        UiPresentation presentation,
        string textResourceKey,
        string? bindPropertyName)
    {
        var targets = bindPropertyName is null
            ? Array.Empty<string>()
            : new[] { bindPropertyName };
        var clear = ClearStrategyForContext(context);
        UiModalPlan? modal = presentation == UiPresentation.Dialog
            ? new UiModalPlan(textResourceKey, UiResourceKeys.Dialog.TitleWarning, severity)
            : null;
        return new UiRenderPlan(errorCode, presentation, severity, targets, textResourceKey, clear, modal);
    }

    private static UiClearStrategy ClearStrategyForContext(ContextType ctx) =>
        ctx switch
        {
            ContextType.CreateMaterialCreate => UiClearStrategy.CreateMaterialSubmit,
            ContextType.CreateMaterialListCategories => UiClearStrategy.CreateMaterialResultOnly,
            ContextType.CreateMaterialCandidates => UiClearStrategy.CreateMaterialCandidateStatus,
            ContextType.CreateReplacementCreate => UiClearStrategy.CreateReplacementSubmit,
            ContextType.CreateReplacementResolveGroup => UiClearStrategy.CreateReplacementResultOnly,
            ContextType.CreateReplacementGroupInfo => UiClearStrategy.CreateReplacementGroupInfo,
            ContextType.CreateReplacementEmbeddedSearch => UiClearStrategy.CreateReplacementEmbeddedStatus,
            ContextType.SearchByCode or ContextType.SearchBySpec or ContextType.SearchListCategories =>
                UiClearStrategy.SearchMessage,
            ContextType.CategoryDialogSave => UiClearStrategy.CategoryDialogError,
            ContextType.Deprecate => UiClearStrategy.DeprecateResult,
            ContextType.Export => UiClearStrategy.ExportResult,
            _ => UiClearStrategy.None
        };

    private static string MapGenericKey(string code) =>
        code switch
        {
            ErrorCodes.VALIDATION_ERROR => UiResourceKeys.Error.ValidationError,
            ErrorCodes.NOT_FOUND => UiResourceKeys.Error.NotFound,
            ErrorCodes.INVALID_QUERY => UiResourceKeys.Error.InvalidQuery,
            ErrorCodes.INTERNAL_ERROR => UiResourceKeys.Error.InternalError,
            _ => UiResourceKeys.Error.GenericFailure
        };

    private static string? DefaultBindProperty(ContextType ctx) =>
        ctx switch
        {
            ContextType.SearchByCode or ContextType.SearchBySpec or ContextType.SearchListCategories => UiBindings.Message,
            ContextType.CategoryDialogSave => UiBindings.Error,
            ContextType.CreateReplacementGroupInfo => UiBindings.GroupInfo,
            _ => UiBindings.Result
        };
}
