using WOTR_MAKING_FRIENDS.Synthesist.Rules;

internal static class ReviewRegressionProbes
{
    internal static void Run()
    {
        int checks = 0;
        void Check(bool value, string message) { checks++; if (!value) throw new Exception("Review regression: " + message); }
        void Reject(Action action, string message)
        {
            bool rejected = false;
            try { action(); } catch (InvalidOperationException) { rejected = true; }
            Check(rejected, message);
        }
        FusionState Split()
        {
            var state = new FusionState("review-eidolon", EidolonForm.Biped, 16, 80);
            state.EnterPreparation(false); state.ConfirmEvolutions(); state.LeavePreparation();
            state.CompleteFusionRitual(1,1,1); state.BeginSplit(100,new Budget());
            return state;
        }
        var killed = Split();
        killed.SetSplitWounds(5,1); killed.EndSplit(1,true);
        Check(killed.Mode == FusionMode.Unfused && killed.Durability.Slain, "A death effect with positive HP cannot rejoin into a living fused shell");
        Check(killed.Durability.Remaining == 0 && !killed.Durability.CanReturn(1) && killed.Durability.CanReturn(2), "Native independent death keeps the next-day lockout");
        var alive = Split(); alive.SetSplitWounds(5,1); alive.EndSplit(1);
        Check(alive.Mode == FusionMode.Fused && alive.Durability.Remaining == 75 && !alive.Durability.Slain, "The positive-HP living rejoin path preserves injuries");

        var old = CharacterBuild.Begin(1,EidolonForm.Biped,null,false,1);
        old.Buy("EvolutionScent","",1,false); old.Buy("EvolutionNaturalArmor","",1,false); old.Buy("EvolutionAbilityIncreaseStrength","",2,false);
        old.Confirmed = true;
        Reject(() => CharacterBuild.Begin(1,EidolonForm.Biped,old,true,0), "Keeping purchases is rejected when removing Extra Evolution would overspend");
        Check(old.BonusEvolutionPoints == 1 && old.Remaining == 0 && old.Purchases.Count == 3 && old.Confirmed, "A rejected keep operation leaves the old build intact");
        var rebuilt = CharacterBuild.Begin(1,EidolonForm.Biped,old,false,0);
        Check(rebuilt.Remaining == 3 && rebuilt.Purchases.Count == 0, "Rebuilding remains available after the feat is removed");
        var definitions = new Dictionary<string,EvolutionDefinition>
        {
            ["EvolutionScent"] = new() {Key="EvolutionScent",Cost=1,Maximum=1},
            ["EvolutionNaturalArmor"] = new() {Key="EvolutionNaturalArmor",Cost=1,Maximum=5},
            ["EvolutionAbilityIncreaseStrength"] = new() {Key="EvolutionAbilityIncreaseStrength",Cost=2,Maximum=4}
        };
        CharacterBuildRules.ValidateSavedPurchases(old,definitions);
        Check(true,"Existing valid purchase history passes preflight");
        var unknown = old.Copy(); unknown.Purchases[0].Key = "EvolutionDoesNotExist";
        Reject(() => CharacterBuildRules.ValidateSavedPurchases(unknown,definitions), "Unknown purchased blueprints fail before native mutation");
        var repeated = CharacterBuild.Begin(10,EidolonForm.Biped,null,false);
        repeated.Buy("EvolutionScent","",1,false); repeated.Buy("EvolutionScent","",1,false);
        Reject(() => CharacterBuildRules.ValidateSavedPurchases(repeated,definitions), "An over-ranked purchased evolution fails preflight");
        var shared = CharacterBuild.Begin(10,EidolonForm.Biped,null,false);
        shared.Buy("EvolutionScent","",1,false); shared.Buy("EvolutionScent","",1,true);
        CharacterBuildRules.ValidateSavedPurchases(shared,definitions);
        Check(true,"Aspect and eidolon purchases keep separate rank ownership");

        const long minute = TimeSpan.TicksPerMinute;
        foreach (var row in new[] { (59L,20,0,60L,false), (60L,20,1,120L,false), (180L,20,3,240L,false),
            (180L,3,3,240L,false), (180L,2,2,0L,true), (60L,0,0,0L,true), (86400L,20,20,0L,true) })
        {
            var charge = new TimedResourceCharge(row.Item1 * TimeSpan.TicksPerSecond,minute,minute,row.Item2);
            Check(charge.Units == row.Item3 && charge.PaidUntil == row.Item4 * TimeSpan.TicksPerSecond && charge.Expired == row.Item5,
                "Twin charges all crossed boundaries against native availability: " + row.Item1 + " seconds / " + row.Item2 + " units");
        }
        var savedDeadline = new TimedResourceCharge(180 * TimeSpan.TicksPerSecond,minute,minute,20).PaidUntil;
        var repeatedTick = new TimedResourceCharge(180 * TimeSpan.TicksPerSecond,savedDeadline,minute,17);
        Check(repeatedTick.Units == 0 && !repeatedTick.Expired, "Repeated reconciliation at the same time does not charge again");
        Console.WriteLine($"PASS: {checks} review regression assertions. Native adapters are not exercised.");
    }
    private sealed class Budget : IAbilityBudget
    {
        public bool TrySpend(DailyAbility ability,int amount) => true;
    }
}
