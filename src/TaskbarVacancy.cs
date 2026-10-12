namespace TrafficSense;

internal readonly record struct PixelRect(int Left, int Top, int Right, int Bottom)
{
    internal int Width => Right - Left;
    internal int Height => Bottom - Top;
    internal bool Intersects(PixelRect other) => Left < other.Right && Right > other.Left && Top < other.Bottom && Bottom > other.Top;
}
internal static class TaskbarVacancy
{
    // Only horizontal bars with a complete accessibility inventory are accepted.
    // Reserve a margin around every occupied rectangle; never move shell controls.
    internal static PixelRect? Find(PixelRect bar, IEnumerable<PixelRect> occupied, int width, int height, bool complete)
    {
        if (!complete || bar.Height is < 20 or > 160 || bar.Width < bar.Height || width < 80 || height < 16 || height > bar.Height) return null;
        var blocks = occupied.Where(r => r.Intersects(bar)).Select(r => (Left: Math.Max(bar.Left, r.Left - 8), Right: Math.Min(bar.Right, r.Right + 8))).OrderBy(r => r.Left).ToArray();
        if (blocks.Length == 0) return null;
        int cursor = bar.Left + 8; PixelRect? best = null; int bestGap = 0;
        foreach (var block in blocks.Append((Left: bar.Right - 8, Right: bar.Right)))
        {
            int gap = block.Left - cursor;
            if (gap >= width && gap > bestGap) { best = new(block.Left - width, bar.Top + (bar.Height - height) / 2, block.Left, bar.Top + (bar.Height + height) / 2); bestGap = gap; }
            cursor = Math.Max(cursor, block.Right);
        }
        return best;
    }
}
