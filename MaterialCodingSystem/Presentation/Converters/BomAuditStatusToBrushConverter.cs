using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;
using MaterialCodingSystem.Application.Contracts;

namespace MaterialCodingSystem.Presentation.Converters;

public sealed class BomAuditStatusToBrushConverter : IValueConverter
{
    private static readonly SolidColorBrush BrushGreen = new(Color.FromRgb(0x16, 0xA3, 0x4A)); // green-600
    private static readonly SolidColorBrush BrushRed = new(Color.FromRgb(0xDC, 0x26, 0x26));   // red-600
    private static readonly SolidColorBrush BrushBlue = new(Color.FromRgb(0x25, 0x63, 0xEB));  // blue-600

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is BomAuditStatus s)
        {
            return s switch
            {
                BomAuditStatus.PASS => BrushGreen,
                BomAuditStatus.ERROR => BrushRed,
                BomAuditStatus.NEW => BrushBlue,
                _ => Brushes.Gray
            };
        }
        return Brushes.Gray;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => Binding.DoNothing;
}

