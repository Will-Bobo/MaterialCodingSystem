namespace MaterialCodingSystem.Presentation.UiSemantics;

/// <summary>UI failure handling site; input to <see cref="UiPolicy"/> together with application <c>AppError</c>.</summary>
public enum ContextType
{
    CreateMaterialCreate,
    CreateMaterialListCategories,
    CreateMaterialCandidates,
    CreateReplacementCreate,
    CreateReplacementResolveGroup,
    CreateReplacementGroupInfo,
    CreateReplacementEmbeddedSearch,
    SearchByCode,
    SearchBySpec,
    SearchListCategories,
    CategoryDialogSave,
    Deprecate,
    Export
}
