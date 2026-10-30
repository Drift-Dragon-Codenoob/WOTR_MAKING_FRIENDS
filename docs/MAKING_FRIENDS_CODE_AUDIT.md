# Making Friends baseline code audit

> Historical audit before repairs. Statements about unchanged source, missing build tooling and proposed remedies describe that earlier snapshot. See [baseline repairs and current verification](BASELINE_REPAIRS.md) for subsequent work.

Date: 30 October 2026.

Audited upstream commit: `34c2940bc1d3a01b68008d96b35764cee1679b64`.

Local branch: `synthesist`. Gameplay source remains identical to upstream. The developer-message draft and research documentation are outside gameplay code.

**Assessment: the code is a useful foundation, but it is not a clean baseline for Synthesist development.** Fourteen defect groups were identified: five high-priority groups and nine medium-priority groups. These are source findings, supported where noted by isolated probes or installed-engine inspection. They are not fourteen in-game reproductions.

**Scope and evidence**

- Automated source inventory and consistency scans covered 231 C# files containing 18,202 lines, the 865-entry blueprint GUID registry, eight localisation JSON files and directly named PNG assets.
- Focused manual inspection covered custom components, Harmony patches, initialisation, eidolon creation/progression, evolution creation and purchasing, reset actions, Summoner features and summoning registration. Not every line of every content table received equivalent manual scrutiny.
- The pinned BlueprintCore v2.8.4 source was checked to resolve copying, configuration order and spell-list behaviour. The installed game assembly was inspected without modifying it, including summon-pool clearing and countable-flag release.
- A copy of the actual `AbilityCasterHasFactRank` source was compiled in memory against minimal stand-in types. Additional C# probes exercised the Life Bond collection-removal pattern and Life Link threshold expression. Stand-ins establish isolated logic, not game integration.
- No full mod build or in-game test was performed. The machine has no .NET SDK; the usual net48 reference-assembly directory and ModMenu installation are absent. Making Friends itself is not installed.
- No gameplay source, installed assemblies, mods or saves were modified. This document records findings and proposed remedies only.

P1 means repair before extending the affected foundation: crashes, unsafe HP/lifecycle behaviour, identity collisions or changes affecting unrelated game systems. P2 means a reproducible source error in gameplay, progression or diagnostics that should be corrected in the baseline repair pass. A confirmed source defect does not establish every possible runtime consequence.

**P1-01 — Life Bond has a throwing collection loop and mismatched activation guard**

