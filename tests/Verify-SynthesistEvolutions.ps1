param(
    [string]$GamePath = 'C:\Program Files (x86)\Steam\steamapps\common\Pathfinder Second Adventure',
    [string]$EvidenceDirectory = (Join-Path (Split-Path -Parent $PSScriptRoot) 'artifacts\runtime-evolutions')
)
$ErrorActionPreference = 'Stop'
# Mutates only the separately loaded SYNTHTEST- fixture. Reload the original save
# afterwards. The retraining adapter deliberately does not accept Transmogrify's
# spell, material cost or casting time. Direct transitions do not accept action timing.
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
    if (!$Condition) { throw "FAILED native evolution integration: $Description" }
    $script:checks++
}
function Rank($Snapshot, [string]$Name) { ($Snapshot.Purchased | Where-Object Name -eq ($Name + 'Feature')).Rank }
function Purchase([string]$Command, [int]$Amount, [int]$ExpectedPoints) {
    $null = Native $Command $Amount
    # Let the native command complete, then inspect its committed outcome.
    Start-Sleep -Milliseconds 300
    $snapshot = Native inspect
    Check ($snapshot.LastPurchase.Result -eq 'Success' -and $snapshot.LastPurchase.DidRun) 'Native purchase command succeeded'
    Check ($snapshot.EvolutionPoints -eq $ExpectedPoints) 'Evolution points charged exactly once'
    return $snapshot
}
$initial = Native inspect
if ($initial.SummonerLevel -ne 20 -or $initial.PetLevel -ne 15 -or $initial.State.Form -ne 0 -or
    $initial.State.Mode -ne 0 -or $initial.IsInCombat -or $initial.TwinActive -or $initial.IsRitualActive -or
    $initial.Strength -ne 10 -or $initial.Dexterity -ne 10 -or $initial.Constitution -ne 13 -or
    $initial.PetStrength -ne 27 -or $initial.PetDexterity -ne 20 -or $initial.EvolutionPoints -ne 26 -or
    $initial.AspectPoints -ne 6 -or @($initial.Purchased | Where-Object {$_.Rank -gt 0}).Count -gt 0) {
    throw 'Load the original level-20 biped post tut handover fixture, outside combat, with no purchased evolutions.'
}
$preparing = Native prepare
Check ($preparing.PetDamage -eq $initial.State.Durability.Wounds) 'Preparation displays the authoritative eidolon wounds'
$null = Native retrain-evolution-probe
$first = Purchase purchase-managed 0 25
Check ((Rank $first EvolutionNaturalArmor) -eq 1) 'First natural armour rank retained'
$aspect = Purchase purchase-aspect-managed 0 24
Check ($aspect.AspectPoints -eq 5 -and $aspect.ArmourClass -eq $initial.ArmourClass + 2) 'Greater Aspect owns one personal rank and diverts one point'
$null = Native confirm
$fused = Native fuse
$natural = ($fused.ArmourModifiers | Where-Object Descriptor -eq 'NaturalArmor' | Measure-Object ModValue -Sum).Sum
Check ($natural -eq 20) 'Fused natural armour counts the shared rank once with original advancement'
Check (@($fused.ArmourModifiers | Where-Object Descriptor -eq 'Armor').Count -eq 0) 'Fusion suppresses worn armour'
$split = Native split
Check ($split.Strength -eq 10 -and $split.Dexterity -eq 10 -and $split.ArmourClass -eq $initial.ArmourClass + 2) 'Split retains personal statistics and one shared armour rank'
$null = Native rejoin
$null = Native dismiss
$closed = Native prepare
Check (!$closed.State.EvolutionEditsOpen) 'Preparation does not reopen confirmed allocation'
$blocked = $false
try { $null = Native purchase-managed 0 }
catch {
    if ($_.Exception.Message -match 'Evolutions may only be allocated|closed|unavailable') { $blocked = $true }
    else { throw }
}
Check $blocked 'Closed native purchase is rejected'
$null = Native retrain-evolution-probe
$second = Purchase purchase-managed 0 23
Check ((Rank $second EvolutionNaturalArmor) -eq 2) 'Second natural armour rank purchased'
$large = Purchase purchase-managed 1 19
Check ((Rank $large EvolutionLarge) -eq 1) 'Large costs four points'
Check ($large.PetSize -eq 'Large' -and $large.PetReachBonus -eq 10) 'Canonical Large biped has ten-foot reach'
$strength = Purchase purchase-managed 3 15
Check ((Rank $strength EvolutionAbilityIncreaseStrength) -eq 1) 'Strength costs four points while Large'
$constitution = Purchase purchase-managed 4 11
Check ((Rank $constitution EvolutionAbilityIncreaseConstitution) -eq 1) 'Constitution costs four points while Large'
$huge = Purchase purchase-managed 1 5
Check ((Rank $huge EvolutionLarge) -eq 2) 'Huge uses the isolated Growth rank and costs six additional points'
Check ($huge.PetSize -eq 'Huge') 'Native canonical size follows Huge Growth'
Check ($huge.PetReachBonus -eq 15) 'Canonical Huge biped has fifteen-foot reach'
Check ($huge.PetDamage -eq $initial.State.Durability.Wounds -and $huge.State.Durability.Wounds -eq $initial.State.Durability.Wounds) 'Constitution changes preserve eidolon wounds'
$null = Native confirm
$fused = Native fuse
Check ($fused.Strength -eq 45 -and $fused.Dexterity -eq 16 -and $fused.Constitution -eq 23) 'Huge and purchased physical increases project once'
Check ($fused.Size -eq 'Huge' -and $fused.ReachBonus -eq $initial.ReachBonus + 10) 'Biped Huge size and reach project once'
Check ($fused.Intelligence -eq $initial.Intelligence -and $fused.Wisdom -eq $initial.Wisdom -and $fused.Charisma -eq $initial.Charisma) 'Physical evolutions retain personal mental statistics'
Check ($fused.ArmourClass -eq 43) 'Huge native size penalty and shared armour match the fixture'
Check ($fused.TemporaryHP -eq $fused.State.Durability.Maximum - $initial.State.Durability.Wounds) 'Projected HP retains missing HP'
$split = Native split
Check ($split.Strength -eq 28 -and $split.Dexterity -eq 6 -and $split.Constitution -eq 23) 'Split shares evolutions without importing eidolon physical bases'
Check ($split.ArmourClass -eq 18 -and $split.Size -eq 'Huge') 'Split owns shared size and armour without fused advancement or Meld'
Check ($split.ReachBonus -eq 15 -and $split.PetReachBonus -eq 15) 'Split applies Growth reach once to each creature'
$null = Native rejoin
$dismissed = Native dismiss
Check ($dismissed.Strength -eq 10 -and $dismissed.Dexterity -eq 10 -and $dismissed.Constitution -eq 13) 'Dismissal restores original physical statistics'
Check ($dismissed.Size -eq $initial.Size -and $dismissed.ReachBonus -eq $initial.ReachBonus) 'Dismissal restores personal size and reach'
Check ($dismissed.PersonalHP -eq $initial.PersonalHP) 'Repeated projection and Constitution changes do not heal personal HP'
Check ($dismissed.PetId -eq $initial.PetId -and !$dismissed.PetActive -and
    $dismissed.ProjectionFacts -eq 0 -and $dismissed.ProjectionModifiers -eq 0) 'Canonical identity retained and projection fully removed'
Check (($dismissed.Resources | Where-Object Ability -eq 'SplitForms').Remaining -le
    ($initial.Resources | Where-Object Ability -eq 'SplitForms').Remaining - 2) 'Transitions do not refill Split Forms rounds'
Write-Host "PASS: $checks targeted native evolution integration checks. Reload the original fixture; this is not complete gameplay acceptance."
