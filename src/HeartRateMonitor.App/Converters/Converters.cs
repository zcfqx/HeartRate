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

public class OverlayOpacityToBrushConverter : IValueConverter
{
    private static readonly Color BaseColor = (Color)ColorConverter.ConvertFromString("#141422");

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is double opacity)
        {
            opacity = Math.Clamp(opacity, 0.0, 1.0);
            byte alpha = (byte)(179 * opacity);
            return new SolidColorBrush(Color.FromArgb(alpha, BaseColor.R, BaseColor.G, BaseColor.B));
        }
        return new SolidColorBrush(Color.FromArgb(179, BaseColor.R, BaseColor.G, BaseColor.B));
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

public class SignalStrengthToOpacityConverter : IValueConverter
{
    public static readonly SignalStrengthToOpacityConverter Instance = new();

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is int rssi)
        {
            // -30 dBm = 1.0 (excellent), -90 dBm = 0.2 (poor)
            double opacity = Math.Clamp((rssi + 90) / 60.0, 0.2, 1.0);
            return opacity;
        }
        return 0.5;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
