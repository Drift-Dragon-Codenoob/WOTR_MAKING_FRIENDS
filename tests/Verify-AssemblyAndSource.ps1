param([string]$GamePath = 'C:\Program Files (x86)\Steam\steamapps\common\Pathfinder Second Adventure')
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
$checks = 0
function Check([bool]$Condition, [string]$Description) {
    if (!$Condition) { throw "FAILED: $Description" }
    $script:checks++
}
$managed = Join-Path $GamePath 'Wrath_Data\Managed'
if (!('dnlib.DotNet.ModuleDefMD' -as [type])) {
    [Reflection.Assembly]::Load([IO.File]::ReadAllBytes((Join-Path $managed 'UnityModManager\dnlib.dll'))) | Out-Null
}
$assemblyPath = Join-Path $projectRoot 'bin\Release\net48\WOTR_MAKING_FRIENDS.dll'
$mod = [dnlib.DotNet.ModuleDefMD]::Load($assemblyPath)
$game = [dnlib.DotNet.ModuleDefMD]::Load((Join-Path $managed 'Assembly-CSharp.dll'))
try {
    Check (@($mod.GetAssemblyRefs() | Where-Object { $_.Name -eq 'BlueprintCore' }).Count -eq 0) 'BlueprintCore is embedded rather than an external runtime dependency'
    Check (@($mod.GetTypes() | Where-Object { $_.FullName -eq 'BlueprintCore.Utils.BlueprintTool' }).Count -eq 1) 'Merged output contains exactly one BlueprintTool'
    Check (@($mod.CustomAttributes | Where-Object TypeFullName -eq 'System.Security.UnverifiableCodeAttribute').Count -eq 1) 'Release module preserves the metadata required by publicised game access'
    $skipVerification = @($mod.Assembly.DeclSecurities | ForEach-Object SecurityAttributes | ForEach-Object NamedArguments | Where-Object { $_.Name -eq 'SkipVerification' -and $_.Argument.Value -eq $true })
    Check ($skipVerification.Count -gt 0) 'Release assembly includes the compiler-generated SkipVerification permission'
    $hook = $mod.Find('WOTR_MAKING_FRIENDS.MechanicsPatches.LifeTransferDamagePatch', $false)
    Check ($null -ne $hook) 'HP adapter is present in the compiled mod'
    $prefix = $hook.Methods | Where-Object { $_.Name -eq 'Prefix' }
    Check (@($prefix.Body.Instructions | Where-Object { $_.Operand -like '*get_CurrentEvent*' }).Count -gt 0) 'HP adapter checks the active rule'
    Check (@($prefix.Body.Instructions | Where-Object { $_.Operand -like '*get_LastHandledDamage*' }).Count -gt 0) 'HP adapter checks the committed target rule'
    $difficultyHook = $mod.Find('WOTR_MAKING_FRIENDS.MechanicsPatches.LifeTransferDamagePatch/TransferDifficultyPatch', $false)
    Check ($null -ne $difficultyHook) 'Transfer-specific difficulty adapter is present'
    $nativeDifficulty = $game.Find('Kingmaker.RuleSystem.Rules.Damage.RuleDealDamage', $false).Methods | Where-Object Name -eq 'ApplyDifficultyModifiers'
    Check (@($nativeDifficulty.Parameters | Where-Object { $_.Name -eq 'damage' -and $_.Type.FullName -eq 'System.Int32' }).Count -eq 1) 'Difficulty hook parameter matches the installed native method'

    $damage = $game.Find('Kingmaker.RuleSystem.Rules.Damage.RuleDealDamage', $false).Methods | Where-Object { $_.Name -eq 'OnTrigger' }
    $instructions = @($damage.Body.Instructions | ForEach-Object { $_.ToString() })
    $difficulty = -1; $temporary = -1; $commit = -1; $reason = -1
    for ($i = 0; $i -lt $instructions.Count; $i++) {
        if ($instructions[$i] -like '*::ApplyDifficultyModifiers(*') { $difficulty = $i }
        if ($instructions[$i] -like '*TemporaryHitPoints::HandleDamage(*') { $temporary = $i }
        if ($instructions[$i] -like '*UnitEntityData::set_Damage(*') { $commit = $i }
        if ($instructions[$i] -like '*UnitEntityData::set_LastHandledDamage(*') { $reason = $i }
    }
    Check ($difficulty -ge 0 -and $reason -gt $difficulty -and $temporary -gt $reason -and $commit -gt $temporary) 'Installed engine resolves difficulty, records the rule and consumes temp HP before the patched commitment'
    foreach ($name in @('LifeBond', 'LifeLinkEidolon')) {
        $type = $mod.Find("WOTR_MAKING_FRIENDS.ComponentsNew.$name", $false)
        Check (@($type.Interfaces).Count -eq 0) "$name no longer subscribes to broad death/health handlers"
        Check (@($type.Methods | ForEach-Object { $_.Body.Instructions } | Where-Object { $_.Operand -like '*Infallible*' }).Count -eq 0) "$name does not retain blanket death protection"
    }

    $customIds = @{}
    foreach ($type in $mod.GetTypes() | Where-Object { $_.FullName -like 'WOTR_MAKING_FRIENDS.*' }) {
        foreach ($attribute in $type.CustomAttributes | Where-Object { $_.TypeFullName -eq 'Kingmaker.Blueprints.JsonSystem.TypeIdAttribute' }) {
            $id = $attribute.ConstructorArguments[0].Value.ToString().Replace('-', '').ToLowerInvariant()
            $parsedId = [Guid]::Empty
            Check ([Guid]::TryParse($id, [ref]$parsedId)) "Valid component GUID format for $($type.FullName)"
            Check (!$customIds.ContainsKey($id)) "Unique custom TypeId for $($type.FullName)"
            $customIds[$id] = $type.FullName
        }
    }
    foreach ($type in $game.GetTypes()) {
        foreach ($attribute in $type.CustomAttributes | Where-Object { $_.TypeFullName -eq 'Kingmaker.Blueprints.JsonSystem.TypeIdAttribute' }) {
            $id = $attribute.ConstructorArguments[0].Value.ToString().Replace('-', '').ToLowerInvariant()
            Check (!$customIds.ContainsKey($id)) "Custom TypeId does not collide with installed $($type.FullName)"
        }
    }

    $registry = Get-Content -LiteralPath (Join-Path $projectRoot 'Output\GUIDs\GUIDs.json') -Raw | ConvertFrom-Json
    Check (@($registry | Group-Object name -CaseSensitive | Where-Object Count -gt 1).Count -eq 0) 'Blueprint registry names are unique with the production ordinal comparison'
    Check (@($registry | Group-Object { ([Guid]$_.guid).ToString('N') } | Where-Object Count -gt 1).Count -eq 0) 'Blueprint identifiers are unique'
    $names = New-Object 'System.Collections.Generic.HashSet[string]' ([StringComparer]::Ordinal)
    foreach ($entry in $registry) { [void]$names.Add($entry.name) }
    foreach ($file in Get-ChildItem -LiteralPath $projectRoot -Recurse -Filter '*.cs' | Where-Object { $_.FullName -notmatch '\\(bin|obj|tests|artifacts)\\' }) {
        $text = [IO.File]::ReadAllText($file.FullName)
        foreach ($match in [regex]::Matches($text, 'GUIDByName\("([^"\r\n]+)"\)')) {
            Check ($names.Contains($match.Groups[1].Value)) "Stable literal identifier $($match.Groups[1].Value)"
        }
    }
    $base = [IO.File]::ReadAllText((Join-Path $projectRoot 'Features\EidolonFeatures\BaseFeatures\FeatureCreateAllEvolutions.cs'))
    $base = $base.Substring($base.IndexOf('internal static void CreateAllBaseEvolutions'))
    Check ($base.IndexOf('feature.SetGroups(FeatureGroupExtension.EvolutionBase)') -gt $base.LastIndexOf('feature.CopyFrom')) 'Base evolution classification is assigned after copying'
    foreach ($name in @('SummonBase', 'SummonAbilities')) {
        $source = [IO.File]::ReadAllText((Join-Path $projectRoot "Spells\Summoning\$name.cs"))
        Check (!$source.Contains('CharacterClassRefs.')) "$name uses spell lists rather than character-class destinations"
    }
    Write-Host "PASS: $checks installed-assembly and source consistency assertions. No game process was launched."
}
finally { $mod.Dispose(); $game.Dispose() }
