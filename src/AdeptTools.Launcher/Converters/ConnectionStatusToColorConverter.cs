using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

namespace AdeptTools.Launcher.Converters;

public class ConnectionStatusToColorConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is not ConnectionStatus status) return Brushes.Gray;
        return status switch
        {
            ConnectionStatus.Disconnected => new SolidColorBrush(Color.FromRgb(0x9F, 0xA5, 0xB2)),  // Neutral400
            ConnectionStatus.Connecting => new SolidColorBrush(Color.FromRgb(0xE3, 0xA9, 0x11)),     // Warning
            ConnectionStatus.Connected => new SolidColorBrush(Color.FromRgb(0x05, 0x96, 0x69)),      // Success
            ConnectionStatus.Error => new SolidColorBrush(Color.FromRgb(0xDC, 0x26, 0x26)),          // Error
            _ => Brushes.Gray
        };
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}

/// <summary>Collapses the element when Status != Connecting (used to show Cancel button only during SSO wait).</summary>
public class ConnectingToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => value is ConnectionStatus.Connecting ? Visibility.Visible : Visibility.Collapsed;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

public enum ConnectionStatus
{
    Disconnected,
    Connecting,
    Connected,
    Error
}
