using System.Windows;
using MaterialCodingSystem.Presentation.ViewModels;

namespace MaterialCodingSystem;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
    }

    private void SpecField_OnGotFocus(object sender, RoutedEventArgs e)
    {
        if (DataContext is MainViewModel m)
            m.CreateMaterial.NotifySpecFieldFocused();
    }

    private void DescriptionField_OnGotFocus(object sender, RoutedEventArgs e)
    {
        if (DataContext is MainViewModel m)
            m.CreateMaterial.NotifyDescriptionFieldFocused();
    }
}
