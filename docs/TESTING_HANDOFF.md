# Synthesist player handoff

1 November 2026. Development candidate on `synthesist`, including reviewed code checkpoint `91f63c3`. The packaged `BUILD_INFO.json` records the exact subsequent build/documentation revision and DLL hash. This is not the accepted 0.3.0 release.

David handles all in-game testing. Codex may prepare/install the package and launch WotR through Steam when requested; no automated character creation, combat, native acceptance probes or profiling is part of this handoff. Installation and save backups are kept under `artifacts/`, with the latest receipt location in `artifacts/latest-player-install.txt`.

## First test — one biped, one fusion cycle

Use a separate test character/save. Keep your normal ToyBox unlimited casting and combined after-combat restoration enabled, with Respec Mod and ModMenu installed.

1. Choose **Summoner → Synthesist → Biped**. Take an ordinary feat such as **Alertness** for this baseline test; Extra Evolution would change its starting point allowance.
2. Choose **Allocate or rebuild eidolon evolutions**. Buy **Improved Natural Armor** for one point, then **Ability Increase: Strength (+2)** for two. Expect three points, then two, then zero.
3. Finish evolutions. Choose the **Biped appearance group**, then **Incubus** for this first cycle. All three groups should be available regardless of mechanical form. Review and confirm.
4. Complete creation. Use **Prepare Eidolon (build only)**. A separate eidolon is expected during preparation. Select its pet portrait strip and finish required native level-up choices, including its feat/skills and class-skill choices.
5. Use **Confirm Eidolon Build**. The separate eidolon should disappear. Use **Summon Fused Eidolon** and allow **one minute of unpaused game time**. Your existing protagonist should take the chosen form, with no separate eidolon combatant.
6. Check your identity, inventory, mental statistics and spells remain yours. Dismiss; your ordinary form and equipment display should return. Save/load and repeat fusion once, checking that attacks, bonuses and injuries do not accumulate or reset.

Report the first failing step and any error text. If a required page or action is unavailable, stop there. Report whether the point balances were correct, confirmation removed the pet, the full ritual fused you and dismissal restored you.

## After that loop passes

- Try **Nabasu** or **Gargoyle** in preparation's appearance controls for the upright winged look. Their animations are candidates, not accepted results.
- Test the four feats using `SYNTHESIST_FEATS.md`. Extra Evolution should give four initial points on a separate creation test.
- Then test advancement, cancellation/respec, damage/healing, the other forms, Split Forms and Twin Eidolon in small batches. The installed folder's `PLAYER_TEST.md` has the wider cases.

## Current limits

Cosmetics use existing prefab details; equipped items are not overlaid, and appearances grant no limbs, attacks or flight. Climb/swim/underwater evolutions remain unavailable by decision. Unknown somatic spells remain allowed until verified. Level-up/respec and other transformations require dismissal.

Original evolution coverage, some attack/spell rules, Split effect counters and Twin racial-trait handling still have recorded release gaps. Passing this first loop does not accept the complete archetype. See the repository's code review and implementation report before planning release.
