using System.Collections.ObjectModel;
using System.Windows;
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
using HeartRateMonitor.App.Views;

namespace HeartRateMonitor.App.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private readonly IHeartRateService _heartRateService;
    private readonly IBleService _bleService;
    private readonly ISettingsService _settingsService;
    private readonly ILogger _logger;

    private readonly Dispatcher _dispatcher;
    private readonly ObservableCollection<DateTimePoint> _heartRateValues = new();
    private readonly ObservableCollection<DateTimePoint> _rrValues = new();

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
    private string _zoneColor = "#6366F1";

    [ObservableProperty]
    private bool _isSensorContact;

    [ObservableProperty]
    private double _avgRrInterval;

    [ObservableProperty]
    private bool _isConnected;

    [ObservableProperty]
    private bool _isScanning;

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

    public ISeries[] HeartRateSeries { get; }
    public ISeries[] RrSeries { get; }

    public Axis[] XAxes { get; } =
    {
        new Axis
        {
            Labeler = value => new DateTime((long)value).ToString("HH:mm:ss"),
            UnitWidth = TimeSpan.FromSeconds(1).Ticks,
            LabelsPaint = new SolidColorPaint(SKColors.WhiteSmoke),
            SeparatorsPaint = new SolidColorPaint(SKColors.WhiteSmoke),
            AnimationsSpeed = TimeSpan.FromMilliseconds(0)
        }
    };

    public Axis[] YAxes { get; } =
    {
        new Axis
        {
            MinLimit = 40,
            MaxLimit = 220,
            LabelsPaint = new SolidColorPaint(SKColors.WhiteSmoke),
            SeparatorsPaint = new SolidColorPaint(SKColors.WhiteSmoke)
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

        OverlayOpacity = _settingsService.OverlayOpacity;

        _ = LoadDeviceInfoAsync();
    }

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

    [RelayCommand]
    private void ToggleMinimalMode()
    {
        IsMinimalMode = !IsMinimalMode;
    }

    [RelayCommand]
    private void ShowDevicePicker()
    {
        var picker = App.Services?.GetService(typeof(DevicePickerWindow)) as DevicePickerWindow;
        if (picker != null)
        {
            picker.Owner = Application.Current.MainWindow;
            picker.ShowDialog();
        }
    }

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
            await _bleService.StartScanningAsync();
            IsScanning = true;
        }
        catch (Exception ex)
        {
            _logger.Error("启动扫描失败", ex);
            IsScanning = false;
            UpdateState(ConnectionState.Disconnected);
        }
    }

    private async Task StopScanAsync()
    {
        try
        {
            await _bleService.StopScanningAsync();
            IsScanning = false;
        }
        catch (Exception ex)
        {
            _logger.Error("停止扫描失败", ex);
        }
    }

    [RelayCommand]
    private async Task ConnectAsync(BleDevice? device)
    {
        if (device == null) return;

        try
        {
            await StopScanAsync();

            _logger.Info($"正在连接设备: {device.DeviceName}");
            await _bleService.ConnectAsync(device);

            _settingsService.LastDeviceId = device.DeviceId;
            await _settingsService.SaveAsync();
        }
        catch (Exception ex)
        {
            _logger.Error("连接设备失败", ex);
            _dispatcher.Invoke(() =>
            {
                MessageBox.Show($"连接设备失败: {ex.Message}", "连接错误",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
            });
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

    private void OnHeartRateReceived(object? sender, HeartRateChangedEventArgs e)
    {
        _dispatcher.Invoke(() =>
        {
            var data = e.Data;
            CurrentHeartRate = data.HeartRate;
            BpmText = data.HeartRate.ToString();
            RrInterval = data.RRInterval > 0 ? $"{data.RRInterval:F1} ms" : "0 ms";
            IsSensorContact = data.IsSensorContact;
            LastUpdate = data.Timestamp.ToString("HH:mm:ss");

            UpdateZone(data.HeartRate);

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

    private void UpdateZone(int heartRate)
    {
        (HeartRateZone, ZoneColor) = heartRate switch
        {
            < 100 => ("静息", "#6366F1"),
            < 140 => ("燃脂", "#22C55E"),
            < 170 => ("有氧", "#F59E0B"),
            < 200 => ("极限", "#F97316"),
            _ => ("最大", "#EF4444")
        };
    }

    private async Task LoadDeviceInfoAsync()
    {
        try
        {
            var endTime = DateTime.Now;
            var startTime = endTime.AddHours(-1);
            var stats = await _heartRateService.GetStatisticsAsync(startTime, endTime);
            MinHeartRate = stats.MinHeartRate;
            MaxHeartRate = stats.MaxHeartRate;
            AvgHeartRate = stats.AverageHeartRate;
        }
        catch (Exception ex)
        {
            _logger.Error("加载设备信息失败", ex);
        }
    }

    [RelayCommand]
    private void NavigateToSettings()
    {
        var settingsWindow = App.Services?.GetService(typeof(SettingsWindow)) as SettingsWindow;
        if (settingsWindow != null)
        {
            settingsWindow.Owner = Application.Current.MainWindow;
            settingsWindow.ShowDialog();
        }
    }

    [RelayCommand]
    private async Task RequestPermissionsAsync()
    {
        var granted = await _bleService.RequestPermissionAsync();
        if (!granted)
        {
            _dispatcher.Invoke(() =>
            {
                MessageBox.Show("需要蓝牙权限才能扫描设备", "权限请求",
                    MessageBoxButton.OK, MessageBoxImage.Information);
            });
        }
    }

    [RelayCommand]
    private async Task StopScanningCommandAsync()
    {
        await StopScanAsync();
    }

    public void Cleanup()
    {
        _bleService.HeartRateReceived -= OnHeartRateReceived;
        _bleService.ConnectionStateChanged -= OnConnectionStateChanged;
        _bleService.DeviceDiscovered -= OnDeviceDiscovered;
    }
}
