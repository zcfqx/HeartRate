using HeartRateMonitor.Core.Models;

namespace HeartRateMonitor.Tests;

[TestClass]
public class HeartRateZonesTests
{
    [TestMethod]
    public void All_ContainsFiveContiguousZones()
    {
        Assert.AreEqual(5, HeartRateZones.All.Count);

        for (var i = 1; i < HeartRateZones.All.Count; i++)
        {
            Assert.AreEqual(HeartRateZones.All[i - 1].Max + 1, HeartRateZones.All[i].Min,
                $"区间 {i} 的起点应紧接上一个区间");
        }
    }

    [TestMethod]
    public void All_StartsWithRestAndEndsWithMax()
    {
        Assert.AreEqual(0, HeartRateZones.All[0].Min);
        Assert.AreEqual("rest", HeartRateZones.All[0].Key);
        Assert.AreEqual("max", HeartRateZones.All[^1].Key);
        Assert.AreEqual(int.MaxValue, HeartRateZones.All[^1].Max);
    }

    [DataTestMethod]
    [DataRow(0, "rest")]
    [DataRow(60, "rest")]
    [DataRow(99, "rest")]
    [DataRow(100, "fatburn")]
    [DataRow(139, "fatburn")]
    [DataRow(140, "cardio")]
    [DataRow(169, "cardio")]
    [DataRow(170, "peak")]
    [DataRow(199, "peak")]
    [DataRow(200, "max")]
    [DataRow(250, "max")]
    public void Classify_MapsBpmToZone(int bpm, string expectedKey)
    {
        Assert.AreEqual(expectedKey, HeartRateZones.Classify(bpm).Key);
    }

    [TestMethod]
    public void IndexOf_MatchesClassify()
    {
        for (var bpm = 0; bpm <= 240; bpm++)
        {
            var index = HeartRateZones.IndexOf(bpm);
            Assert.IsTrue(index >= 0 && index < HeartRateZones.All.Count);
            Assert.AreEqual(HeartRateZones.Classify(bpm).Key, HeartRateZones.All[index].Key);
        }
    }

    [TestMethod]
    public void All_ColorsAreDistinctHexValues()
    {
        var colors = HeartRateZones.All.Select(zone => zone.Color).ToList();

        Assert.AreEqual(colors.Count, colors.Distinct(StringComparer.OrdinalIgnoreCase).Count());
        Assert.IsTrue(colors.All(color => color.StartsWith('#') && color.Length == 7));
    }
}
