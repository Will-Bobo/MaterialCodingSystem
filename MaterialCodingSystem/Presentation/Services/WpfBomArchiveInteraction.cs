using System.Diagnostics;
using System.IO;
using Microsoft.Win32;
using MaterialCodingSystem.Application.Interfaces;
using MaterialCodingSystem.Presentation.Services;

namespace MaterialCodingSystem.Presentation.Services;

public sealed class WpfBomArchiveInteraction : IBomArchiveInteraction
{
    private readonly IUiDialogService _dialogs;

    public WpfBomArchiveInteraction(IUiDialogService dialogs)
    {
        _dialogs = dialogs;
    }

    public string? PickArchiveRootFolder(string? initialDirectory)
    {
        // WPF 下无内置 FolderPicker；使用 OpenFileDialog 的“选文件夹”技巧（ValidateNames=false）。
        var dialog = new OpenFileDialog
        {
            Title = "请选择 BOM 归档目录",
            CheckFileExists = false,
            CheckPathExists = true,
            ValidateNames = false,
            FileName = "选择此文件夹",
            InitialDirectory = string.IsNullOrWhiteSpace(initialDirectory) ? null : initialDirectory
        };

        var ok = dialog.ShowDialog() == true;
        if (!ok) return null;

        var folder = Path.GetDirectoryName(dialog.FileName);
        return string.IsNullOrWhiteSpace(folder) ? null : folder;
    }

    public bool ConfirmOverwrite(BomArchiveOverwritePrompt prompt)
    {
        var title = "确认覆盖保存";
        var body = $"该 BOM 版本已存在，是否覆盖保存？\n\n" +
                   $"成品编码：{prompt.FinishedCode}\n" +
                   $"版本号：{prompt.Version}\n\n" +
                   $"原保存时间：{prompt.ExistingCreatedAt}\n" +
                   $"原路径：{prompt.ExistingFilePath}";
        return _dialogs.Confirm(title, body);
    }

    public bool ShowSavedAndAskOpenFolder(BomArchiveSavedPrompt prompt)
    {
        var title = "已保存审核通过BOM";
        var body = $"已保存审核通过BOM。\n\n" +
                   $"成品编码：{prompt.FinishedCode}\n" +
                   $"版本号：{prompt.Version}\n" +
                   $"保存路径：{prompt.SavedFilePath}\n\n" +
                   $"是否打开文件夹？";
        return _dialogs.Confirm(title, body);
    }

    public void OpenFolder(string folderPath)
    {
        if (string.IsNullOrWhiteSpace(folderPath)) return;
        if (!Directory.Exists(folderPath)) return;
        Process.Start(new ProcessStartInfo
        {
            FileName = "explorer.exe",
            Arguments = $"\"{folderPath}\"",
            UseShellExecute = true
        });
    }
}

