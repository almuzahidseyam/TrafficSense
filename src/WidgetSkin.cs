using System.IO;
using System.Text.Json;

namespace TrafficSense;

internal sealed record WidgetSkin(int FontSize, string FontFamily, bool Bold, int BackgroundOpacity,
    string Background, string Download, string Upload, string Metrics)
{
    internal static string Color(string? value) => value is { Length: 7 } && value[0] == '#' && value[1..].All(Uri.IsHexDigit) ? value.ToUpperInvariant() : "Auto";
    internal static string Family(string? value) => !string.IsNullOrWhiteSpace(value) && value.Length <= 128 &&
        value.All(c => char.IsLetterOrDigit(c) || c is ' ' or ',' or '-') ? value : "Segoe UI, Nirmala UI, Kalpurush";
    internal static WidgetSkin From(AppSettings settings) => new(settings.WidgetFontSize, settings.WidgetFontFamily, settings.WidgetBold,
        settings.BackgroundOpacity, settings.WidgetBackground, settings.DownloadColor, settings.UploadColor, settings.MetricsColor);

    internal void Apply(AppSettings settings)
    {
        settings.WidgetFontSize = FontSize; settings.WidgetFontFamily = FontFamily; settings.WidgetBold = Bold;
        settings.BackgroundOpacity = BackgroundOpacity; settings.WidgetBackground = Background;
        settings.DownloadColor = Download; settings.UploadColor = Upload; settings.MetricsColor = Metrics;
    }

    internal static WidgetSkin Read(string path)
    {
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        if (stream.Length > 16 * 1024) throw new JsonException("Skin file is too large.");
        byte[] buffer = new byte[16 * 1024 + 1]; int count = 0, read;
        while (count < buffer.Length && (read = stream.Read(buffer, count, buffer.Length - count)) != 0) count += read;
        if (count > 16 * 1024) throw new JsonException("Skin file is too large.");
        using var document = JsonDocument.Parse(buffer.AsMemory(0, count), new JsonDocumentOptions { MaxDepth = 4 });
        var root = document.RootElement;
        if (root.ValueKind != JsonValueKind.Object || !root.TryGetProperty("Format", out var format) ||
            format.ValueKind != JsonValueKind.String || format.GetString() != "TrafficSense.Skin" ||
            !root.TryGetProperty("Version", out var version) || version.ValueKind != JsonValueKind.Number ||
            !version.TryGetInt32(out int number) || number != 1)
            throw new JsonException("Unsupported skin.");
        if (!root.EnumerateObject().Select(item => item.Name).Order().SequenceEqual(new[] { "Appearance", "Format", "Version" }))
            throw new JsonException("Unknown or duplicate skin envelope properties.");
        var appearance = root.GetProperty("Appearance");
        string[] required = ["FontSize", "FontFamily", "Bold", "BackgroundOpacity", "Background", "Download", "Upload", "Metrics"];
        if (appearance.ValueKind != JsonValueKind.Object || !appearance.EnumerateObject().Select(item => item.Name).Order().SequenceEqual(required.Order()))
            throw new JsonException("Incomplete or unknown skin properties.");
        var skin = appearance.Deserialize<WidgetSkin>() ?? throw new JsonException("Missing appearance.");
        if (skin.FontSize != WidgetAppearance.NormalizeFontSize(skin.FontSize) || skin.BackgroundOpacity != WidgetAppearance.NormalizeBackgroundOpacity(skin.BackgroundOpacity) ||
            skin.FontFamily != Family(skin.FontFamily) || new[] { skin.Background, skin.Download, skin.Upload, skin.Metrics }.Any(color => color != Color(color)))
            throw new JsonException("Invalid skin values.");
        return skin;
    }

    internal SettingsSaveResult Export(string path)
    {
        string temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            File.WriteAllText(temporary, JsonSerializer.Serialize(new { Format = "TrafficSense.Skin", Version = 1, Appearance = this }, new JsonSerializerOptions { WriteIndented = true }));
            File.Move(temporary, path, true); return new(true);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException) { return new(false, error.Message); }
        finally { try { File.Delete(temporary); } catch (Exception error) when (error is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException) { } }
    }
}
