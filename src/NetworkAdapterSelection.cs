using System.Net.NetworkInformation;

namespace TrafficSense;

internal sealed record NetworkAdapterInfo(
    string Id, string Name, string Description, NetworkInterfaceType Type,
    bool IsUp, bool HasAddress, bool HasGateway, NetworkAdapterSnapshot? Snapshot)
{
    public bool CanMonitor => IsUp && HasAddress && Snapshot.HasValue;
    public bool IsPreferredRoute { get; init; }
    public string Addresses { get; init; } = "";
    public long LinkBitsPerSecond { get; init; }
}

internal sealed class NetworkAdapterSelection
{
    private string? _automaticAdapterId;

    public NetworkAdapterInfo? Select(IReadOnlyCollection<NetworkAdapterInfo> adapters, string? selectedId)
    {
        // A missing/disconnected explicit choice must not silently monitor another
        // connection. Retain its ID so reconnecting resumes automatically.
        if (selectedId != null)
            return adapters.FirstOrDefault(adapter => adapter.Id == selectedId && adapter.CanMonitor);

        var candidates = adapters.Where(adapter => adapter.CanMonitor)
            .OrderBy(Priority)
            .ThenBy(adapter => adapter.Id, StringComparer.Ordinal)
            .ToArray();
        var best = candidates.FirstOrDefault();
        var previous = candidates.FirstOrDefault(adapter => adapter.Id == _automaticAdapterId);
        if (best != null && previous != null && Priority(previous) == Priority(best))
            best = previous;

        _automaticAdapterId = best?.Id;
        // Always return one adapter: summing a VPN and its underlying connection
        // counts the same transfer twice, including VPNs exposed as Ethernet.
        return best;
    }

    private static int Priority(NetworkAdapterInfo adapter)
    {
        if (adapter.IsPreferredRoute)
            return -1;
        bool localVirtualNetwork = new[] { "hyper-v", "wsl", "virtualbox", "vmware" }
            .Any(name => adapter.Description.Contains(name, StringComparison.OrdinalIgnoreCase));
        int priority = localVirtualNetwork ? 4 : 0;
        if (!adapter.HasGateway) priority += 2;
        if (adapter.Type is not (NetworkInterfaceType.Ethernet or NetworkInterfaceType.Wireless80211))
            priority++;
        return priority;
    }
}
