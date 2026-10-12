using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Threading;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using TrafficSense;

internal static class CsvProbe
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    internal static int Run()
    {
        string parent = Path.Combine(Path.GetTempPath(), "TrafficSense.CsvLifecycle");
        string root = Path.Combine(parent, Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var app = new App(@"Local\TrafficSense.Csv." + Guid.NewGuid().ToString("N"), () =>
            new MainWindow(new AppSettings(), _ => new(true), readAdapters: () => []));
        app.InitializeComponent();
        foreach (var type in new[] { typeof(MainWindow), typeof(HistoryWindow) })
        {
            var style = new Style(type);
            style.Setters.Add(new Setter(UIElement.OpacityProperty, 0d));
            style.Setters.Add(new Setter(Window.ShowActivatedProperty, false));
            app.Resources.Add(type, style);
        }
        bool passed = false;
        app.Startup += (_, _) => app.Dispatcher.BeginInvoke(new Action(() =>
        {
            try
            {
                var main = (MainWindow)app.MainWindow; main.Hide();
                var ledger = new UsageLedger(); var language = new UiLanguage();
                var errors = new List<string>(); string? destination = null;
                bool addWhileChoosing = false; int chooses = 0;
                var now = new DateTimeOffset(2026, 10, 12, 12, 0, 0, TimeSpan.FromHours(6));
                var history = new HistoryWindow(language, ledger, () => now, new(), () => true,
                    reportError: errors.Add, chooseExportPath: () =>
                    {
                        chooses++;
                        if (addWhileChoosing) { ledger.Record(new(20, 10), now); addWhileChoosing = false; }
                        return destination;
                    });
                try
                {
                    history.Show(); Drain(history);
                    var export = (Button)typeof(HistoryWindow).GetField("_export", Private)!.GetValue(history)!;
                    var close = (Button)typeof(HistoryWindow).GetField("_close", Private)!.GetValue(history)!;
                    var selectedUsage = Field<TextBlock>(history, "_selectedUsage");
                    Check(selectedUsage.Text == "Selected period usage\nDownload: 0 B\nUpload: 0 B\nTotal: 0 B", "Empty selected totals are not zero.");
                    Check(!export.IsEnabled && export.ToolTip.ToString() == language[TextKey.CsvExportEmpty], "Empty usage export is not explained.");
                    language.Select("bn");
                    Check(export.Content.ToString() == "CSV সংরক্ষণ করুন…" && AutomationProperties.GetHelpText(export).Contains("ডেটা"), "Export did not translate/accessibility help missing.");
                    ledger.Record(new(100, 50), now); history.Refresh();
                    Check(export.IsEnabled && export.ToolTip.ToString() == language[TextKey.CsvExportHint], "Export stayed disabled after traffic.");
                    Click(export);
                    Check(chooses == 1 && Directory.GetFiles(root).Length == 0 && errors.Count == 0 && ledger.Dirty, "Cancel changed files or usage.");
                    destination = Path.Combine(root, "usage.csv"); addWhileChoosing = true;
                    Click(export);
                    Check(File.ReadAllLines(destination)[1] == "2026-10-12,120,60,180" && ledger.Dirty, "Export missed traffic collected while choosing destination or marked usage saved.");
                    Check(selectedUsage.Text == "নির্বাচিত সময়ের ডেটা ব্যবহার\nডাউনলোড: 120 B\nআপলোড: 60 B\nসর্বমোট: 180 B" &&
                        AutomationProperties.GetName(selectedUsage) == selectedUsage.Text, "Bengali totals missed latest traffic or accessible text.");
                    File.WriteAllText(destination, "previous export");
                    using (var locked = new FileStream(destination, FileMode.Open, FileAccess.Read, FileShare.None)) Click(export);
                    Check(errors.Count == 1 && errors[0].StartsWith(language[TextKey.CsvExportFailed]) && File.ReadAllText(destination) == "previous export", "Bengali write failure did not preserve previous file.");
                    language.Select("en");
                    Check(export.Content.ToString() == "Export CSV…", "English export label failed.");
                    using (var locked = new FileStream(destination, FileMode.Open, FileAccess.Read, FileShare.None)) Click(export);
                    Check(errors.Count == 2 && errors[1].StartsWith(language[TextKey.CsvExportFailed]), "English export failure did not report safely.");
                    language.Select("bn"); history.Width = history.MinWidth; Drain(history);
                    var panel = (FrameworkElement)history.Content;
                    var exportBounds = export.TransformToAncestor(panel).TransformBounds(new Rect(export.RenderSize));
                    var closeBounds = close.TransformToAncestor(panel).TransformBounds(new Rect(close.RenderSize));
                    Check(exportBounds.Width > 100 && exportBounds.Right <= closeBounds.Left && closeBounds.Right <= panel.ActualWidth + 1,
                        "Export/close controls overlap or clip at minimum width.");
                    var period = Field<ComboBox>(history, "_period");
                    var months = Field<ComboBox>(history, "_months");
                    var first = Field<TextBox>(history, "_fromDate"); var last = Field<TextBox>(history, "_toDate");
                    var status = Field<TextBlock>(history, "_filterStatus");
                    ledger.Record(new(1000, 500), now.AddDays(-12)); // September 30
                    ledger.Record(new(30, 15), now.AddDays(-1));
                    ledger.Record(new(40, 20), now.AddDays(1));
                    history.Refresh();
                    Select(period, TextKey.CsvMonth);
                    months.SelectedItem = months.Items.OfType<ComboBoxItem>().Single(item => (DateOnly)item.Tag == new DateOnly(2026, 9, 1));
                    Click(export);
                    Check(File.ReadAllLines(destination).SequenceEqual(new[] { UsageCsv.Header, "2026-09-30,1000,500,1500" }), "Month export included another month.");
                    Check(selectedUsage.Text.Contains("ডাউনলোড: 1000 B\nআপলোড: 500 B\nসর্বমোট: " + HistoryWindow.FormatBytes(1500)), "Month totals include other months.");
                    Render(history, "month");
                    Select(period, TextKey.CsvDateRange); first.Text = "2026-10-11"; last.Text = "2026-10-12";
                    Click(export);
                    Check(File.ReadAllLines(destination).SequenceEqual(new[] { UsageCsv.Header, "2026-10-11,30,15,45", "2026-10-12,120,60,180" }), "Range boundaries are not inclusive or included outside dates.");
                    Check(selectedUsage.Text.EndsWith("ডাউনলোড: 150 B\nআপলোড: 75 B\nসর্বমোট: 225 B"), "Selected totals do not match inclusive CSV rows.");
                    Render(history, "range");
                    string before = File.ReadAllText(destination); int choicesBefore = chooses;
                    foreach (string invalid in new[] { "bad", "2026-02-29", "2026-10-13", "" })
                    {
                        first.Text = invalid; Click(export);
                        Check(!export.IsEnabled && status.Text == language[TextKey.CsvInvalidDates] && chooses == choicesBefore && File.ReadAllText(destination) == before,
                            "Invalid dates allowed export or changed the destination.");
                        Check(selectedUsage.Visibility == Visibility.Collapsed && selectedUsage.Text == "" && AutomationProperties.GetName(selectedUsage) == "",
                            "Invalid dates left stale totals visible or accessible.");
                    }
                    Render(history, "invalid");
                    first.Text = "2020-01-01"; last.Text = "2020-12-31"; Click(export);
                    Check(!export.IsEnabled && status.Text == language[TextKey.CsvNoMatches] && chooses == choicesBefore && !history.ExportUsage(destination), "Empty period was not blocked.");
                    Check(selectedUsage.Visibility == Visibility.Visible && selectedUsage.Text.EndsWith("ডাউনলোড: 0 B\nআপলোড: 0 B\nসর্বমোট: 0 B"), "Empty period retained previous totals.");
                    first.Text = last.Text = "2026-10-12";
                    language.Select("en"); history.Refresh();
                    Check((TextKey)((ComboBoxItem)period.SelectedItem).Tag == TextKey.CsvDateRange && first.Text == "2026-10-12" && last.Text == first.Text && export.IsEnabled,
                        "Language/refresh discarded entered dates or period.");
                    addWhileChoosing = true; Click(export);
                    Check(File.ReadAllLines(destination).SequenceEqual(new[] { UsageCsv.Header, "2026-10-12,140,70,210" }) && ledger.Dirty, "Single-day filtered export missed latest traffic.");
                    Check(selectedUsage.Text == "Selected period usage\nDownload: 140 B\nUpload: 70 B\nTotal: 210 B", "English single-day live totals are incorrect.");
                    ledger.Record(new(10, 5), now); history.Refresh();
                    Check(selectedUsage.Text.EndsWith("Download: 150 B\nUpload: 75 B\nTotal: 225 B"), "Live refresh did not update selected totals.");
                    Select(period, TextKey.CsvMonth); now = now.AddMonths(1); history.Refresh();
                    Check((DateOnly)((ComboBoxItem)months.SelectedItem).Tag == new DateOnly(2026, 9, 1) && export.IsEnabled, "New month discarded selection.");
                    Select(period, TextKey.CsvAllDays); Click(export);
                    Check(File.ReadAllLines(destination).Length == 5, "All-days mode did not restore all recorded dates.");
                    Check(selectedUsage.Text.Contains("Download: " + HistoryWindow.FormatBytes(1220)) && selectedUsage.Text.EndsWith("Total: " + HistoryWindow.FormatBytes(1830)),
                        "All-days totals did not include every retained record.");
                    Select(period, TextKey.CsvDateRange); language.Select("bn"); history.MaxHeight = 360; Drain(history);
                    Check(((ScrollViewer)history.Content).ComputedVerticalScrollBarVisibility == Visibility.Visible && history.ActualHeight <= 361,
                        "Small screen cannot scroll to export controls.");
                    Check(!main.IsVisible && history.IsVisible && ledger.Today(new(2026, 10, 12)).Received == 150,
                        "CSV actions changed widget visibility or usage.");
                    Check(Directory.GetFiles(root, "*.tmp").Length == 0, "Export left temporary files.");
                }
                finally { history.Close(); }
                main.Close(); passed = true;
            }
            catch (Exception error) { Console.Error.WriteLine(error); app.Shutdown(1); }
        }));
        try
        {
            Check(app.Run() == 0 && passed, "CSV lifecycle failed.");
            Console.WriteLine("PASS: bilingual CSV controls and selected download/upload/combined totals, all/month/inclusive dates, invalid/empty range guard, live/language/month-rollover selection retention, latest unsaved totals, cancel/blocked-file rollback, scrolling and hidden widget preservation.");
            return 0;
        }
        finally
        {
            if (Path.GetFullPath(root).StartsWith(Path.GetFullPath(parent) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                Directory.Delete(root, true);
        }
    }
    private static void Click(Button button) => button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
    private static T Field<T>(HistoryWindow window, string name) => (T)typeof(HistoryWindow).GetField(name, Private)!.GetValue(window)!;
    private static void Select(ComboBox combo, TextKey key) => combo.SelectedItem = combo.Items.OfType<ComboBoxItem>().Single(item => (TextKey)item.Tag == key);
    private static void Render(HistoryWindow history, string name)
    {
        string? directory = Environment.GetEnvironmentVariable("TRAFFICSENSE_CSV_SCREENSHOTS");
        if (string.IsNullOrWhiteSpace(directory)) return;
        Drain(history);
        var content = (FrameworkElement)history.Content;
        var dpi = VisualTreeHelper.GetDpi(content);
        var bitmap = new RenderTargetBitmap((int)Math.Ceiling(content.ActualWidth * dpi.DpiScaleX),
            (int)Math.Ceiling(content.ActualHeight * dpi.DpiScaleY), dpi.PixelsPerInchX, dpi.PixelsPerInchY, PixelFormats.Pbgra32);
        var visual = new DrawingVisual();
        using (var drawing = visual.RenderOpen()) drawing.DrawRectangle(new VisualBrush(content), null, new Rect(content.RenderSize));
        bitmap.Render(visual);
        Directory.CreateDirectory(directory);
        var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var stream = File.Create(Path.Combine(directory, name + ".png")); encoder.Save(stream);
    }
    private static void Drain(Window window) { window.UpdateLayout(); window.Dispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle); }
    private static void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
}
