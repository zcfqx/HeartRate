using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HeartRateMonitor.App.Extensibility;
using HeartRateMonitor.App.Infrastructure;
using HeartRateMonitor.App.Views.Settings;
using HeartRateMonitor.Core.Enums;
using HeartRateMonitor.Core.Events;
using HeartRateMonitor.Core.Interfaces;
using HeartRateMonitor.Extensibility;

namespace HeartRateMonitor.App.ViewModels;

/// <summary>
/// 设置窗口 ViewModel：属性直接包装 <see cref="ISettingsService"/>，
/// 主题/强调色即时生效，关闭窗口时统一持久化。
/// </summary>
public sealed partial class SettingsViewModel : ObservableObject, IDisposable
{
    private static readonly string[] DefaultAccents =
    [
        "#6366F1", "#8B5CF6", "#EC4899", "#EF4444",
        "#F59E0B", "#22C55E", "#06B6D4", "#3B82F6"
    ];

    private readonly ISettingsService _settings;
    private readonly IBleService _ble;
    private readonly IPluginHost _plugins;
    private readonly IDialogService _dialogs;
    private readonly IDataService _data;
    private readonly ILogger _log;
    private readonly PluginContextFactory _contexts;
    private readonly string _dataDirectory;
    private readonly string _logDirectory;

    [ObservableProperty]
    private SettingsNavItem? _selectedSection;

    public SettingsViewModel(
        ISettingsService settings,
        IBleService ble,
        IPluginHost plugins,
        IDialogService dialogs,
        IDataService data,
        ILogger log,
        PluginContextFactory contexts)
    {
        _settings = settings;
        _ble = ble;
        _plugins = plugins;
        _dialogs = dialogs;
        _data = data;
        _log = log;
        _contexts = contexts;

        var root = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "HeartRateMonitor");
        _dataDirectory = root;
        _logDirectory = Path.Combine(root, "logs");

        _ble.ConnectionStateChanged += OnConnectionStateChanged;

