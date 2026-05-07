using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HeartRateMonitor.Core.Enums;
using HeartRateMonitor.Core.Events;
using HeartRateMonitor.Core.Interfaces;
using HeartRateMonitor.Core.Models;
using LiveChartsCore;
using LiveChartsCore.Defaults;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using SkiaSharp;

namespace HeartRateMonitor.App.ViewModels;

public partial class MainViewModel : ObservableObject, IDisposable
{
    private readonly IBleService _bleService;
    private readonly IHeartRateService _heartRateService;
    private readonly ISettingsService _settingsService;
    private readonly IDataService _dataService;
    private readonly ILogger _logger;
    private readonly Dispatcher _dispatcher;

    private readonly ObservableCollection<DateTimePoint> _heartRatePoints = [];
    private readonly List<int> _recentRates = [];
    private const int MaxChartPoints = 120;

    [ObservableProperty]
    private int _currentHeartRate;

    [ObservableProperty]
    private string _connectionStatus = "未连接";

    [ObservableProperty]
    private string _connectionStatusIcon = "未连接";

    [ObservableProperty]
    private string _deviceName = "--";

    [ObservableProperty]
    private string _bpmText = "--";

    [ObservableProperty]
    private string _lastUpdate = "--";

    [ObservableProperty]
    private int _averageHeartRate;

    [ObservableProperty]
    private int _maxHeartRate;

    [ObservableProperty]
    private int _minHeartRate;

    [ObservableProperty]
    private double _rRInterval;

    [ObservableProperty]
    private string _signalStrength = "无";

    [ObservableProperty]
    private bool _isConnected;

    [ObservableProperty]
    private bool _isScanning;

    [ObservableProperty]
    private string _heartRateColor = "#FFFFFF";

    [ObservableProperty]
    private string _heartRateZone = "静息";

    [ObservableProperty]
    private string _zoneColor = "#6366F1";

    [ObservableProperty]
    private double _minChartValue = 40;

    [ObservableProperty]
    private double _maxChartValue = 180;

    public ISeries[] HeartRateSeries { get; }
    public Axis[] XAxes { get; }
    public Axis[] YAxes { get; }

    public ObservableCollection<BleDevice> DiscoveredDevices { get; } = [];

    public ICommand ConnectCommand { get; }
    public ICommand DisconnectCommand { get; }
    public ICommand StartScanCommand { get; }
    public ICommand StopScanCommand { get; }
    public ICommand ToggleScanCommand { get; }

    public MainViewModel(
        IBleService bleService,
        IHeartRateService heartRateService,
        ISettingsService settingsService,
        IDataService dataService,
        ILogger logger)
    {
        _bleService = bleService;
        _heartRateService = heartRateService;
        _settingsService = settingsService;
        _dataService = dataService;
        _logger = logger;
        _dispatcher = Application.Current.Dispatcher;

        _bleService.HeartRateReceived += OnHeartRateReceived;
        _bleService.ConnectionStateChanged += OnConnectionStateChanged;

        HeartRateSeries =
        [
            new LineSeries<DateTimePoint>
            {
                Values = _heartRatePoints,
                Stroke = new SolidColorPaint(new SKColor(239, 68, 68), 2),
                Fill = new SolidColorPaint(new SKColor(239, 68, 68, 50)),
                GeometryFill = null,
                GeometryStroke = null,
                LineSmoothness = 0.5
            }
        ];

        XAxes =
        [
            new Axis
            {
                IsVisible = false,
                Labeler = value => new DateTime((long)value).ToString("HH:mm:ss")
            }
        ];

        YAxes =
        [
            new Axis
            {
                IsVisible = false,
                MinLimit = 40,
                MaxLimit = 180
            }
        ];

        ConnectCommand = new AsyncRelayCommand<BleDevice?>(ConnectAsync);
        DisconnectCommand = new AsyncRelayCommand(DisconnectAsync);
        StartScanCommand = new AsyncRelayCommand(StartScanAsync);
        StopScanCommand = new AsyncRelayCommand(StopScanAsync);
        ToggleScanCommand = new AsyncRelayCommand(ToggleScanAsync);
    }

    private void OnHeartRateReceived(object? sender, HeartRateChangedEventArgs e)
    {
        _dispatcher.BeginInvoke(() =>
        {
            var data = e.Data;
            CurrentHeartRate = data.HeartRate;
            BpmText = data.HeartRate.ToString();
            LastUpdate = data.Timestamp.ToString("HH:mm:ss");

            if (data.RRInterval.HasValue)
            {
                RRInterval = Math.Round(data.RRInterval.Value / 1000.0, 3);
            }

            _recentRates.Add(data.HeartRate);
            if (_recentRates.Count > 100) _recentRates.RemoveAt(0);

            AverageHeartRate = (int)_recentRates.Average();
            MaxHeartRate = _recentRates.Max();
            MinHeartRate = _recentRates.Min();

            UpdateHeartRateColor(data.HeartRate);
            UpdateChart(data);

            _ = _dataService.SaveHeartRateRecordAsync(data, _bleService.ConnectedDevice?.DeviceId);
        });
    }

