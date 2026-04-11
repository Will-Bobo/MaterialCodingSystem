using MaterialCodingSystem.Application.Contracts;

namespace MaterialCodingSystem.Application.Interfaces;

public interface IExcelMaterialExporter
{
    /// <summary>按 category_code 分 Sheet，列顺序：编码|名称|规格描述|规格号|品牌。</summary>
    Task WriteAsync(string filePath, IReadOnlyList<MaterialExportRow> rows, CancellationToken ct = default);
}
