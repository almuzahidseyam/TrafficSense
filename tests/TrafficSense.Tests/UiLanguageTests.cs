using System.Globalization;
using Xunit;

namespace TrafficSense.Tests;

public sealed class UiLanguageTests
{
    [Theory]
    [InlineData(null, "en")]
    [InlineData("", "en")]
    [InlineData("unknown", "en")]
    [InlineData("en", "en")]
    [InlineData("bn", "bn")]
    public void UnsupportedLanguagesFallBackToEnglish(string? stored, string expected) =>
        Assert.Equal(expected, new UiLanguage(stored).Code);

    [Fact]
    public void LanguageChangesNotifySubscribersOnlyAfterTheCodeChanges()
    {
        var language = new UiLanguage();
        var observed = new List<string>();
        language.Changed += (_, _) => observed.Add(language.Code);
        language.Select("bn");
        language.Select("bn");
        language.Select("en");
        Assert.Equal(new[] { "bn", "en" }, observed);
    }

    [Theory]
    [InlineData("en")]
    [InlineData("bn")]
    public void EveryUiMessageHasATranslationAndTrayTextFitsWindowsLimit(string code)
    {
        var language = new UiLanguage(code);
        foreach (var key in Enum.GetValues<TextKey>())
        {
            Assert.False(string.IsNullOrWhiteSpace(language[key]));
            if (code == "bn") Assert.Contains(language[key], c => c is >= '\u0980' and <= '\u09FF');
        }
        Assert.InRange(language[TextKey.TrayHint].Length, 1, 63);
    }

    [Fact]
    public void SwitchingLanguagesDoesNotChangeNumericCultureOrSpeedUnits()
    {
        var culture = CultureInfo.CurrentCulture;
        var uiCulture = CultureInfo.CurrentUICulture;
        string before = SpeedFormatter.Format(1024);
        new UiLanguage().Select("bn");
        Assert.Same(culture, CultureInfo.CurrentCulture);
        Assert.Same(uiCulture, CultureInfo.CurrentUICulture);
        Assert.Equal(before, SpeedFormatter.Format(1024));
        Assert.EndsWith(" KB/s", before);
    }
}
