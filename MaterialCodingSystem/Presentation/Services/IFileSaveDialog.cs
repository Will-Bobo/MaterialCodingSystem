namespace MaterialCodingSystem.Presentation.Services;

public interface IFileSaveDialog
{
    /// <summary>返回用户选择的 .xlsx 完整路径；取消则 null。</summary>
    string? ShowSaveXlsx(string? initialDirectory);
}
