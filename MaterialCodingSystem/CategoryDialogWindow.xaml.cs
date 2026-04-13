using System.Windows;
using MaterialCodingSystem.Application;
using MaterialCodingSystem.Presentation.UiSemantics;
using MaterialCodingSystem.Presentation.ViewModels;

namespace MaterialCodingSystem;

public partial class CategoryDialogWindow : Window
{
    public CategoryDialogWindow(MaterialApplicationService app, IUiRenderer uiRenderer, IUiDispatcher uiDispatcher)
    {
        InitializeComponent();
        DataContext = new CategoryDialogViewModel(
            app,
            uiRenderer,
            uiDispatcher,
            () => { DialogResult = true; Close(); });
    }
}
