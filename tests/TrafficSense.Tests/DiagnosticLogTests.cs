using System.Text.Json;
using Xunit;

namespace TrafficSense.Tests;

public sealed class DiagnosticLogTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "TrafficSense.LogTests", Guid.NewGuid().ToString("N"));

    [Fact]
    public void RecordsNestedExceptionAndUsefulMetadataAsOneJsonLine()
    {
        var log = new DiagnosticLog(_directory);
        var error = new InvalidOperationException("বাংলা error\nsecond line", new IOException("inner failure"));
        Assert.Equal(log.FilePath, log.Write("Startup", error, fatal: true));
        string line = Assert.Single(File.ReadAllLines(log.FilePath));
        using var entry = JsonDocument.Parse(line);
        Assert.Equal("Startup", entry.RootElement.GetProperty("Event").GetString());
        Assert.True(entry.RootElement.GetProperty("Fatal").GetBoolean());
        Assert.Equal(Environment.ProcessId, entry.RootElement.GetProperty("ProcessId").GetInt32());
        Assert.True(entry.RootElement.GetProperty("TimestampUtc").GetDateTimeOffset() <= DateTimeOffset.UtcNow);
        Assert.False(string.IsNullOrWhiteSpace(entry.RootElement.GetProperty("Runtime").GetString()));
        Assert.False(string.IsNullOrWhiteSpace(entry.RootElement.GetProperty("Version").GetString()));
        Assert.Contains("inner failure", entry.RootElement.GetProperty("Details").GetString());
        Assert.Contains("বাংলা", entry.RootElement.GetProperty("Details").GetString());
    }

    [Fact]
    public void ConcurrentSuccessfulWritesRemainWholeAndDistinct()
    {
        var log = new DiagnosticLog(_directory);
        var written = new System.Collections.Concurrent.ConcurrentBag<int>();
        Parallel.For(0, 16, index =>
        {
            if (log.Write("Concurrent", index.ToString()) != null) written.Add(index);
        });
        var values = File.ReadAllLines(log.FilePath).Select(line =>
        {
            using var entry = JsonDocument.Parse(line);
            return int.Parse(entry.RootElement.GetProperty("Details").GetString()!);
        }).Order().ToArray();
        Assert.NotEmpty(written);
        // Best-effort writes can time out under contention. Every acknowledged
        // write must still be present exactly once as a complete JSON record.
        Assert.Equal(written.Order(), values);
    }

    [Fact]
    public void RotationKeepsThreeBoundedFilesAndNewestError()
    {
        var log = new DiagnosticLog(_directory);
        for (int index = 0; index < 100; index++)
            Assert.NotNull(log.Write("Rotation", index + ":" + new string('x', 16_000)));
        string[] files = Directory.GetFiles(_directory);
        Assert.Equal(3, files.Length);
        Assert.All(files, file => Assert.InRange(new FileInfo(file).Length, 1, DiagnosticLog.MaximumFileBytes));
        Assert.Contains("99:", File.ReadAllLines(log.FilePath).Last());
        Assert.All(files.SelectMany(File.ReadAllLines), line =>
        {
            using var entry = JsonDocument.Parse(line);
            Assert.Contains("[truncated]", entry.RootElement.GetProperty("Details").GetString());
        });
    }

    [Fact]
    public void OversizedUnicodeDiagnosticsStayBoundedAndParseable()
    {
        var log = new DiagnosticLog(_directory);
        Assert.NotNull(log.Write(new string('ক', 500), new string('ক', 100_000)));
        Assert.InRange(new FileInfo(log.FilePath).Length, 1, DiagnosticLog.MaximumFileBytes);
        using var entry = JsonDocument.Parse(File.ReadAllText(log.FilePath));
        Assert.Equal(128, entry.RootElement.GetProperty("Event").GetString()!.Length);
        Assert.EndsWith("[truncated]", entry.RootElement.GetProperty("Details").GetString());
    }

    [Fact]
    public void LockedFileOrInvalidDirectoryDoesNotThrowOrDestroyPreviousLog()
    {
        var log = new DiagnosticLog(_directory);
        Assert.NotNull(log.Write("Previous", "keep this"));
        byte[] previous = File.ReadAllBytes(log.FilePath);
        using (var locked = new FileStream(log.FilePath, FileMode.Open, FileAccess.Read, FileShare.None))
            Assert.Null(log.Write("Locked", new Exception("failure"), fatal: true));
        Assert.Equal(previous, File.ReadAllBytes(log.FilePath));
        Assert.Null(new DiagnosticLog(log.FilePath).Write("InvalidDirectory", "failure"));
    }

    [Fact]
    public void BrokenExceptionFormattingCannotCrashLogging()
    {
        Assert.Null(new DiagnosticLog(_directory).Write("Broken", new BrokenException()));
    }

    [Fact]
    public void CorruptSettingsAreLoggedAndDefaultsStillLoad()
    {
        Directory.CreateDirectory(_directory);
        string path = Path.Combine(_directory, "settings.json");
        File.WriteAllText(path, "{broken");
        var log = new DiagnosticLog(Path.Combine(_directory, "logs"));
        Assert.Equal("Dark", new SettingsStore(path, log).Load().Theme);
        Assert.Contains("SettingsLoad", File.ReadAllText(log.FilePath));
        Assert.Equal("{broken", File.ReadAllText(path));
    }

    private sealed class BrokenException : Exception
    {
        public override string ToString() => throw new InvalidOperationException("Formatting failed.");
    }

    public void Dispose()
    {
        string root = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "TrafficSense.LogTests")) + Path.DirectorySeparatorChar;
        Assert.StartsWith(root, Path.GetFullPath(_directory));
        if (Directory.Exists(_directory)) Directory.Delete(_directory, recursive: true);
    }
}
