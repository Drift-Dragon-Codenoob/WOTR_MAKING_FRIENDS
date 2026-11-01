param(
    [string]$AssemblyPath = (Join-Path (Split-Path -Parent $PSScriptRoot) 'bin\Release\net48\WOTR_MAKING_FRIENDS.dll'),
    [string]$GamePath = 'C:\Program Files (x86)\Steam\steamapps\common\Pathfinder Second Adventure'
)
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$checks = 0
function Check([bool]$Value, [string]$Message) {
    if (!$Value) { throw "FAILED Summoner feats: $Message" }
    $script:checks++
}
if (!('dnlib.DotNet.ModuleDefMD' -as [type])) {
    [Reflection.Assembly]::Load([IO.File]::ReadAllBytes((Join-Path $GamePath 'Wrath_Data\Managed\UnityModManager\dnlib.dll'))) | Out-Null
}
$mod = [dnlib.DotNet.ModuleDefMD]::Load([IO.Path]::GetFullPath($AssemblyPath))
$native = [dnlib.DotNet.ModuleDefMD]::Load((Join-Path $GamePath 'Wrath_Data\Managed\Assembly-CSharp.dll'))
try {
    $rows = Get-Content (Join-Path $root 'Output\GUIDs\GUIDs.json') -Raw | ConvertFrom-Json
    $ids = [Collections.Generic.Dictionary[string,string]]::new([StringComparer]::Ordinal)
    foreach ($row in $rows) { $ids.Add($row.name,$row.guid) }
    foreach ($name in @('SummonerExtraEvolutionFeat','SummonerExpandedArcanaFeat','SummonerRendingClawsFeat','SummonerCallFeat','SummonerExpandedArcanaModeSelection','SummonerCallPreferenceAbility')) {
        Check ($ids.ContainsKey($name)) "Stable feat/control ID: $name"
    }
    foreach ($key in @('Summoner','Bard','Bloodrager','Hunter','Inquisitor','Oracle','Skald','Sorcerer','Magus','Witch','EldritchScion')) {
        foreach ($kind in @('One','Lower')) {
            Check ($ids.ContainsKey("SummonerArcana${key}${kind}Choice")) "Class and allowance: $key / $kind"
            foreach ($level in 0..9) { Check ($ids.ContainsKey("SummonerArcana${key}${kind}Level${level}Feature")) "Fixed original spell level: $key / $kind / $level" }
        }
        foreach ($stage in @('One','LowerFirst','LowerSecond')) { Check ($ids.ContainsKey("SummonerArcana${key}${stage}Selection")) "Independent native spell page: $key / $stage" }
    }
    foreach ($stat in @('Strength','Dexterity','Constitution')) {
        Check ($ids.ContainsKey("SynthesistSummonersCall${stat}Buff")) "Call bonus: $stat"
        Check ($ids.ContainsKey("SummonerCall${stat}Ability")) "Next-summon preference: $stat"
    }
    Check ($ids.ContainsKey('SummonerCallOffAbility')) 'The optional Call bonus can be declined'
    $baseline = (& git -C $root show b747457:Output/GUIDs/GUIDs.json) -join "`n"
    if ($LASTEXITCODE -ne 0) { throw 'Could not read the pre-feat registry.' }
    foreach ($row in ($baseline | ConvertFrom-Json)) { Check ($ids[$row.name] -eq $row.guid) "Preserved pre-feat identity: $($row.name)" }
    $part = $mod.Find('WOTR_MAKING_FRIENDS.Synthesist.Runtime.UnitPartSummonerFeats',$false)
    foreach ($field in @('CallPreference','CallExpiresAt','Rending')) {
        $member = $part.Fields | Where-Object {$_.Name.String -eq $field}
        Check (@($member.CustomAttributes | Where-Object TypeFullName -eq 'Newtonsoft.Json.JsonPropertyAttribute').Count -eq 1) "Saved native feat state: $field"
    }
    $prerequisite = $mod.Find('WOTR_MAKING_FRIENDS.Synthesist.Runtime.ArcanaSpellPrerequisite',$false)
    Check (@($prerequisite.Interfaces | Where-Object {$_.Interface.FullName -eq 'Kingmaker.Blueprints.Classes.Prerequisites.IParamPrerequisite'}).Count -eq 1) 'Actual spell parameters receive native prerequisite validation'
    $picker = $native.Find('Kingmaker.Blueprints.Classes.Selection.BlueprintParametrizedFeature',$false)
    foreach ($method in @('ExtractItemsFromSpellList','GetFullSelectionItems','CanSelect')) {
        Check (@($picker.Methods | Where-Object {$_.Name.String -eq $method}).Count -eq 1) "Native parametrised selector API: $method"
    }
    $full = $picker.Methods | Where-Object {$_.Name.String -eq 'GetFullSelectionItems'}
    Check (@($full.Body.Instructions | Where-Object {$_.OpCode.Code.ToString() -eq 'Ldnull'}).Count -gt 0) 'Native full-selection cache requests a unit-independent spell superset'
    $damage = $mod.Find('WOTR_MAKING_FRIENDS.Synthesist.Runtime.RendingClawsDamage',$false)
    $prepare = $damage.Methods | Where-Object {$_.Name.String -eq 'OnEventAboutToTrigger'}
    $calls = @($prepare.Body.Instructions | ForEach-Object {[string]$_.Operand})
    Check (@($calls | Where-Object {$_ -like '*RulePrepareDamage::Add*'}).Count -eq 1) 'Rending adds to the existing native damage bundle'
    Check (@($calls | Where-Object {$_ -like '*RuleDealDamage::.ctor*'}).Count -eq 0) 'Rending does not create a separately mitigated damage hit'
    Check (@($calls | Where-Object {$_ -like '*BaseDamage::set_Precision*'}).Count -eq 1) 'Rending uses native precision damage handling'
    Check (@($calls | Where-Object {$_ -like '*BaseDamage::set_CriticalModifier*'}).Count -eq 1) 'Rending explicitly controls critical multiplication'
    $runtime = $mod.Find('WOTR_MAKING_FRIENDS.Synthesist.Runtime.UnitPartSynthesist',$false)
    foreach ($methodName in @('BeginPreparation','FinishSplitTransition')) {
        $method = $runtime.Methods | Where-Object {$_.Name.String -eq $methodName}
        Check (@($method.Body.Instructions | Where-Object {$_.Operand -like '*SummonersCallRuntime::Summoned*'}).Count -eq 0) "No Call refresh through $methodName"
    }
    $fusion = $runtime.Methods | Where-Object {$_.Name.String -eq 'EnterFusion'}
    Check (@($fusion.Body.Instructions | Where-Object {$_.Operand -like '*SummonersCallRuntime::Summoned*'}).Count -eq 1) 'Successful fusion invokes the summoning benefit once'
    $learn = $native.Find('Kingmaker.Designers.Mechanics.Facts.LearnSpellParametrized',$false)
    Check ($null -ne $learn -and @($learn.Fields | Where-Object {$_.Name.String -eq 'SpecificSpellLevel'}).Count -eq 1) 'Native spell learning supports an exact original spell level'
    Write-Host "PASS: $checks Summoner feat assembly/identifier checks. No native gameplay was exercised."
}
finally { $mod.Dispose(); $native.Dispose() }
