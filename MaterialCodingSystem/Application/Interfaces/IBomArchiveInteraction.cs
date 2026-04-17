namespace MaterialCodingSystem.Application.Interfaces;

public sealed record BomArchiveOverwritePrompt(
    string FinishedCode,
    string Version,
    string ExistingFilePath,
    string ExistingCreatedAt
);

public sealed record BomArchiveSavedPrompt(
    string FinishedCode,
    string Version,
    string SavedFilePath
);

public interface IBomArchiveInteraction
{
    /// <summary>让用户选择 BOM 归档根目录；返回 null 表示用户取消。</summary>
    string? PickArchiveRootFolder(string? initialDirectory);

    /// <summary>重复版本覆盖确认；false 表示取消覆盖。</summary>
    bool ConfirmOverwrite(BomArchiveOverwritePrompt prompt);

    /// <summary>保存成功提示；返回 true 表示用户希望打开文件夹。</summary>
    bool ShowSavedAndAskOpenFolder(BomArchiveSavedPrompt prompt);

    void OpenFolder(string folderPath);
}

