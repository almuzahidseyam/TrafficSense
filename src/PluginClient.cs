using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.Json;

namespace TrafficSense;

internal sealed class PluginClient : IDisposable
{
    private readonly Process _process;
    private readonly WorkerJob _job;
    private int _disposed, _busy;
    internal int WorkerId => _process.Id;
    internal PluginClient(string executable, string dll, string? configRoot = null)
    {
        var start = new ProcessStartInfo(executable) { UseShellExecute = false, CreateNoWindow = true, RedirectStandardInput = true, RedirectStandardOutput = true };
        start.ArgumentList.Add("--plugin-worker"); start.ArgumentList.Add(dll);
        start.Environment.Remove("TRAFFICSENSE_PLUGIN_CONFIG_ROOT");
        if (configRoot != null) start.Environment["TRAFFICSENSE_PLUGIN_CONFIG_ROOT"] = configRoot;
        _process = Process.Start(start) ?? throw new IOException("Could not start plugin worker.");
        try { _job = new(_process); } catch { try { _process.Kill(); } finally { _process.Dispose(); } throw; }
    }
    internal async Task<PluginSample> Request(PluginRequest request, TimeSpan? timeout = null)
    {
        if (Volatile.Read(ref _disposed) != 0) throw new ObjectDisposedException(nameof(PluginClient));
        if (Interlocked.Exchange(ref _busy, 1) != 0) throw new InvalidOperationException("A plugin request is already running.");
        using var deadline = new CancellationTokenSource(timeout ?? TimeSpan.FromSeconds(3));
        try
        {
            await _process.StandardInput.WriteLineAsync(JsonSerializer.Serialize(request).AsMemory(), deadline.Token).ConfigureAwait(false);
            await _process.StandardInput.FlushAsync(deadline.Token).ConfigureAwait(false);
            var text = new StringBuilder(); var buffer = new char[1];
            while (true)
            {
                if (await _process.StandardOutput.ReadAsync(buffer.AsMemory(), deadline.Token).ConfigureAwait(false) == 0) throw new EndOfStreamException("Plugin stopped or crashed.");
                if (buffer[0] == '\n') break;
                if (text.Length >= 512 * 1024) throw new InvalidDataException("Plugin output exceeds the limit.");
                text.Append(buffer[0]);
            }
            using var document = JsonDocument.Parse(text.ToString(),new JsonDocumentOptions { MaxDepth=8 });
            if (document.RootElement.TryGetProperty("Error",out var error) && error.ValueKind == JsonValueKind.String) throw new InvalidDataException(error.GetString());
            var sample = document.RootElement.Deserialize<PluginSample>() ?? throw new InvalidDataException("Invalid plugin output.");
            if (sample.Items is not { Length: <= 32 } || sample.Commands is not { Length: <= 32 } || sample.Api is < 1 or > 8 || sample.Name?.Length > 256 || sample.Tooltip?.Length > 256 ||
                sample.Items.Any(i => i == null || i.Id is not { Length: > 0 and <= 256 } || i.Name?.Length > 256 || i.Label?.Length > 256 || i.Value?.Length > 256 || i.Png?.Length > 192 * 1024))
                throw new InvalidDataException("Invalid plugin fields.");
            return sample;
        }
        catch { Dispose(); throw; }
        finally { Volatile.Write(ref _busy, 0); }
    }
    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0) return;
        // Idle plugins may persist settings in DLL detach/destructors. Give the
        // normal EOF path a short deadline, then let the job enforce ownership.
        // A busy/crashed/timed-out worker must still be terminated immediately.
        if(Volatile.Read(ref _busy)==0)
        {
            try { _process.StandardInput.Close(); _process.WaitForExit(300); }
            catch(Exception e) when(e is IOException or InvalidOperationException or System.ComponentModel.Win32Exception) { }
        }
        _job.Dispose();
        try { if (!_process.HasExited) _process.Kill(entireProcessTree: true); }
        catch (Exception e) when (e is InvalidOperationException or System.ComponentModel.Win32Exception) { }
        _process.Dispose();
    }
}
