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
    int DataRetentionDays { get; set; }
    bool MinimalMode { get; set; }

    /// <summary>极简模式下是否显示心率趋势曲线。</summary>
    bool MinimalShowChart { get; set; }

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
