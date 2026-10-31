using Kingmaker;
using Kingmaker.Blueprints;
using Kingmaker.Blueprints.Classes;
using Kingmaker.Blueprints.JsonSystem;
using Kingmaker.EntitySystem;
using Kingmaker.EntitySystem.Entities;
using Kingmaker.EntitySystem.Stats;
using Kingmaker.Enums;
using Kingmaker.UnitLogic;
using Kingmaker.UnitLogic.Buffs;
using Kingmaker.UnitLogic.Buffs.Blueprints;
using Kingmaker.UnitLogic.Parts;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using WOTR_MAKING_FRIENDS.Synthesist.Blueprints;
using WOTR_MAKING_FRIENDS.Synthesist.Rules;

namespace WOTR_MAKING_FRIENDS.Synthesist.Runtime
{
    [TypeId("82641e5fe1494af3b42d0c6e5f3d8752")]
    public sealed class UnitPartSynthesist : UnitPart, IUnitPartSurviveRespec
    {
        [JsonProperty] internal FusionState State;
        [JsonProperty] internal EntityRef<UnitEntityData> CanonicalEidolon;
        [JsonProperty] private int appliedPhysicalBonus;
        [JsonProperty] private int appliedEvolutionPoints;
        [JsonProperty] private int appliedAttackSteps;
        [JsonProperty] private int appliedBuildRevision = -1;
        [JsonProperty] internal List<EntityFact> ProjectionFacts = new();
        [JsonProperty] internal List<ModifiableValue.Modifier> ProjectionModifiers = new();
        [JsonProperty] internal List<ModifiableValue.Modifier> CanonicalStatModifiers = new();
        [JsonProperty] internal List<ModifiableValue.Modifier> AspectStatModifiers = new();
        [JsonIgnore] private string evolutionStatisticsSignature;
        [JsonProperty] internal ModifiableValue.Modifier TemporaryPool;
        [JsonProperty] internal Buff PersonalEvolutionHealing;
        [JsonProperty] internal Buff PetEvolutionHealing;
        [JsonProperty] internal List<Buff> SplitEffectCopies = new();
        [JsonProperty] internal bool TwinActive;
        [JsonProperty] internal bool ArmourSuppressed;
        [JsonProperty] internal long TwinPaidUntil;
        [JsonProperty] internal long RitualEndsAt;
        [JsonIgnore] internal bool Changing;
        [JsonIgnore] internal bool NeedsReconcile;
        [JsonIgnore] internal bool CollapsePending;
        [JsonIgnore] private bool respecSuspended;
        [JsonIgnore] private bool sawRespecController;

        public bool ShouldSurviveRespec => true;

        internal void SuspendForRespec()
        {
            if (respecSuspended || State == null) return;
            respecSuspended = true;
            if (Eidolon?.Master != null) Eidolon.RemoveMaster();
        }

        public void CopyAfterRespec(UnitEntityData unit)
        {
            var previous = State;
            var pet = Eidolon;
            ClearOwnedEvolutionEffects();
            if (pet != null) { if (pet.Master != null) pet.RemoveMaster(); pet.MarkForDestroy(); }
            if (!unit.Progression.Features.HasFact(SynthesistBlueprints.ExistingFeature("SynthesistFusedEidolonFeature")))
            {
                var controls = unit.Progression.Features.GetFact(SynthesistBlueprints.ExistingFeature("SynthesistEvolutionControlsFeature"));
                if (controls != null) unit.RemoveFact(controls);
                unit.Remove<UnitPartSynthesistBuild>();
                unit.Remove<UnitPartSynthesist>();
                return;
            }
            var replacement = unit.Ensure<UnitPartSynthesist>();
            replacement.State = null;
            replacement.CanonicalEidolon = default;
            replacement.appliedPhysicalBonus = replacement.appliedEvolutionPoints = replacement.appliedAttackSteps = 0;
            replacement.appliedBuildRevision = -1;
            replacement.respecSuspended = false;
            foreach (EidolonForm form in Enum.GetValues(typeof(EidolonForm)))
            {
                if (!unit.Progression.Features.HasFact(SynthesistBlueprints.ExistingFeature("Synthesist" + form + "FormFeature"))) continue;
                replacement.Initialise(form);
                if (previous != null)
                {
                    previous.Durability.ChangeMaximum(replacement.State.Durability.Maximum);
                    replacement.State.AdoptDurability(previous.Durability);
                    replacement.State.PersonalHpLimit = previous.PersonalHpLimit;
                }
                break;
            }
        }

