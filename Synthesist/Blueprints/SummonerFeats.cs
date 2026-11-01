using System;
using System.Collections.Generic;
using System.Linq;
using BlueprintCore.Blueprints.CustomConfigurators.Classes;
using BlueprintCore.Blueprints.CustomConfigurators.Classes.Selection;
using BlueprintCore.Blueprints.Configurators.Classes.Selection;
using BlueprintCore.Blueprints.CustomConfigurators.UnitLogic.Abilities;
using BlueprintCore.Blueprints.CustomConfigurators.UnitLogic.Buffs;
using BlueprintCore.Blueprints.References;
using BlueprintCore.Utils;
using Kingmaker.Blueprints;
using Kingmaker.Blueprints.Classes;
using Kingmaker.Blueprints.Classes.Selection;
using Kingmaker.EntitySystem.Entities;
using Kingmaker.EntitySystem.Stats;
using Kingmaker.Enums;
using Kingmaker.UnitLogic.Abilities.Blueprints;
using Kingmaker.UnitLogic.Buffs.Blueprints;
using Kingmaker.UnitLogic.Mechanics.Actions;
using WOTR_MAKING_FRIENDS.Synthesist.Runtime;
using WOTR_MAKING_FRIENDS.Utilities;

namespace WOTR_MAKING_FRIENDS.Synthesist.Blueprints
{
    internal sealed class ArcanaChoice
    {
        internal string ClassKey;
        internal bool Lower;
        internal int SpellLevel = -1;
    }

    internal static class SummonerFeats
    {
        internal const string Extra = "SummonerExtraEvolutionFeat", Arcana = "SummonerExpandedArcanaFeat",
            Rending = "SummonerRendingClawsFeat", Call = "SummonerCallFeat";
        internal static readonly Dictionary<string, BlueprintCharacterClass> Classes = new(StringComparer.Ordinal);
        internal static readonly Dictionary<string, ArcanaChoice> ArcanaChoices = new(StringComparer.Ordinal);
        internal static readonly Dictionary<string, BlueprintFeatureSelection> SpellSelections = new(StringComparer.Ordinal);
        internal static readonly Dictionary<string, Kingmaker.Blueprints.Classes.Spells.BlueprintSpellList[]> SpellLists = new(StringComparer.Ordinal);
        internal static BlueprintFeatureSelection ArcanaModes;
        internal static BlueprintFeature ExtraFeature, CallFeature;
        internal static readonly Dictionary<StatType, BlueprintBuff> CallBuffs = new();
        internal static int ExtraPoints(UnitEntityData unit) => ExtraFeature == null ? 0 : unit.Progression.Features.GetRank(ExtraFeature);
        internal static string SelectionName(string key, string stage) => "SummonerArcana" + key + stage + "Selection";

        internal static void Create()
        {
            Classes["Summoner"] = SynthesistBlueprints.SummonerClass;
            Classes["Bard"] = CharacterClassRefs.BardClass.Reference.Get();
            Classes["Bloodrager"] = CharacterClassRefs.BloodragerClass.Reference.Get();
            Classes["Hunter"] = CharacterClassRefs.HunterClass.Reference.Get();
            Classes["Inquisitor"] = CharacterClassRefs.InquisitorClass.Reference.Get();
            Classes["Oracle"] = CharacterClassRefs.OracleClass.Reference.Get();
            Classes["Skald"] = CharacterClassRefs.SkaldClass.Reference.Get();
            Classes["Sorcerer"] = CharacterClassRefs.SorcererClass.Reference.Get();
            Classes["Magus"] = CharacterClassRefs.MagusClass.Reference.Get();
            Classes["Witch"] = CharacterClassRefs.WitchClass.Reference.Get();
            Classes["EldritchScion"] = CharacterClassRefs.EldritchScionClass.Reference.Get();
            ExtraFeature = Feat(Extra, "Extra Evolution", "Your eidolon's evolution allowance increases by 1. This feat can be taken once at character level 1, with additional selections allowed at levels 5, 10, 15 and 20. These are additional allowances, not free feat grants.", 5, FeatRequirement.Extra);
            var arcana = Feat(Arcana, "Expanded Arcana", "Learn one additional spell from a limited-spells-known class, or two spells each at least one spell level below that class's highest available spell level. Choose the class and spells during this advancement. Repeatable; choices remain fixed until retraining.", 100, FeatRequirement.Arcana);
            var rending = Feat(Rending, "Rending Claws", "Requires Strength 13, two claw natural attacks and base attack bonus +6. When two claw attacks hit the same creature in one turn, the second deals 1d6 additional precision damage, once per round. This damage is not multiplied on a critical hit. Synthesist selection checks the saved fused build; the feat functions only while its actual combat prerequisites are met.", 1, FeatRequirement.Rending, true);
            CreateCall();
            CreateArcana();
            SynthesistBlueprints.ExistingFeature("AddEvolutionPointsFeature").AddComponents(new ExtraEvolutionPoolBonus());
            var basic = Kingmaker.Blueprints.Root.BlueprintRoot.Instance.Progression.BasicFeatSelection;
            basic.m_AllFeatures = basic.m_AllFeatures.Concat(new[] { ExtraFeature, arcana, rending, CallFeature }.Select(f => f.ToReference<BlueprintFeatureReference>())).ToArray();
            var fighter = FeatureSelectionRefs.FighterFeatSelection.Reference.Get();
            fighter.m_AllFeatures = fighter.m_AllFeatures.Concat(new[] { rending.ToReference<BlueprintFeatureReference>() }).ToArray();
        }

