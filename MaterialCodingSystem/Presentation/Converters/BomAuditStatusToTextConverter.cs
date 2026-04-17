using System.Globalization;
using System.Windows.Data;
using MaterialCodingSystem.Application.Contracts;

namespace MaterialCodingSystem.Presentation.Converters;

public sealed class BomAuditStatusToTextConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is BomAuditStatus s)
        {
            return s switch
            {
                BomAuditStatus.NEW => "新物料",
                BomAuditStatus.ERROR => "异常物料",
                _ => "PASS"
            };
        }
        return "";
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => Binding.DoNothing;
}

