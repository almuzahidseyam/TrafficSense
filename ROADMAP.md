# TrafficSense development programme

Comparison reviewed on 2026-10-13 against the [TrafficMonitor README](https://github.com/zhongyang219/TrafficMonitor/blob/master/README_en-us.md). Implementations are independent; upstream code and skins are not copied. Popularity is an outcome of usefulness, distribution and maintenance, not a release acceptance criterion.

| Stage | Work | Acceptance evidence | Status |
| --- | --- | --- | --- |
| 1 | Background sampling, actual counter timestamps, 250 ms option, IPv6 route fallback, plain text/CSV clipboard | Deterministic counter/concurrency/midnight tests; Windows recovery, clipboard and resource probes | Implemented in 1.22.0 |
| 2 | Optional CPU, RAM, CPU frequency, GPU engine load and disk activity; network details | Native API failures show unavailable; disabled metrics stop polling; unit and isolated Windows probes | Implemented in 1.23.0 |
| 3 | Taskbar-edge appbar, monitor selection, Explorer restart/DPI recovery, independent visibility | Simulated shell boundary and native WPF lifecycle checks; Windows-supported API; in-taskbar embedding remains a gap | Implemented in 1.24.0 with compatibility limits |
| 4 | Font/item colors, appearance skins, daily/monthly alerts, data-only sensor extensions and separate LHM bridge | Settings migration/rollback, threshold/calendar, file bounds/freshness, provider and isolated UI/cancellation checks | Implemented in 1.25.0 |
| 5 | Product documentation, demo, issue/contribution templates and release quality | Reproducible packages/checksums, clean CI and honest compatibility matrix | Pending |

## Measurement contract

TrafficSense measures **Windows interface byte-counter deltas divided by the actual monotonic elapsed time between reads**. The displayed number is an interval average, not an instantaneous measurement or an ISP capacity/speed test. Driver updates/offload, protocol overhead, local network transfers and VPN encapsulation can differ from application payload bytes. Auto selects one routed adapter and never adds a VPN to its underlying interface. Split routing and concurrent adapters require deliberate selection; Auto does not promise all-application Internet attribution.

Production counter sampling runs on a serialized background timer, independently of WPF rendering. Slow reads cannot overlap and do not hold the delivery lock. Byte totals accumulate by local sample day and drain once; the latest speed replaces older display samples. Native-read timestamps avoid bias from other adapters' enumeration time. Changing a route/adapter, reconnecting, resuming, invalid counters or a gap over five seconds establishes a new baseline. Traffic during these unobservable intervals, while the app is closed, or before its first baseline is deliberately not invented. A sampled interval crossing midnight is assigned to its ending local day, not split using an assumed traffic distribution.

The 250 ms choice improves responsiveness where the driver updates counters that frequently. It can show bursts/zeros when the driver publishes less frequently and uses more CPU than 1 s. No smoothing, artificial speed multiplier or bandwidth clamp is applied. Unavailable/delayed/baseline status is exposed in the widget tooltip. Absolute accuracy across all NIC drivers or superiority over another monitor requires comparative measurements; automated arithmetic tests alone do not establish that claim.

## Priority rationale

Scores use `(impact + risk) × (6 − effort)`, with each input from 1 to 5; effort estimates are relative, not deadlines.

| Debt | Type | Impact | Risk | Effort | Score | Reason |
| --- | --- | --- | --- | --- | --- | --- |
| UI-bound counter collection | Architecture/reliability | 5 | 4 | 3 | 27 | Busy UI can delay measurements and drop unobserved traffic |
| Counter-read timing bias | Correctness | 4 | 3 | 2 | 28 | Enumeration latency must not become the rate denominator |
| Missing optional system metrics | Product/code | 4 | 2 | 3 | 18 | Consolidates common monitoring tasks without mandatory driver overhead |
| Shell taskbar compatibility | Infrastructure | 4 | 4 | 5 | 8 | Windows shell internals change; recovery and tested support matter |
| Missing comparison/support docs | Documentation | 3 | 2 | 1 | 25 | Helps users install, understand measurements and report reproducible bugs |

Temperature monitoring and arbitrary native DLL plugins need a separate opt-in safety/compatibility design. The upstream project itself documents hardware-monitoring stability concerns; driver installation is not part of the base monitor. An extension must not turn an absent sensor into a fabricated zero.
