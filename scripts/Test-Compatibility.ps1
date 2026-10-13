[CmdletBinding()]
param(
    [string]$DotNet='dotnet',
    [switch]$Community,
    [switch]$LiveNetwork,
    [string]$CommunityManifest='',
    [string]$OutputDirectory=(Join-Path $PSScriptRoot '../.artifacts/compatibility')
)
$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$repository=Split-Path $PSScriptRoot -Parent
$root=Join-Path ([IO.Path]::GetFullPath($OutputDirectory)) ([Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $root -Force | Out-Null
$profile=Join-Path $repository 'build/Portable.pubxml'
$probe=Join-Path $root 'probe'
& $DotNet publish (Join-Path $repository 'tests/TrafficSense.LifecycleTests/TrafficSense.LifecycleTests.csproj') -c Release "-p:PublishProfileFullPath=$profile" -o $probe --nologo | Out-Host
if($LASTEXITCODE -ne 0){throw 'Compatibility probe publish failed.'}
& (Join-Path $PSScriptRoot 'Build-TestPlugin.ps1') -OutputDirectory (Join-Path $root 'fixture') | Out-Host
$exe=Join-Path $probe 'TrafficSense.LifecycleTests.exe'
& $exe --display-topology (Join-Path $root 'display-topology.json')
if($LASTEXITCODE -ne 0){throw 'Display topology probe failed.'}
& $exe --plugin-probe (Join-Path $root 'fixture/fixture.dll')
if($LASTEXITCODE -ne 0){throw 'Native plugin fixture failed.'}
if($Community){
    if(!$CommunityManifest){$CommunityManifest=& (Join-Path $PSScriptRoot 'Get-CommunityPlugins.ps1')}
    & $exe --community-plugins $CommunityManifest (Join-Path $root 'community-plugins.json')
    if($LASTEXITCODE -ne 0){throw 'Community plugin probe failed; inspect its report.'}
}
if($LiveNetwork){
    & $exe --vpn-environment (Join-Path $root 'fixture/fixture.dll') (Join-Path $root 'vpn-environment.json')
    if($LASTEXITCODE -ne 0){throw 'Live network probe failed or environment changed; do not label this run passed.'}
}
Write-Output $root
