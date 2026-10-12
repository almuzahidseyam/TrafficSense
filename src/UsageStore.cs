using System.IO;
using System.Text.Json;

namespace TrafficSense;

internal sealed class UsageStore(string path, DiagnosticLog? diagnostics = null)
{
    public UsageLedger Load()
    {
        try
        {
            if (!File.Exists(path)) return new();
            if (new FileInfo(path).Length > 1024 * 1024) throw new IOException("Usage file exceeds 1 MiB.");
            return new(JsonSerializer.Deserialize<UsageDay[]>(File.ReadAllText(path)));
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or JsonException)
        { diagnostics?.Write("UsageLoad", error); return new(); }
    }

    public bool Save(UsageLedger ledger)
    {
        string temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
            File.WriteAllText(temporary, JsonSerializer.Serialize(ledger.Days));
            File.Move(temporary, path, overwrite: true);
            ledger.MarkSaved();
            return true;
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or JsonException)
        { diagnostics?.Write("UsageSave", error); return false; }
        finally
        {
            try { File.Delete(temporary); }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException) { }
        }
    }
}
