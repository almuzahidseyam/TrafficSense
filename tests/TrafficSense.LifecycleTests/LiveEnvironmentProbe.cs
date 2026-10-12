using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text.Json;
using TrafficSense;
using Forms = System.Windows.Forms;

internal static class LiveEnvironmentProbe
{
    [UnmanagedFunctionPointer(CallingConvention.Winapi)] private delegate uint NativeCounters(ref Guid id, out ulong received, out ulong sent);
    internal static int Run(string fixture, string reportPath)
    {
        nint module = NativeLibrary.Load(Path.GetFullPath(fixture));
        try
        {
            var read = Marshal.GetDelegateForFunctionPointer<NativeCounters>(NativeLibrary.GetExport(module, "ReadNicCounters"));
            var watch = Stopwatch.StartNew(); var rows = new List<object>(); var selected = new NetworkAdapterSelection(); var calc = new NetworkSpeedCalculator();
            var beforeAll = NetworkSnapshotReader.Read(() => watch.Elapsed); string? idText = selected.Select(beforeAll, null)?.Id;
            if (idText == null) throw new InvalidOperationException("No connected interface is available for a live comparison.");
            Guid id = Guid.Parse(idText); long firstRx = 0, firstTx = 0, lastRx = 0, lastTx = 0, ledgerRx = 0, ledgerTx = 0;
            for (int i = 0; i <= 20; i++)
            {
                uint error = read(ref id, out ulong rx0, out ulong tx0); if (error != 0) throw new InvalidOperationException("Native interface read failed: " + error);
                var adapters = NetworkSnapshotReader.Read(() => watch.Elapsed); var snapshot = adapters.SingleOrDefault(a => a.Id == idText)?.Snapshot;
                error = read(ref id, out ulong rx1, out ulong tx1); if (error != 0 || snapshot == null) throw new InvalidOperationException("Interface changed during live comparison.");
                var sample = snapshot.Value;
                if ((ulong)sample.BytesReceived < rx0 || (ulong)sample.BytesReceived > rx1 || (ulong)sample.BytesSent < tx0 || (ulong)sample.BytesSent > tx1)
                    throw new InvalidOperationException("Managed counters were outside independent native counter brackets.");
                var speed = calc.Update([sample], watch.Elapsed); ledgerRx += calc.LastTraffic.Received; ledgerTx += calc.LastTraffic.Sent;
                if (i == 0) { firstRx = sample.BytesReceived; firstTx = sample.BytesSent; }
                lastRx = sample.BytesReceived; lastTx = sample.BytesSent;
                rows.Add(new { Seconds = watch.Elapsed.TotalSeconds, sample.BytesReceived, sample.BytesSent, NativeReceivedBefore = rx0, NativeReceivedAfter = rx1,
                    NativeSentBefore = tx0, NativeSentAfter = tx1, speed.DownloadBytesPerSecond, speed.UploadBytesPerSecond, Quality = calc.Quality.ToString() });
                if (i != 20) Thread.Sleep(500);
            }
            if (ledgerRx != lastRx - firstRx || ledgerTx != lastTx - firstTx) throw new InvalidOperationException("Live byte conservation failed.");
            var displays = new List<object>();
            foreach (var screen in Forms.Screen.AllScreens)
            {
                using var client = new EmbeddedTaskbarClient(inspectOnly: true); var b = screen.Bounds; string inspection;
                try { inspection = client.Update(new(new(b.Left,b.Top,b.Right,b.Bottom), "read-only diagnostic", false)).GetAwaiter().GetResult().Reason; }
                catch (Exception e) when (e is InvalidDataException or IOException or OperationCanceledException or InvalidOperationException or JsonException) { inspection = "Inspection unavailable: " + e.GetType().Name; }
                displays.Add(new { b.Left, b.Top, b.Width, b.Height, screen.Primary, Taskbar = inspection });
            }
            var report = new { Format = "TrafficSense.LiveEnvironment", Version = 1, TestedAt = DateTimeOffset.UtcNow, Windows = Environment.OSVersion.VersionString,
                Method = "Passive, same-interface GetIfEntry2 counter brackets; no NIC/VPN/shell configuration changes, no generated transfer.",
                ConnectedInterfaces = beforeAll.Count(a => a.CanMonitor), SelectedType = beforeAll.Single(a => a.Id == idText).Type.ToString(),
                Samples = rows, ReceivedBytes = ledgerRx, SentBytes = ledgerTx, CounterBracketPass = true, ByteConservationPass = true, Displays = displays,
                Unverified = new[] { "Controlled LAN payload throughput", "VPN reconnect and split routes", "Physical monitor removal / mixed DPI", "Explorer restart", "Long-duration run" } };
            File.WriteAllText(reportPath, JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }));
            Console.WriteLine($"PASS: 21 live same-interface/native counter brackets; exact {ledgerRx} received / {ledgerTx} sent bytes; {displays.Count} display(s) inspected without attachment. Report: {reportPath}");
            return 0;
        }
        finally { NativeLibrary.Free(module); }
    }
}
