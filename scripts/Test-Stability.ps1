[CmdletBinding()]
param(
    [ValidateRange(30, 86400)][int]$Seconds = 600,
    [ValidateRange(0, 100000)][int]$StressIterations = 3000,
    [string]$DotNet = 'dotnet',
    [string]$OutputDirectory = (Join-Path $PSScriptRoot '../.artifacts/stability')
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$repository = Split-Path $PSScriptRoot -Parent
$profile = Join-Path $repository 'build/Portable.pubxml'
$buildDirectory = Join-Path ([IO.Path]::GetFullPath($OutputDirectory)) ([Guid]::NewGuid().ToString('N'))
$publishDirectory = Join-Path $buildDirectory 'probe'
$reportPath = Join-Path $buildDirectory 'stability.json'
& $DotNet publish (Join-Path $repository 'tests/TrafficSense.LifecycleTests/TrafficSense.LifecycleTests.csproj') `
    -c Release "-p:PublishProfileFullPath=$profile" -o $publishDirectory --nologo | Out-Host
if ($LASTEXITCODE -ne 0) { throw 'Could not publish the stability probe.' }

$start = New-Object System.Diagnostics.ProcessStartInfo
$start.FileName = Join-Path $publishDirectory 'TrafficSense.LifecycleTests.exe'
$start.Arguments = "--soak --seconds $Seconds --iterations $StressIterations --report `"$reportPath`""
$start.UseShellExecute = $false
$start.CreateNoWindow = $true
$start.RedirectStandardOutput = $true
$start.RedirectStandardError = $true
$start.EnvironmentVariables['DOTNET_ROOT'] = Join-Path $buildDirectory 'no-installed-runtime'
$start.EnvironmentVariables['DOTNET_ROOT_X64'] = $start.EnvironmentVariables['DOTNET_ROOT']
$start.EnvironmentVariables['DOTNET_MULTILEVEL_LOOKUP'] = '0'
$start.EnvironmentVariables['DOTNET_BUNDLE_EXTRACT_BASE_DIR'] = Join-Path $buildDirectory 'bundled-runtime'
$start.EnvironmentVariables['TRAFFICSENSE_VERIFY_BUNDLE'] = '1'
foreach ($name in @('DOTNET_STARTUP_HOOKS', 'DOTNET_ADDITIONAL_DEPS', 'DOTNET_SHARED_STORE')) {
    $start.EnvironmentVariables.Remove($name)
}

$process = [Diagnostics.Process]::Start($start)
try {
    $stdout = $process.StandardOutput.ReadToEndAsync()
    $stderr = $process.StandardError.ReadToEndAsync()
    $watch = [Diagnostics.Stopwatch]::StartNew()
    $deadline = $Seconds + 20 + [Math]::Max(120, $StressIterations / 10)
    $nextProgress = 30
    while (!$process.WaitForExit(1000)) {
        if ($watch.Elapsed.TotalSeconds -gt $deadline) {
            $process.Kill()
            throw 'Stability probe timed out; the UI may be unresponsive.'
        }
        if ($watch.Elapsed.TotalSeconds -ge $nextProgress) {
            $process.Refresh()
            Write-Host ("Stability probe: {0:N0} s elapsed, working set {1:N1} MiB, private memory {2:N1} MiB." -f `
                $watch.Elapsed.TotalSeconds, ($process.WorkingSet64 / 1MB), ($process.PrivateMemorySize64 / 1MB))
            $nextProgress += 60
        }
    }
    $output = $stdout.GetAwaiter().GetResult()
    $errors = $stderr.GetAwaiter().GetResult()
    if ($output) { Write-Host $output.TrimEnd() }
    if ($errors) { Write-Host $errors.TrimEnd() }
    if ($process.ExitCode -ne 0) { throw "Stability probe failed ($($process.ExitCode)); report: $reportPath" }
    Write-Output $reportPath
} finally { $process.Dispose() }
