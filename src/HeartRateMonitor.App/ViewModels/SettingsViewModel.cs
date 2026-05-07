using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HeartRateMonitor.Core.Interfaces;

namespace HeartRateMonitor.App.ViewModels;

public partial class SettingsViewModel : ObservableObject
{
    private readonly ISettingsService _settingsService;
    private readonly ILogger _logger;

    [ObservableProperty]
    private bool _autoConnect = true;

    [ObservableProperty]
    private int _highHeartRateThreshold = 160;

    [ObservableProperty]
    private int _lowHeartRateThreshold = 50;

    [ObservableProperty]
    private bool _enableNotifications = true;

    [ObservableProperty]
    private bool _enableSoundAlert;

    [ObservableProperty]
    private double _overlayOpacity = 1.0;

    partial void OnOverlayOpacityChanged(double value)
    {
        _settingsService.OverlayOpacity = value;
        _settingsService.NotifySettingsChanged();
    }

    [ObservableProperty]
    private string _theme = "深色";

    [ObservableProperty]
    private string _language = "简体中文";

    [ObservableProperty]
    private bool _startWithWindows;

    [ObservableProperty]
    private bool _minimizeToTray = true;

    [ObservableProperty]
    private int _dataRetentionDays = 30;

    [ObservableProperty]
    private bool _minimalMode;

    public SettingsViewModel(ISettingsService settingsService, ILogger logger)
    {
        _settingsService = settingsService;
        _logger = logger;
        LoadFromService();
    }

    private void LoadFromService()
    {
        AutoConnect = _settingsService.AutoConnect;
        HighHeartRateThreshold = _settingsService.HighHeartRateThreshold;
        LowHeartRateThreshold = _settingsService.LowHeartRateThreshold;
        EnableNotifications = _settingsService.EnableNotifications;
        EnableSoundAlert = _settingsService.EnableSoundAlert;
        OverlayOpacity = _settingsService.OverlayOpacity;
        Theme = _settingsService.Theme == "Dark" ? "深色" : "浅色";
        Language = _settingsService.Language == "en" ? "English" : "简体中文";
        StartWithWindows = _settingsService.StartWithWindows;
        MinimizeToTray = _settingsService.MinimizeToTray;
        DataRetentionDays = _settingsService.DataRetentionDays;
        MinimalMode = _settingsService.MinimalMode;
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        try
        {
            _settingsService.AutoConnect = AutoConnect;
            _settingsService.HighHeartRateThreshold = HighHeartRateThreshold;
            _settingsService.LowHeartRateThreshold = LowHeartRateThreshold;
            _settingsService.EnableNotifications = EnableNotifications;
            _settingsService.EnableSoundAlert = EnableSoundAlert;
            _settingsService.OverlayOpacity = OverlayOpacity;
            _settingsService.Theme = Theme == "深色" ? "Dark" : "Light";
            _settingsService.Language = Language == "English" ? "en" : "zh-CN";
            _settingsService.StartWithWindows = StartWithWindows;
            _settingsService.MinimizeToTray = MinimizeToTray;
            _settingsService.DataRetentionDays = DataRetentionDays;
            _settingsService.MinimalMode = MinimalMode;

            await _settingsService.SaveAsync();
            _logger.Info("设置已保存");
        }
        catch (Exception ex)
        {
            _logger.Error("保存设置失败", ex);
        }
    }

    [RelayCommand]
    private void ResetDefaults()
    {
        _settingsService.ResetToDefaults();
        LoadFromService();
    }
}
