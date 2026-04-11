using ClosedXML.Excel;
using MaterialCodingSystem.Application.Contracts;
using MaterialCodingSystem.Application.Interfaces;

namespace MaterialCodingSystem.Infrastructure.Excel;

public sealed class ClosedXmlMaterialExcelExporter : IExcelMaterialExporter
{
    public Task WriteAsync(string filePath, IReadOnlyList<MaterialExportRow> rows, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        using var wb = new XLWorkbook();

        foreach (var group in rows.GroupBy(r => r.CategoryCode).OrderBy(g => g.Key))
        {
            var sheetName = SanitizeSheetName(group.Key);
            var ws = wb.Worksheets.Add(sheetName);
            ws.Cell(1, 1).Value = "编码";
            ws.Cell(1, 2).Value = "名称";
            ws.Cell(1, 3).Value = "规格描述";
            ws.Cell(1, 4).Value = "规格号";
            ws.Cell(1, 5).Value = "品牌";
            var ordered = group
                .OrderBy(r => r.SerialNo)
                .ThenBy(r => r.Suffix, StringComparer.Ordinal);
            var row = 2;
            foreach (var item in ordered)
            {
                ws.Cell(row, 1).Value = item.Code;
                ws.Cell(row, 2).Value = item.Name;
                ws.Cell(row, 3).Value = item.Description;
                ws.Cell(row, 4).Value = item.Spec;
                ws.Cell(row, 5).Value = item.Brand ?? "";
                row++;
            }
        }

        if (!wb.Worksheets.Any())
        {
            var ws = wb.Worksheets.Add("Empty");
            ws.Cell(1, 1).Value = "编码";
            ws.Cell(1, 2).Value = "名称";
            ws.Cell(1, 3).Value = "规格描述";
            ws.Cell(1, 4).Value = "规格号";
            ws.Cell(1, 5).Value = "品牌";
        }

        wb.SaveAs(filePath);
        return Task.CompletedTask;
    }

    private static string SanitizeSheetName(string name)
    {
        var invalid = new[] { '\\', '/', '*', '?', ':', '[', ']' };
        var s = string.Concat(name.Select(c => invalid.Contains(c) ? '_' : c));
        if (s.Length > 31)
            s = s[..31];
        return string.IsNullOrWhiteSpace(s) ? "Sheet" : s;
    }
}
