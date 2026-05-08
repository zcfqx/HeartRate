using HeartRateMonitor.Core.Enums;
using HeartRateMonitor.Core.Events;
using HeartRateMonitor.Core.Models;

namespace HeartRateMonitor.Core.Interfaces;

public interface IBleService
{
    ConnectionState State { get; }
    BleDevice? ConnectedDevice { get; }
    bool IsScanning { get; }

    event EventHandler<HeartRateChangedEventArgs>? HeartRateReceived;
    event EventHandler<ConnectionStateChangedEventArgs>? ConnectionStateChanged;
    event EventHandler<HeartRateAlertEventArgs>? HeartRateAlert;
    event EventHandler<BleDevice>? DeviceDiscovered;

    Task<bool> RequestPermissionAsync();
    Task StartScanningAsync();
    Task StopScanningAsync();
    Task ConnectAsync(BleDevice device);
    Task DisconnectAsync();
    Task<bool> AutoReconnectAsync();
    IAsyncEnumerable<BleDevice> DiscoverDevicesAsync(CancellationToken cancellationToken = default);
    List<BleDevice> GetDiscoveredDevices();
}
