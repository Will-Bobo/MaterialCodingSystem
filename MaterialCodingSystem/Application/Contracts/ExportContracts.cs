namespace MaterialCodingSystem.Application.Contracts;

/// <summary>PRD 7.4 导出行（JOIN material_group 后排序用字段）。</summary>
public sealed record MaterialExportRow(
    string Code,
    string Spec,
    string Description,
    string? Brand,
    string CategoryCode,
    long SerialNo,
    string Suffix,
    long Status,
    string Name
);

public sealed record ExportMaterialsResponse(string FilePath, int RowCount, int SheetCount);
