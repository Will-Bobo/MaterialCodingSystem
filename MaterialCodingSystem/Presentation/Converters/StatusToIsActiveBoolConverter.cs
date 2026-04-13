using System.Globalization;
using System.Windows.Data;

namespace MaterialCodingSystem.Presentation.Converters;

public sealed class StatusToIsActiveBoolConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is null) return false;
        var s = value switch
        {
            int i => i,
            long l => (int)l,
            string str when int.TryParse(str, out var i2) => i2,
            _ => 1
        };
        return s != 0;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => Binding.DoNothing;
}

