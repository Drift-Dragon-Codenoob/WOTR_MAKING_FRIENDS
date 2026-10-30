using System;

namespace WOTR_MAKING_FRIENDS.Synthesist.Rules
{
    internal static class ManagedEvolutionRules
    {
        internal static bool OwnsRank(string key) => key is "EvolutionLarge" or "EvolutionNaturalArmor" or "EvolutionFastHealing" ||
            key.StartsWith("EvolutionAbilityIncrease", StringComparison.Ordinal);
        internal static int MaximumRanks(string key) => key is "EvolutionNaturalArmor" or "EvolutionFastHealing" ? 5 : key == "EvolutionLarge" ? 2 : 4;
        // rank is the number already purchased, before the proposed purchase.
        internal static int RequiredLevel(string key, int rank)
        {
            if (rank < 0 || rank >= MaximumRanks(key)) throw new ArgumentOutOfRangeException(nameof(rank));
            if (key == "EvolutionNaturalArmor") return rank == 0 ? 1 : rank * 5;
            if (key == "EvolutionLarge") return rank == 0 ? 8 : 13;
            if (key == "EvolutionFastHealing") return 11;
            if (key.StartsWith("EvolutionAbilityIncrease", StringComparison.Ordinal)) return rank == 0 ? 1 : rank * 6;
            throw new ArgumentException("Not an independently managed evolution.", nameof(key));
        }
    }
}
