param(
    [string]$GamePath = 'C:\Program Files (x86)\Steam\steamapps\common\Pathfinder Second Adventure',
    [string]$ModMenuDll = "$env:LOCALAPPDATA\CodexTooling\WotrBaseline\ModMenu-2.0.14\ModMenu.dll",
    [string]$DotNet = "$env:LOCALAPPDATA\CodexTooling\WotrBaseline\dotnet\dotnet.exe",
    [string]$InstallerZipDll = '',
    [string]$ArchivePath
)
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
if (!(Test-Path -LiteralPath $DotNet)) { $DotNet = (Get-Command dotnet -ErrorAction Stop).Source }
if (!(Test-Path -LiteralPath $ModMenuDll)) { throw 'Supply the compatible ModMenu DLL.' }
if (!$ArchivePath) { $ArchivePath = Join-Path $root 'artifacts\WOTR_MAKING_FRIENDS-build-flow-player-test.zip' }
& $DotNet build (Join-Path $root 'WOTR_MAKING_FRIENDS.sln') -c Release "-p:WrathPath=$GamePath" "-p:ModMenuPath=$ModMenuDll" -p:SynthesistPreview=true -p:SynthesistAcceptance=false -p:DeployToGame=false -v:quiet
if ($LASTEXITCODE -ne 0) { throw 'Player candidate build failed.' }
& $DotNet run --project (Join-Path $root 'tests\Baseline.Probes.csproj') -c Release --no-restore "-p:WrathPath=$GamePath"
if ($LASTEXITCODE -ne 0) { throw 'Offline rules failed.' }
& (Join-Path $root 'tests\Verify-AssemblyAndSource.ps1') -GamePath $GamePath
& (Join-Path $root 'tests\Verify-CharacterBuildAssembly.ps1') -GamePath $GamePath
$stage = Join-Path $root ('artifacts\player-staging\' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $stage -Force | Out-Null
Copy-Item (Join-Path $root 'Output\*') $stage -Recurse
Copy-Item (Join-Path $root 'bin\Release\net48\WOTR_MAKING_FRIENDS.dll') $stage
foreach ($file in 'LICENSE','MESSAGE_TO_BALKOTH.md') { Copy-Item (Join-Path $root $file) $stage }
Copy-Item (Join-Path $root 'licenses\BlueprintCore-LICENSE') $stage
Copy-Item (Join-Path $root 'docs\SYNTHESIST_BUILD_FLOW_TEST.md') (Join-Path $stage 'PLAYER_TEST.md')
Copy-Item (Join-Path $root 'docs\SYNTHESIST_APPEARANCES.md') $stage
$infoPath = Join-Path $stage 'Info.json'
$info = Get-Content $infoPath -Raw | ConvertFrom-Json
$info.DisplayName = 'MAKING FRIENDS (Synthesist build-flow player test)'
[IO.File]::WriteAllText($infoPath, ($info | ConvertTo-Json -Depth 5), [Text.UTF8Encoding]::new($false))
$manifest = @{ Candidate='Synthesist character-build flow'; ReleaseAccepted=$false; InGameTested=$false;
    AutomatedInGameInterface=$false; BaseCommit=(& git -C $root rev-parse HEAD);
    AssemblySHA256=(Get-FileHash (Join-Path $stage 'WOTR_MAKING_FRIENDS.dll')).Hash }
[IO.File]::WriteAllText((Join-Path $stage 'BUILD_INFO.json'), ($manifest | ConvertTo-Json), [Text.UTF8Encoding]::new($false))
& (Join-Path $PSScriptRoot 'New-ModArchive.ps1') -StageDirectory $stage -ArchivePath $ArchivePath
if (!$InstallerZipDll) {
    $candidate = Join-Path (Split-Path -Parent (Split-Path -Parent $root)) 'UnityModManagerInstaller\Ionic.Zip.dll'
    if (Test-Path $candidate) { $InstallerZipDll = $candidate }
}
& (Join-Path $root 'tests\Verify-Package.ps1') -ArchivePath $ArchivePath -StageDirectory $stage -InstallerZipDll $InstallerZipDll
$hash = Get-FileHash $ArchivePath
$hash.Hash + '  ' + (Split-Path -Leaf $ArchivePath) | Set-Content ($ArchivePath + '.sha256') -Encoding ASCII
Write-Host "Player test archive: $ArchivePath"
Write-Host 'No installation, game launch or in-game/computer-use test performed.'
