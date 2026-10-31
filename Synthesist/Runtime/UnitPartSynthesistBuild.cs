using Kingmaker.Blueprints.JsonSystem;
using Kingmaker.UnitLogic;
using Newtonsoft.Json;
using WOTR_MAKING_FRIENDS.Synthesist.Rules;

namespace WOTR_MAKING_FRIENDS.Synthesist.Runtime
{
    // No entity references: native level-up preview copies can edit this without
    // touching the canonical eidolon. Native Commit replays the selected actions.
    [TypeId("aa470f8543ba46709bbf8f78db88615b")]
    public sealed class UnitPartSynthesistBuild : UnitPart
    {
        [JsonProperty] internal CharacterBuild Current;
        [JsonProperty] internal CharacterBuild Draft;
        [JsonProperty] internal int Revision;
        [JsonProperty] internal string AppearanceOverride;
        internal string SelectedAppearance => AppearanceOverride ?? Current?.Appearance;
        public override void OnPostLoad()
        {
            base.OnPostLoad();
            Current?.Validate(); Draft?.Validate();
        }
        internal void Confirm()
        {
            Draft.Validate();
            Draft.Confirmed = true;
            Current = Draft.Copy();
            AppearanceOverride = null;
            Draft = null;
            Revision++;
        }
        internal void RecordPurchase(EvolutionPurchase purchase)
        {
            if (Current == null) return; // Existing saves can adopt a full plan at their next advancement/respec.
            var copy = Current.Copy(); copy.Confirmed = false;
            copy.Buy(purchase.Key, purchase.Option, purchase.Cost, purchase.Aspect);
            Current = copy;
        }
        internal void RecordReset()
        {
            if (Current == null) return;
            Current = Current.Copy(); Current.Purchases.Clear(); Current.Confirmed = false;
        }
        internal void ConfirmPreparation()
        {
            if (Current != null) { Current = Current.Copy(); Current.Confirmed = true; }
        }
    }
}
