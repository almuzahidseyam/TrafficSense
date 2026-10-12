[CmdletBinding()]
param(
    [string]$DotNet = 'dotnet',
    [string]$OutputDirectory = (Join-Path $PSScriptRoot '../.artifacts/portable')
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$repository = Split-Path $PSScriptRoot -Parent
$profile = Join-Path $repository 'build/Portable.pubxml'
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
Copy-Item -LiteralPath (Join-Path $repository 'packaging/README.txt') -Destination $applicationDirectory
Get-ChildItem -LiteralPath (Join-Path $repository 'packaging/licenses') -File |
    Copy-Item -Destination (Join-Path $applicationDirectory 'licenses')

$archive = Join-Path $buildDirectory 'TrafficSense.zip'
Compress-Archive -Path (Join-Path $applicationDirectory '*') -DestinationPath $archive -CompressionLevel Optimal
$checksum = (Get-FileHash -LiteralPath $archive -Algorithm SHA256).Hash.ToLowerInvariant()
[IO.File]::WriteAllText("$archive.sha256", "$checksum  TrafficSense.zip`n")

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
$start = New-Object System.Diagnostics.ProcessStartInfo
$start.FileName = Join-Path $probeDirectory 'TrafficSense.LifecycleTests.exe'
$start.WorkingDirectory = $extracted
$start.UseShellExecute = $false
$start.CreateNoWindow = $true
$start.RedirectStandardOutput = $true
$start.RedirectStandardError = $true
$start.EnvironmentVariables['DOTNET_ROOT'] = Join-Path $buildDirectory 'no-installed-runtime'
$start.EnvironmentVariables['DOTNET_ROOT_X64'] = $start.EnvironmentVariables['DOTNET_ROOT']
$start.EnvironmentVariables['DOTNET_MULTILEVEL_LOOKUP'] = '0'
$start.EnvironmentVariables['DOTNET_BUNDLE_EXTRACT_BASE_DIR'] = Join-Path $buildDirectory 'bundled-runtime'
$start.EnvironmentVariables['TRAFFICSENSE_VERIFY_BUNDLE'] = '1'
$start.EnvironmentVariables['TRAFFICSENSE_EXPECTED_VERSION'] = $release.Version
$start.EnvironmentVariables['TRAFFICSENSE_PACKAGE_EXE'] = Join-Path $extracted 'TrafficSense.exe'
# Do not inherit developer diagnostics or startup hooks in a distribution test.
foreach ($name in @('DOTNET_STARTUP_HOOKS', 'DOTNET_ADDITIONAL_DEPS', 'DOTNET_SHARED_STORE')) {
    $start.EnvironmentVariables.Remove($name)
}
$process = [Diagnostics.Process]::Start($start)
try {
    $stdout = $process.StandardOutput.ReadToEndAsync()
    $stderr = $process.StandardError.ReadToEndAsync()
    if (!$process.WaitForExit(60000)) {
        $process.Kill()
        throw 'Portable lifecycle verification timed out.'
    }
    $output = $stdout.GetAwaiter().GetResult()
    $errors = $stderr.GetAwaiter().GetResult()
    if ($output) { Write-Host $output.TrimEnd() }
    if ($errors) { Write-Host $errors.TrimEnd() }
    if ($process.ExitCode -ne 0) { throw "Portable lifecycle verification failed ($($process.ExitCode))." }
} finally { $process.Dispose() }

Write-Host "Verified portable package: $archive"
[PSCustomObject]@{
    ArchivePath = $archive
    ChecksumPath = "$archive.sha256"
    ExecutablePath = Join-Path $extracted 'TrafficSense.exe'
}
