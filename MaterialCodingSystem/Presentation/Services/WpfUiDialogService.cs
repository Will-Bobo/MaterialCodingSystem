using System.Windows;
using MaterialCodingSystem.Presentation.UiSemantics;

namespace MaterialCodingSystem.Presentation.Services;

public sealed class WpfUiDialogService : IUiDialogService
{
    public void ShowMessage(string title, string body, UiSeverity severity)
        => MessageBox.Show(body, title, MessageBoxButton.OK, MapIcon(severity));

    public bool Confirm(string title, string body)
        => MessageBox.Show(body, title, MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes;

    private static MessageBoxImage MapIcon(UiSeverity s) =>
        s switch
        {
            UiSeverity.Error => MessageBoxImage.Error,
            UiSeverity.Warning => MessageBoxImage.Warning,
            _ => MessageBoxImage.Information
        };
}

