using HeartRateMonitor.Services.HeartRate;

namespace HeartRateMonitor.Tests;

[TestClass]
public class HeartRateParserTests
{
    private readonly HeartRateParser _parser = new();

    [TestMethod]
    public void Parse_UINT8_Format_Returns_Correct_HeartRate()
    {
        byte[] data = [0x00, 72];
        var result = _parser.Parse(data);
        Assert.AreEqual(72, result.HeartRate);
    }

    [TestMethod]
    public void Parse_UINT16_Format_Returns_Correct_HeartRate()
    {
        byte[] data = [0x01, 0x00, 0x01];
        var result = _parser.Parse(data);
        Assert.AreEqual(256, result.HeartRate);
    }

    [TestMethod]
    public void Parse_UINT16_Format_Large_Value()
    {
        byte[] data = [0x01, 0xFF, 0x00];
        var result = _parser.Parse(data);
        Assert.AreEqual(255, result.HeartRate);
    }

    [TestMethod]
    public void Parse_Sensor_Contact_Detected()
    {
        byte[] data = [0x06, 75];
        var result = _parser.Parse(data);
        Assert.IsTrue(result.IsSensorContact);
    }

    [TestMethod]
    public void Parse_Sensor_Contact_Not_Detected()
    {
        byte[] data = [0x00, 75];
        var result = _parser.Parse(data);
        Assert.IsFalse(result.IsSensorContact);
    }

    [TestMethod]
    public void Parse_RR_Interval_Present()
    {
        byte[] data = [0x10, 80, 0x00, 0x40];
        var result = _parser.Parse(data);
        Assert.IsNotNull(result.RRInterval);
        Assert.IsTrue(result.RRInterval > 0);
    }

    [TestMethod]
    public void Parse_RR_Interval_Absent()
    {
        byte[] data = [0x00, 80];
        var result = _parser.Parse(data);
        Assert.IsNull(result.RRInterval);
    }

    [TestMethod]
    public void Parse_Timestamp_Is_Set()
    {
        byte[] data = [0x00, 72];
        var before = DateTime.Now.AddSeconds(-1);
        var result = _parser.Parse(data);
        var after = DateTime.Now.AddSeconds(1);
        Assert.IsTrue(result.Timestamp >= before && result.Timestamp <= after);
    }

    [TestMethod]
    [ExpectedException(typeof(ArgumentException))]
    public void Parse_Null_Data_Throws()
    {
        _parser.Parse(null!);
    }

    [TestMethod]
    [ExpectedException(typeof(ArgumentException))]
    public void Parse_Empty_Data_Throws()
    {
        _parser.Parse([]);
    }

    [TestMethod]
    [ExpectedException(typeof(ArgumentException))]
    public void Parse_Single_Byte_Throws()
    {
        _parser.Parse([0x00]);
    }

    [TestMethod]
    public void Parse_Energy_Expended_Skipped_Correctly()
    {
        byte[] data = [0x18, 80, 0x64, 0x00, 0x00, 0x40];
        var result = _parser.Parse(data);
        Assert.AreEqual(80, result.HeartRate);
        Assert.IsNotNull(result.RRInterval);
    }
}
