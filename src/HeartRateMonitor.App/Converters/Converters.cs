using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using HeartRateMonitor.App.Infrastructure;
using HeartRateMonitor.Core.Models;

namespace HeartRateMonitor.App.Converters;

public sealed class BoolToVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var flag = value is true;
        if (string.Equals(parameter as string, "Invert", StringComparison.OrdinalIgnoreCase)) flag = !flag;
        return flag ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is Visibility.Visible;
}

public sealed class NullToVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var visible = value is not null && !(value is string s && string.IsNullOrWhiteSpace(s));
        if (string.Equals(parameter as string, "Invert", StringComparison.OrdinalIgnoreCase)) visible = !visible;
        return visible ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

public sealed class InverseBoolConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is not true;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is not true;
}

/// <summary>心率数值 → 区间颜色画刷（用于心率大数字的着色）。</summary>
public sealed class HeartRateToBrushConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not int bpm || bpm <= 0) return Brushes.Gray;
        var color = ThemeManager.TryParseColor(HeartRateZones.Classify(bpm).Color, Colors.Gray);
        return new SolidColorBrush(color);
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>#RRGGBB / #AARRGGBB 字符串 → 冻结后的画刷（用于强调色色板）。</summary>
public sealed class HexToBrushConverter : IValueConverter
{
    private static readonly Dictionary<string, SolidColorBrush> Cache = new(StringComparer.OrdinalIgnoreCase);

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var hex = value as string ?? string.Empty;
        lock (Cache)
        {
            if (Cache.TryGetValue(hex, out var cached)) return cached;

            var brush = new SolidColorBrush(ThemeManager.TryParseColor(hex, Colors.Gray));
            brush.Freeze();
            Cache[hex] = brush;
            return brush;
        }
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>插件清单里的图标逻辑键 → 内置矢量图标。</summary>
public sealed class IconKeyToGeometryConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => AppIcons.Get(value as string);

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>BLE 信号强度(dBm) → 0..1 不透明度。</summary>
public sealed class SignalToOpacityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var dbm = value is int i ? i : -90;
        var opacity = (dbm + 90) / 60.0;
        return Math.Clamp(opacity, 0.15, 1.0);
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
