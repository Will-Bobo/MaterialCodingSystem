using System.IO;
using Microsoft.Win32;

namespace MaterialCodingSystem.Presentation.Services;

public sealed class WpfOpenDbFileDialog : IFileOpenDialog
{
    public string? ShowOpenDb(string? initialDirectory)
    {
        var dlg = new OpenFileDialog
        {
            Filter = "SQLite 数据库 (*.db)|*.db|所有文件 (*.*)|*.*",
            DefaultExt = ".db",
            Multiselect = false
        };

        if (!string.IsNullOrWhiteSpace(initialDirectory) && Directory.Exists(initialDirectory))
            dlg.InitialDirectory = initialDirectory;

        return dlg.ShowDialog() == true ? dlg.FileName : null;
    }
}

