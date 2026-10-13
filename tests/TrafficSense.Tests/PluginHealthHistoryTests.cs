using TrafficSense;
using Xunit;

public sealed class PluginHealthHistoryTests
{
    [Fact]
    public void RetainsTwoMinuteBoundaryAtNormalCadence()
    {
        var history=new PluginHealthHistory();
        for(int i=0;i<=1000;i++)history.Add(TimeSpan.FromSeconds(i),10,i);
        Assert.Equal(121,history.Samples.Count);
        Assert.Equal(TimeSpan.FromSeconds(880),history.Samples.First().Timestamp);
        Assert.Equal(TimeSpan.FromSeconds(1000),history.Samples.Last().Timestamp);
    }
    [Fact]
    public void FastReadBurstsCannotGrowStorageWithoutBound()
    {
        var history=new PluginHealthHistory();
        for(int i=0;i<10000;i++)history.Add(TimeSpan.FromMilliseconds(i),10,i);
        Assert.Equal(PluginHealthHistory.MaximumSamples,history.Samples.Count);
        Assert.Equal(9999,history.Samples.Last().WorkingSetBytes);
    }
    [Fact]
    public void MissingCpuDoesNotDiscardRealRamAndZeroIsMeasured()
    {
        var history=new PluginHealthHistory();
        history.Add(TimeSpan.Zero,null,1234);history.Add(TimeSpan.FromSeconds(1),0,0);history.Add(TimeSpan.FromSeconds(2),null,null);
        var points=history.Samples.ToArray();
        Assert.Null(points[0].CpuPercent);Assert.Equal(1234,points[0].WorkingSetBytes);
        Assert.Equal(0d,points[1].CpuPercent);Assert.Equal(0,points[1].WorkingSetBytes);
        Assert.Null(points[2].CpuPercent);Assert.Null(points[2].WorkingSetBytes);
    }
    [Theory]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(double.NegativeInfinity)]
    [InlineData(-1)]
    [InlineData(101)]
    public void InvalidCpuRemainsUnknownWithoutAffectingRam(double invalid)
    {
        var history=new PluginHealthHistory();history.Add(TimeSpan.Zero,invalid,1234);
        Assert.Null(history.Samples.Single().CpuPercent);Assert.Equal(1234,history.Samples.Single().WorkingSetBytes);
    }
    [Fact]
    public void InvalidRamDoesNotAffectCpuAndMaximumBytesRemainFinite()
    {
        var history=new PluginHealthHistory();history.Add(TimeSpan.Zero,100,-1);history.Add(TimeSpan.FromSeconds(1),50,long.MaxValue);
        Assert.Null(history.Samples.First().WorkingSetBytes);Assert.Equal(100d,history.Samples.First().CpuPercent);
        Assert.Equal(long.MaxValue,history.Samples.Last().WorkingSetBytes);
    }
    [Fact]
    public void DuplicateRollbackAndNegativeTimesDoNotRewriteHistory()
    {
        var history=new PluginHealthHistory();history.Add(TimeSpan.FromSeconds(10),10,10);
        foreach(int i in new[]{10,9,-1})history.Add(TimeSpan.FromSeconds(i),99,99);
        Assert.Single(history.Samples);Assert.Equal(10d,history.Samples.Single().CpuPercent);
    }
    [Fact]
    public void ClearAllowsNewSessionClockWithoutOldSamples()
    {
        var history=new PluginHealthHistory();history.Add(TimeSpan.FromSeconds(100),10,10);history.Clear();
        Assert.Empty(history.Samples);history.Add(TimeSpan.Zero,null,1);
        Assert.Single(history.Samples);Assert.Null(history.Samples.Single().CpuPercent);
    }
    [Fact]
    public void LongGapExpiresPreviousSessionWindowWithoutFabricatedZeros()
    {
        var history=new PluginHealthHistory();history.Add(TimeSpan.Zero,50,10);history.Add(TimeSpan.FromSeconds(121),null,null);
        Assert.Single(history.Samples);Assert.Null(history.Samples.Single().CpuPercent);Assert.Null(history.Samples.Single().WorkingSetBytes);
    }
}
