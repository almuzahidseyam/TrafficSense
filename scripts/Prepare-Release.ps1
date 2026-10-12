[CmdletBinding()]
param(
    [string]$Tag = '',
    [string]$OutputPath = (Join-Path $PSScriptRoot '../.artifacts/release-notes.md')
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$repository = Split-Path $PSScriptRoot -Parent
[xml]$project = Get-Content -LiteralPath (Join-Path $repository 'src/TrafficSense.csproj') -Raw
$version = [string]$project.Project.PropertyGroup.Version
if ($version -notmatch '^\d+\.\d+\.\d+$') { throw "Expected an explicit stable app version, got '$version'." }
if ($Tag -and $Tag -cne "v$version") { throw "Release tag '$Tag' does not match app version 'v$version'." }

$changelog = Get-Content -LiteralPath (Join-Path $repository 'CHANGELOG.md') -Raw -Encoding UTF8
$heading = '(?m)^## \[' + [regex]::Escape($version) + '\][^\r\n]*\r?\n'
$sections = [regex]::Matches($changelog, $heading)
if ($sections.Count -ne 1) { throw "Expected exactly one changelog section for $version." }
$remaining = $changelog.Substring($sections[0].Index + $sections[0].Length)
# Stop at the next release heading or the link references at the end of the file.
$notes = ([regex]::Split($remaining, '(?m)^## |^\[[^\]\r\n]+\]:'))[0].Trim()
if ([string]::IsNullOrWhiteSpace($notes)) { throw "Changelog section for $version is empty." }

$destination = [IO.Path]::GetFullPath($OutputPath)
[IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($destination)) | Out-Null
[IO.File]::WriteAllText($destination, "# TrafficSense v$version`n`n$notes`n", (New-Object Text.UTF8Encoding($false)))
[PSCustomObject]@{ Version = $version; NotesPath = $destination }
