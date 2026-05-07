using HeartRateMonitor.Core.Interfaces;
using HeartRateMonitor.Data.Repositories;

namespace HeartRateMonitor.Services.Settings;

public class SettingsService : ISettingsService
{
    private readonly SettingsRepository _repository;
    private readonly ILogger _logger;

    public event EventHandler? SettingsChanged;

    private const string KeyLastDeviceId = "LastDeviceId";
    private const string KeyLastDeviceName = "LastDeviceName";
    private const string KeyAutoConnect = "AutoConnect";
    private const string KeyHighThreshold = "HighHeartRateThreshold";
    private const string KeyLowThreshold = "LowHeartRateThreshold";
    private const string KeyNotifications = "EnableNotifications";
    private const string KeySoundAlert = "EnableSoundAlert";
    private const string KeyOpacity = "OverlayOpacity";
    private const string KeyTheme = "Theme";
    private const string KeyLanguage = "Language";
    private const string KeyStartWithWindows = "StartWithWindows";
    private const string KeyMinimizeToTray = "MinimizeToTray";
    private const string KeyRetentionDays = "DataRetentionDays";
    private const string KeyMinimalMode = "MinimalMode";
    private const string KeyWindowLeft = "WindowLeft";
    private const string KeyWindowTop = "WindowTop";

    public string? LastDeviceId { get; set; }
    public string? LastDeviceName { get; set; }
    public bool AutoConnect { get; set; } = true;
    public int HighHeartRateThreshold { get; set; } = 160;
    public int LowHeartRateThreshold { get; set; } = 50;
    public bool EnableNotifications { get; set; } = true;
    public bool EnableSoundAlert { get; set; } = false;
    public double OverlayOpacity { get; set; } = 1.0;
    public string Theme { get; set; } = "Dark";
    public string Language { get; set; } = "zh-CN";
    public bool StartWithWindows { get; set; } = false;
    public bool MinimizeToTray { get; set; } = true;
    public int DataRetentionDays { get; set; } = 30;
    public bool MinimalMode { get; set; } = false;
    public double WindowLeft { get; set; } = 100;
    public double WindowTop { get; set; } = 100;

    public SettingsService(SettingsRepository repository, ILogger logger)
    {
        _repository = repository;
        _logger = logger;
    }

    public async Task LoadAsync()
    {
        try
        {
            var settings = await _repository.GetAllAsync();

            LastDeviceId = GetString(settings, KeyLastDeviceId);
            LastDeviceName = GetString(settings, KeyLastDeviceName);
            AutoConnect = GetBool(settings, KeyAutoConnect, true);
            HighHeartRateThreshold = GetInt(settings, KeyHighThreshold, 160);
            LowHeartRateThreshold = GetInt(settings, KeyLowThreshold, 50);
            EnableNotifications = GetBool(settings, KeyNotifications, true);
            EnableSoundAlert = GetBool(settings, KeySoundAlert, false);
            OverlayOpacity = GetDouble(settings, KeyOpacity, 1.0);
            Theme = GetString(settings, KeyTheme) ?? "Dark";
            Language = GetString(settings, KeyLanguage) ?? "zh-CN";
            StartWithWindows = GetBool(settings, KeyStartWithWindows, false);
            MinimizeToTray = GetBool(settings, KeyMinimizeToTray, true);
            DataRetentionDays = GetInt(settings, KeyRetentionDays, 30);
            MinimalMode = GetBool(settings, KeyMinimalMode, false);
            WindowLeft = GetDouble(settings, KeyWindowLeft, 100);
            WindowTop = GetDouble(settings, KeyWindowTop, 100);

            _logger.Info("Settings loaded successfully");
        }
        catch (Exception ex)
        {
            _logger.Error("Failed to load settings, using defaults", ex);
            ResetToDefaults();
        }
    }

    public async Task SaveAsync()
    {
        try
        {
            await _repository.SetValueAsync(KeyLastDeviceId, LastDeviceId ?? "");
            await _repository.SetValueAsync(KeyLastDeviceName, LastDeviceName ?? "");
            await _repository.SetValueAsync(KeyAutoConnect, AutoConnect.ToString());
            await _repository.SetValueAsync(KeyHighThreshold, HighHeartRateThreshold.ToString());
            await _repository.SetValueAsync(KeyLowThreshold, LowHeartRateThreshold.ToString());
            await _repository.SetValueAsync(KeyNotifications, EnableNotifications.ToString());
            await _repository.SetValueAsync(KeySoundAlert, EnableSoundAlert.ToString());
            await _repository.SetValueAsync(KeyOpacity, OverlayOpacity.ToString("F2"));
            await _repository.SetValueAsync(KeyTheme, Theme);
            await _repository.SetValueAsync(KeyLanguage, Language);
            await _repository.SetValueAsync(KeyStartWithWindows, StartWithWindows.ToString());
            await _repository.SetValueAsync(KeyMinimizeToTray, MinimizeToTray.ToString());
            await _repository.SetValueAsync(KeyRetentionDays, DataRetentionDays.ToString());
            await _repository.SetValueAsync(KeyMinimalMode, MinimalMode.ToString());
            await _repository.SetValueAsync(KeyWindowLeft, WindowLeft.ToString("F0"));
            await _repository.SetValueAsync(KeyWindowTop, WindowTop.ToString("F0"));

            SettingsChanged?.Invoke(this, EventArgs.Empty);
            _logger.Info("Settings saved successfully");
        }
        catch (Exception ex)
        {
            _logger.Error("Failed to save settings", ex);
            throw;
        }
    }

    public void NotifySettingsChanged()
    {
        SettingsChanged?.Invoke(this, EventArgs.Empty);
    }

    public void ResetToDefaults()
    {
        LastDeviceId = null;
        LastDeviceName = null;
        AutoConnect = true;
        HighHeartRateThreshold = 160;
        LowHeartRateThreshold = 50;
        EnableNotifications = true;
        EnableSoundAlert = false;
        OverlayOpacity = 1.0;
        Theme = "Dark";
        Language = "zh-CN";
        StartWithWindows = false;
        MinimizeToTray = true;
        DataRetentionDays = 30;
        MinimalMode = false;
        WindowLeft = 100;
        WindowTop = 100;
    }

    private static string? GetString(Dictionary<string, string> settings, string key)
    {
        return settings.TryGetValue(key, out var value) ? value : null;
    }

    private static bool GetBool(Dictionary<string, string> settings, string key, bool defaultValue)
    {
        return settings.TryGetValue(key, out var value) && bool.TryParse(value, out var result) ? result : defaultValue;
    }

    private static int GetInt(Dictionary<string, string> settings, string key, int defaultValue)
    {
        return settings.TryGetValue(key, out var value) && int.TryParse(value, out var result) ? result : defaultValue;
    }

    private static double GetDouble(Dictionary<string, string> settings, string key, double defaultValue)
    {
        return settings.TryGetValue(key, out var value) && double.TryParse(value, out var result) ? result : defaultValue;
    }
}
