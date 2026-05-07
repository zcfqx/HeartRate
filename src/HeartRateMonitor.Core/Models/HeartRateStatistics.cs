namespace HeartRateMonitor.Core.Models;

public class HeartRateStatistics
{
    public int AverageHeartRate { get; set; }
    public int MaxHeartRate { get; set; }
    public int MinHeartRate { get; set; }
    public List<HeartRateZone> Zones { get; set; } = [];
    public TimeSpan TotalDuration { get; set; }
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
}
