using System.Globalization;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Threading;

namespace TrafficSense;

internal sealed partial class HistoryWindow
{
    private readonly TextBlock _periodLabel = new();
    private readonly ComboBox _period = new() { Margin = new(0, 4, 0, 8), TabIndex = 0 };
    private readonly ComboBox _months = new() { Margin = new(0, 0, 0, 8), TabIndex = 1 };
    private readonly Grid _customDates = new() { Margin = new(0, 0, 0, 8) };
    private readonly TextBlock _fromLabel = new(), _toLabel = new();
    private readonly TextBox _fromDate = new() { Margin = new(0, 4, 12, 0), TabIndex = 2 };
    private readonly TextBox _toDate = new() { Margin = new(0, 4, 0, 0), TabIndex = 3 };
    private readonly TextBlock _dateHint = new() { TextWrapping = TextWrapping.Wrap, Margin = new(0, 0, 0, 8) };
    private readonly TextBlock _filterStatus = new() { TextWrapping = TextWrapping.Wrap, Margin = new(0, 0, 0, 12) };
    private readonly TextBlock _selectedUsage = new() { TextWrapping = TextWrapping.Wrap, Margin = new(0, 0, 0, 12) };
    private bool _updatingFilter, _historyPlacementQueued;

    private void InitializeExportFilter(Panel panel)
    {
        foreach (Control control in new Control[] { _period, _months, _fromDate, _toDate })
        {
            // Use native control templates and system colors in this light/system
            // window rather than inheriting the widget's dark WPF-UI styles.
            control.Style = null; control.Background = SystemColors.WindowBrush;
            control.Foreground = SystemColors.WindowTextBrush; control.BorderBrush = SystemColors.ActiveBorderBrush;
        }
        foreach (var key in new[] { TextKey.CsvAllDays, TextKey.CsvMonth, TextKey.CsvDateRange })
            _period.Items.Add(new ComboBoxItem { Tag = key, Style = null });
        _period.SelectedIndex = 0;
        var today = DateOnly.FromDateTime(_localTime().Date);
        _fromDate.Text = UsageDateRange.ForMonth(today).Start.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        _toDate.Text = today.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        _customDates.ColumnDefinitions.Add(new()); _customDates.ColumnDefinitions.Add(new());
        var from = new StackPanel(); from.Children.Add(_fromLabel); from.Children.Add(_fromDate);
        var to = new StackPanel(); to.Children.Add(_toLabel); to.Children.Add(_toDate);
        Grid.SetColumn(to, 1); _customDates.Children.Add(from); _customDates.Children.Add(to);
        panel.Children.Add(_periodLabel); panel.Children.Add(_period); panel.Children.Add(_months);
        panel.Children.Add(_customDates); panel.Children.Add(_dateHint); panel.Children.Add(_filterStatus);
        panel.Children.Add(_selectedUsage);
        _period.SelectionChanged += (_, _) => UpdateExportFilter();
        _months.SelectionChanged += (_, _) => UpdateExportFilter();
        _fromDate.TextChanged += (_, _) => UpdateExportFilter();
        _toDate.TextChanged += (_, _) => UpdateExportFilter();
    }

    private TextKey ExportPeriod => (_period.SelectedItem as ComboBoxItem)?.Tag is TextKey key ? key : TextKey.CsvAllDays;

