using System.Windows;

namespace MaterialCodingSystem.Presentation.Services;

public sealed class WpfDialogService : IDialogService
{
    public void ShowWarning(string title, string message)
    {
        MessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Warning);
    }

    public bool ConfirmCreateDespitePossibleDuplicate()
    {
        var r = MessageBox.Show(
            "检测到可能重复物料，仍然要创建新主料吗？\n\n「是」= 确认创建\n「否」= 返回查看候选",
            "确认",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);
        return r == MessageBoxResult.Yes;
    }
}
