[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$SetupPath,
    [Parameter(Mandatory)][string]$TestId,
    [Parameter(Mandatory)][string]$MutexSuffix,
    [Parameter(Mandatory)][string]$SourceDirectory,
    [string]$RuntimeRoot = '',
    [string]$AlternateSetupPath = ''
)
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
if ($TestId -notmatch '^TrafficSense\.Test\.[a-f0-9]{32}$' -or $MutexSuffix -notmatch '^[a-f0-9]{32}$') { throw 'Only isolated test installers are allowed.' }
$parent = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../.artifacts/installer-tests'))
$root = Join-Path $parent ([Guid]::NewGuid().ToString('N'))
$target = Join-Path $root 'installed app'
New-Item -ItemType Directory -Path $root -Force | Out-Null
$runKey = [Microsoft.Win32.Registry]::CurrentUser.CreateSubKey('Software\Microsoft\Windows\CurrentVersion\Run')
$uninstallKey = "Software\Microsoft\Windows\CurrentVersion\Uninstall\$($TestId)_is1"
$sourceRoot = [IO.Path]::GetFullPath($SourceDirectory).TrimEnd([IO.Path]::DirectorySeparatorChar)
$savedRoot = $env:DOTNET_ROOT
$savedRootX64 = $env:DOTNET_ROOT_X64
function Run-Setup([string]$Executable, [string]$Arguments, [bool]$ExpectedSuccess = $true) {
    $info = New-Object Diagnostics.ProcessStartInfo
    $info.FileName = $Executable; $info.Arguments = $Arguments; $info.UseShellExecute = $false; $info.CreateNoWindow = $true
    $process = [Diagnostics.Process]::Start($info)
    try {
        if (!$process.WaitForExit(90000)) { $process.Kill(); throw 'Installer test timed out.' }
        if (($process.ExitCode -eq 0) -ne $ExpectedSuccess) { throw "Unexpected installer result: $($process.ExitCode)." }
    } finally { $process.Dispose() }
}
function Get-TestUninstaller {
    $key = [Microsoft.Win32.Registry]::CurrentUser.OpenSubKey($uninstallKey)
    if (!$key) { throw 'Test uninstall registration is missing.' }
    try { return ([string]$key.GetValue('UninstallString')).Trim('"') } finally { $key.Dispose() }
}
$arguments = "/CURRENTUSER /VERYSILENT /SUPPRESSMSGBOXES /NORESTART /NOICONS /TASKS=`"`" /DIR=`"$target`" /LOG=`"$root/install.log`""
$stage = 'running-app guard'
try {
    if ($RuntimeRoot) {
        $stage = 'missing Lite runtime guard'
        $fxr = Get-ChildItem (Join-Path $RuntimeRoot 'host/fxr') -Directory |
            Where-Object Name -Match '^8\.0\.\d+$' | Sort-Object { [version]$_.Name } -Descending | Select-Object -First 1
        if (!$fxr) { throw 'No .NET 8 hostfxr for isolated prerequisite tests.' }
        $missingRoot = Join-Path $root 'without-runtime'
        $hostTarget = Join-Path $missingRoot "host/fxr/$($fxr.Name)"
        New-Item -ItemType Directory -Path $hostTarget -Force | Out-Null
        Copy-Item (Join-Path $fxr.FullName 'hostfxr.dll') $hostTarget
        $env:DOTNET_ROOT = $missingRoot; $env:DOTNET_ROOT_X64 = $missingRoot
        Run-Setup $SetupPath $arguments $false
        if ((Test-Path (Join-Path $target 'TrafficSense.exe')) -or $null -ne [Microsoft.Win32.Registry]::CurrentUser.OpenSubKey($uninstallKey)) { throw 'Missing-runtime installer modified the app.' }
        if ((Get-Content (Join-Path $root 'install.log') -Raw) -notmatch 'TrafficSense Lite needs .NET 8 Desktop Runtime') { throw 'Missing-runtime explanation was not logged.' }
        $stage = 'core runtime without Desktop guard'
        New-Item -ItemType Directory -Path (Join-Path $missingRoot 'shared') -Force | Out-Null
        Copy-Item -LiteralPath (Join-Path $RuntimeRoot 'shared/Microsoft.NETCore.App') -Destination (Join-Path $missingRoot 'shared') -Recurse
        Run-Setup $SetupPath $arguments $false
        if (Test-Path (Join-Path $target 'TrafficSense.exe')) { throw 'Plain runtime was incorrectly accepted as Desktop Runtime.' }
        $env:DOTNET_ROOT = $RuntimeRoot; $env:DOTNET_ROOT_X64 = $RuntimeRoot
    }
    $mutex = [Threading.Mutex]::new($false, ("Local\TrafficSense.Installer." + [Environment]::UserName.ToLowerInvariant() + ".$MutexSuffix"))
    try { Run-Setup $SetupPath $arguments $false } finally { $mutex.Dispose() }
    if (Test-Path -LiteralPath (Join-Path $target 'TrafficSense.exe')) { throw 'Installer ignored the running-app mutex.' }
    $stage = 'first install'
    Run-Setup $SetupPath $arguments
    foreach ($file in Get-ChildItem -LiteralPath $SourceDirectory -Recurse -File) {
        $relative = $file.FullName.Substring($sourceRoot.Length + 1)
        $installed = Join-Path $target $relative
        if (!(Test-Path -LiteralPath $installed) -or (Get-FileHash -LiteralPath $file.FullName).Hash -ne (Get-FileHash -LiteralPath $installed).Hash) { throw "Installed payload mismatch: $relative" }
    }
    $registry = [Microsoft.Win32.Registry]::CurrentUser.OpenSubKey($uninstallKey)
    if (!$registry) { throw 'Uninstall registration is missing.' }
    $uninstallCommand = [string]$registry.GetValue('UninstallString')
    $registry.Dispose()
    $uninstaller = $uninstallCommand.Trim('"')
    if (!(Test-Path -LiteralPath $uninstaller)) { throw "Registered uninstaller is missing: $uninstaller" }
    # A repair/upgrade must preserve user-created files and never launch the app silently.
    $sentinel = Join-Path $target 'keep-user-data.txt'
    [IO.File]::WriteAllText($sentinel, 'preserve')
    if ($RuntimeRoot) {
        $stage = 'blocked Lite upgrade preserves installed files'
        $beforeHash = (Get-FileHash (Join-Path $target 'TrafficSense.exe')).Hash
        $env:DOTNET_ROOT = $missingRoot; $env:DOTNET_ROOT_X64 = $missingRoot
        Run-Setup $SetupPath $arguments $false
        if ((Get-FileHash (Join-Path $target 'TrafficSense.exe')).Hash -ne $beforeHash -or !(Test-Path $sentinel)) { throw 'Blocked upgrade changed installed data.' }
        $env:DOTNET_ROOT = $RuntimeRoot; $env:DOTNET_ROOT_X64 = $RuntimeRoot
    }
    if ($AlternateSetupPath) {
        $stage = 'Lite to full and back'
        Run-Setup $AlternateSetupPath $arguments
        if ((Get-FileHash (Join-Path $target 'TrafficSense.exe')).Hash -eq (Get-FileHash (Join-Path $sourceRoot 'TrafficSense.exe')).Hash) { throw 'Full edition did not replace Lite.' }
        Run-Setup $SetupPath $arguments
        if ((Get-FileHash (Join-Path $target 'TrafficSense.exe')).Hash -ne (Get-FileHash (Join-Path $sourceRoot 'TrafficSense.exe')).Hash) { throw 'Lite did not replace full edition.' }
    }
    $stage = 'repair/upgrade'
    Run-Setup $SetupPath $arguments
    if ((Get-Content -LiteralPath $sentinel -Raw) -ne 'preserve') { throw 'Upgrade removed user data.' }
    $runKey.SetValue($TestId, ('"' + (Join-Path $target 'TrafficSense.exe') + '"'))
    $stage = 'first uninstall'
    $uninstaller = Get-TestUninstaller
    Run-Setup $uninstaller '/VERYSILENT /SUPPRESSMSGBOXES /NORESTART'
    if ($null -ne $runKey.GetValue($TestId, $null)) { throw 'Uninstall retained its startup registration.' }
    if (Test-Path -LiteralPath (Join-Path $target 'TrafficSense.exe')) { throw 'Uninstall retained the executable.' }
    if (!(Test-Path -LiteralPath $sentinel)) { throw 'Uninstall removed user data.' }
    $stage = 'second install'
    Run-Setup $SetupPath $arguments
    $otherCommand = '"C:\Other TrafficSense\TrafficSense.exe"'
    $runKey.SetValue($TestId, $otherCommand)
    $stage = 'second uninstall'
    $uninstaller = Get-TestUninstaller
    Run-Setup $uninstaller '/VERYSILENT /SUPPRESSMSGBOXES /NORESTART'
    if ($runKey.GetValue($TestId) -ne $otherCommand) { throw 'Uninstall removed the startup registration of another copy.' }
    Write-Host 'PASS: isolated per-user install, in-use guard, payload hashes, upgrade, uninstall registration, startup ownership and retained user data.'
    if ($RuntimeRoot) { Write-Host 'PASS: Lite rejects missing/plain-only runtime, accepts Desktop x64 and preserves installed payload on blocked upgrade.' }
    if ($AlternateSetupPath) { Write-Host 'PASS: Lite/full/Lite switching shares one isolated installation and retains user data.' }
} catch {
    Write-Host "Installer test failed during $stage."
    Get-ChildItem -LiteralPath $target -ErrorAction SilentlyContinue | Select-Object -ExpandProperty Name | Out-Host
    if (Test-Path -LiteralPath (Join-Path $root 'install.log')) {
        Copy-Item -LiteralPath (Join-Path $root 'install.log') -Destination (Join-Path (Split-Path $SetupPath -Parent) 'installer-test-failure.log') -Force
    }
    throw
} finally {
    $env:DOTNET_ROOT = $savedRoot; $env:DOTNET_ROOT_X64 = $savedRootX64
    $runKey.DeleteValue($TestId, $false); $runKey.Dispose()
    $remaining = [Microsoft.Win32.Registry]::CurrentUser.OpenSubKey($uninstallKey)
    if ($remaining) {
        $cleanupUninstaller = ([string]$remaining.GetValue('UninstallString')).Trim('"')
        $remaining.Dispose()
        if (Test-Path -LiteralPath $cleanupUninstaller) { Run-Setup $cleanupUninstaller '/VERYSILENT /SUPPRESSMSGBOXES /NORESTART' }
        else { [Microsoft.Win32.Registry]::CurrentUser.DeleteSubKeyTree($uninstallKey, $false) }
    }
    $resolved = [IO.Path]::GetFullPath($root)
    if (!$resolved.StartsWith($parent + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) { throw 'Unsafe installer-test cleanup target.' }
    Remove-Item -LiteralPath $resolved -Recurse -Force
}
