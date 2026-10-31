# Synthesist runtime implementation and acceptance report

31 October 2026. Branch: `synthesist`. **Development candidate; the requested stage is not complete or accepted for release.** Keep the existing mod ID. Version 0.3.0 and its distribution archive are reserved for the candidate that passes the full in-game matrix.

## Approved target

Use the original PF1e APG Summoner and Ultimate Magic Synthesist, with the protagonist retained as the same entity. Use eidolon temporary HP and optional Fused Link, initially off. Preserve wounds through dismissal, loading and rest. A slain eidolon returns no earlier than the following day, at half HP after its ritual. There is no combined-HP pool, automatic 2-HP rescue or automatic rest recovery.

The original three forms, original HD/BAB/evolution table and three ability increases at Summoner levels 5, 10 and 15 are the target. Preserve Aspect and Greater Aspect; include Split Forms at 16 and original Twin Eidolon at 20 in the first playable candidate. Level-up/respec and other transformations require dismissal for the initial release.

David's normal configuration is the primary compatibility target: ToyBox unlimited spellcasting and combined after-combat spell/ability restoration enabled, with Respec Mod and ModMenu installed. After-combat ability restoration must preserve eidolon wounds, personal HP payments, death recovery and closed evolution allocation.

Decisions confirmed during implementation:

- The baseline eidolon pet strip provides selection and inventory access.
- If an independent eidolon is alive at non-positive HP when Split Forms ends, rejoining supplies no positive temporary HP and sends it away as slain. It does not rescue the Summoner.
- **Climb, swim and underwater-breathing evolutions are unavailable for now.** No replacement map movement or check bonuses have been invented. The original-rules reference table retains the tabletop entries, while native grants omit the unavailable capabilities.
- **Unknown spell components remain usable until verified.** Confirmed somatic spells require appropriate limbs. The catalogue retains `unknown` separately from a verified absence of somatic components, so later verification can tighten the guard without misrepresenting the source.
- **Eidolon class skills use the approved WotR mapping:** Perception, Stealth, Persuasion and Knowledge (Arcana), plus four different permanent choices from the remaining seven native skills. Craft has no native equivalent. The initial preparation level-up owns these choices, the first feat and skill ranks; evolution retraining does not reset them.

## Implementation in the working tree

The rules layer covers all twenty eidolon rows, base forms, the archetype grant/removal identities, wound and Fused Link transactions, death-day eligibility, mode transitions and native-resource budget interfaces. Native resources now determine ability availability; independent expenditure counters were removed from `FusionState`.

The runtime candidate adds native archetype registration, an isolated d10/full-BAB eidolon class and form-specific native save profiles; a serialised `UnitPartSynthesist` with a native canonical entity reference in cross-scene storage; preparation and allocation controls; separately owned projection facts/modifiers; fusion, dismissal, Melds, Jump, Split and Twin controls; native resources; an optional reflection-based ToyBox after-combat adapter; and initial level-up/respec protection. Rejuvenate spells use a separate spell list/book and native healing rules. Maker's Jump uses native Dimension Door delivery.

These systems are under integration testing. Their presence in source is not evidence that their complete gameplay behaviour is correct. Startup validation has already caught shared mutable evolution components; copies are being isolated before any per-archetype changes are applied. Ordinary Summoner regression testing remains required.

A development-only acceptance interface and archetype registration are enabled only with `-p:SynthesistAcceptance=true` while the runtime remains incomplete. It accepts a fixed set of native integration operations and refuses characters whose names do not begin `SYNTHTEST-`. Normal release builds contain no active command interface and do not offer the unfinished archetype. Internal direct fusion entry does not test the one-minute action itself.

## Actual validation so far