    private void RefreshExportFilter(DateOnly today)
    {
        _updatingFilter = true;
        try
        {
            _periodLabel.Text = _language[TextKey.CsvPeriod];
            foreach (ComboBoxItem item in _period.Items) item.Content = _language[(TextKey)item.Tag];
            _fromLabel.Text = _language[TextKey.CsvFrom]; _toLabel.Text = _language[TextKey.CsvTo];
            _dateHint.Text = _language[TextKey.CsvDatesHint];
            AutomationProperties.SetName(_period, _periodLabel.Text);
            AutomationProperties.SetName(_months, _language[TextKey.CsvMonth]);
            AutomationProperties.SetName(_fromDate, _fromLabel.Text); AutomationProperties.SetName(_toDate, _toLabel.Text);
            AutomationProperties.SetHelpText(_fromDate, _dateHint.Text); AutomationProperties.SetHelpText(_toDate, _dateHint.Text);
            DateOnly current = UsageDateRange.ForMonth(today).Start;
            DateOnly selected = (_months.SelectedItem as ComboBoxItem)?.Tag is DateOnly month ? month : current;
            var months = _usage.Days.Select(day => UsageDateRange.ForMonth(day.Date).Start)
                .Append(current).Append(selected).Distinct().OrderDescending().ToArray();
            if (!_months.Items.OfType<ComboBoxItem>().Select(item => (DateOnly)item.Tag).SequenceEqual(months))
            {
                _months.Items.Clear();
                foreach (var value in months) _months.Items.Add(new ComboBoxItem { Tag = value, Style = null });
                _months.SelectedItem = _months.Items.OfType<ComboBoxItem>().Single(item => (DateOnly)item.Tag == selected);
            }
            var culture = CultureInfo.GetCultureInfo(_language.Code == "bn" ? "bn-BD" : "en-US");
            foreach (ComboBoxItem item in _months.Items) item.Content = ((DateOnly)item.Tag).ToString("MMMM yyyy", culture);
        }
        finally { _updatingFilter = false; }
        UpdateExportFilter();
    }

    private bool TryGetExportRange(out UsageDateRange? range)
    {
        range = null;
        if (ExportPeriod == TextKey.CsvAllDays) return true;
        if (ExportPeriod == TextKey.CsvMonth && (_months.SelectedItem as ComboBoxItem)?.Tag is DateOnly month)
        { range = UsageDateRange.ForMonth(month); return true; }
        if (ExportPeriod == TextKey.CsvDateRange && UsageDateRange.TryParse(_fromDate.Text, _toDate.Text, out var dates))
        { range = dates; return true; }
        return false;
    }

    private void UpdateExportFilter()
    {
        if (_updatingFilter) return;
        _months.Visibility = ExportPeriod == TextKey.CsvMonth ? Visibility.Visible : Visibility.Collapsed;
        _customDates.Visibility = _dateHint.Visibility = ExportPeriod == TextKey.CsvDateRange ? Visibility.Visible : Visibility.Collapsed;
        bool valid = TryGetExportRange(out var range);
        var totals = valid ? _usage.GetTotals(range) : default;
        int count = totals.RecordedDays;
        _export.IsEnabled = valid && count > 0;
        _export.Foreground = _export.IsEnabled ? SystemColors.ControlTextBrush : SystemColors.GrayTextBrush;
        TextKey message = !valid ? TextKey.CsvInvalidDates : _usage.Days.Count == 0 ? TextKey.CsvExportEmpty : TextKey.CsvNoMatches;
        _filterStatus.Text = count > 0 ? string.Format(_language[TextKey.CsvSelectedDays], count) : _language[message];
        _export.ToolTip = count > 0 ? _language[TextKey.CsvExportHint] : _filterStatus.Text;
        AutomationProperties.SetHelpText(_export, _export.ToolTip.ToString());
        AutomationProperties.SetName(_filterStatus, _filterStatus.Text);
        _selectedUsage.Visibility = valid ? Visibility.Visible : Visibility.Collapsed;
        _selectedUsage.Text = valid ? _language[TextKey.SelectedUsage] + "\n" +
            _language[TextKey.Download] + ": " + FormatBytes(totals.Received) + "\n" +
            _language[TextKey.Upload] + ": " + FormatBytes(totals.Sent) + "\n" +
            _language[TextKey.TotalUsage] + ": " + FormatBytes(totals.Total) : "";
        AutomationProperties.SetName(_selectedUsage, _selectedUsage.Text);
        RefreshDailyUsage(valid, range);
    }

    private void QueueHistoryPlacement()
    {
        if (_historyPlacementQueued) return;
        _historyPlacementQueued = true;
        Dispatcher.BeginInvoke(DispatcherPriority.ContextIdle, new Action(() =>
        {
            _historyPlacementQueued = false;
            if (IsVisible) NativeWindowPlacement.EnsureVisible(this);
        }));
    }
}
