using HarmonyLib;
using Kingmaker.EntitySystem.Entities;
using Kingmaker.Items;
using Kingmaker.View;
using Kingmaker.View.Equipment;
using Kingmaker.Visual.CharacterSystem;

namespace WOTR_MAKING_FRIENDS.Synthesist.Runtime
{
    internal static class AppearanceEquipment
    {
        // Visual-only guards: never alter slots, item facts, attacks or inventory.
        internal static bool UsesBaseModel(UnitEntityData unit)
        {
            if (unit == null) return false;
            var part = unit.Get<UnitPartSynthesist>();
            if (part?.IsFused == true || part?.TwinActive == true) return true;
            var master = unit.Master?.Get<UnitPartSynthesist>();
            return master?.Eidolon == unit;
        }
    }

    [HarmonyPatch(typeof(UnitEntityView), "ShouldUpdateEquipment")]
    internal static class AppearanceBodyEquipment
    {
        private static void Postfix(UnitEntityView __instance, ref bool __result)
        {
            if (__result && AppearanceEquipment.UsesBaseModel(__instance.EntityData)) __result = false;
        }
    }

    [HarmonyPatch(typeof(UnitEntityView), nameof(UnitEntityView.AddItemEquipment),
        new[] { typeof(ItemEntity), typeof(UnitEntityData), typeof(Character) })]
    internal static class AppearanceItemEquipment
    {
        private static bool Prefix(UnitEntityData __1) => !AppearanceEquipment.UsesBaseModel(__1);
    }

    [HarmonyPatch(typeof(UnitViewHandsEquipment), nameof(UnitViewHandsEquipment.UpdateVisibility))]
    internal static class AppearanceHandEquipment
    {
        private static void Prefix(UnitViewHandsEquipment __instance, ref bool __0)
        {
            if (__0 && AppearanceEquipment.UsesBaseModel(__instance.Owner)) __0 = false;
        }
    }
}
