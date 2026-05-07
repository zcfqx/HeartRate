using HeartRateMonitor.Core.Enums;
using HeartRateMonitor.Core.Interfaces;
using HeartRateMonitor.Core.Models;

namespace HeartRateMonitor.Services.HeartRate;

public class HeartRateCalculator : IHeartRateCalculator
{
    public HeartRateStatistics CalculateStatistics(IReadOnlyList<HeartRateData> history, DateTime startTime, DateTime endTime)
    {
        if (history == null || history.Count == 0)
        {
            return new HeartRateStatistics
            {
                StartTime = startTime,
                EndTime = endTime
            };
        }

        var filtered = history.Where(h => h.Timestamp >= startTime && h.Timestamp <= endTime).ToList();

        if (filtered.Count == 0)
        {
            return new HeartRateStatistics
            {
                StartTime = startTime,
                EndTime = endTime
            };
        }

        var stats = new HeartRateStatistics
        {
            AverageHeartRate = (int)filtered.Average(h => h.HeartRate),
            MaxHeartRate = filtered.Max(h => h.HeartRate),
            MinHeartRate = filtered.Min(h => h.HeartRate),
            TotalDuration = endTime - startTime,
            StartTime = startTime,
            EndTime = endTime,
            Zones = CalculateZoneDistribution(filtered)
        };

        return stats;
    }

    public DailyReport GenerateDailyReport(IReadOnlyList<HeartRateData> history, DateTime date)
    {
        var dayStart = date.Date;
        var dayEnd = dayStart.AddDays(1);
        var dayData = history.Where(h => h.Timestamp >= dayStart && h.Timestamp < dayEnd).ToList();

        var report = new DailyReport
        {
            Date = date.Date,
            MonitoringDuration = TimeSpan.FromMinutes(dayData.Count > 0 ?
                (dayData.Last().Timestamp - dayData.First().Timestamp).TotalMinutes : 0)
        };

        if (dayData.Count > 0)
        {
            report.AverageHeartRate = (int)dayData.Average(h => h.HeartRate);
            report.MaxHeartRate = dayData.Max(h => h.HeartRate);
            report.MinHeartRate = dayData.Min(h => h.HeartRate);

            var sortedRates = dayData.Select(h => h.HeartRate).OrderBy(r => r).ToList();
            int percentile10Index = (int)(sortedRates.Count * 0.1);
            report.RestingHeartRate = sortedRates[Math.Min(percentile10Index, sortedRates.Count - 1)];

            var activeThreshold = report.AverageHeartRate + 10;
            var activeCount = dayData.Count(h => h.HeartRate > activeThreshold);
            report.ActiveDuration = TimeSpan.FromMinutes(activeCount);

            report.ZoneDistribution = CalculateZoneDistribution(dayData);
        }

        return report;
    }

    public double CalculateCalories(int heartRate, TimeSpan duration, double weight = 70.0)
    {
        double durationMinutes = duration.TotalMinutes;
        double calories;

        if (heartRate < 120)
        {
            calories = (0.1 * heartRate + 0.05 * weight + 1.0) * durationMinutes;
        }
        else
        {
            calories = (0.15 * heartRate + 0.1 * weight + 2.0) * durationMinutes;
        }

        return Math.Max(0, calories);
    }

    private static List<HeartRateZone> CalculateZoneDistribution(List<HeartRateData> data)
    {
        if (data.Count == 0) return [];

        var maxHr = data.Max(h => h.HeartRate);
        var zones = new List<HeartRateZone>
        {
            new() { ZoneType = HeartRateZoneType.Rest, ZoneName = "Rest", MinHeartRate = 0, MaxHeartRate = 99, Color = "#6366F1" },
            new() { ZoneType = HeartRateZoneType.FatBurn, ZoneName = "Fat Burn", MinHeartRate = 100, MaxHeartRate = 139, Color = "#22C55E" },
            new() { ZoneType = HeartRateZoneType.Cardio, ZoneName = "Cardio", MinHeartRate = 140, MaxHeartRate = 169, Color = "#F59E0B" },
            new() { ZoneType = HeartRateZoneType.Peak, ZoneName = "Peak", MinHeartRate = 170, MaxHeartRate = 199, Color = "#F97316" },
            new() { ZoneType = HeartRateZoneType.Maximum, ZoneName = "Maximum", MinHeartRate = 200, MaxHeartRate = 220, Color = "#EF4444" }
        };

        int total = data.Count;
        foreach (var zone in zones)
        {
            int count = data.Count(h => h.HeartRate >= zone.MinHeartRate && h.HeartRate <= zone.MaxHeartRate);
            zone.Percentage = Math.Round((double)count / total * 100, 1);
            zone.Duration = TimeSpan.FromSeconds(count);
        }

        return zones;
    }
}