        internal UnitEntityData Eidolon => CanonicalEidolon.Entity;
        internal int Level => Owner.Progression.GetClassLevel(SynthesistBlueprints.SummonerClass);
        internal NativeBudget Budget => new(Owner);
        internal static long Now => Game.Instance.TimeController.GameTime.Ticks;
        internal static int Day => (int)Game.Instance.TimeController.GameTime.TotalDays;
        internal bool IsFused => State?.Mode == FusionMode.Fused;
        internal bool IsSplit => State?.Mode == FusionMode.Split;
        internal bool IsPreparing => State?.Mode == FusionMode.Preparation;
        internal bool IsRitualActive => RitualEndsAt != 0;

        public override void OnPostLoad()
        {
            base.OnPostLoad();
            State?.Validate();
            // Item-slot countable flags are reconstructed by the native game; our saved
            // ownership marker must not release a flag which has not been retained yet.
            ArmourSuppressed = false;
            NeedsReconcile = true;
        }

        internal void Initialise(EidolonForm form)
        {
            if (State != null) return;
            if (Owner.HoldingState == null || !Owner.PreviewOf.IsEmpty) return;
            var pet = Game.Instance.EntityCreator.SpawnUnit(SynthesistBlueprints.Pet(form), Owner.Position,
                Quaternion.Euler(0, Owner.Orientation, 0), Game.Instance.Player.CrossSceneState);
            CanonicalEidolon = pet;
            pet.SetMaster(Owner, Enums.PetTypeExtensions.Eidolon);
            pet.Descriptor.Faction = Owner.Descriptor.Faction;
            pet.Descriptor.Alignment.Set(Owner.Descriptor.Alignment.ValueRaw, false);
            RemoveNpcDifficulty();
            State = new FusionState(pet.UniqueId, form, Level, Math.Max(1, pet.MaxHP));
            State.PersonalHpLimit = Settings.Settings.GetSetting<int>("fusedlinkminimumhp");
            SyncProgression();
            if (form == EidolonForm.Biped)
                pet.Buffs.AddBuff(SynthesistBlueprints.Buff("EvolutionClawsBuff"), Owner.Descriptor, null);
            pet.IsInGame = false;
            ApplyCharacterBuild();
        }

        internal void ApplyCharacterBuild()
        {
            if (State == null || Eidolon == null || !Owner.PreviewOf.IsEmpty) return;
            var build = Owner.Get<UnitPartSynthesistBuild>();
            if (build?.Current == null || build.Revision == appliedBuildRevision) return;
            EvolutionBuildAdapter.Apply(this, build.Current);
            appliedBuildRevision = build.Revision;
        }

        internal void SyncProgression()
        {
            if (State == null || Eidolon == null || Level < 1) return;
            bool advanced = Level != State.SummonerLevel;
            if (advanced) State.SetLevel(Level);
            var row = EidolonProgression.At(Level);
            bool allowanceChanged = row.EvolutionPoints - 1 != appliedEvolutionPoints;
            // Let the native pet level-up UI allocate skills, feats and ability increases.
            int xp = Kingmaker.Blueprints.Root.BlueprintRoot.Instance.Progression.XPTable.GetBonus(row.HitDice);
            Eidolon.Progression.AdvanceExperienceTo(xp, false, true);
            int delta = row.StrengthDexterityBonus - appliedPhysicalBonus;
            Eidolon.Stats.Strength.BaseValue += delta;
            Eidolon.Stats.Dexterity.BaseValue += delta;
            appliedPhysicalBonus = row.StrengthDexterityBonus;
            foreach (var special in Projection.Specials(Level))
                if (!Eidolon.Descriptor.HasFact(special)) Eidolon.AddFact(special);
            AdjustRank("ExtraEvolutionPoolFeature", row.EvolutionPoints - 1, ref appliedEvolutionPoints);
            AdjustRank("AddMaxAttacksStepFeature", row.MaximumNaturalAttacks - 3, ref appliedAttackSteps);
            if (advanced || allowanceChanged)
            {
                RestoreAllocationResources(Eidolon);
                if (Level >= 10) RestoreAllocationResources(Owner);
            }
            EvolutionStatistics.Refresh(this);
            Eidolon.Body.HandsAreEnabled = Projection.HasArms(this);
            evolutionStatisticsSignature = EvolutionStatistics.Signature(this);
            State.Durability.ChangeMaximum(Math.Max(1, Eidolon.MaxHP));
        }

