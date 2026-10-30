using System;

namespace WOTR_MAKING_FRIENDS.Utilities
{
    // Pure calculations shared by the engine adapters and regression checks.
    internal static class BaselineRules
    {
        internal static int NextRankLevel(int startLevel, int interval, int currentRank)
            => Math.Max(1, startLevel + interval * currentRank);

        internal static int ProtectionDeficit(int maximumHp, int proposedDamage)
            => Math.Max(0, proposedDamage - maximumHp + 1);

        internal static int[] AllocateProtection(int deficit, int[] capacities)
        {
            var result = new int[capacities.Length];
            long total = 0;
            foreach (int capacity in capacities)
                total += Math.Max(0, capacity);
            if (deficit <= 0 || total < deficit)
                return result;
            for (int i = 0; i < capacities.Length && deficit > 0; i++)
            {
                result[i] = Math.Min(deficit, Math.Max(0, capacities[i]));
                deficit -= result[i];
            }
            return result;
        }

        internal static int GrowthReachBonus(int rank, bool biped)
            => rank < 1 ? 0 : biped ? 5 * Math.Min(2, rank) : rank >= 2 ? 5 : 0;

    }
}
