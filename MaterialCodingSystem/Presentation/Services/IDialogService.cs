namespace MaterialCodingSystem.Presentation.Services;

public interface IDialogService
{
    void ShowWarning(string title, string message);

    /// <summary>确认仍要创建主料（可能重复）。返回 true 表示用户确认创建。</summary>
    bool ConfirmCreateDespitePossibleDuplicate();
}
