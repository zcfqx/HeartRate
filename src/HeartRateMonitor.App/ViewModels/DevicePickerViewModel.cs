using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HeartRateMonitor.Core.Enums;
using HeartRateMonitor.Core.Events;
using HeartRateMonitor.Core.Interfaces;
using HeartRateMonitor.Core.Models;

namespace HeartRateMonitor.App.ViewModels;

public partial class DevicePickerViewModel : ObservableObject
{
    private readonly IBleService _bleService;
    private readonly ILogger _logger;
    private readonly Dispatcher _dispatcher;
    private bool _eventsAttached;

    [ObservableProperty]
    private bool _isScanning;

    [ObservableProperty]
    private string _scanButtonText = "开始扫描";

    [ObservableProperty]
    private BleDevice? _selectedDevice;

    public ObservableCollection<BleDevice> Devices { get; } = [];

    public event EventHandler<BleDevice>? DeviceSelected;

    partial void OnSelectedDeviceChanged(BleDevice? value)
    {
        if (value != null)
        {
            DeviceSelected?.Invoke(this, value);
        }
    }

    public DevicePickerViewModel(IBleService bleService, ILogger logger)
    {
        _bleService = bleService;
        _logger = logger;
        _dispatcher = Application.Current.Dispatcher;
    }

    public void AttachEvents()
    {
        if (_eventsAttached) return;
        _eventsAttached = true;

        _bleService.DeviceDiscovered += OnDeviceDiscovered;
        _bleService.ConnectionStateChanged += OnConnectionStateChanged;

        // 同步当前扫描状态
        IsScanning = _bleService.IsScanning;
        ScanButtonText = IsScanning ? "停止扫描" : "开始扫描";

        // 加载已发现的设备
        LoadExistingDevices();
    }

    public void DetachEvents()
    {
        if (!_eventsAttached) return;
        _eventsAttached = false;

        _bleService.DeviceDiscovered -= OnDeviceDiscovered;
        _bleService.ConnectionStateChanged -= OnConnectionStateChanged;
    }

    private void LoadExistingDevices()
    {
        try
        {
            foreach (var device in _bleService.GetDiscoveredDevices())
            {
                if (!Devices.Any(d => d.DeviceId == device.DeviceId))
                {
                    Devices.Add(device);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.Debug($"加载已有设备失败: {ex.Message}");
        }
    }

    private void OnDeviceDiscovered(object? sender, BleDevice device)
    {
        _logger.Debug($"DevicePickerViewModel 收到设备: {device.DeviceName}");
        _dispatcher.BeginInvoke(() =>
        {
            if (!Devices.Any(d => d.DeviceId == device.DeviceId))
            {
                Devices.Add(device);
                _logger.Debug($"设备已添加到UI列表: {device.DeviceName}");
            }
        });
    }

    private void OnConnectionStateChanged(object? sender, ConnectionStateChangedEventArgs e)
    {
        _dispatcher.BeginInvoke(() =>
        {
            IsScanning = e.State == ConnectionState.Scanning;
            ScanButtonText = IsScanning ? "停止扫描" : "开始扫描";
        });
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
            Devices.Clear();
            IsScanning = true;
            ScanButtonText = "停止扫描";

            await _bleService.StartScanningAsync();
        }
        catch (Exception ex)
        {
            _logger.Error("启动扫描失败", ex);
            IsScanning = false;
            ScanButtonText = "开始扫描";
        }
    }

    private async Task StopScanAsync()
    {
        try
        {
            await _bleService.StopScanningAsync();
            IsScanning = false;
            ScanButtonText = "开始扫描";
        }
        catch (Exception ex)
        {
            _logger.Error("停止扫描失败", ex);
        }
    }

    [RelayCommand]
    private void SelectDevice(BleDevice? device)
    {
        if (device != null)
        {
            DeviceSelected?.Invoke(this, device);
        }
    }
}
