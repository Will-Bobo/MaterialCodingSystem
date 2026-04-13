using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace MaterialCodingSystem.Presentation.Converters;

public sealed class StatusToBrushConverter : IValueConverter
{
    private static readonly Brush BrushOk = new SolidColorBrush(Color.FromRgb(0x16, 0xA3, 0x4A));   // #16A34A
    private static readonly Brush BrushBad = new SolidColorBrush(Color.FromRgb(0xDC, 0x26, 0x26));  // #DC2626

    static StatusToBrushConverter()
    {
        BrushOk.Freeze();
        BrushBad.Freeze();
    }

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is null) return BrushOk;
        var s = value switch
        {
            int i => i,
            long l => (int)l,
            string str when int.TryParse(str, out var i2) => i2,
            _ => 1
        };
        return s == 0 ? BrushBad : BrushOk;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => Binding.DoNothing;
}

