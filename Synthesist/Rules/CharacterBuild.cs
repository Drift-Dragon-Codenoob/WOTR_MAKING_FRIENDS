using System;
using System.Collections.Generic;
using System.Linq;

namespace WOTR_MAKING_FRIENDS.Synthesist.Rules
{
    internal sealed class EvolutionPurchase
    {
        public string Key;
        public string Option;
        public bool Aspect;
        public int Cost;
        public EvolutionPurchase Copy() => (EvolutionPurchase)MemberwiseClone();
    }

    internal sealed class CharacterBuild
    {
        public int Version = 1;
        public int Level;
        public EidolonForm Form;
        public string Appearance;
        public bool Confirmed;
        public List<EvolutionPurchase> Purchases = new();
        public int AspectSpent => Purchases.Where(p => p.Aspect).Sum(p => p.Cost);
        public int EidolonSpent => Purchases.Where(p => !p.Aspect).Sum(p => p.Cost);
        public int Remaining => EidolonProgression.At(Level).EvolutionPoints - EidolonSpent - AspectBudget.Diverted(Level, AspectSpent);
        public int AspectRemaining => AspectBudget.Allowance(Level) - AspectSpent;
        public int PurchasedRank(string key, bool aspect) => Purchases.Count(p => p.Key == key && p.Aspect == aspect);
        public int Rank(string key, bool aspect)
            => PurchasedRank(key, aspect) + (aspect ? 0 : BaseForms.For(Form).FreeEvolutions.Where(e => "Evolution" + (e.Key == "ReachBite" ? "Reach" : e.Key) == key).Sum(e => e.Ranks));
        public bool HasArms(bool aspect = false) => aspect || Rank("EvolutionLimbsArms", false) > 0;
        public bool CanUseAppearance(EidolonForm form, bool supportsWeapons) => Form == form && (!HasArms() || supportsWeapons);
        public CharacterBuild Copy() => new()
        {
            Version = Version, Level = Level, Form = Form, Appearance = Appearance,
            Confirmed = Confirmed, Purchases = Purchases.Select(p => p.Copy()).ToList()
        };
        public static CharacterBuild Begin(int level, EidolonForm form, CharacterBuild previous, bool keep)
        {
            EidolonProgression.At(level);
            if (keep && previous != null && (previous.Level > level || previous.Form != form))
                throw new InvalidOperationException("Rebuild evolutions after changing base form or reducing Summoner level.");
            var result = keep && previous != null ? previous.Copy() : new CharacterBuild();
            result.Level = level; result.Form = form; result.Confirmed = false;
            result.Appearance = previous?.Appearance;
            return result;
        }
        public int Cost(string key, int ordinaryCost, bool aspect)
        {
            int rank = PurchasedRank(key, aspect);
            if (key == "EvolutionLarge") return rank == 0 ? 4 : 6;
            if (key is "EvolutionFastHealing" or "EvolutionDamageReduction" && rank > 0) return 2;
            if (key is "EvolutionAbilityIncreaseStrength" or "EvolutionAbilityIncreaseConstitution" && Rank("EvolutionLarge", aspect) > 0) return 4;
            return ordinaryCost;
        }
        public void Buy(string key, string option, int cost, bool aspect)
        {
            if (Confirmed || cost < 1) throw new InvalidOperationException("The evolution build is closed or the cost is invalid.");
            if (aspect)
            {
                if (AspectSpent + cost > AspectBudget.Allowance(Level) ||
                    Remaining < AspectBudget.AdditionalDiversion(Level, AspectSpent, cost))
                    throw new InvalidOperationException("Insufficient Aspect allowance or eidolon points.");
            }
            else if (cost > Remaining) throw new InvalidOperationException("Insufficient eidolon evolution points.");
            Purchases.Add(new EvolutionPurchase { Key = key, Option = option, Cost = cost, Aspect = aspect });
        }
        public int NaturalAttacks(bool aspect)
        {
            int primary = Rank("EvolutionClaws", aspect) > 0 || Rank("EvolutionPincers", aspect) > 0 ? 2 : Rank("EvolutionSlam", aspect) > 0 ? 1 : 0;
            return primary + Rank("EvolutionBite", aspect) + Rank("EvolutionGore", aspect) + Rank("EvolutionTailSlap", aspect) +
                Rank("EvolutionTentacle", aspect) + 2 * Rank("EvolutionWingBuffet", aspect) +
                2 * (Rank("EvolutionClawsLimbs", aspect) + Rank("EvolutionPincersLimbs", aspect)) + Rank("EvolutionSlamLimbs", aspect);
        }
        public void Validate()
        {
            if (Version != 1 || !Enum.IsDefined(typeof(EidolonForm), Form) || Purchases == null ||
                Purchases.Any(p => p == null || string.IsNullOrEmpty(p.Key) || p.Cost < 1) ||
                AspectSpent > AspectBudget.Allowance(Level) || Remaining < 0)
                throw new InvalidOperationException("Invalid saved eidolon build.");
            EidolonProgression.At(Level);
        }
    }

