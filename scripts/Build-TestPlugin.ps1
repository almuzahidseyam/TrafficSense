[CmdletBinding()]
param([string]$OutputDirectory = (Join-Path $PSScriptRoot '../.artifacts/plugin-fixture'), [switch]$Launcher)
$ErrorActionPreference = 'Stop'
$source = Join-Path $PSScriptRoot $(if($Launcher){'../tests/TrafficSense.LifecycleTests/PluginTestLauncher.cpp'}else{'../tests/TrafficSense.LifecycleTests/NativePluginFixture.cpp'})
New-Item -ItemType Directory -Path $OutputDirectory -Force | Out-Null
$dll = Join-Path ([IO.Path]::GetFullPath($OutputDirectory)) $(if($Launcher){'launcher.exe'}else{'fixture.dll'})
$compileOptions=@('/nologo','/EHsc',$source,"/Fe:$dll","/Fo:$OutputDirectory/$(if($Launcher){'launcher'}else{'fixture'}).obj")
if(!$Launcher){$compileOptions+='/LD'}
$vswhere = "${env:ProgramFiles(x86)}/Microsoft Visual Studio/Installer/vswhere.exe"
if (Get-Command cl.exe -ErrorAction SilentlyContinue) {
    & cl.exe @compileOptions user32.lib gdi32.lib iphlpapi.lib /link "/IMPLIB:$OutputDirectory/fixture.lib"
} elseif (Test-Path -LiteralPath $vswhere) {
    $installation = & $vswhere -latest -products '*' -requires Microsoft.VisualStudio.Component.VC.Tools.x86.x64 -property installationPath
    $compiler = Get-ChildItem -LiteralPath "$installation/VC/Tools/MSVC" -Directory | Sort-Object Name -Descending | Select-Object -First 1
    $kit = Get-ChildItem -LiteralPath "${env:ProgramFiles(x86)}/Windows Kits/10/Include" -Directory | Where-Object Name -Match '^10\.0\.' | Sort-Object Name -Descending | Select-Object -First 1
    $env:INCLUDE = "$($compiler.FullName)/include;$($kit.FullName)/ucrt;$($kit.FullName)/shared;$($kit.FullName)/um"
    $kitLib = "${env:ProgramFiles(x86)}/Windows Kits/10/Lib/$($kit.Name)"
    $env:LIB = "$($compiler.FullName)/lib/x64;$kitLib/ucrt/x64;$kitLib/um/x64"
    & "$($compiler.FullName)/bin/Hostx64/x64/cl.exe" @compileOptions user32.lib gdi32.lib iphlpapi.lib /link "/IMPLIB:$OutputDirectory/fixture.lib"
} elseif (Get-Command g++.exe -ErrorAction SilentlyContinue) {
    $gccOptions=@('-static','-O2',$source,'-o',$dll);if($Launcher){$gccOptions+='-municode'}else{$gccOptions+='-shared'}
    & g++.exe @gccOptions -lgdi32 -luser32 -liphlpapi
} else { throw 'A Windows x64 C++ compiler is required for the test fixture.' }
if ($LASTEXITCODE -ne 0) { throw 'Native plugin fixture compilation failed.' }
Write-Output $dll
