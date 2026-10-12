[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$SourceDirectory,
    [string]$Compiler = '',
    [switch]$VerifyInstall
)
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$repository = Split-Path $PSScriptRoot -Parent
$source = [IO.Path]::GetFullPath($SourceDirectory)
$release = & (Join-Path $PSScriptRoot 'Prepare-Release.ps1')
$executable = Join-Path $source 'TrafficSense.exe'
if (!(Test-Path -LiteralPath $executable)) { throw 'Build a verified portable package first.' }
if (([Diagnostics.FileVersionInfo]::GetVersionInfo($executable).ProductVersion -split '\+')[0] -ne $release.Version) { throw 'Installer source version mismatch.' }
if (!$Compiler) { $Compiler = & (Join-Path $PSScriptRoot 'Get-InnoCompiler.ps1') }
$output = Join-Path $repository ('.artifacts/installer/' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $output -Force | Out-Null
$script = Join-Path $repository 'packaging/TrafficSense.iss'
& $Compiler "/DAppVersion=$($release.Version)" "/DSourceDir=$source" "/DOutputDir=$output" $script | Out-Host
if ($LASTEXITCODE -ne 0) { throw 'Installer compilation failed.' }
$setup = Join-Path $output 'TrafficSense-Setup.exe'
$hash = (Get-FileHash -LiteralPath $setup -Algorithm SHA256).Hash.ToLowerInvariant()
[IO.File]::WriteAllText("$setup.sha256", "$hash  TrafficSense-Setup.exe`n")
if ($VerifyInstall) {
    $id = [Guid]::NewGuid().ToString('N')
    $testId = "TrafficSense.Test.$id"
    & $Compiler "/DAppVersion=$($release.Version)" "/DSourceDir=$source" "/DOutputDir=$output" "/DAppId=$testId" "/DOutputName=TrafficSense-TestSetup" "/DStartupValue=$testId" "/DMutexSuffix=.$id" $script | Out-Host
    if ($LASTEXITCODE -ne 0) { throw 'Isolated test installer compilation failed.' }
    & (Join-Path $PSScriptRoot 'Test-Installer.ps1') -SetupPath (Join-Path $output 'TrafficSense-TestSetup.exe') -TestId $testId -MutexSuffix $id -SourceDirectory $source | Out-Host
}
[PSCustomObject]@{ SetupPath = $setup; ChecksumPath = "$setup.sha256" }