        private static void RestoreAllocationResources(UnitEntityData unit)
        {
            foreach (string name in new[] { "SummonerEvolutionPointsResource", "EidolonMaxAttacksResource" })
            {
                var resource = BlueprintCore.Utils.BlueprintTool.Get<BlueprintAbilityResource>(SynthesistBlueprints.Id(name));
                if (unit.Descriptor.Resources.ContainsResource(resource)) unit.Descriptor.Resources.Restore(resource);
            }
        }

        private void AdjustRank(string name, int desired, ref int applied)
        {
            var blueprint = SynthesistBlueprints.ExistingFeature(name);
            for (int i = applied; i < desired; i++) Eidolon.AddFact(blueprint);
            if (desired < applied)
                throw new InvalidOperationException("Eidolon rank reduction requires the respec rebuild path.");
            applied = desired;
        }

        internal void BeginPreparation()
        {
            State.EnterPreparation(Owner.IsInCombat);
            RestoreAllocationResources(Eidolon);
            if (Level >= 10) RestoreAllocationResources(Owner);
            // The fused pool owns wounds while this entity is dormant. Present the
            // same injuries in preparation instead of the entity's older HP snapshot.
            Eidolon.Damage = State.Durability.Wounds;
            Eidolon.Position = Owner.Position;
            Eidolon.IsInGame = true;
            Eidolon.Buffs.AddBuff(SynthesistBlueprints.PreparationBuff, Owner.Descriptor, null);
        }

        internal void ConfirmPreparation()
        {
            if (!AppearanceIntegration.Compatible(this)) throw new InvalidOperationException("Choose an appearance compatible with the eidolon's arms before confirming.");
            SyncProgression();
            if (Eidolon.Progression.CharacterLevel != EidolonProgression.At(Level).HitDice)
                throw new InvalidOperationException("Finish the eidolon's native level-up before confirming its build.");
            State.ConfirmEvolutions();
            Owner.Get<UnitPartSynthesistBuild>()?.ConfirmPreparation();
            State.LeavePreparation();
            RemovePetPreparation();
            Eidolon.IsInGame = false;
        }

        internal void ResetEvolutions()
        {
            if (!IsPreparing || !State.EvolutionEditsOpen) throw new InvalidOperationException("Evolution allocation is closed.");
            ClearPurchasedEvolutions();
            Owner.Get<UnitPartSynthesistBuild>()?.RecordReset();
        }

        internal void ClearPurchasedEvolutions()
        {
            foreach (var unit in new[] { Owner, Eidolon })
            {
                foreach (string key in SynthesistBlueprints.EvolutionAbilities.Keys)
                {
                    var fact = unit.Progression.Features.GetFact(SynthesistBlueprints.BuildFeature(key + "Feature"));
                    if (fact != null) unit.RemoveFact(fact);
                }
                DamageReductionIntegration.Reset(unit);
                for (int i = 1; i <= 4; i++)
                {
                    var fact = unit.Progression.Features.GetFact(SynthesistBlueprints.ExistingFeature("EvolutionCost" + i + "Feature"));
                    if (fact != null) unit.RemoveFact(fact);
                }
                unit.Descriptor.Resources.Restore(BlueprintCore.Utils.BlueprintTool.Get<BlueprintAbilityResource>(SynthesistBlueprints.Id("SummonerEvolutionPointsResource")));
                unit.Descriptor.Resources.Restore(BlueprintCore.Utils.BlueprintTool.Get<BlueprintAbilityResource>(SynthesistBlueprints.Id("EidolonMaxAttacksResource")));
            }
        }

