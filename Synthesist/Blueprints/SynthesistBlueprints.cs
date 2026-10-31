using BlueprintCore.Blueprints.Configurators;
using BlueprintCore.Blueprints.Configurators.Classes;
using BlueprintCore.Blueprints.Configurators.Classes.Spells;
using BlueprintCore.Blueprints.CustomConfigurators.Classes.Spells;
using BlueprintCore.Blueprints.Configurators.UnitLogic.ActivatableAbilities;
using BlueprintCore.Blueprints.CustomConfigurators;
using BlueprintCore.Blueprints.CustomConfigurators.Classes;
using BlueprintCore.Blueprints.CustomConfigurators.Classes.Selection;
using BlueprintCore.Blueprints.CustomConfigurators.UnitLogic.Abilities;
using BlueprintCore.Blueprints.CustomConfigurators.UnitLogic.Buffs;
using BlueprintCore.Blueprints.References;
using BlueprintCore.Utils;
using Kingmaker.Blueprints;
using Kingmaker.Blueprints.Classes;
using Kingmaker.Blueprints.Facts;
using Kingmaker.EntitySystem.Stats;
using Kingmaker.Enums;
using Kingmaker.UnitLogic.Abilities.Blueprints;
using Kingmaker.UnitLogic.Buffs.Blueprints;
using Kingmaker.UnitLogic.FactLogic;
using Kingmaker.UnitLogic;
using Kingmaker.Utility;
using System;
using System.Collections.Generic;
using System.Linq;
using WOTR_MAKING_FRIENDS.GUIDs;
using WOTR_MAKING_FRIENDS.Synthesist.Rules;
using WOTR_MAKING_FRIENDS.Synthesist.Runtime;
using WOTR_MAKING_FRIENDS.Utilities;
using CommandType = Kingmaker.UnitLogic.Commands.Base.UnitCommand.CommandType;

namespace WOTR_MAKING_FRIENDS.Synthesist.Blueprints
{
    internal static class SynthesistBlueprints
    {
        internal static bool Ready { get; private set; }
        // Registration in ordinary builds remains closed until the complete gameplay
        // graph passes acceptance. Internal native-test builds explicitly opt in.
#if SYNTHESIST_ACCEPTANCE
        internal static bool EnableDevelopmentGraph => true;
#else
        internal static bool EnableDevelopmentGraph => false;
#endif
        internal static Kingmaker.Blueprints.Classes.Spells.BlueprintSpellbook Spellbook;
        internal static BlueprintCharacterClass SummonerClass => BlueprintTool.Get<BlueprintCharacterClass>(GetGUID.GUIDByName("SummonerClass"));
        internal static BlueprintBuff PreparationBuff => Buff("SynthesistPreparationBuff");
        internal static readonly Dictionary<string, BlueprintFeature> ProjectionFeatures = new();
        internal static readonly Dictionary<string, BlueprintFeature> PurchasedFeatures = new();
        internal static readonly Dictionary<string, BlueprintAbility> EvolutionAbilities = new();
        internal static string Id(string name) => GetGUID.GUIDByName(name);
        internal static BlueprintFeature ExistingFeature(string name) => BlueprintTool.Get<BlueprintFeature>(Id(name));
        internal static BlueprintFeature BuildFeature(string name) => PurchasedFeatures.TryGetValue(name, out var feature) ? feature : ExistingFeature(name);
        internal static BlueprintBuff Buff(string name) => BlueprintTool.Get<BlueprintBuff>(Id(name));
        internal static BlueprintUnit Pet(EidolonForm form) => BlueprintTool.Get<BlueprintUnit>(Id("Synthesist" + form + "Unit"));
        internal static BlueprintAbilityResource Resource(DailyAbility ability)
            => BlueprintTool.Get<BlueprintAbilityResource>(Id("Synthesist" + ability + "Resource"));
        internal static bool IsOwnedBuff(BlueprintBuff buff) => buff.name.StartsWith("Synthesist", StringComparison.Ordinal);
        internal static bool IsFusionBuff(BlueprintBuff buff) => buff.name.StartsWith("SynthesistFusion", StringComparison.Ordinal);
        internal static Kingmaker.Localization.LocalizedString Text(string key, string value)
            => Helpers.CreateString("wotr-making-friends.synthesist." + key, value);