    private void OnConnectionStateChanged(object? sender, ConnectionStateChangedEventArgs e)
    {
        _dispatcher.BeginInvoke(() =>
        {
            ConnectionStatus = e.State switch
            {
                ConnectionState.Disconnected => "未连接",
                ConnectionState.Scanning => "扫描中...",
                ConnectionState.Connecting => "连接中...",
                ConnectionState.Connected => "已连接",
                ConnectionState.Reconnecting => "重连中...",
                _ => "未知"
            };

            ConnectionStatusIcon = e.State switch
            {
                ConnectionState.Disconnected => "未连接",
                ConnectionState.Scanning => "扫描中",
                ConnectionState.Connecting => "连接中",
                ConnectionState.Connected => "已连接",
                ConnectionState.Reconnecting => "重连中",
                _ => "未知"
            };

            IsConnected = e.State == ConnectionState.Connected;
            IsScanning = e.State == ConnectionState.Scanning;

            if (e.DeviceName != null)
            {
                DeviceName = e.DeviceName;
            }

            if (e.State == ConnectionState.Disconnected)
            {
                BpmText = "--";
                CurrentHeartRate = 0;
            }
        });
    }

    private void UpdateHeartRateColor(int heartRate)
    {
        HeartRateColor = heartRate switch
        {
            < 100 => "#FFFFFF",
            < 140 => "#22C55E",
            < 170 => "#F59E0B",
            < 200 => "#F97316",
            _ => "#EF4444"
        };

        (HeartRateZone, ZoneColor) = heartRate switch
        {
            < 100 => ("静息", "#6366F1"),
            < 140 => ("燃脂", "#22C55E"),
            < 170 => ("有氧", "#F59E0B"),
            < 200 => ("极限", "#F97316"),
            _ => ("最大", "#EF4444")
        };
    }

    private void UpdateChart(HeartRateData data)
    {
        _heartRatePoints.Add(new DateTimePoint(data.Timestamp, data.HeartRate));

        while (_heartRatePoints.Count > MaxChartPoints)
        {
            _heartRatePoints.RemoveAt(0);
        }

        if (_heartRatePoints.Count > 1)
        {
            var min = _heartRatePoints.Min(p => p.Value ?? 0);
            var max = _heartRatePoints.Max(p => p.Value ?? 0);
            MinChartValue = Math.Max(40, min - 10);
            MaxChartValue = Math.Min(220, max + 10);
            YAxes[0].MinLimit = MinChartValue;
            YAxes[0].MaxLimit = MaxChartValue;
        }
    }

    private async Task ConnectAsync(BleDevice? device)
    {
        if (device == null) return;
        try
        {
            await _bleService.ConnectAsync(device);
            _settingsService.LastDeviceId = device.DeviceId;
            _settingsService.LastDeviceName = device.DeviceName;
            await _settingsService.SaveAsync();
            await _dataService.SaveDeviceInfoAsync(device);
        }
        catch (Exception ex)
        {
            _logger.Error("连接设备失败", ex);
        }
    }

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

    private async Task StartScanAsync()
    {
        try
        {
            DiscoveredDevices.Clear();
            await _bleService.StartScanningAsync();

            _ = Task.Run(async () =>
            {
                while (_bleService.IsScanning)
                {
                    await Task.Delay(1000);
                    var devices = new List<BleDevice>();
                    await foreach (var d in _bleService.DiscoverDevicesAsync())
                    {
                        devices.Add(d);
                    }
                    _dispatcher.BeginInvoke(() =>
                    {
                        foreach (var d in devices)
                        {
                            if (!DiscoveredDevices.Any(x => x.DeviceId == d.DeviceId))
                            {
                                DiscoveredDevices.Add(d);
                            }
                        }
                    });
                }
            });
        }
        catch (Exception ex)
        {
            _logger.Error("启动扫描失败", ex);
        }
    }

    private async Task StopScanAsync()
    {
        await _bleService.StopScanningAsync();
    }

    private async Task ToggleScanAsync()
    {
        if (_bleService.IsScanning)
            await StopScanAsync();
        else
            await StartScanAsync();
    }

    public void Dispose()
    {
        _bleService.HeartRateReceived -= OnHeartRateReceived;
        _bleService.ConnectionStateChanged -= OnConnectionStateChanged;
    }
}
