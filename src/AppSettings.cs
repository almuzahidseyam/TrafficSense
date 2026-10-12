namespace TrafficSense;

public class AppSettings
{
    // Retain the legacy WPF coordinates to migrate existing settings.
    public double WindowLeft { get; set; } = -1;
    public double WindowTop { get; set; } = -1;
    public int? ScreenLeft { get; set; }
    public int? ScreenTop { get; set; }
    public bool PositionLocked { get; set; }
    public bool SpeedInBits { get; set; }
    public int DisplayMetrics { get; set; }
    public int RefreshMilliseconds { get; set; } = 1000;
    public bool GlobalShortcutEnabled { get; set; }
    public string Theme { get; set; } = "Dark";
    public string Language { get; set; } = "en";
    public string? SelectedAdapterId { get; set; }
    public int WidgetFontSize { get; set; } = WidgetAppearance.DefaultFontSize;
    public int BackgroundOpacity { get; set; } = WidgetAppearance.DefaultBackgroundOpacity;
}
