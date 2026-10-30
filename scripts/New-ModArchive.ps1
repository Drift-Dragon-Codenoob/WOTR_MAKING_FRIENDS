param(
    [Parameter(Mandatory = $true)][string]$StageDirectory,
    [Parameter(Mandatory = $true)][string]$ArchivePath
)
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.IO.Compression.FileSystem
Add-Type -AssemblyName System.IO.Compression
$stage = (Resolve-Path -LiteralPath $StageDirectory).Path.TrimEnd('\')
if (!(Test-Path -LiteralPath (Join-Path $stage 'Info.json'))) { throw 'Stage must contain Info.json at its root.' }

# Windows PowerShell Compress-Archive writes backslashes, including directory entries
# which the installer's Ionic.Zip version misidentifies as files. Write portable file
# entries explicitly and place Info.json directly at the archive root.
$stream = [IO.File]::Open($ArchivePath, [IO.FileMode]::Create, [IO.FileAccess]::Write)
try {
    $zip = New-Object IO.Compression.ZipArchive($stream, [IO.Compression.ZipArchiveMode]::Create, $true)
    try {
        foreach ($file in Get-ChildItem -LiteralPath $stage -Recurse -File | Sort-Object FullName) {
            $name = $file.FullName.Substring($stage.Length + 1).Replace('\', '/')
            [void][IO.Compression.ZipFileExtensions]::CreateEntryFromFile($zip, $file.FullName, $name, [IO.Compression.CompressionLevel]::Optimal)
        }
    } finally { $zip.Dispose() }
} finally { $stream.Dispose() }
