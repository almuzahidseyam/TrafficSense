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
    public int DailyLimitGiB { get; set; }
    public int MonthlyLimitGiB { get; set; }
    public string? SensorFile { get; set; }
    public string? LayoutSkinFile { get; set; }
    public bool LayoutSkinCompact { get; set; }
    public string WidgetFontFamily { get; set; } = "Segoe UI, Nirmala UI, Kalpurush";
    public bool WidgetBold { get; set; } = true;
    public string WidgetBackground { get; set; } = "Auto";
    public string DownloadColor { get; set; } = "Auto";
    public string UploadColor { get; set; } = "Auto";
    public string MetricsColor { get; set; } = "Auto";
    public bool TaskbarPanel { get; set; }
    public bool EmbedTaskbar { get; set; }
    public bool PanelAtTop { get; set; }
    public string? PanelMonitor { get; set; }
    public int RefreshMilliseconds { get; set; } = 1000;
    public bool GlobalShortcutEnabled { get; set; }
    public string Theme { get; set; } = "Dark";
    public string Language { get; set; } = "en";
    public string? SelectedAdapterId { get; set; }
    public int WidgetFontSize { get; set; } = WidgetAppearance.DefaultFontSize;
    public int BackgroundOpacity { get; set; } = WidgetAppearance.DefaultBackgroundOpacity;
}
