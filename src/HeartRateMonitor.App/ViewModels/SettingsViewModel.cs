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

    partial void OnHighHeartRateThresholdChanged(int value)
    {
        if (value < 30) HighHeartRateThreshold = 30;
        else if (value > 250) HighHeartRateThreshold = 250;
    }

    partial void OnLowHeartRateThresholdChanged(int value)
    {
        if (value < 30) LowHeartRateThreshold = 30;
        else if (value > 250) LowHeartRateThreshold = 250;
    }

    partial void OnDataRetentionDaysChanged(int value)
    {
        if (value < 1) DataRetentionDays = 1;
        else if (value > 365) DataRetentionDays = 365;
    }

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
            if (HighHeartRateThreshold <= LowHeartRateThreshold)
            {
                _logger.Warning("心率上限阈值应大于下限阈值");
            }

            _settingsService.AutoConnect = AutoConnect;
            _settingsService.HighHeartRateThreshold = HighHeartRateThreshold;
            _settingsService.LowHeartRateThreshold = LowHeartRateThreshold;
            _settingsService.EnableNotifications = EnableNotifications;
            _settingsService.EnableSoundAlert = EnableSoundAlert;
            _settingsService.OverlayOpacity = OverlayOpacity;
            _settingsService.StartWithWindows = StartWithWindows;
            _settingsService.MinimizeToTray = MinimizeToTray;
            _settingsService.DataRetentionDays = DataRetentionDays;
            _settingsService.MinimalMode = MinimalMode;

            await _settingsService.SaveAsync();
            _settingsService.NotifySettingsChanged();
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