        private static BlueprintFeature Feat(string name, string title, string description, int ranks, FeatRequirement requirement, bool combat = false)
        {
            var feature = FeatureConfigurator.New(name, SynthesistBlueprints.Id(name))
                .SetDisplayName(SynthesistBlueprints.Text(name + ".name", title))
                .SetDescription(SynthesistBlueprints.Text(name + ".description", description))
                .SetGroups(combat ? new[] { FeatureGroup.Feat, FeatureGroup.CombatFeat } : new[] { FeatureGroup.Feat })
                .SetRanks(ranks).AddComponent<SummonerFeatPrerequisite>(c => c.Requirement = requirement)
                .SetIcon(AbilityRefs.MagicFang.Reference.Get().m_Icon).ConfigureWithLogging();
            if (requirement == FeatRequirement.Rending) feature.AddComponents(new RendingClawsDamage());
            return feature;
        }

        private static void CreateCall()
        {
            var variants = new List<Blueprint<BlueprintAbilityReference>>();
            foreach (StatType stat in new[] { StatType.Unknown, StatType.Strength, StatType.Dexterity, StatType.Constitution })
            {
                string suffix = stat == StatType.Unknown ? "Off" : stat.ToString();
                if (stat != StatType.Unknown)
                {
                    string buffName = "SynthesistSummonersCall" + suffix + "Buff";
                    CallBuffs[stat] = BuffConfigurator.New(buffName, SynthesistBlueprints.Id(buffName))
                        .SetDisplayName(SynthesistBlueprints.Text(buffName + ".name", "Summoner's Call: " + suffix))
                        .SetDescription(SynthesistBlueprints.Text(buffName + ".description", "+2 enhancement to " + suffix + " for ten minutes after the eidolon is summoned."))
                        .AddStatBonus(ModifierDescriptor.Enhancement, stat: stat, value: 2).ConfigureWithLogging();
                }
                string name = "SummonerCall" + suffix + "Ability";
                var ability = AbilityConfigurator.New(name, SynthesistBlueprints.Id(name))
                    .SetDisplayName(SynthesistBlueprints.Text(name + ".name", "Next summoning: " + (stat == StatType.Unknown ? "no bonus" : suffix)))
                    .SetDescription(SynthesistBlueprints.Text(name + ".description", "Choose the optional Summoner's Call bonus for your next actual eidolon summoning. This does not change an existing bonus or restart its duration. Strength is the initial preference."))
                    .SetType(AbilityType.Special).SetRange(AbilityRange.Personal).SetCanTargetSelf(true)
                    .SetActionType(Kingmaker.UnitLogic.Commands.Base.UnitCommand.CommandType.Free)
                    .AddAbilityEffectRunAction(new Kingmaker.ElementsSystem.ActionList { Actions = new Kingmaker.ElementsSystem.GameAction[] { new ChooseSummonersCall { Stat = stat } } })
                    .ConfigureWithLogging();
                variants.Add(ability.ToReference<BlueprintAbilityReference>());
            }
            string menuName = "SummonerCallPreferenceAbility";
            var menu = AbilityConfigurator.New(menuName, SynthesistBlueprints.Id(menuName))
                .SetDisplayName(SynthesistBlueprints.Text(menuName + ".name", "Summoner's Call preference"))
                .SetDescription(SynthesistBlueprints.Text(menuName + ".description", "Choose Strength, Dexterity, Constitution or no bonus for the next eidolon summoning."))
                .SetType(AbilityType.Special).SetRange(AbilityRange.Personal).SetCanTargetSelf(true)
                .AddAbilityVariants(variants).ConfigureWithLogging();
            CallFeature = Feat(Call, "Summoner's Call", "Whenever you summon your eidolon, you may give it a +2 enhancement bonus to Strength, Dexterity or Constitution for ten minutes after the ritual completes. Choose the next-summoning preference from your abilities; initially Strength. It does not refresh on preparation, loading, splitting or rejoining, and enhancement bonuses to the same statistic do not stack.", 1, FeatRequirement.Eidolon);
            CallFeature.AddComponents(new Kingmaker.UnitLogic.FactLogic.AddFacts { m_Facts = new[] { menu.ToReference<BlueprintUnitFactReference>() } });
        }

