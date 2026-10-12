using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace TrafficSense;

// Independent adapter for LHM's published data.json schema. Use numeric
// RawValue, never localized display strings. A temperature remains Celsius.
internal static class LibreSensorBridge
{
    internal static ExternalSensorSample Parse(ReadOnlyMemory<byte> json, DateTimeOffset now)
    {
        if (json.Length > 1024 * 1024) throw new JsonException("Provider response is too large.");
        using var document = JsonDocument.Parse(json, new JsonDocumentOptions { MaxDepth = 48 });
        var sensors = new List<ExternalSensor>(); var seen = new HashSet<string>(StringComparer.Ordinal);
        void Visit(JsonElement node, string hardware)
        {
            if (node.ValueKind != JsonValueKind.Object || sensors.Count >= 8) return;
            if (node.TryGetProperty("HardwareId", out _) && node.TryGetProperty("Text", out var hardwareName)) hardware = hardwareName.GetString() ?? hardware;
            if (node.TryGetProperty("Type", out var type) && type.ValueKind == JsonValueKind.String && type.GetString() == "Temperature" &&
                node.TryGetProperty("RawValue", out var raw) && raw.ValueKind == JsonValueKind.Number && raw.TryGetDouble(out double value) &&
                double.IsFinite(value) && value is >= -100 and <= 300 && node.TryGetProperty("SensorId", out var sensorId) && sensorId.ValueKind == JsonValueKind.String)
            {
                string sourceId = sensorId.GetString() ?? "";
                if (sourceId.Length is > 0 and <= 512 && seen.Add(sourceId))
                {
                    string label = hardware + " " + (node.TryGetProperty("Text", out var text) && text.ValueKind == JsonValueKind.String ? text.GetString() : sourceId);
                    label = new string(label.Where(c => !char.IsControl(c)).Take(48).ToArray()).Trim();
                    if (label.Length == 0) label = "Temperature";
                    string id = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(sourceId)))[..24];
                    sensors.Add(new(id, label, value, "°C"));
                }
            }
            if (node.TryGetProperty("Children", out var children) && children.ValueKind == JsonValueKind.Array)
                foreach (var child in children.EnumerateArray()) Visit(child, hardware);
        }
        Visit(document.RootElement, "");
        if (sensors.Count == 0) throw new JsonException("No numeric temperature sensors were supplied.");
        return new(now, sensors);
    }

    internal static void Write(string path, ExternalSensorSample sample)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        string temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            File.WriteAllText(temporary, JsonSerializer.Serialize(new { Format = "TrafficSense.Sensors", Version = 1, sample.UpdatedAt, sample.Sensors }));
            File.Move(temporary, path, true);
        }
        finally { try { File.Delete(temporary); } catch (IOException) { } catch (UnauthorizedAccessException) { } }
    }
}
