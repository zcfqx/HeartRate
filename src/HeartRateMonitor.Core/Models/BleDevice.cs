namespace HeartRateMonitor.Core.Models;

public class BleDevice
{
    public string DeviceId { get; set; } = string.Empty;
    public string DeviceName { get; set; } = string.Empty;
    public int SignalStrength { get; set; }
    public bool IsConnectable { get; set; }

    /// <summary>广播包里是否包含心率服务 UUID（0x180D）。心率带通常有，手环只在特定模式下才有。</summary>
    public bool AdvertisesHeartRate { get; set; }

    public ulong BluetoothAddress { get; set; }
}
