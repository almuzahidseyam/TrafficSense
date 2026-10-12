using System.Net.NetworkInformation;
using Xunit;

namespace TrafficSense.Tests;

public sealed class NetworkAdapterSelectionTests
{
    [Theory]
    [InlineData(NetworkInterfaceType.Tunnel)]
    [InlineData(NetworkInterfaceType.Ethernet)] // TAP VPNs can report Ethernet.
    public void VpnAndUnderlyingTransferAreCountedOnce(NetworkInterfaceType vpnType)
    {
        var selector = new NetworkAdapterSelection();
        var calculator = new NetworkSpeedCalculator();
        var before = new[] { Adapter("ethernet"), Adapter("vpn", type: vpnType) };
        var after = new[] { Adapter("ethernet", received: 1024, sent: 512),
            Adapter("vpn", type: vpnType, received: 1024, sent: 512) };

        calculator.Update(Sample(selector.Select(before, null)), TimeSpan.Zero);
        var speed = calculator.Update(Sample(selector.Select(after, null)), TimeSpan.FromSeconds(1));

        Assert.Equal(1024, speed.DownloadBytesPerSecond);
        Assert.Equal(512, speed.UploadBytesPerSecond);
    }

    [Fact]
    public void ManualVpnSelectionExcludesUnderlyingConnectionAndOtherTraffic()
    {
        var selector = new NetworkAdapterSelection();
        var calculator = new NetworkSpeedCalculator();
        calculator.Update(Sample(selector.Select([Adapter("ethernet"), Adapter("vpn")], "vpn")), TimeSpan.Zero);
        var selected = selector.Select([Adapter("ethernet", received: 5000), Adapter("vpn", received: 1000)], "vpn");
        var speed = calculator.Update(Sample(selected), TimeSpan.FromSeconds(1));
        Assert.Equal("vpn", selected!.Id);
        Assert.Equal(1000, speed.DownloadBytesPerSecond);
    }

    [Fact]
    public void ManualDisconnectedAdapterWaitsThenResumesWithoutSpike()
    {
        var selector = new NetworkAdapterSelection();
        var calculator = new NetworkSpeedCalculator();
        calculator.Update(Sample(selector.Select([Adapter("vpn", received: 100)], "vpn")), TimeSpan.Zero);
        var offline = selector.Select([Adapter("ethernet"), Adapter("vpn", isUp: false)], "vpn");
        Assert.Null(offline);
        Assert.Equal(default, calculator.Update(Sample(offline), TimeSpan.FromSeconds(1)));
        Assert.Equal(default, calculator.Update(Sample(selector.Select([Adapter("vpn", received: 9000)], "vpn")), TimeSpan.FromSeconds(2)));
        Assert.Equal(100, calculator.Update(Sample(selector.Select([Adapter("vpn", received: 9100)], "vpn")), TimeSpan.FromSeconds(3)).DownloadBytesPerSecond);
    }

    [Fact]
    public void MissingManualAdapterDoesNotFallBack()
    {
        Assert.Null(new NetworkAdapterSelection().Select([Adapter("ethernet")], "missing-vpn"));
    }

    [Fact]
    public void AutoFailsOverWithoutMixingCounterBaselines()
    {
        var selector = new NetworkAdapterSelection();
        var calculator = new NetworkSpeedCalculator();
        calculator.Update(Sample(selector.Select([Adapter("ethernet", received: 100)], null)), TimeSpan.Zero);
        var replacement = selector.Select([Adapter("ethernet", isUp: false), Adapter("wifi", received: 100000)], null);
        Assert.Equal("wifi", replacement!.Id);
        Assert.Equal(default, calculator.Update(Sample(replacement), TimeSpan.FromSeconds(1)));
        var next = selector.Select([Adapter("wifi", received: 101000)], null);
        Assert.Equal(1000, calculator.Update(Sample(next), TimeSpan.FromSeconds(2)).DownloadBytesPerSecond);
    }

    [Fact]
    public void EquallyPreferredConnectionsRemainStableRegardlessOfEnumerationOrder()
    {
        var selector = new NetworkAdapterSelection();
        Assert.Equal("b", selector.Select([Adapter("b")], null)!.Id);
        Assert.Equal("b", selector.Select([Adapter("a"), Adapter("b")], null)!.Id);
        Assert.Equal("b", selector.Select([Adapter("b"), Adapter("a")], null)!.Id);
    }

