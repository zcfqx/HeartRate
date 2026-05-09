using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

namespace HeartRateMonitor.App.Converters;

public class HeartRateToColorConverter : IValueConverter
{
    private static Brush? _textPrimaryBrush;
    private static Brush? _successBrush;
    private static Brush? _warningBrush;
    private static Brush? _orangeBrush;
    private static Brush? _dangerBrush;

    private static Brush GetBrush(string key) =>
        key switch
        {
            "TextPrimaryBrush" => _textPrimaryBrush ??= (Brush)Application.Current.Resources["TextPrimaryBrush"],
            "SuccessBrush" => _successBrush ??= (Brush)Application.Current.Resources["SuccessBrush"],
            "WarningBrush" => _warningBrush ??= (Brush)Application.Current.Resources["WarningBrush"],
            "OrangeBrush" => _orangeBrush ??= (Brush)Application.Current.Resources["OrangeBrush"],
            "DangerBrush" => _dangerBrush ??= (Brush)Application.Current.Resources["DangerBrush"],
            _ => Brushes.White
        };

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is int heartRate)
        {
            return heartRate switch
            {
                < 100 => GetBrush("TextPrimaryBrush"),
                < 140 => GetBrush("SuccessBrush"),
                < 170 => GetBrush("WarningBrush"),
                < 200 => GetBrush("OrangeBrush"),
                _ => GetBrush("DangerBrush")
            };
        }
        return Brushes.White;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

public class BoolToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        bool boolValue = value is true;
        if (parameter?.ToString() == "Invert")
            boolValue = !boolValue;
        return boolValue ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

public class OverlayOpacityToBrushConverter : IValueConverter
{
    private static Color? _baseColor;

    private static Color BaseColor => _baseColor ??= (Color)Application.Current.Resources["OverlayBaseColor"];

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
    private static Brush? _successBrush;
    private static Brush? _warningBrush;
    private static Brush? _dangerBrush;

    private static Brush GetBrush(string key) =>
        key switch
        {
            "SuccessBrush" => _successBrush ??= (Brush)Application.Current.Resources["SuccessBrush"],
            "WarningBrush" => _warningBrush ??= (Brush)Application.Current.Resources["WarningBrush"],
            "DangerBrush" => _dangerBrush ??= (Brush)Application.Current.Resources["DangerBrush"],
            _ => Brushes.Red
        };

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is string state)
        {
            return state switch
            {
                "已连接" => GetBrush("SuccessBrush"),
                "扫描中..." or "连接中..." or "重连中..." or "扫描中" or "连接中" or "重连中" => GetBrush("WarningBrush"),
                _ => GetBrush("DangerBrush")
            };
        }
        return GetBrush("DangerBrush");
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
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