Locations: [LifeBond.cs:32](../ComponentsNew/LifeBond.cs#L32), [LifeBond.cs:71](../ComponentsNew/LifeBond.cs#L71), [LifeBond.cs:92](../ComponentsNew/LifeBond.cs#L92); [ability ownership](../Abilities/AbilityLifeBondActivable.cs#L19).

`TryReduceDamage()` removes an eidolon from `eidolons` while enumerating that same `List`. When the insufficient-HP branch executes, the following enumerator step throws `InvalidOperationException`; the isolated C# probe reproduced this even with one entry. The calculation also runs before checking whether the owner needs protection.

The ability applies Life Bond to the Summoner, but `OnTurnOn()` returns for a normal non-pet owner. Its paired `OnTurnOff()` still releases `Infallible`. The installed flag implementation logs an unmatched release and can decrement a retain belonging to another effect. The damage-sharing expression also divides only the literal `1`, rather than the whole damage deficit, if multiple eidolons ever reach it.

Minimum remedy: filter without mutating an active enumeration; align activation with the intended Summoner owner; track ownership of retained flags; handle sharing and damage order explicitly. Do not reuse this component for fusion HP.

**P1-02 — Life Link has an incorrect HP floor and an unguarded master target**

Locations: [LifeLinkEidolon.cs:33](../ComponentsNew/LifeLinkEidolon.cs#L33), [LifeLinkEidolon.cs:58](../ComponentsNew/LifeLinkEidolon.cs#L58); [buff application](../Buffs/BuffSummonerLifeLink.cs#L25).

The expression `Math.Min(1, percentageValue)` produces zero when the default `HealthPercent` is zero, despite its tooltip saying zero means 1 HP. It also caps a positive percentage at 1 rather than imposing a minimum. The probes returned 0 for 100 HP/0%, and 1 for 100 HP/20%.

The same buff is installed on the Summoner and propagated to the eidolon. Although `OnTurnOn()` returns for the Summoner, the damage/health handlers themselves have no equivalent owner guard. At the threshold, they attempt to damage `Owner.Master`, which is null for an ordinary Summoner. Detached eidolons also need a master check. The exact engine response to that null target was not exercised.

Minimum remedy: separate controller and pet effects or guard every relevant handler, validate master references, correct the threshold and balance flag ownership. Verify death, unconsciousness and simultaneous Life Bond/Life Link interactions in-game.

**P1-03 — Two active equipment components share a serialisation TypeId**

Locations: [LockEquipmentSlotException.cs:13](../ComponentsNew/LockEquipmentSlotException.cs#L13), [UnlockEquipmentSlot.cs:15](../ComponentsNew/UnlockEquipmentSlot.cs#L15).

`LockEquipmentSlotFactException` and `UnlockEquipmentSlot` both declare `537c8f834c094964db16dd2ba24fdb69`. Both classes are wired into the mod: one on eidolon units and the other through the arms evolution. The scan confirmed this is the duplicate custom-component TypeId in the source.

Minimum remedy: give the distinct types unique identifiers, first checking how the existing ID is resolved and whether already-created saves need migration. The collision is confirmed; actual save corruption or which type wins registration has not been demonstrated. Do not change identifiers blindly and assume old saves are repaired.

**P1-04 — Summon-pool clearing is globally replaced with narrower, null-unsafe behaviour**

Location: [ContextActionClearSummonPoolPatch.cs:23](../ComponentPatches/ContextActionClearSummonPoolPatch.cs#L23).

This prefix intercepts every `ContextActionClearSummonPool`, without checking that the pool belongs to Making Friends, and always skips the original method. The installed original clears all units in the requested pool; the replacement clears only matching-caster summons.

It also dereferences both the summon caster and `AbilityContext.MaybeCaster` while constructing log arguments. A missing context causes the per-unit catch to run without clearing that unit. Compiling logging out does not prevent argument evaluation.

Minimum remedy: restrict the altered behaviour to owned pools, preserve the original for other callers, validate contexts and define any required fallback. Campaign-specific failures were not reproduced, but the global behaviour change and null branches are confirmed.

**P1-05 — Base-evolution groups are overwritten during copying**

Location: [FeatureCreateAllEvolutions.cs:143](../Features/EidolonFeatures/BaseFeatures/FeatureCreateAllEvolutions.cs#L143).

`CreateAllBaseEvolutions()` calls `SetGroups(EvolutionBase)` before `CopyFrom(...)`. The pinned library's feature-copy implementation copies `Groups` from the source, and its configuration actions run in insertion order. The source features belong to `EvolutionTransmogrifiable`, so the intended base tag is overwritten.

The reset action selects that removable group. Consequently the code does not reliably distinguish starting/base evolutions from purchased evolutions. Where those base features are present in the collection traversed by the reset, they become removal candidates. The resulting character state requires in-game reproduction.

Minimum remedy: copy first, then explicitly assign the intended base groups. Test every starting form and subtype through reset, save/load and repeated resets, particularly their arms and natural attacks.

**P2-06 — Growth disagrees with its own cost and level requirements**

Locations: [EvolutionLarge.cs:118](../Features/EidolonFeatures/Evolutions/4-Point%20Evolutions/EvolutionLarge.cs#L118), [rank restriction](../ComponentsNew/AbilityCasterFactRank.cs#L24).

The action adds the growth feature before checking whether the target has that feature. The condition is therefore true on the first purchase too: the action adds both the four-point and two-point cost markers. Its availability check initially requires only four points.

The actual rank restriction, using start level 8 and a five-level interval, rejects rank two at level 13 and allows it at 14. The mod's description explicitly promises Huge at 13. The original component compiled against stand-ins reproduced this result.

Minimum remedy: determine the next rank and its cost before mutation, use that same cost for checks and accounting, and correct the level boundary. Also verify Huge bonuses against the description; a second identical stat increment does not obviously implement its stated natural-armour total.

**P2-07 — Several natural-attack evolutions check capacity without accounting for it**

Examples: [EvolutionClawsLimbs.cs:37](../Features/EidolonFeatures/Evolutions/3-Point%20Evolutions/EvolutionClawsLimbs.cs#L37), [EvolutionSpike.cs:30](../Features/EidolonFeatures/Evolutions/1-Point%20Evolutions/EvolutionSpike.cs#L30), [EvolutionPincers.cs:40](../Features/EidolonFeatures/Evolutions/1-Point%20Evolutions/EvolutionPincers.cs#L40).

Claws Limbs, Pincers Limbs, Slam Limbs and Spike add attacks but lack the matching `EidolonMaxAttacksResource` reduction used by Bite, Claws and Tentacle. The primary Pincers, Slam and Tentacle Mass choices also check a two-attack allowance without declaring that debit.

For repeatable additional-limb choices, further purchases can keep passing a capacity check whose underlying allowance has not decreased. Exact attack totals and rank-refresh behaviour still need engine testing.

Minimum remedy: make attack-cap accounting consistent across all attack features and their base variants. Treat weapon-bearing arms separately: their current charge is documented as an upstream gameplay adaptation, so changing that rule requires a design decision rather than a silent bug fix.

**P2-08 — Evolution purchasing restores unrelated resources on every pet**

Locations: [FeatureCreateAllEvolutions.cs:72](../Features/EidolonFeatures/BaseFeatures/FeatureCreateAllEvolutions.cs#L72), [RestoreResourceOnPet.cs:43](../ComponentsNew/RestoreResourceOnPet.cs#L43).

The purchase action configures two ostensibly resource-specific restorations with `m_IsFullRestoreAllResources = true`. The helper then ignores the selected resource and calls `FullRestoreAll()` on every pet, without filtering to the eidolon.

When the Summoner purchases an Aspect evolution, this reaches the Summoner's pets and can refill unrelated resource-based abilities. A purchase executed by the eidolon itself normally has no pets to traverse. This finding concerns ability resources, not a demonstrated reset of spell slots or healing.

Minimum remedy: restore only the intended resource on the intended eidolon. Verify Aspect purchases with another companion and an expended daily ability.

**P2-09 — Transmogrify's caster-reset branches use the target's data**

Locations: [RemoveFactWithGroup.cs:63](../ComponentsNew/RemoveFactWithGroup.cs#L63), [AddFactsOnArray.cs:46](../ComponentsNew/AddFactsOnArray.cs#L46); [spell wiring](../Spells/SpellTransmogrify.cs#L70).

The removal helper builds `casterFeatures` but then loops over the target's `features` again. Caster-only purchased evolutions are therefore not selected for removal. The restoration helper similarly checks whether the target has a replacement feature before deciding whether to add it to the caster.

These errors matter when caster and target differ, which this spell permits. Self-casting can conceal them.

Minimum remedy: use the caster's collection and presence checks in the caster branches; verify self-casting and casting on an eidolon with different evolution choices, alongside P1-05.

**P2-10 — Greater Shield Ally removes the wrong buff from the Summoner**

Location: [BuffSummonerShieldAllyGreater.cs:78](../Buffs/BuffSummonerShieldAllyGreater.cs#L78).

The master branch applies `SummonerShieldAllyGreaterIsMasterBuff`, but its removal action removes `SummonerShieldAllyGreaterBuff`. Because the applied buff is permanent, the intended +4 master benefit is not removed by this exit action. A different cleanup path could mask the symptom, but this branch is mismatched.

Minimum remedy: remove the exact master buff owned by the aura and test entering/leaving range, dismissal and save/load. Include overlapping aura sources in verification.

**P2-11 — Bonded Senses compares different kinds of values and bypasses modifier ownership**

Location: [BondSensesComponent.cs:25](../ComponentsNew/BondSensesComponent.cs#L25).

It compares the eidolon's Perception `BaseValue` against the Summoner's modified total, then copies the eidolon's modified total only when that comparison succeeds. Example: Summoner total 15, eidolon base 10/total 20 incorrectly yields no benefit.

It writes `ModifiedValue` directly rather than adding an owned modifier; removal then attempts to remove a modifier it never created. The assigned value can remain stale until normal recalculation. This is not evidence of permanent save corruption. The upstream README also acknowledges refresh problems.

Minimum remedy: use consistent effective values and an owned, removable adjustment, with updates when the relevant eidolon changes.

**P2-12 — Some summon spells use character-class GUIDs as spell-list references**

Locations: [SummonBase.cs:138](../Spells/Summoning/SummonBase.cs#L138), [SummonAbilities.cs:492](../Spells/Summoning/SummonAbilities.cs#L492), [CreateSummonSpells.cs:206](../Spells/Summoning/CreateSummonSpells.cs#L206).

Several dictionaries contain `CharacterClassRefs.*.Reference.Guid`, then feed those values to `BlueprintSpellListReference`. They identify class blueprints, not spell lists. The pinned BlueprintCore registration helper checks for a real `BlueprintSpellList` and skips an invalid list; the added spell-list component can also retain a reference of the wrong type.

Minimum remedy: replace these entries with the appropriate real spell-list references, accounting for shared lists and avoiding duplicates. Check that each affected spell is actually selectable in the intended classes. This is distinct from the old changelog claim that spell-list problems were fixed.

**P2-13 — The advertised base-form natural armour is not granted by the base-form builder**

Locations: [FeatureEidolonTypes.cs:58](../Features/EidolonFeatures/BaseFeatures/FeatureEidolonTypes.cs#L58), [CreateUnits.cs:131](../Units/CreateUnits.cs#L131), [FeatureArmorBonus.cs:21](../Features/EidolonFeatures/BaseFeatures/FeatureArmorBonus.cs#L21).

The base-form description advertises +2 natural armour, but its generated feature is a marker without that bonus. Unit adjustment replaces creature facts/body and removes components. The separate `EidolonStatBonus` armour progression starts at level 2; subtype-specific armour evolutions are not a universal base-form grant.

Minimum remedy: provide the base-form bonus once, distinctly from subtype and evolution bonuses. Establish actual level-1 AC on representative forms before and after the fix. The source wiring gap matches upstream issue #2; total in-game AC was not measured here.

**P2-14 — Initialisation failures can be reported as success and disappear in Release logs**

Locations: [Main.cs:40](../Main.cs#L40), [Main.cs:46](../Main.cs#L46), [Settings.cs:65](../Settings/Settings.cs#L65).

`Load()` catches patching exceptions but returns `true`. `Main.Log()` has no body in Release builds, including for exceptions. Several blueprint loops also swallow exceptions, and the initialisation flag is set before content registration completes.

The result can be a mod that appears loaded while part of its content is absent, with inadequate diagnostic evidence. This is a confirmed failure-handling flaw, not proof that startup currently fails.

Minimum remedy: keep warning/error logging in Release, report unsuccessful loading honestly, and track partial initialisation explicitly. Avoid automatic retries that duplicate registrations already completed before an exception.

**Additional issues and risks**

- [FeatureStartingEvolutions.cs:77](../Features/EidolonFeatures/BaseFeatures/FeatureStartingEvolutions.cs#L77) sets the description string as the display name a second time. It should use the description setter.
- [AddContextFeatureToPet.cs:72](../ComponentsNew/AddContextFeatureToPet.cs#L72) throws `NotImplementedException` from `GetCaption()`; this can break consumers that request action descriptions. Its null handling also warrants hardening.
- [AddAdditionalBothHandsAttacks.cs:25](../ComponentsNew/AddAdditionalBothHandsAttacks.cs#L25) reads the primary hand's unarmed status in the secondary-hand condition. Mixed-hand cases need a targeted correction and test.
- [AddEidolon.cs:92](../ComponentsNew/AddEidolon.cs#L92) intentionally strips removable equipment and detaches the pet during deactivation. Its retained references, equipment locks, event subscriptions and area-load restoration require a lifecycle test before any suspension-based fusion design. They are not proven safe to repurpose.
- [GUIDs.cs:46](../GUIDs/GUIDs.cs#L46) generates and writes random identifiers when a name is missing. No missing literal name was found, but future dynamic-name omissions could create installation-specific identifiers. A release should fail clearly instead of silently inventing them.
- [WOTR_MAKING_FRIENDS.csproj:139](../WOTR_MAKING_FRIENDS.csproj#L139) deploys to the configured game installation and copies installed GUID/localisation data back into the source tree. Build isolation is needed before compilation; this is workflow risk rather than an observed gameplay defect.
- Upstream reports companion/party UI limits and animation limitations. Those claims were not reproduced. Flight/Wing Buffet prerequisites and external spell-sharing/respec compatibility remain targeted runtime checks.

**Checks that found no issue within their stated scope**

| Check | Result |
|---|---|
| Duplicate blueprint names in the 865-entry GUID registry | None found |
| Duplicate blueprint GUID values, normalised for case and hyphens | None found |
| Literal `GUIDByName("...")` calls absent from that registry | None found; computed names were not exhaustively evaluated |
| Localisation JSON parsing | All eight files parsed |
| Duplicate localisation keys within each individual file | None found; this does not certify cross-file uniqueness or text quality |
| Direct literal `LoadInternal(folder, filename)` PNG references | No missing files found; computed paths were not exhaustively evaluated |
| Source change compared with upstream | No gameplay C# or project-file changes |

**Isolated probe observations**

| Probe | Observed result |
|---|---|
| Actual rank component: level 8, existing rank 0 | Allowed |
| Actual rank component: level 13, existing rank 1 | Rejected |
| Actual rank component: level 14, existing rank 1 | Allowed |
| Actual rank component: level 20, existing rank 2 | Rejected |
| C# `List.Remove()` inside the corresponding `foreach` pattern | `InvalidOperationException` |
| Life Link expression: maximum HP 100, percentage 0 | Floor 0 |
| Life Link expression: maximum HP 100, percentage 20 | Floor 1 |

**Recommended repair order and verification**

1. Establish isolated building and usable diagnostics. Resolve the component-ID collision with an explicit compatibility decision.
2. Repair HP handling and scope the summon-pool patch. Exercise normal Summoner gameplay before building on those mechanisms.
3. Correct evolution groups, reset behaviour, attack capacity and resource accounting as one coherent subsystem. Preserve existing blueprint IDs.
4. Correct growth, aura cleanup, Perception and spell-list references. Add narrow regression checks around the actual failures.
5. Verify levels 1, 4, 8, 12, 13, 14 and 20; all three intended forms; repeated evolution purchases/resets; gear changes; rest; saving/loading; area transitions; death and respec. Then repeat the relevant cases with the user's compatibility mods.

This is a recommendation, not authorisation already acted upon. No repair or archetype implementation was performed. A full clean bill of health needs a successful build and those runtime checks.

**External evidence used**

- [Pinned BlueprintCore feature copying](https://github.com/WittleWolfie/WW-Blueprint-Core/blob/v2.8.4/BlueprintCore/BlueprintCore/Blueprints/Configurators/Classes/BaseFeatureConfigurator.cs): copies `Groups` and other feature fields.
- [Pinned BlueprintCore configuration order](https://github.com/WittleWolfie/WW-Blueprint-Core/blob/v2.8.4/BlueprintCore/BlueprintCore/Blueprints/CustomConfigurators/RootConfigurator.cs): executes internal configuration actions in list order.
- [Pinned BlueprintCore spell-list registration](https://github.com/WittleWolfie/WW-Blueprint-Core/blob/v2.8.4/BlueprintCore/BlueprintCore/Blueprints/CustomConfigurators/UnitLogic/Abilities/AbilityConfigurator.cs): rejects non-spell-list destinations.
- [Making Friends open issues](https://github.com/Balkoth-dev/WOTR_MAKING_FRIENDS/issues): checked for corroboration and remaining runtime questions; user reports were not substituted for local reproduction.

**Follow-up: complete upstream issue review**

Reviewed 30 October 2026: all 14 issue threads returned by GitHub, comprising eight open and six closed issues, including their available comments. Two pull requests returned by the same endpoint were excluded from the issue count. The earlier audit had checked the open issue summaries; this follow-up includes closed reports and developer replies. No comments were posted.

| Issue | Upstream status | Cross-check and treatment |
|---|---|---|
| [#16 — Cannot take Growth twice](https://github.com/Balkoth-dev/WOTR_MAKING_FRIENDS/issues/16) | Open; no replies | The reported level-13 failure matches the actual rank-component probe in P2-06. Repair the boundary as well as the separate cost-accounting defect. |
| [#15 — Reset evolution points](https://github.com/Balkoth-dev/WOTR_MAKING_FRIENDS/issues/15) | Open; no replies | A usage question, not by itself evidence of a missing feature. `SpellTransmogrify.Create()` implements an evolution reset. P1-05 and P2-09 identify errors in that reset path. Verify the spell's availability and explain the existing workflow before considering any new editor. |
| [#14 — Arms limited by max attacks](https://github.com/Balkoth-dev/WOTR_MAKING_FRIENDS/issues/14) | Open; no replies | This is a rules/design disagreement: the current arms description explicitly says weapon-bearing limbs count against the attack limit, and the code enforces a resource restriction. It is distinct from P2-07's missing accounting for other attacks. Decide the desired weapon/natural-attack policy explicitly. |
| [#13 — Spells not selectable](https://github.com/Balkoth-dev/WOTR_MAKING_FRIENDS/issues/13) | Closed; developer reports a fix in the latest release | Current Black Tentacles uses actual `SpellListRefs`, consistent with a repair. However, current `SummonBase` and `SummonAbilities` still contain character-class references in spell-list dictionaries. P2-12 applies to those remaining paths, not a claim that Black Tentacles itself remains unfixed. |
| [#12 — Natural Flight does not qualify for Wing Buffet](https://github.com/Balkoth-dev/WOTR_MAKING_FRIENDS/issues/12) | Open; developer explains distinction | Balkoth distinguishes natural flight from the wings evolution and questions whether those subtypes should have wings. Source confirms the distinction: the Ancestor subtype receives `Airborne`, whereas Wing Buffet requires `EvolutionFlightFeature`. Do not automatically equate all flight with anatomical wings. Establish the intended subtype rule, then test the prerequisites. |
| [#11 — Improved spell sharing from Additional Feats](https://github.com/Balkoth-dev/WOTR_MAKING_FRIENDS/issues/11) | Open; no replies | External-mod compatibility request. Making Friends' own Share Spells patch handles personal-spell targeting; that does not establish support for the external Improved Spell Sharing feat. Investigate the external feat's pet/type checks if that mod enters our compatibility scope. |
| [#10 — Animal companions can choose Summoner](https://github.com/Balkoth-dev/WOTR_MAKING_FRIENDS/issues/10) | Open; heavily modded installation reported | New concrete source lead: Summoner excludes `AnimalClass` levels, while the installed game has a distinct `AnimalCompanionClass`. Their GUIDs were verified as different. The current prerequisite does not explicitly exclude that companion class. Treat as a suspected eligibility defect pending a normal companion level-up test; other engine/mod restrictions may affect selectability. |
| [#8 — More models](https://github.com/Balkoth-dev/WOTR_MAKING_FRIENDS/issues/8) | Open | Cosmetic feature request, not a defect. Keep outside baseline repairs. |
| [#7 — Bonded Senses](https://github.com/Balkoth-dev/WOTR_MAKING_FRIENDS/issues/7) | Closed; no explanatory comments | Closure does not resolve the currently inspected comparison and modifier-ownership errors in P2-11. Retain that finding and its runtime checks. |
| [#6 — Prayer treats eidolon as an enemy](https://github.com/Balkoth-dev/WOTR_MAKING_FRIENDS/issues/6) | Closed; developer unable to reproduce | Historical report, not a confirmed current defect. Add a Prayer ally/enemy-effect check after summoning, loading and changing area. Do not claim the faction implementation is broken from this report alone. |
| [#5 — Unarmed attack before natural attacks](https://github.com/Balkoth-dev/WOTR_MAKING_FRIENDS/issues/5) | Closed; developer unable to reproduce; Bite toggle added | A commenter also describes a biped single-attack case. Test Bite primary/secondary modes and Claws with both single attacks and full attacks. Closure and the added toggle do not establish that every attack configuration was tested. |
| [#4 — Save/load locks weapon slots](https://github.com/Balkoth-dev/WOTR_MAKING_FRIENDS/issues/4) | Closed; developer explicitly reports fixed in 0.1.9 | Treat the original symptom as reportedly fixed, not an additional confirmed current bug. Preserve the save/load equipment test, especially because P1-03 independently identifies a TypeId collision in the present equipment components. |
| [#3 — Additional tabletop content](https://github.com/Balkoth-dev/WOTR_MAKING_FRIENDS/issues/3) | Closed | Wishlist for unrelated classes, feats and spells. No baseline defect established; no scope expansion warranted. |
| [#2 — Starting natural armour](https://github.com/Balkoth-dev/WOTR_MAKING_FRIENDS/issues/2) | Open; developer acknowledges historical TTT stacking concerns | Separate two questions: the universal starting +2 bonus in P2-13, and how progression/evolution natural armour stacks with Tabletop Tweaks. The comment thread discusses the latter as well as the original report. Correcting a missing base grant would not establish TTT compatibility. |

The existing count of 14 audited defect groups is unchanged. Issue #10 adds a suspected defect requiring reproduction; design requests and historical reports have not been promoted to confirmed bugs.

The repair verification set should therefore also include companion class eligibility, Prayer targeting, single versus full natural attacks, inherited flight versus wings, and equipment restoration after load. Tabletop Tweaks armour behaviour and Additional Feats spell sharing are separate compatibility cases, rather than reasons to change the baseline rules without a decision.

Installed blueprint evidence for issue #10:

- `Classes/Monster/Animal/AnimalClass.jbp`: `4cd1757a0eea7694ba5c933729a53920`.
- `Classes/Monster/Animal/AnimalCompanionClass.jbp`: `26b10d4340839004f960f9816f6109fe`.
- Current eligibility check: [SummonerClass.cs:55](../CharacterClass/SummonerClass.cs#L55).

Developer comments informing the classifications: [natural armour/TTT](https://github.com/Balkoth-dev/WOTR_MAKING_FRIENDS/issues/2#issuecomment-1849061846), [equipment fix](https://github.com/Balkoth-dev/WOTR_MAKING_FRIENDS/issues/4#issuecomment-1987522900), [attack report](https://github.com/Balkoth-dev/WOTR_MAKING_FRIENDS/issues/5#issuecomment-2243294570), [Prayer report](https://github.com/Balkoth-dev/WOTR_MAKING_FRIENDS/issues/6#issuecomment-2243292922), [flight distinction](https://github.com/Balkoth-dev/WOTR_MAKING_FRIENDS/issues/12#issuecomment-2599482683), [spell-list fix](https://github.com/Balkoth-dev/WOTR_MAKING_FRIENDS/issues/13#issuecomment-2599866613).
