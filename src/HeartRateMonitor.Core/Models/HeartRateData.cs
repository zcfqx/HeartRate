namespace HeartRateMonitor.Core.Models;

public class HeartRateData
{
    public int HeartRate { get; set; }
    public DateTime Timestamp { get; set; }
    public int? RRInterval { get; set; }
    public bool IsSensorContact { get; set; }
}
