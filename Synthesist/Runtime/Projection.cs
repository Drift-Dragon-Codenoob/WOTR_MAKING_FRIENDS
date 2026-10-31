using BlueprintCore.Utils;
using Kingmaker.Blueprints;
using Kingmaker.EntitySystem;
using Kingmaker.EntitySystem.Stats;
using Kingmaker.Enums;
using Kingmaker.UnitLogic;
using System;
using System.Linq;
using System.Collections.Generic;
using Kingmaker.EntitySystem.Entities;
using Kingmaker.UnitLogic.Buffs;
using WOTR_MAKING_FRIENDS.Synthesist.Blueprints;
using WOTR_MAKING_FRIENDS.Synthesist.Rules;

namespace WOTR_MAKING_FRIENDS.Synthesist.Runtime
{
    internal static class Projection
    {
        internal static IEnumerable<Buff> Buffs(UnitEntityData unit) { foreach (var buff in unit.Buffs) yield return buff; }
        internal static void Apply(UnitPartSynthesist part, bool twin)
        {
            var owner = part.Owner;
            var eidolon = part.Eidolon;
            var state = part.State;
            var source = owner.Buffs.AddBuff(AppearanceIntegration.FusionBuff(part), owner.Descriptor, null);
            Delta(owner.Stats.Strength, eidolon.Stats.Strength.BaseValue - owner.Stats.Strength.BaseValue);
            Delta(owner.Stats.Dexterity, eidolon.Stats.Dexterity.BaseValue - owner.Stats.Dexterity.BaseValue);
            Delta(owner.Stats.Constitution, eidolon.Stats.Constitution.BaseValue - owner.Stats.Constitution.BaseValue);
            Delta(owner.Stats.Speed, eidolon.Stats.Speed.BaseValue - owner.Stats.Speed.BaseValue);
            if (!twin) Delta(owner.Stats.BaseAttackBonus, EidolonProgression.At(state.SummonerLevel).BaseAttackBonus - owner.Stats.BaseAttackBonus.BaseValue);
            Delta(owner.Stats.AC, EidolonProgression.At(state.SummonerLevel).ArmourBonus + 2 +
                EvolutionStatistics.SharedNaturalArmourDelta(part), ModifierDescriptor.NaturalArmor);
            EvolutionStatistics.Project(part, source);
            int meld = twin ? 0 : EidolonProgression.MeldBonus(state.SummonerLevel);
            if (meld > 0)
            {
                Delta(owner.Stats.AC, meld, ModifierDescriptor.Shield);
                Delta(owner.Stats.SaveFortitude, meld, ModifierDescriptor.Circumstance);
                Delta(owner.Stats.SaveReflex, meld, ModifierDescriptor.Circumstance);
                Delta(owner.Stats.SaveWill, meld, ModifierDescriptor.Circumstance);
            }
            ApplyEvolutions(part);
            part.ProjectionFacts.Add(owner.AddFact(BlueprintCore.Blueprints.References.FeatureRefs.OutsiderType.Reference.Get()));
            part.ProjectionFacts.Add(owner.AddFact(BlueprintCore.Blueprints.References.FeatureRefs.SubtypeExtraplanar.Reference.Get()));
            ApplySpecials(part);
            part.ArmourSuppressed = true;
            owner.Body.Armor.RetainDeactivateFlag();
            owner.Body.HandsAreEnabled = HasArms(part);
            if (state.Form == EidolonForm.Biped)
                part.ProjectionFacts.Add(owner.Buffs.AddBuff(SynthesistBlueprints.Buff("EvolutionClawsBuff"), owner.Descriptor, null));
            if (!twin) part.SetPool(state.Durability.Remaining);
            EvolutionHealing.Refresh(part);

            void Delta(ModifiableValue stat, int value, ModifierDescriptor descriptor = ModifierDescriptor.UntypedStackable)
            {
                if (value != 0) part.ProjectionModifiers.Add(stat.AddModifier(value, source, descriptor));
            }
        }

        internal static bool HasArms(UnitPartSynthesist part)
            => part.State.Form == EidolonForm.Biped || part.Eidolon.Progression.Features.HasFact(SynthesistBlueprints.ExistingFeature("EvolutionLimbsArmsFeature"));

        internal static void ApplySplitStatistics(UnitPartSynthesist part)
        {
            var source = part.Owner.Progression.Features.GetFact(SynthesistBlueprints.ExistingFeature("SynthesistSplitFormsFeature"));
            if (source == null) throw new InvalidOperationException("Split Forms has no owning feature.");
            EvolutionStatistics.Project(part, source);
            int armour = EvolutionStatistics.SharedNaturalArmourDelta(part);
            if (armour != 0) part.ProjectionModifiers.Add(part.Owner.Stats.AC.AddModifier(armour, source, ModifierDescriptor.NaturalArmor));
        }

        internal static void ApplyEvolutions(UnitPartSynthesist part)
        {
            foreach (var pair in SynthesistBlueprints.ProjectionFeatures)
            {
                var original = SynthesistBlueprints.BuildFeature(pair.Key);
                int ranks = part.Eidolon.Progression.Features.GetRank(original);
                // Aspect already gives personal evolutions. Do not add a second identical root.
                int personalRanks = part.Owner.Progression.Features.GetRank(original);
                // DR sources overlap rather than adding. A projected upgrade needs
                // its complete value (10), not only the rank above Aspect's DR 5.
                ranks = DamageReductionIntegration.IsChoice(pair.Key)
                    ? ranks > personalRanks ? ranks : 0
                    : ranks - personalRanks;
                for (int i = 0; i < ranks; i++) part.ProjectionFacts.Add(part.Owner.AddFact(pair.Value));
            }
            EvolutionStatistics.ProjectSizeAndReach(part);
        }

        internal static void ApplySpecials(UnitPartSynthesist part)
        {
            foreach (var special in Specials(part.Level))
                if (!part.Owner.Descriptor.HasFact(special)) part.ProjectionFacts.Add(part.Owner.AddFact(special));
        }

        internal static IEnumerable<Kingmaker.Blueprints.Facts.BlueprintUnitFact> Specials(int level)
        {
            if (level >= 2) yield return BlueprintCore.Blueprints.References.FeatureRefs.Evasion.Reference.Get();
            if (level >= 6) yield return BlueprintCore.Blueprints.References.FeatureRefs.AnimalCompanionDevotion.Reference.Get();
            if (level >= 9) yield return BlueprintCore.Blueprints.References.FeatureRefs.Multiattack.Reference.Get();
            if (level >= 14) yield return BlueprintCore.Blueprints.References.FeatureRefs.ImprovedEvasion.Reference.Get();
        }

        internal static void Remove(UnitPartSynthesist part)
        {
            part.TemporaryPool?.Remove(); part.TemporaryPool = null;
            foreach (var fact in part.ProjectionFacts.ToArray()) part.Owner.RemoveFact(fact);
            part.ProjectionFacts.Clear();
            foreach (var modifier in part.ProjectionModifiers.ToArray()) modifier.Remove();
            part.ProjectionModifiers.Clear();
            foreach (var buff in Buffs(part.Owner).Where(b => SynthesistBlueprints.IsFusionBuff(b.Blueprint)).ToArray()) buff.Remove();
            if (part.ArmourSuppressed) { part.Owner.Body.Armor.ReleaseDeactivateFlag(); part.ArmourSuppressed = false; }
            part.Owner.Body.HandsAreEnabled = !part.Owner.OriginalBlueprint.Body.DisableHands;
        }
    }
}
