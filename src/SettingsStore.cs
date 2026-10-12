using System.IO;
using System.Text.Json;

namespace TrafficSense;

public readonly record struct SettingsSaveResult(bool Success, string? Error = null);

internal sealed class SettingsStore(string filePath, DiagnosticLog? diagnostics = null)
{
    public AppSettings Load()
    {
        try
        {
            if (File.Exists(filePath))
                return Normalize(JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(filePath)) ?? new());
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or JsonException)
        {
            // A corrupt/unreadable settings file must not prevent the widget starting.
            diagnostics?.Write("SettingsLoad", error);
        }

        return new AppSettings();
    }

    public SettingsSaveResult Save(AppSettings settings)
    {
        string temporaryFile = filePath + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(filePath))!);
            var json = JsonSerializer.Serialize(Normalize(settings), new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(temporaryFile, json);
            File.Move(temporaryFile, filePath, overwrite: true);
            return new(true);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or JsonException)
        {
            return new(false, error.Message);
        }
        finally
        {
            try { File.Delete(temporaryFile); }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException) { }
        }
    }

    internal static AppSettings Normalize(AppSettings settings) => new()
    {
        WindowLeft = double.IsFinite(settings.WindowLeft) ? settings.WindowLeft : -1,
        WindowTop = double.IsFinite(settings.WindowTop) ? settings.WindowTop : -1,
        ScreenLeft = settings.ScreenLeft,
        ScreenTop = settings.ScreenTop,
        PositionLocked = settings.PositionLocked,
        SpeedInBits = settings.SpeedInBits,
        DisplayMetrics = settings.DisplayMetrics & (int)SystemMetric.All,
        DailyLimitGiB = settings.DailyLimitGiB is >= 0 and <= 10000 ? settings.DailyLimitGiB : 0,
        MonthlyLimitGiB = settings.MonthlyLimitGiB is >= 0 and <= 10000 ? settings.MonthlyLimitGiB : 0,
        SensorFile = ExternalSensorFile.NormalizePath(settings.SensorFile),
        WidgetFontFamily = WidgetSkin.Family(settings.WidgetFontFamily), WidgetBold = settings.WidgetBold,
        WidgetBackground = WidgetSkin.Color(settings.WidgetBackground), DownloadColor = WidgetSkin.Color(settings.DownloadColor),
        UploadColor = WidgetSkin.Color(settings.UploadColor), MetricsColor = WidgetSkin.Color(settings.MetricsColor),
        TaskbarPanel = settings.TaskbarPanel,
        PanelAtTop = settings.PanelAtTop,
        PanelMonitor = string.IsNullOrWhiteSpace(settings.PanelMonitor) ? null : settings.PanelMonitor.Length <= 128 ? settings.PanelMonitor : null,
        GlobalShortcutEnabled = settings.GlobalShortcutEnabled,
        RefreshMilliseconds = MonitorPreferences.NormalizeInterval(settings.RefreshMilliseconds),
        Theme = settings.Theme == "Light" ? "Light" : "Dark",
        Language = UiLanguage.Normalize(settings.Language),
        SelectedAdapterId = string.IsNullOrWhiteSpace(settings.SelectedAdapterId) ? null : settings.SelectedAdapterId,
        WidgetFontSize = WidgetAppearance.NormalizeFontSize(settings.WidgetFontSize),
        BackgroundOpacity = WidgetAppearance.NormalizeBackgroundOpacity(settings.BackgroundOpacity)
    };
}
