param(
    [string]$GamePath = 'C:\Program Files (x86)\Steam\steamapps\common\Pathfinder Second Adventure',
    [string]$EvidenceDirectory = (Join-Path (Split-Path -Parent $PSScriptRoot) 'artifacts\runtime-mental-evolutions')
)
$ErrorActionPreference = 'Stop'
# Uses native purchase commands on the separate fixture. The direct transitions and
# retraining adapter do not accept action timing or Transmogrify. Reload afterwards.
$probe = Join-Path (Split-Path -Parent $PSScriptRoot) 'scripts\Invoke-SynthesistProbe.ps1'
[IO.Directory]::CreateDirectory($EvidenceDirectory) | Out-Null
$script:sequence = 0
$script:checks = 0
function Native([string]$Command, [int]$Amount = 0) {
    $script:sequence++
    & $probe -Command $Command -Amount $Amount -GamePath $GamePath -EvidencePath (
        Join-Path $EvidenceDirectory ('{0:D2}-{1}.json' -f $script:sequence, $Command))
}
function Check([bool]$Condition, [string]$Description) {
    if (!$Condition) { throw "FAILED native mental evolution integration: $Description" }
    $script:checks++
}
$initial = Native inspect
if ($initial.SummonerLevel -ne 20 -or $initial.State.Mode -ne 0 -or $initial.IsInCombat -or
    $initial.TwinActive -or $initial.IsRitualActive -or $initial.EvolutionPoints -ne 26 -or
    @($initial.Purchased | Where-Object {$_.Rank -gt 0}).Count -gt 0) {
    throw 'Load the original level-20 post tut handover fixture outside combat, without purchased evolutions.'
}
$null = Native prepare
$null = Native retrain-evolution-probe
foreach ($index in 5,6,7) {
    $null = Native purchase-managed $index
    Start-Sleep -Milliseconds 300
    $purchased = Native inspect
    Check ($purchased.LastPurchase.Result -eq 'Success' -and $purchased.LastPurchase.DidRun) "Native mental purchase $index succeeds"
    Check ($purchased.EvolutionPoints -eq 26 - 2 * ($index - 4)) "Mental purchase $index costs two points"
}
$null = Native confirm
$fused = Native fuse
foreach ($attribute in 'Intelligence','Wisdom','Charisma') {
    Check ($fused.$attribute -eq $initial.$attribute) "Fusion retains personal $attribute"
    $petAttribute = 'Pet' + $attribute
    Check ($fused.$petAttribute -eq $initial.$petAttribute + 2) "Canonical $attribute retains its own evolution"
}
$split = Native split
foreach ($attribute in 'Intelligence','Wisdom','Charisma') {
    Check ($split.$attribute -eq $initial.$attribute + 2) "Split shares $attribute evolution"
}
$rejoined = Native rejoin
foreach ($attribute in 'Intelligence','Wisdom','Charisma') {
    Check ($rejoined.$attribute -eq $initial.$attribute) "Rejoining removes shared $attribute from fusion"
}
$dismissed = Native dismiss
$twin = Native twin
foreach ($attribute in 'Intelligence','Wisdom','Charisma') {
    Check ($twin.$attribute -eq $initial.$attribute + 2) "Twin copies $attribute evolution"
}
$ended = Native end-twin
foreach ($attribute in 'Intelligence','Wisdom','Charisma') {
    Check ($ended.$attribute -eq $initial.$attribute) "Ending Twin restores personal $attribute"
}
Check ($ended.PersonalHP -eq $initial.PersonalHP -and $ended.State.Durability.Wounds -eq $initial.State.Durability.Wounds) 'Mental evolution transitions do not heal either HP pool'
Check ($ended.UniqueId -eq $initial.UniqueId -and $ended.PetId -eq $initial.PetId -and
    !$ended.PetActive -and $ended.ProjectionFacts -eq 0 -and $ended.ProjectionModifiers -eq 0) 'Mental transitions preserve identities and remove projection completely'
Check (($ended.Resources | Where-Object Ability -eq 'TwinEidolon').Remaining -eq
    ($initial.Resources | Where-Object Ability -eq 'TwinEidolon').Remaining - 1) 'Twin charges one native minute without refilling'
Write-Host "PASS: $checks targeted native mental evolution checks. Reload the original fixture; this is not complete gameplay acceptance."
