using System;
using System.Runtime.Serialization;
using Newtonsoft.Json;

namespace WOTR_MAKING_FRIENDS.Synthesist.Rules
{
    internal readonly struct FusionHit
    {
        public int TemporaryHpLost { get; }
        public int PersonalHpSacrificed { get; }
        public int OverflowDamage { get; }
        public bool EidolonSlain { get; }
        public int PersonalHpLost => checked(PersonalHpSacrificed + OverflowDamage);
        public FusionHit(int temporary, int sacrificed, int overflow, bool slain)
        { TemporaryHpLost = temporary; PersonalHpSacrificed = sacrificed; OverflowDamage = overflow; EidolonSlain = slain; }
    }

    // Engine-independent ledger. The damage adapter must call this only for damage assigned to
    // the eidolon pool, after difficulty/DR and other temporary-HP handling, exactly once per hit.
    // Personal HP, other temporary HP, and Constitution recalculation remain owned by the game.
    [DataContract]
    internal sealed class FusionDurability
    {
        [JsonConstructor] private FusionDurability() { }
        [DataMember] public int Maximum { get; private set; }
        [DataMember] public int Wounds { get; private set; }
        [DataMember] public bool Slain { get; private set; }
        [DataMember] public int ReturnDay { get; private set; }
        public int Remaining => Math.Max(0, Maximum - Wounds);

        public FusionDurability(int maximum)
        {
            if (maximum < 1) throw new ArgumentOutOfRangeException(nameof(maximum));
            Maximum = maximum;
        }

        public void Validate()
        {
            if (Maximum < 1 || Wounds < 0 || ReturnDay < 0 || (Slain && Remaining != 0))
                throw new SerializationException("Invalid Synthesist durability data.");
        }

        [OnDeserialized] private void OnDeserialized(StreamingContext context) => Validate();

        public bool CanReturn(int day) => !Slain || day >= ReturnDay;

        // Only call after the summoning ritual succeeds, never on load, rest, preview or dismissal.
        public void CompleteSummoning(int day)
        {
            if (day < 0) throw new ArgumentOutOfRangeException(nameof(day));
            if (!CanReturn(day)) throw new InvalidOperationException("The slain eidolon cannot return until the following day.");
            if (!Slain) return;
            Wounds = Maximum - Math.Max(1, Maximum / 2);
            Slain = false;
            ReturnDay = 0;
        }

        public FusionHit TakeHit(int damage, int personalHp, bool linkEnabled, int personalHpLimit, int day)
        {
            if (damage < 0) throw new ArgumentOutOfRangeException(nameof(damage));
            if (day < 0 || day == int.MaxValue) throw new ArgumentOutOfRangeException(nameof(day));
            if (Slain || Remaining == 0) throw new InvalidOperationException("No living eidolon pool is present.");
            int temporary = Remaining;
            int sacrifice = 0;
            if (damage >= temporary && linkEnabled && personalHp > 0)
            {
                // long arithmetic prevents an extreme overkill hit wrapping into a free rescue.
                long needed = (long)damage - temporary + 1;
                long available = Math.Max(0L, (long)personalHp - personalHpLimit);
                if (needed <= available) sacrifice = checked((int)needed);
            }
            int absorbed = Math.Min(temporary, damage - sacrifice);
            int overflow = damage - sacrifice - absorbed;
            Wounds = checked(Wounds + absorbed);
            if (Remaining == 0) MarkSlain(day);
            return new FusionHit(absorbed, sacrifice, overflow, Slain);
        }

        // Rejuvenate Eidolon, or ordinary healing of the independent Split Forms eidolon.
        // The caller enforces target/mode rules; ordinary cure spells on the fused protagonist
        // must never call this method.
        public int HealEidolon(int amount)
        {
            if (amount < 0) throw new ArgumentOutOfRangeException(nameof(amount));
            if (Slain) return 0;
            int restored = Math.Min(Wounds, amount);
            Wounds -= restored;
            return restored;
        }

        public void ChangeMaximum(int maximum)
        {
            if (maximum < 1) throw new ArgumentOutOfRangeException(nameof(maximum));
            Maximum = maximum;
            // Preserve missing HP: ending a Constitution bonus must not erase injuries.
            // A live-state adapter must resolve zero HP before permitting further actions.
            if (Slain) Wounds = maximum;
        }

        public void SetIndependentWounds(int missingHp)
        {
            if (missingHp < 0) throw new ArgumentOutOfRangeException(nameof(missingHp));
            if (Slain) throw new InvalidOperationException("A slain eidolon cannot have independent wounds updated.");
            // Normal independent eidolons can be alive below zero HP. Only the native death
            // result may call MarkSlain; never apply the fused zero-temp-HP rule here.
            Wounds = missingHp;
        }

        public void MarkSlain(int day)
        {
            if (day < 0 || day == int.MaxValue) throw new ArgumentOutOfRangeException(nameof(day));
            Wounds = Maximum;
            Slain = true;
            ReturnDay = checked(day + 1);
        }
    }
}
