namespace HeartRateMonitor.Core.Models;

public class DailyReport
{
    public DateTime Date { get; set; }
    public int AverageHeartRate { get; set; }
    public int MaxHeartRate { get; set; }
    public int MinHeartRate { get; set; }
    public int RestingHeartRate { get; set; }
    public TimeSpan ActiveDuration { get; set; }
    public TimeSpan MonitoringDuration { get; set; }
    public List<HeartRateZone> ZoneDistribution { get; set; } = [];
}
