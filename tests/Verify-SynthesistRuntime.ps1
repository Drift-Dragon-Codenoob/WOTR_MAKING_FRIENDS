param(
    [string]$GamePath = 'C:\Program Files (x86)\Steam\steamapps\common\Pathfinder Second Adventure',
    [string]$EvidenceDirectory = (Join-Path (Split-Path -Parent $PSScriptRoot) 'artifacts\runtime-durability')
)
$ErrorActionPreference = 'Stop'
# Run only when nobody is playing the separate test character. These native damage
# tests leave injuries behind. Reload the pre-test save afterwards. Direct fusion does
# not verify ritual timing, animation, complete attacks or the wider acceptance matrix.
$probe = Join-Path (Split-Path -Parent $PSScriptRoot) 'scripts\Invoke-SynthesistProbe.ps1'
[IO.Directory]::CreateDirectory($EvidenceDirectory) | Out-Null
$script:sequence = 0
$script:checks = 0
function Native([string]$Command, [int]$Amount = 0) {
    $script:sequence++
    $evidence = Join-Path $EvidenceDirectory ('{0:D2}-{1}.json' -f $script:sequence, $Command)
    & $probe -Command $Command -Amount $Amount -GamePath $GamePath -EvidencePath $evidence
}
function Check([bool]$Condition, [string]$Description) {
    if (!$Condition) { throw "FAILED native integration: $Description" }
    $script:checks++
}
$initial = Native inspect
if ($initial.IsInCombat -or $initial.TwinActive -or $initial.IsRitualActive -or
    $initial.State.Mode -in 1,3 -or $initial.State.EvolutionEditsOpen -or
    $initial.State.Durability.Slain -or $initial.State.PersonalHpLimit -ne 1 -or
    $initial.PersonalHP -lt 20 -or
    ($initial.State.Durability.Maximum - $initial.State.Durability.Wounds) -lt 4) {
    throw 'Load a prepared, living test character outside combat, with at least four eidolon HP, twenty personal HP and a Fused Link minimum of one.'
}
if ($initial.State.Mode -eq 2) { $unfused = Native dismiss } else { $unfused = $initial }
$originalLink = [int][bool]$initial.State.LinkEnabled
try {
    $null = Native link 0
    $fused = Native fuse
    Check ($fused.UniqueId -eq $initial.UniqueId -and $fused.PetId -eq $initial.PetId) 'Fusion preserves both canonical identities'
    $remaining = $fused.State.Durability.Maximum - $fused.State.Durability.Wounds
    $hit = Native damage ($remaining - 3)
    Check ($hit.TemporaryHP -eq 3 -and $hit.PersonalHP -eq $fused.PersonalHP) 'Ordinary damage consumes only the eidolon pool'
    $null = Native link 1
    $linked = Native damage 5
    Check ($linked.TemporaryHP -eq 1 -and $linked.PersonalHP -eq ($fused.PersonalHP - 3)) 'Fused Link commits the minimum sufficient personal payment'
    $healed = Native heal-personal 2
    Check ($healed.PersonalHP -eq ($linked.PersonalHP + 2) -and $healed.TemporaryHP -eq 1 -and
        $healed.State.Durability.Wounds -eq $linked.State.Durability.Wounds) 'Ordinary healing preserves eidolon injuries'
    $resourceSnapshot = $healed.Resources | ConvertTo-Json -Compress
    for ($cycle = 1; $cycle -le 3; $cycle++) {
        $restored = Native dismiss
        Check ($restored.Strength -eq $unfused.Strength -and $restored.Dexterity -eq $unfused.Dexterity -and
            $restored.Constitution -eq $unfused.Constitution -and $restored.BAB -eq $unfused.BAB -and
            $restored.ProjectionFacts -eq 0 -and $restored.ProjectionModifiers -eq 0) "Dismissal restores the protagonist in cycle $cycle"
        $again = Native fuse
        Check ($again.TemporaryHP -eq 1 -and $again.PersonalHP -eq $healed.PersonalHP -and
            $again.State.Durability.Wounds -eq $healed.State.Durability.Wounds) "Cycle $cycle cannot heal either pool"
        Check ($again.UniqueId -eq $initial.UniqueId -and $again.PetId -eq $initial.PetId -and
            !$again.PetActive -and $again.ProjectionFacts -eq $fused.ProjectionFacts -and
            $again.ProjectionModifiers -eq $fused.ProjectionModifiers -and
            ($again.Resources | ConvertTo-Json -Compress) -eq $resourceSnapshot) "Cycle $cycle cannot duplicate entities, projection entries or daily uses"
    }
}
finally {
    $null = Native dismiss
    $null = Native link $originalLink
}
Write-Output "PASS: $script:checks targeted native durability and repeated-transition assertions. Reload the pre-test save; wider gameplay acceptance remains separate."
