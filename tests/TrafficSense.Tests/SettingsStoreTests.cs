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
        Assert.False(settings.PositionLocked);
        Assert.Null(settings.SelectedAdapterId);
        Assert.Equal("en", settings.Language);
        Assert.Equal(14, settings.WidgetFontSize);
        Assert.Equal(120, settings.BackgroundOpacity);
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
        Assert.False(settings.PositionLocked);
        Assert.Equal("en", settings.Language);
        Assert.Equal(14, settings.WidgetFontSize);
        Assert.Equal(120, settings.BackgroundOpacity);
        Assert.True(store.Save(settings).Success);
        using var document = JsonDocument.Parse(File.ReadAllText(FilePath));
        Assert.False(document.RootElement.TryGetProperty("AutoStart", out _));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void PositionLockRoundTripsWithoutLosingOtherPreferences(bool locked)
    {
        var store = new SettingsStore(FilePath);
        var settings = new AppSettings
        {
            PositionLocked = locked, ScreenLeft = -1200, ScreenTop = 100,
            WindowLeft = -800, WindowTop = 66, Theme = "Light", Language = "bn",
            SelectedAdapterId = "manual", WidgetFontSize = 18, BackgroundOpacity = 64
        };
        Assert.True(store.Save(settings).Success);
        var loaded = store.Load();
        Assert.Equal(locked, loaded.PositionLocked);
        Assert.Equal(-1200, loaded.ScreenLeft);
        Assert.Equal(100, loaded.ScreenTop);
        Assert.Equal(-800, loaded.WindowLeft);
        Assert.Equal(66, loaded.WindowTop);
        Assert.Equal("Light", loaded.Theme);
        Assert.Equal("bn", loaded.Language);
        Assert.Equal("manual", loaded.SelectedAdapterId);
        Assert.Equal(18, loaded.WidgetFontSize);
        Assert.Equal(64, loaded.BackgroundOpacity);
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

    [Theory]
    [InlineData(12, 255, 12, 255)]
    [InlineData(14, 120, 14, 120)]
    [InlineData(16, 191, 16, 191)]
    [InlineData(18, 128, 18, 128)]
    [InlineData(20, 64, 20, 64)]
    [InlineData(24, 26, 24, 26)]
    [InlineData(13, 200, 14, 120)]
    [InlineData(0, 0, 14, 120)]
    [InlineData(-1, -1, 14, 120)]
    [InlineData(int.MaxValue, int.MaxValue, 14, 120)]
    public void AppearanceNormalizesAndRoundTripsWithoutLosingOtherSettings(int size, int opacity, int expectedSize, int expectedOpacity)
    {
        File.WriteAllText(FilePath, JsonSerializer.Serialize(new AppSettings
        {
            WidgetFontSize = size, BackgroundOpacity = opacity, Language = "bn", Theme = "Light",
            SelectedAdapterId = "manual-id", ScreenLeft = -400
        }));
        var store = new SettingsStore(FilePath);
        var loaded = store.Load();
        Assert.Equal(expectedSize, loaded.WidgetFontSize);
        Assert.Equal(expectedOpacity, loaded.BackgroundOpacity);
        Assert.True(store.Save(loaded).Success);
        var reopened = store.Load();
        Assert.Equal(expectedSize, reopened.WidgetFontSize);
        Assert.Equal(expectedOpacity, reopened.BackgroundOpacity);
        Assert.Equal("bn", reopened.Language);
        Assert.Equal("Light", reopened.Theme);
        Assert.Equal("manual-id", reopened.SelectedAdapterId);
        Assert.Equal(-400, reopened.ScreenLeft);
    }

    [Fact]
    public void InvalidAppearanceIsSanitizedBeforeSavingWithoutChangingCaller()
    {
        var settings = new AppSettings { WidgetFontSize = -100, BackgroundOpacity = 999, Language = "bn" };
        var store = new SettingsStore(FilePath);
        Assert.True(store.Save(settings).Success);
        Assert.Equal(-100, settings.WidgetFontSize);
        Assert.Equal(999, settings.BackgroundOpacity);
        Assert.Equal(14, store.Load().WidgetFontSize);
        Assert.Equal(120, store.Load().BackgroundOpacity);
        Assert.Equal("bn", store.Load().Language);
    }

    [Fact]
    public void SelectedAdapterIsRememberedAndCanBeResetToAuto()
    {
        var store = new SettingsStore(FilePath);
        Assert.True(store.Save(new AppSettings { SelectedAdapterId = "{vpn-adapter-id}" }).Success);
        Assert.Equal("{vpn-adapter-id}", store.Load().SelectedAdapterId);
        Assert.True(store.Save(new AppSettings { SelectedAdapterId = null }).Success);
        Assert.Null(store.Load().SelectedAdapterId);
    }

    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    public void EmptyAdapterSelectionUsesAuto(string value)
    {
        File.WriteAllText(FilePath, JsonSerializer.Serialize(new AppSettings { SelectedAdapterId = value }));
        Assert.Null(new SettingsStore(FilePath).Load().SelectedAdapterId);
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

    [Theory]
    [InlineData("en")]
    [InlineData("bn")]
    public void LanguageRoundTripsWithoutLosingOtherPreferences(string language)
    {
        var store = new SettingsStore(FilePath);
        Assert.True(store.Save(new AppSettings
        {
            Language = language, SelectedAdapterId = "vpn-id", Theme = "Light", ScreenLeft = -400
        }).Success);
        var loaded = store.Load();
        Assert.Equal(language, loaded.Language);
        Assert.Equal("vpn-id", loaded.SelectedAdapterId);
        Assert.Equal("Light", loaded.Theme);
        Assert.Equal(-400, loaded.ScreenLeft);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("fr")]
    public void InvalidSavedLanguageFallsBackToEnglish(string? language)
    {
        File.WriteAllText(FilePath, JsonSerializer.Serialize(new { Language = language }));
        Assert.Equal("en", new SettingsStore(FilePath).Load().Language);
    }

    public void Dispose()
    {
        foreach (var file in Directory.GetFiles(_directory))
            File.Delete(file);
        Directory.Delete(_directory);
    }
}
