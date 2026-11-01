using Newtonsoft.Json;
using WOTR_MAKING_FRIENDS.Synthesist.Rules;

internal static class SummonerFeatProbes
{
    internal static void Run()
    {
        int checks = 0;
        void Check(bool value, string message) { checks++; if (!value) throw new Exception("Summoner feats: " + message); }
        foreach (var row in new[] { (0,0), (1,1), (4,1), (5,2), (9,2), (10,3), (14,3), (15,4), (19,4), (20,5), (40,5) })
            Check(SummonerFeatRules.ExtraEvolutionLimit(row.Item1) == row.Item2, "Extra Evolution character-level repeat limit " + row.Item1);
        var ordinary = CharacterBuild.Begin(1, EidolonForm.Biped, null, false);
        var extra = CharacterBuild.Begin(1, EidolonForm.Biped, null, false, 1);
        Check(extra.Remaining == ordinary.Remaining + 1 && extra.AspectRemaining == ordinary.AspectRemaining, "Extra point increases only the eidolon pool");
        extra.Buy("EvolutionScent", "", 1, false); extra.Appearance = "Nabasu"; extra.Confirmed = true;
        var saved = JsonConvert.DeserializeObject<CharacterBuild>(JsonConvert.SerializeObject(extra))!;
        saved.Validate();
        Check(saved.BonusEvolutionPoints == 1 && saved.Remaining == 3, "Bonus allowance and its purchase persist");
        var advancement = CharacterBuild.Begin(5, EidolonForm.Biped, saved, true, 2);
        Check(advancement.Purchases.Count == 1 && advancement.BonusEvolutionPoints == 2 && advancement.Remaining == EidolonProgression.At(5).EvolutionPoints + 1,
            "Advancement retains purchases and uses the current feat count exactly once");
        Check(saved.BonusEvolutionPoints == 1, "Preview allowance edits do not mutate the previous build");
        var legacy = JsonConvert.DeserializeObject<CharacterBuild>(JsonConvert.SerializeObject(ordinary).Replace("\"BonusEvolutionPoints\":0,", ""))!;
        Check(legacy.BonusEvolutionPoints == 0 && legacy.Remaining == 3, "Pre-feat saves default to no bonus");
        for (int maximum = 1; maximum <= 9; maximum++)
        {
            Check(SummonerFeatRules.ArcanaMaximum(maximum, false) == maximum, "One spell may use the highest native spell level");
            Check(SummonerFeatRules.ArcanaMaximum(maximum, true) == maximum - 1, "Both lower spells must stay below the highest native spell level");
        }
        Check(SummonerFeatRules.RendingQualification(13, 6, 2), "Rending accepts exact prerequisites");
        Check(!SummonerFeatRules.RendingQualification(12, 6, 2) && !SummonerFeatRules.RendingQualification(13, 5, 2) &&
            !SummonerFeatRules.RendingQualification(13, 6, 1), "Each Rending prerequisite is enforced");
        var round = new RendingClawsRound(); round.BeginTurn("turn one");
        Check(!round.Hit("A", false) && !round.Hit("A", true), "Misses and non-claws do not count; the first claw adds no damage");
        Check(!round.Hit("B", true) && round.Hit("A", true), "Two hits must share a target");
        Check(!round.Hit("B", true) && !round.Hit("A", true), "The bonus is consumed once for the whole round, not once per target");
        var loaded = JsonConvert.DeserializeObject<RendingClawsRound>(JsonConvert.SerializeObject(round))!;
        loaded.BeginTurn("turn two");
        Check(loaded.Spent && !loaded.Hit("C", true), "Saving and a different actor's turn do not refill a spent bonus");
        loaded.Reset(); loaded.BeginTurn("next round");
        Check(!loaded.Hit("A", true), "The next personal round starts with no previous hit");
        loaded.BeginTurn("different actor");
        Check(!loaded.Hit("A", true), "Hits from different turns cannot form a pair");
        var midTurn = JsonConvert.DeserializeObject<RendingClawsRound>(JsonConvert.SerializeObject(loaded))!;
        midTurn.BeginTurn("different actor");
        Check(midTurn.Hit("A", true), "A mid-turn save retains the first hit");
        Console.WriteLine($"PASS: {checks} Summoner feat rules assertions. Native selections, buffs and damage events are not exercised.");
    }
}
