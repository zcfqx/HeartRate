using HeartRateMonitor.Core.Models;

namespace HeartRateMonitor.Tests;

[TestClass]
public class BleAdvertisementRulesTests
{
    [TestMethod]
    [DataRow(true, true, false, true)]
    [DataRow(true, false, true, true)]
    [DataRow(true, true, true, true)]
    [DataRow(false, true, true, false)]
    [DataRow(false, false, true, false)]
    [DataRow(true, false, false, false)]
    public void ShouldDiscover_RequiresConnectableAndUsefulInfo(
        bool isConnectable, bool hasName, bool advertisesHeartRate, bool expected)
    {
        Assert.AreEqual(expected, BleAdvertisementRules.ShouldDiscover(isConnectable, hasName, advertisesHeartRate));
    }

    [TestMethod]
    public void ResolveName_PrefersAdvertisedName()
    {
        Assert.AreEqual("HUAWEI Band HR-8F4",
            BleAdvertisementRules.ResolveName(null, "HUAWEI Band HR-8F4", false));
    }

    [TestMethod]
    public void ResolveName_KeepsExistingNameWhenNewAdvertisementHasNoName()
    {
        Assert.AreEqual("HUAWEI Band HR-8F4",
            BleAdvertisementRules.ResolveName("HUAWEI Band HR-8F4", null, true));
    }

    [TestMethod]
    public void ResolveName_UsesPlaceholderForUnnamedHeartRateDevice()
    {
        Assert.AreEqual(BleAdvertisementRules.HeartRatePlaceholderName,
            BleAdvertisementRules.ResolveName(null, null, true));
    }

    [TestMethod]
    public void ResolveName_ReturnsEmptyForUnnamedNonHeartRateDevice()
    {
        Assert.AreEqual(string.Empty, BleAdvertisementRules.ResolveName(null, null, false));
    }

    [TestMethod]
    public void ResolveName_TrimsAdvertisedName()
    {
        Assert.AreEqual("Polar H10", BleAdvertisementRules.ResolveName("旧名称", "  Polar H10  ", true));
    }

    [TestMethod]
    public void ShouldUpdateName_UpgradesPlaceholderToRealName()
    {
        Assert.IsTrue(BleAdvertisementRules.ShouldUpdateName(
            BleAdvertisementRules.HeartRatePlaceholderName, "HUAWEI Band HR-8F4"));
    }

    [TestMethod]
    public void ShouldUpdateName_RejectsEmptyOrSameName()
    {
        Assert.IsFalse(BleAdvertisementRules.ShouldUpdateName("HUAWEI Band HR-8F4", null));
        Assert.IsFalse(BleAdvertisementRules.ShouldUpdateName("HUAWEI Band HR-8F4", "  "));
        Assert.IsFalse(BleAdvertisementRules.ShouldUpdateName("HUAWEI Band HR-8F4", "HUAWEI Band HR-8F4"));
    }

    [TestMethod]
    public void ShouldUpdateName_AcceptsDifferentRealName()
    {
        Assert.IsTrue(BleAdvertisementRules.ShouldUpdateName("LE_WH-1000XM4", "WH-1000XM4"));
    }

    [TestMethod]
    public void SortForDisplay_HeartRateFirstThenSignalStrength()
    {
        var devices = new List<BleDevice>
        {
            new() { DeviceId = "1", DeviceName = "耳机", SignalStrength = -40, AdvertisesHeartRate = false },
            new() { DeviceId = "2", DeviceName = "心率带", SignalStrength = -80, AdvertisesHeartRate = true },
            new() { DeviceId = "3", DeviceName = "手环", SignalStrength = -60, AdvertisesHeartRate = true }
        };

        var sorted = BleAdvertisementRules.SortForDisplay(devices);

        CollectionAssert.AreEqual(new[] { "3", "2", "1" }, sorted.Select(d => d.DeviceId).ToArray());
    }

    [TestMethod]
    public void SortForDisplay_NonHeartRateDevicesKeepSignalOrder()
    {
        var devices = new List<BleDevice>
        {
            new() { DeviceId = "weak", DeviceName = "A", SignalStrength = -90 },
            new() { DeviceId = "strong", DeviceName = "B", SignalStrength = -50 }
        };

        var sorted = BleAdvertisementRules.SortForDisplay(devices);

        CollectionAssert.AreEqual(new[] { "strong", "weak" }, sorted.Select(d => d.DeviceId).ToArray());
    }

    [TestMethod]
    public void SortForDisplay_EmptyInput_ReturnsEmpty()
    {
        Assert.AreEqual(0, BleAdvertisementRules.SortForDisplay([]).Count);
    }
}
