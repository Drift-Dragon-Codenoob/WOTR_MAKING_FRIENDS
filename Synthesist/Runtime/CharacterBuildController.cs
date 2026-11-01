using System;
using System.Collections.Generic;
using HarmonyLib;
using Kingmaker.Blueprints;
using Kingmaker.UnitLogic;
using Kingmaker.UnitLogic.Class.LevelUp;
using Kingmaker.UnitLogic.Class.LevelUp.Actions;
using WOTR_MAKING_FRIENDS.Synthesist.Blueprints;
using WOTR_MAKING_FRIENDS.Synthesist.Rules;

namespace WOTR_MAKING_FRIENDS.Synthesist.Runtime
{
    [HarmonyPatch(typeof(SelectFeature), nameof(SelectFeature.Apply))]
    internal static class CharacterBuildSelectionHook
    {
        private static readonly HashSet<BuildAction> reportedTransitions = new();
        private static void Postfix(SelectFeature __instance, LevelUpState __0, UnitDescriptor __1)
        {
            if (!SynthesistBlueprints.Ready || __instance.Item?.Feature == null ||
                !CharacterBuildIntegration.Choices.TryGetValue(__instance.Item.Feature.name, out var choice)) return;
            string reason = CharacterBuildIntegration.Reason(__instance.Item.Feature.name, __1);
            if (reason != null) throw new InvalidOperationException(reason);
            var part = __1.Unit.Ensure<UnitPartSynthesistBuild>();
            var selection = __instance.GetSelectionState(__0);
            Kingmaker.Blueprints.Classes.Selection.BlueprintFeatureSelection next = null;
            switch (choice.Action)
            {
                case BuildAction.Rebuild:
                case BuildAction.Keep:
                    int level = __1.Progression.GetClassLevel(SynthesistBlueprints.SummonerClass);
                    var form = CharacterBuildIntegration.Form(__1) ?? throw new InvalidOperationException("Choose a base form first.");
                    part.Draft = CharacterBuild.Begin(level, form, part.Current, choice.Action == BuildAction.Keep, SummonerFeats.ExtraPoints(__1.Unit));
                    part.Draft.Appearance = part.SelectedAppearance;
                    next = level >= 10 ? CharacterBuildIntegration.AspectPurchases : CharacterBuildIntegration.EidolonPurchases;
                    break;
                case BuildAction.Buy:
                    int cost = part.Draft.Cost(choice.Key, CharacterBuildIntegration.Definitions[choice.Key].Cost, choice.Aspect);
                    part.Draft.Buy(choice.Key, choice.Option, cost, choice.Aspect);
                    next = choice.Aspect ? CharacterBuildIntegration.AspectPurchases : CharacterBuildIntegration.EidolonPurchases;
                    break;
                case BuildAction.FinishPurchases:
                    next = choice.Aspect ? CharacterBuildIntegration.EidolonPurchases : CharacterBuildIntegration.Appearance;
                    break;
                case BuildAction.AppearanceGroup:
                    next = CharacterBuildIntegration.AppearanceGroups[choice.Key];
                    break;
                case BuildAction.Appearance:
                    part.Draft.Appearance = choice.Key;
                    next = CharacterBuildIntegration.Summary;
                    break;
                case BuildAction.Confirm:
                    part.Confirm();
                    break;
            }
            // CharGenVM filters normal pages to Parent == null. Our choice items
            // are plain features, not nested BlueprintFeatureSelections, so a child
            // cannot be rendered as a nested picker even with its Next link set.
            if (next != null)
            {
                var page = AddVisibleStep(__0, selection, next);
                if (reportedTransitions.Add(choice.Action))
                    Main.Log("Synthesist build page: " + choice.Action + " -> " + next.name +
                        "; root=" + (page.Parent == null) + "; selections=" + __0.Selections.Count);
            }
        }

        private static FeatureSelectionState AddVisibleStep(LevelUpState state, FeatureSelectionState previous,
            Kingmaker.Blueprints.Classes.Selection.BlueprintFeatureSelection next)
            => state.AddSelection(null, previous.Source, next, previous.Level);
    }

    [HarmonyPatch(typeof(LevelUpController), nameof(LevelUpController.Commit))]
    internal static class CharacterBuildCommitHook
    {
        private static bool Prefix(LevelUpController __instance)
        {
            if (!SynthesistBlueprints.Ready) return true;
            var preview = __instance.Preview;
            if (preview == null || CharacterBuildIntegration.Form(preview.Descriptor) == null) return true;
            var part = preview.Get<UnitPartSynthesistBuild>();
            // Older prototype saves without a build ledger may still advance
            // other classes/mythic ranks. Their next Summoner level adopts the UI.
            if (__instance.State.SelectedClass != SynthesistBlueprints.SummonerClass && part?.Current == null && part?.Draft == null) return true;
            if (part?.Current?.Confirmed == true && part.Draft == null &&
                part.Current.Level == preview.Progression.GetClassLevel(SynthesistBlueprints.SummonerClass) &&
                part.Current.BonusEvolutionPoints == SummonerFeats.ExtraPoints(preview)) return true;
            Main.Log("Finish the eidolon evolution, appearance and review selections before committing this level.");
            return false;
        }
        private static void Postfix(LevelUpController __instance)
        {
            if (__instance.Committed) __instance.Unit.Get<UnitPartSynthesist>()?.ApplyCharacterBuild();
        }
    }

    internal static class EvolutionBuildAdapter
    {
        internal static void Apply(UnitPartSynthesist part, CharacterBuild build)
        {
            build.Validate();
            if (!build.Confirmed || build.Form != part.State.Form || build.Level != part.Level || build.BonusEvolutionPoints != SummonerFeats.ExtraPoints(part.Owner) ||
                !AppearanceIntegration.Compatible(build, AppearanceIntegration.Find(build.Appearance)))
                throw new InvalidOperationException("The committed eidolon build does not match this character.");
            if (part.IsFused || part.IsSplit || part.TwinActive || part.IsPreparing)
                throw new InvalidOperationException("Dismiss and close preparation before applying a character build.");
            // Record wounds/resources remain authoritative. Rebuild only purchased
            // evolution facts and allocation accounting, never base-form grants.
            part.ClearPurchasedEvolutions();
            foreach (var purchase in build.Purchases)
            {
                var unit = purchase.Aspect ? part.Owner : part.Eidolon;
                unit.AddFact(SynthesistBlueprints.BuildFeature(purchase.Key + "Feature"));
                if (purchase.Key == DamageReductionIntegration.Key) unit.AddFact(DamageReductionIntegration.Choices[purchase.Option]);
                for (int i = 0; i < purchase.Cost; i++) unit.AddFact(SynthesistBlueprints.ExistingFeature("EvolutionCost1Feature"));
            }
            for (int i = 0; i < AspectBudget.Diverted(build.Level, build.AspectSpent); i++)
                part.Eidolon.AddFact(SynthesistBlueprints.ExistingFeature("EvolutionCost1Feature"));
            part.SyncProgression();
            foreach (var unit in new[] { part.Owner, part.Eidolon })
                foreach (string resourceName in new[] { "SummonerEvolutionPointsResource", "EidolonMaxAttacksResource" })
                {
                    var resource = BlueprintCore.Utils.BlueprintTool.Get<BlueprintAbilityResource>(SynthesistBlueprints.Id(resourceName));
                    if (unit.Descriptor.Resources.ContainsResource(resource)) unit.Descriptor.Resources.Restore(resource);
                }
            part.State.CloseCharacterBuild();
            AppearanceIntegration.ApplyCanonical(part);
        }
    }
}
