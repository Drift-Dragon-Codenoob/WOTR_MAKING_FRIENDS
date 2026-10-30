using System.Runtime.Serialization;
using Newtonsoft.Json;
using WOTR_MAKING_FRIENDS.Synthesist.Rules;

internal static class SynthesistRulesProbes
{
    private sealed class ProbeBudget : IAbilityBudget
    {
        private readonly int[] spent = new int[3];
        private static readonly int[] maximum = [2, 16, 20];
        public bool TrySpend(DailyAbility ability, int amount)
        {
            int index = (int)ability;
            if (amount < 0 || spent[index] + amount > maximum[index]) return false;
            spent[index] += amount;
            return true;
        }
        public int Spent(DailyAbility ability) => spent[(int)ability];
        public void Restore() => Array.Clear(spent);
    }
    internal static void Run()
    {
        int checks = 0;
        var budget = new ProbeBudget();
        void Check(bool condition, string message)
        {
            checks++;
            if (!condition) throw new Exception("Synthesist: " + message);
        }
        void Reject(Action action, string message)
        {
            bool rejected = false;
            try { action(); }
            catch (ArgumentException) { rejected = true; }
            catch (InvalidOperationException) { rejected = true; }
            Check(rejected, message);
        }
        T RoundTrip<T>(T value)
        {
            // Use the installed game's Newtonsoft.Json build, not an unrelated test serializer.
            return JsonConvert.DeserializeObject<T>(JsonConvert.SerializeObject(value))!;
        }

        int[] hd = [1, 2, 3, 3, 4, 5, 6, 6, 7, 8, 9, 9, 10, 11, 12, 12, 13, 14, 15, 15];
        Check(AspectBudget.Allowance(9) == 0 && AspectBudget.Allowance(10) == 2 && AspectBudget.Allowance(18) == 6, "Aspect allowance boundaries");
        Check(AspectBudget.AdditionalDiversion(10, 0, 1) == 1 && AspectBudget.AdditionalDiversion(10, 1, 1) == 1, "Ordinary Aspect charges each point");
        Check(AspectBudget.AdditionalDiversion(18, 0, 1) == 1 && AspectBudget.AdditionalDiversion(18, 1, 1) == 0, "Greater Aspect rounds the aggregate, not each purchase");
        Check(AspectBudget.AdditionalDiversion(18, 3, 3) == 1 && AspectBudget.Diverted(18, 6) == 3, "Greater Aspect maximum diversion");
        Reject(() => AspectBudget.AdditionalDiversion(18, 6, 1), "Restoration cannot exceed Greater Aspect allowance");
        int[] points = [3, 4, 5, 7, 8, 9, 10, 11, 13, 14, 15, 16, 17, 19, 20, 21, 22, 23, 25, 26];
        int[] attacks = [3, 3, 3, 4, 4, 4, 4, 4, 5, 5, 5, 5, 5, 6, 6, 6, 6, 6, 7, 7];
        for (int level = 1; level <= 20; level++)
        {
            var row = EidolonProgression.At(level);
            Check(row.HitDice == hd[level - 1] && row.BaseAttackBonus == hd[level - 1], "Original HD and BAB at " + level);
            Check(row.EvolutionPoints == points[level - 1], "Original evolution pool at " + level);
            Check(row.MaximumNaturalAttacks == attacks[level - 1], "Original natural attack cap at " + level);
            Check(row.AbilityIncreases == hd[level - 1] / 4, "Ability increases follow HD, including level 20");
            Check(row.SkillRanks == 4 * row.HitDice, "Independent skill ranks at " + level);
        }
        Reject(() => EidolonProgression.At(0), "No level-zero fallback");
        Reject(() => EidolonProgression.At(21), "No extrapolated level-21 table");
        Check(EidolonProgression.MakersJumpUses(5) == 0 && EidolonProgression.MakersJumpUses(6) == 1 &&
            EidolonProgression.MakersJumpUses(11) == 1 && EidolonProgression.MakersJumpUses(12) == 2 &&
            EidolonProgression.MakersJumpUses(18) == 3, "Jump resource boundaries");
        Check(EidolonProgression.MeldBonus(3) == 0 && EidolonProgression.MeldBonus(4) == 2 &&
            EidolonProgression.MeldBonus(12) == 4, "Meld replaces rather than adds bonuses");
        Check(ArchetypeProgression.Removals.Any(x => x.Level == 2 && x.Feature == "SummonerBondedSensesFeature"), "Remove Bond Senses at its actual level");
        Check(!ArchetypeProgression.Removals.Any(x => x.Level == 16), "Do not invent a Merge Forms removal");
        Check(ArchetypeProgression.Additions.Any(x => x.Level == 20 && x.Feature == "SynthesistTwinEidolonFeature"), "Original capstone supplied");
        Check(!ArchetypeProgression.Removals.Any(x => x.Feature.Contains("Aspect")), "Retain Aspect progression");
        Check(BaseForms.For(EidolonForm.Biped).Strength == 16 &&
            BaseForms.For(EidolonForm.Biped).FreeEvolutions.Any(x => x.Key == "LimbsArms"), "Biped starts with arms and original Strength");
        Check(BaseForms.For(EidolonForm.Quadruped).SpeedFeet == 40 &&
            BaseForms.For(EidolonForm.Quadruped).FreeEvolutions.Single(x => x.Key == "LimbsLegs").Ranks == 2,
            "Quadruped movement and free limbs");
        Check(BaseForms.For(EidolonForm.Serpentine).ClimbSpeedFeet == 20 &&
            BaseForms.For(EidolonForm.Serpentine).FreeEvolutions.Any(x => x.Key == "ReachBite"),
            "Serpentine climb and bite-specific reach");
        foreach (var form in Enum.GetValues<EidolonForm>())
        {
            var definition = BaseForms.For(form);
            Check(definition.NaturalArmour == 2 && definition.Constitution == 13, "Starting armour and Constitution for " + form);
            Check(new[] { definition.GoodFortitude, definition.GoodReflex, definition.GoodWill }.Count(x => x) == 2,
                "Two good independent saves for " + form);
        }

        Check(PfSpellComponents.RequiresSomatic("Blur") == false && PfSpellComponents.RequiresSomatic("DimensionDoorBase") == false,
            "Verified verbal-only spell exceptions remain castable without arms");
        Check(PfSpellComponents.RequiresSomatic("MageArmor") == true && PfSpellComponents.RequiresSomatic("SynthesistRejuvenate1Spell") == true,
            "Known somatic spells require appropriate limbs");
        Check(PfSpellComponents.RequiresSomatic("UnknownModSpell") == null,
            "Unknown components are not treated as a spell without somatic components");

        var pool = new FusionDurability(30);
        var hit = pool.TakeHit(10, 20, false, 1, 7);
        Check(pool.Remaining == 20 && hit.PersonalHpLost == 0, "Ordinary damage consumes eidolon temporary HP");
        hit = pool.TakeHit(24, 20, true, 10, 7);
        Check(pool.Remaining == 1 && hit.PersonalHpSacrificed == 5 && !hit.EidolonSlain, "Link spends minimum necessary");
        Check(pool.HealEidolon(100) == 29 && pool.Remaining == 30, "Rejuvenation capped by missing eidolon HP");
        hit = pool.TakeHit(50, 20, true, 10, 7);
        Check(hit.PersonalHpSacrificed == 0 && hit.OverflowDamage == 20 && pool.Slain, "Insufficient authorisation declines without a partial sacrifice");
        Check(hit.PersonalHpLost == 20, "No 2-HP floor or overkill immunity");
        Check(pool.HealEidolon(30) == 0 && !pool.CanReturn(7), "Healing cannot revive a slain eidolon");
        Reject(() => pool.CompleteSummoning(7), "Cannot summon again on death day");
        pool = RoundTrip(pool); pool.Validate();
        Check(pool.Slain && pool.ReturnDay == 8, "Death lockout survives serialisation");
        pool.CompleteSummoning(8);
        Check(pool.Remaining == 15 && !pool.Slain, "Following-day ritual returns half HP");
        pool.CompleteSummoning(8);
        Check(pool.Remaining == 15, "Repeated ritual grants no healing");
        pool.ChangeMaximum(40);
        Check(pool.Remaining == 25, "Maximum changes preserve wounds");
        pool.ChangeMaximum(30);
        Check(pool.Remaining == 15, "Ending maximum bonus cannot erase wounds");

        // Check conservation and authorisation over a broad range, including exact exhaustion.
        for (int temporary = 1; temporary <= 25; temporary++)
        for (int damage = 0; damage <= 65; damage++)
        foreach (bool link in new[] { false, true })
        {
            var sample = new FusionDurability(temporary);
            var result = sample.TakeHit(damage, 30, link, 7, 1);
            Check(result.TemporaryHpLost + result.PersonalHpLost == damage, "No damage created or discarded");
            Check(result.PersonalHpSacrificed <= 23 && (link || result.PersonalHpSacrificed == 0), "Authorised personal HP limit");
            Check(result.PersonalHpSacrificed == 0 || (sample.Remaining == 1 && !sample.Slain), "Sacrifice always preserves the eidolon");
        }
        hit = new FusionDurability(1).TakeHit(int.MaxValue, 10, true, 1, 0);
        Check(hit.PersonalHpSacrificed == 0 && hit.PersonalHpLost == int.MaxValue - 1, "Extreme overkill does not wrap");

        FusionState Prepared(int level = 16)
        {
            var state = new FusionState("canonical-eidolon-42", EidolonForm.Biped, level, 80);
            state.EnterPreparation(false); state.ConfirmEvolutions(); state.LeavePreparation();
            return state;
        }
        var fusion = Prepared();
        Check(!fusion.LinkEnabled && fusion.PersonalHpLimit == 1, "Sacrifice defaults to disabled");
        fusion.EnterPreparation(false); fusion.OnCombatStarted();
        Check(fusion.Mode == FusionMode.Unfused, "Combat closes preparation");
        Reject(() => fusion.EnterPreparation(true), "No preparation combatant");
        Reject(() => fusion.CompleteFusionRitual(1, 2, 1), "No undersized shell");
        fusion.CompleteFusionRitual(1, 1, 1);
        fusion.TakeFusedHit(12, 30, 1);
        fusion.UseMakersJump(budget);
        Reject(() => fusion.SetLevel(17), "Dismiss before level-up");
        fusion.Dismiss(); fusion.CompleteFusionRitual(1, 1, 1);
        Check(fusion.Durability.Remaining == 68 && budget.Spent(DailyAbility.MakersJump) == 1, "Dismiss and re-fuse preserve damage and resources");
        long now = 100;
        fusion.BeginSplit(now, budget);
        Check(budget.Spent(DailyAbility.SplitForms) == 1, "Split consumes its first round immediately");
        fusion.SetSplitWounds(18, 1);
        fusion = RoundTrip(fusion); fusion.Validate();
        Check(fusion.Mode == FusionMode.Split && fusion.EidolonEntityId == "canonical-eidolon-42" &&
            fusion.Durability.Remaining == 62 && fusion.SplitPaidUntil == now + FusionState.RoundTicks,
            "Active split retains identity, damage and timer on save/load");
        Check(!fusion.AdvanceSplit(now + FusionState.RoundTicks - 1, 1, budget), "No premature round charge");
        Check(!fusion.AdvanceSplit(now + FusionState.RoundTicks, 1, budget), "Paid next round without premature expiry");
        Check(budget.Spent(DailyAbility.SplitForms) == 2, "A round boundary consumes exactly one round");
        Check(fusion.AdvanceSplit(now + FusionState.RoundTicks * 16, 1, budget), "Split expires at the daily allowance");
        Check(fusion.Mode == FusionMode.Fused && fusion.Durability.Remaining == 62, "Expiry recombines without healing");
        Reject(() => fusion.BeginSplit(now, budget), "No free additional Split Forms after expiry");
        fusion.OnCompletedRest(); budget.Restore();
        Check(fusion.Mode == FusionMode.Unfused && budget.Spent(DailyAbility.SplitForms) == 0 && budget.Spent(DailyAbility.MakersJump) == 0 &&
            fusion.Durability.Remaining == 62, "Rest resets daily abilities without healing eidolon");
        fusion.CompleteFusionRitual(2, 1, 1); fusion.BeginSplit(100, budget); fusion.EidolonDied(2);
        fusion.OnCompletedRest(); budget.Restore();
        Check(fusion.Mode == FusionMode.Unfused && fusion.Durability.Slain && !fusion.Durability.CanReturn(2), "Rest cannot bypass death return day");
        fusion.CompleteFusionRitual(3, 1, 1);
        Check(fusion.Durability.Remaining == 40, "Half-HP recovery through state controller");
        fusion.ForceDismiss(); fusion.SetLevel(17);
        Check(fusion.EvolutionEditsOpen, "Level advancement opens evolution allocation");
        Reject(() => fusion.CompleteFusionRitual(3, 1, 1), "Cannot fight with an unconfirmed evolution build");
        fusion = Prepared(20); fusion.SpendTwinMinute(budget);
        fusion = RoundTrip(fusion); fusion.Validate();
        Check(budget.Spent(DailyAbility.TwinEidolon) == 1, "Capstone expenditure survives load");
        for (int i = 1; i < 20; i++) fusion.SpendTwinMinute(budget);
        Reject(() => fusion.SpendTwinMinute(budget), "Cannot overspend Twin Eidolon budget");
        var json = JsonConvert.SerializeObject(fusion);
        Check(!json.Contains("Remaining"), "Persist canonical wounds, not a second current-HP value");
        bool badSaveRejected = false;
        try { JsonConvert.DeserializeObject<FusionState>(json.Replace("\"Version\":2", "\"Version\":999")); }
        catch (Exception e) when (e is SerializationException || e.InnerException is SerializationException)
        { badSaveRejected = true; }
        Check(badSaveRejected, "Reject unsupported save schema before engine application");
        budget.Restore(); fusion = Prepared(); fusion.CompleteFusionRitual(1, 1, 1); fusion.BeginSplit(100, budget);
        fusion.SetSplitWounds(85, 1);
        fusion = RoundTrip(fusion); fusion.Validate();
        Check(!fusion.Durability.Slain && fusion.Durability.Wounds == 85, "An independent eidolon below zero HP is not automatically slain");
        fusion.EndSplit(1);
        Check(fusion.Mode == FusionMode.Unfused && fusion.Durability.Slain && fusion.Durability.ReturnDay == 2,
            "Agreed ruling: unconscious rejoin depletes the fused pool without a Summoner rescue");
        budget.Restore(); fusion = Prepared(); fusion.CompleteFusionRitual(1, 1, 1); fusion.BeginSplit(100, budget);
        fusion.SetSplitWounds(79, 1); fusion.EndSplit(1);
        Check(fusion.Mode == FusionMode.Fused && fusion.Durability.Remaining == 1,
            "Healing an independent eidolon permits rejoining with remaining HP");
        Console.WriteLine($"PASS: {checks} Synthesist rules assertions. Native runtime adapters are not exercised.");
    }
}
