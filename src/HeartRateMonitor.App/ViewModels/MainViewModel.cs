using System.Collections.ObjectModel;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LiveChartsCore;
using LiveChartsCore.Defaults;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using SkiaSharp;
using HeartRateMonitor.Core.Enums;
using HeartRateMonitor.Core.Events;
using HeartRateMonitor.Core.Interfaces;
using HeartRateMonitor.Core.Models;

namespace HeartRateMonitor.App.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private const string ZoneColorRest = "#6366F1";
    private const string ZoneColorFatBurn = "#22C55E";
    private const string ZoneColorCardio = "#F59E0B";
    private const string ZoneColorPeak = "#F97316";
    private const string ZoneColorMax = "#EF4444";

    private readonly IHeartRateService _heartRateService;
    private readonly IBleService _bleService;
    private readonly ISettingsService _settingsService;
    private readonly ILogger _logger;

    private readonly Dispatcher _dispatcher;
    private readonly ObservableCollection<DateTimePoint> _heartRateValues = new();
    private readonly ObservableCollection<DateTimePoint> _rrValues = new();

    private int _runningMin = int.MaxValue;
    private int _runningMax = int.MinValue;
    private double _runningSum;
    private int _runningCount;

    // ==================== 心率数据 ====================

    [ObservableProperty]
    private int _currentHeartRate;

    [ObservableProperty]
    private int _minHeartRate;

    [ObservableProperty]
    private int _maxHeartRate;

    [ObservableProperty]
    private double _avgHeartRate;

    [ObservableProperty]
    private string _rrInterval = "0 ms";

    [ObservableProperty]
    private string _connectionStatus = "未连接";

    [ObservableProperty]
    private string _connectionStatusIcon = "未连接";

    [ObservableProperty]
    private string _signalStrength = "无";

    [ObservableProperty]
    private string _lastUpdate = "00:00:00";

    [ObservableProperty]
    private string _heartRateZone = "静息";

    [ObservableProperty]
    private string _zoneColor = ZoneColorRest;

    [ObservableProperty]
    private bool _isSensorContact;

    [ObservableProperty]
    private double _avgRrInterval;

    [ObservableProperty]
    private bool _isConnected;

    [ObservableProperty]
    private bool _isScanning;

    [ObservableProperty]
    private string _scanStatusText = "未扫描";

    [ObservableProperty]
    private bool _isMinimalMode;

    [ObservableProperty]
    private string _bpmText = "---";

    [ObservableProperty]
    private string _deviceName = "";

    [ObservableProperty]
    private double _overlayOpacity = 0.7;

    [ObservableProperty]
    private BleDevice? _connectedDevice;

    [ObservableProperty]
    private ObservableCollection<BleDevice> _discoveredDevices = new();

    // ==================== 面板状态 ====================

    [ObservableProperty]
    private string _activePanel = "None"; // None, Device, Settings

    [ObservableProperty]
    private bool _isDevicePanelOpen;

    [ObservableProperty]
    private bool _isSettingsPanelOpen;

    [ObservableProperty]
    private bool _isHeartRatePanelOpen = true;

    // ==================== 设置属性 ====================

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
    private bool _startWithWindows;

    [ObservableProperty]
    private bool _minimizeToTray = true;

    [ObservableProperty]
    private int _dataRetentionDays = 30;

    // ==================== 图表 ====================

    public ISeries[] HeartRateSeries { get; }
    public ISeries[] RrSeries { get; }

    public Axis[] XAxes { get; } =
    {
        new Axis
        {
            IsVisible = false
        }
    };

    public Axis[] YAxes { get; } =
    {
        new Axis
        {
            IsVisible = false,
            MinLimit = 40,
            MaxLimit = 120
        }
    };

    public MainViewModel(
        IHeartRateService heartRateService,
        IBleService bleService,
        ISettingsService settingsService,
        ILogger logger)
    {
        _heartRateService = heartRateService;
        _bleService = bleService;
        _settingsService = settingsService;
        _logger = logger;

        _dispatcher = Dispatcher.CurrentDispatcher;

        HeartRateSeries = new ISeries[]
        {
            new LineSeries<DateTimePoint>
            {
                Values = _heartRateValues,
                Fill = null,
                Stroke = new SolidColorPaint(SKColors.CornflowerBlue, 2),
                GeometryStroke = null,
                GeometryFill = null,
                LineSmoothness = 0.5
            }
        };

        RrSeries = new ISeries[]
        {
            new LineSeries<DateTimePoint>
            {
                Values = _rrValues,
                Fill = null,
                Stroke = new SolidColorPaint(SKColors.MediumPurple, (float)1.5),
                GeometryStroke = null,
                GeometryFill = null,
                LineSmoothness = 0.3
            }
        };

        _bleService.HeartRateReceived += OnHeartRateReceived;
        _bleService.ConnectionStateChanged += OnConnectionStateChanged;
        _bleService.DeviceDiscovered += OnDeviceDiscovered;
        _settingsService.SettingsChanged += OnSettingsChanged;

        OverlayOpacity = _settingsService.OverlayOpacity;
        IsMinimalMode = _settingsService.MinimalMode;
        LoadSettingsFromService();

        _ = LoadDeviceInfoAsync();
    }

    // ==================== 设备发现 ====================

    private void OnDeviceDiscovered(object? sender, BleDevice device)
    {
        _dispatcher.Invoke(() =>
        {
            if (DiscoveredDevices.All(d => d.DeviceId != device.DeviceId))
            {
                DiscoveredDevices.Add(device);
            }
        });
    }

    // ==================== 面板切换 ====================

    [RelayCommand]
    private void ToggleDevicePanel()
    {
        if (ActivePanel == "Device")
        {
            ActivePanel = "None";
            IsDevicePanelOpen = false;
            IsHeartRatePanelOpen = true;
        }
        else
        {
            ActivePanel = "Device";
            IsDevicePanelOpen = true;
            IsSettingsPanelOpen = false;
            IsHeartRatePanelOpen = false;
        }
    }

    [RelayCommand]
    private void ToggleSettingsPanel()
    {
        if (ActivePanel == "Settings")
        {
            ActivePanel = "None";
            IsSettingsPanelOpen = false;
            IsHeartRatePanelOpen = true;
        }
        else
        {
            ActivePanel = "Settings";
            IsSettingsPanelOpen = true;
            IsDevicePanelOpen = false;
            IsHeartRatePanelOpen = false;
        }
    }

    [RelayCommand]
    private void ShowHeartRatePanel()
    {
        ActivePanel = "None";
        IsDevicePanelOpen = false;
        IsSettingsPanelOpen = false;
        IsHeartRatePanelOpen = true;
    }

    // ==================== 极简模式 ====================

    [RelayCommand]
    private void ToggleMinimalMode()
    {
        IsMinimalMode = !IsMinimalMode;
    }

    partial void OnIsMinimalModeChanged(bool value)
    {
        _settingsService.MinimalMode = value;
        _ = _settingsService.SaveAsync();
    }

    // ==================== 扫描 ====================

    [RelayCommand]
    private async Task ToggleScanAsync()
    {
        if (IsScanning)
        {
            await StopScanAsync();
        }
        else
        {
            await StartScanAsync();
        }
    }

    private async Task StartScanAsync()
    {
        try
        {
            DiscoveredDevices.Clear();
            ScanStatusText = "扫描中...";
            await _bleService.StartScanningAsync();
            IsScanning = true;
            ScanStatusText = "扫描中...";
        }
        catch (Exception ex)
        {
            _logger.Error("启动扫描失败", ex);
            IsScanning = false;
            ScanStatusText = $"扫描失败: {ex.Message}";
            UpdateState(ConnectionState.Disconnected);
        }
    }

    private async Task StopScanAsync()
    {
        try
        {
            await _bleService.StopScanningAsync();
            IsScanning = false;
            ScanStatusText = "已停止";
        }
        catch (Exception ex)
        {
            _logger.Error("停止扫描失败", ex);
            ScanStatusText = $"停止失败: {ex.Message}";
        }
    }

    // ==================== 连接 ====================

    [RelayCommand]
    public async Task ConnectAsync(BleDevice? device)
    {
        if (device == null) return;

        try
        {
            await StopScanAsync();

            _logger.Info($"正在连接设备: {device.DeviceName}");
            await _bleService.ConnectAsync(device);

            _settingsService.LastDeviceId = device.DeviceId;
            await _settingsService.SaveAsync();

            // 连接成功后关闭设备面板
            _dispatcher.Invoke(() =>
            {
                ActivePanel = "None";
                IsDevicePanelOpen = false;
            });
        }
        catch (Exception ex)
        {
            _logger.Error("连接设备失败", ex);
        }
    }

    [RelayCommand]
    private async Task DisconnectAsync()
    {
        try
        {
            await _bleService.DisconnectAsync();
        }
        catch (Exception ex)
        {
            _logger.Error("断开连接失败", ex);
        }
    }

    // ==================== 心率数据处理 ====================

    private void OnHeartRateReceived(object? sender, HeartRateChangedEventArgs e)
    {
        var data = e.Data;

        _runningMin = Math.Min(_runningMin, data.HeartRate);
        _runningMax = Math.Max(_runningMax, data.HeartRate);
        _runningSum += data.HeartRate;
        _runningCount++;

        _dispatcher.Invoke(() =>
        {
            CurrentHeartRate = data.HeartRate;
            BpmText = data.HeartRate.ToString();
            RrInterval = data.RRInterval > 0 ? $"{data.RRInterval:F1} ms" : "0 ms";
            IsSensorContact = data.IsSensorContact;
            LastUpdate = data.Timestamp.ToString("HH:mm:ss");

            UpdateZone(data.HeartRate);

            MinHeartRate = _runningMin;
            MaxHeartRate = _runningMax;
            AvgHeartRate = _runningCount > 0 ? Math.Round(_runningSum / _runningCount, 1) : 0;

            UpdateChartAxis(data.HeartRate);

            var point = new DateTimePoint(data.Timestamp, data.HeartRate);
            _heartRateValues.Add(point);
            if (_heartRateValues.Count > 120)
                _heartRateValues.RemoveAt(0);

            if (data.RRInterval > 0)
            {
                var rrPoint = new DateTimePoint(data.Timestamp, data.RRInterval);
                _rrValues.Add(rrPoint);
                if (_rrValues.Count > 60)
                    _rrValues.RemoveAt(0);
            }
        });

        _heartRateService.UpdateHeartRate(e.Data);
    }

    private void OnConnectionStateChanged(object? sender, ConnectionStateChangedEventArgs e)
    {
        _dispatcher.Invoke(() => UpdateState(e.State));

        if (e.State == ConnectionState.Connected)
        {
            ResetRunningStatistics();
            _ = LoadDeviceInfoAsync();
        }
    }

    private void ResetRunningStatistics()
    {
        _runningMin = int.MaxValue;
        _runningMax = int.MinValue;
        _runningSum = 0;
        _runningCount = 0;
    }

    private void UpdateState(ConnectionState state)
    {
        ConnectionStatus = state switch
        {
            ConnectionState.Disconnected => "未连接",
            ConnectionState.Scanning => "扫描中...",
            ConnectionState.Connecting => "连接中...",
            ConnectionState.Connected => "已连接",
            ConnectionState.Reconnecting => "重连中...",
            _ => "未知"
        };

        ConnectionStatusIcon = state switch
        {
            ConnectionState.Disconnected => "未连接",
            ConnectionState.Scanning => "扫描中",
            ConnectionState.Connecting => "连接中",
            ConnectionState.Connected => "已连接",
            ConnectionState.Reconnecting => "重连中",
            _ => "未知"
        };

        IsConnected = state == ConnectionState.Connected;
        IsScanning = state == ConnectionState.Scanning;
        ConnectedDevice = _bleService.ConnectedDevice;
        DeviceName = ConnectedDevice?.DeviceName ?? "";
        SignalStrength = ConnectedDevice != null ? $"{ConnectedDevice.SignalStrength} dBm" : "无";
    }

    private void UpdateChartAxis(int heartRate)
    {
        var axis = YAxes[0];
        var min = (int)Math.Floor(Math.Min(_runningMin, heartRate) / 10.0) * 10 - 10;
        var max = (int)Math.Ceiling(Math.Max(_runningMax, heartRate) / 10.0) * 10 + 10;
        min = Math.Max(min, 0);
        max = Math.Max(max, min + 20);
        axis.MinLimit = min;
        axis.MaxLimit = max;
    }

    private void UpdateZone(int heartRate)
    {
        (HeartRateZone, ZoneColor) = heartRate switch
        {
            < 100 => ("静息", ZoneColorRest),
            < 140 => ("燃脂", ZoneColorFatBurn),
            < 170 => ("有氧", ZoneColorCardio),
            < 200 => ("极限", ZoneColorPeak),
            _ => ("最大", ZoneColorMax)
        };
    }

    private async Task LoadDeviceInfoAsync()
    {
        try
        {
            var endTime = DateTime.Now;
            var startTime = endTime.AddHours(-1);
            var stats = await _heartRateService.GetStatisticsAsync(startTime, endTime);

            _dispatcher.Invoke(() =>
            {
                MinHeartRate = stats.MinHeartRate;
                MaxHeartRate = stats.MaxHeartRate;
                AvgHeartRate = stats.AverageHeartRate;
            });

            _runningMin = stats.MinHeartRate == 0 ? int.MaxValue : stats.MinHeartRate;
            _runningMax = stats.MaxHeartRate == 0 ? int.MinValue : stats.MaxHeartRate;
            _runningSum = 0;
            _runningCount = 0;
        }
        catch (Exception ex)
        {
            _logger.Error("加载设备信息失败", ex);
        }
    }

    // ==================== 设置 ====================

    private void LoadSettingsFromService()
    {
        AutoConnect = _settingsService.AutoConnect;
        HighHeartRateThreshold = _settingsService.HighHeartRateThreshold;
        LowHeartRateThreshold = _settingsService.LowHeartRateThreshold;
        EnableNotifications = _settingsService.EnableNotifications;
        EnableSoundAlert = _settingsService.EnableSoundAlert;
        StartWithWindows = _settingsService.StartWithWindows;
        MinimizeToTray = _settingsService.MinimizeToTray;
        DataRetentionDays = _settingsService.DataRetentionDays;
    }

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

    [RelayCommand]
    private async Task SaveSettingsAsync()
    {
        try
        {
            _settingsService.AutoConnect = AutoConnect;
            _settingsService.HighHeartRateThreshold = HighHeartRateThreshold;
            _settingsService.LowHeartRateThreshold = LowHeartRateThreshold;
            _settingsService.EnableNotifications = EnableNotifications;
            _settingsService.EnableSoundAlert = EnableSoundAlert;
            _settingsService.StartWithWindows = StartWithWindows;
            _settingsService.MinimizeToTray = MinimizeToTray;
            _settingsService.DataRetentionDays = DataRetentionDays;

            await _settingsService.SaveAsync();
            _settingsService.NotifySettingsChanged();
            _logger.Info("设置已保存");

            // 关闭设置面板
            _dispatcher.Invoke(() =>
            {
                ActivePanel = "None";
                IsSettingsPanelOpen = false;
            });
        }
        catch (Exception ex)
        {
            _logger.Error("保存设置失败", ex);
        }
    }

    [RelayCommand]
    private void ResetSettings()
    {
        _settingsService.ResetToDefaults();
        LoadSettingsFromService();
    }

    // ==================== 权限 ====================

    [RelayCommand]
    private async Task RequestPermissionsAsync()
    {
        var granted = await _bleService.RequestPermissionAsync();
        if (!granted)
        {
            _logger.Warning("需要蓝牙权限才能扫描设备");
        }
    }

    // ==================== 清理 ====================

    private void OnSettingsChanged(object? sender, EventArgs e)
    {
        _dispatcher.Invoke(() =>
        {
            OverlayOpacity = _settingsService.OverlayOpacity;
            IsMinimalMode = _settingsService.MinimalMode;
        });
    }

    public void Cleanup()
    {
        _bleService.HeartRateReceived -= OnHeartRateReceived;
        _bleService.ConnectionStateChanged -= OnConnectionStateChanged;
        _bleService.DeviceDiscovered -= OnDeviceDiscovered;
        _settingsService.SettingsChanged -= OnSettingsChanged;
    }
}
