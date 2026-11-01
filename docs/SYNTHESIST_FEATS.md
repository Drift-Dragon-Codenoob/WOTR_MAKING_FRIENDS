# Summoner and Synthesist feats

Development candidate, 1 November 2026. Four selectable feats are implemented; native UI, combat, respec and lifecycle acceptance remain David's manual tests. No automatic in-game tests are run.

These spend normal feat selections. They are not automatic archetype grants and are not imported from the canonical eidolon's feats. Rending Claws is also offered in the fighter combat-feat selection.

## Extra Evolution

Prerequisite: eidolon class feature. Each purchase adds one evolution point. The repeat limit increases at character levels 1, 5, 10, 15 and 20, reaching five selections. These levels unlock purchases, not free feats.

The saved character-build allowance and canonical eidolon's native resource both include the bonus once. Aspect allowance and natural-attack limits do not increase. Creation, advancement and respec can allocate it. Selecting it on a non-Summoner level also opens the Synthesist build pages. Restoration and fusion do not reopen allocation. Old ledgers default to zero bonus points.

Rules: [Extra Evolution, Ultimate Magic](https://aonprd.com/FeatDisplay.aspx?ItemName=Extra%20Evolution).

## Expanded Arcana

Prerequisite: caster level 1 and a spontaneous class with a limited list of spells known. Choose either one unknown spell up to the class's highest available spell level, or two unknown spells each at least one spell level lower. Repeatable.

Choose the class and allowance, then use the native parametrised spell pages. Each chosen spell is learned in the current native class spellbook at its original level. The two-spell choice has separate first/second pages. Actual replacement spell lists are used, and known spells are excluded. The native selector's unit-independent cache gets an unfiltered spell-list superset, rather than caching one character's known spells for everyone.

Class adapters cover Making Friends Summoner, Bard, Bloodrager, Hunter, Inquisitor, Oracle, Skald, Sorcerer, Magus, Witch and the game's separate Eldritch Scion class. A prepared book is ineligible; Magus/Witch entries allow qualifying spontaneous archetypes. Additional mod-added classes need their own adapter. Native spell learning/saving and spell consumption remain responsible for the spell, so ToyBox unlimited casting follows its ordinary path.

Rules: [Expanded Arcana, Advanced Player's Guide](https://aonprd.com/FeatDisplay.aspx?ItemName=Expanded%20Arcana).

## Rending Claws

Prerequisites: Strength 13, two claw natural attacks and BAB +6. Two claw hits against the same creature in one turn add 1d6 precision damage to the second hit, once per round. No critical multiplication. This is separate from the Rend evolution.

Selection can evaluate the chosen Synthesist fused build while dismissed, avoiding a dead end caused by the required level-up dismissal. It uses mechanical form, evolution purchases, advancement and applicable existing physical advancement; appearance supplies nothing. Combat still checks actual Strength, BAB and current claw weapons. Inactive weapon sets do not qualify. A synthetic Rend attack does not count as another claw hit.

The additional damage enters the original native damage bundle, preserving its normal mitigation, difficulty and precision-immunity handling. Hit/usage state is saved on the protagonist. Turn-based hits are separated by the native turn start; real-time combat uses the owner's combat-round event. Fusion, splitting and loading do not reset the once-per-round allowance.

Rules: [Rending Claws, Advanced Player's Guide](https://www.aonprd.com/FeatDisplay.aspx?ItemName=Rending%20Claws).

## Summoner's Call

Prerequisite: eidolon class feature. After actual summoning, an optional +2 enhancement bonus applies to Strength, Dexterity or Constitution for ten minutes. Enhancement bonuses to the same statistic do not stack.

The feat grants **Summoner's Call preference**. Choose Strength, Dexterity, Constitution or no bonus for the next summoning; Strength is the initial preference. Changing preference does not modify or refresh a current bonus. The bonus starts only after successful fusion, not when preparation opens or the ritual starts. An ordinary Making Friends eidolon's actual new spawn also receives it when its owner already has the feat; load reconciliation of an existing pet does not refresh it.

The canonical eidolon owns the timed native buff. Fusion projects its remaining duration onto the protagonist. Dismissal removes that projection; Split Forms retains the original eidolon bonus and rejoining uses its remaining time. A saved expiry deadline lets dormant canonical buffs expire without a per-frame buff scan. Constitution changes follow the existing wound-preserving pool calculation. No rest/after-combat hook refills the duration, heals wounds or changes the chosen evolutions.

Rules: [Summoner's Call, Advanced Player's Guide](https://aonprd.com/FeatDisplay.aspx?ItemName=Summoner%27s%20Call).

## Focused player acceptance

Use separate test saves; keep your usual ToyBox settings enabled.

1. **Extra Evolution:** take it at creation. Expect four initial evolution points, normal Aspect allowance and the normal attack cap. Allocate all four, then confirm, prepare, fuse, dismiss and save/load. Check the bonus remains one point. Check repeat availability below/at the next threshold and removal during respec.
2. **Expanded Arcana:** take the one-spell choice and verify the chosen spell is known at its original level. At a higher spell tier, test the two-lower-spell choice and two different unknown spells. Highest-tier and already-known spells must be unavailable in that option. Cancel once before committing; the live spellbook must not change. Save/load and cast with ToyBox unlimited casting on. A separate qualifying ordinary spontaneous caster should also see the feat.
3. **Rending Claws:** use an advanced claw build meeting every prerequisite. Two hits against the same target in one turn should add one 1d6 precision entry; misses, different targets, later hits and critical hits must not produce extra applications. Check precision immunity, both combat modes, the next-round reset and a mid-round save/load. Changing the appearance or using manufactured weapons must not create qualifying claws.
4. **Summoner's Call:** select each preference before a fresh ritual. Expect only the selected +2 enhancement after completion. Change the preference during that bonus: the current statistic and remaining duration must stay unchanged. Check no stacking with a stronger enhancement effect, dismissal/rejoining, save/load, area changes, ten-minute expiry, and Constitution/pool behaviour. Preparation, Split Forms and ToyBox after-combat restoration must not refresh it.

The complete archetype remains a development candidate. Passing offline assertions does not accept these game scenarios.
