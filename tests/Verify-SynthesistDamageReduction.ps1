param(
    [string]$GamePath = 'C:\Program Files (x86)\Steam\steamapps\common\Pathfinder Second Adventure',
    [string]$EvidenceDirectory = (Join-Path (Split-Path -Parent $PSScriptRoot) 'artifacts\runtime-damage-reduction')
)
$ErrorActionPreference = 'Stop'
# Native purchase and fixed physical-damage rules on the separate test campaign.
# Explicit healing prepares the fixture; it is not an automatic restoration policy.
# Direct transitions/retraining do not accept action timing or Transmogrify.
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
    if (!$Condition) { throw "FAILED native damage reduction integration: $Description" }
    $script:checks++
}
function Purchase([string]$Command, [int]$Option, [int]$Points) {
    $null = Native $Command $Option
    Start-Sleep -Milliseconds 300
    $snapshot = Native inspect
    Check ($snapshot.LastPurchase.Result -eq 'Success' -and $snapshot.LastPurchase.DidRun) 'Native DR purchase succeeds'
    Check ($snapshot.EvolutionPoints -eq $Points) 'DR purchase charges the exact point/diversion cost'
    return $snapshot
}
function Hit([string]$Target, [int]$Alignment, [int]$ExpectedLoss) {
    $before = Native inspect
    $after = Native $Target $Alignment
    if ($Target -eq 'physical-damage-pet') { $loss = $before.PetHP - $after.PetHP }
    elseif ($before.State.Mode -eq 2) { $loss = $before.TemporaryHP - $after.TemporaryHP }
    else { $loss = $before.PersonalHP - $after.PersonalHP }
    Check ($loss -eq $ExpectedLoss) "$Target alignment $Alignment loses $ExpectedLoss HP after native mitigation (actual $loss)"
    return $after
}
$initial = Native inspect
if ($initial.SummonerLevel -ne 20 -or $initial.State.Mode -ne 0 -or $initial.IsInCombat -or
    $initial.TwinActive -or $initial.EvolutionPoints -ne 26 -or $initial.AspectPoints -ne 6 -or
    $initial.PersonalHP -lt 100 -or @($initial.Purchased | Where-Object {$_.Rank -gt 0}).Count -gt 0) {
    throw 'Load the original level-20 handover fixture outside combat with no purchased evolutions.'
}
$options = @('Good','Evil','Lawful','Chaotic')
$choice = @($initial.DamageReductionChoices | Where-Object AvailableForCanonical | Select-Object -First 1)
if ($choice.Count -ne 1) { throw 'The DR fixture needs a non-neutral alignment component.' }
$option = [Array]::IndexOf($options, $choice[0].Alignment)
$damageFlags = @{Good=1; Evil=2; Lawful=8; Chaotic=4}
$bypass = $damageFlags[$options[$option]]
$other = (0..3 | Where-Object {$_ -ne $option} | Select-Object -First 1)
$originalLink = [int][bool]$initial.State.LinkEnabled
try {
    $null = Native link 0
    $null = Native prepare
    $null = Native retrain-evolution-probe
    $first = Purchase purchase-dr $option 23
    Check (($first.DamageReductionChoices | Where-Object Alignment -eq $options[$option]).CanonicalRank -eq 1) 'First DR choice retained as one purchased rank'
    $rejected = $false
    try { $null = Native purchase-dr $other }
    catch {
        if ($_.Exception.Message -match 'opposing|upgrad|Requires') { $rejected = $true } else { throw }
    }
    Check $rejected 'An upgrade cannot switch alignment choices'
    $aspect = Purchase purchase-aspect-dr $option 21
    Check ($aspect.AspectPoints -eq 3) 'Greater Aspect DR 5 spends three personal points and diverts two'
    $null = Native confirm
    $null = Native fuse
    $null = Native split
    $null = Native heal-pet 100
    $null = Native rejoin
    $null = Hit physical-damage 0 15
    $null = Hit physical-damage $bypass 20
    $null = Hit physical-damage $damageFlags[$options[$other]] 15
    $null = Native split
    $null = Hit physical-damage 0 15
    $null = Hit physical-damage-pet 0 15
    $null = Hit physical-damage-pet $bypass 20
    $null = Native heal-pet 100
    $null = Native rejoin
    $null = Native dismiss
    $null = Native prepare
    $null = Native retrain-evolution-probe
    $upgrade = Purchase purchase-dr $option 19
    Check (($upgrade.DamageReductionChoices | Where-Object Alignment -eq $options[$option]).CanonicalRank -eq 2) 'The two-point upgrade retains the alignment and reaches rank two'
    $null = Native confirm
    $null = Native fuse
    $null = Hit physical-damage 0 10
    $null = Hit physical-damage $bypass 20
    $null = Native split
    $null = Hit physical-damage 0 10
    $null = Hit physical-damage-pet 0 10
    $null = Native rejoin
    $null = Native dismiss
    $null = Hit physical-damage 0 15
    $null = Native prepare
    $null = Native retrain-evolution-probe
    $reset = Native reset-evolutions
    Check ($reset.EvolutionPoints -eq 26 -and $reset.AspectPoints -eq 6) 'Retraining reset restores allocation capacity without retaining DR costs'
    Check (@($reset.DamageReductionChoices | Where-Object {$_.CanonicalRank -gt 0 -or $_.PersonalRank -gt 0}).Count -eq 0) 'Reset removes all owned DR alignment choices'
    $null = Native confirm
    $ended = Hit physical-damage 0 20
    Check ($ended.PetId -eq $initial.PetId -and $ended.UniqueId -eq $initial.UniqueId -and
        !$ended.PetActive -and $ended.ProjectionFacts -eq 0 -and $ended.ProjectionModifiers -eq 0) 'DR transitions/reset preserve identities and remove projection completely'
}
finally {
    $null = Native dismiss
    $null = Native link $originalLink
}
Write-Host "PASS: $checks targeted native damage reduction checks. Reload the original fixture; this is not complete gameplay acceptance."