        internal static void Create()
        {
            foreach (DailyAbility ability in Enum.GetValues(typeof(DailyAbility)))
                AbilityResourceConfigurator.New("Synthesist" + ability + "Resource", Id("Synthesist" + ability + "Resource"))
                    .SetMaxAmount(ResourceAmountBuilder.New(0).Build()).ConfigureWithLogging();

            var petProgression = ProgressionConfigurator.New("SynthesistEidolonProgression", Id("SynthesistEidolonProgression"))
                .ConfigureWithLogging();
            var petClass = CharacterClassConfigurator.New("SynthesistEidolonClass", Id("SynthesistEidolonClass"))
                .SetLocalizedName(Text("eidolon.name", "Eidolon"))
                .SetLocalizedDescription(Text("eidolon.description", "The Synthesist's original eidolon progression."))
                .SetHitDie(Kingmaker.RuleSystem.DiceType.D10)
                .SetSkillPoints(6).SetHideIfRestricted(true)
                .SetClassSkills(StatType.SkillPerception, StatType.SkillStealth, StatType.SkillPersuasion, StatType.SkillKnowledgeArcana)
                .SetBaseAttackBonus(StatProgressionRefs.BABFull.Reference.Get())
                .SetFortitudeSave(StatProgressionRefs.SavesHigh.Reference.Get()).SetReflexSave(StatProgressionRefs.SavesLow.Reference.Get())
                .SetWillSave(StatProgressionRefs.SavesHigh.Reference.Get())
                .SetProgression(petProgression).AddPrerequisiteIsPet().ConfigureWithLogging();
            ProgressionConfigurator.For(Id("SynthesistEidolonProgression")).SetClasses(petClass).ConfigureWithLogging(true);
            CreateEidolonClassSkillChoices();
            var root = Kingmaker.Blueprints.Root.BlueprintRoot.Instance;
            root.Progression.m_PetClasses = CommonTool.Append(root.Progression.m_PetClasses, petClass.ToReference<BlueprintCharacterClassReference>());

            BuffConfigurator.New("SynthesistPreparationBuff", Id("SynthesistPreparationBuff"))
                .SetDisplayName(Text("preparation.name", "Eidolon Preparation"))
                .SetDescription(Text("preparation.description", "Manage the eidolon's build outside combat."))
                .AddCondition(UnitCondition.CantMove).AddCondition(UnitCondition.CanNotAttack).ConfigureWithLogging();
            BuffConfigurator.New("SynthesistRitualBuff", Id("SynthesistRitualBuff"))
                .SetDisplayName(Text("ritual.name", "Summoning Fused Eidolon"))
                .SetDescription(Text("ritual.description", "The fusion ritual takes one minute, or ten combat rounds. The Summoner cannot take other actions during the ritual."))
                .AddCondition(UnitCondition.CantAct).ConfigureWithLogging();

            CreateProjectionFeatures();
            foreach (EidolonForm form in Enum.GetValues(typeof(EidolonForm))) CreateForm(form, petClass);
            CreateActionAbilities();

            var features = new Dictionary<string, BlueprintFeatureBase>();
            foreach (var grant in ArchetypeProgression.Additions.Where(g => g.Feature != "SynthesistBaseFormSelection"))
            {
                var cfg = FeatureConfigurator.New(grant.Feature, Id(grant.Feature))
                    .SetDisplayName(Text(grant.Feature + ".name", FeatureName(grant.Feature)))
                    .SetDescription(Text(grant.Feature + ".description", FeatureDescription(grant.Feature)))
                    .SetIsClassFeature(true);
                var abilities = AbilitiesFor(grant.Feature).Select(n => (Blueprint<BlueprintUnitFactReference>)BlueprintTool.GetRef<BlueprintUnitFactReference>(Id(n))).ToList();
                if (abilities.Count > 0) cfg.AddFacts(abilities);
                if (grant.Feature == "SynthesistFusedEidolonFeature")
                    cfg.AddComponent<SynthesistController>();
                if (grant.Feature == "SynthesistMakersJumpFeature") cfg.AddAbilityResources(1, Resource(DailyAbility.MakersJump), true, true);
                if (grant.Feature == "SynthesistSplitFormsFeature") cfg.AddAbilityResources(1, Resource(DailyAbility.SplitForms), true, true);
                if (grant.Feature == "SynthesistTwinEidolonFeature") cfg.AddAbilityResources(1, Resource(DailyAbility.TwinEidolon), true, true);
                features[grant.Feature] = cfg.ConfigureWithLogging();
            }
            var forms = Enum.GetValues(typeof(EidolonForm)).Cast<EidolonForm>()
                .Select(f => (Blueprint<BlueprintFeatureReference>)BlueprintTool.GetRef<BlueprintFeatureReference>(Id("Synthesist" + f + "FormFeature"))).ToArray();
            features["SynthesistBaseFormSelection"] = FeatureSelectionConfigurator.New("SynthesistBaseFormSelection", Id("SynthesistBaseFormSelection"))
                .SetDisplayName(Text("form.name", "Eidolon Base Form"))
                .SetDescription(Text("form.description", "Choose biped, quadruped or serpentine."))
                .SetAllFeatures(forms).ConfigureWithLogging();
            CreateHealingSpells();
            ArchetypeRegistrar.Register(features);
            Ready = true;
        }

