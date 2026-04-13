using MaterialCodingSystem.Application.Contracts;

namespace MaterialCodingSystem.Application.Interfaces;

public interface IExcelMaterialExporter
{
    /// <summary>PRD V1.3：Sheet1=全量；分类 Sheet=按 category_code 分 Sheet；列顺序见 <see cref="MaterialExportRow"/>。</summary>
    Task WriteAsync(string filePath, IReadOnlyList<MaterialExportRow> rows, CancellationToken ct = default);
}
