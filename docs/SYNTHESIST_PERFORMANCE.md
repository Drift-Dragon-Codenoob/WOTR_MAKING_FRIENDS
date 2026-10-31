# Combat slowdown investigation - 31 October 2026

The severe slowdown was reproduced using David's `post tut handover` save and a fixed native opponent in the separate `SYNTHTEST-BIPED-L1` campaign. The loaded area is `Prologue_Caves_1`. ToyBox unlimited spellcasting and combined after-combat restoration remained enabled; infinite abilities remained disabled.

## Confirmed defect

`ReconcileAspectControls` attempted to remove the four inherited Aspect evolution menu abilities every frame. Native `AbilityCollection.PrepareFactForDetach` retains an ability that has an action-bar slot: it turns it off, marks it temporarily disabled, raises an ability-removal notification and returns without removing the fact. The next update therefore attempted the same removal again.

The notifications made the native action bar clear and recollect spells, abilities, items and slots every frame. This work is outside `Game.Tick`, which explains why the initial controller-only timing did not account for the frame time. With the protagonist selected, the deeper sample recorded 1,024 removal notifications across 256 combat frames and 255 complete action-bar rebuilds. The rebuild consumed about 22.12 ms per frame.

The repair preserves native ownership and stops repeatedly removing those retained abilities. The Synthesist availability guard blocks all four legacy roots and every legacy evolution variant, including excluded variants. Allocation continues through the existing preparation-restricted Synthesist menus. Ordinary characters return from that guard without these restrictions.

## Measurements and limits

Eight-second focused, unpaused real-time samples used the selected protagonist's action bar and the fixed opponent. Timings are nested and must not be added together.

| Candidate/sample | Result |
|---|---|
| Before repair, `artifacts/performance-ui-combat.json` | 31.89 FPS; 256/256 combat frames; 255 rebuilds; 1,024 removal notifications. |
| Initial repair, `artifacts/performance-removal-fix-combat.json` | 135.68 FPS; 1,086/1,086 combat frames; steady-action-bar verification passed. |
| Final repair, `artifacts/performance-final-combat.json` | 133.80 FPS; 1,071/1,071 combat frames; zero rebuilds and zero removal notifications. |
| Final repair fused, `artifacts/performance-final-fused-combat.json` | 138.70 FPS; 1,110/1,110 combat frames; steady-action-bar verification passed. |

The final inspection checked 67 legacy roots/variants: all received the Synthesist guard, with none receiving that guard when evaluated for the ordinary companions. This checks guard isolation, not the full ordinary Summoner regression matrix. A targeted dismissal after fused combat retained canonical entity `87BB` and 98 eidolon wounds, restored personal Strength/Dexterity to 10/10, left no temporary HP or owned projection facts/modifiers, and kept the eidolon inactive. Native attacks and animation acceptance remain broader release gates.

These are short local comparisons, not guarantees for every encounter or a long-session memory-leak test. Native UI rebuilds are the demonstrated cause of this reproduced slowdown. The initial single-core CPU usage, RAM growth over several hours and Unity refresh-rate warnings did not establish that cause. Windowed mode made no noticeable difference according to David. Built-in Unity profiling markers were unavailable, so that attempted diagnostic was removed.

The experiment that removed Synthesist allocation controls outside preparation did not resolve the slowdown and is not part of the final repair. Settings-key caching and blueprint-identity lookup avoid small recurring allocations; their measured costs were too small to explain the severe slowdown.

## Reproduction and safeguards

Development builds compiled with `SynthesistAcceptance=true` contain opt-in eight-second timing and a fixed native-opponent test. Production builds exclude the command interface and timing hooks and keep the unfinished archetype registration closed. No game code or proprietary assets are included in the timing report.

`scripts/Measure-SynthesistPerformance.ps1` rejects stale sample IDs, paused/unfocused/time-scaled samples, incomplete combat samples when requested, and repeated rebuild/removal events when steady-action-bar verification is requested. Select the test protagonist and allow the scene to settle before sampling. Run controlled fights only while David has handed over control; always end the opponent immediately after the sample. Do not save those mutations.

All saves and the installed candidate were backed up again under `artifacts/post-tutorial-performance-backup/` before profiling. The original handover save SHA-256 is `112D8F762A66FEB227EAD20B46305DCEF5701CFEC45B1C3DB6DA4409CDD2FCD7`. Test fights are discarded by reloading it. This repair does not complete the wider Synthesist acceptance matrix or justify version 0.3.0.

At handback, WotR is closed and the handover save still matches that hash. The original Doorstop configuration has been restored and the temporary `steam_appid.txt` removed. The repaired internal candidate remains installed with SHA-256 `58D232CE984CC869EBF7CFE5CD5D4390C3082F35AE04A76276C57F0EF916A689`; it retains the development archetype so the separate Synthesist test save remains usable. Launch normally through Steam. This is not an accepted release package.

Merged acceptance and normal production builds passed with zero warnings/errors. The normal production assembly was inspected: command/timing types are absent and unfinished archetype registration remains closed. The 863 baseline, 10,069 rules and 5,976 assembly/source checks pass. These automated totals do not replace native gameplay acceptance.
