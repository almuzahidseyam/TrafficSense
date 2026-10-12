using System.Text;
using Xunit;

namespace TrafficSense.Tests;

public sealed class LibreSensorBridgeTests
{
    internal const string Fixture = """
        {"Children":[{"HardwareId":"/cpu/0","Text":"CPU","Children":[
        {"Type":"Temperature","SensorId":"/cpu/0/temperature/0","Text":"Package","RawValue":42.5,"Value":"99,9 °F"},
        {"Type":"Load","SensorId":"/cpu/0/load/0","Text":"Load","RawValue":90},
        {"Type":"Temperature","SensorId":"/cpu/0/temperature/1","Text":"Unavailable","RawValue":null}]}]}
        """;
    [Fact]
    public void NumericTemperatureIgnoresLocalizedDisplayAndUnknownSensors()
    {
        var now = DateTimeOffset.UtcNow;
        var sample = LibreSensorBridge.Parse(Encoding.UTF8.GetBytes(Fixture), now);
        var sensor = Assert.Single(sample.Sensors);
        Assert.Equal(42.5, sensor.Value); Assert.Equal("°C", sensor.Unit); Assert.Equal("CPU Package", sensor.Label);
        Assert.Equal(24, sensor.Id.Length); Assert.Equal(now, sample.UpdatedAt);
    }
    [Theory]
    [InlineData("{}")]
    [InlineData("{\"Type\":\"Temperature\",\"SensorId\":\"a\",\"RawValue\":999}")]
    [InlineData("{\"Type\":\"Temperature\",\"SensorId\":\"a\",\"Value\":\"50 °C\"}")]
    public void MissingInvalidOrLegacyTextReadingsAreNotInvented(string json) =>
        Assert.Throws<System.Text.Json.JsonException>(() => LibreSensorBridge.Parse(Encoding.UTF8.GetBytes(json), DateTimeOffset.UtcNow));
}
