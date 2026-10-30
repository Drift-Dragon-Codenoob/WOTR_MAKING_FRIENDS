# Making Friends baseline repairs

30 October 2026. Fork version **0.2.4.2**, branch `baseline-cleanup`. Gameplay repair checkpoint: 0.2.4.1; 0.2.4.2 repairs the installer archive and a malformed component identifier discovered during startup testing.

This is a repair of Balkoth's existing Summoner mod, based on upstream commit `34c2940bc1d3a01b68008d96b35764cee1679b64`. It does not implement the Synthesist archetype yet. Balkoth's authorship and MIT licence are retained. The message in `MESSAGE_TO_BALKOTH.md` remains a draft; it has not been sent.

The source repairs, offline verification, startup and class-list checks are complete. **Broader gameplay acceptance is pending.** Successful startup and isolated tests do not certify Unity lifecycle behaviour, existing-save migration or compatibility with other mods.

## Agreed rules

- Life Link and Life Bond protect against ordinary HP damage at a **1 HP** floor. They do not grant blanket protection against instant-death effects. This is separate from any future Synthesist collapse rule.
- Claws, Pincers, Slam and Tentacle Mass are alternative primary attack sets: reserve the largest set once, then add simultaneous extra attacks. The upstream weapon-bearing arms limit is retained; its rank accounting is corrected.
- Growth follows its existing description: Large at level 8 for four points; Huge at level 13 for six additional points; natural-armour increases of +2 and +5 total; biped reach increases of +5 and +10 feet total, with other forms gaining +5 only at Huge. Strength and Constitution evolution purchases made while Large or larger cost twice as much. Growth requires Medium, then Large current size. Existing cosmetic size markers remain separate from actual size.

## Repairs

The identifiers below correspond to the original [audit](MAKING_FRIENDS_CODE_AUDIT.md). These are implemented source changes, not claims of fourteen reproduced and resolved game symptoms.

| Audit group | Implemented repair |
|---|---|
| P1-01 Life Bond | Replace collection-mutating health/death handlers with a pre-commit HP-damage adapter. Allocate affordable transfers without changing an enumerated collection; donors retain 1 HP. Prevent reciprocal transfer loops. |
| P1-02 Life Link | Use the agreed 1 HP floor, validate master availability, count the HP actually paid and remove blanket `Infallible` ownership. The Summoner can still die from paying a Life Link transfer, as in the original implementation. A master at zero or negative HP cannot fund another rescue. |
| P1-03 Component identities | Assign distinct component TypeIds and check them against the installed game. This also uncovered three additional collisions with native components: secondary attack limbs, equipment locking and weapon-size adjustment. Blueprint GUIDs are unchanged. |
| P1-04 Summon pools | Limit the caster-specific clearing override to Making Friends' Summoner and Draconic Ally pools. Other pools retain native handling; absent contexts use native handling. |
| P1-05 Base evolutions | Assign base groups after copying, so Transmogrify does not classify inherited evolutions as purchased ones. |
| P2-06 Growth | Check the old rank before charging the second-purchase surcharge, fix level boundaries, implement +2/+5 armour totals and form-dependent reach, double subsequent Strength/Constitution purchase costs at Large or greater, and copy original-size feature data from the actual source rather than itself. Greater Aspect shared costs use the matching rounded discount. |
| P2-07 Attack capacity | Reserve two attacks once across alternative primary sets; debit Spike and additional natural-attack limbs. Charge arms only from the configured threshold rank. |
| P2-08 Resource isolation | Restore only the selected evolution or attack-budget resource on owned eidolons. Do not refill unrelated daily abilities or other pets. |
| P2-09 Transmogrify | Use the caster's own feature collection in caster branches, skip duplicate self-processing and preserve base groups. |
| P2-10 Greater Shield Ally | Remove the same master buff that the aura applies. |
| P2-11 Bonded Senses | Compare effective Perception totals, use an owned removable modifier and watch the relevant stats, level-up, spawn and area events. |
| P2-12 Spell registration | Replace character-class destinations with actual spell lists and deduplicate shared Wizard, Cleric and Bard list entries. |
| P2-13 Base armour | Grant the advertised +2 natural armour once through each base-form feature. External armour-stacking patches still require compatibility testing. |
| P2-14 Diagnostics | Preserve Release error logs, report patch-loading failure, detect registration errors and prevent retries over a partially initialised registry. |

