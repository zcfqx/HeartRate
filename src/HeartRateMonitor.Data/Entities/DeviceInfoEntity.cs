namespace HeartRateMonitor.Data.Entities;

public class DeviceInfoEntity
{
    public string DeviceId { get; set; } = string.Empty;
    public string DeviceName { get; set; } = string.Empty;
    public long LastConnected { get; set; }
    public bool IsAutoConnect { get; set; }
}