    internal sealed class EvolutionDefinition
    {
        public string Key;
        public int Cost;
        public int Maximum;
        public int Start = 1;
        public int Interval;
        public string[] ForbiddenFacts = Array.Empty<string>();
    }

    internal static class CharacterBuildRules
    {
        public static string Reason(CharacterBuild build, EvolutionDefinition definition, string option, bool aspect, string alignment)
        {
            if (build == null || build.Confirmed) return "Start an eidolon build first.";
            string key = definition.Key;
            int rank = build.PurchasedRank(key, aspect);
            if (aspect && key.StartsWith("EvolutionAbilityIncrease", StringComparison.Ordinal)) return "Aspect cannot grant ability increases.";
            int required = ManagedEvolutionRules.OwnsRank(key) && rank < ManagedEvolutionRules.MaximumRanks(key)
                ? ManagedEvolutionRules.RequiredLevel(key, rank) : Math.Max(1, definition.Start + rank * definition.Interval);
            if (rank >= definition.Maximum) return "Maximum purchases reached.";
            if (build.Level < required) return "Requires Summoner level " + required + ".";
            if (!ManagedEvolutionRules.OwnsRank(key) && definition.ForbiddenFacts.Any(f => FactRank(build, f, aspect) > 0))
                return "Already supplied by this build or incompatible with an existing evolution.";
            EidolonForm form = aspect ? EidolonForm.Biped : build.Form;
            if (key == "EvolutionPounce" && form != EidolonForm.Quadruped) return "Requires the quadruped base form.";
            if (key == "EvolutionTrample" && form == EidolonForm.Serpentine) return "Requires a biped or quadruped.";
            if (key == "EvolutionWeb") return "Requires climb, which is unavailable in this version.";
            if (key is "EvolutionPincers" or "EvolutionSlam" && !build.HasArms(aspect)) return "Requires arms.";
            if (key == "EvolutionTailSlap" && build.Rank("EvolutionTail", aspect) == 0) return "Requires a tail.";
            if (key == "EvolutionWingBuffet" && build.Rank("EvolutionFlight", aspect) == 0) return "Requires flight with wings.";
            if (key == "EvolutionTrip" && build.Rank("EvolutionBite", aspect) == 0) return "Requires a bite.";
            if (key == "EvolutionRend" && build.Rank("EvolutionClaws", aspect) + build.Rank("EvolutionClawsLimbs", aspect) == 0) return "Requires claws.";
            if (key.EndsWith("Limbs", StringComparison.Ordinal) && key != "EvolutionLimbs")
            {
                int allocated = build.Rank("EvolutionClawsLimbs", aspect) + build.Rank("EvolutionPincersLimbs", aspect) + build.Rank("EvolutionSlamLimbs", aspect);
                int arms = build.Rank("EvolutionLimbsArms", aspect) + (aspect ? 1 : 0);
                int primaryArms = build.Rank("EvolutionClaws", aspect) + build.Rank("EvolutionPincers", aspect) + build.Rank("EvolutionSlam", aspect) > 0 ? 1 : 0;
                if (arms <= allocated + primaryArms) return "Requires an unused additional pair of arms.";
            }
            if (key == "EvolutionDamageReduction")
            {
                if (rank > 0 && build.Purchases.First(p => p.Key == key && p.Aspect == aspect).Option != option)
                    return "Upgrade the existing damage-reduction alignment.";
                if (rank == 0 && !DamageReductionRules.OpposesAlignment(option, alignment.Contains("Good"), alignment.Contains("Evil"), alignment.Contains("Lawful"), alignment.Contains("Chaotic")))
                    return "Choose an alignment opposite your own.";
            }
            try
            {
                var candidate = build.Copy();
                candidate.Buy(key, option, candidate.Cost(key, definition.Cost, aspect), aspect);
                if (candidate.NaturalAttacks(aspect) > EidolonProgression.At(build.Level).MaximumNaturalAttacks)
                    return "Exceeds the eidolon's natural-attack limit.";
            }
            catch (InvalidOperationException error) { return error.Message; }
            return null;
        }

        private static int FactRank(CharacterBuild build, string name, bool aspect)
        {
            EidolonForm form = aspect ? EidolonForm.Biped : build.Form;
            if (name == "Eidolon" + form + "BaseFormFeature") return 1;
            if (name == "EvolutionSizeChangeLargeFeature") return build.Rank("EvolutionLarge", aspect) == 1 ? 1 : 0;
            if (name == "EvolutionSizeChangeHugeFeature") return build.Rank("EvolutionLarge", aspect) == 2 ? 1 : 0;
            bool free = name.EndsWith("BaseFeature", StringComparison.Ordinal);
            string key = name.Replace("BaseFeature", "").Replace("Feature", "");
            return free ? build.Rank(key, aspect) - build.PurchasedRank(key, aspect) : build.PurchasedRank(key, aspect);
        }
    }
}