| Check | Result and limits |
|---|---|
| Build | Debug compilation passes with no warnings/errors after the current source changes. Merged Release candidates have also built and loaded; every subsequent candidate must be rechecked. |
| Offline rules | 863 baseline assertions and 10,069 Synthesist assertions pass. Many assertions are a damage parameter sweep; these do not exercise Unity. |
| Assembly and source | 5,976 consistency assertions pass, including explicit validation of every registry row's plain name and GUID. Registry uniqueness alone did not reject an accidental nested-array serialisation; the additional validation closes that verification gap. |
| Registration | A native candidate loaded with the installed four-mod set. Synthesist appeared under Summoner in character creation with all three forms available. Full 1-20 grant validation remains outstanding. |
| Accelerated progression | Native level transactions on the separate biped test reached Summoner 5/10/15/20 with eidolon HD 4/8/12/15. At 20, the three assigned Strength increases plus the original physical progression produced Strength 27, Dexterity 20 and BAB 15 while fused. Existing three wounds remained. This does not verify every intermediate choice UI, skill allocation or evolution prerequisite. |
| Separate test save | Created `SYNTHTEST-BIPED-L1`. Native saves contain both its canonical dormant eidolon and its serialised rules state. No existing campaign was loaded for these tests. |
| Basic fusion | Direct native test entry produced Strength 16, Dexterity 12, Constitution 13, BAB 1 and a six-HP eidolon pool on the level-one test. Identity remained unchanged. This does not accept HP advancement or attack/casting animations. |
| Ordinary damage | A native three-damage hit reduced the six-HP eidolon pool to three and left personal HP unchanged. |
| Fused Link | Initial test failed: native damage commit overwrote the payment. The repaired transaction passes the targeted case: five damage against three eidolon HP consumes two eidolon HP and three personal HP, leaving one eidolon HP and seven personal HP. |
| Repeated entry/removal | Targeted dismissal restored the protagonist's original statistics and appearance. Re-entry retained one eidolon HP and seven personal HP, without healing either pool. Broader modifier/attack/resource comparisons remain outstanding. |
| Fused load | Earlier candidates failed with premature view/equipment reconciliation. The targeted level-one retest now restores appearance, physical statistics and three remaining eidolon HP, with the same canonical entity inactive after reconciliation. No Synthesist runtime or transition exception appeared in that run. Broader mode/area/load testing remains outstanding. |
| Basic advanced transitions | Direct native test entry at level 20 split and rejoined the same canonical entity, retaining three wounds and restoring the 94-HP pool. Twin entry changed the physical statistics without adding temporary HP; dismissal restored the original form. This does not accept action timing, effect counters, racial suppression, resource limits or death/expiry behaviour. |
| Native spell casting | The first real Rejuvenate cast failed because native polymorph blocked casting. With source-owned native casting permissions, the biped completed a normal Lesser Rejuvenate cast, restoring three eidolon wounds and leaving personal HP unchanged. Its displayed spell uses stayed at three with ToyBox unlimited casting enabled. Other spells/forms and exhausted-slot casting remain outstanding. |
| Higher-level HP load | The earlier native `DifficultOpponent` penalty of -20 HP is repaired. A save/load retest retains the same canonical entity, d10 hit die, maximum HP 99, three wounds and 96 temporary HP. The canonical entity is inactive after reconciliation, with no NPC difficulty modifier. This is one targeted higher-level load case. |
| ToyBox after-combat restoration | A fixed native opponent was engaged through the native combat system in the separate festival test. After a real-time claw attack killed it and party combat ended, Jump/Split/Twin refilled from 1/11/19 to 3/20/20. Personal HP stayed at 140 following a three-HP Fused Link payment; eidolon wounds stayed at 98, temporary HP at one, and evolution allocation stayed closed. Both requested ToyBox settings remained enabled. Dormant-eidolon daily resources, active Split timers, slain recovery and turn-based combat still need separate checks. |
| Evolution purchase probe | The first adapter probe passed caster availability but did not purchase anything: its bare native `AbilityData` lacked the granted menu fact, so native execution interrupted it. The probe now constructs variants from the granted native menu fact and validates the target. The corrected probe and purchase/upgrade/removal cases still await execution. This failure does not establish that the player-facing menu was broken. |
| Class-skill choices | The four base class skills and four native selections from seven additional skills are implemented; their blueprint graph loaded without errors. Fresh first-level selection and duplicate-choice prevention still require native UI acceptance. Existing accelerated prototype saves predate these initial choices. |
| Models | The biped view has appeared in a native test. Movement, casting, natural/manufactured attacks, other forms and armed variants are not accepted yet. |
| Complete gameplay matrix | Outstanding. Split/Twin effects, timers, death, all forms, full progression, ToyBox restoration, native save/load, area/rest/equipment/respec and ordinary Summoner regression are not yet accepted. |

