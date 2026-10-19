using System.Windows.Media;
using HeartRateMonitor.Core.Interfaces;

namespace HeartRateMonitor.App.Infrastructure;

/// <summary>
/// 极简模式皮肤的配色表。皮肤只影响极简悬浮窗（数字、红心、曲线、阴影），
/// 看板与设置页仍由应用主题（深色/浅色）控制。
/// </summary>
internal static class MinimalSkinPalette
{
    public const string Auto = "Auto";
    public const string Dark = "Dark";
    public const string Light = "Light";
    public const string Neon = "Neon";
    public const string Warm = "Warm";
    public const string Custom = "Custom";

    public static readonly IReadOnlyList<(string Value, string Label)> Presets =
    [
        (Auto, "自动（跟随主题）"),
        (Dark, "暗夜·白字"),
        (Light, "亮底·深字"),
        (Neon, "霓虹·青"),
        (Warm, "暖阳·橙"),
        (Custom, "自定义")
    ];

    /// <summary>归一化皮肤名，未知值回退到 Dark（保持原有白字红心视觉）。</summary>
    public static string Normalize(string? skin) => skin switch
    {
        Auto => Auto,
        Light => Light,
        Neon => Neon,
        Warm => Warm,
        Custom => Custom,
        _ => Dark
    };

    public static string LabelFor(string? skin)
    {
        var normalized = Normalize(skin);
        foreach (var preset in Presets)
        {
            if (string.Equals(preset.Value, normalized, StringComparison.Ordinal))
            {
                return preset.Label;
            }
        }

        return Presets[1].Label;
    }

    /// <summary>预设皮肤配套的三色；Custom 不使用该结果。</summary>
    public static (string Text, string Heart, string Chart) GetPresetColors(string? skin) => Normalize(skin) switch
    {
        // 自动模式只用主题决定文字明暗，红心/曲线保持默认红。
        Auto => ("#FFFFFF", "#FF3B5C", "#FF3B5C"),
        Light => ("#111827", "#E11D48", "#E11D48"),
        Neon => ("#A5F3FC", "#22D3EE", "#22D3EE"),
        Warm => ("#FDE68A", "#F97316", "#F97316"),
        _ => ("#FFFFFF", "#FF3B5C", "#FF3B5C")
    };

    /// <summary>解析当前皮肤的实际颜色（Auto 时文字颜色跟随应用主题）。</summary>
    public static (Color Text, Color Heart, Color Chart) Resolve(ISettingsService settings)
    {
        var skin = Normalize(settings.MinimalSkin);
        var textHex = skin == Auto
            ? (string.Equals(ThemeManager.CurrentTheme, "Light", StringComparison.OrdinalIgnoreCase) ? "#111827" : "#FFFFFF")
            : settings.MinimalTextColor;

        return (
            Parse(textHex, Colors.White),
            Parse(settings.MinimalHeartColor, Color.FromRgb(0xFF, 0x3B, 0x5C)),
            Parse(settings.MinimalChartColor, Color.FromRgb(0xFF, 0x3B, 0x5C)));
    }

    /// <summary>解析 #RRGGBB / #AARRGGBB，失败返回 fallback。</summary>
    public static Color Parse(string? hex, Color fallback)
    {
        var normalized = NormalizeColor(hex, string.Empty);
        if (normalized.Length == 0)
        {
            return fallback;
        }

        try
        {
            return (Color)ColorConverter.ConvertFromString(normalized);
        }
        catch (FormatException)
        {
            return fallback;
        }
    }

    /// <summary>颜色文本归一化成大写 #RRGGBB；非法值返回空字符串。</summary>
    public static string NormalizeColor(string? value, string fallback)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return fallback;
        }

        var text = value.Trim();
        if (!text.StartsWith('#'))
        {
            text = "#" + text;
        }

        if (text.Length == 9)
        {
            text = "#" + text.Substring(3);
        }

        if (text.Length != 7)
        {
            return fallback;
        }

        for (var i = 1; i < text.Length; i++)
        {
            if (!Uri.IsHexDigit(text[i]))
            {
                return fallback;
            }
        }

        return text.ToUpperInvariant();
    }

    /// <summary>把颜色向白色提亮（amount 0–1），用于心形/曲线的渐变高光。</summary>
    public static Color Lighten(Color color, double amount)
    {
        var ratio = Math.Clamp(amount, 0, 1);
        byte Mix(byte channel) => (byte)Math.Round(channel + (255 - channel) * ratio);
        return Color.FromArgb(color.A, Mix(color.R), Mix(color.G), Mix(color.B));
    }

    /// <summary>人眼感知亮度（0–255），用于决定阴影/发光颜色。</summary>
    public static double Luminance(Color color)
        => 0.299 * color.R + 0.587 * color.G + 0.114 * color.B;
}
