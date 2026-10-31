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
    Check (@($hookBody.Body.Instructions | Where-Object {$_.Operand -like '*LevelUpState::AddSelection*'}).Count -eq 1) 'Build flow appends native dependent selections'
    Check (@($hookBody.Body.Instructions | Where-Object {$_.Operand -like '*FeatureSelectionState::Select*'}).Count -eq 1) 'Build flow links the appended child to the native Next chain'
    $nativeState = $native.Find('Kingmaker.UnitLogic.Class.LevelUp.FeatureSelectionState', $false)
    $nativeLink = $nativeState.Methods | Where-Object Name -eq 'Select'
    Check (@($nativeLink.Body.Instructions | Where-Object {$_.Operand -like '*FeatureSelectionState::set_Next*'}).Count -eq 1) 'The native linking API sets the Next pointer consumed by navigation'
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
    foreach ($model in 'Incubus','Succubus','MovanicDeva','HellHound','Nightmare','Manticore','Vavakia','Marilith','Lillend','Salamander') {
        foreach ($name in @("Eidolon$model", "SynthesistBuildAppearance${model}Choice", "SynthesistFusionAppearance${model}Buff", "SynthesistChooseAppearance${model}Ability")) {
            Check ($ids.ContainsKey($name)) "Model source and runtime ID: $name"
        }
    }
    # The registry intentionally has case-distinct names, including dummyBuff and
    # DummyBuff. PowerShell's default hashtable/-eq comparisons conflate them.
    $baselineJson = (& git -C $root show b49af82:Output/GUIDs/GUIDs.json) -join "`n"
    if ($LASTEXITCODE -ne 0) { throw 'Could not read the pre-build-flow identifier checkpoint.' }
    foreach ($row in ($baselineJson | ConvertFrom-Json)) { Check ($ids[$row.name] -eq $row.guid) "Preserved existing ID: $($row.name)" }
    Write-Host "PASS: $checks offline build-flow assembly/identifier checks. No native UI or gameplay was exercised."
}
finally { $module.Dispose(); $native.Dispose() }
