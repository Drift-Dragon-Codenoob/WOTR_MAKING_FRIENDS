using System;
using System.Runtime.Serialization;
using Newtonsoft.Json;

namespace WOTR_MAKING_FRIENDS.Synthesist.Rules
{
    internal enum FusionMode { Unfused, Preparation, Fused, Split }

    // Persistent rules state, not an engine UnitPart. No unit spawning, view changes or HP
    // setters belong here. A runtime transaction must apply engine changes before committing
    // this state; a failed engine transaction must restore its previous serialised state.
    [DataContract]
    internal sealed class FusionState
    {
        [JsonConstructor] private FusionState() { Version = 0; }
        public const int CurrentVersion = 2;
        public const int RitualSeconds = 60;
        public const long RoundTicks = TimeSpan.TicksPerSecond * 6;

        [DataMember] public int Version { get; private set; } = CurrentVersion;
        [DataMember] public string EidolonEntityId { get; private set; }
        [DataMember] public EidolonForm Form { get; private set; }
        [DataMember] public FusionMode Mode { get; private set; }
        [DataMember] public int SummonerLevel { get; private set; }
        [DataMember] public FusionDurability Durability { get; private set; }
        [DataMember] public bool LinkEnabled { get; set; }
        [DataMember] public int PersonalHpLimit { get; set; } = 1;
        [DataMember] public long SplitPaidUntil { get; private set; }
        [DataMember] public bool EvolutionEditsOpen { get; private set; } = true;

        public FusionState(string eidolonEntityId, EidolonForm form, int level, int eidolonHp)
        {
            if (string.IsNullOrWhiteSpace(eidolonEntityId)) throw new ArgumentException("A canonical eidolon is required.", nameof(eidolonEntityId));
            if (!Enum.IsDefined(typeof(EidolonForm), form)) throw new ArgumentOutOfRangeException(nameof(form));
            EidolonProgression.At(level);
            EidolonEntityId = eidolonEntityId; Form = form; SummonerLevel = level;
            Durability = new FusionDurability(eidolonHp);
        }

        public void Validate()
        {
            if (Version != CurrentVersion || string.IsNullOrWhiteSpace(EidolonEntityId) ||
                !Enum.IsDefined(typeof(FusionMode), Mode) || !Enum.IsDefined(typeof(EidolonForm), Form) ||
                SummonerLevel < 1 || SummonerLevel > 20 || Durability == null || SplitPaidUntil < 0 ||
                ((Mode == FusionMode.Fused || Mode == FusionMode.Split) && (Durability.Slain || EvolutionEditsOpen)) ||
                (Mode == FusionMode.Fused && Durability.Remaining == 0) ||
                (Mode == FusionMode.Split && (SummonerLevel < 16 || SplitPaidUntil == 0)) ||
                (Mode != FusionMode.Split && SplitPaidUntil != 0))
                throw new SerializationException("Invalid or unsupported Synthesist state. Do not apply fusion modifiers.");
            Durability.Validate();
        }

        [OnDeserialized] private void OnDeserialized(StreamingContext context) => Validate();

        public void EnterPreparation(bool inCombat)
        {
            Require(Mode == FusionMode.Unfused && !inCombat, "Preparation requires an unfused character outside combat.");
            Mode = FusionMode.Preparation;
        }

        public void LeavePreparation()
        {
            Require(Mode == FusionMode.Preparation, "Not in preparation mode.");
            Mode = FusionMode.Unfused;
        }

        public void OnCombatStarted()
        {
            if (Mode == FusionMode.Preparation) Mode = FusionMode.Unfused;
        }

        public void ConfirmEvolutions()
        {
            Require(Mode == FusionMode.Preparation, "Confirm the eidolon build during preparation.");
            EvolutionEditsOpen = false;
        }

        public void OpenEvolutionRetraining()
        {
            Require(Mode == FusionMode.Preparation, "Retraining requires preparation mode.");
            EvolutionEditsOpen = true;
        }

        public void SetLevel(int level)
        {
            Require(Mode == FusionMode.Unfused || Mode == FusionMode.Preparation, "Dismiss fusion before level-up or respec.");
            EidolonProgression.At(level);
            if (level != SummonerLevel) EvolutionEditsOpen = true;
            SummonerLevel = level;
            // Daily expenditure persists across level-up and respec; decreasing a level does
            // not refund resources. A full respec removing the archetype removes this state.
        }

        internal void AdoptDurability(FusionDurability durability)
        {
            Require(Mode == FusionMode.Unfused, "Restore respec durability before applying a form.");
            durability.Validate();
            Durability = durability;
        }

        public void CompleteFusionRitual(int day, int summonerSize, int eidolonSize)
        {
            Require(Mode == FusionMode.Unfused && !EvolutionEditsOpen, "Finish preparation before summoning.");
            Require(eidolonSize >= summonerSize, "The eidolon must be at least as large as its Summoner.");
            Require(Durability.Slain || Durability.Remaining > 0, "The eidolon has no remaining HP.");
            Durability.CompleteSummoning(day);
            Mode = FusionMode.Fused;
        }