    [Fact]
    public void AutoPrefersGatewayAndCanUpgradeFromFallback()
    {
        var selector = new NetworkAdapterSelection();
        Assert.Equal("fallback", selector.Select([Adapter("fallback", gateway: false)], null)!.Id);
        Assert.Equal("gateway", selector.Select([Adapter("fallback", gateway: false), Adapter("gateway")], null)!.Id);
    }

    [Fact]
    public void AutoPrefersEthernetOrWifiOverTunnelWithEqualGatewayStatus()
    {
        Assert.Equal("wifi", new NetworkAdapterSelection().Select(
            [Adapter("a-vpn", type: NetworkInterfaceType.Tunnel), Adapter("wifi", type: NetworkInterfaceType.Wireless80211)], null)!.Id);
    }

    [Fact]
    public void AutoFollowsWindowsRouteIncludingVpnWithoutGateway()
    {
        var selector = new NetworkAdapterSelection();
        Assert.Equal("ethernet", selector.Select([Adapter("ethernet")], null)!.Id);
        var adapters = new[] { Adapter("ethernet"),
            Adapter("vpn", gateway: false, type: NetworkInterfaceType.Tunnel) with { IsPreferredRoute = true } };
        Assert.Equal("vpn", selector.Select(adapters, null)!.Id);
        Assert.Equal("ethernet", selector.Select(adapters, "ethernet")!.Id);
    }

    [Fact]
    public void AutoFollowsRouteChangesBetweenConnectedAdapters()
    {
        var selector = new NetworkAdapterSelection();
        Assert.Equal("ethernet", selector.Select([Adapter("ethernet") with { IsPreferredRoute = true }, Adapter("wifi")], null)!.Id);
        Assert.Equal("wifi", selector.Select([Adapter("ethernet"), Adapter("wifi") with { IsPreferredRoute = true }], null)!.Id);
    }

    [Fact]
    public void UnavailablePreferredRouteCannotBlockFallback()
    {
        Assert.Equal("ethernet", new NetworkAdapterSelection().Select(
            [Adapter("vpn", isUp: false) with { IsPreferredRoute = true }, Adapter("ethernet")], null)!.Id);
    }

    [Theory]
    [InlineData("Hyper-V Virtual Ethernet Adapter")]
    [InlineData("WSL")]
    [InlineData("VirtualBox Host-Only Ethernet Adapter")]
    [InlineData("VMware Virtual Ethernet Adapter")]
    public void LocalVmAdaptersHaveLowerAutoPriorityButCanBeSelectedManually(string description)
    {
        var adapters = new[] { Adapter("a-vm") with { Description = description }, Adapter("ethernet", gateway: false) };
        var selector = new NetworkAdapterSelection();
        Assert.Equal("ethernet", selector.Select(adapters, null)!.Id);
        Assert.Equal("a-vm", selector.Select(adapters, "a-vm")!.Id);
    }

    [Fact]
    public void InterfacesWithoutAddressesOrSamplesAreNotMonitored()
    {
        var adapters = new[] { Adapter("filter") with { HasAddress = false },
            Adapter("unreadable") with { Snapshot = null }, Adapter("down", isUp: false) };
        var selector = new NetworkAdapterSelection();
        Assert.Null(selector.Select(adapters, null));
        Assert.Null(selector.Select(adapters, "filter"));
    }

    [Fact]
    public void NoInterfacesIsSafeInBothModes()
    {
        var selector = new NetworkAdapterSelection();
        Assert.Null(selector.Select([], null));
        Assert.Null(selector.Select([], "missing"));
    }

    private static NetworkAdapterInfo Adapter(string id, bool isUp = true, bool gateway = true,
        NetworkInterfaceType type = NetworkInterfaceType.Ethernet, long received = 0, long sent = 0) =>
        new(id, id, id, type, isUp, true, gateway, new(id, received, sent));

    private static NetworkAdapterSnapshot[] Sample(NetworkAdapterInfo? adapter) =>
        adapter?.Snapshot is { } snapshot ? [snapshot] : [];
}
