param(
    [ValidateSet('inspect','prepare','confirm','fuse','dismiss','split','rejoin','twin','end-twin','link',
        'advance','damage','heal-personal','damage-pet','heal-pet','purchase-managed','purchase-aspect-managed',
        'retrain-evolution-probe','combat-probe')]
    [string]$Command = 'inspect',
    [int]$Amount = 0,
    [string]$GamePath = 'C:\Program Files (x86)\Steam\steamapps\common\Pathfinder Second Adventure',
    [string]$EvidencePath
)
$ErrorActionPreference = 'Stop'
# Requires an explicitly compiled SynthesistAcceptance build and a separately loaded
# SYNTHTEST- character. Production builds do not read this interface. Operations mutate
# that test character; this is not a save-file editor or a general execution interface.
$modPath = Join-Path $GamePath 'Mods\WOTR_MAKING_FRIENDS'
$requestPath = Join-Path $modPath 'synthesist-test-request.json'
$responsePath = Join-Path $modPath 'synthesist-test-response.json'
if (!(Get-Process Wrath -ErrorAction SilentlyContinue)) { throw 'Launch the acceptance build and load a separate SYNTHTEST- character first.' }
if (Test-Path -LiteralPath $requestPath) { throw 'Another native request is pending. Do not run concurrent probes.' }
$id = [Guid]::NewGuid().ToString('N')
$request = @{ Id = $id; Command = $Command; Amount = $Amount } | ConvertTo-Json -Compress
[IO.File]::WriteAllText($requestPath, $request, [Text.UTF8Encoding]::new($false))
$deadline = [DateTime]::UtcNow.AddSeconds(15)
do {
    Start-Sleep -Milliseconds 200
    if (!(Test-Path -LiteralPath $responsePath)) { continue }
    try { $result = ConvertFrom-Json -InputObject ([IO.File]::ReadAllText($responsePath)) }
    catch { continue } # The native write may still be completing.
    if ($result.Id -ne $id) { continue }
    if ($EvidencePath) {
        [IO.File]::WriteAllText([IO.Path]::GetFullPath($EvidencePath),
            [IO.File]::ReadAllText($responsePath), [Text.UTF8Encoding]::new($false))
    }
    if (!$result.Success) { throw $result.Error }
    if (!$result.CharacterName.StartsWith('SYNTHTEST-', [StringComparison]::Ordinal)) { throw 'Unexpected response character.' }
    return $result
} while ([DateTime]::UtcNow -lt $deadline)
throw 'Native acceptance request timed out. Check the loaded character, development build and game log; do not assume the operation failed or retry a mutation blindly.'
