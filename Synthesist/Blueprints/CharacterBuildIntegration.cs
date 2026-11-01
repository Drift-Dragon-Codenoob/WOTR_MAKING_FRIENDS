using System;
using System.Collections.Generic;
using System.Linq;
using BlueprintCore.Blueprints.CustomConfigurators.Classes;
using BlueprintCore.Blueprints.CustomConfigurators.Classes.Selection;
using Kingmaker.Blueprints;
using Kingmaker.Blueprints.Classes;
using Kingmaker.Blueprints.Classes.Prerequisites;
using Kingmaker.Blueprints.Classes.Selection;
using Kingmaker.Blueprints.JsonSystem;
using Kingmaker.UnitLogic;
using Kingmaker.UnitLogic.Class.LevelUp;
using WOTR_MAKING_FRIENDS.Synthesist.Rules;
using WOTR_MAKING_FRIENDS.Synthesist.Runtime;
using WOTR_MAKING_FRIENDS.Utilities;

namespace WOTR_MAKING_FRIENDS.Synthesist.Blueprints
{
    internal enum BuildAction { Rebuild, Keep, Buy, FinishPurchases, Appearance, Confirm, AppearanceGroup }
    internal sealed class BuildChoice
    {
        internal BuildAction Action;
        internal string Key;
        internal string Option;
        internal bool Aspect;
    }

    internal static class CharacterBuildIntegration
    {
        internal static readonly Dictionary<string, BuildChoice> Choices = new(StringComparer.Ordinal);
        internal static readonly Dictionary<string, EvolutionDefinition> Definitions = new(StringComparer.Ordinal);
        internal static BlueprintFeatureSelection Root, EidolonPurchases, AspectPurchases, Appearance, Summary;
        internal static readonly Dictionary<string, BlueprintFeatureSelection> AppearanceGroups = new(StringComparer.Ordinal);
        internal static string ChoiceName(string key, bool aspect, string option = null)
            => "SynthesistBuild" + (aspect ? "Aspect" : "Eidolon") + key + (option ?? "") + "Choice";