        private static void CreateForm(EidolonForm form, BlueprintCharacterClass petClass)
        {
            string sourceName = form == EidolonForm.Biped ? "EidolonIncubus" : form == EidolonForm.Quadruped ? "EidolonHellHound" : "EidolonMarilith";
            var definition = BaseForms.For(form);
            var petArchetype = ArchetypeConfigurator.New("Synthesist" + form + "PetArchetype", Id("Synthesist" + form + "PetArchetype"))
                .SetLocalizedName(Text(form + ".name", form.ToString()))
                .SetFortitudeSave(definition.GoodFortitude ? StatProgressionRefs.SavesHigh.Reference.Get() : StatProgressionRefs.SavesLow.Reference.Get())
                .SetReflexSave(definition.GoodReflex ? StatProgressionRefs.SavesHigh.Reference.Get() : StatProgressionRefs.SavesLow.Reference.Get())
                .SetWillSave(definition.GoodWill ? StatProgressionRefs.SavesHigh.Reference.Get() : StatProgressionRefs.SavesLow.Reference.Get())
                .ConfigureWithLogging();
            petArchetype.m_ParentClass = petClass;
            petClass.m_Archetypes = CommonTool.Append(petClass.m_Archetypes, petArchetype.ToReference<BlueprintArchetypeReference>());
            var facts = new List<Blueprint<BlueprintUnitFactReference>>
            {
                FeatureRefs.OutsiderType.Cast<BlueprintUnitFactReference>().Reference,
                Id("EidolonSubtypeFeature"), Id("Eidolon" + form + "BaseFormFeature"),
                Id("AddEvolutionPointsFeature"), Id("AddMaxAttacksFeature"), Id("SynthesistEvolutionControlsFeature")
            };
            foreach (var evolution in definition.FreeEvolutions)
            {
                if (evolution.Key == "Climb") continue; // Explicitly unavailable in this release by David's decision.
                string name = evolution.Key == "ReachBite" ? "EvolutionReach" : "Evolution" + evolution.Key;
                for (int i = 0; i < evolution.Ranks; i++) facts.Add(Id(name + "BaseFeature"));
            }
            var unit = UnitConfigurator.New("Synthesist" + form + "Unit", Id("Synthesist" + form + "Unit"))
                .CopyFrom(Id(sourceName), c => false).AddAllowDyingCondition()
                .SetStrength(definition.Strength).SetDexterity(definition.Dexterity).SetConstitution(definition.Constitution)
                .SetIntelligence(7).SetWisdom(10).SetCharisma(11).SetSize(Size.Medium)
                .SetFaction(Kingmaker.Blueprints.Root.BlueprintRoot.Instance.PlayerFaction.ToReference<BlueprintFactionReference>())
                .SetSpeed((definition.SpeedFeet - definition.FreeEvolutions.Where(e => e.Key == "LimbsLegs").Sum(e => e.Ranks) * 10).Feet())
                .SetBody(new BlueprintUnit.UnitBody
                {
                    DisableHands = form != EidolonForm.Biped,
                    EmptyHandWeapon = ItemWeaponRefs.WeaponEmptyHand.Cast<BlueprintItemWeaponReference>().Reference
                })
                .SetAddFacts(facts.ToArray()).ConfigureWithLogging();
            unit.MaxHP = 0; // HP comes from the isolated d10 class, not the model's NPC template.
            unit.AddComponents(new AddClassLevels
            {
                // Preparation's native first level-up owns the feat, skill ranks and
                // four permanent class-skill choices. Do not auto-pick those on spawn.
                m_CharacterClass = petClass.ToReference<BlueprintCharacterClassReference>(), Levels = 0,
                LevelsStat = StatType.Strength, RaceStat = StatType.Unknown,
                Skills = new[] { StatType.SkillPerception }, Selections = Array.Empty<SelectionEntry>(),
                m_Archetypes = new[] { petArchetype.ToReference<BlueprintArchetypeReference>() },
                m_SelectSpells = Array.Empty<BlueprintAbilityReference>(), m_MemorizeSpells = Array.Empty<BlueprintAbilityReference>()
            });
            FeatureConfigurator.New("Synthesist" + form + "FormFeature", Id("Synthesist" + form + "FormFeature"))
                .SetDisplayName(Text(form + ".name", form.ToString()))
                .SetDescription(Text(form + ".description", $"{definition.Strength} Strength, {definition.Dexterity} Dexterity, 13 Constitution; {definition.SpeedFeet} ft. movement."))
                .AddComponent<SynthesistForm>(c => c.Form = form).ConfigureWithLogging();
            BuffConfigurator.New("SynthesistFusion" + form + "Buff", Id("SynthesistFusion" + form + "Buff"))
                .SetDisplayName(Text("fusion.name", "Fused Eidolon"))
                .SetDescription(Text("fusion.description", "Summoner and eidolon act as one creature."))
                .AddMechanicsFeature(AddMechanicsFeature.MechanicsFeatureType.NaturalSpell)
                .AddMechanicsFeature(AddMechanicsFeature.MechanicsFeatureType.PolymorphSpellcasting)
                .AddComponent<Kingmaker.UnitLogic.Buffs.Polymorph>(c =>
                {
                    c.m_Prefab = unit.Prefab; c.m_PrefabFemale = unit.Prefab;
                    c.m_EnterTransition = EmptyTransition();
                    c.m_ExitTransition = EmptyTransition();
                    c.Size = Size.Medium; c.m_KeepSlots = true; c.m_SilentCaster = false;
                    c.m_Facts = Array.Empty<BlueprintUnitFactReference>();
                    c.m_AdditionalLimbs = Array.Empty<BlueprintItemWeaponReference>();
                    c.m_SecondaryAdditionalLimbs = Array.Empty<BlueprintItemWeaponReference>();
                }).ConfigureWithLogging();
        }

