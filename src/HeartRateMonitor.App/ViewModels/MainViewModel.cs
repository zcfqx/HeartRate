using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HeartRateMonitor.App.Extensibility;
using HeartRateMonitor.Core.Enums;
using HeartRateMonitor.Core.Events;
using HeartRateMonitor.Core.Interfaces;
using HeartRateMonitor.Core.Models;
using HeartRateMonitor.Extensibility;

namespace HeartRateMonitor.App.ViewModels;

/// <summary>
/// 主窗口 ViewModel：管理组件看板（增删组件）、连接状态与插件同步。
/// 看板内容完全由插件贡献，布局以组件 Id 列表持久化在设置里。
/// </summary>
public sealed partial class MainViewModel : ObservableObject, IDisposable
{
    private readonly IPluginHost _plugins;
    private readonly PluginContextFactory _contexts;
    private readonly ISettingsService _settings;
    private readonly IHeartRateSession _session;
    private readonly IBleService _ble;
    private readonly IDialogService _dialogs;
    private readonly ILogger _log;
    private readonly DispatcherTimer _timer;
    private bool _disposed;

    [ObservableProperty]
    private bool _isEditMode;

    [ObservableProperty]
    private bool _isAddPanelOpen;

    [ObservableProperty]
    private bool _isEmpty;

    [ObservableProperty]
    private bool _isConnected;

    [ObservableProperty]
    private string _connectionLabel = "未连接";

    [ObservableProperty]
    private string _deviceName = "未连接设备";

    [ObservableProperty]
    private string _sessionDurationText = "00:00";

    [ObservableProperty]
    private string _pluginText = string.Empty;

    [ObservableProperty]
    private ConnectionState _connectionState = ConnectionState.Disconnected;

    /// <summary>极简模式：只显示心跳图标与数字（可选曲线），隐藏顶栏、看板与底栏。</summary>
    [ObservableProperty]
    private bool _isMinimalMode;

    /// <summary>极简模式下是否显示趋势曲线。</summary>
    [ObservableProperty]
    private bool _minimalShowChart = true;

    [ObservableProperty]
    private int _currentBpm;

    [ObservableProperty]
    private string _minimalZoneName = "等待连接设备";

    [ObservableProperty]
    private string _minimalZoneColor = "#6366F1";

    /// <summary>极简模式趋势图数据（随心跳刷新）。</summary>
    public IReadOnlyList<HeartRatePoint> SessionPoints => _session.Points;

    /// <summary>看板上的组件实例（顺序即布局顺序）。</summary>
    public ObservableCollection<WidgetTileViewModel> Tiles { get; } = [];

    /// <summary>组件库里尚未添加到看板的组件。</summary>
    public ObservableCollection<WidgetDescriptor> AvailableWidgets { get; } = [];

    public MainViewModel(
        IPluginHost plugins,
        PluginContextFactory contexts,
        ISettingsService settings,
        IHeartRateSession session,
        IBleService ble,
        IDialogService dialogs,
        ILogger log)
    {
        _plugins = plugins;
        _contexts = contexts;
        _settings = settings;
        _session = session;
        _ble = ble;
        _dialogs = dialogs;
        _log = log;

        _timer = new DispatcherTimer(DispatcherPriority.Background)
        {
            Interval = TimeSpan.FromSeconds(1)
        };
        _timer.Tick += OnTimerTick;
        _timer.Start();

        _plugins.PluginsChanged += OnPluginsChanged;
        _ble.ConnectionStateChanged += OnConnectionStateChanged;
        _session.SessionReset += OnSessionReset;
        _session.SampleReceived += OnSampleReceived;
        _settings.SettingsChanged += OnSettingsChanged;

        IsMinimalMode = _settings.MinimalMode;
        MinimalShowChart = _settings.MinimalShowChart;

        UpdateConnection(_ble.State, _ble.ConnectedDevice?.DeviceName);
        RefreshLayout();
        ApplyMinimalState();
    }

    // === 命令 ===

    [RelayCommand]
    private void ToggleEditMode()
    {
        IsEditMode = !IsEditMode;
        if (!IsEditMode)
        {
            IsAddPanelOpen = false;
        }
    }