Additional corrections: exclude pets from selecting Summoner; fix starting-feature description assignment; inspect the correct secondary hand when checking unarmed attacks; provide a safe action caption; validate missing action targets; require stable registered blueprint identifiers instead of generating random identifiers at runtime.

The HP adapter checks the active `RuleDealDamage` and the target's `LastHandledDamage` at the HP setter. Installed-engine inspection confirms this point follows difficulty scaling and temporary HP absorption. A separate difficulty hook bypasses repeat scaling only for the exact transfer event created by this adapter. Other damage events follow the normal engine path. Actual recipient durability loss, including temporary HP, determines the amount removed from the original hit.

## Verification

- Production builds target .NET Framework 4.8 and the installed WotR 2.7.0x assemblies. Debug and merged Release builds succeeded with zero warnings and errors.
- **863 offline regression assertions passed**, linking production components directly to small engine stand-ins. They cover rank boundaries, resource isolation, target/caster resets, primary alternatives, arms thresholds, Growth size/cost/reach rules and HP transfers including overkill, temporary HP, missing/dead masters, insufficient donors, reciprocal links, fake damage and difficulty scaling. Four hundred deterministic allocation samples contribute 800 of these assertions; this is not a count of distinct game scenarios.
- The release verification script passed 3,723 assembly/source consistency assertions covering embedded BlueprintCore and required publicised-access metadata, HP hook presence and native damage order, custom/native TypeId validity and uniqueness, registry uniqueness, literal identifier references, base-group copy order and spell-list destinations. This count includes individual registry and native-type entries; it is not a count of gameplay tests.
- The original 865-entry blueprint GUID registry is preserved. No original game assemblies are distributed in the mod ZIP.

