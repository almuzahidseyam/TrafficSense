[CmdletBinding()]
param(
    [string]$DotNet='dotnet',
    [switch]$Community,
    [string]$CommunityManifest='',
    [ValidateRange(0,86400)][int]$Seconds=0,
    [switch]$InteractiveSoak,
    [string]$OutputDirectory=(Join-Path $PSScriptRoot '../.artifacts/plugin-interactions')
)
$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
if($Seconds -gt 0 -and (!$Community -or $Seconds -lt 30)){throw 'A soak requires -Community and at least 30 seconds.'}
$repository=Split-Path $PSScriptRoot -Parent
$root=Join-Path ([IO.Path]::GetFullPath($OutputDirectory)) ([Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $root -Force | Out-Null
$profile=Join-Path $repository 'build/Portable.pubxml'
$probe=Join-Path $root 'probe'
& $DotNet publish (Join-Path $repository 'tests/TrafficSense.LifecycleTests/TrafficSense.LifecycleTests.csproj') -c Release "-p:PublishProfileFullPath=$profile" -o $probe --nologo | Out-Host
if($LASTEXITCODE -ne 0){throw 'Interaction probe publish failed.'}
& (Join-Path $PSScriptRoot 'Build-TestPlugin.ps1') -OutputDirectory (Join-Path $root 'fixture') | Out-Host
& (Join-Path $PSScriptRoot 'Build-TestPlugin.ps1') -Launcher -OutputDirectory (Join-Path $root 'launcher') | Out-Host
$oldLauncher=$env:TRAFFICSENSE_TEST_LAUNCHER
$env:TRAFFICSENSE_TEST_LAUNCHER=Join-Path $root 'launcher/launcher.exe'
$exe=Join-Path $probe 'TrafficSense.LifecycleTests.exe'
try {
    foreach($operation in @('--plugin-interactions','--plugin-bridge-ui','--plugin-private-desktop')){
        & $exe $operation (Join-Path $root 'fixture/fixture.dll')
        if($LASTEXITCODE -ne 0){throw "Native interaction check failed: $operation"}
    }
    if($Community){
        if(!$CommunityManifest){$CommunityManifest=& (Join-Path $PSScriptRoot 'Get-CommunityPlugins.ps1')}
        & $exe --community-interactions $CommunityManifest (Join-Path $root 'community-interactions.json')
        if($LASTEXITCODE -ne 0){throw 'Community dialog checks failed.'}
        & $exe --plugin-dialog-resources $CommunityManifest (Join-Path $root 'dialog-resources.json')
        if($LASTEXITCODE -ne 0){throw 'Dialog resource trace failed.'}
        if($Seconds -gt 0){
            $soakOperation=if($InteractiveSoak){'--plugin-soak-interactive'}else{'--plugin-soak'}
            & $exe $soakOperation $CommunityManifest $Seconds (Join-Path $root 'plugin-soak.json')
            if($LASTEXITCODE -ne 0){throw 'Plugin soak failed; inspect its report.'}
        }
    }
} finally { $env:TRAFFICSENSE_TEST_LAUNCHER=$oldLauncher }
Write-Output $root
