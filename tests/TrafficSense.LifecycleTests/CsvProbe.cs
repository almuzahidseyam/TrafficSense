using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
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
                    var daily = Field<DataGrid>(history, "_dailyUsage");
                    var chart = Field<DailyUsageChart>(history, "_dailyChart");
                    Check(chart.Series.Days.Count == 0 && chart.Visibility == Visibility.Collapsed, "Empty chart retained visible records.");
                    Check(daily.IsReadOnly && !daily.CanUserAddRows && daily.Items.Count == 0 && daily.Visibility == Visibility.Collapsed, "Empty daily list is editable or has stale rows.");
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
                    var dayRow = (UsageDayRow)daily.Items[0];
                    Check(chart.Series.Days.SequenceEqual(new[] { dayRow.Day }) && chart.Visibility == Visibility.Visible &&
                        AutomationProperties.GetName(chart).Contains("দিনভিত্তিক") && AutomationProperties.GetName(chart).Contains("2026-10-12"),
                        "Bengali chart missed latest traffic, date or accessible description.");
                    var peer = UIElementAutomationPeer.CreatePeerForElement(chart)!;
                    Check(peer.GetAutomationControlType() == AutomationControlType.Image && peer.GetName() == AutomationProperties.GetName(chart) &&
                        peer.GetHelpText().Contains("রেকর্ড"), "Chart has no accessible image peer/name/help.");
                    Check(daily.Items.Count == 1 && dayRow.Date == "2026-10-12" && dayRow.Download == "120 B" && dayRow.Upload == "60 B" && dayRow.Total == "180 B" &&
                        dayRow.Details.EndsWith("সর্বমোট: 180 বাইট") && ((TextBlock)daily.Columns[0].Header).Text == "তারিখ" &&
                        AutomationProperties.GetName(daily).Contains("দিনভিত্তিক"), "Bengali daily list/header/exact-byte accessibility failed.");
                    CheckDayDetails(chart, dayRow);
                    var detailTip = (ToolTip)chart.ToolTip;
                    detailTip.Opacity = 0; detailTip.IsOpen = true; Drain(history);
                    Check(detailTip.IsOpen && Details(chart).EndsWith("সর্বমোট: 180 বাইট"), "Native chart tooltip did not open with Bengali exact bytes.");
                    RenderDetails(chart, "tooltip-bn");
                    File.WriteAllText(destination, "previous export");
                    using (var locked = new FileStream(destination, FileMode.Open, FileAccess.Read, FileShare.None)) Click(export);
                    Check(errors.Count == 1 && errors[0].StartsWith(language[TextKey.CsvExportFailed]) && File.ReadAllText(destination) == "previous export", "Bengali write failure did not preserve previous file.");
                    language.Select("en");
                    Check(Details(chart) == "2026-10-12\nDownload: 120 bytes\nUpload: 60 bytes\nTotal: 180 bytes",
                        "Open chart tooltip did not translate without moving the pointer.");
                    RenderDetails(chart, "tooltip-en");
                    detailTip.IsOpen = false;
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
                    Check(daily.Items.Count == 1 && ((UsageDayRow)daily.Items[0]).Date == "2026-09-30", "Daily list did not filter month.");
                    Check(chart.Series.Days.Count == 1 && chart.Series.Start == new DateOnly(2026, 9, 30) && chart.Series.PeakBytes == 1000,
                        "Month chart included other dates or used the wrong scale.");
                    CheckDayDetails(chart, (UsageDayRow)daily.Items[0]);
                    Render(history, "month");
                    Select(period, TextKey.CsvDateRange); first.Text = "2026-10-11"; last.Text = "2026-10-12";
                    Click(export);
                    Check(File.ReadAllLines(destination).SequenceEqual(new[] { UsageCsv.Header, "2026-10-11,30,15,45", "2026-10-12,120,60,180" }), "Range boundaries are not inclusive or included outside dates.");
                    Check(selectedUsage.Text.EndsWith("ডাউনলোড: 150 B\nআপলোড: 75 B\nসর্বমোট: 225 B"), "Selected totals do not match inclusive CSV rows.");
                    Check(daily.Items.Cast<UsageDayRow>().Select(row => row.Date).SequenceEqual(new[] { "2026-10-12", "2026-10-11" }) &&
                        daily.Items.Cast<UsageDayRow>().Sum(row => (decimal)row.Day.Received + row.Day.Sent) == 225, "Daily list disagrees with inclusive CSV or newest-first ordering.");
                    Render(history, "range");
                    Check(chart.Series.Days.Select(day => day.Date).SequenceEqual(new[] { new DateOnly(2026, 10, 11), new DateOnly(2026, 10, 12) }) &&
                        chart.Series.Days.Sum(day => (decimal)day.Received + day.Sent) == 225, "Range chart disagrees with daily rows/CSV or chronological order.");
                    CheckDayDetails(chart, (UsageDayRow)daily.Items[1]);
                    CheckDayDetails(chart, (UsageDayRow)daily.Items[0]);
                    SelectChartDay(history, new(2026, 10, 11));
                    Check(((UsageDayRow)daily.SelectedItem).Date == "2026-10-11" && ledger.Dirty,
                        "Chronological chart selected the newest-first table by index or marked usage saved.");
                    detailTip.IsOpen = true;
                    string before = File.ReadAllText(destination); int choicesBefore = chooses;
                    foreach (string invalid in new[] { "bad", "2026-02-29", "2026-10-13", "" })
                    {
                        first.Text = invalid; Click(export);
                        Check(!export.IsEnabled && status.Text == language[TextKey.CsvInvalidDates] && chooses == choicesBefore && File.ReadAllText(destination) == before,
                            "Invalid dates allowed export or changed the destination.");
                        Check(selectedUsage.Visibility == Visibility.Collapsed && selectedUsage.Text == "" && AutomationProperties.GetName(selectedUsage) == "",
                            "Invalid dates left stale totals visible or accessible.");
                        Check(daily.Items.Count == 0 && daily.Visibility == Visibility.Collapsed, "Invalid dates left stale daily rows.");
                        Check(daily.SelectedItem == null && !chart.SelectDayAt(new(20, 40), System.Windows.Input.MouseButton.Left),
                            "Invalid period retained a selection or accepted chart input.");
                        Check(chart.Series.Days.Count == 0 && chart.Visibility == Visibility.Collapsed && AutomationProperties.GetName(chart) == "",
                            "Invalid dates left stale chart records or accessible values.");
                        Check(Details(chart) == "" && !detailTip.IsOpen, "Invalid period retained tooltip content or popup.");
                    }
                    Render(history, "invalid");
                    first.Text = "2020-01-01"; last.Text = "2020-12-31"; Click(export);
                    Check(!export.IsEnabled && status.Text == language[TextKey.CsvNoMatches] && chooses == choicesBefore && !history.ExportUsage(destination), "Empty period was not blocked.");
                    Check(selectedUsage.Visibility == Visibility.Visible && selectedUsage.Text.EndsWith("ডাউনলোড: 0 B\nআপলোড: 0 B\nসর্বমোট: 0 B"), "Empty period retained previous totals.");
                    Check(daily.Items.Count == 0 && daily.Visibility == Visibility.Collapsed, "Empty period retained previous daily rows.");
                    Check(chart.Series.Days.Count == 0 && chart.Visibility == Visibility.Collapsed, "Empty period retained previous chart.");
                    Check(Details(chart) == "", "Empty period retained tooltip details.");
                    first.Text = last.Text = "2026-10-12";
                    language.Select("en"); history.Refresh();
                    Check((TextKey)((ComboBoxItem)period.SelectedItem).Tag == TextKey.CsvDateRange && first.Text == "2026-10-12" && last.Text == first.Text && export.IsEnabled,
                        "Language/refresh discarded entered dates or period.");
                    addWhileChoosing = true; Click(export);
                    Check(File.ReadAllLines(destination).SequenceEqual(new[] { UsageCsv.Header, "2026-10-12,140,70,210" }) && ledger.Dirty, "Single-day filtered export missed latest traffic.");
                    Check(selectedUsage.Text == "Selected period usage\nDownload: 140 B\nUpload: 70 B\nTotal: 210 B", "English single-day live totals are incorrect.");
                    Check(chart.Series.Days.Single().Received == 140 && peer.GetName().StartsWith("Daily usage chart") && peer.GetHelpText().Contains("Missing records"),
                        "English chart translation/live sample failed.");
                    var retainedRow = (UsageDayRow)daily.Items[0]; SelectChartDay(history, retainedRow.Day.Date);
                    CheckDayDetails(chart, retainedRow);
                    ledger.Record(new(10, 5), now); history.Refresh();
                    Check(selectedUsage.Text.EndsWith("Download: 150 B\nUpload: 75 B\nTotal: 225 B"), "Live refresh did not update selected totals.");
                    Check(chart.Series.Days.Single().Received == 150 && chart.Series.Days.Single().Sent == 75, "Live chart values stayed stale.");
                    Check(Details(chart) == retainedRow.Details && Details(chart).EndsWith("Total: 225 bytes"), "Stationary hover did not update with new traffic.");
                    var unchangedSeries = chart.Series; history.Refresh();
                    Check(ReferenceEquals(chart.Series, unchangedSeries), "Unchanged refresh replaced the chart snapshot.");
                    Check(ReferenceEquals(daily.SelectedItem, retainedRow) && ReferenceEquals(daily.Items[0], retainedRow) && retainedRow.Download == "150 B" &&
                        retainedRow.Details.EndsWith("Total: 225 bytes") && ((TextBlock)daily.Columns[0].Header).Text == "Date", "Live daily refresh replaced selection or missed English amounts/headers.");
                    Select(period, TextKey.CsvMonth); now = now.AddMonths(1); history.Refresh();
                    Check((DateOnly)((ComboBoxItem)months.SelectedItem).Tag == new DateOnly(2026, 9, 1) && export.IsEnabled, "New month discarded selection.");
                    Select(period, TextKey.CsvAllDays); Click(export);
                    Check(File.ReadAllLines(destination).Length == 5, "All-days mode did not restore all recorded dates.");
                    Check(daily.Items.Cast<UsageDayRow>().Select(row => row.Date).SequenceEqual(new[] { "2026-10-13", "2026-10-12", "2026-10-11", "2026-09-30" }), "All-days list missed or duplicated records.");
                    Check(chart.Series.Days.Count == 4 && chart.Series.CalendarDays == 14 && chart.Series.Days.Select(day => day.Date).SequenceEqual(
                        daily.Items.Cast<UsageDayRow>().Select(row => row.Day.Date).Reverse()), "All-days chart invented missing records or disagrees with the table.");
                    Hover(chart, new(2026, 10, 1));
                    Check(Details(chart) == "2026-10-01\nNo stored usage for this day.", "Missing-day hover invented zero usage or selected a neighboring record.");
                    SelectChartDay(history, new(2026, 9, 30));
                    var beforeIgnoredClick = daily.SelectedItem;
                    Check(!chart.SelectDayAt(DayPosition(chart, new(2026, 10, 1)), System.Windows.Input.MouseButton.Left) &&
                        !chart.SelectDayAt(DayPosition(chart, new(2026, 10, 12)), System.Windows.Input.MouseButton.Right) &&
                        !chart.SelectDayAt(new(3, 40), System.Windows.Input.MouseButton.Left) &&
                        !chart.SelectDayAt(new(chart.ActualWidth - 4, 40), System.Windows.Input.MouseButton.Left) &&
                        !chart.SelectDayAt(new(20, 24), System.Windows.Input.MouseButton.Left) &&
                        ReferenceEquals(daily.SelectedItem, beforeIgnoredClick), "Gap, outside-plot or right click changed daily selection.");
                    chart.SetHoverPosition(new(3, 40));
                    Check(Details(chart).StartsWith("Daily usage chart") && !Details(chart).Contains("Total: 225 bytes"), "Outside-plot hover retained stale day values.");
                    Hover(chart, new(2026, 10, 12));
                    chart.RaiseEvent(new System.Windows.Input.MouseEventArgs(System.Windows.Input.Mouse.PrimaryDevice, 0)
                        { RoutedEvent = System.Windows.Input.Mouse.MouseLeaveEvent });
                    Check(!detailTip.IsOpen && Details(chart).StartsWith("Daily usage chart"), "Leaving chart retained daily details or popup.");
                    Render(history, "gaps");
                    Check(selectedUsage.Text.Contains("Download: " + HistoryWindow.FormatBytes(1220)) && selectedUsage.Text.EndsWith("Total: " + HistoryWindow.FormatBytes(1830)),
                        "All-days totals did not include every retained record.");
                    Select(period, TextKey.CsvDateRange); language.Select("bn"); history.MaxHeight = 360; Drain(history);
                    Check(((ScrollViewer)history.Content).ComputedVerticalScrollBarVisibility == Visibility.Visible && history.ActualHeight <= 361,
                        "Small screen cannot scroll to export controls.");
                    Check(!main.IsVisible && history.IsVisible && ledger.Today(new(2026, 10, 12)).Received == 150,
                        "CSV actions changed widget visibility or usage.");
                    Check(Directory.GetFiles(root, "*.tmp").Length == 0, "Export left temporary files.");
                    detailTip.IsOpen = true;
                }
                finally
                {
                    history.Close();
                    var closedGrid = Field<DataGrid>(history, "_dailyUsage");
                    Check(closedGrid.ItemsSource == null && closedGrid.Columns.Count == 0, "Closing history retained daily view/cell bindings.");
                    Check(Field<DailyUsageChart>(history, "_dailyChart").Series.Days.Count == 0, "Closed history retained chart records.");
                    Check(Details(Field<DailyUsageChart>(history, "_dailyChart")) == "", "Closed history retained tooltip details.");
                    Check(!((ToolTip)Field<DailyUsageChart>(history, "_dailyChart").ToolTip).IsOpen, "Closed history retained its tooltip popup.");
                }
                VerifyLargeDailyList(language, now);
                VerifyZeroChart(language, now);
                main.Close(); passed = true;
            }
            catch (Exception error) { Console.Error.WriteLine(error); app.Shutdown(1); }
        }));
        try
        {
            Check(app.Run() == 0 && passed, "CSV lifecycle failed.");
            Console.WriteLine("PASS: bilingual CSV controls, totals/daily rows and accessible 30-calendar-day chart, exact-byte hover/native-popup translation/live update/cleanup, chart click selects/reveals the matching daily row, inclusive filtering/ordering/gaps, stable live updates/selection/scroll, 400-row virtualization/retention, invalid/empty guards, latest unsaved traffic, cancel/blocked-file rollback, scrolling and hidden widget preservation.");
            return 0;
        }
        finally
        {
            if (Path.GetFullPath(root).StartsWith(Path.GetFullPath(parent) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                Directory.Delete(root, true);
        }
    }
    private static void VerifyLargeDailyList(UiLanguage language, DateTimeOffset now)
    {
        var today = DateOnly.FromDateTime(now.Date);
        var ledger = new UsageLedger(Enumerable.Range(0, 400).Select(i => new UsageDay(today.AddDays(-i), long.MaxValue, long.MaxValue)));
        // Keep the outer scrollbar present across translations/shorter totals;
        // the rollover assertion below tests a stationary point in a stable plot.
        var window = new HistoryWindow(language, ledger, () => now, new(), () => false) { MaxHeight = 360 };
        try
        {
            window.Show(); Drain(window);
            var grid = Field<DataGrid>(window, "_dailyUsage");
            var chart = Field<DailyUsageChart>(window, "_dailyChart");
            Check(chart.Series.Days.Count == 30 && chart.Series.CalendarDays == 30 && chart.Series.Start == today.AddDays(-29) &&
                chart.Series.End == today && chart.Series.PeakBytes == long.MaxValue, "Large chart exceeded 30 calendar days or overflowed its scale.");
            Hover(chart, today);
            Check(Details(chart).EndsWith("সর্বমোট: 18446744073709551614 বাইট"), "Maximum hover totals overflowed.");
            var outer = (ScrollViewer)window.Content;
            outer.ScrollToBottom(); Drain(window);
            var first = (UsageDayRow)grid.Items[0];
            Check(grid.Items.Count == 400 && first.Date == today.ToString("yyyy-MM-dd") && first.Details.EndsWith("সর্বমোট: 18446744073709551614 বাইট"),
                "Maximum daily counters overflowed or large list omitted rows.");
            Check(Descendants(grid).OfType<DataGridRow>().Count() < 50, "Daily grid created hundreds of visual rows instead of virtualizing.");
            var selected = grid.Items[200]; grid.SelectedItem = selected; grid.ScrollIntoView(selected); Drain(window);
            var scroll = Descendants(grid).OfType<ScrollViewer>().First();
            double offset = scroll.VerticalOffset;
            var initialSeries = chart.Series;
            window.Refresh(); Drain(window);
            Check(ReferenceEquals(chart.Series, initialSeries), "Unchanged large chart snapshot was replaced.");
            Check(ReferenceEquals(grid.SelectedItem, selected) && Math.Abs(scroll.VerticalOffset - offset) < 0.01, "Unchanged refresh lost daily selection or scroll.");
            language.Select("en"); Drain(window);
            Check(Details(chart).EndsWith("Total: 18446744073709551614 bytes"), "Large stationary hover did not translate.");
            Check(ReferenceEquals(chart.Series, initialSeries) && AutomationProperties.GetName(chart).StartsWith("Daily usage chart"), "Chart translation changed its records.");
            Check(ReferenceEquals(grid.SelectedItem, selected) && first.Details.EndsWith("Total: 18446744073709551614 bytes"), "Translation replaced daily rows or missed exact bytes.");
            Hover(chart, today);
            double rolloverWidth = chart.ActualWidth;
            ledger.Record(new(1, 2), now.AddDays(1)); now = now.AddDays(1); window.Refresh(); Drain(window);
            Check(chart.Series.Days.Count == 30 && chart.Series.End == today.AddDays(1) && chart.Series.Start == today.AddDays(-28) &&
                chart.Series.Days[^1].Received == 1 && chart.Series.Days[^1].Sent == 2, "Chart rollover retained expired days or missed new samples.");
            Check(Math.Abs(chart.ActualWidth - rolloverWidth) < .01 &&
                Details(chart) == today.AddDays(1).ToString("yyyy-MM-dd") + "\nDownload: 1 bytes\nUpload: 2 bytes\nTotal: 3 bytes",
                "Stationary hover did not follow the calendar slot on rollover: " + Details(chart));
            Check(grid.Items.Count == 400 && ((UsageDayRow)grid.Items[0]).Date == DateOnly.FromDateTime(now.Date).ToString("yyyy-MM-dd") &&
                grid.Items.Cast<UsageDayRow>().Last().Day.Date == today.AddDays(-398) && ReferenceEquals(grid.SelectedItem, selected),
                "Daily rollover/retention changed selection, duplicated rows or kept an expired record.");
            window.Width = window.MinWidth; language.Select("bn"); Drain(window);
            Hover(chart, today);
            RenderDetails(chart, "tooltip-large-bn");
            outer.ScrollToBottom(); Drain(window);
            Check(scroll.ExtentWidth <= scroll.ViewportWidth + 1, "Minimum-width daily columns overflow horizontally.");
            Render(window, "daily-large");
            window.MaxHeight = 360; Drain(window); outer.ScrollToBottom(); Drain(window);
            Check(outer.ComputedVerticalScrollBarVisibility == Visibility.Visible && window.ActualHeight <= 361, "Daily list made small-screen controls unreachable.");
            Check(ledger.Dirty, "Viewing daily rows marked pending usage saved.");
            outer.ScrollToTop(); Drain(window);
            SelectChartDay(window, chart.Series.Start);
            Check(grid.SelectedIndex == 29 && scroll.VerticalOffset > 0 && outer.VerticalOffset > 0,
                "Chart click did not scroll both virtualized daily rows and the small history viewport.");
            window.Refresh(); language.Select("en"); Drain(window);
            Check(((UsageDayRow)grid.SelectedItem).Day.Date == chart.Series.Start, "Refresh/translation lost chart selection.");
            chart.SelectDayAt(DayPosition(chart, chart.Series.Start), System.Windows.Input.MouseButton.Left);
            SelectChartDay(window, chart.Series.End);
            Check(grid.SelectedIndex == 0, "Queued earlier click overrode the latest selection.");
            Render(window, "chart-selection-en");
        }
        finally { window.Close(); }
    }
    private static void VerifyZeroChart(UiLanguage language, DateTimeOffset now)
    {
        language.Select("bn");
        var today = DateOnly.FromDateTime(now.Date);
        var ledger = new UsageLedger([new(today.AddDays(-2), 0, 0), new(today, 0, 0)]);
        var window = new HistoryWindow(language, ledger, () => now, new(), () => false);
        try
        {
            window.Width = window.MinWidth; window.Show(); Drain(window);
            var chart = Field<DailyUsageChart>(window, "_dailyChart");
            Check(chart.Series.PeakBytes == 0 && chart.Series.Days.Count == 2 && chart.Series.CalendarDays == 3 &&
                chart.Visibility == Visibility.Visible && chart.ActualWidth >= 100, "Zero chart collapsed, invented a missing day or lost its scale.");
            Hover(chart, today.AddDays(-2));
            Check(Details(chart).EndsWith("ডাউনলোড: 0 বাইট\nআপলোড: 0 বাইট\nসর্বমোট: 0 বাইট"), "Recorded zero was confused with a missing day.");
            Hover(chart, today.AddDays(-1));
            Check(Details(chart).EndsWith("এই দিনের সংরক্ষিত হিসাব নেই।"), "Bengali gap tooltip is missing.");
            SelectChartDay(window, today.AddDays(-2));
            Check(!chart.SelectDayAt(DayPosition(chart, today.AddDays(-1)), System.Windows.Input.MouseButton.Left), "Zero-chart gap accepted a click.");
            language.Select("bn"); Drain(window);
            Check(((UsageDayRow)Field<DataGrid>(window, "_dailyUsage").SelectedItem).Day.Date == today.AddDays(-2),
                "Zero-record selection did not survive language change.");
            Render(window, "zero-gaps");
            Check(!ledger.Dirty, "Viewing zero records changed stored usage.");
        }
        finally { window.Close(); }
    }

    private static void RenderDetails(DailyUsageChart chart, string name)
    {
        string? directory = Environment.GetEnvironmentVariable("TRAFFICSENSE_CSV_SCREENSHOTS");
        if (string.IsNullOrWhiteSpace(directory)) return;
        var tip = (ToolTip)chart.ToolTip;
        bool wasOpen = tip.IsOpen;
        tip.Opacity = 0; tip.IsOpen = true; Drain(Window.GetWindow(chart));
        var text = (TextBlock)tip.Content;
        Check(text.ActualWidth > 0 && text.ActualWidth <= 300 && text.ActualHeight > 0, "Tooltip is empty or exceeds its wrapping width.");
        var dpi = VisualTreeHelper.GetDpi(text);
        var size = new Size(text.ActualWidth + 16, text.ActualHeight + 16);
        var bitmap = new RenderTargetBitmap((int)Math.Ceiling(size.Width * dpi.DpiScaleX), (int)Math.Ceiling(size.Height * dpi.DpiScaleY),
            dpi.PixelsPerInchX, dpi.PixelsPerInchY, PixelFormats.Pbgra32);
        var visual = new DrawingVisual();
        using (var drawing = visual.RenderOpen())
        {
            drawing.DrawRectangle(tip.Background, new Pen(SystemColors.ActiveBorderBrush, 1), new Rect(size));
            drawing.DrawRectangle(new VisualBrush(text), null, new Rect(new Point(8, 8), text.RenderSize));
        }
        bitmap.Render(visual); Directory.CreateDirectory(directory);
        var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var stream = File.Create(Path.Combine(directory, name + ".png")); encoder.Save(stream);
        tip.IsOpen = wasOpen;
    }
    private static string Details(DailyUsageChart chart) => ((TextBlock)((ToolTip)chart.ToolTip).Content).Text;
    private static Point DayPosition(DailyUsageChart chart, DateOnly date) =>
        new(4 + (date.DayNumber - chart.Series.Start.DayNumber + .5) * (chart.ActualWidth - 8) / chart.Series.CalendarDays, 26);
    private static void SelectChartDay(HistoryWindow window, DateOnly date)
    {
        Drain(window);
        var chart = Field<DailyUsageChart>(window, "_dailyChart");
        var grid = Field<DataGrid>(window, "_dailyUsage");
        var point = DayPosition(chart, date);
        Check(VisualTreeHelper.HitTest(chart, point)?.VisualHit == chart, "Chart day cannot receive a click.");
        Check(chart.SelectDayAt(point, System.Windows.Input.MouseButton.Left), "Recorded chart day refused a click.");
        Drain(window);
        Check(grid.SelectedItem is UsageDayRow row && row.Day.Date == date && grid.CurrentCell.Item == row,
            "Chart click selected the wrong daily row/current cell.");
        Check(!((ToolTip)chart.ToolTip).IsOpen, "Chart click left its popup covering the selected row.");
        var visual = (DataGridRow?)grid.ItemContainerGenerator.ContainerFromItem(grid.SelectedItem);
        Check(visual != null, "Chart click did not realize its virtualized row.");
        var outer = (ScrollViewer)window.Content;
        var bounds = visual!.TransformToAncestor(outer).TransformBounds(new Rect(visual.RenderSize));
        Check(bounds.Top >= -1 && bounds.Bottom <= outer.ViewportHeight + 1,
            "Selected chart row was not brought into the outer viewport: " + bounds);
    }
    private static void Hover(DailyUsageChart chart, DateOnly date)
    {
        Drain(Window.GetWindow(chart));
        var position = DayPosition(chart, date);
        Check(VisualTreeHelper.HitTest(chart, position)?.VisualHit == chart, "Day columns above small bars do not receive mouse input.");
        chart.SetHoverPosition(position);
    }
    private static void CheckDayDetails(DailyUsageChart chart, UsageDayRow row)
    {
        Hover(chart, row.Day.Date);
        Check(Details(chart) == row.Details, "Hover disagrees with the exact daily row/date.");
        var tip = (ToolTip)chart.ToolTip;
        Check(tip.Style == null && tip.Background == SystemColors.InfoBrush && tip.Foreground == SystemColors.InfoTextBrush &&
            ((TextBlock)tip.Content).TextWrapping == TextWrapping.Wrap, "Tooltip does not use readable native system colors/wrapping.");
    }

    private static IEnumerable<DependencyObject> Descendants(DependencyObject root)
    {
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var child = VisualTreeHelper.GetChild(root, i); yield return child;
            foreach (var descendant in Descendants(child)) yield return descendant;
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
        var chart = Field<DailyUsageChart>(history, "_dailyChart");
        if (chart.Visibility != Visibility.Visible) return;
        var chartImage = new RenderTargetBitmap((int)Math.Ceiling(chart.ActualWidth * dpi.DpiScaleX),
            (int)Math.Ceiling(chart.ActualHeight * dpi.DpiScaleY), dpi.PixelsPerInchX, dpi.PixelsPerInchY, PixelFormats.Pbgra32);
        var chartVisual = new DrawingVisual();
        using (var drawing = chartVisual.RenderOpen())
        {
            drawing.DrawRectangle(SystemColors.WindowBrush, null, new Rect(chart.RenderSize));
            drawing.DrawRectangle(new VisualBrush(chart), null, new Rect(chart.RenderSize));
        }
        chartImage.Render(chartVisual);
        var chartEncoder = new PngBitmapEncoder(); chartEncoder.Frames.Add(BitmapFrame.Create(chartImage));
        using var chartStream = File.Create(Path.Combine(directory, name + "-chart.png")); chartEncoder.Save(chartStream);
    }
    private static void Drain(Window window) { window.UpdateLayout(); window.Dispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle); }
    private static void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
}
