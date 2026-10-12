using System.Globalization;
using System.IO;
using System.Text;

namespace TrafficSense;

internal readonly record struct UsageExportResult(bool Success, string? Error = null);

internal static class UsageCsv
{
    internal const string Header = "Date,DownloadBytes,UploadBytes,TotalBytes";
    internal static string FormatDay(UsageDay day) => string.Create(CultureInfo.InvariantCulture,
        $"{day.Date:yyyy-MM-dd},{day.Received},{day.Sent},{(decimal)day.Received + day.Sent}");
    internal static string CopyDay(UsageDay day) => Header + "\r\n" + FormatDay(day) + "\r\n";

    internal static UsageExportResult Export(string path, UsageLedger ledger, UsageDateRange? range = null)
    {
        if (range is { IsValid: false }) return new(false, "Invalid date range.");
        string temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            // Called on the UI thread after the file dialog closes: include the
            // latest sampled totals without marking pending usage as persisted.
            var days = ledger.Days.Where(day => range == null || range.Value.Contains(day.Date)).ToArray();
            using (var writer = new StreamWriter(temporary, false, new UTF8Encoding(true)))
            {
                writer.NewLine = "\r\n";
                writer.WriteLine(Header);
                foreach (var day in days)
                {
                    // Only fixed headers, ISO dates and nonnegative numbers are
                    // written. Decimal preserves the exact sum of two Int64 totals.
                    writer.WriteLine(FormatDay(day));
                }
            }
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
}