        private void ReconcileAspectControls()
        {
            if (Level < 10) return;
            var controls = SynthesistBlueprints.ExistingFeature("SynthesistEvolutionControlsFeature");
            if (!Owner.Descriptor.HasFact(controls)) Owner.AddFact(controls);
            // Aspect owns its inherited menu facts. Repeatedly trying to remove those
            // retained facts raises native ability-removal notifications every frame,
            // rebuilding the entire action bar. Keep their ownership intact; the
            // archetype-scoped availability guard blocks both roots and all variants.
        }

        private void RemovePetPreparation()
        {
            foreach (var buff in Projection.Buffs(Eidolon).Where(b => b.Blueprint == SynthesistBlueprints.PreparationBuff).ToArray()) buff.Remove();
        }

        internal void EnterFusion()
        {
            if (Owner.Body.IsPolymorphed) throw new InvalidOperationException("Dismiss other transformations before fusion.");
            bool returning = State.Durability.Slain;
            State.CompleteFusionRitual(Day, (int)Owner.State.Size, (int)Eidolon.State.Size);
            // Reuse the canonical entity and its build. Native resurrection clears death
            // state without a full restore; assign the already-approved half-HP ledger
            // afterwards. Never revive it merely because a save, rest or area loaded.
            if (returning && Eidolon.State.IsDead) Eidolon.Descriptor.Resurrect(Owner, 0f, true);
            Eidolon.Damage = State.Durability.Wounds;
            Eidolon.IsInGame = false;
            try { ApplyProjection(false); }
            catch { RemoveProjection(); State.ForceDismiss(); throw; }
        }

        internal void BeginFusionRitual()
        {
            if (IsRitualActive || State.Mode != FusionMode.Unfused || State.EvolutionEditsOpen)
                throw new InvalidOperationException("Finish preparation before starting the fusion ritual.");
            RitualEndsAt = checked(Now + TimeSpan.TicksPerSecond * FusionState.RitualSeconds);
            Owner.Buffs.AddBuff(SynthesistBlueprints.Buff("SynthesistRitualBuff"), Owner.Descriptor, null);
        }

        private void EndRitual()
        {
            RitualEndsAt = 0;
            foreach (var buff in Projection.Buffs(Owner).Where(b => b.Blueprint == SynthesistBlueprints.Buff("SynthesistRitualBuff")).ToArray()) buff.Remove();
        }

        internal void Dismiss()
        {
            if (Changing) return;
            Changing = true;
            try
            {
                if (IsSplit && Eidolon != null && !State.Durability.Slain)
                {
                    if (Eidolon.State.IsDead) State.EidolonDied(Day);
                    else State.SetSplitWounds(Eidolon.Damage, Day);
                }
                EndRitual();
                ClearSplitEffectCopies();
                RemoveProjection();
                State.ForceDismiss();
                TwinActive = false;
                TwinPaidUntil = 0;
                if (Eidolon != null) { RemovePetPreparation(); Eidolon.IsInGame = false; }
                EvolutionHealing.Refresh(this);
            }
            finally { Changing = false; }
        }

        internal void BeginSplit()
        {
            // Snapshot the fused creature's external effects before replacing its
            // source-owned form/evolution facts. Those facts are supplied separately.
            var effects = Projection.Buffs(Owner).Where(b => !ProjectionFacts.Contains(b) &&
                !SynthesistBlueprints.IsOwnedBuff(b.Blueprint) &&
                b.Blueprint.GetComponent<Kingmaker.UnitLogic.Buffs.Polymorph>() == null).ToArray();
            State.BeginSplit(Now, Budget);
            RemoveProjection();
            ClearSplitEffectCopies();
            Eidolon.Damage = State.Durability.Wounds;
            Eidolon.Position = Owner.Position + new Vector3(1, 0, 0);
            Eidolon.IsInGame = true;
            // Both creatures retain the eidolon's evolutions, but not its physical base values.
            ApplyEvolutionFacts();
            Projection.ApplySplitStatistics(this);
            Projection.ApplySpecials(this);
            foreach (var buff in effects)
            {
                var previous = Projection.Buffs(Eidolon).ToArray();
                var duration = buff.IsPermanent ? (TimeSpan?)null : buff.TimeLeft;
                var copy = Eidolon.Buffs.AddBuff(buff.Blueprint, buff.Context, duration);
                if (copy != null && !previous.Contains(copy)) SplitEffectCopies.Add(copy);
            }
            EvolutionHealing.Refresh(this);
        }

