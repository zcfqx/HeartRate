using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HeartRateMonitor.Core.Interfaces;
using HeartRateMonitor.Core.Models;

namespace HeartRateMonitor.App.ViewModels;

public partial class DevicePickerViewModel : ObservableObject
{
    private readonly IBleService _bleService;
    private readonly ILogger _logger;
    private readonly Dispatcher _dispatcher;
    private CancellationTokenSource? _scanCts;

    [ObservableProperty]
    private bool _isScanning;

    [ObservableProperty]
    private string _scanButtonText = "开始扫描";

    public ObservableCollection<BleDevice> Devices { get; } = [];

    public event EventHandler<BleDevice>? DeviceSelected;

    public DevicePickerViewModel(IBleService bleService, ILogger logger)
    {
        _bleService = bleService;
        _logger = logger;
        _dispatcher = Application.Current.Dispatcher;
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
            _scanCts = new CancellationTokenSource();
            IsScanning = true;
            ScanButtonText = "停止扫描";

            await _bleService.StartScanningAsync();

            _ = Task.Run(async () =>
            {
                while (!_scanCts.Token.IsCancellationRequested && _bleService.IsScanning)
                {
                    await Task.Delay(1000, _scanCts.Token);
                    var devices = _bleService.DiscoverDevicesAsync(_scanCts.Token);
                    await foreach (var device in devices.WithCancellation(_scanCts.Token))
                    {
                        _dispatcher.BeginInvoke(() =>
                        {
                            if (!Devices.Any(d => d.DeviceId == device.DeviceId))
                            {
                                Devices.Add(device);
                            }
                        });
                    }
                }
            }, _scanCts.Token);
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
            _scanCts?.Cancel();
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
