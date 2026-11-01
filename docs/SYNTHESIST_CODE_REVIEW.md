# Synthesist code review and cleanup

1 November 2026. Reviewed from `c4dbb75` on `synthesist`. This is a source/API repair checkpoint, not approval to ship 0.3.0.

## Scope

Reviewed recent build-selection, appearance and four-feat changes alongside their fusion, durability, resource, respec, healing and ability-guard integration. Inspected installed native controller signatures and the installed Respec Mod entry point. Used compilation, pure regressions, metadata inspection and ZIP extraction only. No game launch, native acceptance probe or computer-use test. Installed DLL, saves and settings remain untouched.

## Findings repaired

| Priority | Finding | Repair |
|---|---|---|
| High | Manual Split Forms rejoining could miss a death effect that left positive HP. | Pass native death state to the shared rejoin rule. Apply slain/next-day recovery rather than creating a living fused shell. |
| High | Respec detached the canonical pet at the options entry point; cancellation before a controller opened could leave it detached. Tick also reconciled live state before checking suspension. | Suspend at actual controller startup, resolving its original actor. Restore ownership on cancellation, failed startup or controller closure. Handle suspension before live reconciliation. |
| Medium | Twin charged only one overdue minute per frame after travel or a clock jump. | Charge every crossed minute boundary against native availability in one update; dismiss if remaining resources cannot cover it. |
| Medium | Failed Twin projection could leave active/timer state and partial owned effects. | Add projection cleanup and clear the active flag and deadline. No invented resource refund. |
| Medium | Keeping purchases after losing Extra Evolution could exceed the new allowance and fail at confirmation. | Reject the keep operation without changing the old profile; explain that the player must rebuild. |
| Medium | Build replay cleared old owned facts before resolving every saved evolution and DR choice. | Validate known keys/rank ownership and resolve native references before clearing anything. |
| Medium | Global ability hooks assumed a caster existed; transformation scans assumed non-null action lists/actions/buff references. | Guard missing casters/uninitialised graphs and skip null action data. |
| Medium | The accepted transformation restriction covered the split Summoner but missed its independent canonical eidolon. | Apply the same caster/target restriction to that specific entity, excluding unrelated pets. |
| Low | An exception after recording a Fused Link payment could retain a damage event and scene through the thread-local cache. | Clear pending payment references on the damage exception path. |
| Low | A mechanically too-small eidolon could start the full ritual and fail only at completion. | Check minimum size in availability as well as the final rules guard. Cosmetics do not impose eligibility. |

Preflight protects against invalid saved choices; it is not a general rollback for arbitrary exceptions inside native AddFact. Damage-cache cleanup likewise does not roll back an entire failed native event or other mods' side effects. Those limits are explicit.

## Validation

- Release player-preview build: zero warnings/errors.
- Pure assertions: 873 baseline, 10,083 Synthesist, 335 character-build, 43 feat-rule and **18 review regressions**.
- Static/API checks: 7,224 consistency, 1,657 build-flow, 1,740 feat and **12 review/native-signature checks**.
- Player ZIP: 43 files; root metadata/DLL, portable paths and matching hashes after .NET and Unity Mod Manager Ionic.Zip extraction.
- Existing blueprint IDs unchanged; these repairs need no new blueprint IDs.

Regression cases cover positive-HP death, ordinary rejoining, reduced evolution allowance, unknown/over-ranked purchases, separate Aspect ownership, elapsed minute boundaries, exhaustion and repeated timer reconciliation. Static checks cover cancellation/startup contracts, native death forwarding, preflight ordering, exception cleanup and owned projection cleanup. The instruction helper counts call/new-object opcodes, avoiding false counts from branch operands pointing at calls. No native transition is claimed as executed.

## Still blocks release acceptance

The review does not finish missing original evolutions, breath damage/uses, attack-specific Reach or Transmogrify gaps. Alignment DR has source implementation awaiting native damage acceptance. Natural/manufactured attack combinations and exact equipment/stat/casting interactions still need their existing checks.

Split Forms needs complete handling/acceptance for copied effects with internal counters and effects gained while split. Twin racial-trait handling remains unfinished. Respec completion/cancellation/removal, injury/recovery edges, timers, all-form progression, models and ordinary Summoner regression need the approved player matrix. Cosmetic form/arm gates were deliberately removed and are not a task to reinstate.

The installed DLL is older. The next test needs this reviewed package installed with WotR closed. Historical automated probe results are evidence for earlier candidates, not instructions to resume that workflow; David remains the in-game tester.

## Next player handoff

Start with one separate biped test: creation/build pages, required pet advancement during preparation, confirmation, the one-minute ritual, dismissal and save/load. Then use the focused feat checks in `SYNTHESIST_FEATS.md`. Test cancellation before opening a respec controller and after it opens separately. Advanced Split/Twin and the wider matrix follow once the basic loop passes.
