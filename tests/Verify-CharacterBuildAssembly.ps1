param(
    [string]$AssemblyPath = (Join-Path (Split-Path -Parent $PSScriptRoot) 'bin\Release\net48\WOTR_MAKING_FRIENDS.dll'),
    [bool]$ExpectPreview = $true,
    [string]$GamePath = 'C:\Program Files (x86)\Steam\steamapps\common\Pathfinder Second Adventure'
)
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$checks = 0
function Check([bool]$Value, [string]$Message) {
    if (!$Value) { throw "FAILED build assembly: $Message" }
    $script:checks++
}
if (!('dnlib.DotNet.ModuleDefMD' -as [type])) {
    [Reflection.Assembly]::Load([IO.File]::ReadAllBytes((Join-Path $GamePath 'Wrath_Data\Managed\UnityModManager\dnlib.dll'))) | Out-Null
}
$module = [dnlib.DotNet.ModuleDefMD]::Load([IO.Path]::GetFullPath($AssemblyPath))
$native = [dnlib.DotNet.ModuleDefMD]::Load((Join-Path $GamePath 'Wrath_Data\Managed\Assembly-CSharp.dll'))
try {
    $types = @($module.GetTypes())
    Check (@($types | Where-Object FullName -match 'Synthesist.Runtime.(RuntimeAcceptance|RuntimePerformance|NativePerformanceHooks)$').Count -eq 0) 'Player/production builds contain no automated acceptance or profiling interface'
    $blueprints = $module.Find('WOTR_MAKING_FRIENDS.Synthesist.Blueprints.SynthesistBlueprints', $false)
    $enabled = $blueprints.Methods | Where-Object Name -eq 'get_EnableDevelopmentGraph'
    $opcode = if ($ExpectPreview) {'Ldc_I4_1'} else {'Ldc_I4_0'}
    Check ($enabled.Body.Instructions[0].OpCode.Code.ToString() -eq $opcode) 'Archetype graph matches the requested player/production build flag'
    $nativeSelect = $native.Find('Kingmaker.UnitLogic.Class.LevelUp.Actions.SelectFeature', $false)
    Check (@($nativeSelect.Methods | Where-Object {$_.Name -eq 'Apply' -and $_.Parameters.Count -eq 3}).Count -eq 1) 'Native selection Apply signature matches the scoped hook'
    $hook = $module.Find('WOTR_MAKING_FRIENDS.Synthesist.Runtime.CharacterBuildSelectionHook', $false)
    $hookBody = $hook.Methods | Where-Object Name -eq 'Postfix'
    Check (@($hookBody.Body.Instructions | Where-Object {$_.Operand -like '*CharacterBuildSelectionHook::AddVisibleStep*'}).Count -eq 1) 'Build transitions use the visible-page adapter'
    $visibleStep = $hook.Methods | Where-Object Name -eq 'AddVisibleStep'
    Check (@($visibleStep.Body.Instructions | Where-Object {$_.Operand -like '*LevelUpState::AddSelection*'}).Count -eq 1) 'Visible-page adapter uses the native selection API'
    Check ($visibleStep.Body.Instructions[1].OpCode.Code.ToString() -eq 'Ldnull') 'The parent argument is null, creating a normal page rather than an unrenderable child'
    Check (@($hookBody.Body.Instructions | Where-Object {$_.Operand -like '*FeatureSelectionState::Select*'}).Count -eq 0) 'Plain feature choices are not misrepresented as nested selections'
    $pageFilter = $native.Find('Kingmaker.UI.MVVM._VM.CharGen.CharGenVM/<>c', $false).Methods |
        Where-Object {$_.Name.String -eq '<UpdateAllFeatureSelectorPhases>b__58_0'}
    Check (@($pageFilter.Body.Instructions | Where-Object {$_.Operand -like '*FeatureSelectionState::get_Parent*'}).Count -eq 1 -and
        @($pageFilter.Body.Instructions | Where-Object {$_.OpCode.Code.ToString() -eq 'Ldnull'}).Count -eq 1) 'Installed CharGen page filter explicitly checks the null parent required by the adapter'
    Check (@($hookBody.Body.Instructions | Where-Object {$_.Operand -like '*SpawnUnit*' -or $_.Operand -like '*ApplyCharacterBuild*'}).Count -eq 0) 'Selection edits do not spawn or apply a canonical entity'
    $part = $module.Find('WOTR_MAKING_FRIENDS.Synthesist.Runtime.UnitPartSynthesistBuild', $false)
    foreach ($field in 'Current','Draft','Revision','AppearanceOverride') {
        $member = $part.Fields | Where-Object Name -eq $field
        Check (@($member.CustomAttributes | Where-Object TypeFullName -eq 'Newtonsoft.Json.JsonPropertyAttribute').Count -eq 1) "$field is explicitly persisted"
    }
    $rows = Get-Content (Join-Path $root 'Output\GUIDs\GUIDs.json') -Raw | ConvertFrom-Json
    $ids = [Collections.Generic.Dictionary[string,string]]::new([StringComparer]::Ordinal)
    foreach ($row in $rows) { $ids.Add($row.name, $row.guid) }
    $source = [IO.File]::ReadAllText((Join-Path $root 'Features\EidolonFeatures\BaseFeatures\FeatureCreateAllEvolutions.cs'))
    $source = $source.Substring($source.IndexOf('Evolutions = new'))
    $keys = @([regex]::Matches($source, '"(Evolution[A-Za-z]+)"') | ForEach-Object {$_.Groups[1].Value} | Select-Object -Unique)
    $excluded = @('EvolutionBloodFrenzy','EvolutionBullrush','EvolutionSunderarmor','EvolutionTentacleMass','EvolutionSpike','EvolutionAmorphous')
    foreach ($key in $keys | Where-Object {$_ -notin $excluded}) {
        foreach ($personal in @($false,$true)) {
            if ($personal -and $key.StartsWith('EvolutionAbilityIncrease')) { continue }
            $side = if ($personal) {'Aspect'} else {'Eidolon'}
            $options = if ($key -eq 'EvolutionDamageReduction') {@('Good','Evil','Lawful','Chaotic')} else {@('')}
            foreach ($option in $options) { Check ($ids.ContainsKey("SynthesistBuild${side}${key}${option}Choice")) "Stable ID for $side $key $option" }
        }
    }
    $catalogue = [IO.File]::ReadAllText((Join-Path $root 'Synthesist\Rules\EidolonAppearances.cs'))
    $models = @([regex]::Matches($catalogue, 'new\("([^"]+)",') | ForEach-Object {$_.Groups[1].Value})
    Check ($models.Count -eq 59 -and @($models | Select-Object -Unique).Count -eq 59) 'Expanded catalogue has 59 unique keys'
    foreach ($model in $models) {
        if ($model -notin @('Nabasu','Gargoyle')) { Check ($ids.ContainsKey("Eidolon$model")) "Existing source ID: $model" }
        foreach ($name in @("SynthesistBuildAppearance${model}Choice", "SynthesistFusionAppearance${model}Buff", "SynthesistChooseAppearance${model}Ability")) {
            Check ($ids.ContainsKey($name)) "Model source and runtime ID: $name"
        }
    }
    foreach ($category in @('Biped','Quadruped','Serpentine')) {
        foreach ($name in @("SynthesistBuildAppearance${category}GroupChoice", "SynthesistBuildAppearance${category}Selection", "SynthesistAppearance${category}Menu")) {
            Check ($ids.ContainsKey($name)) "Stable appearance browsing group: $name"
        }
    }
    $appearances = $module.Find('WOTR_MAKING_FRIENDS.Synthesist.Blueprints.AppearanceIntegration', $false)
    foreach ($method in $appearances.Methods | Where-Object {$_.Name.String -eq 'Compatible'}) {
        Check (@($method.Body.Instructions | Where-Object {$_.Operand -like '*HasArms*' -or $_.Operand -like '*SupportsWeapons*' -or $_.Operand -like '*get_Form*'}).Count -eq 0) 'Appearance validation does not gate on form or arms'
    }
    $equipment = $module.Find('WOTR_MAKING_FRIENDS.Synthesist.Runtime.AppearanceEquipment', $false)
    Check ($null -ne $equipment) 'Scoped visual equipment guard is present'
    foreach ($patch in @(@('Kingmaker.View.UnitEntityView','ShouldUpdateEquipment',0), @('Kingmaker.View.UnitEntityView','AddItemEquipment',3), @('Kingmaker.View.Equipment.UnitViewHandsEquipment','UpdateVisibility',1))) {
        $type = $native.Find($patch[0],$false)
        Check (@($type.Methods | Where-Object {$_.Name.String -eq $patch[1] -and $_.MethodSig.Params.Count -eq $patch[2]}).Count -eq 1) "Native visual hook signature: $($patch[0]).$($patch[1])"
    }
    # The registry intentionally has case-distinct names, including dummyBuff and
    # DummyBuff. PowerShell's default hashtable/-eq comparisons conflate them.
    $baselineJson = (& git -C $root show 18a81e9:Output/GUIDs/GUIDs.json) -join "`n"
    if ($LASTEXITCODE -ne 0) { throw 'Could not read the pre-build-flow identifier checkpoint.' }
    foreach ($row in ($baselineJson | ConvertFrom-Json)) { Check ($ids[$row.name] -eq $row.guid) "Preserved existing ID: $($row.name)" }
    Write-Host "PASS: $checks offline build-flow assembly/identifier checks. No native UI or gameplay was exercised."
}
finally { $module.Dispose(); $native.Dispose() }
