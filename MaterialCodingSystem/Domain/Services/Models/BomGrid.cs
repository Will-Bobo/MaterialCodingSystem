namespace MaterialCodingSystem.Domain.Services.Models;

public sealed record BomGrid(IReadOnlyList<BomGridRow> Rows)
{
    public int RowCount => Rows.Count;
    public int ColCount => Rows.Count == 0 ? 0 : Rows.Max(r => r.Cells.Length);

    public string GetCell(int rowIndex, int colIndex)
    {
        if (rowIndex < 0 || rowIndex >= Rows.Count) return "";
        var row = Rows[rowIndex];
        if (colIndex < 0 || colIndex >= row.Cells.Length) return "";
        return row.Cells[colIndex] ?? "";
    }

    public int GetRowIndex(int rowIndex)
        => rowIndex < 0 || rowIndex >= Rows.Count ? rowIndex + 1 : Rows[rowIndex].RowIndex;
}

public sealed record BomGridRow(int RowIndex, string[] Cells);