        public void Dismiss()
        {
            Require(Mode == FusionMode.Fused, "Voluntary fusion dismissal requires a fused character.");
            Mode = FusionMode.Unfused;
        }

        public FusionHit TakeFusedHit(int damage, int personalHp, int day)
        {
            Require(Mode == FusionMode.Fused, "Fused Link only applies while fused.");
            var result = Durability.TakeHit(damage, personalHp, LinkEnabled, PersonalHpLimit, day);
            if (result.EidolonSlain) Mode = FusionMode.Unfused;
            return result;
        }

        public int Rejuvenate(int amount)
        {
            Require(Mode == FusionMode.Fused || Mode == FusionMode.Split, "Rejuvenate Eidolon requires a present eidolon.");
            return Durability.HealEidolon(amount);
        }

        public void UseMakersJump(IAbilityBudget budget)
        {
            Require(Mode == FusionMode.Fused && SummonerLevel >= 6, "Maker's Jump is unavailable.");
            Require(budget.TrySpend(DailyAbility.MakersJump, 1), "Maker's Jump is exhausted.");
        }

        public void BeginSplit(long gameTimeTicks, IAbilityBudget budget)
        {
            Require(Mode == FusionMode.Fused && SummonerLevel >= 16,
                "Split Forms is unavailable.");
            if (gameTimeTicks < 0 || gameTimeTicks > long.MaxValue - RoundTicks)
                throw new ArgumentOutOfRangeException(nameof(gameTimeTicks));
            // Charge immediately. Repeated short splits cannot create free rounds.
            Require(budget.TrySpend(DailyAbility.SplitForms, 1), "Split Forms rounds are exhausted.");
            SplitPaidUntil = gameTimeTicks + RoundTicks;
            Mode = FusionMode.Split;
        }

        // True means the split expired and the runtime must recombine the units.
        // Use the game's unpaused clock, not wall time or frame count.
        public bool AdvanceSplit(long gameTimeTicks, int day, IAbilityBudget budget)
        {
            if (gameTimeTicks < 0) throw new ArgumentOutOfRangeException(nameof(gameTimeTicks));
            if (day < 0 || day == int.MaxValue) throw new ArgumentOutOfRangeException(nameof(day));
            if (Mode != FusionMode.Split) return false;
            while (gameTimeTicks >= SplitPaidUntil)
            {
                if (!budget.TrySpend(DailyAbility.SplitForms, 1))
                {
                    EndSplit(day);
                    return true;
                }
                SplitPaidUntil = checked(SplitPaidUntil + RoundTicks);
            }
            return false;
        }

        public void EndSplit(int day)
        {
            Require(Mode == FusionMode.Split && !Durability.Slain, "No living split eidolon can rejoin.");
            if (day < 0 || day == int.MaxValue) throw new ArgumentOutOfRangeException(nameof(day));
            // David's explicit ruling: zero/negative independent HP becomes a depleted fused
            // pool on rejoining, so apply the slain-eidolon return rule. No personal-HP rescue.
            if (Durability.Remaining == 0) Durability.MarkSlain(day);
            Mode = Durability.Slain ? FusionMode.Unfused : FusionMode.Fused;
            SplitPaidUntil = 0;
        }

        // The engine owns independent eidolon HP during Split Forms. Synchronise it before
        // rejoining; never copy the Summoner's HP into this pool or vice versa.
        public void SetSplitWounds(int missingHp, int day)
        {
            Require(Mode == FusionMode.Split, "Independent HP only apply during Split Forms.");
            if (missingHp < 0 || day < 0 || day == int.MaxValue) throw new ArgumentOutOfRangeException();
            Durability.SetIndependentWounds(missingHp);
        }

        public void EidolonDied(int day)
        {
            Require(Mode == FusionMode.Split, "Independent eidolon death requires Split Forms.");
            Durability.MarkSlain(day);
            SplitPaidUntil = 0;
            Mode = FusionMode.Unfused;
        }

        // Includes sleep/unconsciousness and banishment. These dismiss a surviving eidolon;
        // they do not invent a slain-eidolon lockout or restore its injuries.
        public void ForceDismiss()
        {
            Mode = FusionMode.Unfused;
            SplitPaidUntil = 0;
        }

        public void SpendTwinMinute(IAbilityBudget budget)
        {
            Require(SummonerLevel == 20, "Twin Eidolon requires level 20.");
            // The tabletop resource is spent in one-minute increments, even for a brief use.
            Require(budget.TrySpend(DailyAbility.TwinEidolon, 1), "Twin Eidolon time is exhausted.");
        }

        public void OnCompletedRest()
        {
            // Sleep dismisses the eidolon before resources reset. It never heals the pool or
            // overrides ReturnDay, and cannot leave an active split with a fresh round budget.
            ForceDismiss();
        }

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
    }
}
