# Measurement and verification

## What is measured

For one stable selected interface:

`download bytes/s = (new received bytes − previous received bytes) / elapsed seconds`

Upload uses sent bytes. Counters are subtracted as integers **before** conversion to floating-point rates, preserving tiny deltas even when lifetime counters exceed double's exact integer range. Native reads are timestamped at the midpoint of the counter call with a monotonic clock; enumeration/display latency is not used as the interval denominator.

.NET 8's Windows `GetIPStatistics()` uses `GetIfEntry2`/`MIB_IF_ROW2` [64-bit counters](https://github.com/dotnet/runtime/blob/v8.0.0/src/libraries/System.Net.NetworkInformation/src/System/Net/NetworkInformation/SystemIPInterfaceStatistics.cs). TrafficSense does not truncate counters to 32 bits or estimate exact usage by integrating rounded displayed rates.

Production sampling is serialized on a background timer. The UI consumes the latest speed and drains accumulated daily byte totals once. A slow UI does not stop native collection; native calls cannot overlap. A fresh read is still an interval average. Windows driver batching/offload, local traffic, protocol/VPN overhead and different sampling boundaries can explain differences from browser download speeds.

## Unknown intervals and units

First observation, adapter/route changes, reconnects, counter rollbacks, power resume and gaps over five seconds reset the baseline. Negative/duplicate/invalid adapters are rejected. Unknown periods contribute no invented bytes; the realtime graph breaks its lines and shows unavailable rather than plotting fabricated idle. Valid measured zero remains zero. The widget exposes baseline/unavailable/delayed status.

Bytes display uses binary steps: KB = 1,024 bytes, MB = 1,048,576 bytes. Bits display uses decimal steps: Mbps = 1,000,000 bits/s. Thus 1 MB/s = 8.388608 Mbps under these display conventions. Exact CSV is integer bytes, independent of display units and language.

Daily totals use the local day at an interval's end. A midnight-crossing interval is assigned to that ending day, because its within-interval traffic distribution is unknown. History retains up to 400 recorded days; missing days do not become measured zero days. Periodic saves and normal exit persist usage; a crash can lose the unsaved recent interval, typically up to the one-minute save period. Time while the app is closed is unobserved.

## Reproducible arithmetic evidence

`MeasurementTraceTests` runs 4,096 samples for each supported interval (250, 500, 1,000 and 2,000 ms), varying actual read time and post-read enumeration delay. It checks a known 125,000,000 / 12,500,000 bytes/s download/upload trace, relative floating-point error ≤10⁻¹² and exact byte conservation across totals beyond 4 GiB. A separate case checks a one-byte delta near `long.MaxValue`. These are synthetic fixtures, not physical bandwidth measurements.

Other tests cover malformed counters, reconnect/reset gaps, default route/VPN selection, UI delivery concurrency, midnight buffering and drain-once accounting. `--graph-quality` renders the real WPF graph and checks that both paths break over unavailable samples without changing the scale. Run:

```powershell
dotnet test tests/TrafficSense.Tests -c Release
dotnet run --project tests/TrafficSense.LifecycleTests -c Release -- --graph-quality
```

## Physical comparison procedure

1. Record app version, Windows build, NIC/driver, selected interface, VPN state, units and refresh interval. Use a controlled LAN peer/transfer so ISP congestion is not the reference.
2. Compare integer counter deltas from the **same interface** over a long monotonic window, excluding initial/reset intervals. Record sample timestamps; do not expect two tools with different interval boundaries to show identical momentary values.
3. Compare payload size separately from interface bytes; the difference includes protocol/encapsulation/local traffic. Treat a transfer utility's throughput as a payload reference, not an exact NIC reference.
4. Repeat idle, sustained download/upload, simultaneous transfers, route switches, VPN split routing, reconnect and sleep/resume. Never sum a tunnel with its physical interface as though they were separate application traffic.
5. Run the resource probe with visible/hidden states, then a prolonged normal session with the intended optional metrics/panel/sensors enabled. Preserve raw reports, not just averages.

```powershell
./scripts/Test-Stability.ps1 -Seconds 3600 -StressIterations 10000
```

No universal percentage-accuracy or superiority claim follows from the arithmetic tests. Physical driver/route validation and multi-hour/day observations are still required for such claims.
