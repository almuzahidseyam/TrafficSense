using Xunit;

namespace TrafficSense.Tests;

public sealed class DisplayTopologyMatrixTests
{
    [Theory] [InlineData(1d)] [InlineData(1.25)] [InlineData(1.5)] [InlineData(2d)]
    public void PhysicalPixelPlacementStaysVisibleAcrossTopologies(double scale)
    {
        int width=(int)Math.Ceiling(240*scale),height=(int)Math.Ceiling(90*scale);
        DisplayWorkArea[][] configurations = [
            [new(0,0,1920,1040,true),new(-2560,-200,0,1200)],
            [new(0,0,1920,1040,true),new(0,-1440,2560,0)],
            [new(64,48,1920,1080,true),new(2400,0,6240,2080)],
            [new(0,0,1280,680,true)],
            [new(-3840,-2160,0,0,true),new(0,0,1920,1040)] ];
        foreach(var displays in configurations)
        foreach(int x in new[]{-5000,-2500,-1,0,1100,1900,3000,7000})
        foreach(int y in new[]{-3000,-1300,-1,0,500,1050,2200})
        {
            var resolved=WidgetPlacement.Resolve(x,y,width,height,displays);
            Assert.Contains(displays,a=>resolved.Left>=a.Left && resolved.Top>=a.Top && resolved.Left+(long)width<=a.Right && resolved.Top+(long)height<=a.Bottom);
        }
    }
}
