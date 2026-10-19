using System.Windows.Media;

namespace HeartRateMonitor.App.Infrastructure;

/// <summary>
/// 插件与界面共用的轻量矢量图标：用 16×16 几何路径绘制，不依赖图标字体，
/// 因此插件只需在 <c>WidgetDescriptor.Icon</c> 里写一个逻辑键。
/// </summary>
internal static class AppIcons
{
    private static readonly Dictionary<string, string> Paths = new(StringComparer.OrdinalIgnoreCase)
    {
        // 组件图标
        ["heart"] = "M8,13.6 C8,13.6 1.8,9.6 1.8,5.9 A3.2,3.2 0 0 1 8,4.2 A3.2,3.2 0 0 1 14.2,5.9 C14.2,9.6 8,13.6 8,13.6 Z",
        ["chart"] = "M1.8,12.4 L5.2,7.2 L8,9.8 L11.6,4.2 L14.2,7.4",
        ["device"] = "M8,1.8 V14.2 M8,1.8 L12.2,5.4 L3.8,10.6 M8,14.2 L12.2,10.6 L3.8,5.4",
        ["stats"] = "M2.2,13.4 V8.6 M5.9,13.4 V4.2 M9.6,13.4 V10.2 M13.3,13.4 V6.2",
        ["zone"] = "M1.8,5 H14.2 M1.8,8 H14.2 M1.8,11 H14.2",
        ["clock"] = "M8,1.9 A6.1,6.1 0 1 1 7.99,1.9 M8,4.7 V8.1 L10.3,9.7",
        ["sparkles"] = "M8,1.9 L9.3,6.7 L14.1,8 L9.3,9.3 L8,14.1 L6.7,9.3 L1.9,8 L6.7,6.7 Z",
        ["puzzle"] = "M2.4,5.6 H6 V2.2 H8.6 A2,2 0 1 1 11.4,4.6 H13.6 V8.4 H10.2 M2.4,5.6 V13.6 H10.2 V10.6 H13.6 V8.4 M10.2,5.6 H6 M2.4,9.4 H6",
        ["sliders"] = "M2,3.4 H14 M2,8 H14 M2,12.6 H14 M5,1.9 V4.9 M10.6,6.5 V9.5 M7.4,11.1 V14.1",
        ["palette"] = "M8,1.9 A6.1,6.1 0 1 0 8,14.1 C9.3,14.1 9.7,13.1 9.1,12.2 C8.3,11 9.1,9.7 10.5,9.7 H12.3 A1.9,1.9 0 0 0 14.2,7.8 C14.2,4.5 11.5,1.9 8,1.9 Z M5.2,6.1 V6.2 M7.8,4.4 V4.5 M10.4,6.1 V6.2",
        ["bell"] = "M8,2 A3.4,3.4 0 0 0 4.6,5.4 V8.6 L3.2,11 H12.8 L11.4,8.6 V5.4 A3.4,3.4 0 0 0 8,2 Z M6.5,12.7 A1.7,1.7 0 0 0 9.5,12.7",
        ["info"] = "M8,1.9 A6.1,6.1 0 1 1 7.99,1.9 M8,7.2 V11.4 M8,4.7 V5.1",
        // 动作图标
        ["add"] = "M8,2.4 V13.6 M2.4,8 H13.6",
        ["close"] = "M3.2,3.2 L12.8,12.8 M12.8,3.2 L3.2,12.8",
        ["minimize"] = "M3.2,8 H12.8",
        ["edit"] = "M2.4,11.6 L11.2,2.8 L13.2,4.8 L4.4,13.6 L2.2,13.8 Z M9.8,4.2 L11.8,6.2",
        ["refresh"] = "M3,8 A5,5 0 1 1 5.2,12.4 M2.6,4 V8.2 H6.8",
        ["check"] = "M3,8.4 L6.4,11.8 L13,4.4",
        ["remove"] = "M3.2,3.2 L12.8,12.8 M12.8,3.2 L3.2,12.8",
        ["settings"] = "M2,3.4 H14 M2,8 H14 M2,12.6 H14 M5,1.9 V4.9 M10.6,6.5 V9.5 M7.4,11.1 V14.1"
    };

    private static readonly Dictionary<string, Geometry> Cache = new(StringComparer.OrdinalIgnoreCase);

    public static Geometry Get(string? key)
    {
        var actual = string.IsNullOrWhiteSpace(key) ? "puzzle" : key.Trim();

        if (Cache.TryGetValue(actual, out var cached))
        {
            return cached;
        }

        var geometry = Paths.TryGetValue(actual, out var data)
            ? Geometry.Parse(data)
            : Geometry.Parse(Paths["puzzle"]);

        geometry.Freeze();
        Cache[actual] = geometry;
        return geometry;
    }
}
