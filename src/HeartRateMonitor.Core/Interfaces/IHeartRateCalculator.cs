using HeartRateMonitor.Core.Models;

namespace HeartRateMonitor.Core.Interfaces;

public interface IHeartRateCalculator
{
    HeartRateStatistics CalculateStatistics(IReadOnlyList<HeartRateData> history, DateTime startTime, DateTime endTime);
    DailyReport GenerateDailyReport(IReadOnlyList<HeartRateData> history, DateTime date);
    double CalculateCalories(int heartRate, TimeSpan duration, double weight = 70.0);
}
