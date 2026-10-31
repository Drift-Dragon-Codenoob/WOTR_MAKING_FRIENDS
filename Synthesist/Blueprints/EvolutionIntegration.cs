using BlueprintCore.Blueprints.CustomConfigurators.Classes;
using BlueprintCore.Blueprints.CustomConfigurators.UnitLogic.Abilities;
using BlueprintCore.Utils;
using Kingmaker.Blueprints;
using Kingmaker.Blueprints.Classes;
using Kingmaker.Blueprints.JsonSystem;
using Kingmaker.EntitySystem.Entities;
using Kingmaker.UnitLogic.Abilities.Components.Base;
using Kingmaker.UnitLogic.Abilities.Components;
using Kingmaker.UnitLogic.Abilities;
using Kingmaker.UnitLogic.Mechanics.Actions;
using Kingmaker.ElementsSystem;
using Kingmaker.UnitLogic.FactLogic;
using Kingmaker.UnitLogic;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using WOTR_MAKING_FRIENDS.ComponentsNew;
using WOTR_MAKING_FRIENDS.Features.EidolonFeatures.BaseFeatures;
using WOTR_MAKING_FRIENDS.Synthesist.Runtime;
using WOTR_MAKING_FRIENDS.Synthesist.Rules;
using WOTR_MAKING_FRIENDS.Utilities;

namespace WOTR_MAKING_FRIENDS.Synthesist.Blueprints
{
    internal static class EvolutionIntegration
    {
        internal static readonly Dictionary<string, int> Costs = new(StringComparer.Ordinal);
        internal static readonly HashSet<string> Excluded = new(StringComparer.Ordinal)
        { "EvolutionBloodFrenzy", "EvolutionBullrush", "EvolutionSunderarmor", "EvolutionTentacleMass", "EvolutionSpike", "EvolutionAmorphous" };

