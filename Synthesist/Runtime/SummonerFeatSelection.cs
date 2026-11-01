using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using Kingmaker.Blueprints;
using Kingmaker.Blueprints.Classes.Prerequisites;
using Kingmaker.Blueprints.Classes.Selection;
using Kingmaker.Blueprints.JsonSystem;
using Kingmaker.EntitySystem.Entities;
using Kingmaker.UnitLogic;
using Kingmaker.UnitLogic.Class.LevelUp;
using Kingmaker.UnitLogic.Class.LevelUp.Actions;
using WOTR_MAKING_FRIENDS.Synthesist.Blueprints;
using WOTR_MAKING_FRIENDS.Synthesist.Rules;

namespace WOTR_MAKING_FRIENDS.Synthesist.Runtime
{
    public enum FeatRequirement { Extra, Arcana, Rending, Eidolon }

    [TypeId("9ed9c2012234404a9e38c5e736ab32e0")]
    public sealed class SummonerFeatPrerequisite : Prerequisite
    {
        public FeatRequirement Requirement;
        public override bool CheckInternal(FeatureSelectionState selection, UnitDescriptor unit, LevelUpState state)
        {
            if (selection?.SelectedItem?.Feature == OwnerBlueprint) return true;
            bool eidolon = unit.Progression.GetClassLevel(SynthesistBlueprints.SummonerClass) > 0;
            return Requirement switch
            {
                FeatRequirement.Eidolon => eidolon,
                FeatRequirement.Extra => eidolon && SummonerFeats.ExtraPoints(unit.Unit) < SummonerFeatRules.ExtraEvolutionLimit(unit.Progression.CharacterLevel),
                FeatRequirement.Arcana => SummonerFeats.ArcanaChoices.Values.Where(c => c.SpellLevel < 0).Any(c => ArcanaRuntime.Available(unit, c)),
                FeatRequirement.Rending => RendingQualification.CanSelect(unit.Unit),
                _ => false
            };
        }
        public override string GetUITextInternal(UnitDescriptor unit) => Requirement switch
        {
            FeatRequirement.Extra => "Eidolon class feature; at most " + SummonerFeatRules.ExtraEvolutionLimit(unit.Progression.CharacterLevel) + " Extra Evolution selections at this character level.",
            FeatRequirement.Arcana => "Caster level 1 or higher; a spontaneous class with a limited list of spells known and an additional spell available to learn.",
            FeatRequirement.Rending => "Strength 13, two claw natural attacks, base attack bonus +6. A Synthesist may qualify through the selected fused build; appearance does not count.",
            _ => "Eidolon class feature."
        };
    }

    [TypeId("41fb985219fc4fc492ee69b1d30c4772")]
    public sealed class ArcanaSpellPrerequisite : Prerequisite, IParamPrerequisite
    {
        public string Choice;
        public override bool CheckInternal(FeatureSelectionState selection, UnitDescriptor unit, LevelUpState state)
            => selection?.SelectedItem?.Feature == OwnerBlueprint || ArcanaRuntime.Available(unit, SummonerFeats.ArcanaChoices[Choice]);
        public bool CanBeSelected(BlueprintParametrizedFeature feature, UnitDescriptor unit, FeatureParam param)
        {
            var choice = SummonerFeats.ArcanaChoices[Choice];
            if (choice.SpellLevel < 0 || !ArcanaRuntime.Available(unit, choice) ||
                param?.Value?.Blueprint is not Kingmaker.UnitLogic.Abilities.Blueprints.BlueprintAbility spell) return false;
            return ArcanaRuntime.Unknown(ArcanaRuntime.Book(unit, choice), choice.SpellLevel).Contains(spell);
        }
        public override string GetUITextInternal(UnitDescriptor unit)
        {
            var choice = SummonerFeats.ArcanaChoices[Choice];
            return choice.Lower ? "Both spells must be unknown and at least one spell level below this class's highest available spell level." : "Choose an unknown spell from this class's available native spell list.";
        }
    }

