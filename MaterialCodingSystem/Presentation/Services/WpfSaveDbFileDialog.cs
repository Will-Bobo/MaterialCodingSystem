using System.IO;
using Microsoft.Win32;

namespace MaterialCodingSystem.Presentation.Services;

public sealed class WpfSaveDbFileDialog : IFileDbSaveDialog
{
    public string? ShowSaveDb(string? initialDirectory)
    {
        var dlg = new SaveFileDialog
        {
            Filter = "SQLite 数据库 (*.db)|*.db|所有文件 (*.*)|*.*",
            DefaultExt = ".db",
            AddExtension = true
        };

        if (!string.IsNullOrWhiteSpace(initialDirectory) && Directory.Exists(initialDirectory))
            dlg.InitialDirectory = initialDirectory;

        return dlg.ShowDialog() == true ? dlg.FileName : null;
    }
}

