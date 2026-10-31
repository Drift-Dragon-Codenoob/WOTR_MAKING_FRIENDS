using System;
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
                    part.Draft = CharacterBuild.Begin(level, form, part.Current, choice.Action == BuildAction.Keep);
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
                case BuildAction.Appearance:
                    part.Draft.Appearance = choice.Key;
                    next = CharacterBuildIntegration.Summary;
                    break;
                case BuildAction.Confirm:
                    part.Confirm();
                    break;
            }
            // Native action replay rebuilds this dependent chain when an earlier choice
            // is changed or unselected. Only a purchase creates another purchase slot.
            if (next != null)
            {
                var child = __0.AddSelection(selection, selection.Source, next, selection.Level);
                // Native SelectFeature.Apply does both operations for nested
                // selections. Merely appending a state leaves the UI's Next chain
                // empty, so the new purchase/appearance step never opens.
                selection.Select(__instance.Item, child);
            }
        }
    }

    [HarmonyPatch(typeof(LevelUpController), nameof(LevelUpController.Commit))]
    internal static class CharacterBuildCommitHook
    {
        private static bool Prefix(LevelUpController __instance)
        {
            if (!SynthesistBlueprints.Ready || __instance.State.SelectedClass != SynthesistBlueprints.SummonerClass) return true;
            var preview = __instance.Preview;
            if (preview == null || CharacterBuildIntegration.Form(preview.Descriptor) == null) return true;
            var part = preview.Get<UnitPartSynthesistBuild>();
            if (part?.Current?.Confirmed == true && part.Draft == null &&
                part.Current.Level == preview.Progression.GetClassLevel(SynthesistBlueprints.SummonerClass)) return true;
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
            if (!build.Confirmed || build.Form != part.State.Form || build.Level != part.Level ||
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
