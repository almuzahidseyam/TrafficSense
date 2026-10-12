using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;

namespace TrafficSense;

internal sealed partial class HistoryWindow
{
    private readonly TextBlock _dailyChartTitle = new() { TextWrapping = TextWrapping.Wrap, Margin = new(0, 0, 0, 4) };
    private readonly TextBlock _dailyChartLegend = new() { TextWrapping = TextWrapping.Wrap, Margin = new(0, 0, 0, 6) };
    private readonly DailyUsageChart _dailyChart = new() { Height = 170, Margin = new(0, 0, 0, 12) };
    private readonly TextBlock _dailyLabel = new() { TextWrapping = TextWrapping.Wrap, Margin = new(0, 0, 0, 4) };
    private readonly ObservableCollection<UsageDayRow> _dailyRows = new();
    private readonly DataGrid _dailyUsage = new()
    {
        Style = null, Height = 200, Margin = new(0, 0, 0, 12), TabIndex = 4,
        AutoGenerateColumns = false, IsReadOnly = true, CanUserAddRows = false, CanUserDeleteRows = false,
        CanUserSortColumns = false, CanUserReorderColumns = false, CanUserResizeColumns = false, CanUserResizeRows = false,
        SelectionMode = DataGridSelectionMode.Single, SelectionUnit = DataGridSelectionUnit.FullRow,
        HeadersVisibility = DataGridHeadersVisibility.Column, RowHeaderWidth = 0, MinRowHeight = 28,
        EnableRowVirtualization = true, EnableColumnVirtualization = true,
        Background = SystemColors.WindowBrush, Foreground = SystemColors.WindowTextBrush,
        RowBackground = SystemColors.WindowBrush, AlternatingRowBackground = SystemColors.ControlBrush,
        BorderBrush = SystemColors.ActiveBorderBrush, GridLinesVisibility = DataGridGridLinesVisibility.Horizontal,
        HorizontalGridLinesBrush = SystemColors.ControlLightBrush,
        HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled, VerticalScrollBarVisibility = ScrollBarVisibility.Auto
    };

    private void InitializeDailyUsage(Panel panel)
    {
        var header = new Style(typeof(DataGridColumnHeader));
        header.Setters.Add(new Setter(Control.BackgroundProperty, SystemColors.ControlBrush));
        header.Setters.Add(new Setter(Control.ForegroundProperty, SystemColors.ControlTextBrush));
        header.Setters.Add(new Setter(Control.PaddingProperty, new Thickness(4)));
        var cell = new Style(typeof(DataGridCell));
        cell.Setters.Add(new Setter(ToolTipProperty, new Binding(nameof(UsageDayRow.Details))));
        var row = new Style(typeof(DataGridRow));
        row.Setters.Add(new Setter(AutomationProperties.NameProperty, new Binding(nameof(UsageDayRow.Details))));
        var text = new Style(typeof(TextBlock));
        text.Setters.Add(new Setter(TextBlock.TextWrappingProperty, TextWrapping.Wrap));
        text.Setters.Add(new Setter(FrameworkElement.MarginProperty, new Thickness(4, 2, 4, 2)));
        text.Setters.Add(new Setter(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center));
        _dailyUsage.FontSize = 12;
        _dailyUsage.ColumnHeaderStyle = header; _dailyUsage.CellStyle = cell; _dailyUsage.RowStyle = row;
        foreach (string property in new[] { nameof(UsageDayRow.Date), nameof(UsageDayRow.Download), nameof(UsageDayRow.Upload), nameof(UsageDayRow.Total) })
            _dailyUsage.Columns.Add(new DataGridTextColumn
            {
                Binding = new Binding(property), ElementStyle = text,
                Header = new TextBlock { Style = null, FontSize = 12, TextWrapping = TextWrapping.Wrap },
                Width = property == nameof(UsageDayRow.Date) ? new DataGridLength(86) : new DataGridLength(1, DataGridLengthUnitType.Star)
            });
        ScrollViewer.SetCanContentScroll(_dailyUsage, true);
        VirtualizingPanel.SetVirtualizationMode(_dailyUsage, VirtualizationMode.Recycling);
        _dailyUsage.ItemsSource = _dailyRows;
        panel.Children.Add(_dailyChartTitle); panel.Children.Add(_dailyChartLegend); panel.Children.Add(_dailyChart);
        panel.Children.Add(_dailyLabel); panel.Children.Add(_dailyUsage);
    }

