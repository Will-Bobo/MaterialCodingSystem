namespace MaterialCodingSystem.Presentation.UiSemantics;

/// <summary>How the user should perceive the feedback channel.</summary>
public enum UiPresentation
{
    Inline,
    Toast,
    Dialog,
    Banner
}

public enum UiSeverity
{
    Info,
    Warning,
    Error
}

/// <summary>Which bindable feedback slots to clear on the host before applying resolved text for <see cref="UiRenderPlan.TextResourceKey"/>.</summary>
public enum UiClearStrategy
{
    None,
    CreateMaterialSubmit,
    CreateMaterialResultOnly,
    CreateMaterialCandidateStatus,
    CreateReplacementSubmit,
    CreateReplacementResultOnly,
    CreateReplacementGroupInfo,
    CreateReplacementEmbeddedStatus,
    SearchMessage,
    CategoryDialogError,
    DeprecateResult,
    ExportResult
}

/// <summary>Optional modal; only when <see cref="UiRenderPlan.Presentation"/> is <see cref="UiPresentation.Dialog"/>. Keys resolve via WPF ResourceDictionary.</summary>
public sealed record UiModalPlan(string BodyResourceKey, string TitleResourceKey, UiSeverity Severity);

/// <summary>Single UI failure pipeline output: resource keys + presentation metadata (no localized text in-policy).</summary>
public sealed record UiRenderPlan(
    string ErrorCode,
    UiPresentation Presentation,
    UiSeverity Severity,
    IReadOnlyList<string> TargetBindings,
    /// <summary>Resource key for text written to each <see cref="TargetBindings"/> property; empty when dialog-only.</summary>
    string TextResourceKey,
    UiClearStrategy ClearSlots,
    UiModalPlan? Modal);
