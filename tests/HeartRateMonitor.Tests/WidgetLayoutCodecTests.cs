using HeartRateMonitor.Core.Models;

namespace HeartRateMonitor.Tests;

[TestClass]
public class WidgetLayoutCodecTests
{
    [TestMethod]
    public void Parse_EmptyJson_ReturnsFallback()
    {
        var layout = WidgetLayoutCodec.Parse(null, ["bpm", "chart"]);

        CollectionAssert.AreEqual(new List<string> { "bpm", "chart" }, layout);
    }

    [TestMethod]
    public void Parse_ValidJson_KeepsOrder()
    {
        var layout = WidgetLayoutCodec.Parse("[\"chart\",\"bpm\",\"zone\"]");

        CollectionAssert.AreEqual(new List<string> { "chart", "bpm", "zone" }, layout);
    }

    [TestMethod]
    public void Parse_InvalidJson_FallsBack()
    {
        var layout = WidgetLayoutCodec.Parse("{not json", ["bpm"]);

        CollectionAssert.AreEqual(new List<string> { "bpm" }, layout);
    }

    [TestMethod]
    public void Parse_EmptyArray_FallsBack()
    {
        var layout = WidgetLayoutCodec.Parse("[]", ["bpm", "device"]);

        CollectionAssert.AreEqual(new List<string> { "bpm", "device" }, layout);
    }

    [TestMethod]
    public void Parse_RemovesDuplicatesIgnoringCase()
    {
        var layout = WidgetLayoutCodec.Parse("[\"bpm\",\"BPM\",\"chart\",\"bpm\"]");

        CollectionAssert.AreEqual(new List<string> { "bpm", "chart" }, layout);
    }

    [TestMethod]
    public void Parse_TrimsAndSkipsBlankIds()
    {
        var layout = WidgetLayoutCodec.Parse("[\" bpm \",\"\",\"   \",\"chart\"]");

        CollectionAssert.AreEqual(new List<string> { "bpm", "chart" }, layout);
    }

    [TestMethod]
    public void Parse_FallbackIsAlsoSanitized()
    {
        var layout = WidgetLayoutCodec.Parse("", ["bpm", "bpm", " "]);

        CollectionAssert.AreEqual(new List<string> { "bpm" }, layout);
    }

    [TestMethod]
    public void Serialize_ProducesJsonRoundTrip()
    {
        var json = WidgetLayoutCodec.Serialize(["bpm", "chart"]);

        CollectionAssert.AreEqual(new List<string> { "bpm", "chart" }, WidgetLayoutCodec.Parse(json));
    }

    [TestMethod]
    public void Serialize_Null_ReturnsEmptyArrayJson()
    {
        Assert.AreEqual("[]", WidgetLayoutCodec.Serialize(null));
    }

    [TestMethod]
    public void Serialize_SkipsBlankIds()
    {
        var json = WidgetLayoutCodec.Serialize(["bpm", "", "  ", "chart"]);

        CollectionAssert.AreEqual(new List<string> { "bpm", "chart" }, WidgetLayoutCodec.Parse(json));
    }
}
