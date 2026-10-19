namespace HeartRateMonitor.Core.Interfaces;

public interface ISettingsService
{
    event EventHandler? SettingsChanged;

    string? LastDeviceId { get; set; }
    string? LastDeviceName { get; set; }
    bool AutoConnect { get; set; }
    int HighHeartRateThreshold { get; set; }
    int LowHeartRateThreshold { get; set; }
    bool EnableNotifications { get; set; }
    bool EnableSoundAlert { get; set; }
    double OverlayOpacity { get; set; }
    string Theme { get; set; }
    string AccentColor { get; set; }
    string Language { get; set; }
    bool StartWithWindows { get; set; }
    bool MinimizeToTray { get; set; }

    /// <summary>窗口置顶：始终显示在其他窗口之上（默认开启）。</summary>
    bool WindowTopmost { get; set; }
    int DataRetentionDays { get; set; }
    bool MinimalMode { get; set; }

    /// <summary>极简模式下是否显示心率趋势曲线。</summary>
    bool MinimalShowChart { get; set; }

    /// <summary>极简模式心形/光环缩放比例（0.6–2.0，默认 1.0）。</summary>
    double MinimalHeartSize { get; set; }

    /// <summary>极简模式心率数字字号（px，28–120，默认 56）。</summary>
    double MinimalBpmFontSize { get; set; }

    /// <summary>极简模式趋势曲线区域缩放比例（0.5–2.5，默认 1.0）。</summary>
    double MinimalChartScale { get; set; }

    /// <summary>极简模式皮肤：Auto（跟随主题）/ Dark / Light / Neon / Warm / Custom。</summary>
    string MinimalSkin { get; set; }

    /// <summary>极简模式数字颜色（#RRGGBB，默认白色）。</summary>
    string MinimalTextColor { get; set; }

    /// <summary>极简模式心形颜色（#RRGGBB，默认 #FF3B5C）。</summary>
    string MinimalHeartColor { get; set; }

    /// <summary>极简模式趋势曲线颜色（#RRGGBB，默认 #FF3B5C）。</summary>
    string MinimalChartColor { get; set; }

    /// <summary>极简模式数字阴影强度（0–1，默认 0.6）。</summary>
    double MinimalTextShadow { get; set; }

    double WindowLeft { get; set; }
    double WindowTop { get; set; }
    double WindowWidth { get; set; }

    /// <summary>悬浮窗组件布局（JSON 字符串：组件 Id 数组）。</summary>
    string Widgets { get; set; }

    /// <summary>被禁用的插件 Id（分号分隔）。</summary>
    string DisabledPlugins { get; set; }

    Task LoadAsync();
    Task SaveAsync();
    void ResetToDefaults();
    void NotifySettingsChanged();
}
