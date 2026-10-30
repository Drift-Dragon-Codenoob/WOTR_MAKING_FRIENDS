param(
    [Parameter(Mandatory = $true)][string]$ArchivePath,
    [Parameter(Mandatory = $true)][string]$StageDirectory,
    [string]$InstallerZipDll = ''
)
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.IO.Compression.FileSystem
Add-Type -AssemblyName System.IO.Compression
$projectRoot = Split-Path -Parent $PSScriptRoot
$stage = (Resolve-Path -LiteralPath $StageDirectory).Path.TrimEnd('\')
$expected = New-Object 'System.Collections.Generic.Dictionary[string,string]' ([StringComparer]::Ordinal)
foreach ($file in Get-ChildItem -LiteralPath $stage -Recurse -File) {
    $expected.Add($file.FullName.Substring($stage.Length + 1).Replace('\', '/'), $file.FullName)
}
$zip = [IO.Compression.ZipFile]::OpenRead($ArchivePath)
try {
    foreach ($entry in $zip.Entries) {
        if ($entry.FullName.Contains('\')) { throw "ZIP entry uses a backslash: $($entry.FullName)" }
        if ($entry.FullName.StartsWith('/') -or $entry.FullName -match '(^|/)\.\.(/|$)|:') { throw 'Unsafe ZIP entry.' }
    }
    if ($null -eq $zip.GetEntry('Info.json') -or $null -eq $zip.GetEntry('WOTR_MAKING_FRIENDS.dll')) { throw 'Mod metadata and DLL must be at the ZIP root.' }
    $entries = @($zip.Entries | Where-Object Name -ne '')
    if ($entries.Count -ne $expected.Count) { throw 'Package file count differs from stage.' }
    foreach ($entry in $entries) {
        if (!$expected.ContainsKey($entry.FullName)) { throw "Unexpected entry: $($entry.FullName)" }
    }
} finally { $zip.Dispose() }

function Verify-Extracted([string]$Destination) {
    if (@(Get-ChildItem -LiteralPath $Destination -Recurse -File).Count -ne $expected.Count) { throw 'Extraction file count differs from stage.' }
    foreach ($pair in $expected.GetEnumerator()) {
        $actual = Join-Path $Destination $pair.Key
        if (!(Test-Path -LiteralPath $actual -PathType Leaf)) { throw "Missing extracted file: $($pair.Key)" }
        if ((Get-FileHash -LiteralPath $actual -Algorithm SHA256).Hash -ne (Get-FileHash -LiteralPath $pair.Value -Algorithm SHA256).Hash) { throw "Extracted hash mismatch: $($pair.Key)" }
    }
}
$probeRoot = Join-Path $projectRoot ('artifacts\package-checks\' + [Guid]::NewGuid().ToString('N'))
$standard = Join-Path $probeRoot 'standard'
[IO.Compression.ZipFile]::ExtractToDirectory($ArchivePath, $standard)
Verify-Extracted $standard
Write-Host "PASS: root layout, portable paths and $($expected.Count) file hashes after .NET ZIP extraction."

if ($InstallerZipDll) {
    if (!('Ionic.Zip.ZipFile' -as [type])) { [Reflection.Assembly]::Load([IO.File]::ReadAllBytes($InstallerZipDll)) | Out-Null }
    $installer = Join-Path $probeRoot 'installer'
    New-Item -ItemType Directory -Path $installer -Force | Out-Null
    $ionic = [Ionic.Zip.ZipFile]::Read($ArchivePath)
    try {
        # Use the installer's own ZIP library and its file-by-file extraction pattern.
        foreach ($entry in $ionic.EntriesSorted) {
            $target = Join-Path $installer $entry.FileName
            if ($entry.IsDirectory) { New-Item -ItemType Directory -Path $target -Force | Out-Null; continue }
            New-Item -ItemType Directory -Path (Split-Path -Parent $target) -Force | Out-Null
            $output = [IO.File]::Open($target, [IO.FileMode]::Create, [IO.FileAccess]::Write)
            try { $entry.Extract($output) } finally { $output.Dispose() }
        }
    } finally { $ionic.Dispose() }
    Verify-Extracted $installer
    Write-Host "PASS: Unity Mod Manager's Ionic.Zip extracted all $($expected.Count) files with matching hashes."
}
