namespace MaterialCodingSystem.Application.Contracts;

/// <summary>导出行：仓储 JOIN category；Excel 仅输出 Code/Name/Description/Spec/Brand/Status，CategoryCode/SerialNo/Suffix 用于排序与分 Sheet。</summary>
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
