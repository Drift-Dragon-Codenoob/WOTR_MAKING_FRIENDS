using System;

namespace WOTR_MAKING_FRIENDS.Synthesist.Rules
{
    internal static class AspectBudget
    {
        internal static int Allowance(int level) => level >= 18 ? 6 : level >= 10 ? 2 : 0;
        internal static int Diverted(int level, int personalPoints)
        {
            if (personalPoints < 0) throw new ArgumentOutOfRangeException(nameof(personalPoints));
            return level >= 18 ? personalPoints / 2 + personalPoints % 2 : personalPoints;
        }
        internal static int AdditionalDiversion(int level, int spent, int cost)
        {
            if (spent < 0 || cost < 1 || spent > Allowance(level) - cost)
                throw new ArgumentOutOfRangeException(nameof(cost));
            return Diverted(level, spent + cost) - Diverted(level, spent);
        }
    }
}
