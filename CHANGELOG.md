# Changelog

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
