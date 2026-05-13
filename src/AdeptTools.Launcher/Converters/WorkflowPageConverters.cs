using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace AdeptTools.Launcher.ViewModels;

/// <summary>
/// Simple converters used inline in WorkflowPage.xaml.
/// </summary>
public static class WorkflowPage_Converters
{
    public static IValueConverter GreaterThanZero { get; } = new GreaterThanZeroConverter();
    public static IValueConverter ZeroToVisible { get; } = new ZeroToVisibleConverter();

    private class GreaterThanZeroConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
            => value is int n && n > 0;

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotSupportedException();
    }

    private class ZeroToVisibleConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
            => value is int n && n == 0 ? Visibility.Visible : Visibility.Collapsed;

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotSupportedException();
    }
}
