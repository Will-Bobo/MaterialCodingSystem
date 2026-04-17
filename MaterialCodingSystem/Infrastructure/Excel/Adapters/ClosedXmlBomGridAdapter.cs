using ClosedXML.Excel;
using MaterialCodingSystem.Domain.Services.Models;

namespace MaterialCodingSystem.Infrastructure.Excel.Adapters;

public sealed class ClosedXmlBomGridAdapter
{
    public BomGrid ToGrid(IXLWorksheet ws)
    {
        var used = ws.RangeUsed();
        if (used is null)
            return new BomGrid(Array.Empty<BomGridRow>());

        var firstRow = used.FirstRow().RowNumber();
        var lastRow = used.LastRow().RowNumber();
        var firstCol = used.FirstColumn().ColumnNumber();
        var lastCol = used.LastColumn().ColumnNumber();

        var rows = new List<BomGridRow>(capacity: Math.Max(0, lastRow - firstRow + 1));

        for (var r = firstRow; r <= lastRow; r++)
        {
            var row = new List<string>(capacity: Math.Max(0, lastCol - firstCol + 1));
            for (var c = firstCol; c <= lastCol; c++)
            {
                row.Add(ws.Cell(r, c).GetString());
            }
            rows.Add(new BomGridRow(r, row.ToArray()));
        }

        return new BomGrid(rows);
    }
}

