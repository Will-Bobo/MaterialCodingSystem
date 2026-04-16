using System.Windows;
using MaterialCodingSystem.Presentation.Models;

namespace MaterialCodingSystem;

public partial class CreateMaterialConfirmWindow : Window
{
    public CreateMaterialConfirmWindow(CreateMaterialConfirmModel model)
    {
        InitializeComponent();
        DataContext = model;
    }

    private void Confirm_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = true;
        Close();
    }
}

