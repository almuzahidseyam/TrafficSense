using System.Net.NetworkInformation;
using System.Runtime.InteropServices;

namespace TrafficSense;

internal static class NetworkSnapshotReader
{
    public static IReadOnlyCollection<NetworkAdapterInfo> Read()
    {
        NetworkInterface[] adapters;
        try
        {
            adapters = NetworkInterface.GetAllNetworkInterfaces();
        }
        catch (NetworkInformationException)
        {
            // An unavailable sample resets the baseline instead of leaving stale
            // counters that would produce a spike when the network returns.
            return Array.Empty<NetworkAdapterInfo>();
        }

        var result = new List<NetworkAdapterInfo>();
        // Query the routing table only; this does not send a packet to 1.1.1.1.
        // On IPv6-only/offline systems, selection falls back to gateway metadata.
        uint? preferredIndex = GetBestInterface(0x01010101, out uint index) == 0 ? index : null;

        foreach (var adapter in adapters)
        {
            try
            {
                if (adapter.NetworkInterfaceType == NetworkInterfaceType.Loopback)
                {
                    continue;
                }

                var properties = adapter.GetIPProperties();
                // Filter-driver pseudo interfaces duplicate their parent adapter
                // in Windows enumeration and are not independent IP connections.
                if (properties.UnicastAddresses.Count == 0 &&
                    (adapter.Description.Contains("filter", StringComparison.OrdinalIgnoreCase) ||
                     adapter.Description.Contains("packet scheduler", StringComparison.OrdinalIgnoreCase)))
                    continue;
                bool isUp = adapter.OperationalStatus == OperationalStatus.Up;
                NetworkAdapterSnapshot? snapshot = null;
                if (isUp && properties.UnicastAddresses.Count > 0)
                {
                    var statistics = adapter.GetIPStatistics();
                    snapshot = new(adapter.Id, statistics.BytesReceived, statistics.BytesSent);
                }
                result.Add(new(adapter.Id, adapter.Name, adapter.Description,
                    adapter.NetworkInterfaceType, isUp, properties.UnicastAddresses.Count > 0,
                    properties.GatewayAddresses.Count > 0, snapshot)
                {
                    IsPreferredRoute = preferredIndex.HasValue &&
                        adapter.Supports(NetworkInterfaceComponent.IPv4) &&
                        properties.GetIPv4Properties()?.Index == preferredIndex.Value
                });
            }
            catch (NetworkInformationException)
            {
                // Adapters can disappear between enumeration and reading counters.
                // Keep monitoring the other adapters without a recurring error dialog.
            }
        }

        // Windows also enumerates packet-filter interfaces without IP addresses.
        // They have no sample and cannot be selected automatically.
        return result;
    }

    // https://learn.microsoft.com/windows/win32/api/iphlpapi/nf-iphlpapi-getbestinterface
    [DllImport("iphlpapi.dll", ExactSpelling = true)]
    private static extern uint GetBestInterface(uint destinationAddress, out uint bestInterfaceIndex);
}
