using System;
using System.Linq;
using Kingmaker.Blueprints.JsonSystem;
using Kingmaker.EntitySystem.Entities;
using Kingmaker.Enums;
using Kingmaker.Enums.Damage;
using Kingmaker.PubSubSystem;
using Kingmaker.RuleSystem;
using Kingmaker.RuleSystem.Rules.Damage;
using Kingmaker.UnitLogic;
using WOTR_MAKING_FRIENDS.Synthesist.Rules;

namespace WOTR_MAKING_FRIENDS.Synthesist.Runtime
{
    internal static class RendingQualification
    {
        internal static int ActualClaws(UnitEntityData unit) =>
            new[] { unit.Body.PrimaryHand.Weapon, unit.Body.SecondaryHand.Weapon }
                .Concat(unit.Body.AdditionalLimbs.Select(s => s.MaybeWeapon))
                .Count(w => w?.Blueprint.Category == WeaponCategory.Claw);
        internal static bool Active(UnitEntityData unit) => SummonerFeatRules.RendingQualification(unit.Stats.Strength.ModifiedValue, unit.Stats.BaseAttackBonus.ModifiedValue, ActualClaws(unit));
        internal static bool CanSelect(UnitEntityData unit)
        {
            if (Active(unit)) return true;
            var buildPart = unit.Get<UnitPartSynthesistBuild>();
            var build = buildPart?.Draft ?? buildPart?.Current;
            if (build == null || Blueprints.CharacterBuildIntegration.Form(unit.Descriptor) != build.Form) return false;
            int level = unit.Progression.GetClassLevel(Blueprints.SynthesistBlueprints.SummonerClass);
            if (level < 1) return false;
            int strength = BaseForms.For(build.Form).Strength + EidolonProgression.At(level).StrengthDexterityBonus +
                build.Rank("EvolutionLarge", false) * 8 + build.Rank("EvolutionAbilityIncreaseStrength", false) * 2;
            var original = unit.PreviewOf.Entity ?? unit;
            var canonical = original.Get<UnitPartSynthesist>()?.Eidolon;
            if (canonical != null && original.Get<UnitPartSynthesist>().Level > 0 && original.Get<UnitPartSynthesist>().State.Form == build.Form)
                strength += Math.Min(EidolonProgression.At(level).AbilityIncreases, Math.Max(0, canonical.Stats.Strength.BaseValue - BaseForms.For(build.Form).Strength -
                    EidolonProgression.At(original.Get<UnitPartSynthesist>().Level).StrengthDexterityBonus));
            int claws = (build.Rank("EvolutionClaws", false) > 0 ? 2 : 0) + build.Rank("EvolutionClawsLimbs", false) * 2;
            return SummonerFeatRules.RendingQualification(strength, EidolonProgression.At(level).BaseAttackBonus, claws);
        }
    }

    [TypeId("36ce70168335440ab5f965f044c7c647")]
    public sealed class RendingClawsDamage : UnitFactComponentDelegate, IInitiatorRulebookHandler<RulePrepareDamage>,
        IRulebookHandler<RulePrepareDamage>, IInitiatorRulebookSubscriber, IUnitNewCombatRoundHandler, IGlobalSubscriber, ISubscriber
    {
        public void HandleNewCombatRound(UnitEntityData unit)
        {
            if (unit != Owner) return;
            var state = Owner.Get<UnitPartSummonerFeats>();
            if (state != null) { state.Rending.Reset(); state.SeenAttacks.Clear(); }
        }
        public void OnEventAboutToTrigger(RulePrepareDamage evt)
        {
            var attack = evt.ParentRule.AttackRoll;
            var weapon = attack?.RuleAttackWithWeapon?.Weapon;
            if (attack?.IsHit != true || attack.RuleAttackWithWeapon?.IsRend == true || weapon?.Blueprint.Category != WeaponCategory.Claw || !RendingQualification.Active(Owner)) return;
            var state = Owner.Ensure<UnitPartSummonerFeats>();
            var combat = Kingmaker.Game.Instance.TurnBasedCombatController;
            state.Rending.BeginTurn(combat?.CurrentTurn == null ? "real-time personal round" : combat.RoundNumber + ":" + combat.TurnStartTime.Ticks);
            if (!state.SeenAttacks.Add(attack) || !state.Rending.Hit(evt.Target.UniqueId, true)) return;
            // Add to this hit's native bundle: one mitigation/difficulty pass,
            // normal precision immunity and no extra critical multiplication.
            var damage = new PhysicalDamage(new Kingmaker.RuleSystem.Rules.ModifiableDiceFormula(new DiceFormula(1, DiceType.D6)), 0, PhysicalDamageForm.Slashing)
            { Precision = true, CriticalModifier = 1 };
            evt.Add(damage);
        }
        public void OnEventDidTrigger(RulePrepareDamage evt) { }
    }
}