The release package contains the merged mod DLL, upstream assets and localisation, stable GUID registry, Info.json, this report, the upstream README, licence and developer-message draft. It requires Unity Mod Manager and **ModMenu 2.0.14 or newer**. It is built against the maintained [CasDragon release](https://github.com/CasDragon/ModMenu/releases/tag/2.0.14). ModMenu is a separate dependency and is not bundled in this ZIP.

The first 0.2.4.1 installation failed before loading gameplay code: the game mod folder contained only empty directories and ModMenu was absent. The original ZIP used Windows backslashes, and the installer's Ionic.Zip library misidentified its `Assets\` directory entry as a file. Version 0.2.4.2 writes portable paths, places metadata and the DLL directly at the ZIP root and checks actual extraction with both ZIP libraries. The earlier file-hash comparison normalised entry names and therefore missed the malformed path.

After extraction was corrected, actual startup found a 33-character weapon-size component TypeId introduced in the initial repairs. Its malformed format caused Harmony's attribute scan to throw before patching. The identifier is corrected and format validation is now part of the assembly checks.

The archived ModMenu 1.3.2 release then failed during blueprint-cache initialisation with `TypeLoadException: VTable setup of type ModMenu.Settings.Fix failed`. It could report Active before reaching that failure, demonstrating why mod-loader status alone was insufficient. The original maintainer's README redirects to CasDragon's maintained fork. ModMenu 2.0.14 was downloaded from that fork, its published SHA-256 digest checked, installed and used to rebuild Making Friends. The manifest now enforces that version. The earlier ModMenu files and failed startup logs are preserved under the ignored local `artifacts/installation-backups` directory. Existing saves were not changed.

A further Release-only build defect caused `FieldAccessException` when embedded BlueprintCore accessed `LocalizationPack.m_Strings`. The original project enabled the necessary compiler metadata only in Debug; the merge therefore omitted it from Release. `AllowUnsafeBlocks` is now enabled in both configurations so the primary module carries the `UnverifiableCode` marker and compiler-generated `SkipVerification` declaration. Both are checked in the merged assembly. This changes the mod build, not Windows security settings or the installed game assemblies.

**Successful runtime check on WotR 2.7.0x:** the repaired Release DLL and ModMenu 2.0.14 reached the main menu. Both the external installer and in-game manager recognised Making Friends; the in-game manager displayed all four mods active. The startup log reported 832 new Making Friends blueprints, with no initialisation exceptions. In a new custom-character preview, Summoner appeared between Sorcerer and Warpriest and its description and level-one class features loaded. The preview was left open for David; no character was completed and no campaign save was loaded or overwritten. This check does not validate combat, progression or old saves.

## Build and package

On this workstation:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\Build-Baseline.ps1
```

The process-specific execution-policy option does not change Windows' saved execution policy. The script accepts `-GamePath`, `-ModMenuDll`, `-DotNet` and optional `-InstallerZipDll` overrides. It builds Release, runs the probes, assembly/source and extraction checks, then creates `artifacts/WOTR_MAKING_FRIENDS-0.2.4.2-baseline.zip` and a SHA-256 file. It automatically uses the local Unity Mod Manager installer ZIP library when found. Build-only tooling is under `%LOCALAPPDATA%\CodexTooling\WotrBaseline`; it is not part of the game or package.

Normal builds do not deploy to the game. Any publicised game assembly is generated only under the ignored local `lib` directory. The old deployment target requires explicit `DeployToGame=true`.

## In-game acceptance still required

Use a disposable test character and separate test saves. The original source repair checkpoint did not install or launch the mod. The subsequent installation diagnosis and package correction are described above; full gameplay acceptance remains pending.

1. Confirm Unity Mod Manager reports both ModMenu and Making Friends loaded, the settings page appears, and registration completes without errors in `Player.log` and the UMM log.
2. Test a normal Summoner and each intended body form at levels 1, 4, 8, 12, 13, 14 and 20. Check level-one armour, progression and the absence of Summoner from pet class choices.
3. Buy Growth with exactly four points at level 8 and six points at level 13. Check actual size, stat totals, AC breakdown, reach and subsequent Strength/Constitution costs. Repeat the cost checks through Aspect and Greater Aspect.
4. Fill the attack limit using primary alternatives, Spike, natural-attack limbs and weapon-bearing arms. Compare single attacks and full attacks, including Bite's modes and mixed-hand equipment. Check that alternates remain selectable without consuming a second primary allowance.
5. Cast Transmogrify on self and on an eidolon with different purchased evolutions; repeat it. Starting evolutions must survive, purchased evolutions and shared costs must reset, and expended unrelated daily resources must remain expended.
6. Exercise Life Link and Life Bond separately and together: exact-zero hits, overkill, temporary HP on both sides, insufficient donor HP, detached eidolons, instant-death effects and half/double difficulty damage. Verify the combat log, HP totals, unconsciousness/death and removal of each toggle. Include other damage redirection/protection effects before relying on combined behaviour.
7. Compare Perception before/after summoning, stat buffs, levelling and dismissal. Move into/out of Greater Shield Ally range, dismiss the eidolon and verify aura removal.
8. Verify spell availability in the affected class lists, Prayer ally/enemy targeting, and that native summon pools still clear correctly. Preserve the distinction between intrinsic flight and the wings evolution.
9. Save/load, rest, change areas and respec. Check equipment slots, pet restoration, event subscriptions and removable modifiers. Repeat relevant checks with the installed ToyBox and RespecWrath versions.

**Existing Making Friends saves are not migration-certified.** Four component TypeIds changed to eliminate confirmed collisions; preserving blueprint GUIDs alone does not establish old-save compatibility. This workstation had no Making Friends installation to migrate. Do not treat this package as a tested drop-in upgrade for an existing campaign.

Tabletop Tweaks natural-armour behaviour, Additional Feats spell sharing, cosmetic model additions and the historical unconfirmed Prayer/attack reports are outside the confirmed source repairs. Their status is recorded in the audit; they have not been represented as resolved runtime bugs.
