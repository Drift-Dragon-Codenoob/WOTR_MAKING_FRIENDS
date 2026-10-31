using BlueprintCore.Utils;
using Kingmaker;
using Kingmaker.Blueprints;
using Kingmaker.Blueprints.JsonSystem;
using Kingmaker.ElementsSystem;
using Kingmaker.EntitySystem.Entities;
using Kingmaker.UnitLogic;
using Kingmaker.UnitLogic.Abilities;
using Kingmaker.UnitLogic.Abilities.Blueprints;
using Kingmaker.UnitLogic.Abilities.Components;
using Kingmaker.UnitLogic.Abilities.Components.Base;
using Kingmaker.UnitLogic.Mechanics.Actions;
using Kingmaker.Utility;
using System;
using System.Collections.Generic;
using WOTR_MAKING_FRIENDS.Synthesist.Blueprints;
using WOTR_MAKING_FRIENDS.Synthesist.Rules;

namespace WOTR_MAKING_FRIENDS.Synthesist.Runtime
{
    internal enum SynthAction { Prepare, Confirm, Fuse, Dismiss, Link, Jump, Split, Rejoin, Twin, Rejuvenate, Reset, EndTwin }

    [TypeId("bf825a75e3fd45b2ab871167d07a2056")]
    public sealed class FusedLinkPermission : UnitFactComponentDelegate
    {
        public override void OnTurnOn() { var p = Owner.Get<UnitPartSynthesist>(); if (p?.State != null) { p.State.LinkEnabled = true; p.State.PersonalHpLimit = Settings.Settings.GetSetting<int>("fusedlinkminimumhp"); } }
        public override void OnTurnOff() { var p = Owner.Get<UnitPartSynthesist>(); if (p?.State != null) p.State.LinkEnabled = false; }
    }

    [TypeId("a279a5b079504851a96f465d7e266024")]
    public sealed class SynthesistController : UnitFactComponentDelegate, Kingmaker.PubSubSystem.IResourceAmountBonusHandler,
        Kingmaker.PubSubSystem.IUnitSubscriber, Kingmaker.PubSubSystem.ISubscriber
    {
        public void CalculateMaxResourceAmount(BlueprintAbilityResource resource, ref int bonus)
        {
            int level = Owner.Progression.GetClassLevel(SynthesistBlueprints.SummonerClass);
            foreach (DailyAbility ability in Enum.GetValues(typeof(DailyAbility)))
                if (resource == SynthesistBlueprints.Resource(ability))
                    bonus += ability == DailyAbility.MakersJump ? EidolonProgression.MakersJumpUses(level) :
                        ability == DailyAbility.SplitForms ? (level >= 16 ? level : 0) : (level == 20 ? 20 : 0);
        }
    }

    [TypeId("42c667c376044d3cb095913c9fed9ac7")]
    public sealed class SynthesistForm : UnitFactComponentDelegate
    {
        internal EidolonForm Form;
        public override void OnTurnOn()
        {
            if (!Owner.PreviewOf.IsEmpty) return;
            Owner.Ensure<UnitPartSynthesist>().Initialise(Form);
        }
    }

