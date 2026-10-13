using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using TrafficSense;

internal static class GraphQualityProbe
{
    internal static int Run()
    {
        var graph = new SpeedGraph();
        graph.Update([
            new(TimeSpan.Zero, new(100, 50)), new(TimeSpan.FromSeconds(1), new(110, 55)),
            new(TimeSpan.FromSeconds(2), new(9000, 9000), false),
            new(TimeSpan.FromSeconds(3), new(120, 60)), new(TimeSpan.FromSeconds(4), new(130, 65))], false);
        graph.Measure(new Size(480, 170)); graph.Arrange(new Rect(0, 0, 480, 170));
        new RenderTargetBitmap(480, 170, 96, 96, PixelFormats.Pbgra32).Render(graph);
        var lines = Drawings(VisualTreeHelper.GetDrawing(graph)).OfType<GeometryDrawing>().Where(drawing => drawing.Pen?.Thickness == 2).ToArray();
        // Preserve open, unfilled line figures; flattening can omit unfilled figures.
        if (lines.Length != 2 || lines.Any(line => PathGeometry.CreateFromGeometry(line.Geometry).Figures.Count != 2))
            throw new InvalidOperationException("Unknown graph interval was connected to a measured zero or adjacent sample.");
        if (lines[0].Geometry.Bounds.Height < 20)
            throw new InvalidOperationException("Invalid rate influenced the graph scale.");
        Console.WriteLine("PASS: real WPF graph breaks both rate paths over unavailable samples and excludes them from its scale.");
        PluginHealthGraphProbe.Run();
        return 0;
    }
    private static IEnumerable<Drawing> Drawings(Drawing drawing)
    {
        yield return drawing;
        if (drawing is DrawingGroup group) foreach (var child in group.Children) foreach (var nested in Drawings(child)) yield return nested;
    }
}
