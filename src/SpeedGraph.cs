using System.Globalization;
using System.Windows;
using System.Windows.Media;

namespace TrafficSense;

internal sealed class SpeedGraph : FrameworkElement
{
    private SpeedPoint[] _points = [];
    private bool _bits;
    internal void Update(IEnumerable<SpeedPoint> points, bool bits)
    { _points = points.ToArray(); _bits = bits; InvalidateVisual(); }

    protected override void OnRender(DrawingContext context)
    {
        base.OnRender(context);
        if (ActualWidth < 100 || ActualHeight < 60) return;
        var plot = new Rect(4, 25, ActualWidth - 8, ActualHeight - 48);
        var pen = new Pen(SystemColors.GrayTextBrush, 1);
        context.DrawRectangle(SystemColors.WindowBrush, pen, plot);
        double maximum = Math.Max(1, _points.Select(p => Math.Max(p.Speed.DownloadBytesPerSecond, p.Speed.UploadBytesPerSecond)).DefaultIfEmpty().Max());
        var dpi = VisualTreeHelper.GetDpi(this).PixelsPerDip;
        void Label(string value, double x, double y) => context.DrawText(new FormattedText(value, CultureInfo.CurrentCulture,
            FlowDirection.LeftToRight, new Typeface("Segoe UI"), 12, SystemColors.WindowTextBrush, dpi), new Point(x, y));
        Label(SpeedFormatter.Format(maximum, _bits), 4, 2);
        Label("−120 s", 4, ActualHeight - 20);
        Label("0 s", ActualWidth - 28, ActualHeight - 20);
        if (_points.Length == 0) return;
        double end = _points[^1].Timestamp.TotalSeconds;
        context.PushClip(new RectangleGeometry(plot));
        void Line(Func<NetworkSpeed, double> select, Brush brush, DashStyle dash)
        {
            var geometry = new StreamGeometry();
            using (var drawing = geometry.Open())
            {
                bool started = false; TimeSpan? previous = null;
                foreach (var sample in _points)
                {
                    var point = new Point(plot.Left + (sample.Timestamp.TotalSeconds - end + 120) / 120 * plot.Width,
                        plot.Bottom - select(sample.Speed) / maximum * plot.Height);
                    if (!started || sample.Timestamp - previous!.Value > TimeSpan.FromSeconds(5)) drawing.BeginFigure(point, false, false);
                    else drawing.LineTo(point, true, false);
                    started = true; previous = sample.Timestamp;
                }
            }
            geometry.Freeze();
            context.DrawGeometry(null, new Pen(brush, 2) { DashStyle = dash }, geometry);
        }
        Line(s => s.DownloadBytesPerSecond, SystemParameters.HighContrast ? SystemColors.WindowTextBrush : Brushes.ForestGreen, DashStyles.Solid);
        Line(s => s.UploadBytesPerSecond, SystemParameters.HighContrast ? SystemColors.WindowTextBrush : Brushes.RoyalBlue, DashStyles.Dash);
        context.Pop();
    }
}
