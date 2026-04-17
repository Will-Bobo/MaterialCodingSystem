using System.IO;
using Microsoft.Win32;

namespace MaterialCodingSystem.Presentation.Services;

public interface IBomExcelOpenFileDialog
{
    string? ShowOpenBomExcel(string? initialDirectory);
}

public sealed class WpfOpenBomExcelFileDialog : IBomExcelOpenFileDialog
{
    public string? ShowOpenBomExcel(string? initialDirectory)
    {
        var dlg = new OpenFileDialog
        {
            Filter = "BOM Excel (*.xls;*.xlsx)|*.xls;*.xlsx|所有文件 (*.*)|*.*",
            DefaultExt = ".xlsx",
            Multiselect = false
        };

        if (!string.IsNullOrWhiteSpace(initialDirectory) && Directory.Exists(initialDirectory))
            dlg.InitialDirectory = initialDirectory;

        return dlg.ShowDialog() == true ? dlg.FileName : null;
    }
}