        internal static void Create()
        {
            int category = 0;
            foreach (var group in FeatureCreateAllEvolutions.Evolutions.Values)
            {
                category++;
                foreach (string key in group) Costs[key] = category;
            }
            Costs["EvolutionGore"] = 2;
            Costs["EvolutionPounce"] = 1;
            Costs["EvolutionReach"] = 1;
            Costs["EvolutionImprovedDamage"] = 1;
            foreach (string key in FeatureCreateAllEvolutions.Evolutions.Values.SelectMany(v => v).Where(k => !Excluded.Contains(k)))
            {
                if (ManagedEvolutionRules.OwnsRank(key))
                {
                    string name = "SynthesistPurchased" + key + "Feature";
                    SynthesistBlueprints.PurchasedFeatures[key + "Feature"] = FeatureConfigurator.New(name, SynthesistBlueprints.Id(name))
                        .CopyFrom(SynthesistBlueprints.ExistingFeature(key + "Feature"), c => false)
                        .SetDescription(SynthesistBlueprints.Text(name + ".description", ManagedDescription(key)))
                        .SetRanks(ManagedEvolutionRules.MaximumRanks(key)).ConfigureWithLogging();
                }
                foreach (string suffix in new[] { "Feature", "BaseFeature" })
                {
                    string name = key + suffix;
                    var source = SynthesistBlueprints.ExistingFeature(name);
                    string projectionName = "SynthesistProjection" + name;
                    SynthesistBlueprints.ProjectionFeatures[name] = FeatureConfigurator.New(projectionName, SynthesistBlueprints.Id(projectionName))
                        .CopyFrom(source, c => false)
                        .SetRanks(ManagedEvolutionRules.OwnsRank(key) ? ManagedEvolutionRules.MaximumRanks(key) : source.Ranks)
                        .ConfigureWithLogging();
                    var projection = SynthesistBlueprints.ProjectionFeatures[name];
                    projection.AddComponents(IsolatedCopy.Components(source, c => key != "EvolutionFastHealing" && !IsBudgetComponent(c) &&
                        !(EvolutionStatistics.IsManaged(key) && (c is AddStatBonus || c.GetType().Name == "AddContextStatBonus" || c is GrowthReachBonus))));
                    if (source.ComponentsArray.Any(c => projection.ComponentsArray.Any(p => ReferenceEquals(c, p))))
                        throw new InvalidOperationException("Evolution projection shares mutable source components: " + name);
                    // Rank-based native/custom components must read the separately owned
                    // projection rank, rather than an absent ordinary evolution on the hero.
                    foreach (var component in projection.ComponentsArray)
                    foreach (var field in component.GetType().GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
                    {
                        var value = field.GetValue(component);
                        if (value is BlueprintFeatureReference featureRef &&
                            (featureRef.Get() == SynthesistBlueprints.ExistingFeature(key + "Feature") || featureRef.Get() == SynthesistBlueprints.ExistingFeature(key + "BaseFeature")))
                            field.SetValue(component, projection.ToReference<BlueprintFeatureReference>());
                        else if (value is BlueprintUnitFactReference factRef &&
                            (factRef.Get() == SynthesistBlueprints.ExistingFeature(key + "Feature") || factRef.Get() == SynthesistBlueprints.ExistingFeature(key + "BaseFeature")))
                            field.SetValue(component, projection.ToReference<BlueprintUnitFactReference>());
                    }
                }
                string purchaseName = "SynthesistPurchase" + key;
                var sourceAbility = BlueprintTool.Get<Kingmaker.UnitLogic.Abilities.Blueprints.BlueprintAbility>(SynthesistBlueprints.Id(key + "Ability"));
                var ability = AbilityConfigurator.New(purchaseName, SynthesistBlueprints.Id(purchaseName))
                    .CopyFrom(sourceAbility, c => false)
                    .AddComponent<EvolutionPreparationRestriction>()
                    .AddComponent<EvolutionBudgetRestriction>(c => c.Key = key)
                    .AddComponent<SynthEvolutionResourceLogic>(c =>
                    {
                        c.Key = key; c.m_RequiredResource = BlueprintTool.GetRef<BlueprintAbilityResourceReference>(SynthesistBlueprints.Id("SummonerEvolutionPointsResource"));
                        c.m_IsSpendResource = true;
                    })
                    .AddAbilityEffectRunAction(new ActionList { Actions = new GameAction[] { new PurchaseEvolution { Key = key } } })
                    .ConfigureWithLogging();
                if (ManagedEvolutionRules.OwnsRank(key))
                    ability.m_Description = SynthesistBlueprints.Text("SynthesistPurchased" + key + "Feature.description", ManagedDescription(key));
                ability.AddComponents(IsolatedCopy.Components(sourceAbility, c => c is not AbilityCasterHasFactRank &&
                    c is not AbilityEffectRunAction && c is not AbilityResourceLogic && c is not AbilityEidolonHasResource && c is not AbilityOwnerOrPetHasFacts &&
                    (c is not AbilityCasterHasResource resourceGate || resourceGate.m_Resource?.Get()?.name == "EidolonMaxAttacksResource")));
                var rankGate = BlueprintTool.Get<Kingmaker.UnitLogic.Abilities.Blueprints.BlueprintAbility>(SynthesistBlueprints.Id(key + "Ability"))
                    .GetComponent<AbilityCasterHasFactRank>();
                if (rankGate != null) ability.AddComponents(new EvolutionMasterRankRestriction
                {
                    Key = key, Feature = rankGate.m_UnitFact, Start = key is "EvolutionPounce" or "EvolutionReach" or "EvolutionImprovedDamage" ? 1 : rankGate.startLevel,
                    Interval = rankGate.numLevelsBetweenRanks, Maximum = rankGate.maxRank
                });
                SynthesistBlueprints.EvolutionAbilities[key] = ability;
            }
            var roots = new List<Blueprint<BlueprintUnitFactReference>>();
            for (int cost = 1; cost <= 4; cost++)
            {
                string name = "SynthesistEvolution" + cost + "Menu";
                var variants = SynthesistBlueprints.EvolutionAbilities.Where(p => Costs[p.Key] == cost)
                    .Select(p => (Blueprint<BlueprintAbilityReference>)p.Value.ToReference<BlueprintAbilityReference>()).ToList();
                var menu = AbilityConfigurator.New(name, SynthesistBlueprints.Id(name))
                    .SetDisplayName(SynthesistBlueprints.Text(name + ".name", cost + "-point evolutions"))
                    .SetDescription(SynthesistBlueprints.Text(name + ".description", "Allocate original Summoner evolutions during preparation. Climb, swim and underwater breathing are unavailable in this version."))
                    .SetType(Kingmaker.UnitLogic.Abilities.Blueprints.AbilityType.Special)
                    .SetRange(Kingmaker.UnitLogic.Abilities.Blueprints.AbilityRange.Personal)
                    .SetActionType(Kingmaker.UnitLogic.Commands.Base.UnitCommand.CommandType.Free)
                    .SetIcon(SynthesistBlueprints.ExistingFeature("EvolutionCost" + cost + "Feature").m_Icon)
                    .AddAbilityVariants(variants).ConfigureWithLogging();
                roots.Add(menu.ToReference<BlueprintUnitFactReference>());
            }
            FeatureConfigurator.New("SynthesistEvolutionControlsFeature", SynthesistBlueprints.Id("SynthesistEvolutionControlsFeature"))
                .SetDisplayName(SynthesistBlueprints.Text("evolutions.name", "Evolutions"))
                .SetDescription(SynthesistBlueprints.Text("evolutions.description", "Allocate evolution points during preparation after advancement or retraining."))
                .AddFacts(roots).ConfigureWithLogging();
            EvolutionHealing.Create();
        }

        private static bool IsBudgetComponent(BlueprintComponent component)
            => component is IncreaseResourceAmount || component.GetType().Name.Contains("ResourceAmount") ||
                component.GetType().Name.Contains("ActionsOnBuffApply");

        internal static int Cost(string key, UnitEntityData unit)
        {
            if (key == "EvolutionLarge") return unit.Progression.Features.GetRank(SynthesistBlueprints.BuildFeature(key + "Feature")) == 0 ? 4 : 6;
            if (key == "EvolutionFastHealing" && unit.Progression.Features.HasFact(SynthesistBlueprints.BuildFeature(key + "Feature"))) return 2;
            if (key is "EvolutionAbilityIncreaseStrength" or "EvolutionAbilityIncreaseConstitution" && unit.State.Size >= Kingmaker.Enums.Size.Large) return 4;
            return Costs[key];
        }

        internal static int AspectSpent(UnitEntityData owner) => owner.Progression.Features.GetRank(SynthesistBlueprints.ExistingFeature("EvolutionCost1Feature"));

        private static string ManagedDescription(string key) => key switch
        {
            "EvolutionNaturalArmor" => "Each purchase improves natural armour by 2. Costs 1 evolution point; available once at Summoner level 1, and again at levels 5, 10, 15 and 20.",
            "EvolutionLarge" => "Large costs 4 points at level 8: +8 Strength, +4 Constitution, -2 Dexterity and +2 natural armour. Huge costs 6 additional points at level 13: total +16 Strength, +8 Constitution, -4 Dexterity and +5 natural armour. Size penalties and body-form reach apply. Strength and Constitution evolution purchases cost twice as much while Large or Huge.",
            "EvolutionFastHealing" => "Fast healing 1 costs 4 points and requires Summoner level 11. Each additional 2 points increases healing by 1, to a maximum of 5. While fused, fast healing restores personal HP; it does not restore eidolon temporary HP.",
            _ => "Increase this ability score by 2 for 2 evolution points. Available once at level 1, plus once more for every six Summoner levels. Strength and Constitution purchases cost 4 points while Large or Huge. Aspect cannot grant ability increases."
        };
    }