        internal void EndSplit()
        {
            State.SetSplitWounds(Eidolon.Damage, Day);
            State.EndSplit(Day);
            FinishSplitTransition();
        }

        private void FinishSplitTransition()
        {
            ClearSplitEffectCopies();
            RemoveProjection();
            Eidolon.IsInGame = false;
            if (State.Mode == FusionMode.Fused) ApplyProjection(false);
            EvolutionHealing.Refresh(this);
        }

        private void ClearSplitEffectCopies()
        {
            foreach (var buff in SplitEffectCopies.ToArray()) buff?.Remove();
            SplitEffectCopies.Clear();
        }

        internal void BeginTwin()
        {
            if (State.Mode != FusionMode.Unfused || State.EvolutionEditsOpen || TwinActive || Owner.Body.IsPolymorphed)
                throw new InvalidOperationException("Twin Eidolon requires an unfused, untransformed Summoner.");
            State.SpendTwinMinute(Budget);
            TwinActive = true;
            TwinPaidUntil = Now + TimeSpan.TicksPerMinute;
            ApplyProjection(true);
        }

        internal void EndTwin()
        {
            if (!TwinActive) throw new InvalidOperationException("Twin Eidolon is not active.");
            Dismiss();
        }

        internal void Tick()
        {
#if SYNTHESIST_ACCEPTANCE
            using var timing = RuntimePerformance.Measure("UnitPart.Tick");
#endif
            if (State == null || Eidolon == null || Changing) return;
            if (!IsFused && !IsSplit && !IsPreparing && !TwinActive) ApplyCharacterBuild();
            if (Eidolon.Descriptor.Faction != Owner.Descriptor.Faction)
            {
                Eidolon.Descriptor.Faction = Owner.Descriptor.Faction;
                Eidolon.Stats.HitPoints.UpdateValue();
            }
            if (Eidolon.Descriptor.Alignment.ValueRaw != Owner.Descriptor.Alignment.ValueRaw)
                Eidolon.Descriptor.Alignment.Set(Owner.Descriptor.Alignment.ValueRaw, false);
            RemoveNpcDifficulty();
            // Native party restoration can reactivate pets after an area/save load. The
            // canonical record is only an independent unit in the two explicit modes.
            bool shouldBeActive = IsSplit || IsPreparing;
            if (Eidolon.IsInGame != shouldBeActive) Eidolon.IsInGame = shouldBeActive;
            ReconcileAspectControls();
            ReconcileSummonSpellbook();
            EvolutionHealing.Refresh(this);
            string signature = EvolutionStatistics.Signature(this);
            if (signature != evolutionStatisticsSignature)
            {
                EvolutionStatistics.Refresh(this);
                evolutionStatisticsSignature = signature;
            }
            if (respecSuspended)
            {
                if (Game.Instance.LevelUpController != null) { sawRespecController = true; return; }
                if (!sawRespecController) return;
                respecSuspended = false; sawRespecController = false;
                if (Eidolon.Master == null) Eidolon.SetMaster(Owner, Enums.PetTypeExtensions.Eidolon);
            }
            if (!IsFused && !IsSplit && !TwinActive && Level != State.SummonerLevel) SyncProgression();
            if (NeedsReconcile)
            {
                if (Owner.View == null) return;
                AppearanceIntegration.ApplyCanonical(this);
                Eidolon.Body.HandsAreEnabled = Projection.HasArms(this);
                Eidolon.Progression.RecalculateHP();
                ReconcileDurabilityMaximum();
                if (IsFused || TwinActive) { RemoveProjection(); ApplyProjection(TwinActive); }
                else if (IsSplit)
                {
                    RemoveProjection();
                    ApplyEvolutionFacts();
                    Projection.ApplySplitStatistics(this);
                    Projection.ApplySpecials(this);
                }
                Eidolon.IsInGame = IsSplit || IsPreparing;
                NeedsReconcile = false;
            }
            ReconcileDurabilityMaximum();
            if ((Owner.State.IsDead || Owner.State.IsUnconscious) && (IsFused || IsSplit || IsPreparing || TwinActive || IsRitualActive)) { Dismiss(); return; }
            if (IsRitualActive && Now >= RitualEndsAt)
            {
                EndRitual();
                EnterFusion();
            }
            if (IsPreparing && Owner.IsInCombat) { State.OnCombatStarted(); RemovePetPreparation(); Eidolon.IsInGame = false; }
            if (IsSplit)
            {
                if (Eidolon.State.IsDead) { State.EidolonDied(Day); FinishSplitTransition(); return; }
                State.SetSplitWounds(Eidolon.Damage, Day);
                if (State.AdvanceSplit(Now, Day, Budget)) FinishSplitTransition();
            }
            if (TwinActive && Now >= TwinPaidUntil)
            {
                if (!Budget.TrySpend(DailyAbility.TwinEidolon, 1)) { Dismiss(); return; }
                TwinPaidUntil += TimeSpan.TicksPerMinute;
            }
            if (CollapsePending) { CollapsePending = false; Dismiss(); }
        }

