using System.Text.Json;
using Xunit;

namespace TrafficSense.Tests;

public sealed class SettingsStoreTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "TrafficSense.Tests", Guid.NewGuid().ToString("N"));
    private string FilePath => Path.Combine(_directory, "settings.json");

    public SettingsStoreTests() => Directory.CreateDirectory(_directory);

    [Fact]
    public void MissingSettingsUseDefaults()
    {
        var settings = new SettingsStore(FilePath).Load();
        Assert.Equal("Dark", settings.Theme);
        Assert.Equal(-1, settings.WindowLeft);
        Assert.Null(settings.ScreenLeft);
    }

    [Theory]
    [InlineData("{broken json")]
    [InlineData("null")]
    [InlineData("{\"Theme\":null}")]
    [InlineData("{\"Theme\":\"Unknown\"}")]
    public void CorruptOrInvalidSettingsDoNotPreventStartup(string json)
    {
        File.WriteAllText(FilePath, json);
        Assert.Equal("Dark", new SettingsStore(FilePath).Load().Theme);
    }

    [Fact]
    public void SettingsRoundTripWithNegativeMonitorCoordinates()
    {
        var store = new SettingsStore(FilePath);
        Assert.True(store.Save(new AppSettings { Theme = "Light", ScreenLeft = -1500, ScreenTop = 200 }).Success);
        var loaded = store.Load();
        Assert.Equal("Light", loaded.Theme);
        Assert.Equal(-1500, loaded.ScreenLeft);
        Assert.Equal(200, loaded.ScreenTop);
        Assert.Empty(Directory.GetFiles(_directory, "*.tmp"));
    }

    [Fact]
    public void LegacySettingsRetainPositionWithoutPersistingStartupState()
    {
        File.WriteAllText(FilePath, "{\"WindowLeft\":100,\"WindowTop\":200,\"AutoStart\":true,\"Theme\":\"Light\"}");
        var store = new SettingsStore(FilePath);
        var settings = store.Load();
        Assert.Equal(100, settings.WindowLeft);
        Assert.Equal(200, settings.WindowTop);
        Assert.True(store.Save(settings).Success);
        using var document = JsonDocument.Parse(File.ReadAllText(FilePath));
        Assert.False(document.RootElement.TryGetProperty("AutoStart", out _));
    }

    [Fact]
    public void NonFinitePositionsAreSanitizedBeforeSaving()
    {
        var store = new SettingsStore(FilePath);
        Assert.True(store.Save(new AppSettings { WindowLeft = double.NaN, WindowTop = double.PositiveInfinity }).Success);
        var loaded = store.Load();
        Assert.Equal(-1, loaded.WindowLeft);
        Assert.Equal(-1, loaded.WindowTop);
    }

    [Fact]
    public void FailedReplacementPreservesPreviousSettingsAndReportsError()
    {
        var store = new SettingsStore(FilePath);
        Assert.True(store.Save(new AppSettings { Theme = "Light" }).Success);
        using (var lockedFile = new FileStream(FilePath, FileMode.Open, FileAccess.Read, FileShare.None))
        {
            var result = store.Save(new AppSettings { Theme = "Dark" });
            Assert.False(result.Success);
            Assert.NotNull(result.Error);
        }
        Assert.Equal("Light", store.Load().Theme);
        Assert.Empty(Directory.GetFiles(_directory, "*.tmp"));
    }

    public void Dispose()
    {
        foreach (var file in Directory.GetFiles(_directory))
            File.Delete(file);
        Directory.Delete(_directory);
    }
}
