namespace MaterialCodingSystem.Application.Contracts;

/// <summary>PRD 7.4 导出行（JOIN material_group 后排序用字段）。</summary>
public sealed record MaterialExportRow(
    string Code,
    string Name,
    string Description,
    string Spec,
    string? Brand,
    string CategoryCode,
    long SerialNo,
    string Suffix
);

public sealed record ExportMaterialsResponse(string FilePath, int RowCount, int SheetCount);