    [RelayCommand]
    private void ToggleAddPanel() => IsAddPanelOpen = !IsAddPanelOpen;

    [RelayCommand]
    private void CloseAddPanel() => IsAddPanelOpen = false;

    [RelayCommand]
    private void AddWidget(WidgetDescriptor? descriptor)
    {
        if (descriptor is null)
        {
            return;
        }

        if (Tiles.Any(t => string.Equals(t.Descriptor.Id, descriptor.Id, StringComparison.OrdinalIgnoreCase)))
        {
            IsAddPanelOpen = false;
            return;
        }

        Tiles.Add(CreateTile(descriptor));
        SyncCollections();
        IsAddPanelOpen = false;
        PersistLayout();
    }

    [RelayCommand]
    private void RemoveWidget(WidgetTileViewModel? tile)
    {
        if (tile is null)
        {
            return;
        }

        RemoveById(tile.Descriptor.Id);
    }

    [RelayCommand]
    private void OpenSettings() => _dialogs.ShowSettingsDialog();

    [RelayCommand]
    private void StartNewSession() => _session.Reset();

    [RelayCommand]
    private void ToggleMinimalMode()
    {
        IsMinimalMode = !IsMinimalMode;
        _settings.MinimalMode = IsMinimalMode;
        _ = SaveSettingsAsync();
    }

    [RelayCommand]
    private void ToggleMinimalChart()
    {
        MinimalShowChart = !MinimalShowChart;
        _settings.MinimalShowChart = MinimalShowChart;
        _ = SaveSettingsAsync();
    }

    // === 布局 ===

    private WidgetTileViewModel CreateTile(WidgetDescriptor descriptor)
    {
        var context = (IWidgetContext)_contexts.Create(descriptor.PluginId, () => RemoveById(descriptor.Id));
        var view = descriptor.CreateView(context);
        var pluginName = _plugins.Plugins.FirstOrDefault(p => p.Id == descriptor.PluginId)?.Name ?? "插件";
        return new WidgetTileViewModel(descriptor, view, pluginName);
    }

    private void RefreshLayout()
    {
        var widgets = _plugins.Widgets;
        var defaults = widgets.Where(w => w.DefaultEnabled).Select(w => w.Id);
        var ids = WidgetLayoutCodec.Parse(_settings.Widgets, defaults);

        Tiles.Clear();
        foreach (var id in ids)
        {
            var descriptor = widgets.FirstOrDefault(w => string.Equals(w.Id, id, StringComparison.OrdinalIgnoreCase));
            if (descriptor is not null)
            {
                Tiles.Add(CreateTile(descriptor));
            }
        }

        SyncCollections();
    }

    /// <summary>按布局 Id 列表重建看板（外部设置变化时使用）。</summary>
    private void ApplyLayout(IReadOnlyList<string> ids)
    {
        foreach (var tile in Tiles)
        {
            tile.Dispose();
        }

        Tiles.Clear();
        foreach (var id in ids)
        {
            var descriptor = _plugins.Widgets.FirstOrDefault(w => string.Equals(w.Id, id, StringComparison.OrdinalIgnoreCase));
            if (descriptor is not null)
            {
                Tiles.Add(CreateTile(descriptor));
            }
        }

        SyncCollections();
    }

    private void SyncCollections()
    {
        AvailableWidgets.Clear();
        var current = Tiles.Select(t => t.Descriptor.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var descriptor in _plugins.Widgets.Where(w => !current.Contains(w.Id)))
        {
            AvailableWidgets.Add(descriptor);
        }

        IsEmpty = Tiles.Count == 0;
        PluginText = $"{_plugins.Plugins.Count(p => p.IsEnabled)} 个插件 · {_plugins.Widgets.Count} 个组件";
    }

    private void PersistLayout()
    {
        _settings.Widgets = WidgetLayoutCodec.Serialize(Tiles.Select(t => t.Descriptor.Id));
        _ = SaveSettingsAsync();
    }

