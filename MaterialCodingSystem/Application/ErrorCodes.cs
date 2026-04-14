namespace MaterialCodingSystem.Application;

public static class ErrorCodes
{
    public const string SPEC_DUPLICATE = "SPEC_DUPLICATE";
    public const string SUFFIX_OVERFLOW = "SUFFIX_OVERFLOW";
    public const string SUFFIX_SEQUENCE_BROKEN = "SUFFIX_SEQUENCE_BROKEN";
    public const string SUFFIX_ALLOCATION_FAILED = "SUFFIX_ALLOCATION_FAILED";
    public const string CODE_CONFLICT_RETRY = "CODE_CONFLICT_RETRY";
    public const string NOT_FOUND = "NOT_FOUND";
    public const string INVALID_QUERY = "INVALID_QUERY";
    public const string VALIDATION_ERROR = "VALIDATION_ERROR";
    public const string CATEGORY_NOT_FOUND = "CATEGORY_NOT_FOUND";
    public const string ANCHOR_ITEM_DEPRECATED = "ANCHOR_ITEM_DEPRECATED";
    public const string CATEGORY_CODE_DUPLICATE = "CATEGORY_CODE_DUPLICATE";
    public const string CATEGORY_NAME_DUPLICATE = "CATEGORY_NAME_DUPLICATE";
    public const string INTERNAL_ERROR = "INTERNAL_ERROR";
    /// <summary>导出目标文件被占用，无法覆盖（例如 Excel 正打开该文件）。</summary>
    public const string EXPORT_FILE_IN_USE = "EXPORT_FILE_IN_USE";
}