    private void RefreshDailyUsage(bool valid, UsageDateRange? range)
    {
        _dailyLabel.Text = _language[TextKey.DailyUsage];
        AutomationProperties.SetName(_dailyUsage, _dailyLabel.Text);
        AutomationProperties.SetHelpText(_dailyUsage, _language[TextKey.DailyUsageHint]);
        var keys = new[] { TextKey.UsageDate, TextKey.Download, TextKey.Upload, TextKey.TotalUsage };
        for (int i = 0; i < keys.Length; i++)
            ((TextBlock)_dailyUsage.Columns[i].Header).Text = _language[keys[i]];
        var days = valid ? _usage.Days.Where(day => range == null || range.Value.Contains(day.Date)).Reverse().ToArray() : [];
        _dailyChart.Update(DailyUsageSeries.Create(days));
        _dailyChartTitle.Text = _language[TextKey.DailyChart];
        _dailyChartLegend.Text = _language[TextKey.DailyChartLegend];
        var series = _dailyChart.Series;
        string chartSummary = series.Days.Count == 0 ? "" : _dailyChartTitle.Text + "\n" +
            string.Format(CultureInfo.InvariantCulture, _language[TextKey.DailyChartRange], series.Start, series.End, series.Days.Count);
        AutomationProperties.SetName(_dailyChart, chartSummary);
        AutomationProperties.SetHelpText(_dailyChart, _dailyChartLegend.Text + "\n" + _language[TextKey.DailyChartHint]);
        _dailyChart.ConfigureDetails(_language, chartSummary + "\n" + _language[TextKey.DailyChartHint]);
        _dailyChartTitle.Visibility = _dailyChartLegend.Visibility = _dailyChart.Visibility = days.Length > 0 ? Visibility.Visible : Visibility.Collapsed;
        var dates = days.Select(day => day.Date).ToHashSet();
        // Keep existing rows for stable selection/scrolling and update only changed
        // counters. At most 400 records are retained, with virtualized visual rows.
        for (int i = _dailyRows.Count - 1; i >= 0; i--)
            if (!dates.Contains(_dailyRows[i].Day.Date)) _dailyRows.RemoveAt(i);
        for (int i = 0; i < days.Length; i++)
        {
            if (i == _dailyRows.Count || _dailyRows[i].Day.Date != days[i].Date)
                _dailyRows.Insert(i, new UsageDayRow(days[i], _language));
            else _dailyRows[i].Update(days[i], _language);
        }
        _dailyLabel.Visibility = _dailyUsage.Visibility = days.Length > 0 ? Visibility.Visible : Visibility.Collapsed;
        _dailyUsage.Height = Math.Clamp(32 + days.Length * 28, 100, 200);
    }
}

internal sealed class UsageDayRow : INotifyPropertyChanged
{
    public UsageDay Day { get; private set; }
    private string _languageCode, _culture;
    public string Date => Day.Date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
    public string Download => HistoryWindow.FormatBytes(Day.Received);
    public string Upload => HistoryWindow.FormatBytes(Day.Sent);
    public string Total => HistoryWindow.FormatBytes((decimal)Day.Received + Day.Sent);
    public string Details { get; private set; } = "";
    public event PropertyChangedEventHandler? PropertyChanged;

    internal UsageDayRow(UsageDay day, UiLanguage language)
    { Day = day; _languageCode = _culture = ""; Update(day, language); }

    internal void Update(UsageDay day, UiLanguage language)
    {
        if (Day == day && _languageCode == language.Code && _culture == CultureInfo.CurrentCulture.Name) return;
        Day = day; _languageCode = language.Code; _culture = CultureInfo.CurrentCulture.Name;
        string Exact(decimal value) => string.Format(language[TextKey.ExactBytes], value.ToString(CultureInfo.InvariantCulture));
        Details = Date + "\n" + language[TextKey.Download] + ": " + Exact(day.Received) + "\n" +
            language[TextKey.Upload] + ": " + Exact(day.Sent) + "\n" + language[TextKey.TotalUsage] + ": " + Exact((decimal)day.Received + day.Sent);
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(null));
    }
}
