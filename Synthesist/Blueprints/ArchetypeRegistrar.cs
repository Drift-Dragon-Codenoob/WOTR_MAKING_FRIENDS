using BlueprintCore.Blueprints.CustomConfigurators.Classes;
using BlueprintCore.Utils;
using Kingmaker.Blueprints;
using Kingmaker.Blueprints.Classes;
using System;
using System.Collections.Generic;
using System.Linq;
using WOTR_MAKING_FRIENDS.GUIDs;
using WOTR_MAKING_FRIENDS.Synthesist.Rules;
using WOTR_MAKING_FRIENDS.Utilities;

namespace WOTR_MAKING_FRIENDS.Synthesist.Blueprints
{
    internal static class ArchetypeRegistrar
    {
        // Called only when the complete gameplay feature set exists. Do not register empty
        // feature labels in normal startup: they would offer a non-functional archetype.
        internal static BlueprintArchetype Register(IReadOnlyDictionary<string, BlueprintFeatureBase> features)
        {
            if (features == null) throw new ArgumentNullException(nameof(features));
            var parent = BlueprintTool.Get<BlueprintCharacterClass>(GetGUID.GUIDByName("SummonerClass"));
            if (parent == null || parent.Progression == null)
                throw new InvalidOperationException("Register Synthesist after the Summoner progression.");

            foreach (var grant in ArchetypeProgression.Additions)
            {
                if (!features.TryGetValue(grant.Feature, out var feature) || feature == null ||
                    feature.AssetGuid.ToString() != BlueprintGuid.Parse(GetGUID.GUIDByName(grant.Feature)).ToString())
                    throw new InvalidOperationException("Missing or incorrectly identified Synthesist feature: " + grant.Feature);
            }

            LocalizationTool.LoadEmbeddedLocalizationPacks("WOTR_MAKING_FRIENDS.Output.Localizations.Synthesist.json");

            // Fail before changing the parent class if a future baseline revision moves a grant.
            var removals = ArchetypeProgression.Removals.Select(grant => new
            {
                Grant = grant,
                Feature = BlueprintTool.Get<BlueprintFeatureBase>(GetGUID.GUIDByName(grant.Feature))
            }).ToArray();
            foreach (var removal in removals)
            {
                int count = parent.Progression.LevelEntries.Where(e => e.Level == removal.Grant.Level)
                    .SelectMany(e => e.Features).Count(f => f == removal.Feature);
                if (removal.Feature == null || count != 1)
                    throw new InvalidOperationException("Expected exactly one Summoner grant at level " +
                        removal.Grant.Level + ": " + removal.Grant.Feature);
            }

            var id = BlueprintGuid.Parse(GetGUID.GUIDByName("SynthesistArchetype"));
            if (parent.m_Archetypes != null && parent.m_Archetypes.Any(a => a.deserializedGuid == id))
                throw new InvalidOperationException("Synthesist is already registered on this class.");

            var builder = ArchetypeConfigurator.New("SynthesistArchetype", id.ToString(),
                    parent.ToReference<BlueprintCharacterClassReference>())
                .SetLocalizedName(Helpers.ObtainString("SynthesistArchetype.Name"))
                .SetLocalizedDescription(Helpers.ObtainString("SynthesistArchetype.Description"))
                .SetReplaceSpellbook(SynthesistBlueprints.Spellbook);

            foreach (var removal in removals)
                builder.AddToRemoveFeatures(removal.Grant.Level,
                    removal.Feature.ToReference<BlueprintFeatureBaseReference>());
            foreach (var grant in ArchetypeProgression.Additions)
                builder.AddToAddFeatures(grant.Level,
                    features[grant.Feature].ToReference<BlueprintFeatureBaseReference>());
            for (int level = 1; level <= 20; level++)
                builder.AddToAddFeatures(level, CharacterBuildIntegration.Root.ToReference<BlueprintFeatureBaseReference>());

            // The spellbook clone adds Synthesist healing without modifying ordinary Summoners.
            // No permanent BAB override: replacement BAB is active only in fusion.
            return builder.ConfigureWithLogging();
        }
    }
}