    private async Task SaveSettingsAsync()
    {
        try
        {
            await _settings.SaveAsync();
        }
        catch (Exception ex)
        {
            _log.Error("保存布局失败", ex);
        }
    }

    private void RemoveById(string id)
    {
        var tile = Tiles.FirstOrDefault(t => string.Equals(t.Descriptor.Id, id, StringComparison.OrdinalIgnoreCase));
        if (tile is null)
        {
            return;
        }

        Tiles.Remove(tile);
        tile.Dispose();
        SyncCollections();
        PersistLayout();
    }

    // === 事件 ===

    private void OnPluginsChanged(object? sender, EventArgs e) => RunOnUi(() =>
    {
        var widgets = _plugins.Widgets;
        var defaults = widgets.Where(w => w.DefaultEnabled).Select(w => w.Id);
        var desired = WidgetLayoutCodec.Parse(_settings.Widgets, defaults);
        var current = Tiles.Select(t => t.Descriptor.Id).ToList();

        if (!desired.SequenceEqual(current, StringComparer.OrdinalIgnoreCase))
        {
            ApplyLayout(desired);
        }
        else
        {
            SyncCollections();
        }
    });

    private void OnConnectionStateChanged(object? sender, ConnectionStateChangedEventArgs e)
        => RunOnUi(() => UpdateConnection(e.State, e.DeviceName));

    private void OnSessionReset(object? sender, EventArgs e) => RunOnUi(UpdateSessionDuration);

    private void OnSampleReceived(object? sender, HeartRateChangedEventArgs e) => RunOnUi(ApplyMinimalState);

    private void OnSettingsChanged(object? sender, EventArgs e) => RunOnUi(() =>
    {
        if (IsMinimalMode != _settings.MinimalMode)
        {
            IsMinimalMode = _settings.MinimalMode;
        }

        if (MinimalShowChart != _settings.MinimalShowChart)
        {
            MinimalShowChart = _settings.MinimalShowChart;
        }
    });

    /// <summary>刷新极简模式的心率数字、区间与曲线数据。</summary>
    private void ApplyMinimalState()
    {
        var latest = _session.Latest;
        CurrentBpm = latest?.HeartRate ?? 0;

        if (latest is null)
        {
            MinimalZoneName = "等待连接设备";
            MinimalZoneColor = "#6366F1";
        }
        else
        {
            var zone = HeartRateZones.Classify(latest.HeartRate);
            MinimalZoneName = zone.Name;
            MinimalZoneColor = zone.Color;
        }

        OnPropertyChanged(nameof(SessionPoints));
    }

    private void OnTimerTick(object? sender, EventArgs e) => UpdateSessionDuration();

    private void UpdateSessionDuration()
    {
        var duration = _session.Duration;
        SessionDurationText = duration.TotalHours >= 1
            ? duration.ToString(@"h\:mm\:ss")
            : duration.ToString(@"mm\:ss");
    }

    private void UpdateConnection(ConnectionState state, string? deviceName)
    {
        ConnectionState = state;
        IsConnected = state is ConnectionState.Connected;
        DeviceName = string.IsNullOrWhiteSpace(deviceName)
            ? (_settings.LastDeviceName ?? "未连接设备")
            : deviceName!;
        ConnectionLabel = state switch
        {
            ConnectionState.Scanning => "扫描中",
            ConnectionState.Connecting => "连接中",
            ConnectionState.Connected => "已连接",
            ConnectionState.Reconnecting => "重连中",
            _ => "未连接"
        };
    }

    private static void RunOnUi(Action action)
    {
        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher is null || dispatcher.CheckAccess())
        {
            action();
        }
        else
        {
            dispatcher.BeginInvoke(action);
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _timer.Stop();
        _timer.Tick -= OnTimerTick;
        _plugins.PluginsChanged -= OnPluginsChanged;
        _ble.ConnectionStateChanged -= OnConnectionStateChanged;
        _session.SessionReset -= OnSessionReset;
        _session.SampleReceived -= OnSampleReceived;
        _settings.SettingsChanged -= OnSettingsChanged;

        foreach (var tile in Tiles)
        {
            tile.Dispose();
        }

        Tiles.Clear();
    }
}
