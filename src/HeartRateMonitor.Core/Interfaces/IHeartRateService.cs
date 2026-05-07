using HeartRateMonitor.Core.Events;
using HeartRateMonitor.Core.Models;

namespace HeartRateMonitor.Core.Interfaces;

public interface IHeartRateService
{
    int CurrentHeartRate { get; }
    HeartRateData? LatestData { get; }
    IReadOnlyList<HeartRateData> RecentHistory { get; }

    event EventHandler<HeartRateChangedEventArgs>? HeartRateUpdated;

    void UpdateHeartRate(HeartRateData data);
    Task<HeartRateStatistics> GetStatisticsAsync(DateTime startTime, DateTime endTime);
    Task<DailyReport> GetDailyReportAsync(DateTime date);
    void ClearHistory();
}
