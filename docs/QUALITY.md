# Quality evidence and review

Scope: independent TrafficSense improvements from 1.22.0 through 1.27.0. Reviewed on 2026-10-13 for correctness, resource use, input boundaries, lifecycle cleanup and maintainability. This is a development record, not certification of every Windows/driver combination.

## Review summary

The network sampler now separates collection from rendering, carries actual native read times, buffers exact observed usage and distinguishes unknown intervals. Optional native metrics and external hardware providers have separate lifecycles. Imported data is bounded and appearance imports cannot change network/history preferences. No unresolved critical defect was identified in the reviewed paths; the limitations below remain explicit.

| Finding | Severity | Resolution / evidence |
| --- | --- | --- |
| UI-bound collection could delay observations | High | Serialized background sampler; tests block delivery while collection continues, reject stale generations and drain bytes once |
| Enumeration delay could bias the rate denominator | High | Per-adapter native-read timestamps; jitter traces and latency-bias tests |
| Lifetime/rollback or reconnect counters could generate false spikes | High | Integer subtraction, baseline/reset guards, negative/duplicate rejection, power/reconnect probes |
| Extreme skin version could escape import error handling | Medium | Typed envelope validation with bounded integer parsing; malformed/huge-version unit and native rollback tests |
| Notification shell failure could interrupt monitoring | Medium | Expected notification failures logged without propagating into sampling; throwing-notification native probe |
| Unknown intervals appeared as measured zero in recent graphs | Medium | Explicit sample validity, broken paths, unavailable legend and native geometry checks |
| Unchanged stale worker timestamp could leave history's latest legend fresh | Medium | Unknown point uses consumption time when delayed; deterministic stalled-delivery native test |
| Provider outages/cached files could show stale sensor data | Medium | Bounded loopback HTTP, cancellation, numeric schema validation, atomic publication and reader/UI freshness checks |

Suggestions still open: simplify the growing context menu into grouped settings, validate physical shell/driver combinations, add optional-metric/panel long-duration measurements, plan runtime servicing and signed/ARM64 distribution. These are follow-up work, not hidden claims of completed parity.

## What is verified

- **291 pure regression cases** at this release: exact bytes, timing, counter resets, adapter selection, preference migration/backup, range filters, daily series, clipboard/export formatting, threshold calendars, skins, sensor files and LibreHardwareMonitor schema. Graph retention covers the full two minutes even at 250 ms.
- **Synthetic timing characterization:** 4,096 samples at each of four intervals; known-rate relative error ≤10⁻¹², byte conservation beyond 4 GiB, tiny deltas at large lifetime values. See [methodology](MEASUREMENT.md).
- **Real WPF/process probes:** bilingual live controls, history/chart/table selection, clipboard boundary, graph path gaps, appearance and restart, native counters, taskbar messages with a simulated shell, sensor bridge entry/cancellation, update errors, tray/duplicate activation and fatal/normal cleanup.
- **Native compatibility probes:** independently authored compiler-built DLL fixtures exercise retained host callbacks, legacy GDI and extended drawing, options/commands, startup failures, crash/hang deadlines and kernel-job cleanup after an abrupt owner exit. Actual WPF checks cover layout positions, PNG alpha, managed copies, compact layout, invalid/save rollback and backup/restore. Child attachment is exercised against an isolated owned native window; Explorer is only inspected read-only locally.
- **Distribution checks:** self-contained ZIP extraction/content/runtime checks, per-user install/uninstall, version/changelog/tag alignment and SHA-256 files. Public release binaries are downloaded and checked against checksums/metadata before release completion is reported.
- **Resource regression:** CI warms up for 20 seconds, then measures 60 seconds across shown/hidden states and performs 1,500 accelerated stress iterations. Reports include CPU, managed/private/working-set memory, native handles, ticks and UI delays. CPU is recorded rather than gated by a machine-specific percentage.
- **Dependency advisory check:** the configured NuGet sources reported no vulnerable packages for the app's direct/transitive package graph at review time. This is not a complete security audit and must be rerun as advisories change.

Retention checks record ordinary collection snapshots and separately use the existing aggressive test-only collection to reclaim unused managed regions; the production app never forces garbage collection. The same regression budgets apply. Accelerated stress is not equivalent to hours/days of elapsed use, and the default soak does not exercise every optional feature combination.

## What still needs physical evidence

- Windows 10/11 Explorer restart, fullscreen, monitor removal and mixed-DPI behavior with real appbar reservations. Automated shell registration is simulated to preserve the user's desktop.
- Long-duration runs with optional GPU/disk counters, panel and sensor bridge enabled, on multiple devices.
- Controlled same-interface traffic comparisons across NIC drivers and VPN/split-route arrangements. No “most accurate” claim is made from arithmetic tests alone.
- Actual sensor-provider versions/hardware coverage. Included tests use realistic schema fixtures and cancellation boundaries; they install no hardware driver or real provider.
- Full screen-reader usability and non-English/Bengali languages, native ARM64/x86, signed packages and a larger real community-plugin/legacy-skin compatibility corpus.

## Verdict

The reviewed implementation is ready for the documented Windows x64 feature scope after its release workflows pass. Hardware/OS matrix coverage and the compatibility gaps remain follow-up requirements; passing tests does not establish universal reliability or competitive popularity.
