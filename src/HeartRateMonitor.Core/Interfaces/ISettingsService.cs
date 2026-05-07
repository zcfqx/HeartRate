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
    string Language { get; set; }
    bool StartWithWindows { get; set; }
    bool MinimizeToTray { get; set; }
    int DataRetentionDays { get; set; }
    double WindowLeft { get; set; }
    double WindowTop { get; set; }

    Task LoadAsync();
    Task SaveAsync();
    void ResetToDefaults();
}
