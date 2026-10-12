using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Threading;
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
                    Check(!main.IsVisible && history.IsVisible && ledger.Today(new(2026, 10, 12)).Received == 120,
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
            Console.WriteLine("PASS: bilingual CSV button/help, empty-to-active state, cancel, latest unsaved totals, blocked-file rollback, minimum-width layout and hidden widget preservation.");
            return 0;
        }
        finally
        {
            if (Path.GetFullPath(root).StartsWith(Path.GetFullPath(parent) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                Directory.Delete(root, true);
        }
    }
    private static void Click(Button button) => button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
    private static void Drain(Window window) { window.UpdateLayout(); window.Dispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle); }
    private static void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
}
