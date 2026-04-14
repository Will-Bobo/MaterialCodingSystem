using MaterialCodingSystem.Presentation.UiSemantics;

namespace MaterialCodingSystem.Presentation.Services;

public interface IUiDialogService
{
    void ShowMessage(string title, string body, UiSeverity severity);
    bool Confirm(string title, string body);
}

