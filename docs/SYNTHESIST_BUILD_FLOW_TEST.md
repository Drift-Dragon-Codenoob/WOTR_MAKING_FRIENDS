# Synthesist character-build player test

1 November 2026. **Development candidate, not version 0.3.0 or an accepted release.** David performs the in-game tests. No automated game control, native probes or profiling interface is included in this player candidate.

**Latest player feedback:** David reached evolution choices and entered preparation. The creation log also records purchase, appearance and review transitions. Ability increases now name their affected stat and +2 benefit; energy and skill variants have distinct labels. These label changes await his retest. A bounded passive `Synthesist build page:` log records the first transition of each kind. The broader advancement/respec matrix remains pending.

## Installation

Close WotR normally. Back up the installed `Mods/WOTR_MAKING_FRIENDS` folder and your saves. Install `WOTR_MAKING_FRIENDS-build-flow-player-test.zip` using Unity Mod Manager. It retains the existing mod ID and baseline version metadata, with a distinct **Synthesist build-flow player test** display name. Its ZIP has `Info.json` and the DLL at the root. This package has not been installed or launched by Codex.

Keep ToyBox unlimited casting and combined after-combat restoration enabled. Use separate test saves. The earlier local development loader overrides have not been changed by this work.

## First test: creation only

Please start with this short test before the remaining cases.

1. Create a separate Summoner/Synthesist and choose **Biped**.
2. Find **Build your eidolon** in its native feature selections. Choose **Allocate or rebuild eidolon evolutions**.
   Continue to the next creation page: **Eidolon evolutions** should appear as its own page/tab. It is not an expanding list inside the acquired build-start feature. Each purchase adds the next allocation page; finishing purchases leads to appearance and review pages.
3. Buy **Improved Natural Armor** once and **Ability Increase: Strength (+2)** once. Check that all six ability-increase choices name different stats. The cost/balance information appears in the selected choice's requirements panel. Expect three initial points, then two, then zero. Free claws/arms/legs must not consume those three points. Another natural-armour purchase should be unavailable at level one.
4. Choose **Finish eidolon evolutions**, then choose an appearance group: **Biped (36)**, **Quadruped (16)** or **Serpentine (7)**. Every group must be available for your biped build. The next normal creation page shows the models in that group. Try **Nabasu** or **Gargoyle** in Biped, or deliberately choose a dragon from Quadruped. Review and confirm. The summary should retain your mechanical biped form, selected appearance, both purchases and zero remaining points.
5. Complete creation. Use **Prepare Eidolon (build only)** and finish the canonical pet's native level-up if its portrait shows an advancement. Preparation intentionally displays a separate eidolon; skills/feats still use that native pet interface. Use **Confirm Eidolon Build**: the separate eidolon should disappear. Then use **Summon Fused Eidolon** and allow one minute of unpaused game time. Expect your existing character to take the selected appearance and purchased evolutions, with no separate eidolon combatant. Fusion remains unavailable until the pet reaches its required HD. Report whether confirmation removes the pet and whether the full ritual transforms your character.

Report: whether the build selections appeared, whether each point balance was correct, whether completion worked, and any error text. If a selection fails to appear or advance, stop there; further gameplay tests are not useful until that is repaired.

## Follow-up cases

Four normal feats are now candidates: **Extra Evolution**, **Expanded Arcana**, **Rending Claws** and **Summoner's Call**. They spend your normal feat picks. Keep an ordinary feat for the three-point baseline creation test above; choosing Extra Evolution instead raises the starting allowance to four. See [feat rules and focused player checks](SYNTHESIST_FEATS.md), also included in the ZIP. Native feat selections, combat effects and respec behaviour still await David's tests.

| Case | Action | Expected result |
|---|---|---|
| Deliberately unspent points | On another fresh test, finish purchases early. | Ordinary confirmation is unavailable; **Confirm and leave remaining evolution points unspent** explicitly completes that build. Ordinary play cannot reopen allocation. |
| Level-up and backing out | Gain a Summoner level while dismissed. Try keeping the build, then changing an earlier choice; cancel once before completing another attempt. | Keep preserves paid costs and exposes the new allowance. Rebuild exposes the full allowance. Appearance/review return. Cancelling leaves the live build, injuries and equipment unchanged. |
| Other forms and arms | Create quadruped and serpentine tests. On a quadruped, buy arms and choose any appearance, including a model without visible hands. | All three appearance groups remain available. Arms must not invalidate an appearance. The model alone grants no mechanical limbs, wings, attacks or subtype powers. Check movement/attack/casting animations yourself. |
| Base-model visuals | Fuse while equipped, change a weapon where permitted, dismiss, then check preparation and Split Forms. | Eidolon views use the selected model's built-in appearance, without overlays of your equipped items. Built-in weapons are decorative; actual attack/damage values come from the build and equipped weapon. Inventory and applicable equipment effects remain intact. Dismissal restores ordinary protagonist equipment rendering; other party members and pets render normally. |
| Respec | While dismissed and outside preparation, cancel a respec, then complete a separate one retaining Synthesist; separately test removing it. | Cancellation retains the original build. Completion offers base form, evolutions and appearance again. Wounds/recovery persist. Removal leaves no owned projection or build state. Native and Respec Mod paths need separate testing. |
| Persistence | Save/load and change areas while fused; dismiss and re-fuse. Try Split Forms when available. | Same chosen appearance/build and canonical eidolon; no area-change recast, extra entity, equipment copy or injury reset. Split uses the same appearance as fusion. |

Preparation also has **Choose eidolon appearance**, organised into the same three browsing groups. Every model is available to every build. Changing appearance does not reopen evolution allocation. Applicable retraining remains necessary for mechanical changes outside advancement/respec. See [the full 59-model catalogue](SYNTHESIST_APPEARANCES.md), also included in the player ZIP.

## What this candidate does not establish

- Native selection navigation, backtracking, respec, view recreation and area persistence await these gameplay tests. Compilation and offline checks do not prove them.
- Appearance choices use model portrait icons and descriptions. A separate animated 3D preview screen is not implemented.
- The 59 appearance templates, new grouped menu pages and visual equipment guards are candidates; their native visuals and animations have not passed gameplay acceptance. A prefab's built-in details are retained, but separate weapons or armour from the source NPC's inventory are not copied or reconstructed. The chosen look may not show the weapon that supplies your actual attacks.
- Canonical eidolon feat/skill advancement remains in its native pet level-up during preparation; those choices are not moved onto the protagonist's creation screen.
- The original evolution catalogue and full Split/Twin, combat, lifecycle and compatibility matrices remain incomplete. The pending isolated alignment-DR repair is included, with no new native damage-test pass claimed.
- Existing prototype saves without a saved build plan retain their current canonical build/appearance. Their next Summoner advancement/respec can adopt the new flow; creation choices are not silently assigned on loading.
