using System.IO;
using System.Reflection;
using System.Text;
using System.Text.Json;

namespace TrafficSense;

// Best-effort local diagnostics. Logging must never cause another app error.
internal sealed class DiagnosticLog(string directory)
{
    internal const int MaximumFileBytes = 256 * 1024;
    private const int MaximumDetailsCharacters = 16_000;
    private readonly object _sync = new();
    public static DiagnosticLog Default { get; } = new(Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "TrafficSense", "logs"));
    public string DirectoryPath => directory;
    public string FilePath => Path.Combine(DirectoryPath, "errors.jsonl");

    public string? Write(string eventName, Exception error, bool fatal = false)
    {
        try { return Write(eventName, error.ToString(), fatal); }
        catch (Exception) { return null; }
    }

    public string? Write(string eventName, string? details, bool fatal = false)
    {
        bool entered = false;
        try
        {
            // A terminating worker must not wait for a lock held by another
            // failing thread. Ordinary diagnostics have a short bounded wait.
            entered = Monitor.TryEnter(_sync, fatal ? 0 : 250);
            if (!entered) return null;
            details ??= "";
            if (details.Length > MaximumDetailsCharacters)
                details = details[..MaximumDetailsCharacters] + " [truncated]";
            var entry = new
            {
                TimestampUtc = DateTimeOffset.UtcNow,
                ProcessId = Environment.ProcessId,
                Version = typeof(DiagnosticLog).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion,
                Runtime = Environment.Version.ToString(),
                Event = eventName.Length > 128 ? eventName[..128] : eventName,
                Fatal = fatal,
                Details = details
            };
            byte[] bytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(entry) + "\n");
            Directory.CreateDirectory(directory);
            if (File.Exists(FilePath) && new FileInfo(FilePath).Length + bytes.Length > MaximumFileBytes)
            {
                string previous = Path.Combine(directory, "errors.1.jsonl");
                if (File.Exists(previous)) File.Move(previous, Path.Combine(directory, "errors.2.jsonl"), true);
                File.Move(FilePath, previous, true);
            }
            using var stream = new FileStream(FilePath, FileMode.Append, FileAccess.Write, FileShare.Read);
            stream.Write(bytes);
            stream.Flush(flushToDisk: true);
            return FilePath;
        }
        catch (Exception) { return null; }
        finally { if (entered) Monitor.Exit(_sync); }
    }
}
