[CmdletBinding()]
param([string]$ToolsDirectory = (Join-Path $PSScriptRoot '../.artifacts/tools'))
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$version = '7.1.0'
$directory = Join-Path ([IO.Path]::GetFullPath($ToolsDirectory)) "inno-$version"
$compiler = Join-Path $directory 'ISCC.exe'
if (Test-Path -LiteralPath $compiler) { return $compiler }
New-Item -ItemType Directory -Path $directory -Force | Out-Null
$download = Join-Path $directory 'compiler-setup.exe'
$url = 'https://github.com/jrsoftware/issrc/releases/download/is-7_1_0/innosetup-7.1.0-x64.exe'
Invoke-WebRequest -Uri $url -OutFile $download -UseBasicParsing
$expected = '0362a383ed217d4c4239b5933866dd96d3eb2102737da92f80f6057a4b40df2f'
if ((Get-FileHash -LiteralPath $download -Algorithm SHA256).Hash.ToLowerInvariant() -ne $expected) { throw 'Inno Setup compiler checksum mismatch.' }
$start = New-Object Diagnostics.ProcessStartInfo
$start.FileName = $download
$start.Arguments = "/PORTABLE=1 /CURRENTUSER /VERYSILENT /SUPPRESSMSGBOXES /NORESTART /NOICONS /DIR=`"$directory`""
$start.UseShellExecute = $false
$start.CreateNoWindow = $true
$process = [Diagnostics.Process]::Start($start)
try {
    if (!$process.WaitForExit(90000)) { $process.Kill(); throw 'Compiler extraction timed out.' }
    if ($process.ExitCode -ne 0) { throw "Compiler extraction failed ($($process.ExitCode))." }
} finally { $process.Dispose() }
if (!(Test-Path -LiteralPath $compiler)) { throw 'Compiler was not extracted.' }
return $compiler
