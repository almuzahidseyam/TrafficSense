using System.Net.NetworkInformation;

namespace TrafficSense;

internal static class NetworkSnapshotReader
{
    public static IReadOnlyCollection<NetworkAdapterSnapshot> Read()
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
            return Array.Empty<NetworkAdapterSnapshot>();
        }

        var gatewayAdapters = new List<NetworkAdapterSnapshot>();
        var fallbackAdapters = new List<NetworkAdapterSnapshot>();

        foreach (var adapter in adapters)
        {
            try
            {
                if (adapter.OperationalStatus != OperationalStatus.Up ||
                    adapter.NetworkInterfaceType == NetworkInterfaceType.Loopback)
                {
                    continue;
                }

                var statistics = adapter.GetIPStatistics();
                var snapshot = new NetworkAdapterSnapshot(
                    adapter.Id, statistics.BytesReceived, statistics.BytesSent);

                if (adapter.GetIPProperties().GatewayAddresses.Count > 0)
                {
                    gatewayAdapters.Add(snapshot);
                }

                if (!adapter.Description.Contains("wsl", StringComparison.OrdinalIgnoreCase) &&
                    !adapter.Description.Contains("hyper-v", StringComparison.OrdinalIgnoreCase) &&
                    !adapter.Description.Contains("virtual", StringComparison.OrdinalIgnoreCase))
                {
                    fallbackAdapters.Add(snapshot);
                }
            }
            catch (NetworkInformationException)
            {
                // Adapters can disappear between enumeration and reading counters.
                // Keep monitoring the other adapters without a recurring error dialog.
            }
        }

        return gatewayAdapters.Count > 0 ? gatewayAdapters : fallbackAdapters;
    }
}
