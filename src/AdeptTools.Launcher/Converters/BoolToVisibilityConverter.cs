using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace AdeptTools.Launcher.Converters;

public class BoolToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        bool boolValue = value switch
        {
            bool b => b,
            string s => !string.IsNullOrEmpty(s),
            null => false,
            _ => true
        };
        if (parameter is string p && p == "Invert")
            boolValue = !boolValue;
        return boolValue ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value is Visibility v && v == Visibility.Visible;
    }
}
