[CmdletBinding()]
param([string]$OutputDirectory = (Join-Path $PSScriptRoot '../.artifacts/community-plugins'))
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$root = Join-Path ([IO.Path]::GetFullPath($OutputDirectory)) ([Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $root -Force | Out-Null
$repository = 'zhongyang219/TrafficMonitorPlugins'
# Explicitly selected, source-reviewable releases. No automatic plugin discovery
# or execution; this script only acquires reference binaries into test storage.
$cases = @(
    @{Name='DateTime'; Tag='DateTime_V1.00'; Revision='d02fdf60227b1354a43eea34ab363ce208d18e96'},
    @{Name='Battery'; Tag='Battery_V1.03'; Revision='d02fdf60227b1354a43eea34ab363ce208d18e96'},
    @{Name='KeyboardIndicator'; Tag='KeyboardIndicator_V1.00'; Revision='fae9840fb6c134b120e182571931b2bc258b5b78'},
    @{Name='TextReader'; Tag='TextReader_V1.02'; Revision='d02fdf60227b1354a43eea34ab363ce208d18e96'}
)
$manifest = @()
$pins = @{
    DateTime=@('1a3da9e4333c54ddcbac07d80a9a9faf97cae5d05ca0314d95ed550dd45daf88','ade0addb758828c08f36130d04e83b1fa3641c191e4a792d99a2e40d08f21fed')
    Battery=@('9478190119430534ebfe87724bf6dcc7060afa5606404999e104b8249a0d9c80','c1a2e12b494c97d0726b05a2ffe757c275405d587111dbce5ff324b4249db0cd')
    KeyboardIndicator=@('11fcf7171380285d43e6353e1dff2bad9c27ca3baab5be472c5dfa8bbbcb25a3','a29a5def11207a1a1465440943a97ed4fa4ede51e32634909db644d21af57afe')
    TextReader=@('b26145f92f2a2d4a765f0424ce89f5dba11d5c168a7a44992c98c2e1d2e3abd7','59883e8bc0aa11c3422af2dba731d7526a943b7321fe6fdcd2a8722a45299d3e')
}
Add-Type -AssemblyName System.IO.Compression.FileSystem
foreach ($case in $cases) {
    $revision = & gh api "repos/$repository/commits/$($case.Tag)" --jq .sha
    if ($LASTEXITCODE -ne 0 -or $revision -cne $case.Revision) { throw 'Upstream tag revision differs from the reviewed snapshot.' }
    $assetName = "$($case.Tag)_x64.zip"
    $folder = Join-Path $root $case.Name
    New-Item -ItemType Directory -Path $folder -Force | Out-Null
    & gh release download $case.Tag --repo $repository --pattern $assetName --dir $folder
    if ($LASTEXITCODE -ne 0) { throw "Could not acquire $assetName" }
    $archive = Join-Path $folder $assetName
    if ((Get-Item -LiteralPath $archive).Length -gt 2MB) { throw 'Unexpected plugin archive size.' }
    $archiveHash = (Get-FileHash -LiteralPath $archive -Algorithm SHA256).Hash.ToLowerInvariant()
    if ($archiveHash -cne $pins[$case.Name][0]) { throw 'Plugin archive differs from reviewed bytes; review the new release before changing pins.' }
    $zip = [IO.Compression.ZipFile]::OpenRead($archive)
    try {
        if ($zip.Entries.Count -gt 64) { throw 'Unexpected plugin archive entries.' }
        $total = 0L
        foreach ($entry in $zip.Entries) {
            $destination = [IO.Path]::GetFullPath((Join-Path $folder $entry.FullName))
            if (!$destination.StartsWith($folder + [IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)) { throw 'Unsafe plugin archive path.' }
            $total += $entry.Length
            if ($total -gt 16MB) { throw 'Unexpected expanded plugin size.' }
        }
    } finally { $zip.Dispose() }
    Expand-Archive -LiteralPath $archive -DestinationPath $folder
    $dlls = @(Get-ChildItem -LiteralPath $folder -Filter "$($case.Name).dll" -Recurse -File)
    if ($dlls.Count -ne 1) { throw 'Expected one named plugin DLL.' }
    $dllHash = (Get-FileHash -LiteralPath $dlls[0].FullName -Algorithm SHA256).Hash.ToLowerInvariant()
    if ($dllHash -cne $pins[$case.Name][1]) { throw 'Plugin DLL differs from reviewed bytes.' }
    $manifest += [pscustomobject]@{
        Name=$case.Name; Tag=$case.Tag; SourceRevision=$case.Revision
        Source="https://github.com/$repository/tree/$($case.Revision)/Plugins/$($case.Name)"
        Release="https://github.com/$repository/releases/tag/$($case.Tag)"
        ArchiveSHA256=$archiveHash
        DllSHA256=$dllHash
        Dll=$dlls[0].FullName
    }
}
$path = Join-Path $root 'manifest.json'
$manifest | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath $path -Encoding utf8
Write-Output $path
