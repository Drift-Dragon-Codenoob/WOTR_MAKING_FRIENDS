using System;
using System.Collections.Generic;
using System.Linq;
using Kingmaker.EntitySystem;
using Kingmaker.EntitySystem.Entities;
using Kingmaker.EntitySystem.Stats;
using Kingmaker.Enums;
using Kingmaker.UnitLogic;
using WOTR_MAKING_FRIENDS.Synthesist.Blueprints;
using WOTR_MAKING_FRIENDS.Synthesist.Rules;

namespace WOTR_MAKING_FRIENDS.Synthesist.Runtime
{
    // These original-rules values differ from the base mod's Growth package. Own all of
    // their numerical modifiers together so natural armour and repeated purchases stack
    // correctly, without changing ordinary Summoners' feature blueprints.
    internal static class EvolutionStatistics
    {
        internal static readonly string[] Attributes = { "Strength", "Dexterity", "Constitution", "Intelligence", "Wisdom", "Charisma" };
        internal static bool IsManaged(string key) => key == "EvolutionLarge" || key == "EvolutionNaturalArmor" || key.StartsWith("EvolutionAbilityIncrease", StringComparison.Ordinal);
        internal static int Rank(UnitEntityData unit, string key) => unit.Progression.Features.GetRank(SynthesistBlueprints.BuildFeature(key + "Feature")) +
            unit.Progression.Features.GetRank(SynthesistBlueprints.ExistingFeature(key + "BaseFeature"));
        internal static string Signature(UnitPartSynthesist part)
        {
#if SYNTHESIST_ACCEPTANCE
            using var timing = RuntimePerformance.Measure("EvolutionStatistics.Signature");
#endif
            return part.Level + ":" + string.Join(",", new[] { part.Owner, part.Eidolon }
                .SelectMany(unit => new[] { Rank(unit, "EvolutionLarge"), Rank(unit, "EvolutionNaturalArmor") }.Concat(Attributes.Select(a => Rank(unit, "EvolutionAbilityIncrease" + a)))));
        }

        internal static int AttributeBonus(UnitEntityData unit, int attribute)
        {
            int growth = Rank(unit, "EvolutionLarge");
            int bonus = attribute == 0 ? growth * 8 : attribute == 1 ? growth * -2 : attribute == 2 ? growth * 4 : 0;
            return bonus + Rank(unit, "EvolutionAbilityIncrease" + Attributes[attribute]) * 2;
        }
        internal static int NaturalArmour(UnitEntityData unit)
        {
            int growth = Rank(unit, "EvolutionLarge");
            return Rank(unit, "EvolutionNaturalArmor") * 2 + (growth >= 2 ? 5 : growth == 1 ? 2 : 0);
        }

        internal static int SharedNaturalArmourDelta(UnitPartSynthesist part)
        {
            // Native NaturalArmor is a stacking descriptor. Aspect's existing modifier
            // remains on the protagonist; add only the extra ranks supplied by the
            // canonical build, never the same evolution for a second time.
            int natural = Math.Max(Rank(part.Owner, "EvolutionNaturalArmor"), Rank(part.Eidolon, "EvolutionNaturalArmor"));
            int growth = Math.Max(Rank(part.Owner, "EvolutionLarge"), Rank(part.Eidolon, "EvolutionLarge"));
            return natural * 2 + (growth >= 2 ? 5 : growth == 1 ? 2 : 0) - NaturalArmour(part.Owner);
        }

        internal static void Refresh(UnitPartSynthesist part)
        {
            foreach (var modifier in part.CanonicalStatModifiers.Concat(part.AspectStatModifiers).ToArray()) modifier.Remove();
            part.CanonicalStatModifiers.Clear(); part.AspectStatModifiers.Clear();
            Apply(part.Eidolon, part.CanonicalStatModifiers, part.Eidolon.Descriptor.MainFact,
                EidolonProgression.At(part.Level).ArmourBonus + 2);
            Apply(part.Owner, part.AspectStatModifiers, part.Owner.Progression.Features.GetFact(SynthesistBlueprints.ExistingFeature("SummonerAspectFeature")), 0);
            var petSource = part.Eidolon.Descriptor.MainFact;
            int petReach = GrowthReachAdjustment(Rank(part.Eidolon, "EvolutionLarge"), part.State.Form == EidolonForm.Biped);
            if (petReach != 0) part.CanonicalStatModifiers.Add(part.Eidolon.Stats.GetStat(StatType.Reach).AddModifier(petReach, petSource, ModifierDescriptor.Other));
            var aspectSource = part.Owner.Progression.Features.GetFact(SynthesistBlueprints.ExistingFeature("SummonerAspectFeature"));
            int aspectReach = GrowthReachAdjustment(Rank(part.Owner, "EvolutionLarge"), true);
            if (aspectReach != 0 && aspectSource != null) part.AspectStatModifiers.Add(part.Owner.Stats.GetStat(StatType.Reach).AddModifier(aspectReach, aspectSource, ModifierDescriptor.Other));
            UpdateGrowthSize(part.Eidolon);
            UpdateGrowthSize(part.Owner);
        }

