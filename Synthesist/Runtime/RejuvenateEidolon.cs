using System;
using Kingmaker.Blueprints;
using Kingmaker.Blueprints.JsonSystem;
using Kingmaker.EntitySystem.Entities;
using Kingmaker.RuleSystem;
using Kingmaker.RuleSystem.Rules.Damage;
using Kingmaker.UnitLogic.Abilities;
using Kingmaker.UnitLogic.Abilities.Components.Base;
using Kingmaker.UnitLogic.Mechanics.Actions;
using Kingmaker.Utility;
using WOTR_MAKING_FRIENDS.Synthesist.Blueprints;

namespace WOTR_MAKING_FRIENDS.Synthesist.Runtime
{
    [TypeId("8b0f6641b7ee47da8835c24ab2154930")]
    public sealed class RejuvenateTarget : BlueprintComponent, IAbilityTargetRestriction
    {
        public bool IsTargetRestrictionPassed(UnitEntityData caster, TargetWrapper target)
        {
            var unit = target?.Unit;
            if (unit == null) return false;
            var fused = unit.Get<UnitPartSynthesist>();
            if (fused?.IsFused == true) return !fused.State.Durability.Slain;
            return !unit.State.IsDead && unit.Master != null &&
                unit.Progression.Features.HasFact(SynthesistBlueprints.ExistingFeature("EidolonSubtypeFeature"));
        }
        public string GetAbilityTargetRestrictionUIText(UnitEntityData caster, TargetWrapper target) => "Requires a living eidolon or fused Synthesist.";
    }

    [TypeId("358d66d41ec247f3a80f4d875f8e6c2f")]
    public sealed class RejuvenateEidolon : ContextAction
    {
        public int DiceCount;
        public int MaximumCasterLevel;
        public override string GetCaption() => "Restore eidolon hit points";
        public override void RunAction()
        {
            var unit = Target.Unit;
            var part = unit?.Get<UnitPartSynthesist>();
            bool fused = part?.IsFused == true;
            UnitEntityData recipient = fused ? part.Eidolon : unit;
            if (recipient == null || recipient.State.IsDead) return;
            if (fused) recipient.Damage = part.State.Durability.Wounds;
            var healing = new RuleHealDamage(Context.MaybeCaster, recipient, new DiceFormula(DiceCount, DiceType.D10),
                Math.Min(MaximumCasterLevel, Context.Params.CasterLevel));
            if (Context.HasMetamagic(Metamagic.Empower)) healing.Empower.Set(true, Metamagic.Empower);
            if (Context.HasMetamagic(Metamagic.Maximize)) healing.CalculationType.Set(DamageCalculationType.Maximized, Metamagic.Maximize);
            Context.TriggerRule(healing);
            if (fused)
            {
                part.State.Durability.SetIndependentWounds(recipient.Damage);
                part.SetPool(part.State.Durability.Remaining);
            }
        }
    }
}