        private static Kingmaker.UnitLogic.Buffs.Polymorph.VisualTransitionSettings EmptyTransition() => new()
        {
            OldPrefabFX = new Kingmaker.ResourceLinks.PrefabLink(),
            NewPrefabFX = new Kingmaker.ResourceLinks.PrefabLink()
        };

        private static string FeatureName(string name) => name.Replace("Synthesist", "").Replace("Feature", "") switch
        {
            "FusedEidolon" => "Fused Eidolon", "FusedLink" => "Fused Link", "ShieldedMeld" => "Shielded Meld",
            "MakersJump" => "Maker's Jump", "GreaterShieldedMeld" => "Greater Shielded Meld",
            "SplitForms" => "Split Forms", "TwinEidolon" => "Twin Eidolon", var text => text
        };
        private static string FeatureDescription(string name) => name switch
        {
            "SynthesistFusedLinkFeature" => "When eidolon temporary HP would be exhausted, Fused Link may sacrifice personal HP to preserve it. Enable the action-bar toggle and configure your personal HP limit in mod settings.",
            "SynthesistShieldedMeldFeature" => "While fused, gain +2 shield AC and +2 circumstance saves.",
            "SynthesistGreaterShieldedMeldFeature" => "While fused, gain +4 shield AC and +4 circumstance saves, replacing Shielded Meld.",
            "SynthesistSplitFormsFeature" => "Separate as a swift action for rounds equal to Summoner level per day. Both creatures retain evolutions and existing effects. Rejoin with a full-round action.",
            "SynthesistTwinEidolonFeature" => "Assume the eidolon's form for 20 minutes per day, spent in one-minute increments, without gaining eidolon temporary HP.",
            "SynthesistMakersJumpFeature" => "Dimension Door affecting only the fused character, once per day at level 6, twice at 12 and three times at 18.",
            _ => "Summon the eidolon's essence in a one-minute ritual. Use its physical statistics, base attack bonus and evolutions while retaining your mind, skills and spellcasting. Worn armour grants no benefit."
        };

