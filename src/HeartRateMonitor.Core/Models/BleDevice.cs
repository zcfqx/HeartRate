namespace HeartRateMonitor.Core.Models;

public class BleDevice
{
    public string DeviceId { get; set; } = string.Empty;
    public string DeviceName { get; set; } = string.Empty;
    public int SignalStrength { get; set; }
    public bool IsConnectable { get; set; }
    public ulong BluetoothAddress { get; set; }
}
