param(
    [string]$GamePath = 'C:\Program Files (x86)\Steam\steamapps\common\Pathfinder Second Adventure',
    [Parameter(Mandatory=$true)][string]$EvidencePath,
    [switch]$ExpectCombat,
    [switch]$VerifySteadyActionBar
)
$ErrorActionPreference = 'Stop'
# Acceptance builds only. Select the test protagonist and let the scene settle first.
# This records eight seconds without spawning enemies or altering game settings.
$sample = & (Join-Path $PSScriptRoot 'Invoke-SynthesistProbe.ps1') -Command performance -GamePath $GamePath
$path = Join-Path $GamePath 'Mods\WOTR_MAKING_FRIENDS\synthesist-performance.json'
$deadline = [DateTime]::UtcNow.AddSeconds(15)
$report = $null
do {
    Start-Sleep -Milliseconds 250
    if (!(Test-Path -LiteralPath $path)) { continue }
    try { $report = ConvertFrom-Json -InputObject ([IO.File]::ReadAllText($path)) }
    catch { continue }
    if ($report.SampleId -eq $sample.Id) { break }
} while ([DateTime]::UtcNow -lt $deadline)
if ($report.SampleId -ne $sample.Id) { throw 'No matching completed performance sample; an older report is not evidence.' }
[IO.File]::WriteAllText([IO.Path]::GetFullPath($EvidencePath), [IO.File]::ReadAllText($path), [Text.UTF8Encoding]::new($false))
if (!$report.isFocused -or $report.Mode -ne 'Default' -or $report.EngineTimeScale -ne 1 -or $report.PlayerTimeScale -ne 1) {
    throw 'Performance sample was unfocused, paused or time-scaled. Evidence retained, but comparison is invalid.'
}
if ($ExpectCombat -and $report.CombatFrames -ne $report.Frames) { throw 'Combat did not continue throughout the sample.' }
if ($VerifySteadyActionBar) {
    $updates = @($report.Mod | Where-Object Name -eq 'ActionBarSlotVM.UpdateResource' | ForEach-Object Calls | Measure-Object -Sum).Sum
    if ($updates -lt $report.Frames * 10) { throw 'The selected character action bar was not active throughout this sample.' }
    $rebuilds = @($report.Mod | Where-Object Name -eq 'ActionBarVM.OnUnitChanged' | ForEach-Object Calls | Measure-Object -Sum).Sum
    $removals = @($report.Mod | Where-Object {$_.Name -like '*IPlayerAbilitiesHandler.HandleAbilityRemoved'} | ForEach-Object Calls | Measure-Object -Sum).Sum
    if ($rebuilds -gt 2 -or $removals -gt 0) { throw "Action bar notification regression: $rebuilds rebuilds, $removals ability-removal notifications." }
}
[pscustomobject]@{
    SampleId = $report.SampleId
    FramesPerSecond = $report.FramesPerSecond
    Frames = $report.Frames
    CombatFrames = $report.CombatFrames
    MeanFrameMilliseconds = $report.MeanFrameMilliseconds
    SteadyActionBarVerified = [bool]$VerifySteadyActionBar
    EvidencePath = [IO.Path]::GetFullPath($EvidencePath)
}
