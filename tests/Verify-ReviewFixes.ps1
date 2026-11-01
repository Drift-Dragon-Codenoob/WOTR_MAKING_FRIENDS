param(
    [string]$AssemblyPath = (Join-Path (Split-Path -Parent $PSScriptRoot) 'bin\Release\net48\WOTR_MAKING_FRIENDS.dll'),
    [string]$GamePath = 'C:\Program Files (x86)\Steam\steamapps\common\Pathfinder Second Adventure'
)
$ErrorActionPreference = 'Stop'
$checks = 0
function Check([bool]$Value,[string]$Message) {
    if (!$Value) { throw "FAILED review fix: $Message" }
    $script:checks++
}
if (!('dnlib.DotNet.ModuleDefMD' -as [type])) {
    [Reflection.Assembly]::Load([IO.File]::ReadAllBytes((Join-Path $GamePath 'Wrath_Data\Managed\UnityModManager\dnlib.dll'))) | Out-Null
}
$mod = [dnlib.DotNet.ModuleDefMD]::Load([IO.Path]::GetFullPath($AssemblyPath))
$native = [dnlib.DotNet.ModuleDefMD]::Load((Join-Path $GamePath 'Wrath_Data\Managed\Assembly-CSharp.dll'))
function Method([string]$Type,[string]$Name) {
    $method = $mod.Find('WOTR_MAKING_FRIENDS.Synthesist.Runtime.' + $Type,$false).Methods | Where-Object {$_.Name.String -eq $Name}
    if (@($method).Count -ne 1) { throw "Expected one method: $Type.$Name" }
    return $method
}
function Calls($Method,[string]$Pattern) {
    return @($Method.Body.Instructions | Where-Object {
        $_.OpCode.Code.ToString() -in @('Call','Callvirt','Newobj') -and [string]$_.Operand -like $Pattern
    }).Count
}
try {
    $controller = $native.Find('Kingmaker.UnitLogic.Class.LevelUp.LevelUpController',$false)
    $start = $controller.Methods | Where-Object {$_.Name.String -eq 'Start'}
    Check ($start.MethodSig.Params.Count -eq 4 -and $start.MethodSig.Params[1].FullName -like '*/CharBuildMode') 'The respec-start hook matches the installed native mode argument'
    Check (@($controller.Methods | Where-Object {$_.Name.String -eq 'Stop' -and $_.MethodSig.Params.Count -eq 0}).Count -eq 1) 'The cancellation hook matches native Stop'
    Check ((Calls (Method 'RespecCompatibilityHook' 'Prefix') '*SuspendForRespec*') -eq 0) 'Opening a respec options entry point cannot detach the canonical pet'
    $guard = Method 'FusionLevelUpGuard' 'Prefix'
    Check ((Calls $guard '*get_PreviewOf*') -eq 1 -and (Calls $guard '*SuspendForRespec*') -eq 1) 'Actual respec startup resolves the original actor before suspension'
    Check ((Calls (Method 'FusionLevelUpGuard' 'Finalizer') '*ResumeAfterRespec*') -eq 1) 'A failed controller startup restores ownership'
    Check ((Calls (Method 'FusionRespecCancelled' 'Postfix') '*ResumeAfterRespec*') -eq 1) 'Cancellation restores ownership explicitly'
    $split = Method 'UnitPartSynthesist' 'EndSplit'
    Check ((Calls $split '*get_IsDead*') -eq 1 -and (Calls $split '*FusionState::EndSplit(System.Int32,System.Boolean)*') -eq 1) 'Manual rejoining forwards native death state, independent of HP'
    $adapter = Method 'EvolutionBuildAdapter' 'Apply'
    $ops = @($adapter.Body.Instructions | ForEach-Object {[string]$_.Operand})
    $validation = -1; $clear = -1; $alignment = -1
    for ($i=0;$i -lt $ops.Count;$i++) {
        if ($ops[$i] -like '*CharacterBuildRules::ValidateSavedPurchases*') { $validation=$i }
        if ($ops[$i] -like '*ClearPurchasedEvolutions*') { $clear=$i }
        if ($ops[$i] -eq 'Unknown damage-reduction alignment in the saved build.') { $alignment=$i }
    }
    Check ($validation -ge 0 -and $clear -gt $validation -and $alignment -ge 0 -and $clear -gt $alignment) 'Saved choices and native DR references are checked before clearing existing owned facts'
    Check ((Calls (Method 'FusionDamageCompleteHook' 'Finalizer') '*FusionDamageHook::TakePayment*') -eq 1) 'Failed damage events do not remain in the thread-local payment cache'
    $tick = Method 'UnitPartSynthesist' 'Tick'
    Check ((Calls $tick '*TimedResourceCharge::.ctor*') -eq 1) 'Twin uses the elapsed-interval charge calculation'
    $ins = @($tick.Body.Instructions)
    $suspend=-1; $apply=-1
    for($i=0;$i -lt $ins.Count;$i++) {
        if ($suspend -lt 0 -and [string]$ins[$i].Operand -like '*respecSuspended') { $suspend=$i }
        if ($apply -lt 0 -and [string]$ins[$i].Operand -like '*ApplyCharacterBuild*') { $apply=$i }
    }
    Check ($suspend -ge 0 -and $apply -gt $suspend) 'Respec suspension is handled before live build reconciliation'
    $twin = Method 'UnitPartSynthesist' 'BeginTwin'
    Check ($twin.Body.ExceptionHandlers.Count -gt 0 -and (Calls $twin '*RemoveProjection*') -eq 1) 'Failed Twin application has an owned-projection cleanup path'
    Write-Host "PASS: $checks review assembly/native-signature checks. No game process or UI was exercised."
}
finally { $mod.Dispose();$native.Dispose() }
