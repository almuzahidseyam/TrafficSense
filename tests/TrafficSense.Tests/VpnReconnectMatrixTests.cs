using System.Net.NetworkInformation;
using Xunit;

namespace TrafficSense.Tests;

public sealed class VpnReconnectMatrixTests
{
    [Theory]
    [InlineData(250)] [InlineData(500)] [InlineData(1000)] [InlineData(2000)]
    public void RouteChangesDisconnectAndCounterResetNeverMixDomains(int interval)
    {
        TimeSpan time=TimeSpan.Zero; long physical=1000,tunnel=1_000_000; bool routeTunnel=true,tunnelUp=true;
        NetworkAdapterInfo Adapter(string id,long rx,bool up,bool preferred)=>new(id,id,id,NetworkInterfaceType.Ethernet,up,true,id=="physical",up?new(id,rx,rx/2):null){IsPreferredRoute=preferred};
        using var service=new NetworkSamplingService(()=>[Adapter("physical",physical,true,!routeTunnel),Adapter("warp",tunnel,tunnelUp,routeTunnel)],()=>time,()=>new(2026,10,13,12,0,0,TimeSpan.Zero),null,interval,start:false);
        SamplingDelivery Step(){time+=TimeSpan.FromMilliseconds(interval);service.Sample();return service.Consume();}
        Assert.Equal(SampleQuality.Sampling,Step().Quality);
        tunnel+=1000; physical+=1100; var steady=Step();
        Assert.Equal("warp",steady.Adapter!.Id); Assert.Equal(1000,Assert.Single(steady.Usage).Received);
        Assert.Equal(1000/(interval/1000d),steady.Speed.DownloadBytesPerSecond);
        routeTunnel=false; physical+=2000; tunnel+=1800; var switched=Step();
        Assert.Equal(SampleQuality.Sampling,switched.Quality); Assert.Empty(switched.Usage); Assert.Equal(default,switched.Speed);
        physical+=2000; var fallback=Step(); Assert.Equal("physical",fallback.Adapter!.Id); Assert.Equal(2000,Assert.Single(fallback.Usage).Received);
        tunnelUp=false; physical+=50; Assert.Equal(50,Assert.Single(Step().Usage).Received);
        tunnelUp=true; tunnel=5; routeTunnel=true; var reconnect=Step(); Assert.Equal(SampleQuality.Sampling,reconnect.Quality); Assert.Empty(reconnect.Usage);
        tunnel+=500; var resumed=Step(); Assert.Equal(500,Assert.Single(resumed.Usage).Received);
        service.Configure("warp",interval); tunnelUp=false; var missing=Step(); Assert.Null(missing.Adapter); Assert.Equal(SampleQuality.Unavailable,missing.Quality); Assert.Empty(missing.Usage);
        tunnelUp=true; tunnel=1; Assert.Equal(SampleQuality.Sampling,Step().Quality);
        tunnel+=100; Assert.Equal(100,Assert.Single(Step().Usage).Received);
        Assert.Empty(service.Consume().Usage);
    }
}