    internal static class ArcanaRuntime
    {
        internal static Spellbook Book(UnitDescriptor unit, ArcanaChoice choice)
            => unit.Unit.GetSpellbook(SummonerFeats.Classes[choice.ClassKey]);
        internal static bool Eligible(Spellbook book) => book != null && book.CasterLevel >= 1 && book.Blueprint.Spontaneous &&
            !book.Blueprint.AllSpellsKnown && book.Blueprint.SpellsKnown != null && book.Blueprint.SpellList != null;
        internal static IEnumerable<Kingmaker.UnitLogic.Abilities.Blueprints.BlueprintAbility> Unknown(Spellbook book, int level)
            => book.Blueprint.SpellList.SpellsByLevel.Where(l => l.SpellLevel == level).SelectMany(l => l.Spells).Distinct().Where(s => !book.IsKnown(s));
        internal static bool Available(UnitDescriptor unit, ArcanaChoice choice)
        {
            var book = Book(unit, choice);
            if (!Eligible(book)) return false;
            int maximum = SummonerFeatRules.ArcanaMaximum(book.MaxSpellLevel, choice.Lower);
            if (choice.SpellLevel >= 0) return choice.SpellLevel <= maximum && Unknown(book, choice.SpellLevel).Any();
            return Enumerable.Range(0, Math.Max(0, maximum + 1)).SelectMany(l => Unknown(book, l)).Distinct().Take(choice.Lower ? 2 : 1).Count() >= (choice.Lower ? 2 : 1);
        }
    }

    // Use the actual replacement spellbook's list, including archetype additions.
    // Native LearnSpellParametrized grants the choice at its original spell level.
    [HarmonyPatch(typeof(BlueprintParametrizedFeature), "ExtractItemsFromSpellList")]
    internal static class ArcanaSpellPicker
    {
        private static bool Prefix(BlueprintParametrizedFeature __instance, UnitDescriptor __0, ref IEnumerable<FeatureUIData> __result)
        {
            if (!SummonerFeats.ArcanaChoices.TryGetValue(__instance.name, out var choice) || choice.SpellLevel < 0) return true;
            if (__0 == null)
            {
                // Native CanSelect checks a cached, unit-independent superset.
                // Do not cache one character's known-spell filtering globally.
                __result = SummonerFeats.SpellLists[choice.ClassKey].SelectMany(l => l.SpellsByLevel)
                    .Where(l => l.SpellLevel == choice.SpellLevel).SelectMany(l => l.Spells).Distinct()
                    .Select(s => new FeatureUIData(__instance, new FeatureParam(s))).ToArray();
                return false;
            }
            var book = ArcanaRuntime.Book(__0, choice);
            __result = !ArcanaRuntime.Available(__0, choice) ? Array.Empty<FeatureUIData>() :
                ArcanaRuntime.Unknown(book, choice.SpellLevel).Select(s => new FeatureUIData(__instance, new FeatureParam(s))).ToArray();
            return false;
        }
    }

    [HarmonyPatch(typeof(SelectFeature), nameof(SelectFeature.Apply))]
    internal static class SummonerFeatSelection
    {
        private static void Postfix(SelectFeature __instance, LevelUpState __0, UnitDescriptor __1)
        {
            if (!SynthesistBlueprints.Ready || __instance.Item?.Feature == null) return;
            string name = __instance.Item.Feature.name;
            var previous = __instance.GetSelectionState(__0);
            BlueprintFeatureSelection next = null;
            if (name == SummonerFeats.Arcana) next = SummonerFeats.ArcanaModes;
            else if (SummonerFeats.ArcanaChoices.TryGetValue(name, out var choice))
            {
                if (choice.SpellLevel < 0) next = SummonerFeats.SpellSelections[SummonerFeats.SelectionName(choice.ClassKey, choice.Lower ? "LowerFirst" : "One")];
                else if (choice.Lower && __instance.Item.Param?.Value?.Blueprint is Kingmaker.UnitLogic.Abilities.Blueprints.BlueprintAbility)
                {
                    for (var page = previous; page != null; page = page.Parent)
                        if (ReferenceEquals(page.Selection, SummonerFeats.SpellSelections[SummonerFeats.SelectionName(choice.ClassKey, "LowerFirst")]))
                        { next = SummonerFeats.SpellSelections[SummonerFeats.SelectionName(choice.ClassKey, "LowerSecond")]; break; }
                }
            }
            else if (name == SummonerFeats.Extra && CharacterBuildIntegration.Form(__1) != null)
            {
                var build = __1.Unit.Get<UnitPartSynthesistBuild>();
                if (build?.Draft != null) build.Draft.BonusEvolutionPoints = SummonerFeats.ExtraPoints(__1.Unit);
                bool pending = __0.Selections.Any(s => ReferenceEquals(s.Selection, CharacterBuildIntegration.Root) && s.SelectedItem == null);
                if (!pending && build?.Draft == null) next = CharacterBuildIntegration.Root;
            }
            if (next != null) __0.AddSelection(null, previous.Source, next, previous.Level);
        }
    }
}