        private static void CreateArcana()
        {
            var modes = new List<BlueprintFeatureReference>();
            foreach (var pair in Classes)
            {
                string key = pair.Key;
                var books = new[] { pair.Value.Spellbook }.Concat((pair.Value.m_Archetypes ?? Array.Empty<BlueprintArchetypeReference>()).Select(a => a.Get()?.ReplaceSpellbook));
                if (key == "Summoner") books = books.Concat(new[] { SynthesistBlueprints.Spellbook });
                SpellLists[key] = books.Where(b => b != null).Select(b => b.SpellList).Where(l => l != null).Distinct().ToArray();
                foreach (bool lower in new[] { false, true })
                {
                    string kind = lower ? "Lower" : "One";
                    string name = "SummonerArcana" + key + kind + "Choice";
                    ArcanaChoices[name] = new ArcanaChoice { ClassKey = key, Lower = lower };
                    var choice = FeatureConfigurator.New(name, SynthesistBlueprints.Id(name))
                        .SetDisplayName(SynthesistBlueprints.Text(name + ".name", key + ": " + (lower ? "two lower-level spells" : "one additional spell")))
                        .SetDescription(SynthesistBlueprints.Text(name + ".description", "Use this class's current native spellbook and spell list. Each chosen spell must be unknown. The two-spell option requires both spells to be below your highest available spell level."))
                        .SetRanks(100).AddComponent<ArcanaSpellPrerequisite>(c => c.Choice = name).ConfigureWithLogging();
                    modes.Add(choice.ToReference<BlueprintFeatureReference>());
                    var spells = new List<BlueprintFeatureReference>();
                    for (int level = 0; level <= 9; level++)
                    {
                        string featureName = "SummonerArcana" + key + kind + "Level" + level + "Feature";
                        ArcanaChoices[featureName] = new ArcanaChoice { ClassKey = key, Lower = lower, SpellLevel = level };
                        var feature = ParametrizedFeatureConfigurator.New(featureName, SynthesistBlueprints.Id(featureName))
                            .SetDisplayName(SynthesistBlueprints.Text(featureName + ".name", key + " spell, level " + level))
                            .SetDescription(SynthesistBlueprints.Text(featureName + ".description", "Permanently learn the selected spell at its original level in this class's native spellbook."))
                            .SetParameterType(FeatureParameterType.LearnSpell).SetRanks(100)
                            .AddComponent<ArcanaSpellPrerequisite>(c => c.Choice = featureName)
                            .ConfigureWithLogging();
                        feature.m_SpellcasterClass = pair.Value.ToReference<BlueprintCharacterClassReference>();
                        feature.SpecificSpellLevel = true; feature.SpellLevel = level;
                        feature.AddComponents(new Kingmaker.Designers.Mechanics.Facts.LearnSpellParametrized
                        { m_SpellcasterClass = pair.Value.ToReference<BlueprintCharacterClassReference>(), SpecificSpellLevel = true, SpellLevel = level });
                        spells.Add(feature.ToReference<BlueprintFeatureReference>());
                    }
                    foreach (string stage in lower ? new[] { "LowerFirst", "LowerSecond" } : new[] { "One" })
                    {
                        string selectionName = SelectionName(key, stage);
                        SpellSelections[selectionName] = Selection(selectionName, key + ": " + (stage == "LowerSecond" ? "second lower-level spell" : lower ? "first lower-level spell" : "additional spell"), spells);
                    }
                }
            }
            ArcanaModes = Selection("SummonerExpandedArcanaModeSelection", "Expanded Arcana: choose class and spell allowance", modes);
        }
        private static BlueprintFeatureSelection Selection(string name, string title, IEnumerable<BlueprintFeatureReference> choices)
            => FeatureSelectionConfigurator.New(name, SynthesistBlueprints.Id(name))
                .SetDisplayName(SynthesistBlueprints.Text(name + ".name", title)).SetRanks(100)
                .SetAllFeatures(choices.Select(c => (Blueprint<BlueprintFeatureReference>)c).ToArray()).ConfigureWithLogging();
    }
}
