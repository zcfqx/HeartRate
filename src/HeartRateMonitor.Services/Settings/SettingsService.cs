using HeartRateMonitor.Core.Interfaces;
using HeartRateMonitor.Data.Repositories;
using Microsoft.Data.Sqlite;

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
    private const string KeyAccent = "AccentColor";
    private const string KeyLanguage = "Language";
    private const string KeyStartWithWindows = "StartWithWindows";
    private const string KeyMinimizeToTray = "MinimizeToTray";
    private const string KeyRetentionDays = "DataRetentionDays";
    private const string KeyMinimalMode = "MinimalMode";
    private const string KeyMinimalShowChart = "MinimalShowChart";
    private const string KeyWindowLeft = "WindowLeft";
    private const string KeyWindowTop = "WindowTop";
    private const string KeyWindowWidth = "WindowWidth";
    private const string KeyWidgets = "Widgets";
    private const string KeyDisabledPlugins = "DisabledPlugins";

    public string? LastDeviceId { get; set; }
    public string? LastDeviceName { get; set; }
    public bool AutoConnect { get; set; } = true;
    public int HighHeartRateThreshold { get; set; } = 160;
    public int LowHeartRateThreshold { get; set; } = 50;
    public bool EnableNotifications { get; set; } = true;
    public bool EnableSoundAlert { get; set; } = false;
    public double OverlayOpacity { get; set; } = 1.0;
    public string Theme { get; set; } = "Dark";
    public string AccentColor { get; set; } = "#6366F1";
    public string Language { get; set; } = "zh-CN";
    public bool StartWithWindows { get; set; } = false;
    public bool MinimizeToTray { get; set; } = true;
    public int DataRetentionDays { get; set; } = 30;
    public bool MinimalMode { get; set; } = false;
    public bool MinimalShowChart { get; set; } = true;
    public double WindowLeft { get; set; } = 100;
    public double WindowTop { get; set; } = 100;
    public double WindowWidth { get; set; } = 320;
    public string Widgets { get; set; } = "";
    public string DisabledPlugins { get; set; } = "";

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
            AccentColor = GetString(settings, KeyAccent) ?? "#6366F1";
            Language = GetString(settings, KeyLanguage) ?? "zh-CN";
            StartWithWindows = GetBool(settings, KeyStartWithWindows, false);
            MinimizeToTray = GetBool(settings, KeyMinimizeToTray, true);
            DataRetentionDays = GetInt(settings, KeyRetentionDays, 30);
            MinimalMode = GetBool(settings, KeyMinimalMode, false);
            MinimalShowChart = GetBool(settings, KeyMinimalShowChart, true);
            WindowLeft = GetDouble(settings, KeyWindowLeft, 100);
            WindowTop = GetDouble(settings, KeyWindowTop, 100);
            WindowWidth = GetDouble(settings, KeyWindowWidth, 320);
            Widgets = GetString(settings, KeyWidgets) ?? "";
            DisabledPlugins = GetString(settings, KeyDisabledPlugins) ?? "";

            _logger.Info("设置加载完成");
        }
        catch (Exception ex)
        {
            _logger.Error("加载设置失败，使用默认值", ex);
            ResetToDefaults();
        }
    }

    public async Task SaveAsync()
    {
        try
        {
            var values = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                [KeyLastDeviceId] = LastDeviceId ?? "",
                [KeyLastDeviceName] = LastDeviceName ?? "",
                [KeyAutoConnect] = AutoConnect.ToString(),
                [KeyHighThreshold] = HighHeartRateThreshold.ToString(),
                [KeyLowThreshold] = LowHeartRateThreshold.ToString(),
                [KeyNotifications] = EnableNotifications.ToString(),
                [KeySoundAlert] = EnableSoundAlert.ToString(),
                [KeyOpacity] = OverlayOpacity.ToString("F2"),
                [KeyTheme] = Theme,
                [KeyAccent] = AccentColor,
                [KeyLanguage] = Language,
                [KeyStartWithWindows] = StartWithWindows.ToString(),
                [KeyMinimizeToTray] = MinimizeToTray.ToString(),
                [KeyRetentionDays] = DataRetentionDays.ToString(),
                [KeyMinimalMode] = MinimalMode.ToString(),
                [KeyMinimalShowChart] = MinimalShowChart.ToString(),
                [KeyWindowLeft] = WindowLeft.ToString("F0"),
                [KeyWindowTop] = WindowTop.ToString("F0"),
                [KeyWindowWidth] = WindowWidth.ToString("F0"),
                [KeyWidgets] = Widgets,
                [KeyDisabledPlugins] = DisabledPlugins
            };

            // 心率数据每秒都在落库，单事务写入 + 忙等待重试，避免用户看到“保存设置失败”。
            const int maxAttempts = 3;
            for (var attempt = 1; ; attempt++)
            {
                try
                {
                    await _repository.SetManyAsync(values);
                    break;
                }
                catch (SqliteException ex) when (attempt < maxAttempts && IsDatabaseBusy(ex))
                {
                    _logger.Warning($"保存设置时数据库忙，第 {attempt} 次重试：{ex.Message}");
                    await Task.Delay(150 * attempt);
                }
            }

            SettingsChanged?.Invoke(this, EventArgs.Empty);
            _logger.Info("设置已保存");
        }
        catch (Exception ex)
        {
            _logger.Error("保存设置失败", ex);
            throw;
        }
    }

    private static bool IsDatabaseBusy(SqliteException ex)
        => ex.SqliteErrorCode is 5 or 6; // SQLITE_BUSY / SQLITE_LOCKED

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
        AccentColor = "#6366F1";
        Language = "zh-CN";
        StartWithWindows = false;
        MinimizeToTray = true;
        DataRetentionDays = 30;
        MinimalMode = false;
        MinimalShowChart = true;
        WindowLeft = 100;
        WindowTop = 100;
        WindowWidth = 320;
        Widgets = "";
        DisabledPlugins = "";
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