        internal static void Create()
        {
            var eidolon = new List<BlueprintFeatureReference>();
            var aspect = new List<BlueprintFeatureReference>();
            foreach (var pair in SynthesistBlueprints.EvolutionAbilities)
            {
                var feature = SynthesistBlueprints.BuildFeature(pair.Key + "Feature");
                var gate = pair.Value.GetComponent<EvolutionMasterRankRestriction>();
                var noFacts = pair.Value.ComponentsArray.Where(c => c.GetType().Name == "AbilityCasterHasNoFacts")
                    .SelectMany(c => c.GetType().GetField("m_Facts")?.GetValue(c) as BlueprintUnitFactReference[] ?? Array.Empty<BlueprintUnitFactReference>())
                    .Select(f => f.Get()?.name).Where(n => n != null).ToArray();
                Definitions[pair.Key] = new EvolutionDefinition
                {
                    Key = pair.Key, Cost = EvolutionIntegration.Costs[pair.Key],
                    Maximum = ManagedEvolutionRules.OwnsRank(pair.Key) ? ManagedEvolutionRules.MaximumRanks(pair.Key) : gate?.Maximum > 0 ? gate.Maximum : feature.Ranks,
                    Start = gate?.Start ?? 1, Interval = gate?.Interval ?? 0, ForbiddenFacts = noFacts
                };
                foreach (bool personal in new[] { false, true })
                {
                    if (personal && pair.Key.StartsWith("EvolutionAbilityIncrease", StringComparison.Ordinal)) continue;
                    foreach (string option in pair.Key == DamageReductionIntegration.Key ? DamageReductionIntegration.Choices.Keys : new[] { "" }.AsEnumerable())
                    {
                        string name = ChoiceName(pair.Key, personal, option);
                        var choice = NewChoice(name, EvolutionIntegration.DisplayName(pair.Key, feature.Name) + (option.Length > 0 ? " / " + option.ToLowerInvariant() : ""),
                            feature.Description + "\n\nEvolution costs, remaining points and prerequisites are shown below.",
                            new BuildChoice { Action = BuildAction.Buy, Key = pair.Key, Aspect = personal, Option = option });
                        choice.m_Icon = feature.m_Icon;
                        (personal ? aspect : eidolon).Add(choice.ToReference<BlueprintFeatureReference>());
                    }
                }
            }
            var finishEidolon = NewChoice("SynthesistBuildFinishEidolonChoice", "Finish eidolon evolutions",
                "Continue to appearance and review. Any remaining evolution points will be left unspent only when you explicitly confirm the final build.",
                new BuildChoice { Action = BuildAction.FinishPurchases });
            var finishAspect = NewChoice("SynthesistBuildFinishAspectChoice", "Finish Aspect allocation",
                "Continue to the eidolon. Greater Aspect diversion is calculated from the total allocation.",
                new BuildChoice { Action = BuildAction.FinishPurchases, Aspect = true });
            eidolon.Add(finishEidolon.ToReference<BlueprintFeatureReference>());
            aspect.Add(finishAspect.ToReference<BlueprintFeatureReference>());
            EidolonPurchases = Selection("SynthesistBuildEidolonSelection", "Eidolon evolutions", eidolon);
            AspectPurchases = Selection("SynthesistBuildAspectSelection", "Aspect evolutions", aspect);
            var categories = new List<BlueprintFeatureReference>();
            foreach (EidolonForm category in Enum.GetValues(typeof(EidolonForm)))
            {
                var models = new List<BlueprintFeatureReference>();
                foreach (var model in AppearanceIntegration.Models.Where(m => m.Category == category))
                {
                    var choice = NewChoice("SynthesistBuildAppearance" + model.Key + "Choice", model.Name,
                        AppearanceIntegration.Description(model), new BuildChoice { Action = BuildAction.Appearance, Key = model.Key });
                    choice.m_Icon = model.Source.PortraitSafe?.Data?.SmallPortrait;
                    models.Add(choice.ToReference<BlueprintFeatureReference>());
                }
                string key = category.ToString();
                AppearanceGroups[key] = Selection("SynthesistBuildAppearance" + key + "Selection", key + " appearances", models);
                var group = NewChoice("SynthesistBuildAppearance" + key + "GroupChoice", key + " appearances (" + models.Count + ")",
                    "Browse this visual group. All groups are available regardless of your mechanical base form or purchased limbs.",
                    new BuildChoice { Action = BuildAction.AppearanceGroup, Key = key });
                categories.Add(group.ToReference<BlueprintFeatureReference>());
            }
            Appearance = Selection("SynthesistBuildAppearanceSelection", "Eidolon appearance group", categories);
            var confirm = NewChoice("SynthesistBuildConfirmChoice", "Confirm eidolon build",
                "Review your build summary below. Confirming saves these choices, including any deliberately unspent points. Use the level-up screen's back controls to revise a choice.",
                new BuildChoice { Action = BuildAction.Confirm });
            var unspent = NewChoice("SynthesistBuildConfirmUnspentChoice", "Confirm and leave remaining evolution points unspent",
                "The remaining eidolon points shown below will deliberately remain unallocated. They cannot be spent during ordinary play after confirmation; allocation requires advancement or applicable retraining.",
                new BuildChoice { Action = BuildAction.Confirm, Option = "unspent" });
            Summary = Selection("SynthesistBuildSummarySelection", "Review eidolon build", new[] { confirm.ToReference<BlueprintFeatureReference>(), unspent.ToReference<BlueprintFeatureReference>() });
            var rebuild = NewChoice("SynthesistBuildRebuildChoice", "Allocate or rebuild eidolon evolutions",
                "Allocate the full current allowance. Your base form is retained. Existing play-state changes are applied only after completing character creation, level-up or respec.",
                new BuildChoice { Action = BuildAction.Rebuild });
            var keep = NewChoice("SynthesistBuildKeepChoice", "Keep evolutions and spend remaining points",
                "Retain your saved purchases, then allocate remaining points and review your appearance.",
                new BuildChoice { Action = BuildAction.Keep });
            Root = Selection("SynthesistCharacterBuildSelection", "Build your eidolon", new[] { rebuild.ToReference<BlueprintFeatureReference>(), keep.ToReference<BlueprintFeatureReference>() });
        }

