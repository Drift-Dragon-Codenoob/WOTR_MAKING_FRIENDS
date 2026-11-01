using System;
using System.Collections.Generic;

namespace WOTR_MAKING_FRIENDS.Synthesist.Rules
{
    internal static class SummonerFeatRules
    {
        internal static int ExtraEvolutionLimit(int characterLevel) => characterLevel < 1 ? 0 : Math.Min(5, 1 + characterLevel / 5);
        internal static int ArcanaMaximum(int highestSpellLevel, bool lower) => highestSpellLevel - (lower ? 1 : 0);
        internal static bool RendingQualification(int strength, int bab, int claws) => strength >= 13 && bab >= 6 && claws >= 2;
    }

    // Remains on the protagonist's feat, not on a fusion buff. Changing form cannot reset it.
    internal sealed class RendingClawsRound
    {
        public Dictionary<string, int> Hits = new(StringComparer.Ordinal);
        public bool Spent;
        public string TurnKey;
        public void Reset() { Hits.Clear(); Spent = false; TurnKey = null; }
        public void BeginTurn(string key)
        {
            if (TurnKey == key) return;
            Hits.Clear(); TurnKey = key;
        }
        public bool Hit(string target, bool qualifyingClawHit)
        {
            if (!qualifyingClawHit || string.IsNullOrEmpty(target) || Spent) return false;
            Hits.TryGetValue(target, out int previous);
            Hits[target] = previous + 1;
            if (previous == 0) return false;
            Spent = true;
            return true;
        }
    }
}