        private static IEnumerable<string> AbilitiesFor(string feature) => feature switch
        {
            "SynthesistFusedEidolonFeature" => new[] { "SynthesistPrepareAbility", "SynthesistConfirmAbility", "SynthesistResetAbility", "SynthesistFuseAbility", "SynthesistDismissAbility" },
            "SynthesistFusedLinkFeature" => new[] { "SynthesistLinkAbility" },
            "SynthesistMakersJumpFeature" => new[] { "SynthesistJumpAbility" },
            "SynthesistSplitFormsFeature" => new[] { "SynthesistSplitAbility", "SynthesistRejoinAbility" },
            "SynthesistTwinEidolonFeature" => new[] { "SynthesistTwinAbility", "SynthesistEndTwinAbility" },
            _ => Array.Empty<string>()
        };

        private static void CreateActionAbilities()
        {
            BuffConfigurator.New("SynthesistLinkPermissionBuff", Id("SynthesistLinkPermissionBuff"))
                .SetDisplayName(Text("link.name", "Fused Link"))
                .SetDescription(Text("link.description", "Personal HP sacrifice is authorised up to the configured limit."))
                .SetIcon(ExistingFeature("SummonerLifeLinkFeature").m_Icon)
                .AddComponent<FusedLinkPermission>().ConfigureWithLogging();
            ActivatableAbilityConfigurator.New("SynthesistLinkAbility", Id("SynthesistLinkAbility"))
                .SetDisplayName(Text("link.name", "Fused Link"))
                .SetDescription(Text("link.toggle.description", "Toggle permission to sacrifice personal HP to preserve the eidolon. Configure the minimum personal HP in ModMenu."))
                .SetIcon(ExistingFeature("SummonerLifeLinkFeature").m_Icon)
                .SetBuff(Id("SynthesistLinkPermissionBuff"))
                .SetActivationType(Kingmaker.UnitLogic.ActivatableAbilities.AbilityActivationType.Immediately)
                .SetActivateWithUnitCommand(CommandType.Free).ConfigureWithLogging();
            foreach (SynthAction action in Enum.GetValues(typeof(SynthAction)))
            {
                if (action == SynthAction.Rejuvenate || action == SynthAction.Link) continue;
                string name = "Synthesist" + action + "Ability";
                if (action == SynthAction.Jump)
                {
                    var jump = AbilityConfigurator.New(name, Id(name))
                        .CopyFrom(AbilityRefs.DimensionDoorCasterOnly, c => false)
                        .SetDisplayName(Text(name + ".name", "Maker's Jump"))
                        .SetDescription(Text(name + ".description", FeatureDescription("SynthesistMakersJumpFeature")))
                        .SetType(AbilityType.SpellLike).SetActionType(CommandType.Standard)
                        .AddComponent<SynthesistRestriction>(c => c.Action = action)
                        .AddAbilityResourceLogic(1, isSpendResource: true, requiredResource: Resource(DailyAbility.MakersJump))
                        .ConfigureWithLogging();
                    jump.AddComponents(IsolatedCopy.Components(AbilityRefs.DimensionDoorCasterOnly.Reference.Get(), c =>
                        c is not Kingmaker.UnitLogic.Abilities.Components.AbilityResourceLogic && c is not Kingmaker.Blueprints.Classes.Spells.SpellListComponent));
                    jump.m_Parent = null;
                    continue;
                }
                var cfg = AbilityConfigurator.New(name, Id(name)).SetDisplayName(Text(name + ".name", action switch
                {
                    SynthAction.Prepare => "Prepare Eidolon", SynthAction.Confirm => "Confirm Eidolon Build",
                    SynthAction.Reset => "Reallocate Evolutions",
                    SynthAction.EndTwin => "End Twin Eidolon",
                    SynthAction.Fuse => "Summon Fused Eidolon", SynthAction.Dismiss => "Dismiss Eidolon Form",
                    SynthAction.Link => "Toggle Fused Link", SynthAction.Jump => "Maker's Jump",
                    SynthAction.Split => "Split Forms", SynthAction.Rejoin => "Rejoin Forms", _ => "Twin Eidolon"
                })).SetDescription(Text(name + ".description", "Use the Synthesist's " + action + " ability."))
                    .SetType(AbilityType.Supernatural).SetRange(AbilityRange.Personal)
                    .SetActionType(action == SynthAction.Split ? CommandType.Swift : action is SynthAction.Link or SynthAction.Prepare or SynthAction.Confirm or SynthAction.Reset or SynthAction.EndTwin ? CommandType.Free : CommandType.Standard)
                    .AddComponent<SynthesistRestriction>(c => c.Action = action);
                cfg.SetIcon(ExistingFeature("SummonerMakersCallFeature").m_Icon);
                if (action == SynthAction.Jump) cfg.SetRange(AbilityRange.Long).SetCanTargetPoint(true).SetCanTargetSelf(false);
                if (action == SynthAction.Fuse) cfg.AddComponent<FusionRitual>();
                else cfg.AddAbilityEffectRunAction(new Kingmaker.ElementsSystem.ActionList
                { Actions = new Kingmaker.ElementsSystem.GameAction[] { new SynthesistAction { Action = action } } });
                var ability = cfg.ConfigureWithLogging();
                if (action == SynthAction.Rejoin) ability.m_IsFullRoundAction = true;
            }
        }

