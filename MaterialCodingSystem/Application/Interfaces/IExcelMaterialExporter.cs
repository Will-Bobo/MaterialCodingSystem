using MaterialCodingSystem.Application.Contracts;

namespace MaterialCodingSystem.Application.Interfaces;

public interface IExcelMaterialExporter
{
    /// <summary>Sheet1=全量；分类 Sheet=按 category_code 分 Sheet；导出列：编码、名称、规格描述、规格号、品牌、状态（<see cref="MaterialExportRow"/> 含排序/分组用字段）。</summary>
    Task WriteAsync(string filePath, IReadOnlyList<MaterialExportRow> rows, CancellationToken ct = default);
}
