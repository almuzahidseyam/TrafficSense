[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$SourceDirectory,
    [string]$Compiler = '',
    [switch]$VerifyInstall,
    [switch]$Lite,
    [string]$DotNet = 'dotnet',
    [string]$AlternateSourceDirectory = ''
)
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$repository = Split-Path $PSScriptRoot -Parent
$source = [IO.Path]::GetFullPath($SourceDirectory)
$release = & (Join-Path $PSScriptRoot 'Prepare-Release.ps1')
$executable = Join-Path $source 'TrafficSense.exe'
if (!(Test-Path -LiteralPath $executable)) { throw 'Build a verified portable package first.' }
if (([Diagnostics.FileVersionInfo]::GetVersionInfo($executable).ProductVersion -split '\+')[0] -ne $release.Version) { throw 'Installer source version mismatch.' }
if (((Get-Content (Join-Path $source 'README.txt') -Raw) -match '^TrafficSense Lite') -ne [bool]$Lite) { throw 'Installer edition does not match the source package.' }
if (!$Compiler) { $Compiler = & (Join-Path $PSScriptRoot 'Get-InnoCompiler.ps1') }
$kind = if ($Lite) { 'installer-lite' } else { 'installer' }
$output = Join-Path $repository ('.artifacts/' + $kind + '/' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $output -Force | Out-Null
$script = Join-Path $repository 'packaging/TrafficSense.iss'
$defines = @()
$testParameters = @{}
if ($Lite) {
    $runtimeRoot = Split-Path (Get-Command $DotNet -ErrorAction Stop).Source -Parent
    $checkDirectory = Join-Path $output 'runtime-check'
    & $DotNet publish (Join-Path $repository 'tools/TrafficSense.RuntimeCheck/TrafficSense.RuntimeCheck.csproj') -c Release "-p:PublishProfileFullPath=$(Join-Path $repository 'build/Lite.pubxml')" -o $checkDirectory --nologo | Out-Host
    if ($LASTEXITCODE -ne 0) { throw 'Runtime checker publish failed.' }
    $defines = @('/DLite', "/DRuntimeCheck=$(Join-Path $checkDirectory 'TrafficSense.RuntimeCheck.exe')")
    $testParameters.RuntimeRoot = $runtimeRoot
}
$outputName = if ($Lite) { 'TrafficSense-Lite-Setup' } else { 'TrafficSense-Setup' }
& $Compiler @defines "/DAppVersion=$($release.Version)" "/DSourceDir=$source" "/DOutputDir=$output" "/DOutputName=$outputName" $script | Out-Host
if ($LASTEXITCODE -ne 0) { throw 'Installer compilation failed.' }
$setup = Join-Path $output "$outputName.exe"
$hash = (Get-FileHash -LiteralPath $setup -Algorithm SHA256).Hash.ToLowerInvariant()
[IO.File]::WriteAllText("$setup.sha256", "$hash  $outputName.exe`n")
if ($VerifyInstall) {
    $id = [Guid]::NewGuid().ToString('N')
    $testId = "TrafficSense.Test.$id"
    & $Compiler @defines "/DAppVersion=$($release.Version)" "/DSourceDir=$source" "/DOutputDir=$output" "/DAppId=$testId" "/DOutputName=TrafficSense-TestSetup" "/DStartupValue=$testId" "/DMutexSuffix=.$id" $script | Out-Host
    if ($LASTEXITCODE -ne 0) { throw 'Isolated test installer compilation failed.' }
    if ($Lite -and $AlternateSourceDirectory) {
        $alternate = [IO.Path]::GetFullPath($AlternateSourceDirectory)
        if (([Diagnostics.FileVersionInfo]::GetVersionInfo((Join-Path $alternate 'TrafficSense.exe')).ProductVersion -split '\+')[0] -ne $release.Version) { throw 'Alternate installer version mismatch.' }
        & $Compiler "/DAppVersion=$($release.Version)" "/DSourceDir=$alternate" "/DOutputDir=$output" "/DAppId=$testId" "/DOutputName=TrafficSense-TestFullSetup" "/DStartupValue=$testId" "/DMutexSuffix=.$id" $script | Out-Host
        if ($LASTEXITCODE -ne 0) { throw 'Alternate installer compilation failed.' }
        $testParameters.AlternateSetupPath = Join-Path $output 'TrafficSense-TestFullSetup.exe'
    }
    & (Join-Path $PSScriptRoot 'Test-Installer.ps1') @testParameters -SetupPath (Join-Path $output 'TrafficSense-TestSetup.exe') -TestId $testId -MutexSuffix $id -SourceDirectory $source | Out-Host
}
[PSCustomObject]@{ SetupPath = $setup; ChecksumPath = "$setup.sha256" }