    [TypeId("e564f82ca75641f3baf9c20802b817ca")]
    public sealed class SynthEvolutionResourceLogic : AbilityResourceLogic
    {
        public string Key;
        public override int CalculateCost(AbilityData ability) => EvolutionIntegration.Cost(Key, ability.Caster.Unit);
    }

    [TypeId("a16b4e5f224c48a28f1ae79fe55436ba")]
    public sealed class PurchaseEvolution : ContextAction
    {
        public string Key;
        public override string GetCaption() => "Purchase " + Key;
        public override void RunAction()
        {
            var unit = Context.MaybeCaster;
            var part = unit?.Get<UnitPartSynthesist>() ?? unit?.Master?.Get<UnitPartSynthesist>();
            if (part?.IsPreparing != true || !part.State.EvolutionEditsOpen) throw new InvalidOperationException("Evolution allocation is closed.");
            int cost = EvolutionIntegration.Cost(Key, unit);
            int diverted = unit == part.Owner
                ? AspectBudget.AdditionalDiversion(part.Level, EvolutionIntegration.AspectSpent(unit), cost) : 0;
            if (unit == part.Owner && Key.StartsWith("EvolutionAbilityIncrease", StringComparison.Ordinal))
                throw new InvalidOperationException("Aspect cannot grant the ability increase evolution.");
            unit.AddFact(SynthesistBlueprints.BuildFeature(Key + "Feature"));
            for (int i = 0; i < cost; i++) unit.AddFact(SynthesistBlueprints.ExistingFeature("EvolutionCost1Feature"));
            if (unit == part.Owner)
            {
                for (int i = 0; i < diverted; i++) part.Eidolon.AddFact(SynthesistBlueprints.ExistingFeature("EvolutionCost1Feature"));
                part.Eidolon.Descriptor.Resources.Restore(BlueprintTool.Get<BlueprintAbilityResource>(SynthesistBlueprints.Id("SummonerEvolutionPointsResource")));
            }
            unit.Descriptor.Resources.Restore(BlueprintTool.Get<BlueprintAbilityResource>(SynthesistBlueprints.Id("SummonerEvolutionPointsResource")));
            unit.Descriptor.Resources.Restore(BlueprintTool.Get<BlueprintAbilityResource>(SynthesistBlueprints.Id("EidolonMaxAttacksResource")));
            // Apply size and allowance changes before another free purchase can be
            // evaluated, including doubled Strength/Constitution costs after Growth.
            part.SyncProgression();
        }
    }

