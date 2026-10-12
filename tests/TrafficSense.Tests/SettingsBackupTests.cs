using System.Text.Json;
using System.Text.Json.Nodes;
using Xunit;

namespace TrafficSense.Tests;

public sealed class SettingsBackupTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "TrafficSense.BackupTests", Guid.NewGuid().ToString("N"));
    private string Backup => Path.Combine(_root, "backup.json");
    public SettingsBackupTests() => Directory.CreateDirectory(_root);

    [Fact]
    public void BackupRoundTripsAllPreferencesWithoutMutatingSource()
    {
        var settings = new AppSettings
        {
            Language = "bn", Theme = "Light", WindowLeft = -1000, WindowTop = 150,
            ScreenLeft = -1500, ScreenTop = 200, PositionLocked = true, SpeedInBits = true,
            RefreshMilliseconds = 500, GlobalShortcutEnabled = true, SelectedAdapterId = "adapter-1",
            WidgetFontSize = 24, BackgroundOpacity = 64
        };
        string before = JsonSerializer.Serialize(settings);
        Assert.True(SettingsBackup.Export(Backup, settings).Success);
        Assert.Equal(before, JsonSerializer.Serialize(SettingsBackup.Read(Backup)));
        Assert.Equal(before, JsonSerializer.Serialize(settings));
        var root = JsonNode.Parse(File.ReadAllText(Backup))!;
        Assert.Equal("TrafficSense.Settings", root["Format"]!.GetValue<string>());
        Assert.Equal(1, root["Version"]!.GetValue<int>());
        Assert.DoesNotContain("AutoStart", File.ReadAllText(Backup));
        Assert.Empty(Directory.GetFiles(_root, "*.tmp"));
    }

    [Theory]
    [InlineData("null")]
    [InlineData("{}")]
    [InlineData("[]")]
    [InlineData("{broken")]
    [InlineData("{\"Theme\":\"Light\"}")]
    public void WrongFilesAreRejected(string text)
    {
        File.WriteAllText(Backup, text);
        Assert.ThrowsAny<JsonException>(() => SettingsBackup.Read(Backup));
    }

    [Theory]
    [InlineData("Format", "\"OtherApp\"")]
    [InlineData("Format", "null")]
    [InlineData("Version", "2")]
    [InlineData("Version", "\"1\"")]
    [InlineData("Settings", "null")]
    [InlineData("Settings", "{}")]
    public void InvalidEnvelopeIsRejected(string property, string value)
    {
        var root = ValidBackup(); root[property] = JsonNode.Parse(value);
        Reject(root);
    }

    [Theory]
    [InlineData("Language", "\"fr\"")]
    [InlineData("Language", "null")]
    [InlineData("Theme", "\"unknown\"")]
    [InlineData("WidgetFontSize", "13")]
    [InlineData("BackgroundOpacity", "999")]
    [InlineData("RefreshMilliseconds", "0")]
    [InlineData("GlobalShortcutEnabled", "\"yes\"")]
    [InlineData("SelectedAdapterId", "\" \"")]
    [InlineData("WindowLeft", "1e100")]
    [InlineData("ScreenLeft", "100")]
    public void InvalidPreferencesAreRejected(string property, string value)
    {
        var root = ValidBackup(); root["Settings"]![property] = JsonNode.Parse(value);
        Reject(root);
    }

    [Fact]
    public void MissingUnknownAndDuplicatePropertiesAreRejected()
    {
        var root = ValidBackup(); root["Settings"]!.AsObject().Remove("Theme"); Reject(root);
        root = ValidBackup(); root["Settings"]!["Unknown"] = true; Reject(root);
        root = ValidBackup(); root["Unknown"] = true; Reject(root);
        root = ValidBackup();
        File.WriteAllText(Backup, root.ToJsonString().Replace("\"Version\":1", "\"Version\":1,\"Version\":1"));
        Assert.ThrowsAny<JsonException>(() => SettingsBackup.Read(Backup));
        root = ValidBackup();
        File.WriteAllText(Backup, root.ToJsonString().Replace("\"Theme\":\"Dark\"", "\"Theme\":\"Dark\",\"Theme\":\"Light\""));
        Assert.ThrowsAny<JsonException>(() => SettingsBackup.Read(Backup));
    }

    [Fact]
    public void LargeFilesAndOversizedExportAreRejected()
    {
        File.WriteAllText(Backup, new string(' ', 65537));
        Assert.ThrowsAny<JsonException>(() => SettingsBackup.Read(Backup));
        Assert.False(SettingsBackup.Export(Backup, new AppSettings { SelectedAdapterId = new string('a', 65536) }).Success);
        Assert.Equal(65537, new FileInfo(Backup).Length);
        Assert.Empty(Directory.GetFiles(_root, "*.tmp"));
    }

    [Fact]
    public void BlockedExportPreservesPreviousBackupAndCleansTemporaryFile()
    {
        Assert.True(SettingsBackup.Export(Backup, new AppSettings { Language = "bn" }).Success);
        string before = File.ReadAllText(Backup);
        using (var locked = new FileStream(Backup, FileMode.Open, FileAccess.Read, FileShare.None))
            Assert.False(SettingsBackup.Export(Backup, new AppSettings()).Success);
        Assert.Equal(before, File.ReadAllText(Backup));
        Assert.Empty(Directory.GetFiles(_root, "*.tmp"));
    }

    private JsonNode ValidBackup()
    {
        Assert.True(SettingsBackup.Export(Backup, new AppSettings()).Success);
        return JsonNode.Parse(File.ReadAllText(Backup))!;
    }
    private void Reject(JsonNode root)
    {
        File.WriteAllText(Backup, root.ToJsonString());
        Assert.ThrowsAny<JsonException>(() => SettingsBackup.Read(Backup));
    }
    public void Dispose() => Directory.Delete(_root, true);
}
