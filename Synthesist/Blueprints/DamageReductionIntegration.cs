using System;
using System.Collections.Generic;
using System.Linq;
using BlueprintCore.Blueprints.CustomConfigurators.Classes;
using BlueprintCore.Blueprints.CustomConfigurators.UnitLogic.Abilities;
using BlueprintCore.Utils.Types;
using Kingmaker.Blueprints;
using Kingmaker.Blueprints.Classes;
using Kingmaker.Blueprints.JsonSystem;
using Kingmaker.ElementsSystem;
using Kingmaker.EntitySystem.Entities;
using Kingmaker.Enums;
using Kingmaker.Enums.Damage;
using Kingmaker.UnitLogic.Abilities.Blueprints;
using Kingmaker.UnitLogic.Abilities.Components.Base;
using Kingmaker.UnitLogic.Mechanics.Actions;
using Kingmaker.UnitLogic;
using WOTR_MAKING_FRIENDS.Synthesist.Rules;
using WOTR_MAKING_FRIENDS.Synthesist.Runtime;
using WOTR_MAKING_FRIENDS.Utilities;

namespace WOTR_MAKING_FRIENDS.Synthesist.Blueprints
{
    internal static class DamageReductionIntegration
    {
        internal const string Key = "EvolutionDamageReduction";
        internal const string Description = "Choose a weapon alignment opposite one of your eidolon's alignment components. Gain DR 5 bypassed by that alignment for 3 evolution points at Summoner level 9. At level 12, spend 2 additional points to improve the same protection to DR 10.";
        internal static readonly Dictionary<string, BlueprintFeature> Choices = new(StringComparer.Ordinal);
        internal static readonly Dictionary<string, BlueprintAbility> Purchases = new(StringComparer.Ordinal);

        internal static void Create()
        {
            foreach (DamageAlignment alignment in new[] { DamageAlignment.Good, DamageAlignment.Evil, DamageAlignment.Lawful, DamageAlignment.Chaotic })
            {
                string option = alignment.ToString();
                string name = "SynthesistDamageReduction" + option + "Feature";
                var feature = CreateFeature(name, alignment);
                Choices[option] = feature;
                SynthesistBlueprints.PurchasedFeatures[name] = feature;
                SynthesistBlueprints.ProjectionFeatures[name] = CreateFeature("SynthesistProjectionDamageReduction" + option + "Feature", alignment);
                string abilityName = "SynthesistPurchaseDamageReduction" + option;
                Purchases[option] = AbilityConfigurator.New(abilityName, SynthesistBlueprints.Id(abilityName))
                    .CopyFrom(SynthesistBlueprints.EvolutionAbilities[Key], c => false)
                    .SetDisplayName(SynthesistBlueprints.Text(abilityName + ".name", "Damage reduction / " + option.ToLowerInvariant()))
                    .SetDescription(SynthesistBlueprints.Text(abilityName + ".description", Description))
                    .AddComponent<EvolutionPreparationRestriction>()
                    .AddComponent<EvolutionBudgetRestriction>(c => c.Key = Key)
                    .AddComponent<DamageReductionChoiceRestriction>(c => c.Option = option)
                    .AddComponent<SynthEvolutionResourceLogic>(c =>
                    {
                        c.Key = Key;
                        c.m_RequiredResource = BlueprintCore.Utils.BlueprintTool.GetRef<BlueprintAbilityResourceReference>(SynthesistBlueprints.Id("SummonerEvolutionPointsResource"));
                        c.m_IsSpendResource = true;
                    })
                    .AddAbilityEffectRunAction(new ActionList { Actions = new GameAction[] { new PurchaseEvolution { Key = Key, Option = option } } })
                    .ConfigureWithLogging();
            }
        }

        private static BlueprintFeature CreateFeature(string name, DamageAlignment alignment)
            => FeatureConfigurator.New(name, SynthesistBlueprints.Id(name))
                .CopyFrom(SynthesistBlueprints.ExistingFeature(Key + "Feature"), c => false)
                .SetRanks(2)
                .SetDisplayName(SynthesistBlueprints.Text(name + ".name", "Damage reduction / " + alignment.ToString().ToLowerInvariant()))
                .SetDescription(SynthesistBlueprints.Text(name + ".description", Description))
                .AddContextRankConfig(ContextRankConfigs.FeatureRank(SynthesistBlueprints.Id(name)).WithCustomProgression((1, 5), (2, 10)))
                .AddDamageResistancePhysical(value: ContextValues.Rank(), alignment: alignment, bypassedByAlignment: true)
                .ConfigureWithLogging();

        internal static bool IsChoice(string name) => Choices.Values.Any(f => f.name == name);
        internal static bool CanPurchase(UnitEntityData unit, string option)
        {
            if (option == null || !Choices.TryGetValue(option, out var feature)) return false;
            var part = unit.Get<UnitPartSynthesist>() ?? unit.Master?.Get<UnitPartSynthesist>();
            int rank = unit.Progression.Features.GetRank(SynthesistBlueprints.BuildFeature(Key + "Feature"));
            if (part == null || rank >= 2 || part.Level < ManagedEvolutionRules.RequiredLevel(Key, rank)) return false;
            if (rank != 0) return unit.Progression.Features.GetRank(feature) == rank;
            var alignment = unit.Descriptor.Alignment.ValueRaw;
            return DamageReductionRules.OpposesAlignment(option,
                alignment.HasComponent(AlignmentComponent.Good), alignment.HasComponent(AlignmentComponent.Evil),
                alignment.HasComponent(AlignmentComponent.Lawful), alignment.HasComponent(AlignmentComponent.Chaotic));
        }

        internal static void ValidatePurchase(UnitEntityData unit, string option)
        {
            if (!CanPurchase(unit, option)) throw new InvalidOperationException("Choose an opposing damage-reduction alignment, retaining that choice for its level-12 upgrade.");
        }

        internal static void Reset(UnitEntityData unit)
        {
            foreach (var choice in Choices.Values)
            {
                var fact = unit.Progression.Features.GetFact(choice);
                if (fact != null) unit.RemoveFact(fact);
            }
        }
    }

    [TypeId("e08cc88458b04f0ab83cb4ef7fe708fb")]
    public sealed class DamageReductionChoiceRestriction : BlueprintComponent, IAbilityCasterRestriction
    {
        public string Option;
        public bool IsCasterRestrictionPassed(UnitEntityData caster) => DamageReductionIntegration.CanPurchase(caster, Option);
        public string GetAbilityCasterRestrictionUIText() => "Requires an opposing alignment and Summoner level 9; upgrading the same choice requires level 12.";
    }
}