    [TypeId("c172a664886c4105887dfb5c10dc6587")]
    public sealed class EvolutionPreparationRestriction : BlueprintComponent, IAbilityCasterRestriction
    {
        public bool IsCasterRestrictionPassed(UnitEntityData caster)
        {
            var part = caster.Get<UnitPartSynthesist>() ?? caster.Master?.Get<UnitPartSynthesist>();
            return part != null && (part.Eidolon == caster || (part.Owner == caster && part.Level >= 10)) &&
                part.IsPreparing && part.State.EvolutionEditsOpen && !part.Owner.IsInCombat;
        }
        public string GetAbilityCasterRestrictionUIText() => "Evolutions may only be allocated during preparation after advancement or retraining.";
    }

    [TypeId("414554957bc841a484ef45242631b079")]
    public sealed class EvolutionMasterRankRestriction : BlueprintComponent, IAbilityCasterRestriction
    {
        public string Key;
        public BlueprintUnitFactReference Feature;
        public int Start;
        public int Interval;
        public int Maximum;
        public bool IsCasterRestrictionPassed(UnitEntityData caster)
        {
            var part = caster.Get<UnitPartSynthesist>() ?? caster.Master?.Get<UnitPartSynthesist>();
            if (part == null) return false;
            if (ManagedEvolutionRules.OwnsRank(Key))
            {
                int purchased = caster.Progression.Features.GetRank(SynthesistBlueprints.BuildFeature(Key + "Feature"));
                return purchased < ManagedEvolutionRules.MaximumRanks(Key) && part.Level >= ManagedEvolutionRules.RequiredLevel(Key, purchased);
            }
            int rank = caster.Progression.Features.GetRank((BlueprintFeature)Feature.Get());
            return rank < Maximum && part.Level >= Math.Max(1, Start + Interval * rank);
        }
        public string GetAbilityCasterRestrictionUIText() => "Requires further Summoner advancement.";
    }

    [TypeId("a5bd33f58e124ec79a7535ea282f7fe2")]
    public sealed class EvolutionBudgetRestriction : BlueprintComponent, IAbilityCasterRestriction
    {
        public string Key;
        public bool IsCasterRestrictionPassed(UnitEntityData caster)
        {
            var part = caster.Get<UnitPartSynthesist>() ?? caster.Master?.Get<UnitPartSynthesist>();
            if (part == null) return false;
            int cost = EvolutionIntegration.Cost(Key, caster);
            var resource = BlueprintTool.Get<BlueprintAbilityResource>(SynthesistBlueprints.Id("SummonerEvolutionPointsResource"));
            if (caster.Descriptor.Resources.GetResourceAmount(resource) < cost) return false;
            if (caster != part.Owner) return true;
            int spent = EvolutionIntegration.AspectSpent(caster);
            if (Key.StartsWith("EvolutionAbilityIncrease", StringComparison.Ordinal) || spent + cost > AspectBudget.Allowance(part.Level)) return false;
            return part.Eidolon.Descriptor.Resources.GetResourceAmount(resource) >= AspectBudget.AdditionalDiversion(part.Level, spent, cost);
        }
        public string GetAbilityCasterRestrictionUIText() => "Insufficient evolution points or Aspect diversion allowance.";
    }
}
