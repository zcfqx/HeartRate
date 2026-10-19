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

    /// <summary>
    /// 已发现设备的信息发生实质变化（名称从占位名升级为真实名称、补上心率服务标记）时触发，
    /// 让界面可以就地刷新条目，而不必重建整个列表。
    /// </summary>
    event EventHandler<BleDevice>? DeviceUpdated;

    /// <summary>扫描过程中断（例如扫描途中蓝牙被关闭）时触发。</summary>
    event EventHandler<BleScanFailedEventArgs>? ScanFailed;

    /// <summary>检查蓝牙无线电/适配器是否可用（蓝牙开关是否打开）。</summary>
    Task<BleAvailability> CheckAvailabilityAsync();

    Task<bool> RequestPermissionAsync();
    Task StartScanningAsync();
    Task StopScanningAsync();
    Task ConnectAsync(BleDevice device);
    Task DisconnectAsync();
    Task<bool> AutoReconnectAsync();
    IAsyncEnumerable<BleDevice> DiscoverDevicesAsync(CancellationToken cancellationToken = default);
    List<BleDevice> GetDiscoveredDevices();
}