        BuildSections();
        RebuildPlugins();
        UpdateConnection();
    }

    // === 外观 ===

    public IReadOnlyList<string> AccentPresets => DefaultAccents;

    public bool IsLightTheme
    {
        get => string.Equals(_settings.Theme, "Light", StringComparison.OrdinalIgnoreCase);
        set
        {
            var theme = value ? "Light" : "Dark";
            if (string.Equals(_settings.Theme, theme, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            _settings.Theme = theme;
            ThemeManager.Apply(theme, _settings.AccentColor);
            OnPropertyChanged();
        }
    }

    public string AccentColor => _settings.AccentColor;

    public double OverlayOpacity
    {
        get => _settings.OverlayOpacity;
        set
        {
            if (Math.Abs(_settings.OverlayOpacity - value) < 0.001)
            {
                return;
            }

            _settings.OverlayOpacity = value;
            OnPropertyChanged();
        }
    }

    public double WindowWidth
    {
        get => _settings.WindowWidth;
        set
        {
            if (Math.Abs(_settings.WindowWidth - value) < 0.5)
            {
                return;
            }

            _settings.WindowWidth = value;
            OnPropertyChanged();
        }
    }

    public bool MinimizeToTray
    {
        get => _settings.MinimizeToTray;
        set
        {
            if (_settings.MinimizeToTray == value)
            {
                return;
            }

            _settings.MinimizeToTray = value;
            OnPropertyChanged();
        }
    }

    public bool StartWithWindows
    {
        get => _settings.StartWithWindows;
        set
        {
            if (_settings.StartWithWindows == value)
            {
                return;
            }

            _settings.StartWithWindows = value;
            OnPropertyChanged();
        }
    }

    /// <summary>极简模式：只显示心跳图标与数字。</summary>
    public bool MinimalMode
    {
        get => _settings.MinimalMode;
        set
        {
            if (_settings.MinimalMode == value)
            {
                return;
            }

            _settings.MinimalMode = value;
            OnPropertyChanged();
        }
    }

    /// <summary>极简模式下是否显示趋势曲线。</summary>
    public bool MinimalShowChart
    {
        get => _settings.MinimalShowChart;
        set
        {
            if (_settings.MinimalShowChart == value)
            {
                return;
            }

            _settings.MinimalShowChart = value;
            OnPropertyChanged();
        }
    }

    // === 连接 ===

    public bool AutoConnect
    {
        get => _settings.AutoConnect;
        set
        {
            if (_settings.AutoConnect == value)
            {
                return;
            }

            _settings.AutoConnect = value;
            OnPropertyChanged();
        }
    }

    [ObservableProperty]
    private string _connectionLabel = "未连接";

    [ObservableProperty]
    private string _connectedDeviceName = "未连接设备";

    // === 提醒 ===

    public bool EnableNotifications
    {
        get => _settings.EnableNotifications;
        set
        {
            if (_settings.EnableNotifications == value)
            {
                return;
            }

            _settings.EnableNotifications = value;
            OnPropertyChanged();
        }
    }

    public bool EnableSoundAlert
    {
        get => _settings.EnableSoundAlert;
        set
        {
            if (_settings.EnableSoundAlert == value)
            {
                return;
            }

            _settings.EnableSoundAlert = value;
            OnPropertyChanged();
        }
    }

    public double HighHeartRateThreshold
    {
        get => _settings.HighHeartRateThreshold;
        set
        {
            var v = (int)Math.Round(value);
            if (_settings.HighHeartRateThreshold == v)
            {
                return;
            }

            _settings.HighHeartRateThreshold = v;
            OnPropertyChanged();
        }
    }

    public double LowHeartRateThreshold
    {
        get => _settings.LowHeartRateThreshold;
        set
        {
            var v = (int)Math.Round(value);
            if (_settings.LowHeartRateThreshold == v)
            {
                return;
            }

            _settings.LowHeartRateThreshold = v;
            OnPropertyChanged();
        }
    }

    // === 数据 ===

    public double DataRetentionDays
    {
        get => _settings.DataRetentionDays;
        set
        {
            var v = (int)Math.Round(value);
            if (_settings.DataRetentionDays == v)
            {
                return;
            }

            _settings.DataRetentionDays = v;
            OnPropertyChanged();
        }
    }

    public string DataLocation => _dataDirectory;

    public string LogLocation => _logDirectory;

    public string PluginsLocation => _plugins.PluginsDirectory;

    // === 插件 / 关于 ===

    public ObservableCollection<SettingsNavItem> Sections { get; } = [];

    public ObservableCollection<PluginItemViewModel> Plugins { get; } = [];

    public string Version { get; } = typeof(SettingsViewModel).Assembly.GetName().Version?.ToString(3) ?? "1.0.0";

    public string PluginSummary => $"已启用 {_plugins.Plugins.Count(p => p.IsEnabled)} / {_plugins.Plugins.Count} 个插件，共贡献 {_plugins.Widgets.Count} 个组件";

    // === 命令 ===

    [RelayCommand]
    private void SetAccentColor(string? hex)
    {
        if (string.IsNullOrWhiteSpace(hex))
        {
            return;
        }

        _settings.AccentColor = hex.Trim();
        ThemeManager.Apply(_settings.Theme, _settings.AccentColor);
        OnPropertyChanged(nameof(AccentColor));
    }

    [RelayCommand]
    private async Task SaveCommand() => await SaveAsync();

    [RelayCommand]
    private async Task ResetAllAsync()
    {
        if (!_dialogs.Confirm("确定恢复默认设置？主题、窗口与插件布局都会重置。", "恢复默认"))
        {
            return;
        }

        _settings.ResetToDefaults();
        ThemeManager.Apply("Dark", ThemeManager.DefaultAccent);
        OnPropertyChanged(string.Empty);
        await SaveAsync();
    }

    [RelayCommand]
    private async Task ReconnectAsync()
    {
        try
        {
            var ok = await _ble.AutoReconnectAsync();
            if (!ok)
            {
                _dialogs.ShowMessage("自动重连失败，请在主窗口的设备组件中重新扫描并连接。", "连接");
            }
        }
        catch (Exception ex)
        {
            _log.Error("重连失败", ex);
            _dialogs.ShowMessage($"重连失败：{ex.Message}", "连接");
        }
    }

    [RelayCommand]
    private async Task DisconnectAsync()
    {
        try
        {
            await _ble.DisconnectAsync();
        }
        catch (Exception ex)
        {
            _log.Error("断开连接失败", ex);
        }
    }

    [RelayCommand]
    private void OpenBluetoothSettings() => OpenPath("ms-settings:bluetooth");

    [RelayCommand]
    private void OpenDataFolder() => OpenPath(_dataDirectory);

    [RelayCommand]
    private void OpenLogFolder() => OpenPath(_logDirectory);

    [RelayCommand]
    private void OpenPluginsFolder() => OpenPath(_plugins.PluginsDirectory);

    [RelayCommand]
    private async Task RescanPluginsAsync()
    {
        try
        {
            await _plugins.RescanAsync();
            RebuildPlugins();
            OnPropertyChanged(nameof(PluginSummary));
        }
        catch (Exception ex)
        {
            _log.Error("重新扫描插件失败", ex);
            _dialogs.ShowMessage($"重新扫描插件失败：{ex.Message}", "插件");
        }
    }

    [RelayCommand]
    private async Task CleanupDataAsync()
    {
        try
        {
            await _data.CleanupOldDataAsync(TimeSpan.FromDays(Math.Max(1, _settings.DataRetentionDays)));
            _dialogs.ShowMessage($"已清理 {_settings.DataRetentionDays} 天之前的历史心率数据。", "数据");
        }
        catch (Exception ex)
        {
            _log.Error("清理数据失败", ex);
            _dialogs.ShowMessage($"清理数据失败：{ex.Message}", "数据");
        }
    }

    // === 内部 ===

    public async Task SaveAsync()
    {
        try
        {
            await _settings.SaveAsync();
        }
        catch (Exception ex)
        {
            _log.Error("保存设置失败", ex);
            _dialogs.ShowMessage($"保存设置失败：{ex.Message}", "设置");
        }
    }

    /// <summary>放弃未保存的修改：从存储重新加载设置并刷新全部绑定。</summary>
    public async Task DiscardChangesAsync()
    {
        try
        {
            await _settings.LoadAsync();
            ThemeManager.Apply(_settings.Theme, _settings.AccentColor);
            OnPropertyChanged(string.Empty);
        }
        catch (Exception ex)
        {
            _log.Error("放弃设置修改失败", ex);
        }
    }

    private void BuildSections()
    {
        Sections.Clear();
        AddSection("外观", "palette", new AppearancePage());
        AddSection("连接", "device", new ConnectionPage());
        AddSection("提醒", "bell", new NotificationPage());
        AddSection("数据", "stats", new DataPage());

        foreach (var descriptor in _plugins.SettingsSections)
        {
            try
            {
                var view = descriptor.CreateView(_contexts.Create(descriptor.PluginId));
                AddSection(descriptor.Title, descriptor.Icon, view);
            }
            catch (Exception ex)
            {
                _log.Error($"创建插件设置页失败: {descriptor.Id}", ex);
            }
        }

        AddSection("功能插件", "puzzle", new PluginsPage());
        AddSection("关于", "info", new AboutPage());

        SelectedSection = Sections.FirstOrDefault();
    }

    private void AddSection(string title, string iconKey, FrameworkElement view)
    {
        view.DataContext = this;
        Sections.Add(new SettingsNavItem { Title = title, IconKey = iconKey, View = view });
    }

    private void RebuildPlugins()
    {
        Plugins.Clear();
        foreach (var info in _plugins.Plugins)
        {
            Plugins.Add(new PluginItemViewModel(info, _plugins, _dialogs, _log));
        }
    }

    private void OnConnectionStateChanged(object? sender, ConnectionStateChangedEventArgs e)
    {
        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher is null || dispatcher.CheckAccess())
        {
            UpdateConnection();
        }
        else
        {
            dispatcher.BeginInvoke(UpdateConnection);
        }
    }

    private void UpdateConnection()
    {
        ConnectedDeviceName = _ble.ConnectedDevice?.DeviceName
            ?? _settings.LastDeviceName
            ?? "未连接设备";
        ConnectionLabel = _ble.State switch
        {
            ConnectionState.Scanning => "扫描中",
            ConnectionState.Connecting => "连接中",
            ConnectionState.Connected => "已连接",
            ConnectionState.Reconnecting => "重连中",
            _ => "未连接"
        };
    }

    private void OpenPath(string path)
    {
        try
        {
            if (path.StartsWith("ms-settings:", StringComparison.OrdinalIgnoreCase))
            {
                Process.Start(new ProcessStartInfo { FileName = path, UseShellExecute = true });
                return;
            }

            Directory.CreateDirectory(path);
            Process.Start(new ProcessStartInfo { FileName = path, UseShellExecute = true });
        }
        catch (Exception ex)
        {
            _log.Error($"打开路径失败: {path}", ex);
            _dialogs.ShowMessage($"无法打开：{path}\n{ex.Message}", "提示");
        }
    }

    public void Dispose() => _ble.ConnectionStateChanged -= OnConnectionStateChanged;
}
