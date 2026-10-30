using System;
using System.Collections.Generic;

namespace WOTR_MAKING_FRIENDS.Synthesist.Rules
{
    internal sealed class ArchetypeGrant
    {
        public int Level { get; }
        public string Feature { get; }
        public ArchetypeGrant(int level, string feature) { Level = level; Feature = feature; }
    }

    internal static class ArchetypeProgression
    {
        // Root grants only. Never insert a nested pet grant into an archetype's removal array.
        public static IReadOnlyList<ArchetypeGrant> Removals { get; } = Array.AsReadOnly(new[]
        {
            new ArchetypeGrant(1, "SummonerLifeLinkFeature"),
            new ArchetypeGrant(1, "EidolonBaseFeatureSelection"),
            new ArchetypeGrant(1, "EidolonProgression"),
            new ArchetypeGrant(1, "EidolonRankFeature"),
            // Baseline grants these to the Summoner, not merely the pet. New arms are form-dependent.
            new ArchetypeGrant(1, "EvolutionLimbsArmsFeature"),
            new ArchetypeGrant(1, "EvolutionLimbsArmsBaseFeature"),
            new ArchetypeGrant(2, "SummonerBondedSensesFeature"),
            new ArchetypeGrant(4, "SummonerShieldAllyFeature"),
            new ArchetypeGrant(6, "SummonerMakersCallFeature"),
            new ArchetypeGrant(8, "SummonerTranspositionFeature"),
            new ArchetypeGrant(12, "SummonerShieldAllyGreaterFeature"),
            new ArchetypeGrant(14, "SummonerLifeBondFeature")
        });

        public static IReadOnlyList<ArchetypeGrant> Additions { get; } = Array.AsReadOnly(new[]
        {
            new ArchetypeGrant(1, "SynthesistFusedEidolonFeature"),
            new ArchetypeGrant(1, "SynthesistFusedLinkFeature"),
            new ArchetypeGrant(1, "SynthesistBaseFormSelection"),
            new ArchetypeGrant(4, "SynthesistShieldedMeldFeature"),
            new ArchetypeGrant(6, "SynthesistMakersJumpFeature"),
            new ArchetypeGrant(12, "SynthesistGreaterShieldedMeldFeature"),
            new ArchetypeGrant(16, "SynthesistSplitFormsFeature"),
            new ArchetypeGrant(20, "SynthesistTwinEidolonFeature")
        });
    }
}
