using HeartRateMonitor.Core.Enums;

namespace HeartRateMonitor.Core.Models;

public class HeartRateZone
{
    public HeartRateZoneType ZoneType { get; set; }
    public string ZoneName { get; set; } = string.Empty;
    public int MinHeartRate { get; set; }
    public int MaxHeartRate { get; set; }
    public TimeSpan Duration { get; set; }
    public double Percentage { get; set; }
    public string Color { get; set; } = string.Empty;
}
