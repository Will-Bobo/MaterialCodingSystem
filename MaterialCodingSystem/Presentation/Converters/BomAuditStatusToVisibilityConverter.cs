using System.Globalization;
using System.Windows;
using System.Windows.Data;
using MaterialCodingSystem.Application.Contracts;

namespace MaterialCodingSystem.Presentation.Converters;

/// <summary>Visible when status is NEW; otherwise Collapsed.</summary>
public sealed class BomAuditStatusToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is BomAuditStatus s && s == BomAuditStatus.NEW)
            return Visibility.Visible;
        return Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => Binding.DoNothing;
}