        private void ReconcileDurabilityMaximum()
        {
            int maximum = Eidolon.MaxHP;
            if (IsFused)
            {
                // Physical Constitution effects on the fused creature also change the
                // eidolon's HD-derived pool. Keep missing HP, rather than healing on a
                // buff cycle. The protagonist's own native HP recalculation is separate.
                maximum += EidolonProgression.At(Level).HitDice * (Owner.Stats.Constitution.Bonus - Eidolon.Stats.Constitution.Bonus);
            }
            maximum = Math.Max(1, maximum);
            if (maximum == State.Durability.Maximum) return;
            State.Durability.ChangeMaximum(maximum);
            if (IsFused && State.Durability.Remaining == 0)
            {
                State.EidolonDied(Day);
                Dismiss();
            }
            else if (IsFused) SetPool(State.Durability.Remaining);
        }

        private void RemoveNpcDifficulty()
        {
            // The visual template's Basic Feats progression can attach NPC difficulty
            // advancement. This is a party eidolon, so it must not inherit enemy HP/stat
            // adjustments (the normal preset applied -20 HP after loading the test).
            foreach (var fact in Eidolon.Progression.Features.Enumerable.Where(f => f.Blueprint.name == "DifficultOpponent").ToArray())
                Eidolon.RemoveFact(fact);
        }

        private void ReconcileSummonSpellbook()
        {
#if SYNTHESIST_ACCEPTANCE
            using var timing = RuntimePerformance.Measure("ReconcileSummonSpellbook");
#endif
            // The inherited AddSpellbook component caches its initial context rank.
            // Advance the existing native SLA book for this archetype only. Native
            // spellbook advancement supplies the original spell tiers and slot limits.
            var blueprint = BlueprintCore.Utils.BlueprintTool.Get<Kingmaker.Blueprints.Classes.Spells.BlueprintSpellbook>(
                SynthesistBlueprints.Id("SummonerSecondSpellbookSpellBook"));
            var book = Owner.DemandSpellbook(blueprint);
            while (book.RawBaseLevel < Level)
            {
                int previous = book.RawBaseLevel;
                book.AddBaseLevel();
                if (book.RawBaseLevel <= previous) throw new InvalidOperationException("Summon Monster spellbook did not advance.");
            }
        }

        private void ClearOwnedEvolutionEffects()
        {
            ClearSplitEffectCopies();
            PersonalEvolutionHealing?.Remove(); PersonalEvolutionHealing = null;
            PetEvolutionHealing?.Remove(); PetEvolutionHealing = null;
            foreach (var modifier in CanonicalStatModifiers.Concat(AspectStatModifiers).ToArray()) modifier.Remove();
            CanonicalStatModifiers.Clear(); AspectStatModifiers.Clear();
            evolutionStatisticsSignature = null;
        }

        internal void ApplyProjection(bool twin) => Projection.Apply(this, twin);
        internal void RemoveProjection() => Projection.Remove(this);
        internal void ApplyEvolutionFacts() => Projection.ApplyEvolutions(this);

        internal void SetPool(int amount)
        {
            TemporaryPool?.Remove();
            TemporaryPool = null;
            if (amount > 0 && IsFused)
            {
                var source = Projection.Buffs(Owner).First(b => SynthesistBlueprints.IsFusionBuff(b.Blueprint));
                TemporaryPool = Owner.Stats.TemporaryHitPoints.AddModifier(amount, source, ModifierDescriptor.UntypedStackable);
            }
        }
    }
}
