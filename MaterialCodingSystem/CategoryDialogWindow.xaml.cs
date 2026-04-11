using System.Windows;
using MaterialCodingSystem.Application;
using MaterialCodingSystem.Presentation.ViewModels;

namespace MaterialCodingSystem;

public partial class CategoryDialogWindow : Window
{
    public CategoryDialogWindow(MaterialApplicationService app)
    {
        InitializeComponent();
        DataContext = new CategoryDialogViewModel(
            app,
            () => { DialogResult = true; Close(); });
    }
}
