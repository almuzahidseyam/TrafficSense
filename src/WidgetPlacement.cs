namespace TrafficSense;

internal readonly record struct DisplayWorkArea(int Left, int Top, int Right, int Bottom, bool IsPrimary = false);
internal readonly record struct WidgetPosition(int Left, int Top);

internal static class WidgetPlacement
{
    public static WidgetPosition Resolve(int? left, int? top, int width, int height, IReadOnlyList<DisplayWorkArea> displays)
    {
        if (displays.Count == 0)
            throw new ArgumentException("At least one display is required.", nameof(displays));

        var display = displays.FirstOrDefault(area => area.IsPrimary, displays[0]);
        if (!left.HasValue || !top.HasValue)
        {
            return new(Math.Max(display.Left, display.Right - width - 20),
                Math.Max(display.Top, display.Bottom - height - 20));
        }

        // Use the monitor containing most of the widget, or the nearest remaining
        // work area when the old display is gone or the position falls in a gap.
        display = displays.OrderByDescending(area => Overlap(area, left.Value, top.Value, width, height))
            .ThenBy(area => Distance(area, left.Value, top.Value))
            .ThenByDescending(area => area.IsPrimary).First();

        return new(Math.Clamp(left.Value, display.Left, Math.Max(display.Left, display.Right - width)),
            Math.Clamp(top.Value, display.Top, Math.Max(display.Top, display.Bottom - height)));
    }

    private static double Overlap(DisplayWorkArea area, int left, int top, int width, int height) =>
        Math.Max(0, Math.Min(area.Right, (double)left + width) - Math.Max(area.Left, left)) *
        Math.Max(0, Math.Min(area.Bottom, (double)top + height) - Math.Max(area.Top, top));

    private static double Distance(DisplayWorkArea area, int left, int top)
    {
        double dx = (double)left - Math.Clamp(left, area.Left, area.Right);
        double dy = (double)top - Math.Clamp(top, area.Top, area.Bottom);
        return dx * dx + dy * dy;
    }
}
