using HeartRateMonitor.Core.Enums;
using HeartRateMonitor.Core.Models;
using HeartRateMonitor.Services.HeartRate;

namespace HeartRateMonitor.Tests;

[TestClass]
public class HeartRateCalculatorTests
{
    private readonly HeartRateCalculator _calculator = new();

    private static List<HeartRateData> CreateTestData(int count, int minHr = 60, int maxHr = 180)
    {
        var random = new Random(42);
        var data = new List<HeartRateData>();
        var baseTime = new DateTime(2026, 1, 1, 10, 0, 0);

        for (int i = 0; i < count; i++)
        {
            data.Add(new HeartRateData
            {
                HeartRate = random.Next(minHr, maxHr + 1),
                Timestamp = baseTime.AddSeconds(i * 5),
                IsSensorContact = true
            });
        }
        return data;
    }

    [TestMethod]
    public void CalculateStatistics_Empty_History_Returns_Empty_Stats()
    {
        var start = new DateTime(2026, 1, 1, 10, 0, 0);
        var end = start.AddHours(1);
        var stats = _calculator.CalculateStatistics([], start, end);
        Assert.AreEqual(0, stats.AverageHeartRate);
        Assert.AreEqual(0, stats.MaxHeartRate);
        Assert.AreEqual(0, stats.MinHeartRate);
    }

    [TestMethod]
    public void CalculateStatistics_Returns_Correct_Average()
    {
        var data = new List<HeartRateData>
        {
            new() { HeartRate = 60, Timestamp = new DateTime(2026, 1, 1, 10, 0, 0) },
            new() { HeartRate = 80, Timestamp = new DateTime(2026, 1, 1, 10, 0, 5) },
            new() { HeartRate = 100, Timestamp = new DateTime(2026, 1, 1, 10, 0, 10) }
        };

        var stats = _calculator.CalculateStatistics(data, data[0].Timestamp, data[2].Timestamp);
        Assert.AreEqual(80, stats.AverageHeartRate);
    }

    [TestMethod]
    public void CalculateStatistics_Returns_Correct_Min_Max()
    {
        var data = CreateTestData(100);
        var stats = _calculator.CalculateStatistics(data, data[0].Timestamp, data[^1].Timestamp);
        Assert.AreEqual(data.Min(d => d.HeartRate), stats.MinHeartRate);
        Assert.AreEqual(data.Max(d => d.HeartRate), stats.MaxHeartRate);
    }

    [TestMethod]
    public void CalculateStatistics_Filters_By_Time_Range()
    {
        var data = new List<HeartRateData>
        {
            new() { HeartRate = 60, Timestamp = new DateTime(2026, 1, 1, 9, 0, 0) },
            new() { HeartRate = 80, Timestamp = new DateTime(2026, 1, 1, 10, 0, 0) },
            new() { HeartRate = 100, Timestamp = new DateTime(2026, 1, 1, 11, 0, 0) }
        };

        var start = new DateTime(2026, 1, 1, 9, 30, 0);
        var end = new DateTime(2026, 1, 1, 10, 30, 0);
        var stats = _calculator.CalculateStatistics(data, start, end);
        Assert.AreEqual(80, stats.AverageHeartRate);
    }

    [TestMethod]
    public void CalculateStatistics_Returns_Zones()
    {
        var data = CreateTestData(200);
        var stats = _calculator.CalculateStatistics(data, data[0].Timestamp, data[^1].Timestamp);
        Assert.IsNotNull(stats.Zones);
        Assert.IsTrue(stats.Zones.Count > 0);
    }

    [TestMethod]
    public void GenerateDailyReport_Empty_Data_Returns_Empty_Report()
    {
        var date = new DateTime(2026, 1, 1);
        var report = _calculator.GenerateDailyReport([], date);
        Assert.AreEqual(date, report.Date);
        Assert.AreEqual(0, report.AverageHeartRate);
    }

    [TestMethod]
    public void GenerateDailyReport_Calculates_Correct_Values()
    {
        var date = new DateTime(2026, 1, 1);
        var data = new List<HeartRateData>();
        for (int i = 0; i < 100; i++)
        {
            data.Add(new HeartRateData
            {
                HeartRate = 60 + i,
                Timestamp = date.AddMinutes(i)
            });
        }

        var report = _calculator.GenerateDailyReport(data, date);
        Assert.IsTrue(report.AverageHeartRate > 0);
        Assert.AreEqual(data.Max(d => d.HeartRate), report.MaxHeartRate);
        Assert.AreEqual(data.Min(d => d.HeartRate), report.MinHeartRate);
    }

    [TestMethod]
    public void CalculateCalories_Returns_Positive_Value()
    {
        var calories = _calculator.CalculateCalories(120, TimeSpan.FromMinutes(30));
        Assert.IsTrue(calories > 0);
    }

    [TestMethod]
    public void CalculateCalories_Higher_HR_More_Calories()
    {
        var caloriesLow = _calculator.CalculateCalories(80, TimeSpan.FromMinutes(30));
        var caloriesHigh = _calculator.CalculateCalories(150, TimeSpan.FromMinutes(30));
        Assert.IsTrue(caloriesHigh > caloriesLow);
    }

    [TestMethod]
    public void CalculateCalories_Longer_Duration_More_Calories()
    {
        var caloriesShort = _calculator.CalculateCalories(120, TimeSpan.FromMinutes(15));
        var caloriesLong = _calculator.CalculateCalories(120, TimeSpan.FromMinutes(60));
        Assert.IsTrue(caloriesLong > caloriesShort);
    }

    [TestMethod]
    public void CalculateCalories_Zero_Duration_Returns_Zero()
    {
        var calories = _calculator.CalculateCalories(120, TimeSpan.Zero);
        Assert.AreEqual(0, calories, 0.01);
    }
}