        private static void CreateProjectionFeatures() => EvolutionIntegration.Create();

        private static void CreateEidolonClassSkillChoices()
        {
            var choices = new List<Blueprint<BlueprintFeatureReference>>();
            var skills = new[] { StatType.SkillAthletics, StatType.SkillMobility, StatType.SkillThievery,
                StatType.SkillKnowledgeWorld, StatType.SkillLoreNature, StatType.SkillLoreReligion, StatType.SkillUseMagicDevice };
            string[] labels = { "Athletics", "Mobility", "Trickery", "Knowledge (World)", "Lore (Nature)", "Lore (Religion)", "Use Magic Device" };
            for (int i = 0; i < skills.Length; i++)
            {
                string name = "SynthesistClassSkill" + skills[i] + "Feature";
                var feature = FeatureConfigurator.New(name, Id(name))
                    .SetDisplayName(Text(name + ".name", labels[i]))
                    .SetDescription(Text(name + ".description", "This is a permanent class skill for the canonical eidolon. Fusion does not grant its skill ranks or class skills to the Summoner."))
                    .SetIsClassFeature(true).SetRanks(1).AddClassSkill(skills[i])
                    .AddPrerequisiteNoFeature(Id(name)).ConfigureWithLogging();
                choices.Add(feature.ToReference<BlueprintFeatureReference>());
            }
            var selections = new List<Blueprint<BlueprintFeatureBaseReference>>();
            for (int i = 1; i <= 4; i++)
            {
                string name = "SynthesistAdditionalClassSkill" + i + "Selection";
                var selection = FeatureSelectionConfigurator.New(name, Id(name))
                    .SetDisplayName(Text(name + ".name", "Additional eidolon class skill " + i))
                    .SetDescription(Text("eidolon.classskills.description", "Choose four different additional class skills at the eidolon's first level-up. Perception, Stealth, Persuasion and Knowledge (Arcana) are already class skills. These choices remain fixed through evolution retraining."))
                    .SetIsClassFeature(true).SetRanks(1).SetAllFeatures(choices.ToArray()).ConfigureWithLogging();
                selections.Add(selection.ToReference<BlueprintFeatureBaseReference>());
            }
            ProgressionConfigurator.For(Id("SynthesistEidolonProgression")).AddToLevelEntry(1, selections.ToArray()).ConfigureWithLogging(true);
        }
        private static void CreateHealingSpells()
        {
            var list = SpellListConfigurator.New("SynthesistSpellList", Id("SynthesistSpellList"))
                .CopyFrom(SummonerClass.Spellbook.SpellList).ConfigureWithLogging();
            list.SpellsByLevel = SummonerClass.Spellbook.SpellList.SpellsByLevel.Select(level =>
                new Kingmaker.Blueprints.Classes.Spells.SpellLevelList(level.SpellLevel) { m_Spells = level.m_Spells.ToList() }).ToArray();
            Spellbook = SpellbookConfigurator.New("SynthesistSpellbook", Id("SynthesistSpellbook"))
                .CopyFrom(SummonerClass.Spellbook).SetSpellList(list).ConfigureWithLogging();
            HealingIntegration.Create();
        }
    }
}
