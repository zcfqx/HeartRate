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
    private const string KeyWindowTopmost = "WindowTopmost";
    private const string KeyRetentionDays = "DataRetentionDays";
    private const string KeyMinimalMode = "MinimalMode";
    private const string KeyMinimalShowChart = "MinimalShowChart";
    private const string KeyMinimalHeartSize = "MinimalHeartSize";
    private const string KeyMinimalBpmFontSize = "MinimalBpmFontSize";
    private const string KeyMinimalChartScale = "MinimalChartScale";
    private const string KeyMinimalSkin = "MinimalSkin";
    private const string KeyMinimalTextColor = "MinimalTextColor";
    private const string KeyMinimalHeartColor = "MinimalHeartColor";
    private const string KeyMinimalChartColor = "MinimalChartColor";
    private const string KeyMinimalTextShadow = "MinimalTextShadow";
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
    public bool WindowTopmost { get; set; } = true;
    public int DataRetentionDays { get; set; } = 30;
    public bool MinimalMode { get; set; } = false;
    public bool MinimalShowChart { get; set; } = true;

    private double _minimalHeartSize = 1.0;
    private double _minimalBpmFontSize = 56;
    private double _minimalChartScale = 1.0;

    /// <summary>极简模式心形/光环缩放比例，范围 0.6–2.0。</summary>
    public double MinimalHeartSize
    {
        get => _minimalHeartSize;
        set => _minimalHeartSize = double.IsFinite(value) ? Math.Clamp(value, 0.6, 2.0) : 1.0;
    }

    /// <summary>极简模式心率数字字号（px），范围 28–120。</summary>
    public double MinimalBpmFontSize
    {
        get => _minimalBpmFontSize;
        set => _minimalBpmFontSize = double.IsFinite(value) ? Math.Clamp(value, 28, 120) : 56;
    }

    /// <summary>极简模式趋势曲线区域缩放比例，范围 0.5–2.5。</summary>
    public double MinimalChartScale
    {
        get => _minimalChartScale;
        set => _minimalChartScale = double.IsFinite(value) ? Math.Clamp(value, 0.5, 2.5) : 1.0;
    }

    private string _minimalSkin = "Dark";
    private string _minimalTextColor = "#FFFFFF";
    private string _minimalHeartColor = "#FF3B5C";
    private string _minimalChartColor = "#FF3B5C";
    private double _minimalTextShadow = 0.6;

    /// <summary>极简模式皮肤：Auto（跟随主题）/ Dark / Light / Neon / Warm / Custom。</summary>
    public string MinimalSkin
    {
        get => _minimalSkin;
        set => _minimalSkin = NormalizeSkin(value);
    }

    /// <summary>极简模式心率数字颜色（#RRGGBB）。</summary>
    public string MinimalTextColor
    {
        get => _minimalTextColor;
        set => _minimalTextColor = NormalizeColor(value, "#FFFFFF");
    }

    /// <summary>极简模式心形颜色（#RRGGBB）。</summary>
    public string MinimalHeartColor
    {
        get => _minimalHeartColor;
        set => _minimalHeartColor = NormalizeColor(value, "#FF3B5C");
    }

    /// <summary>极简模式趋势曲线颜色（#RRGGBB）。</summary>
    public string MinimalChartColor
    {
        get => _minimalChartColor;
        set => _minimalChartColor = NormalizeColor(value, "#FF3B5C");
    }

    /// <summary>极简模式数字阴影强度，范围 0–1（越大在复杂背景上越清晰）。</summary>
    public double MinimalTextShadow
    {
        get => _minimalTextShadow;
        set => _minimalTextShadow = double.IsFinite(value) ? Math.Clamp(value, 0, 1) : 0.6;
    }

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
            WindowTopmost = GetBool(settings, KeyWindowTopmost, true);
            DataRetentionDays = GetInt(settings, KeyRetentionDays, 30);
            MinimalMode = GetBool(settings, KeyMinimalMode, false);
            MinimalShowChart = GetBool(settings, KeyMinimalShowChart, true);
            MinimalHeartSize = GetDouble(settings, KeyMinimalHeartSize, 1.0);
            MinimalBpmFontSize = GetDouble(settings, KeyMinimalBpmFontSize, 56);
            MinimalChartScale = GetDouble(settings, KeyMinimalChartScale, 1.0);
            MinimalSkin = GetString(settings, KeyMinimalSkin) ?? "Dark";
            MinimalTextColor = GetString(settings, KeyMinimalTextColor) ?? "#FFFFFF";
            MinimalHeartColor = GetString(settings, KeyMinimalHeartColor) ?? "#FF3B5C";
            MinimalChartColor = GetString(settings, KeyMinimalChartColor) ?? "#FF3B5C";
            MinimalTextShadow = GetDouble(settings, KeyMinimalTextShadow, 0.6);
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
                [KeyWindowTopmost] = WindowTopmost.ToString(),
                [KeyRetentionDays] = DataRetentionDays.ToString(),
                [KeyMinimalMode] = MinimalMode.ToString(),
                [KeyMinimalShowChart] = MinimalShowChart.ToString(),
                [KeyMinimalHeartSize] = MinimalHeartSize.ToString("F2"),
                [KeyMinimalBpmFontSize] = MinimalBpmFontSize.ToString("F0"),
                [KeyMinimalChartScale] = MinimalChartScale.ToString("F2"),
                [KeyMinimalSkin] = MinimalSkin,
                [KeyMinimalTextColor] = MinimalTextColor,
                [KeyMinimalHeartColor] = MinimalHeartColor,
                [KeyMinimalChartColor] = MinimalChartColor,
                [KeyMinimalTextShadow] = MinimalTextShadow.ToString("F2"),
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
        WindowTopmost = true;
        DataRetentionDays = 30;
        MinimalMode = false;
        MinimalShowChart = true;
        MinimalHeartSize = 1.0;
        MinimalBpmFontSize = 56;
        MinimalChartScale = 1.0;
        MinimalSkin = "Dark";
        MinimalTextColor = "#FFFFFF";
        MinimalHeartColor = "#FF3B5C";
        MinimalChartColor = "#FF3B5C";
        MinimalTextShadow = 0.6;
        WindowLeft = 100;
        WindowTop = 100;
        WindowWidth = 320;
        Widgets = "";
        DisabledPlugins = "";
    }

    private static string NormalizeSkin(string? value)
    {
        return value switch
        {
            "Auto" => "Auto",
            "Light" => "Light",
            "Neon" => "Neon",
            "Warm" => "Warm",
            "Custom" => "Custom",
            _ => "Dark"
        };
    }

    /// <summary>把 #RRGGBB / #AARRGGBB / RRGGBB 归一化成大写 #RRGGBB，非法值回退到默认色。</summary>
    private static string NormalizeColor(string? value, string fallback)
    {
        if (string.IsNullOrWhiteSpace(value)) return fallback;

        var text = value.Trim();
        if (!text.StartsWith('#')) text = "#" + text;

        if (text.Length == 9)
        {
            // 带 alpha 的输入只保留颜色部分。
            text = "#" + text.Substring(3);
        }

        if (text.Length != 7) return fallback;

        for (var i = 1; i < text.Length; i++)
        {
            if (!Uri.IsHexDigit(text[i])) return fallback;
        }

        return text.ToUpperInvariant();
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
