using ClosedXML.Excel;
using MaterialCodingSystem.Application.Contracts;
using MaterialCodingSystem.Application.Interfaces;

namespace MaterialCodingSystem.Infrastructure.Excel;

public sealed class ClosedXmlMaterialExcelExporter : IExcelMaterialExporter
{
    private const double RowHeight = 30d;
    private const int ColCount = 6;
    private const string ExportFontName = "宋体";

    public Task WriteAsync(string filePath, IReadOnlyList<MaterialExportRow> rows, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        using var wb = new XLWorkbook();

        // Sheet1：全量
        var all = wb.Worksheets.Add("电子总表");
        WriteHeader(all);
        var allLastRow = WriteRows(all, rows);
        ApplyTableStyle(all, allLastRow);

        // 分类 Sheet：每个分类一个 Sheet（含废弃）
        foreach (var group in rows.GroupBy(r => r.CategoryCode).OrderBy(g => g.Key, StringComparer.Ordinal))
        {
            var categoryName = group.FirstOrDefault()?.CategoryName ?? "";
            var sheetName = SanitizeSheetName($"{categoryName}（{group.Key}）");
            var ws = wb.Worksheets.Add(sheetName);
            WriteHeader(ws);
            var lastRow = WriteRows(ws, group);
            ApplyTableStyle(ws, lastRow);
        }

        // 无数据：仅保留 Sheet1 表头
        if (rows.Count == 0)
            AutoFit(all);
        else
            AutoFitAll(wb);

        wb.SaveAs(filePath);
        return Task.CompletedTask;
    }

    private static void WriteHeader(IXLWorksheet ws)
    {
        ws.Cell(1, 1).Value = "编码";
        ws.Cell(1, 2).Value = "名称";
        ws.Cell(1, 3).Value = "规格描述";
        ws.Cell(1, 4).Value = "规格号";
        ws.Cell(1, 5).Value = "品牌";
        ws.Cell(1, 6).Value = "状态";

        ws.Row(1).Height = RowHeight;
    }

    private static int WriteRows(IXLWorksheet ws, IEnumerable<MaterialExportRow> rows)
    {
        var ordered = rows
            .OrderByDescending(r => r.Status)
            .ThenBy(r => r.CategoryCode, StringComparer.Ordinal)
            .ThenBy(r => r.SerialNo)
            .ThenBy(r => r.Suffix, StringComparer.Ordinal)
            .ThenBy(r => r.Code, StringComparer.Ordinal);

        var ok = XLColor.FromHtml("#16A34A");
        var bad = XLColor.FromHtml("#DC2626");

        var row = 2;
        foreach (var item in ordered)
        {
            ws.Row(row).Height = RowHeight;
            var statusText = item.Status == 1 ? "正常" : "已废弃";
            ws.Cell(row, 1).Value = item.Code;
            ws.Cell(row, 2).Value = item.DisplayNameForUi;
            ws.Cell(row, 3).Value = item.Description;
            ws.Cell(row, 4).Value = item.Spec;
            ws.Cell(row, 5).Value = item.Brand ?? "";
            var statusCell = ws.Cell(row, 6);
            statusCell.Value = statusText;
            statusCell.Style.Font.FontColor = item.Status == 1 ? ok : bad;
            row++;
        }

        return row - 1; // last row index (>=1)
    }

    private static void ApplyTableStyle(IXLWorksheet ws, int lastRow)
    {
        if (lastRow < 1) lastRow = 1;
        var r = ws.Range(1, 1, lastRow, ColCount);

        // 字体统一：宋体
        r.Style.Font.FontName = ExportFontName;

        // 垂直居中
        r.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;

        // 黑色直线边框（含内外）
        r.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
        r.Style.Border.OutsideBorderColor = XLColor.Black;
        r.Style.Border.InsideBorder = XLBorderStyleValues.Thin;
        r.Style.Border.InsideBorderColor = XLColor.Black;
    }

    private static void AutoFitAll(XLWorkbook wb)
    {
        foreach (var ws in wb.Worksheets)
            AutoFit(ws);
    }

    private static void AutoFit(IXLWorksheet ws)
    {
        ws.Columns(1, 6).AdjustToContents();
        // 导出优化：名称 / 规格描述列可读（最小宽度兜底）
        if (ws.Column(2).Width < 25) ws.Column(2).Width = 25;
        if (ws.Column(3).Width < 20) ws.Column(3).Width = 20;
        // 状态列：避免“正常/已废弃”显示拥挤
        if (ws.Column(6).Width < 12) ws.Column(6).Width = 12;
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
