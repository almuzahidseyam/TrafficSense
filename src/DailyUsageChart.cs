using System.Globalization;
using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace TrafficSense;

internal sealed class DailyUsageChart : FrameworkElement
{
    private readonly TextBlock _detailText = new()
    {
        Style = null, FontSize = 14, FontFamily = new FontFamily("Segoe UI, Nirmala UI, Kalpurush"),
        TextWrapping = TextWrapping.Wrap, MaxWidth = 300, Foreground = SystemColors.InfoTextBrush
    };
    private readonly ToolTip _details = new()
    {
        Style = null, Background = SystemColors.InfoBrush, Foreground = SystemColors.InfoTextBrush, Padding = new Thickness(8)
    };
    private UiLanguage _language = new();
    private string _summary = "";
    private Point? _hoverPosition;
    private UsageDayRow? _hoverRow;
    internal event Action<DateOnly>? DaySelected;

    internal DailyUsageChart()
    {
        _details.Content = _detailText; ToolTip = _details;
        ToolTipService.SetShowDuration(this, 30000);
        Unloaded += (_, _) => SetHoverPosition(null);
    }

    internal void ConfigureDetails(UiLanguage language, string summary)
    { _language = language; _summary = summary; RefreshDetails(); }

    internal void SetHoverPosition(Point? position)
    {
        _hoverPosition = position;
        if (position == null) _details.IsOpen = false;
        RefreshDetails();
    }

    private Rect Plot => ActualWidth < 100 || ActualHeight < 60 ? Rect.Empty : new(4, 25, ActualWidth - 8, ActualHeight - 49);

    private DateOnly? DateAt(Point position)
    {
        var plot = Plot;
        return plot.Contains(position) ? Series.DateAt(position.X - plot.Left, plot.Width) : null;
    }

    internal bool SelectDayAt(Point position, MouseButton button)
    {
        if (button != MouseButton.Left || DateAt(position) is not { } date || !Series.Days.Any(day => day.Date == date)) return false;
        SetHoverPosition(null);
        DaySelected?.Invoke(date);
        return true;
    }

