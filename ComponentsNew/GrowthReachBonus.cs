using Kingmaker.Blueprints;
using Kingmaker.Blueprints.JsonSystem;
using Kingmaker.EntitySystem.Stats;
using Kingmaker.Enums;
using Kingmaker.UnitLogic;
using BlueprintCore.Utils;
using WOTR_MAKING_FRIENDS.GUIDs;
using WOTR_MAKING_FRIENDS.Utilities;

namespace WOTR_MAKING_FRIENDS.ComponentsNew
{
    [TypeId("c5aecba5a6e542e8868bf6b1ee71e9a1")]
    public class GrowthReachBonus : UnitFactComponentDelegate
    {
        public override void OnTurnOn() => Apply();
        public override void OnRecalculate() => Apply();
        public override void OnTurnOff() => Owner.Stats.GetStat(StatType.Reach).RemoveModifiersFrom(Runtime);

        private void Apply()
        {
            var reach = Owner.Stats.GetStat(StatType.Reach);
            reach.RemoveModifiersFrom(Runtime);
            bool biped = (!Owner.IsPet && !Owner.IsReallyInFactPet) ||
                Owner.Progression.Features.HasFact(BlueprintTool.GetRef<BlueprintFeatureReference>(GetGUID.GUIDByName("EidolonBipedBaseFormFeature")));
            int rank = Fact.GetRank();
            // The native table stores total reach, including Medium's five feet.
            int native = rank < 1 ? 0 : WeaponSizeExtension.GetModifiers(rank == 1 ? Size.Large : Size.Huge).Reach -
                WeaponSizeExtension.GetModifiers(Size.Medium).Reach;
            int bonus = BaselineRules.GrowthReachAdjustment(rank, biped, native);
            if (bonus != 0) reach.AddModifier(bonus, Runtime, ModifierDescriptor.Other);
        }
    }
}
