using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace HeartRateMonitor.App.Converters;

public class HeartRateToColorConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is int heartRate)
        {
            return heartRate switch
            {
                < 100 => new SolidColorBrush(ColorFromHex("#FFFFFF")),
                < 140 => new SolidColorBrush(ColorFromHex("#22C55E")),
                < 170 => new SolidColorBrush(ColorFromHex("#F59E0B")),
                < 200 => new SolidColorBrush(ColorFromHex("#F97316")),
                _ => new SolidColorBrush(ColorFromHex("#EF4444"))
            };
        }
        return new SolidColorBrush(Colors.White);
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }

    private static Color ColorFromHex(string hex)
    {
        return (Color)ColorConverter.ConvertFromString(hex);
    }
}

public class BoolToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        bool boolValue = value is true;
        if (parameter?.ToString() == "Invert")
            boolValue = !boolValue;
        return boolValue ? System.Windows.Visibility.Visible : System.Windows.Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

public class ConnectionStateToColorConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is string state)
        {
            return state switch
            {
                "已连接" => new SolidColorBrush(ColorFromHex("#22C55E")),
                "扫描中..." or "连接中..." or "重连中..." or "扫描中" or "连接中" or "重连中" => new SolidColorBrush(ColorFromHex("#F59E0B")),
                _ => new SolidColorBrush(ColorFromHex("#EF4444"))
            };
        }
        return new SolidColorBrush(ColorFromHex("#EF4444"));
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }

    private static Color ColorFromHex(string hex)
    {
        return (Color)ColorConverter.ConvertFromString(hex);
    }
}
