param(
    [string]$GamePath = 'C:\Program Files (x86)\Steam\steamapps\common\Pathfinder Second Adventure',
    [string]$ModMenuDll = "$env:LOCALAPPDATA\CodexTooling\WotrBaseline\ModMenu\ModMenu.dll",
    [string]$DotNet = "$env:LOCALAPPDATA\CodexTooling\WotrBaseline\dotnet\dotnet.exe"
)
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
if (!(Test-Path -LiteralPath $DotNet)) { $DotNet = (Get-Command dotnet -ErrorAction Stop).Source }
if (!(Test-Path -LiteralPath $ModMenuDll)) { throw 'Supply -ModMenuDll pointing to a compatible ModMenu.dll.' }
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
$env:DOTNET_NOLOGO = '1'

& $DotNet build (Join-Path $projectRoot 'WOTR_MAKING_FRIENDS.sln') -c Release "-p:WrathPath=$GamePath" "-p:ModMenuPath=$ModMenuDll" -p:DeployToGame=false -v:minimal
if ($LASTEXITCODE -ne 0) { throw 'Production build failed.' }
& $DotNet run --project (Join-Path $projectRoot 'tests\Baseline.Probes.csproj') -c Release
if ($LASTEXITCODE -ne 0) { throw 'Offline regression checks failed.' }
& (Join-Path $projectRoot 'tests\Verify-AssemblyAndSource.ps1') -GamePath $GamePath

$info = Get-Content -LiteralPath (Join-Path $projectRoot 'Output\Info.json') -Raw | ConvertFrom-Json
$artifactRoot = Join-Path $projectRoot 'artifacts'
$stageParent = Join-Path $artifactRoot ('staging\' + [Guid]::NewGuid().ToString('N'))
$stage = Join-Path $stageParent 'WOTR_MAKING_FRIENDS'
New-Item -ItemType Directory -Path $stage -Force | Out-Null
Copy-Item -Path (Join-Path $projectRoot 'Output\*') -Destination $stage -Recurse
Copy-Item -LiteralPath (Join-Path $projectRoot 'bin\Release\net48\WOTR_MAKING_FRIENDS.dll') -Destination $stage
Copy-Item -LiteralPath (Join-Path $projectRoot 'README.md') -Destination (Join-Path $stage 'UPSTREAM_README.md')
Copy-Item -LiteralPath (Join-Path $projectRoot 'docs\BASELINE_REPAIRS.md') -Destination (Join-Path $stage 'BASELINE_REPAIRS.md')
Copy-Item -LiteralPath (Join-Path $projectRoot 'LICENSE') -Destination $stage
Copy-Item -LiteralPath (Join-Path $projectRoot 'licenses\BlueprintCore-LICENSE') -Destination $stage
Copy-Item -LiteralPath (Join-Path $projectRoot 'MESSAGE_TO_BALKOTH.md') -Destination $stage
$archive = Join-Path $artifactRoot ("WOTR_MAKING_FRIENDS-$($info.Version)-baseline.zip")
Compress-Archive -LiteralPath $stage -DestinationPath $archive -Force
$hash = Get-FileHash -LiteralPath $archive -Algorithm SHA256
$hash.Hash + '  ' + (Split-Path -Leaf $archive) | Set-Content -LiteralPath ($archive + '.sha256') -Encoding ASCII
$hash
Write-Host 'Build, offline checks and packaging complete. No game deployment performed.'
