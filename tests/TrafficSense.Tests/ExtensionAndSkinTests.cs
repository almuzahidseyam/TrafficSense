using System.Text.Json;
using System.Text.Json.Nodes;
using Xunit;

namespace TrafficSense.Tests;

public sealed class ExtensionAndSkinTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "TrafficSense.ExtensionTests", Guid.NewGuid().ToString("N"));
    private string PathTo(string name) => Path.Combine(_root, name);
    public ExtensionAndSkinTests() => Directory.CreateDirectory(_root);

    [Fact]
    public void SensorFreshnessSizeAndSchemaNeverTurnMissingDataIntoZero()
    {
        var now = new DateTimeOffset(2026, 10, 13, 1, 0, 0, TimeSpan.Zero);
        var source = new { Format = "TrafficSense.Sensors", Version = 1, UpdatedAt = now,
            Sensors = new[] { new { Id = "cpu-temp", Label = "CPU package", Value = 42.5, Unit = "°C" } } };
        string path = PathTo("sensors.json"); File.WriteAllText(path, JsonSerializer.Serialize(source));
        Assert.Equal(42.5, Assert.Single(ExternalSensorFile.Read(path, now)!.Sensors).Value);
        Assert.Null(ExternalSensorFile.Read(path, now.AddSeconds(6)));
        Assert.Null(ExternalSensorFile.Read(path, now.AddSeconds(-2)));
        File.WriteAllText(path, "{}"); Assert.Null(ExternalSensorFile.Read(path, now));
        File.WriteAllText(path, new string('x', ExternalSensorFile.MaximumBytes + 1)); Assert.Null(ExternalSensorFile.Read(path, now));
        File.WriteAllText(path, JsonSerializer.Serialize(source).Replace("42.5", "1e300")); Assert.Null(ExternalSensorFile.Read(path, now));
        File.Delete(path); Assert.Null(ExternalSensorFile.Read(path, now));
    }

    [Fact]
    public void SkinsRoundTripOnlyAppearanceAndRejectInvalidOrUnboundedInputs()
    {
        var skin = new WidgetSkin(18, "Consolas, Nirmala UI", false, 191, "#061C2D", "#45E0B6", "#60A5FA", "#FFFFFF");
        string path = PathTo("skin.json"); Assert.True(skin.Export(path).Success); Assert.Equal(skin, WidgetSkin.Read(path));
        var settings = new AppSettings { SelectedAdapterId = "vpn", PositionLocked = true, TaskbarPanel = true, DailyLimitGiB = 5 };
        skin.Apply(settings); Assert.Equal("vpn", settings.SelectedAdapterId); Assert.True(settings.TaskbarPanel && settings.PositionLocked); Assert.Equal(5, settings.DailyLimitGiB);
        var root = JsonNode.Parse(File.ReadAllText(path))!; root["Appearance"]!["FontFamily"] = "https://example.com/font#Remote";
        File.WriteAllText(path, root.ToJsonString()); Assert.Throws<JsonException>(() => WidgetSkin.Read(path));
        Assert.True(skin.Export(path).Success); root = JsonNode.Parse(File.ReadAllText(path))!; root["Appearance"]!["Download"] = "#GGGGGG";
        File.WriteAllText(path, root.ToJsonString()); Assert.Throws<JsonException>(() => WidgetSkin.Read(path));
        Assert.True(skin.Export(path).Success); File.WriteAllText(path, File.ReadAllText(path).Replace("\"Version\": 1", "\"Version\": 1, \"Version\": 1"));
        Assert.Throws<JsonException>(() => WidgetSkin.Read(path));
        File.WriteAllText(path, new string('x', 17000)); Assert.Throws<JsonException>(() => WidgetSkin.Read(path));
        Assert.Empty(Directory.GetFiles(_root, "*.tmp"));
    }

    [Fact]
    public void SkinEnvelopeRejectsMissingWrongTypeAndOversizedVersion()
    {
        string path = PathTo("malformed.json");
        foreach (string json in new[] { "{}", "{\"Format\":null,\"Version\":1}",
            "{\"Format\":\"TrafficSense.Skin\",\"Version\":1e100}",
            "{\"Format\":\"TrafficSense.Skin\",\"Version\":\"1\"}" })
        {
            File.WriteAllText(path, json); Assert.Throws<JsonException>(() => WidgetSkin.Read(path));
        }
    }

    [Fact]
    public void OriginalCompleteBackupImportsBeforeAllNewPreferences()
    {
        string path = PathTo("backup.json"); SettingsBackup.Export(path, new());
        var root = JsonNode.Parse(File.ReadAllText(path))!;
        string[] original = ["WindowLeft", "WindowTop", "ScreenLeft", "ScreenTop", "PositionLocked", "SpeedInBits", "RefreshMilliseconds",
            "GlobalShortcutEnabled", "Theme", "Language", "SelectedAdapterId", "WidgetFontSize", "BackgroundOpacity"];
        var settings = root["Settings"]!.AsObject();
        foreach (string key in settings.Select(item => item.Key).Except(original).ToArray()) settings.Remove(key);
        File.WriteAllText(path, root.ToJsonString()); var imported = SettingsBackup.Read(path);
        Assert.Equal(0, imported.DisplayMetrics); Assert.False(imported.TaskbarPanel); Assert.Null(imported.SensorFile); Assert.Equal("Auto", imported.DownloadColor);
    }
    public void Dispose() => Directory.Delete(_root, true);
}
