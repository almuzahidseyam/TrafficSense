namespace TrafficSense;

public class AppSettings
{
    // Retain the legacy WPF coordinates to migrate existing settings.
    public double WindowLeft { get; set; } = -1;
    public double WindowTop { get; set; } = -1;
    public int? ScreenLeft { get; set; }
    public int? ScreenTop { get; set; }
    public string Theme { get; set; } = "Dark";
}