        private static void UpdateGrowthSize(UnitEntityData unit)
        {
            int rank = Rank(unit, "EvolutionLarge");
            foreach (string size in new[] { "Large", "Huge" })
            {
                var blueprint = SynthesistBlueprints.ExistingFeature("EvolutionSizeChange" + size + "Feature");
                var fact = unit.Progression.Features.GetFact(blueprint);
                bool wanted = rank > 0 && size == (rank == 1 ? "Large" : "Huge");
                if (fact != null && !wanted) unit.RemoveFact(fact);
                if (fact == null && wanted) unit.AddFact(blueprint);
            }
        }

        private static void Apply(UnitEntityData unit, List<ModifiableValue.Modifier> owned, EntityFact source, int baseArmour)
        {
            for (int i = 0; i < Attributes.Length; i++)
            {
                var stat = unit.Stats.GetStat((StatType)(i + 1));
                RemoveLegacy(stat);
                Add(stat, AttributeBonus(unit, i), ModifierDescriptor.UntypedStackable);
            }
            RemoveLegacy(unit.Stats.AC);
            Add(unit.Stats.AC, baseArmour + NaturalArmour(unit), ModifierDescriptor.NaturalArmor);
            void Add(ModifiableValue stat, int amount, ModifierDescriptor descriptor)
            { if (amount != 0 && source != null) owned.Add(stat.AddModifier(amount, source, descriptor)); }
        }

        private static void RemoveLegacy(ModifiableValue stat)
        {
            foreach (var modifier in stat.Modifiers.Where(m => m.Source?.Blueprint != null &&
                IsManaged(m.Source.Blueprint.name.Replace("BaseFeature", "").Replace("Feature", ""))).ToArray()) modifier.Remove();
        }

        internal static void Project(UnitPartSynthesist part, EntityFact source)
        {
            // Fusion retains the Summoner's own mental scores. During Split Forms
            // both independent creatures have the evolutions; Twin also copies them.
            int attributeCount = part.IsFused ? 3 : Attributes.Length;
            for (int i = 0; i < attributeCount; i++)
            {
                int amount = AttributeBonus(part.Eidolon, i) - AttributeBonus(part.Owner, i);
                if (amount != 0) part.ProjectionModifiers.Add(part.Owner.Stats.GetStat((StatType)(i + 1)).AddModifier(amount, source, ModifierDescriptor.UntypedStackable));
            }
        }

        internal static void ProjectSizeAndReach(UnitPartSynthesist part)
        {
            int growth = Rank(part.Eidolon, "EvolutionLarge");
            if (growth == 0) return;
            var blueprint = SynthesistBlueprints.ExistingFeature("EvolutionSizeChange" + (growth == 1 ? "Large" : "Huge") + "Feature");
            EntityFact fact = part.Owner.Progression.Features.GetFact(blueprint);
            if (fact == null)
            {
                fact = part.Owner.AddFact(blueprint);
                part.ProjectionFacts.Add(fact);
            }
            int reach = GrowthReachAdjustment(growth, part.IsSplit || part.State.Form == EidolonForm.Biped);
            reach -= GrowthReachAdjustment(Rank(part.Owner, "EvolutionLarge"), true);
            if (reach != 0) part.ProjectionModifiers.Add(part.Owner.Stats.GetStat(StatType.Reach).AddModifier(reach, fact, ModifierDescriptor.Other));
        }

        private static int GrowthReachAdjustment(int rank, bool biped)
            => Utilities.BaselineRules.GrowthReachAdjustment(rank, biped,
                rank < 1 ? 0 : WeaponSizeExtension.GetModifiers(rank == 1 ? Size.Large : Size.Huge).Reach -
                    WeaponSizeExtension.GetModifiers(Size.Medium).Reach);
    }
}
