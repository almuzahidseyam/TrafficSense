using Xunit;

namespace TrafficSense.Tests;

public class WidgetPlacementTests
{
    private static readonly DisplayWorkArea Primary = new(0, 0, 1920, 1040, true);

    [Fact]
    public void FirstLaunchUsesPrimaryWorkAreaAndMeasuredSize()
    {
        var position = WidgetPlacement.Resolve(null, null, 200, 60,
            new[] { new DisplayWorkArea(-1600, 0, 0, 900), Primary });
        Assert.Equal(new WidgetPosition(1700, 960), position);
    }

    [Theory]
    [InlineData(-1500, 200)]
    [InlineData(-1, 200)]
    public void ValidNegativeScreenCoordinatesAreSupported(int left, int top)
    {
        var position = WidgetPlacement.Resolve(left, top, 200, 60,
            new[] { new DisplayWorkArea(-1600, 0, 0, 900), Primary });
        // -1 straddles monitors and is clamped to the monitor containing most of the widget.
        Assert.Equal(new WidgetPosition(left == -1 ? 0 : left, top), position);
    }

    [Fact]
    public void MonitorAbovePrimaryKeepsNegativeVerticalPosition()
    {
        Assert.Equal(new WidgetPosition(100, -500), WidgetPlacement.Resolve(100, -500, 200, 60,
            new[] { Primary, new DisplayWorkArea(0, -1080, 1920, 0) }));
    }

    [Fact]
    public void RemovedSecondaryMonitorReturnsWidgetToRemainingDisplay()
    {
        Assert.Equal(new WidgetPosition(1720, 200),
            WidgetPlacement.Resolve(2500, 200, 200, 60, new[] { Primary }));
    }

    [Fact]
    public void PartiallyHiddenWidgetIsFullyVisible()
    {
        Assert.Equal(new WidgetPosition(1720, 980),
            WidgetPlacement.Resolve(1850, 1020, 200, 60, new[] { Primary }));
    }

    [Fact]
    public void ResolutionShrinkRepositionsWidget()
    {
        Assert.Equal(new WidgetPosition(1080, 660),
            WidgetPlacement.Resolve(1700, 960, 200, 60, new[] { new DisplayWorkArea(0, 0, 1280, 720, true) }));
    }

    [Fact]
    public void PositionInMonitorGapMovesIntoARealWorkArea()
    {
        var position = WidgetPlacement.Resolve(1600, 200, 200, 60,
            new[] { new DisplayWorkArea(0, 0, 1000, 900, true), new DisplayWorkArea(2000, 0, 3000, 900) });
        Assert.Equal(new WidgetPosition(2000, 200), position);
    }

    [Fact]
    public void TaskbarAtTopOrLeftDoesNotCoverWidget()
    {
        Assert.Equal(new WidgetPosition(60, 40),
            WidgetPlacement.Resolve(0, 0, 200, 60, new[] { new DisplayWorkArea(60, 40, 1920, 1080, true) }));
    }

    [Fact]
    public void OversizedWidgetKeepsItsDragAreaReachable()
    {
        Assert.Equal(new WidgetPosition(0, 0),
            WidgetPlacement.Resolve(100, 100, 500, 300, new[] { new DisplayWorkArea(0, 0, 200, 100, true) }));
    }

    [Fact]
    public void ExtremeSavedCoordinatesDoNotOverflow()
    {
        Assert.Equal(new WidgetPosition(1720, 980),
            WidgetPlacement.Resolve(int.MaxValue, int.MaxValue, 200, 60, new[] { Primary }));
    }
}