## Installation and test safeguards

Before experimental installation, the installed Making Friends folder, all saves, ToyBox settings, Respec settings and Doorstop configuration were backed up under `artifacts/pre-synthesist-20261031-081607/`. The installed DLL is an internal candidate and still carries the baseline metadata. User ToyBox settings were retained.

Steam's restart path prevented Doorstop injection during direct development launches. Testing temporarily uses a local `steam_appid.txt` and an absolute Doorstop target. These are local test arrangements, must not enter a distribution, and must be removed/restored before handing back the installation. The original configuration is in the backup.

## Remaining release gates

Finish and verify the original evolution catalogue, prerequisites, costs and ownership; natural/manufactured attack assembly; arms/model compatibility; exact statistics, size and equipment behaviour; somatic catalogue handling; ordinary/eidolon healing and Constitution changes; effect-state reproduction in Split Forms; native resource/timer persistence; original Twin racial-trait handling; respec completion/cancellation/removal; and the complete acceptance matrix from the approved plan.

Improved natural armour's fifth purchase and the original managed-evolution level gates now use separate native purchase facts. Fast healing has separate native 1-5-HP buffs, with personal healing while fused and independent eidolon healing only during Split Forms. Native purchases, upgrades and removal still need acceptance. Initial/advancement evolution capacity is refreshed without changing daily combat resources or opening a closed allocation.

Split Forms now projects managed ability-score and natural-armour evolutions onto the Summoner without importing the eidolon's physical base values or advancement armour. Aspect overlap is counted once; native natural armour is a stacking descriptor. These repairs await purchase-based native acceptance. Split-created effect copies are tracked separately, retained across an active split load, and cleaned up on rejoining/dismissal to prevent accumulating copied buffs. Reproducing arbitrary effect component counters and reconciling effects acquired independently during a split remain release gates.

`scripts/Invoke-SynthesistProbe.ps1` provides the bounded native test interface. `tests/Verify-SynthesistRuntime.ps1` adds a reproducible, targeted damage/healing/repeated-transition sequence for a separately prepared test character. It deliberately leaves test injuries behind and requires reloading the pre-test save afterwards. The newly added sequence has passed script parsing but has not yet been run; its assertion count is not included in accepted results. No probes should run while David is playing the test character.

The opening festival produced a kill without normal party combat in the first spawn attempt. Explicit native engagement produced the real combat transition used above. David offered to advance the separate test character through the opening to Defender's Heart; computer input and state-changing probes are left to him during that handover. A normal adventuring area is still required for broader combat, AI and lifecycle acceptance. The development installation remains in use for that work; restore the loader configuration and stable installation when experimental testing ends.

Specific inherited mismatches still requiring correction include alignment-based DR and its upgrade, breath damage/daily resources, missing original evolution choices, attack-specific Reach and the inherited level-one/free Transmogrify spell. The Summon Monster secondary book stayed at caster level one during accelerated advancement; an archetype-scoped native-book advancement repair is in source and awaiting tier/charge acceptance. These are release failures, not accepted rules departures.

No acceptance total or successful compilation substitutes for these scenarios. Any additional rules departure requires David's decision.

## References

- [Original Synthesist](https://www.aonprd.com/ArchetypeDisplay.aspx?FixedName=Summoner+Synthesist).
- [Original Summoner](https://www.aonprd.com/ClassDisplay.aspx?ItemName=Summoner).
- [Original eidolon advancement](https://www.aonprd.com/ClassDisplay.aspx?ItemName=Eidolon).
- [Original base forms](https://www.aonprd.com/EidolonBaseForms.aspx).
- [Spell-component facts and primary links](research/PF1E_SPELL_COMPONENTS.json).
- [Installed-game archetype research](WOTR_ARCHETYPE_RESEARCH.md).
