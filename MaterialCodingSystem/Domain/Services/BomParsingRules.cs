using MaterialCodingSystem.Domain.Services.Models;

namespace MaterialCodingSystem.Domain.Services;

/// <summary>
/// BOM 解析规则（冻结）：纯规则，不依赖 Excel API / IO。
/// </summary>
public static class BomParsingRules
{
    public static readonly string[] HeaderFinishedCodeKeys = { "成品编码" };
    public static readonly string[] HeaderVersionKeys = { "PCBA版本号", "PCBA版本", "PCBA" };
    public static readonly string[] DetailHeaderNames = { "编码", "名称", "描述", "规格", "品牌" };

    public static BomParsingResult Parse(BomGrid grid)
    {
        var finishedCode = FindHeaderValue(grid, HeaderFinishedCodeKeys);
        var version = FindHeaderValue(grid, HeaderVersionKeys);
        if (IsBlank(finishedCode) || IsBlank(version))
            return BomParsingResult.Fail(new BomParsingFailure(BomParsingFailureKind.HeaderMissing, null));

        if (!TryFindDetailHeaderRow(grid, out var headerRowIndex, out var colMap))
            return BomParsingResult.Fail(new BomParsingFailure(BomParsingFailureKind.DetailHeaderRowNotFound, null));

        foreach (var required in DetailHeaderNames)
        {
            if (!colMap.ContainsKey(required))
                return BomParsingResult.Fail(new BomParsingFailure(BomParsingFailureKind.MissingColumn, required));
        }

        var rows = new List<BomParsedRowValue>();
        for (var r = headerRowIndex + 1; r < grid.RowCount; r++)
        {
            var excelRowNo = grid.GetRowIndex(r);

            var code = Normalize(grid.GetCell(r, colMap["编码"]));
            var name = Normalize(grid.GetCell(r, colMap["名称"]));
            var desc = Normalize(grid.GetCell(r, colMap["描述"]));
            var spec = Normalize(grid.GetCell(r, colMap["规格"]));
            var brand = Normalize(grid.GetCell(r, colMap["品牌"]));

            if (IsSkippableEmptyDetailRow(code, name, desc, spec, brand))
                continue;

            rows.Add(new BomParsedRowValue(excelRowNo, code, name, spec, desc, brand));
        }

        return BomParsingResult.Ok(new BomParsingSuccess(
            FinishedCode: Normalize(finishedCode),
            Version: Normalize(version),
            Rows: rows));
    }

    public static string FindHeaderValue(BomGrid grid, IReadOnlyList<string> keys)
    {
        for (var r = 0; r < grid.RowCount; r++)
        {
            for (var c = 0; c < grid.ColCount; c++)
            {
                var text = Normalize(grid.GetCell(r, c));
                if (IsBlank(text)) continue;
                // Case 1：单元格内容 = key（原行为）
                var exactKey = keys.FirstOrDefault(k => string.Equals(text, k, StringComparison.OrdinalIgnoreCase));
                if (!IsBlank(exactKey))
                {
                    // prefer right cell, else below
                    var right = Normalize(grid.GetCell(r, c + 1));
                    if (!IsBlank(right)) return right;

                    var down = Normalize(grid.GetCell(r + 1, c));
                    if (!IsBlank(down)) return down;

                    continue;
                }

                // Case 2：同格 "key:value" / "key：value"（新增，兼容模板 BOM.xls，不影响原 key 结构）
                foreach (var key in keys)
                {
                    if (TryParseInlineKeyValue(text, key, out var inlineVal) && !IsBlank(inlineVal))
                        return inlineVal;
                }
            }
        }

        return "";
    }

    private static bool TryParseInlineKeyValue(string cellText, string key, out string value)
    {
        value = "";
        if (IsBlank(cellText) || IsBlank(key))
            return false;

        var searchFrom = 0;
        while (searchFrom < cellText.Length)
        {
            var pos = cellText.IndexOf(key, searchFrom, StringComparison.OrdinalIgnoreCase);
            if (pos < 0) return false;

            // 需要边界：key 前面是行首或空白，避免误匹配到更长单词的一部分
            if (pos > 0 && !char.IsWhiteSpace(cellText[pos - 1]))
            {
                searchFrom = pos + key.Length;
                continue;
            }

            var i = pos + key.Length;
            while (i < cellText.Length && char.IsWhiteSpace(cellText[i])) i++;
            if (i >= cellText.Length || (cellText[i] != ':' && cellText[i] != '：'))
            {
                searchFrom = pos + key.Length;
                continue;
            }

            i++; // skip colon
            while (i < cellText.Length && char.IsWhiteSpace(cellText[i])) i++;
            if (i >= cellText.Length) return false;

            var start = i;
            while (i < cellText.Length && !char.IsWhiteSpace(cellText[i])) i++;
            value = cellText[start..i].Trim();
            return value.Length > 0;
        }

        return false;
    }

    public static bool TryFindDetailHeaderRow(BomGrid grid, out int headerRowIndex, out Dictionary<string, int> colMap)
    {
        headerRowIndex = -1;
        colMap = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        for (var r = 0; r < grid.RowCount; r++)
        {
            var local = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            for (var c = 0; c < grid.ColCount; c++)
            {
                var t = Normalize(grid.GetCell(r, c));
                if (IsBlank(t)) continue;
                if (DetailHeaderNames.Contains(t))
                    local[t] = c;
            }

            if (local.ContainsKey("编码") && local.ContainsKey("规格"))
            {
                headerRowIndex = r;
                colMap = local;
                return true;
            }
        }

        return false;
    }

    public static bool IsSkippableEmptyDetailRow(string code, string name, string desc, string spec, string brand)
        => IsBlank(code) && IsBlank(name) && IsBlank(desc) && IsBlank(spec) && IsBlank(brand);

    public static string Normalize(string? text) => (text ?? "").Trim();

    public static bool IsBlank(string? text) => string.IsNullOrWhiteSpace(text);
}

