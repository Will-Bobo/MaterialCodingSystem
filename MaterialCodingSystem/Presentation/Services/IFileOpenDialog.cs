namespace MaterialCodingSystem.Presentation.Services;

public interface IFileOpenDialog
{
    /// <summary>返回用户选择的 .db 完整路径；取消则 null。</summary>
    string? ShowOpenDb(string? initialDirectory);
}

