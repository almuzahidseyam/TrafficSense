using System.IO;
using System.Text.Json;

namespace TrafficSense;

// Backups have a separate, versioned format. Startup's forgiving settings loader
// must never turn a malformed import into a successful restore of defaults.
internal static class SettingsBackup
{
    private const int MaximumBytes = 64 * 1024;
    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true };

    internal static SettingsSaveResult Export(string path, AppSettings settings)
    {
        string temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            var json = JsonSerializer.Serialize(new
            {
                Format = "TrafficSense.Settings", Version = 1, Settings = SettingsStore.Normalize(settings)
            }, Options);
            if (System.Text.Encoding.UTF8.GetByteCount(json) > MaximumBytes)
                return new(false, "Backup is too large.");
            File.WriteAllText(temporary, json);
            File.Move(temporary, path, overwrite: true);
            return new(true);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        { return new(false, error.Message); }
        finally
        {
            try { File.Delete(temporary); }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException) { }
        }
    }

    internal static AppSettings Read(string path)
    {
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        if (stream.Length > MaximumBytes) throw new JsonException("Backup is too large.");
        // Bound the read even if another process extends the file while it is open.
        var bytes = new byte[MaximumBytes + 1];
        int count = 0, read;
        while (count < bytes.Length && (read = stream.Read(bytes, count, bytes.Length - count)) != 0) count += read;
        if (count > MaximumBytes) throw new JsonException("Backup is too large.");
        using var document = JsonDocument.Parse(bytes.AsMemory(0, count));
        var root = document.RootElement;
        RequireProperties(root, ["Format", "Version", "Settings"]);
        if (root.GetProperty("Format").ValueKind != JsonValueKind.String ||
            root.GetProperty("Format").GetString() != "TrafficSense.Settings" ||
            root.GetProperty("Version").ValueKind != JsonValueKind.Number ||
            !root.GetProperty("Version").TryGetInt32(out int version) || version != 1)
            throw new JsonException("Unsupported backup format or version.");
        var data = root.GetProperty("Settings");
        var defaults = JsonSerializer.SerializeToElement(new AppSettings());
        // New preferences are optional in version 1 backups. Older complete
        // backups remain importable; unknown/duplicate and missing legacy keys
        // are still rejected rather than silently restoring defaults.
        var expected = defaults.EnumerateObject().Select(property => property.Name).ToHashSet(StringComparer.Ordinal);
        if (data.ValueKind != JsonValueKind.Object) throw new JsonException("Expected settings object.");
        foreach (var property in data.EnumerateObject())
            if (!expected.Remove(property.Name)) throw new JsonException("Unknown or duplicate preference.");
        string[] optional = [nameof(AppSettings.EmbedTaskbar), nameof(AppSettings.LayoutSkinFile), nameof(AppSettings.LayoutSkinCompact), nameof(AppSettings.DisplayMetrics), nameof(AppSettings.TaskbarPanel), nameof(AppSettings.PanelAtTop), nameof(AppSettings.PanelMonitor),
            nameof(AppSettings.DailyLimitGiB), nameof(AppSettings.MonthlyLimitGiB), nameof(AppSettings.SensorFile), nameof(AppSettings.WidgetFontFamily),
            nameof(AppSettings.WidgetBold), nameof(AppSettings.WidgetBackground), nameof(AppSettings.DownloadColor), nameof(AppSettings.UploadColor), nameof(AppSettings.MetricsColor)];
        if (expected.Except(optional).Any()) throw new JsonException("Incomplete backup.");
        var settings = data.Deserialize<AppSettings>() ?? throw new JsonException("Missing settings.");
        // Reject out-of-range values rather than silently normalizing a damaged backup.
        var normalized = JsonSerializer.SerializeToElement(SettingsStore.Normalize(settings));
        var original = JsonSerializer.SerializeToElement(settings);
        if (original.EnumerateObject().Any(property => property.Value.GetRawText() != normalized.GetProperty(property.Name).GetRawText()) ||
            settings.ScreenLeft.HasValue != settings.ScreenTop.HasValue ||
            settings.WindowLeft is < int.MinValue or > int.MaxValue || settings.WindowTop is < int.MinValue or > int.MaxValue)
            throw new JsonException("Invalid settings values.");
        return settings;
    }

    private static void RequireProperties(JsonElement value, IEnumerable<string> expected)
    {
        if (value.ValueKind != JsonValueKind.Object) throw new JsonException("Expected an object.");
        var remaining = new HashSet<string>(expected, StringComparer.Ordinal);
        foreach (var property in value.EnumerateObject())
            if (!remaining.Remove(property.Name)) throw new JsonException("Unknown or duplicate property.");
        if (remaining.Count != 0) throw new JsonException("Incomplete backup.");
    }
}
