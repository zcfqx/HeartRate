using HeartRateMonitor.Core.Models;
using HeartRateMonitor.Services.HeartRate;

namespace HeartRateMonitor.Tests;

[TestClass]
public class HeartRateServiceTests
{
    private static HeartRateService CreateService()
    {
        var calculator = new HeartRateCalculator();
        var logger = new TestLogger();
        return new HeartRateService(calculator, logger);
    }

    [TestMethod]
    public void UpdateHeartRate_Updates_CurrentHeartRate()
    {
        var service = CreateService();
        service.UpdateHeartRate(new HeartRateData { HeartRate = 72, Timestamp = DateTime.Now });
        Assert.AreEqual(72, service.CurrentHeartRate);
    }

    [TestMethod]
    public void UpdateHeartRate_Updates_LatestData()
    {
        var service = CreateService();
        var data = new HeartRateData { HeartRate = 80, Timestamp = DateTime.Now };
        service.UpdateHeartRate(data);
        Assert.AreEqual(data, service.LatestData);
    }

    [TestMethod]
    public void UpdateHeartRate_Adds_To_History()
    {
        var service = CreateService();
        service.UpdateHeartRate(new HeartRateData { HeartRate = 70, Timestamp = DateTime.Now });
        service.UpdateHeartRate(new HeartRateData { HeartRate = 75, Timestamp = DateTime.Now });
        Assert.AreEqual(2, service.RecentHistory.Count);
    }

    [TestMethod]
    public void UpdateHeartRate_Fires_Event()
    {
        var service = CreateService();
        var fired = false;
        service.HeartRateUpdated += (s, e) => fired = true;
        service.UpdateHeartRate(new HeartRateData { HeartRate = 70, Timestamp = DateTime.Now });
        Assert.IsTrue(fired);
    }

    [TestMethod]
    public void UpdateHeartRate_Null_Data_Does_Nothing()
    {
        var service = CreateService();
        service.UpdateHeartRate(null!);
        Assert.IsNull(service.LatestData);
        Assert.AreEqual(0, service.CurrentHeartRate);
    }

    [TestMethod]
    public void ClearHistory_Empties_History()
    {
        var service = CreateService();
        service.UpdateHeartRate(new HeartRateData { HeartRate = 70, Timestamp = DateTime.Now });
        service.ClearHistory();
        Assert.AreEqual(0, service.RecentHistory.Count);
    }

    [TestMethod]
    public void GetStatisticsAsync_Returns_Stats()
    {
        var service = CreateService();
        service.UpdateHeartRate(new HeartRateData { HeartRate = 70, Timestamp = DateTime.Now });
        service.UpdateHeartRate(new HeartRateData { HeartRate = 80, Timestamp = DateTime.Now });
        var stats = service.GetStatisticsAsync(DateTime.Now.AddHours(-1), DateTime.Now).Result;
        Assert.IsNotNull(stats);
        Assert.AreEqual(75, stats.AverageHeartRate);
    }

    private class TestLogger : Core.Interfaces.ILogger
    {
        public void Debug(string message) { }
        public void Error(string message, Exception? exception = null) { }
        public void Info(string message) { }
        public void Warning(string message) { }
    }
}