    [TypeId("3f6817cd6a7f46e497a09f3e13248140")]
    public sealed class SynthesistRestriction : BlueprintComponent, IAbilityCasterRestriction
    {
        internal SynthAction Action;
        public bool IsCasterRestrictionPassed(UnitEntityData caster)
        {
            var part = caster.Get<UnitPartSynthesist>();
            if (part?.State == null) return false;
            if (part.IsRitualActive) return Action == SynthAction.Dismiss;
            return Action switch
            {
                SynthAction.Prepare => part.State.Mode == FusionMode.Unfused && !part.TwinActive && !caster.IsInCombat,
                SynthAction.Confirm => part.IsPreparing && AppearanceIntegration.Compatible(part),
                SynthAction.Reset => part.IsPreparing && part.State.EvolutionEditsOpen,
                SynthAction.Fuse => part.State.Mode == FusionMode.Unfused && !part.TwinActive && !part.State.EvolutionEditsOpen &&
                    part.State.Durability.CanReturn(UnitPartSynthesist.Day) && !caster.Body.IsPolymorphed &&
                    part.Eidolon.Progression.CharacterLevel == EidolonProgression.At(part.Level).HitDice && AppearanceIntegration.Compatible(part),
                SynthAction.Dismiss => part.IsFused || part.TwinActive,
                SynthAction.Link => true,
                SynthAction.Jump => part.IsFused && caster.Descriptor.Resources.GetResourceAmount(SynthesistBlueprints.Resource(DailyAbility.MakersJump)) > 0,
                SynthAction.Split => part.IsFused && caster.Descriptor.Resources.GetResourceAmount(SynthesistBlueprints.Resource(DailyAbility.SplitForms)) > 0,
                SynthAction.Rejoin => part.IsSplit,
                SynthAction.Twin => part.State.Mode == FusionMode.Unfused && !part.TwinActive &&
                    !part.State.EvolutionEditsOpen && !caster.Body.IsPolymorphed && AppearanceIntegration.Compatible(part) &&
                    part.Eidolon.Progression.CharacterLevel == EidolonProgression.At(part.Level).HitDice &&
                    caster.Descriptor.Resources.GetResourceAmount(SynthesistBlueprints.Resource(DailyAbility.TwinEidolon)) > 0,
                SynthAction.EndTwin => part.TwinActive,
                SynthAction.Rejuvenate => part.IsFused || part.IsSplit,
                _ => false
            };
        }
        public string GetAbilityCasterRestrictionUIText() => "The Synthesist's current form, build or remaining resources do not permit this action.";
    }

    [TypeId("d20665c53a1e4a62a08676ff41f8fa33")]
    public sealed class SynthesistAction : ContextAction
    {
        internal SynthAction Action;
        public int DiceCount;
        public int MaximumCasterLevel;
        public override string GetCaption() => "Synthesist: " + Action;
        public override void RunAction()
        {
            var part = Context.MaybeCaster?.Get<UnitPartSynthesist>();
            if (part?.State == null) throw new InvalidOperationException("No Synthesist state is available.");
            switch (Action)
            {
                case SynthAction.Prepare: part.BeginPreparation(); break;
                case SynthAction.Confirm: part.ConfirmPreparation(); break;
                case SynthAction.Reset: part.ResetEvolutions(); break;
                case SynthAction.Dismiss: part.Dismiss(); break;
                case SynthAction.Link: part.State.LinkEnabled = !part.State.LinkEnabled; break;
                case SynthAction.Split: part.BeginSplit(); break;
                case SynthAction.Rejoin: part.EndSplit(); break;
                case SynthAction.Twin: part.BeginTwin(); break;
                case SynthAction.EndTwin: part.EndTwin(); break;
                case SynthAction.Jump:
                    part.State.UseMakersJump(part.Budget);
                    part.Owner.Position = Target.Point;
                    break;
                case SynthAction.Rejuvenate:
                    if (!part.IsFused && !part.IsSplit) throw new InvalidOperationException("A present eidolon is required.");
                    part.Eidolon.Damage = part.State.Durability.Wounds;
                    var healing = new Kingmaker.RuleSystem.Rules.Damage.RuleHealDamage(part.Owner, part.Eidolon,
                        new Kingmaker.RuleSystem.DiceFormula(DiceCount, Kingmaker.RuleSystem.DiceType.D10),
                        Math.Min(MaximumCasterLevel, Context.Params.CasterLevel));
                    if (Context.HasMetamagic(Metamagic.Empower)) healing.Empower.Set(true, Metamagic.Empower);
                    if (Context.HasMetamagic(Metamagic.Maximize)) healing.CalculationType.Set(Kingmaker.RuleSystem.Rules.Damage.DamageCalculationType.Maximized, Metamagic.Maximize);
                    Context.TriggerRule(healing);
                    part.State.Durability.SetIndependentWounds(part.Eidolon.Damage);
                    if (part.IsFused) part.SetPool(part.State.Durability.Remaining);
                    else part.Eidolon.Damage = part.State.Durability.Wounds;
                    break;
            }
        }
    }

    [TypeId("93d5291f29f74edba57071ec34c3fce8")]
    public sealed class FusionRitual : AbilityCustomLogic
    {
        public override bool IsEngageUnit => true;
        public override IEnumerator<AbilityDeliveryTarget> Deliver(AbilityExecutionContext context, TargetWrapper target)
        {
            var part = context.Caster.Get<UnitPartSynthesist>();
            part.BeginFusionRitual();
            yield break;
        }
        public override void Cleanup(AbilityExecutionContext context) { }
    }
}
