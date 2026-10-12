using System.IO;
using System.Text.Json;

namespace TrafficSense;

internal sealed record ExternalSensor(string Id, string Label, double Value, string Unit);
internal sealed record ExternalSensorSample(DateTimeOffset UpdatedAt, IReadOnlyList<ExternalSensor> Sensors);

// Explicit data-only extension boundary. No DLLs/scripts are loaded or executed.
internal static class ExternalSensorFile
{
    internal const int MaximumBytes = 64 * 1024;
    internal static bool IsFresh(DateTimeOffset timestamp, DateTimeOffset now) => now - timestamp <= TimeSpan.FromSeconds(5) && timestamp - now <= TimeSpan.FromSeconds(1);
    internal static string? NormalizePath(string? path) => path is { Length: >= 3 and <= 1024 } && char.IsAsciiLetter(path[0]) &&
        path[1] == ':' && path[2] is '\\' or '/' && !path.Any(char.IsControl) ? path : null;
    internal static ExternalSensorSample? Read(string? path, DateTimeOffset now)
    {
        if (string.IsNullOrWhiteSpace(path)) return null;
        try
        {
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            if (stream.Length > MaximumBytes) return null;
            byte[] buffer = new byte[MaximumBytes + 1]; int count = 0, read;
            while (count < buffer.Length && (read = stream.Read(buffer, count, buffer.Length - count)) != 0) count += read;
            if (count > MaximumBytes) return null;
            using var document = JsonDocument.Parse(buffer.AsMemory(0, count), new JsonDocumentOptions { MaxDepth = 8 });
            var root = document.RootElement;
            if (root.GetProperty("Format").GetString() != "TrafficSense.Sensors" || root.GetProperty("Version").GetInt32() != 1) return null;
            var timestamp = root.GetProperty("UpdatedAt").GetDateTimeOffset();
            if (!IsFresh(timestamp, now)) return null;
            var array = root.GetProperty("Sensors");
            if (array.ValueKind != JsonValueKind.Array || array.GetArrayLength() is < 1 or > 8) return null;
            var sensors = new List<ExternalSensor>(); var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (var element in array.EnumerateArray())
            {
                string? id = element.GetProperty("Id").GetString(), label = element.GetProperty("Label").GetString(), unit = element.GetProperty("Unit").GetString();
                double value = element.GetProperty("Value").GetDouble();
                if (!Text(id, 48) || !ids.Add(id!) || !Text(label, 48) || !Text(unit, 16) || !double.IsFinite(value) || Math.Abs(value) > 1e12) return null;
                sensors.Add(new(id!, label!, value, unit!));
            }
            return new(timestamp, sensors);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or JsonException or InvalidOperationException or
            KeyNotFoundException or FormatException or ArgumentException or NotSupportedException) { return null; }
    }
    private static bool Text(string? value, int limit) => !string.IsNullOrWhiteSpace(value) && value.Length <= limit && !value.Any(char.IsControl);
}
