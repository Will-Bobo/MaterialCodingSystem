using System.Data;
using MaterialCodingSystem.Domain.Services.Models;

namespace MaterialCodingSystem.Infrastructure.Excel.Adapters;

public sealed class ExcelDataReaderBomGridAdapter
{
    public BomGrid ToGrid(DataTable table)
    {
        var rows = new List<BomGridRow>(capacity: table.Rows.Count);

        for (var r = 0; r < table.Rows.Count; r++)
        {
            var row = new List<string>(capacity: table.Columns.Count);
            for (var c = 0; c < table.Columns.Count; c++)
                row.Add(table.Rows[r][c]?.ToString() ?? "");
            rows.Add(new BomGridRow(r + 1, row.ToArray())); // 1-based row number
        }

        return new BomGrid(rows);
    }
}

