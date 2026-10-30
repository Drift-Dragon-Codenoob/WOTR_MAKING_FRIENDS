using BlueprintCore.Actions.Builder;
using BlueprintCore.Actions.Builder.ContextEx;
using BlueprintCore.Blueprints.CustomConfigurators.Classes;
using BlueprintCore.Blueprints.CustomConfigurators.UnitLogic.Abilities;
using BlueprintCore.Blueprints.References;
using BlueprintCore.Conditions.Builder;
using BlueprintCore.Conditions.Builder.BasicEx;
using BlueprintCore.Utils;
using BlueprintCore.Utils.Types;
using Kingmaker.Blueprints;
using Kingmaker.Designers.Mechanics.Buffs;
using Kingmaker.EntitySystem.Stats;
using Kingmaker.Enums;
using Kingmaker.UnitLogic.Abilities.Components;
using System;
using UnityEngine;
using WOTR_MAKING_FRIENDS.ComponentsNew;
using WOTR_MAKING_FRIENDS.Enums;
using WOTR_MAKING_FRIENDS.GUIDs;
using WOTR_MAKING_FRIENDS.Utilities;
namespace WOTR_MAKING_FRIENDS.Features.EidolonFeatures.Evolutions._4_Point_Evolutions
{
    internal class EvolutionLarge
    {
        internal static class IClass
        {
            internal static Sprite icon = AbilityRefs.LegendaryProportions.Reference.Get().m_Icon;
            internal const string Evolution = "EvolutionLarge";
            internal const string EvolutionSizeChange = "EvolutionSizeChange";
            internal const string Feature = Evolution + "Feature";
            internal const string BaseFeature = Evolution + "BaseFeature";
            internal const string Ability = Evolution + "Ability";
        }
        internal static BlueprintFeatureReference[] blueprintUnitFactReferences = new BlueprintFeatureReference[9];
        internal static int[] SizeACPenalty = new int[] { -8, -4, -2, -1 };
        public static void Adjust()
        {
            AdjustFeature();
            AddSizeChangeFeatures();
            AdjustAbility();
        }
        public static void AdjustFeature()
        {
            FeatureConfigurator.For(GetGUID.GUIDByName(IClass.Feature))
                .SetIcon(IClass.icon)
                .AddStatBonus(ModifierDescriptor.Racial, false, StatType.Strength, 4)
                .AddStatBonus(ModifierDescriptor.Racial, false, StatType.Constitution, 2)
                .AddContextRankConfig(ContextRankConfigs.FeatureRank(GetGUID.GUIDByName(IClass.Feature)).WithCustomProgression((1, 2), (2, 5)))
                .AddContextStatBonus(StatType.AC, ContextValues.Rank(), ModifierDescriptor.NaturalArmor)
                .AddStatBonus(ModifierDescriptor.Racial, false, StatType.Dexterity, -2)
                .AddComponent<GrowthReachBonus>()
                .SetRanks(2)
                .ConfigureWithLogging(true);
        }
        public static void AddSizeChangeFeatures()
        {
            int mediumInt = (int)Size.Medium;
            int arrayInt = 0;
            foreach (Size enumValue in System.Enum.GetValues(typeof(Size)))
            {
                try
                {
                    var eidolonFeatureName = IClass.EvolutionSizeChange + System.Enum.GetName(typeof(Size), enumValue) + "Feature";
                    var eidolonFeatureGuid = GetGUID.GUIDByName(eidolonFeatureName);

                    int sizeInt = (int)enumValue;
                    int diff = mediumInt - sizeInt;

                    var feature = FeatureConfigurator.New(eidolonFeatureName, eidolonFeatureGuid)
                        .SetIcon(IClass.icon)
                        .SetDisplayName(Helpers.ObtainString(eidolonFeatureName + ".Name"))
                        .SetDescription(Helpers.ObtainString(eidolonFeatureName + ".Description"))
                        .SetRanks(1)
                        .SetGroups(FeatureGroupExtension.EvolutionTransmogrifiable, FeatureGroupExtension.EvolutionSizeChangeGroup)
                        .AddChangeUnitSize(size: enumValue, type: ChangeUnitSize.ChangeType.Value);

                    if (diff > 0)
                    {
                        feature.AddComponent<WeaponSizeChangeWeaponGroup>(c =>
                        {
                            c.ignoreWeaponGroup = true;
                            c.SizeCategoryChange = diff;
                            c.plusFeatureRanks = true;
                            c.m_Feature = BlueprintTool.GetRef<BlueprintFeatureReference>(GetGUID.GUIDByName(IClass.Feature));
                        });
                        if (arrayInt < SizeACPenalty.Length)
                        {
                            feature.AddStatBonus(ModifierDescriptor.Size, false, StatType.AC, SizeACPenalty[arrayInt]);
                            feature.AddStatBonus(ModifierDescriptor.Size, false, StatType.AdditionalAttackBonus, SizeACPenalty[arrayInt]);
                        }
                    }

                    feature.ConfigureWithLogging();

                    var eidolonOriginalFeatureName = IClass.EvolutionSizeChange + System.Enum.GetName(typeof(Size), enumValue) + "OriginalFeature";
                    var eidolonOriginalFeatureGuid = GetGUID.GUIDByName(eidolonOriginalFeatureName);
                    FeatureConfigurator.New(eidolonOriginalFeatureName, eidolonOriginalFeatureGuid)
                        .CopyFrom(eidolonFeatureGuid, c => c is not ChangeUnitSize)
                        .SetGroups(FeatureGroupExtension.EvolutionBase, FeatureGroupExtension.EvolutionSizeChangeGroup)
                        .ConfigureWithLogging();

                    blueprintUnitFactReferences[arrayInt] = BlueprintTool.GetRef<BlueprintFeatureReference>(GetGUID.GUIDByName(eidolonFeatureName));
                    arrayInt++;

                }
                catch (Exception ex)
                {
                    Main.Log(e: ex);
                }
            }
        }

