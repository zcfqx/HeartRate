using HeartRateMonitor.Core.Models;

namespace HeartRateMonitor.Tests;

[TestClass]
public class AppProcessNamesTests
{
    [TestMethod]
    [DataRow("HeartRateMonitor", true)]
    [DataRow("HeartRateMonitor.App", true)]
    [DataRow("HeartRateMonitor.App.exe", true)]
    [DataRow("HeartRateMonitor-1.3.1-win-x64", true)]
    [DataRow("HeartRateMonitor-1.3.1-win-x64.exe", true)]
    [DataRow("HeartRate", true)]
    [DataRow("HeartRate-1.3.1-win-x64-self-contained", true)]
    [DataRow("HeartRate-1.3.1-win-x64-framework-dependent.exe", true)]
    [DataRow("heartrate.app", true)]
    [DataRow("  HeartRateMonitor.App  ", true)]
    [DataRow("HeartRateMonitorHelper", false)]
    [DataRow("HeartRateMonitorX", false)]
    [DataRow("HeartRateX", false)]
    [DataRow("HeartRateZone", false)]
    [DataRow("", false)]
    [DataRow(null, false)]
    public void IsAppProcess_MatchesOnlyOurOwnProcessNames(string? processName, bool expected)
    {
        Assert.AreEqual(expected, AppProcessNames.IsAppProcess(processName));
    }
}
