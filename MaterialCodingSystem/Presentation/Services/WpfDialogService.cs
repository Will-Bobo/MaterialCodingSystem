using System.Windows;

namespace MaterialCodingSystem.Presentation.Services;

public sealed class WpfDialogService : IDialogService
{
    public void ShowWarning(string title, string message)
    {
        MessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Warning);
    }
}
