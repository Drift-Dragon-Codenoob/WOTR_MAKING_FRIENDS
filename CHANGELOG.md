# Changelog

## Unreleased - Synthesist runtime candidate - 31 October 2026

- Add isolated original-PF1e progression, native archetype/form registration, canonical cross-scene eidolon storage, preparation/allocation controls and reversible fusion adapters.
- Connect native daily resources, eidolon temporary HP, Fused Link payments, Rejuvenate healing, Jump delivery and initial Split/Twin/lifecycle/compatibility systems. Full gameplay acceptance remains in progress.
- Isolate blueprint components and spell-list storage from ordinary Summoners; retain existing blueprint identifiers.
- Keep climb, swim and underwater-breathing evolutions unavailable following David's decision.
- Add offline rules probes and development-only native integration checks restricted to separate test characters. Record actual passes, failures and remaining release gates in `docs/SYNTHESIST_IMPLEMENTATION.md`.
- Internal candidates have been installed with backups for testing. No accepted 0.3.0 package or release-version change yet.
- Repair severe combat slowdown caused by repeatedly trying to remove action-bar-retained Aspect abilities. Preserve their native ownership and block legacy evolution menus/variants through the archetype guard. Add bounded development timing and a native action-bar notification regression check; see `docs/SYNTHESIST_PERFORMANCE.md`.

## 0.2.4.2 — Installer archive repair — 30 October 2026

- Replace Windows PowerShell `Compress-Archive` with explicit ZIP creation using forward-slash paths and root-level `Info.json` and DLL.
- Omit malformed directory entries. The installed Unity Mod Manager's Ionic.Zip library interpreted the original `Assets\` entry as a file and installation stopped with only empty directories created.
- Add package validation that rejects backslashes and wrapper folders, then extracts and compares file hashes using both .NET ZIP and the installer's own Ionic.Zip library when available.
- Gameplay rules are unchanged from 0.2.4.1. The previous package's hash check normalised paths and missed this installer failure.
- ModMenu remains a required separate dependency; the game installation was missing it during the first test.
- Correct the weapon-size component's 33-character TypeId found during the first successful extraction and actual game launch. Validate GUID syntax as well as uniqueness before packaging.
- Build against and require the maintained ModMenu 2.0.14. The archived 1.3.2 release failed during blueprint-cache initialisation on the installed WotR 2.7.0x build.
- Enable the publicised-member compilation metadata in Release as well as Debug. The merged Release DLL previously omitted BlueprintCore's required module/assembly metadata and failed when accessing the localisation pack. Add verification for both markers.
- Verified actual startup on WotR 2.7.0x with ModMenu 2.0.14: all four mods active, 832 Making Friends blueprints registered without initialisation exceptions, and Summoner visible with its description and level-one features in character creation. Broader gameplay and save migration remain unverified.

## 0.2.4.1 — Summoner baseline repairs — 30 October 2026

Source repair checkpoint: `2e801a9`, on `baseline-cleanup`, based on Balkoth's upstream commit `34c2940bc1d3a01b68008d96b35764cee1679b64`.

This release prepares the existing Summoner foundation for later Synthesist archetype work. Synthesist implementation has not started.

- Repair Life Link and Life Bond HP transfers, including the 1 HP floor, valid donor/master checks, temporary HP, reciprocal-loop prevention and repeat difficulty scaling.
- Resolve four component TypeId collisions while preserving all 865 blueprint GUID entries.
- Scope summon-pool clearing to the mod's own pools and preserve native handling elsewhere.
- Preserve inherited evolutions during Transmogrify and use the correct caster/target collections.
- Correct Growth purchase costs, level gates, armour, reach, size eligibility and subsequent Strength/Constitution evolution costs.
- Count alternative primary attack sets once, account for simultaneous extra attacks and correct the existing arms threshold calculation.
- Restrict resource restoration to the intended eidolon resource.
- Correct Greater Shield Ally cleanup, Bonded Senses modifier ownership, spell-list destinations and starting natural armour.
- Exclude pets from selecting Summoner and correct smaller description, hand-selection and action-safety defects.
- Preserve Release diagnostics, detect partial initialisation and require registered stable identifiers.
- Add isolated building, regression probes, assembly/source verification and packaging with licence notices and a SHA-256 file.

Recorded validation: Debug and merged Release builds passed with zero warnings/errors; 863 offline regression assertions and 3,687 assembly/source consistency assertions passed. All 40 packaged files matched their source hashes. These counts include repeated allocation samples and individual registry/type entries; they are not counts of distinct gameplay tests.

In-game acceptance, existing-save migration and compatibility testing remain outstanding. Four changed component TypeIds require explicit migration testing. No game installation or saves were changed. Balkoth's message remains an unsent draft.

See [the detailed repair report and game-test checklist](docs/BASELINE_REPAIRS.md), [the original audit](docs/MAKING_FRIENDS_CODE_AUDIT.md) and [modding research](docs/WOTR_MODDING_RESEARCH.md).