    private void RefreshDetails()
    {
        string text = Series.Days.Count == 0 ? "" : _summary;
        DateOnly? date = _hoverPosition is { } point ? DateAt(point) : null;
        Cursor = date is { } hovered && Series.Days.Any(day => day.Date == hovered) ? Cursors.Hand : null;
        if (date is { } selected)
        {
            var day = Series.Days.FirstOrDefault(day => day.Date == selected);
            if (day == null)
                text = selected.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) + "\n" + _language[TextKey.DailyChartNoRecord];
            else
            {
                if (_hoverRow == null) _hoverRow = new(day, _language);
                else _hoverRow.Update(day, _language);
                text = _hoverRow.Details;
            }
        }
        else _hoverRow = null;
        if (_detailText.Text != text) _detailText.Text = text;
        if (Series.Days.Count == 0)
        { _hoverPosition = null; _hoverRow = null; _details.IsOpen = false; }
    }

    protected override void OnMouseMove(MouseEventArgs e)
    { base.OnMouseMove(e); SetHoverPosition(e.GetPosition(this)); }

    protected override void OnMouseLeave(MouseEventArgs e)
    { base.OnMouseLeave(e); SetHoverPosition(null); }

    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonDown(e);
        if (SelectDayAt(e.GetPosition(this), e.ChangedButton)) e.Handled = true;
    }

    protected override void OnRenderSizeChanged(SizeChangedInfo sizeInfo)
    { base.OnRenderSizeChanged(sizeInfo); RefreshDetails(); }

    protected override AutomationPeer OnCreateAutomationPeer() => new ChartAutomationPeer(this);

    private sealed class ChartAutomationPeer(DailyUsageChart owner) : FrameworkElementAutomationPeer(owner)
    {
        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Image;
        protected override string GetClassNameCore() => nameof(DailyUsageChart);
    }

    internal DailyUsageSeries Series { get; private set; } = DailyUsageSeries.Empty;
    internal DateOnly? SelectedDay { get; private set; }
    internal DateOnly? HighlightedDay => SelectedDay is { } date && Series.Days.Any(day => day.Date == date) ? date : null;

    internal void SetSelectedDay(DateOnly? date)
    {
        if (SelectedDay == date) return;
        SelectedDay = date;
        InvalidateVisual();
    }

    internal void Update(DailyUsageSeries series)
    {
        if (Series.Start == series.Start && Series.End == series.End && Series.Days.SequenceEqual(series.Days)) return;
        Series = series;
        RefreshDetails();
        InvalidateVisual();
    }

    protected override void OnRender(DrawingContext context)
    {
        base.OnRender(context);
        if (ActualWidth < 100 || ActualHeight < 60 || Series.Days.Count == 0) return;
        var plot = Plot;
        context.DrawRectangle(SystemColors.WindowBrush, new Pen(SystemColors.GrayTextBrush, 1), plot);
        double dpi = VisualTreeHelper.GetDpi(this).PixelsPerDip;
        FormattedText Text(string value) => new(value, CultureInfo.CurrentCulture, FlowDirection.LeftToRight,
            new Typeface("Segoe UI"), 12, SystemColors.WindowTextBrush, dpi);
        context.DrawText(Text(HistoryWindow.FormatBytes(Series.PeakBytes)), new Point(4, 2));
        var first = Text(Series.Start.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
        var last = Text(Series.End.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
        if (Series.Start == Series.End)
            context.DrawText(first, new Point((ActualWidth - first.Width) / 2, ActualHeight - 20));
        else
        {
            context.DrawText(first, new Point(4, ActualHeight - 20));
            context.DrawText(last, new Point(ActualWidth - 4 - last.Width, ActualHeight - 20));
        }
        Brush download = SystemParameters.HighContrast ? SystemColors.WindowTextBrush : Brushes.ForestGreen;
        Brush upload = SystemParameters.HighContrast ? SystemColors.WindowTextBrush : Brushes.RoyalBlue;
        double slot = plot.Width / Series.CalendarDays, width = slot * .32;
        decimal maximum = Math.Max(1, Series.PeakBytes);
        context.PushClip(new RectangleGeometry(plot));
        foreach (var day in Series.Days)
        {
            double x = plot.Left + (day.Date.DayNumber - Series.Start.DayNumber) * slot + slot * .12;
            void Bar(long value, double left, Brush brush, bool filled)
            {
                double height = (double)(value / maximum) * plot.Height;
                if (value == 0)
                    context.DrawLine(new Pen(brush, 1.5), new Point(left, plot.Bottom - 1), new Point(left + width, plot.Bottom - 1));
                else
                    context.DrawRectangle(filled ? brush : SystemColors.WindowBrush, filled ? null : new Pen(brush, 1),
                        new Rect(left, plot.Bottom - height, width, height));
            }
            Bar(day.Received, x, download, true);
            Bar(day.Sent, x + slot * .44, upload, false);
        }
        if (HighlightedDay is { } selected)
        {
            double left = plot.Left + (selected.DayNumber - Series.Start.DayNumber) * slot;
            // Draw over both bars so the marker survives maximum-height bars,
            // measured zeros and high contrast. The inset keeps edge days visible.
            context.DrawRectangle(null, new Pen(SystemColors.HighlightBrush, 2),
                new Rect(left + 1, plot.Top + 1, slot - 2, plot.Height - 2));
            double center = left + slot / 2, half = Math.Min(4, (slot - 2) / 2);
            var marker = new StreamGeometry();
            using (var geometry = marker.Open())
            {
                geometry.BeginFigure(new Point(center - half, plot.Top + 2), true, true);
                geometry.LineTo(new Point(center + half, plot.Top + 2), true, false);
                geometry.LineTo(new Point(center, plot.Top + 8), true, false);
            }
            marker.Freeze();
            context.DrawGeometry(SystemColors.HighlightBrush, null, marker);
        }
        context.Pop();
    }
}
