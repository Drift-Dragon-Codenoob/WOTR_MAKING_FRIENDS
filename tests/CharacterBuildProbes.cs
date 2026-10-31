using Newtonsoft.Json;
using WOTR_MAKING_FRIENDS.Synthesist.Rules;

internal static class CharacterBuildProbes
{
    internal static void Run()
    {
        int checks = 0;
        void Check(bool value, string message) { checks++; if (!value) throw new Exception("Character build: " + message); }
        void Reject(Action action, string message)
        {
            bool rejected = false;
            try { action(); } catch (InvalidOperationException) { rejected = true; }
            Check(rejected, message);
        }
        EvolutionDefinition Definition(string key, int cost, int maximum = 1, int start = 1) => new()
        { Key = key, Cost = cost, Maximum = maximum, Start = start };
        var armour = Definition("EvolutionNaturalArmor", 1, 5);
        var strength = Definition("EvolutionAbilityIncreaseStrength", 2, 4);
        var growth = Definition("EvolutionLarge", 4, 2);
        var plan = CharacterBuild.Begin(1, EidolonForm.Biped, null, false);
        Check(plan.Remaining == 3 && plan.Purchases.Count == 0, "First-level points begin unspent");
        Check(plan.Rank("EvolutionClaws", false) == 1 && plan.NaturalAttacks(false) == 2 && plan.HasArms(), "Free biped evolutions do not consume points");
        Check(CharacterBuildRules.Reason(plan, armour, "", false, "NeutralGood") == null, "First natural armour purchase is legal");
        plan.Buy(armour.Key, "", 1, false);
        Check(CharacterBuildRules.Reason(plan, armour, "", false, "NeutralGood") != null, "Natural armour cannot be purchased twice at level one");
        plan.Buy(strength.Key, "", 2, false);
        Check(plan.Remaining == 0, "Mixed one- and two-point purchases consume three points exactly");
        Reject(() => plan.Buy("EvolutionScent", "", 1, false), "Overspending is rejected");
        plan.Appearance = "Succubus"; plan.Confirmed = true;
        Reject(() => plan.Buy("EvolutionScent", "", 1, false), "Closed builds reject ordinary purchases");
        var preview = CharacterBuild.Begin(2, EidolonForm.Biped, plan, true);
        Check(preview.Remaining == 1 && preview.Purchases.Count == 2 && !preview.Confirmed, "Level-up keeps purchases and exposes the additional allowance");
        preview.Purchases[0].Cost = 99;
        Check(plan.Purchases[0].Cost == 1 && plan.Level == 1 && plan.Confirmed, "Cancelled preview changes cannot mutate the previous confirmed build");
        var rebuilt = CharacterBuild.Begin(2, EidolonForm.Biped, plan, false);
        Check(rebuilt.Purchases.Count == 0 && rebuilt.Remaining == 4, "Reallocation uses the full current allowance");
        var saved = JsonConvert.DeserializeObject<CharacterBuild>(JsonConvert.SerializeObject(plan))!;
        saved.Validate();
        Check(saved.Appearance == "Succubus" && saved.Confirmed && saved.Remaining == 0 && saved.Purchases[1].Key == strength.Key, "Save round-trip retains appearance, confirmation, purchases and paid costs");
        Reject(() => CharacterBuild.Begin(1, EidolonForm.Quadruped, plan, true), "Keep cannot carry a build onto a different base form");
        Reject(() => CharacterBuild.Begin(1, EidolonForm.Biped, preview, true), "Keep cannot retain a higher-level build after level reduction");
        for (int level = 1; level <= 20; level++)
        {
            foreach (var form in Enum.GetValues<EidolonForm>())
            {
                var fresh = CharacterBuild.Begin(level, form, null, false);
                fresh.Validate();
                Check(fresh.Remaining == EidolonProgression.At(level).EvolutionPoints, "Full allowance at " + level + " / " + form);
                Check(fresh.AspectRemaining == AspectBudget.Allowance(level), "Separate Aspect allowance at " + level + " / " + form);
            }
        }
        var grown = CharacterBuild.Begin(13, EidolonForm.Biped, null, false);
        grown.Buy(strength.Key, "", grown.Cost(strength.Key, 2, false), false);
        grown.Buy(growth.Key, "", grown.Cost(growth.Key, 4, false), false);
        Check(grown.Cost("EvolutionAbilityIncreaseConstitution", 2, false) == 4, "Large doubles subsequent Constitution purchases");
        grown.Buy("EvolutionAbilityIncreaseConstitution", "", 4, false);
        Check(grown.Cost(growth.Key, 4, false) == 6, "Huge is an additional six-point purchase");
        grown.Buy(growth.Key, "", 6, false);
        Check(grown.Remaining == 1 && grown.Purchases[0].Cost == 2, "Growth does not retroactively reprice earlier ability purchases");
        Check(CharacterBuildRules.Reason(grown, growth, "", false, "LawfulGood") != null, "A third Growth purchase is rejected");
        var low = CharacterBuild.Begin(7, EidolonForm.Biped, null, false);
        Check(CharacterBuildRules.Reason(low, growth, "", false, "LawfulGood") != null, "Large requires Summoner eight rather than eidolon HD");
        var aspect = CharacterBuild.Begin(18, EidolonForm.Quadruped, null, false);
        aspect.Buy("EvolutionNaturalArmor", "", 1, true);
        int afterOne = aspect.Remaining;
        aspect.Buy("EvolutionScent", "", 1, true);
        Check(aspect.Remaining == afterOne && aspect.AspectRemaining == 4, "Greater Aspect rounds aggregate diversion rather than each purchase");
        Check(CharacterBuildRules.Reason(aspect, strength, "", true, "LawfulGood") != null, "Aspect cannot purchase ability increases");
        Reject(() => aspect.Buy("EvolutionFlight", "", 5, true), "Aspect cannot exceed its separate allowance");
        Check(aspect.CanUseAppearance(EidolonForm.Quadruped, false) && !aspect.CanUseAppearance(EidolonForm.Biped, true), "Appearance is filtered by mechanical form");
        aspect.Buy("EvolutionLimbsArms", "", 2, false);
        Check(!aspect.CanUseAppearance(EidolonForm.Quadruped, false) && aspect.CanUseAppearance(EidolonForm.Quadruped, true), "Purchased arms require a compatible appearance without changing base form");
        var attacks = CharacterBuild.Begin(4, EidolonForm.Biped, null, false);
        attacks.Buy("EvolutionPincers", "", 1, false);
        Check(attacks.NaturalAttacks(false) == 2, "Switchable primary sets are counted once");
        attacks.Buy("EvolutionBite", "", 1, false); attacks.Buy("EvolutionGore", "", 2, false);
        Check(attacks.NaturalAttacks(false) == 4, "Simultaneous additional natural attacks count towards the limit");
        Check(CharacterBuildRules.Reason(attacks, Definition("EvolutionTentacle", 1), "", false, "NeutralGood") != null, "A purchase cannot exceed the current natural-attack limit");
        var quad = CharacterBuild.Begin(1, EidolonForm.Quadruped, null, false);
        Check(CharacterBuildRules.Reason(quad, Definition("EvolutionPounce", 1), "", false, "NeutralGood") == null, "Quadrupeds can choose original one-point Pounce");
        Check(CharacterBuildRules.Reason(plan.Copy(), Definition("EvolutionPounce", 1), "", false, "NeutralGood") != null, "Bipeds cannot choose quadruped-only Pounce");
        Check(CharacterBuildRules.Reason(quad, Definition("EvolutionWeb", 3), "", false, "NeutralGood") != null, "Unavailable climb cannot be bypassed through Web");
        var dr = CharacterBuild.Begin(12, EidolonForm.Biped, null, false);
        var drDefinition = Definition("EvolutionDamageReduction", 3, 2);
        Check(CharacterBuildRules.Reason(dr, drDefinition, "Evil", false, "LawfulGood") == null, "DR permits an opposing alignment");
        Check(CharacterBuildRules.Reason(dr, drDefinition, "Good", false, "LawfulGood") != null, "DR rejects a matching alignment");
        dr.Buy(drDefinition.Key, "Evil", 3, false);
        Check(dr.Cost(drDefinition.Key, 3, false) == 2 && CharacterBuildRules.Reason(dr, drDefinition, "Chaotic", false, "LawfulGood") != null, "DR upgrades cost two and retain the chosen alignment");
        var state = new FusionState("build-probe", EidolonForm.Biped, 1, 6);
        state.CloseCharacterBuild();
        Check(!state.EvolutionEditsOpen && state.Mode == FusionMode.Unfused, "Native build confirmation closes runtime allocation without spawning an independent unit");
        state.CompleteFusionRitual(1, 1, 1);
        Reject(() => state.CloseCharacterBuild(), "A character build cannot be applied during fusion");
        Console.WriteLine($"PASS: {checks} character-build rules assertions. Native UI, models and lifecycle are not exercised.");
    }
}
