using System.Globalization;
using System.Windows.Controls;
using System.Windows.Data;

namespace NPUtil.App.Common;

public class ComboBoxContentConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is ComboBoxItem cbi)
        {
            return cbi.Content;
        }
        return value;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
