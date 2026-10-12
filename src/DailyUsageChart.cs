using System.Globalization;
using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Media;

namespace TrafficSense;

internal sealed class DailyUsageChart : FrameworkElement
{
    protected override AutomationPeer OnCreateAutomationPeer() => new ChartAutomationPeer(this);

    private sealed class ChartAutomationPeer(DailyUsageChart owner) : FrameworkElementAutomationPeer(owner)
    {
        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Image;
        protected override string GetClassNameCore() => nameof(DailyUsageChart);
    }

    internal DailyUsageSeries Series { get; private set; } = DailyUsageSeries.Empty;

    internal void Update(DailyUsageSeries series)
    {
        if (Series.Start == series.Start && Series.End == series.End && Series.Days.SequenceEqual(series.Days)) return;
        Series = series;
        InvalidateVisual();
    }

    protected override void OnRender(DrawingContext context)
    {
        base.OnRender(context);
        if (ActualWidth < 100 || ActualHeight < 60 || Series.Days.Count == 0) return;
        var plot = new Rect(4, 25, ActualWidth - 8, ActualHeight - 49);
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
        context.Pop();
    }
}
