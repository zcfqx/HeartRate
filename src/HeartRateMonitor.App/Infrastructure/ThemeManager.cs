using System.Windows;
using System.Windows.Media;

namespace HeartRateMonitor.App.Infrastructure;

/// <summary>
/// 运行时换肤：交换主题资源字典 + 注入强调色。
/// <para>
/// 样式全部通过 DynamicResource 引用主题键，所以只需替换资源即可整体换肤，无需重建窗口。
/// </para>
/// </summary>
internal static class ThemeManager
{
    public const string DefaultAccent = "#6366F1";

    private static readonly Color FallbackAccent = Color.FromRgb(0x63, 0x66, 0xF1);

    public static string CurrentTheme { get; private set; } = "Dark";

    public static string CurrentAccent { get; private set; } = DefaultAccent;

    public static event EventHandler? ThemeChanged;

    public static void Apply(string? theme, string? accentHex)
    {
        var app = Application.Current;
        if (app is null) return;

        var normalized = string.Equals(theme, "Light", StringComparison.OrdinalIgnoreCase) ? "Light" : "Dark";
        var accent = TryParseColor(accentHex, FallbackAccent);

        SwapThemeDictionary(app, normalized);
        InjectAccent(app, accent);

        CurrentTheme = normalized;
        CurrentAccent = $"#{accent.R:X2}{accent.G:X2}{accent.B:X2}";
        ThemeChanged?.Invoke(null, EventArgs.Empty);
    }

    /// <summary>把 #RRGGBB / #AARRGGBB 解析为颜色，失败时返回 <paramref name="fallback"/>。</summary>
    public static Color TryParseColor(string? hex, Color fallback)
    {
        if (string.IsNullOrWhiteSpace(hex)) return fallback;

        try
        {
            var text = hex.Trim();
            if (!text.StartsWith('#')) text = "#" + text;

            return text.Length switch
            {
                7 => Color.FromRgb(
                    Convert.ToByte(text.Substring(1, 2), 16),
                    Convert.ToByte(text.Substring(3, 2), 16),
                    Convert.ToByte(text.Substring(5, 2), 16)),
                9 => Color.FromArgb(
                    Convert.ToByte(text.Substring(1, 2), 16),
                    Convert.ToByte(text.Substring(3, 2), 16),
                    Convert.ToByte(text.Substring(5, 2), 16),
                    Convert.ToByte(text.Substring(7, 2), 16)),
                _ => fallback
            };
        }
        catch
        {
            return fallback;
        }
    }

    private static void SwapThemeDictionary(Application app, string theme)
    {
        var dictionaries = app.Resources.MergedDictionaries;
        var uri = new Uri(
            $"pack://application:,,,/HeartRateMonitor.App;component/Themes/Theme.{theme}.xaml",
            UriKind.Absolute);

        foreach (var dictionary in dictionaries)
        {
            if (dictionary.Source is null) continue;
            // App.xaml 里的合并字典是相对 URI（Themes/Theme.Dark.xaml），
            // 运行时替换后变成 pack URI（.../Themes/Theme.Dark.xaml），两者都要能匹配。
            if (dictionary.Source.OriginalString.Contains("Themes/Theme.", StringComparison.OrdinalIgnoreCase))
            {
                if (dictionary.Source != uri)
                {
                    dictionary.Source = uri;
                }

                return;
            }
        }

        dictionaries.Insert(0, new ResourceDictionary { Source = uri });
    }

    private static void InjectAccent(Application app, Color accent)
    {
        var background = accent.R * 0.299 + accent.G * 0.587 + accent.B * 0.114;
        var onAccent = background > 170 ? Color.FromRgb(0x11, 0x14, 0x1E) : Colors.White;

        app.Resources["Color.Accent"] = accent;
        app.Resources["Brush.Accent"] = Freeze(new SolidColorBrush(accent));
        app.Resources["Brush.AccentSoft"] = Freeze(new SolidColorBrush(Color.FromArgb(0x2E, accent.R, accent.G, accent.B)));
        app.Resources["Brush.AccentDeep"] = Freeze(new SolidColorBrush(Adjust(accent, 0.78)));
        app.Resources["Brush.OnAccent"] = Freeze(new SolidColorBrush(onAccent));
    }

    private static Color Adjust(Color color, double factor)
        => Color.FromArgb(
            color.A,
            (byte)Math.Clamp(Math.Round(color.R * factor), 0, 255),
            (byte)Math.Clamp(Math.Round(color.G * factor), 0, 255),
            (byte)Math.Clamp(Math.Round(color.B * factor), 0, 255));

    private static SolidColorBrush Freeze(SolidColorBrush brush)
    {
        brush.Freeze();
        return brush;
    }
}
