# Contributing

Start with a reproducible [bug report or feature request](https://github.com/almuzahidseyam/TrafficSense/issues/new/choose). English and বাংলা are welcome. Check existing issues and [compatibility boundaries](docs/COMPATIBILITY.md) before proposing a feature already covered or intentionally separated.

## Local development

Use Windows, PowerShell and the .NET 8 SDK. Clone the repository and run:

```powershell
dotnet build src/TrafficSense.csproj -c Release
dotnet test tests/TrafficSense.Tests -c Release
dotnet run --project tests/TrafficSense.LifecycleTests -c Release
```

The pure test project links core source files and can run without WPF. Native lifecycle tests use isolated settings/instance names, hidden non-activating windows and injected boundaries for clipboard, startup, power events and appbar reservations. A temporary test tray icon can appear. Tests must never suspend the user's machine, change real adapters/startup, overwrite their clipboard or terminate their running widget. Packaged entry activation is skipped if a real widget owns the instance gate; CI exercises it on a fresh runner.

## Changes and evidence

- Keep native collection outside the UI dispatcher and retain monotonic timing. Exact usage uses integer counter deltas; never derive it from rounded/smoothed displayed speed. Unknown is different from measured zero.
- New settings need normalization, migration, save-failure rollback and backup behavior. Add both English and Bengali strings, accessible names and keyboard behavior where relevant.
- Keep hardware providers outside the base monitor. Bound imported files/HTTP responses, validate freshness and propagate missing data as unavailable.
- Use deterministic tests for real correctness risks: byte conservation, calendar boundaries, reset/reconnect, cancellation and failure cleanup. Add a native UI probe when behavior depends on WPF/Win32; avoid tests that only repeat a property assignment.
- Update the changelog, docs and compatibility matrix for user-visible changes. Distinguish simulation from physical device observations. Do not claim universal exactness, negligible resource use or feature parity without evidence.
- Respect third-party licenses. Our sensor protocol and skins are independently implemented; native TrafficMonitor plugins/skins are not compatible formats.

For packaging and optional measurement:

```powershell
./scripts/Build-Portable.ps1
./scripts/Build-Installer.ps1 -SourceDirectory '<printed portable build>/app' -VerifyInstall
./scripts/Test-Stability.ps1 -Seconds 600 -StressIterations 10000
dotnet run --project tests/TrafficSense.LifecycleTests -c Release -- --screenshots docs/images
```

Screenshot fixtures use **illustrative** data. Render actual controls, label the examples and inspect their output. Build scripts place outputs under `.artifacts`; do not commit generated executables, private logs, user settings or credentials.

## Pull requests and releases

Explain the user problem, resulting behavior and relevant validation. Link supporting issues if they exist. The Windows CI checks release metadata, regression tests, self-contained lifecycle, installer/uninstaller and resource stability. Finite stress iterations are not elapsed hours of operation.

Maintainers update the explicit project version and matching changelog section, then run `scripts/Prepare-Release.ps1 -Tag vX.Y.Z`. The release tag must name the tested merged commit. A `v*` tag triggers the release workflow; verify both ZIP/Setup plus checksums, their embedded version and workflow results before announcing the release. A normal branch/PR build does not publish a release.

Self-contained runtimes must be rebuilt for .NET security servicing. A checksum detects a changed download; it does not replace Authenticode signing. Signing, package-manager distribution and new architectures require separate validated release work.