        private static BlueprintFeature NewChoice(string name, string title, string description, BuildChoice choice)
        {
            Choices[name] = choice;
            return FeatureConfigurator.New(name, SynthesistBlueprints.Id(name))
                .SetDisplayName(SynthesistBlueprints.Text(name + ".name", title))
                .SetDescription(SynthesistBlueprints.Text(name + ".description", description))
                .SetRanks(100).SetHideInCharacterSheetAndLevelUp(false)
                .AddComponent<SynthesistBuildPrerequisite>(c => c.Choice = name).ConfigureWithLogging();
        }
        private static BlueprintFeatureSelection Selection(string name, string title, IEnumerable<BlueprintFeatureReference> choices)
        {
            var selection = FeatureSelectionConfigurator.New(name, SynthesistBlueprints.Id(name))
                .SetDisplayName(SynthesistBlueprints.Text(name + ".name", title))
                .SetDescription(SynthesistBlueprints.Text(name + ".description", "Complete the eidolon build as part of this character advancement. Purchases use evolution points, not daily ability uses."))
                .SetRanks(100).SetAllFeatures(choices.Select(c => (BlueprintCore.Utils.Blueprint<BlueprintFeatureReference>)c).ToArray()).ConfigureWithLogging();
            selection.Mode = SelectionMode.Default;
            return selection;
        }
        internal static EidolonForm? Form(UnitDescriptor unit)
        {
            foreach (EidolonForm form in Enum.GetValues(typeof(EidolonForm)))
                if (unit.Progression.Features.HasFact(SynthesistBlueprints.ExistingFeature("Synthesist" + form + "FormFeature"))) return form;
            return null;
        }
        internal static string Reason(string name, UnitDescriptor unit)
        {
            if (!Choices.TryGetValue(name, out var choice)) return "Unknown build choice.";
            var part = unit.Unit.Get<UnitPartSynthesistBuild>();
            var draft = part?.Draft;
            if (choice.Action is BuildAction.Rebuild or BuildAction.Keep)
            {
                var form = Form(unit);
                if (form == null) return "Choose an eidolon base form first.";
                if (choice.Action == BuildAction.Keep && part?.Current == null) return "No previous saved build.";
                if (choice.Action == BuildAction.Keep && (part.Current.Form != form || part.Current.Level > unit.Progression.GetClassLevel(SynthesistBlueprints.SummonerClass)))
                    return "Rebuild evolutions after changing base form or reducing Summoner level.";
                return null;
            }
            if (draft == null) return "Start your eidolon build first.";
            if (choice.Action == BuildAction.Buy)
                return CharacterBuildRules.Reason(draft, Definitions[choice.Key], choice.Option, choice.Aspect, unit.Alignment.ValueRaw.ToString());
            if (choice.Action == BuildAction.Appearance && !AppearanceIntegration.Compatible(draft, AppearanceIntegration.Find(choice.Key)))
                return "Choose a known appearance.";
            if (choice.Action == BuildAction.Confirm && !AppearanceIntegration.Compatible(draft, AppearanceIntegration.Find(draft.Appearance)))
                return "Select an appearance first.";
            if (choice.Action == BuildAction.Confirm && (draft.Remaining > 0) != (choice.Option == "unspent"))
                return draft.Remaining > 0 ? "Explicitly confirm leaving the remaining points unspent." : "There are no unspent eidolon points.";
            return null;
        }
        internal static string SummaryText(UnitDescriptor unit, bool detail = true)
        {
            var part = unit.Unit.Get<UnitPartSynthesistBuild>();
            var build = part?.Draft ?? part?.Current;
            if (build == null) return "Choose a base form and start an eidolon build.";
            string purchases = !detail ? "" : string.Join("\n", build.Purchases.Select(p => (p.Aspect ? "Aspect: " : "Eidolon: ") +
                EvolutionIntegration.DisplayName(p.Key, SynthesistBlueprints.BuildFeature(p.Key + "Feature").Name) + (string.IsNullOrEmpty(p.Option) ? "" : " / " + p.Option) + " (" + p.Cost + " points)"));
            return $"{build.Form}; appearance: {AppearanceIntegration.Find(build.Appearance)?.Name ?? "not chosen"}\n" +
                $"Eidolon points remaining: {build.Remaining}/{EidolonProgression.At(build.Level).EvolutionPoints + build.BonusEvolutionPoints} (Extra Evolution: +{build.BonusEvolutionPoints}). Aspect remaining: {build.AspectRemaining}.\n" +
                $"Natural attacks: {build.NaturalAttacks(false)}/{EidolonProgression.At(build.Level).MaximumNaturalAttacks}.\n" + purchases;
        }
    }

    [TypeId("20e55d5cbfd14376bf123fc365aafc71")]
    public sealed class SynthesistBuildPrerequisite : Prerequisite
    {
        public string Choice;
        public override bool CheckInternal(FeatureSelectionState selectionState, UnitDescriptor unit, LevelUpState state)
        {
            // A paid choice remains valid in its original slot after it consumes
            // points or reaches its rank limit. A new slot still checks the budget.
            if (selectionState?.SelectedItem?.Feature?.name == Choice) return true;
            return CharacterBuildIntegration.Reason(Choice, unit) == null;
        }
        public override string GetUITextInternal(UnitDescriptor unit)
        {
            var choice = CharacterBuildIntegration.Choices[Choice];
            var buildPart = unit.Unit.Get<UnitPartSynthesistBuild>();
            if (buildPart?.Draft == null && buildPart?.Current?.Confirmed == true && choice.Action is not (BuildAction.Rebuild or BuildAction.Keep))
                return "Confirmed build.\n" + CharacterBuildIntegration.SummaryText(unit, choice.Action == BuildAction.Confirm);
            string cost = choice.Action == BuildAction.Buy && unit.Unit.Get<UnitPartSynthesistBuild>()?.Draft is CharacterBuild draft
                ? "This purchase costs " + draft.Cost(choice.Key, CharacterBuildIntegration.Definitions[choice.Key].Cost, choice.Aspect) + " points.\n" : "";
            return cost + (CharacterBuildIntegration.Reason(Choice, unit) ?? "Available.") + "\n" + CharacterBuildIntegration.SummaryText(unit, choice.Action == BuildAction.Confirm);
        }
    }
}
