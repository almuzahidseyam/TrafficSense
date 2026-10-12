using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using TrafficSense;

internal static partial class CsvProbe
{
    private static void VerifyCopyUsage(UiLanguage language, DateTimeOffset now, string root)
    {
        language.Select("en");
        var today = DateOnly.FromDateTime(now.Date);
        var older = today.AddDays(-35);
        var ledger = new UsageLedger([new(older, long.MaxValue, long.MaxValue), new(today.AddDays(-1), 0, 0), new(today, 120, 60)]);
        var copied = new List<string>();
        Exception? failure = null;
        var log = new DiagnosticLog(Path.Combine(root, "copy-logs"));
        // Inject the clipboard boundary: never read or replace the user's clipboard.
        var window = new HistoryWindow(language, ledger, () => now, new(), () => false, diagnostics: log,
            copyText: text => { if (failure != null) throw failure; copied.Add(text); }) { MaxHeight = 360 };
        try
        {
            window.Width = window.MinWidth; window.Show(); Drain(window);
            var button = Field<Button>(window, "_copyDay");
            var status = Field<TextBlock>(window, "_copyDayStatus");
            var grid = Field<DataGrid>(window, "_dailyUsage");
            var chart = Field<DailyUsageChart>(window, "_dailyChart");
            Check(!button.IsEnabled && button.ToolTip.ToString() == language[TextKey.CopyDaySelect] &&
                !window.CopySelectedDay() && copied.Count == 0 && status.Text == "", "No selection enabled copying or changed the clipboard boundary.");
            Check(button.TabIndex == 5 && Field<Button>(window, "_export").TabIndex == 6 && Field<Button>(window, "_close").TabIndex == 7,
                "Copy/CSV/close keyboard order is wrong.");
            SelectChartDay(window, today); Click(button);
            string english = today.ToString("yyyy-MM-dd") + "\r\nDownload: 120 bytes\r\nUpload: 60 bytes\r\nTotal: 180 bytes";
            Check(copied.Single() == english && status.Text == "Copied usage for " + today.ToString("yyyy-MM-dd") + "." &&
                AutomationProperties.GetName(status) == status.Text && AutomationProperties.GetLiveSetting(status) == AutomationLiveSetting.Polite && !ledger.Dirty,
                "English copy missed exact bytes, ISO date, Windows line breaks, accessible success or changed usage.");
            var selected = grid.SelectedItem;
            window.Refresh(); Check(status.Text != "", "Unchanged refresh erased copy feedback.");
            ledger.Record(new(7, 3), now); Click(button);
            Check(copied[^1].EndsWith("Download: 127 bytes\r\nUpload: 63 bytes\r\nTotal: 190 bytes") &&
                ReferenceEquals(grid.SelectedItem, selected) && chart.HighlightedDay == today && ledger.Dirty,
                "Copy missed pending traffic, changed selection/highlight or marked pending usage saved.");
            ledger.Record(new(1, 2), now); window.Refresh();
            Check(status.Text == "", "Traffic changes retained stale success feedback.");
            language.Select("bn"); Click(button);
            Check(((TextBlock)button.Content).Text == "নির্বাচিত দিনের হিসাব কপি করুন" &&
                copied[^1] == today.ToString("yyyy-MM-dd") + "\r\nডাউনলোড: 128 বাইট\r\nআপলোড: 65 বাইট\r\nসর্বমোট: 193 বাইট" &&
                status.Text.EndsWith("দিনের হিসাব কপি হয়েছে।"), "Bengali clipboard text/button/status is incorrect.");
            language.Select("en"); Check(status.Text == "", "Language change falsely reported current-language content already copied.");
            grid.SelectedItem = grid.Items.Cast<UsageDayRow>().Single(row => row.Day.Date == older);
            Click(button);
            Check(chart.HighlightedDay == null && button.IsEnabled && copied[^1] == older.ToString("yyyy-MM-dd") +
                "\r\nDownload: 9223372036854775807 bytes\r\nUpload: 9223372036854775807 bytes\r\nTotal: 18446744073709551614 bytes",
                "Older table selection could not copy or maximum totals overflowed/rounded.");
            foreach (string code in new[] { "en", "bn" })
            {
                language.Select(code);
                foreach (var error in new Exception[] { new ExternalException("clipboard busy"), new InvalidOperationException("unavailable"), new System.Threading.ThreadStateException() })
                {
                    failure = error; int before = copied.Count;
                    Check(!window.CopySelectedDay() && copied.Count == before && status.Text == language[TextKey.CopyDayFailed] && button.IsEnabled &&
                        ReferenceEquals(grid.SelectedItem, grid.Items.Cast<UsageDayRow>().Single(row => row.Day.Date == older)),
                        "Clipboard failure crashed, reported success, changed selection or prevented retry.");
                    failure = null; Click(button);
                    Check(copied.Count == before + 1 && status.Text != language[TextKey.CopyDayFailed], "Copy retry did not recover.");
                }
            }
            Check(File.ReadAllText(log.FilePath).Contains("UsageCopy"), "Clipboard failure was not diagnosed.");
            var first = Field<TextBox>(window, "_fromDate"); var last = Field<TextBox>(window, "_toDate");
            first.Text = last.Text = today.AddDays(-1).ToString("yyyy-MM-dd");
            Select(Field<ComboBox>(window, "_period"), TextKey.CsvDateRange); Drain(window);
            Check(!button.IsEnabled && status.Text == "", "Filtering out a selected day retained copy eligibility/status.");
            SelectChartDay(window, today.AddDays(-1)); Click(button);
            Check(copied[^1].EndsWith("ডাউনলোড: 0 বাইট\r\nআপলোড: 0 বাইট\r\nসর্বমোট: 0 বাইট"), "Measured zero day was not copied exactly.");
            first.Text = "invalid"; int count = copied.Count; Click(button);
            Check(!button.IsEnabled && copied.Count == count && status.Text == "", "Invalid filter allowed a stale copy.");
            first.Text = "2020-01-01"; last.Text = "2020-01-02"; Click(button);
            Check(!button.IsEnabled && copied.Count == count && status.Text == "", "Empty filter allowed a stale copy.");
            Select(Field<ComboBox>(window, "_period"), TextKey.CsvAllDays); SelectChartDay(window, today); Click(button); Drain(window);
            var outer = (ScrollViewer)window.Content;
            var bounds = button.TransformToAncestor(outer).TransformBounds(new Rect(button.RenderSize));
            Check(bounds.Left >= 0 && bounds.Right <= outer.ViewportWidth + 1 && bounds.Top >= -1 && bounds.Bottom <= outer.ViewportHeight + 1,
                "Copy button is clipped at Bengali minimum width/small height.");
            var feedbackBounds = status.TransformToAncestor(outer).TransformBounds(new Rect(status.RenderSize));
            Check(feedbackBounds.Top >= -1 && feedbackBounds.Bottom <= outer.ViewportHeight + 1,
                "Copy feedback was left below the small viewport.");
            Render(window, "copy-day-bn");
            Check(ledger.Dirty && ledger.Today(today).Received == 128, "Copying altered stored usage or dirty state.");
        }
        finally
        {
            window.Close();
            Check(!Field<Button>(window, "_copyDay").IsEnabled && Field<TextBlock>(window, "_copyDayStatus").Text == "",
                "Closing history retained copy eligibility/status.");
        }
        Console.WriteLine("PASS: selected-day copy boundary, bilingual exact/zero/maximum bytes and CRLF, latest unsaved traffic, no-selection/filter/close guards, clipboard failure/retry/logging, accessible feedback and small-screen layout without touching the user's clipboard.");
    }
}
