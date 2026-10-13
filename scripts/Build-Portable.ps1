[CmdletBinding()]
param(
    [string]$DotNet = 'dotnet',
    [string]$OutputDirectory = '',
    [switch]$Lite
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$repository = Split-Path $PSScriptRoot -Parent
$profile = Join-Path $repository $(if ($Lite) { 'build/Lite.pubxml' } else { 'build/Portable.pubxml' })
if (!$OutputDirectory) { $OutputDirectory = Join-Path $repository $(if ($Lite) { '.artifacts/lite' } else { '.artifacts/portable' }) }
$runtimeRoot = ''
if ($Lite) {
    $runtimeRoot = Split-Path (Get-Command $DotNet -ErrorAction Stop).Source -Parent
    $runtimes = & $DotNet --list-runtimes
    if ($LASTEXITCODE -ne 0 -or !($runtimes -match '^Microsoft.WindowsDesktop.App 8\.0\.')) {
        throw 'Lite verification requires .NET 8 Desktop Runtime x64 beside the selected dotnet executable.'
    }
}
# A fresh directory prevents old DLLs or a running preview from contaminating the ZIP.
$buildDirectory = Join-Path ([IO.Path]::GetFullPath($OutputDirectory)) ([Guid]::NewGuid().ToString('N'))
$applicationDirectory = Join-Path $buildDirectory 'app'
$probeDirectory = Join-Path $buildDirectory 'probe'
New-Item -ItemType Directory -Path $buildDirectory -Force | Out-Null
$release = & (Join-Path $PSScriptRoot 'Prepare-Release.ps1') -OutputPath (Join-Path $buildDirectory 'RELEASE-NOTES.md')

function Publish-Portable([string]$Project, [string]$Destination) {
    & $DotNet publish (Join-Path $repository $Project) -c Release "-p:PublishProfileFullPath=$profile" -o $Destination --nologo | Out-Host
    if ($LASTEXITCODE -ne 0) { throw "Portable publish failed: $Project" }
}

Publish-Portable 'src/TrafficSense.csproj' $applicationDirectory
$metadata = [Diagnostics.FileVersionInfo]::GetVersionInfo((Join-Path $applicationDirectory 'TrafficSense.exe'))
if (($metadata.ProductVersion -split '\+')[0] -ne $release.Version -or $metadata.FileVersion -ne "$($release.Version).0") {
    throw "Published executable version does not match $($release.Version)."
}
Copy-Item -LiteralPath (Join-Path $repository 'LICENSE') -Destination (Join-Path $applicationDirectory 'TrafficSense-LICENSE.txt')
Copy-Item -LiteralPath (Join-Path $repository 'CHANGELOG.md') -Destination $applicationDirectory
Copy-Item -LiteralPath $release.NotesPath -Destination $applicationDirectory
$readme = if ($Lite) { 'packaging/README-Lite.txt' } else { 'packaging/README.txt' }
Copy-Item -LiteralPath (Join-Path $repository $readme) -Destination (Join-Path $applicationDirectory 'README.txt')
New-Item -ItemType Directory -Path (Join-Path $applicationDirectory 'licenses') -Force | Out-Null
if ($Lite) {
    # The small native .NET apphost is still redistributed in Lite.
    Copy-Item -LiteralPath (Join-Path $runtimeRoot 'LICENSE.txt') -Destination (Join-Path $applicationDirectory 'licenses/Microsoft.NET-LICENSE.txt')
    Copy-Item -LiteralPath (Join-Path $runtimeRoot 'ThirdPartyNotices.txt') -Destination (Join-Path $applicationDirectory 'licenses/Microsoft.NET-ThirdPartyNotices.txt')
}
Get-ChildItem -LiteralPath (Join-Path $repository 'packaging/licenses') -File |
    Copy-Item -Destination (Join-Path $applicationDirectory 'licenses')

$archiveName = if ($Lite) { 'TrafficSense-Lite-win-x64.zip' } else { 'TrafficSense.zip' }
$archive = Join-Path $buildDirectory $archiveName
Compress-Archive -Path (Join-Path $applicationDirectory '*') -DestinationPath $archive -CompressionLevel Optimal
$checksum = (Get-FileHash -LiteralPath $archive -Algorithm SHA256).Hash.ToLowerInvariant()
[IO.File]::WriteAllText("$archive.sha256", "$checksum  $archiveName`n")

# Verify the files actually distributed, from a path containing spaces.
$extracted = Join-Path $buildDirectory 'extracted package'
Expand-Archive -LiteralPath $archive -DestinationPath $extracted
$originalFiles = @(Get-ChildItem -LiteralPath $applicationDirectory -Recurse -File)
$extractedFiles = @(Get-ChildItem -LiteralPath $extracted -Recurse -File)
if ($originalFiles.Count -ne $extractedFiles.Count) { throw 'ZIP file inventory differs from publish output.' }
foreach ($file in $originalFiles) {
    $relative = $file.FullName.Substring($applicationDirectory.Length + 1)
    $copy = Join-Path $extracted $relative
    if (!(Test-Path -LiteralPath $copy -PathType Leaf) -or
        (Get-FileHash -LiteralPath $file.FullName).Hash -ne (Get-FileHash -LiteralPath $copy).Hash) {
        throw "ZIP content differs: $relative"
    }
}
if (!(Test-Path -LiteralPath (Join-Path $extracted 'TrafficSense.exe'))) { throw 'ZIP has no executable.' }
if (Get-ChildItem -LiteralPath $extracted -Filter '*.dll') { throw 'The executable still depends on loose DLLs.' }

# Use the same publish profile for a probe that exercises the actual App, WPF,
# tray, language, restart and single-instance code with temporary settings.
Publish-Portable 'tests/TrafficSense.LifecycleTests/TrafficSense.LifecycleTests.csproj' $probeDirectory
$fixture = & (Join-Path $PSScriptRoot 'Build-TestPlugin.ps1') -OutputDirectory (Join-Path $buildDirectory 'plugin-fixture')
& (Join-Path $PSScriptRoot 'Build-TestPlugin.ps1') -Launcher -OutputDirectory (Join-Path $buildDirectory 'plugin-launcher') | Out-Host
$start = New-Object System.Diagnostics.ProcessStartInfo
$start.FileName = Join-Path $probeDirectory 'TrafficSense.LifecycleTests.exe'
$start.WorkingDirectory = $extracted
$start.UseShellExecute = $false
$start.CreateNoWindow = $true
$start.RedirectStandardOutput = $true
$start.RedirectStandardError = $true
$start.EnvironmentVariables['DOTNET_ROOT'] = if ($Lite) { $runtimeRoot } else { Join-Path $buildDirectory 'no-installed-runtime' }
$start.EnvironmentVariables['DOTNET_ROOT_X64'] = $start.EnvironmentVariables['DOTNET_ROOT']
$start.EnvironmentVariables['DOTNET_MULTILEVEL_LOOKUP'] = '0'
$start.EnvironmentVariables['DOTNET_BUNDLE_EXTRACT_BASE_DIR'] = Join-Path $buildDirectory 'bundled-runtime'
$start.EnvironmentVariables['TRAFFICSENSE_VERIFY_BUNDLE'] = if ($Lite) { '0' } else { '1' }
$start.EnvironmentVariables['TRAFFICSENSE_SHARED_RUNTIME'] = $runtimeRoot
$start.EnvironmentVariables['TRAFFICSENSE_EXPECTED_VERSION'] = $release.Version
$start.EnvironmentVariables['TRAFFICSENSE_PACKAGE_EXE'] = Join-Path $extracted 'TrafficSense.exe'
$start.EnvironmentVariables['TRAFFICSENSE_PLUGIN_FIXTURE'] = Join-Path $buildDirectory 'plugin-fixture/fixture.dll'
$start.EnvironmentVariables['TRAFFICSENSE_TEST_LAUNCHER'] = Join-Path $buildDirectory 'plugin-launcher/launcher.exe'
# Do not inherit developer diagnostics or startup hooks in a distribution test.
foreach ($name in @('DOTNET_STARTUP_HOOKS', 'DOTNET_ADDITIONAL_DEPS', 'DOTNET_SHARED_STORE')) {
    $start.EnvironmentVariables.Remove($name)
}
$process = [Diagnostics.Process]::Start($start)
try {
    $stdout = $process.StandardOutput.ReadToEndAsync()
    $stderr = $process.StandardError.ReadToEndAsync()
    # The complete suite starts many isolated workers and deliberately spends
    # seven seconds testing cold startup/hang deadlines. Keep an aggregate
    # watchdog with CI headroom; individual production request limits are unchanged.
    $completed = $process.WaitForExit(120000)
    if (!$completed) {
        $process.Kill()
        $null = $process.WaitForExit(5000)
    }
    # Preserve completed probe diagnostics on failure, without waiting forever
    # for redirected streams if a descendant unexpectedly retained a pipe.
    if (!$stdout.Wait(5000) -or !$stderr.Wait(5000)) { throw 'Portable lifecycle output did not close after termination.' }
    $output = $stdout.GetAwaiter().GetResult()
    $errors = $stderr.GetAwaiter().GetResult()
    if ($output) { Write-Host $output.TrimEnd() }
    if ($errors) { Write-Host $errors.TrimEnd() }
    if (!$completed) { throw 'Portable lifecycle verification timed out after 120 seconds.' }
    if ($process.ExitCode -ne 0) { throw "Portable lifecycle verification failed ($($process.ExitCode))." }
} finally { $process.Dispose() }

if ($Lite) {
    # A valid hostfxr installation without shared frameworks prevents fallback
    # to a developer's globally installed runtime. This is a private test root.
    $emptyRuntime = Join-Path $buildDirectory 'runtime-without-frameworks'
    $fxr = Get-ChildItem (Join-Path $runtimeRoot 'host/fxr') -Directory |
        Where-Object Name -Match '^8\.0\.\d+$' | Sort-Object { [version]$_.Name } -Descending | Select-Object -First 1
    if (!$fxr) { throw 'No .NET 8 hostfxr available for the missing-runtime check.' }
    $fxrTarget = Join-Path $emptyRuntime "host/fxr/$($fxr.Name)"
    New-Item -ItemType Directory -Path $fxrTarget -Force | Out-Null
    Copy-Item (Join-Path $fxr.FullName 'hostfxr.dll') $fxrTarget
    $missing = New-Object Diagnostics.ProcessStartInfo
    $missing.FileName = Join-Path $extracted 'TrafficSense.exe'
    $missing.Arguments = '--plugin-worker missing-test-plugin.dll'
    $missing.UseShellExecute = $false; $missing.CreateNoWindow = $true
    $missing.RedirectStandardOutput = $true; $missing.RedirectStandardError = $true
    $missing.EnvironmentVariables['DOTNET_ROOT_X64'] = $emptyRuntime
    $missing.EnvironmentVariables['DOTNET_ROOT'] = $emptyRuntime
    $missing.EnvironmentVariables['DOTNET_DISABLE_GUI_ERRORS'] = '1'
    $child = [Diagnostics.Process]::Start($missing)
    try {
        $outTask = $child.StandardOutput.ReadToEndAsync(); $errTask = $child.StandardError.ReadToEndAsync()
        if (!$child.WaitForExit(15000)) { $child.Kill(); throw 'Missing-runtime check timed out.' }
        $null = $outTask.GetAwaiter().GetResult(); $errorText = $errTask.GetAwaiter().GetResult()
        if ($child.ExitCode -ne -2147450730 -or $errorText -notmatch 'Microsoft.NETCore.App|Microsoft.WindowsDesktop.App') {
            throw "Lite did not report a missing shared framework: exit $($child.ExitCode). $errorText"
        }
        Write-Host 'PASS: Lite reports a missing shared runtime without starting the application.'
    } finally { $child.Dispose() }
}

Write-Host "Verified portable package: $archive"
[PSCustomObject]@{
    ArchivePath = $archive
    ChecksumPath = "$archive.sha256"
    ExecutablePath = Join-Path $extracted 'TrafficSense.exe'
}