        public static void AdjustAbility()
        {

            AbilityConfigurator.For(GetGUID.GUIDByName(IClass.Ability))
                .RemoveComponents(c => c is (AbilityEffectRunAction or AbilityEidolonHasResource))
                .ConfigureWithLogging(true);

            // Check the old rank before granting Growth: only the second purchase costs six.
            var action = ActionsBuilder.New()
                                             .Conditional(ConditionsBuilder.New().HasFact(GetGUID.GUIDByName(IClass.Feature)),
                                                 ActionsBuilder.New()
                                                     .AddFeature(GetGUID.GUIDByName("EvolutionCost2Feature"))
                                                     .Add<AddContextFeatureToPet>(c =>
                                                     {
                                                         c.m_Feature = BlueprintTool.GetRef<BlueprintFeatureReference>(GetGUID.GUIDByName("EvolutionCost2Feature"));
                                                         c.m_ReplaceFeature = BlueprintTool.GetRef<BlueprintFeatureReference>(GetGUID.GUIDByName("EvolutionCost1Feature"));
                                                         c.m_HasFeature = BlueprintTool.GetRef<BlueprintFeatureReference>(GetGUID.GUIDByName("SummonerAspectGreaterFeature"));
                                                     }))
                                             .AddFeature(BlueprintTool.GetRef<BlueprintFeatureReference>(GetGUID.GUIDByName(IClass.Feature)))
                                             .Add<ContextActionAddRemoveFeature>(c =>
                                             {
                                                 c.m_Features = blueprintUnitFactReferences;
                                                 c.m_Default_Feature = BlueprintTool.GetRef<BlueprintFeatureReference>(GetGUID.GUIDByName(IClass.EvolutionSizeChange + "LargeFeature"));
                                             })
                                             .Add<AddContextFeatureToPet>(c =>
                                             {
                                                 c.m_Feature = BlueprintTool.GetRef<BlueprintFeatureReference>(GetGUID.GUIDByName("EvolutionCost4Feature"));
                                                 c.m_ReplaceFeature = BlueprintTool.GetRef<BlueprintFeatureReference>(GetGUID.GUIDByName("EvolutionCost2Feature"));
                                                 c.m_HasFeature = BlueprintTool.GetRef<BlueprintFeatureReference>(GetGUID.GUIDByName("SummonerAspectGreaterFeature"));
                                             })
                                             .AddFeature(BlueprintTool.GetRef<BlueprintFeatureReference>(GetGUID.GUIDByName("EvolutionCost4Feature")))
                                             .RestoreResource(BlueprintTool.GetRef<BlueprintAbilityResourceReference>(GetGUID.GUIDByName("SummonerEvolutionPointsResource")), 99)
                                             .RestoreResource(BlueprintTool.GetRef<BlueprintAbilityResourceReference>(GetGUID.GUIDByName("EidolonMaxAttacksResource")), 99)
                                             .Add<ContextRestoreResourceOnPets>(c => { c.m_Resource = BlueprintTool.GetRef<BlueprintAbilityResourceReference>(GetGUID.GUIDByName("SummonerEvolutionPointsResource")); })
                                             .Add<ContextRestoreResourceOnPets>(c => { c.m_Resource = BlueprintTool.GetRef<BlueprintAbilityResourceReference>(GetGUID.GUIDByName("EidolonMaxAttacksResource")); });

            AbilityConfigurator.For(GetGUID.GUIDByName(IClass.Ability))
                .SetIcon(IClass.icon)
                .AddComponent<AbilityGrowthSizeRestriction>()
                .AddAbilityEffectRunAction(action.Build())
                .AddComponent<AbilityCasterHasResource>(c =>
                {
                    c.m_Resource = BlueprintTool.GetRef<BlueprintAbilityResourceReference>(GetGUID.GUIDByName("SummonerEvolutionPointsResource"));
                    c.m_Feature = BlueprintTool.GetRef<BlueprintFeatureReference>(GetGUID.GUIDByName(IClass.Feature));
                    c.featureRank = 2;
                    c.resourceAmount = 4;
                    c.costMultiplierByRank = 2;
                })
                .AddComponent<AbilityEidolonHasResource>(c =>
                {
                    c.m_Resource = BlueprintTool.GetRef<BlueprintAbilityResourceReference>(GetGUID.GUIDByName("SummonerEvolutionPointsResource"));
                    c.m_Feature = BlueprintTool.GetRef<BlueprintFeatureReference>(GetGUID.GUIDByName(IClass.Feature));
                    c.featureRank = 2;
                    c.resourceAmount = 4;
                    c.costMultiplierByRank = 2;
                    c.useCostMultiplier = true;
                })
                .AddComponent<AbilityCasterHasFactRank>(c =>
                {
                    c.m_UnitFact = BlueprintTool.GetRef<BlueprintUnitFactReference>(GetGUID.GUIDByName(IClass.Feature));
                    c.maxRank = 2;
                    c.numLevelsBetweenRanks = 5;
                    c.startLevel = 8;
                })
                .ConfigureWithLogging(true);
        }
    }
}
