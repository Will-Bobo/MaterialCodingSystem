using System.IO;
using Microsoft.Win32;

namespace MaterialCodingSystem.Presentation.Services;

public sealed class WpfSaveExcelFileDialog : IFileSaveDialog
{
    public string? ShowSaveXlsx(string? initialDirectory)
    {
        var dlg = new SaveFileDialog
        {
            Filter = "Excel 工作簿 (*.xlsx)|*.xlsx",
            DefaultExt = ".xlsx",
            AddExtension = true
        };

        if (!string.IsNullOrWhiteSpace(initialDirectory) && Directory.Exists(initialDirectory))
            dlg.InitialDirectory = initialDirectory;

        return dlg.ShowDialog() == true ? dlg.FileName : null;
    }
}
