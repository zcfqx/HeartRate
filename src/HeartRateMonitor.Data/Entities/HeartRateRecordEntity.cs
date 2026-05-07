namespace HeartRateMonitor.Data.Entities;

public class HeartRateRecordEntity
{
    public long Id { get; set; }
    public int HeartRate { get; set; }
    public long Timestamp { get; set; }
    public int? RRInterval { get; set; }
    public bool IsSensorContact { get; set; }
    public string? DeviceId { get; set; }
}
