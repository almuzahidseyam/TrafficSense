using TrafficSense;
using Xunit;

public sealed class PluginHealthTests
{
    [Fact]
    public void CpuUsesIntervalAndAllProcessors()
    {
        var sampler=new PluginCpuSampler(4);
        Assert.Null(sampler.Update(TimeSpan.FromSeconds(8),TimeSpan.FromSeconds(12)));
        Assert.Equal(5d,sampler.Update(TimeSpan.FromSeconds(9),TimeSpan.FromMilliseconds(12200)));
        Assert.Equal(0d,sampler.Update(TimeSpan.FromSeconds(10),TimeSpan.FromMilliseconds(12200)));
    }
    [Fact]
    public void SubtractsIntegerCountersBeforeConvertingToDouble()
    {
        var sampler=new PluginCpuSampler(1);
        long large=long.MaxValue-10000;
        sampler.Update(TimeSpan.FromTicks(large),TimeSpan.FromTicks(large));
        Assert.Equal(10d,sampler.Update(TimeSpan.FromTicks(large+1000),TimeSpan.FromTicks(large+100)));
    }
    [Theory]
    [InlineData(0,1)] // duplicate clock
    [InlineData(-1,1)] // clock rollback
    [InlineData(1,-1)] // CPU counter rollback
    [InlineData(6,1)] // delayed observation, including resume
    public void DiscontinuousObservationRebases(double clockDelta,double cpuDelta)
    {
        var sampler=new PluginCpuSampler(2);
        sampler.Update(TimeSpan.FromSeconds(10),TimeSpan.FromSeconds(10));
        var time=TimeSpan.FromSeconds(10+clockDelta);var cpu=TimeSpan.FromSeconds(10+cpuDelta);
        Assert.Null(sampler.Update(time,cpu));
        Assert.Equal(25d,sampler.Update(time+TimeSpan.FromSeconds(1),cpu+TimeSpan.FromSeconds(.5)));
    }
    [Fact]
    public void NegativeOrResetObservationRequiresNewBaseline()
    {
        var sampler=new PluginCpuSampler(1);
        sampler.Update(TimeSpan.FromSeconds(1),TimeSpan.FromSeconds(1));
        Assert.Null(sampler.Update(TimeSpan.FromSeconds(-1),TimeSpan.Zero));
        Assert.Null(sampler.Update(TimeSpan.FromSeconds(2),TimeSpan.FromSeconds(2)));
        sampler.Reset();
        Assert.Null(sampler.Update(TimeSpan.FromSeconds(3),TimeSpan.FromSeconds(3)));
        Assert.Null(sampler.Update(TimeSpan.FromSeconds(4),TimeSpan.FromSeconds(-1)));
        Assert.Null(sampler.Update(TimeSpan.FromSeconds(5),TimeSpan.Zero));
    }
    [Fact]
    public void ImpossibleCpuIsClampedAndFiveSecondsIsAccepted()
    {
        var sampler=new PluginCpuSampler(2);
        sampler.Update(TimeSpan.Zero,TimeSpan.Zero);
        Assert.Equal(100d,sampler.Update(TimeSpan.FromSeconds(5),TimeSpan.FromSeconds(20)));
    }
    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void RejectsInvalidProcessorCount(int count)=>Assert.Throws<ArgumentOutOfRangeException>(()=>new PluginCpuSampler(count));
}
