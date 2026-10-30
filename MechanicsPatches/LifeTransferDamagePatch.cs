using HarmonyLib;
using Kingmaker.Blueprints;
using Kingmaker.EntitySystem.Entities;
using Kingmaker.RuleSystem;
using Kingmaker.RuleSystem.Rules.Damage;
using Kingmaker.UnitLogic;
using System;
using System.Linq;
using WOTR_MAKING_FRIENDS.ComponentsNew;
using WOTR_MAKING_FRIENDS.Enums;
using WOTR_MAKING_FRIENDS.Utilities;

namespace WOTR_MAKING_FRIENDS.MechanicsPatches
{
    [HarmonyPatch(typeof(UnitEntityData), nameof(UnitEntityData.Damage), MethodType.Setter)]
    internal static class LifeTransferDamagePatch
    {
        [ThreadStatic] private static bool transferring;
        [ThreadStatic] private static RuleDealDamage activeTransfer;

        // The original hit has already been scaled. Scale only that hit, not the transfer again.
        [HarmonyPatch(typeof(RuleDealDamage), "ApplyDifficultyModifiers")]
        private static class TransferDifficultyPatch
        {
            private static bool Prefix(RuleDealDamage __instance, int damage, ref int __result)
            {
                if (__instance != activeTransfer) return true;
                __instance.DifficultyModifier = 1f;
                __result = damage;
                return false;
            }
        }

        private static bool HasActiveComponent<T>(UnitEntityData unit) where T : BlueprintComponent
        {
            foreach (var buff in unit.Buffs)
                if (buff.Active && buff.Blueprint.GetComponent<T>() != null)
                    return true;
            return false;
        }

        private static int Durability(UnitEntityData unit)
            => unit.HPLeft + unit.Stats.TemporaryHitPoints.ModifiedValue;

        private static int Transfer(RuleDealDamage reason, UnitEntityData recipient, int amount)
        {
            if (amount <= 0 || recipient == null || recipient.State.IsDead || recipient.HPLeft < 1)
                return 0;
            int before = Durability(recipient);
            var previousTransfer = activeTransfer;
            try
            {
                activeTransfer = new RuleDealDamage(reason.Target, recipient, new DirectDamage(DiceFormula.Zero, amount));
                Rulebook.Trigger(activeTransfer);
            }
            finally { activeTransfer = previousTransfer; }
            return Math.Min(amount, Math.Max(0, before - Durability(recipient)));
        }

        private static void Prefix(UnitEntityData __instance, ref int value)
        {
            // Restrict this global hook to the currently committing, real HP-damage rule.
            // The engine has already consumed temporary HP and applied difficulty scaling.
            if (transferring || value <= __instance.Damage || __instance.HPLeft < 1 || __instance.State.IsDead ||
                Rulebook.CurrentContext?.CurrentEvent is not RuleDealDamage damage || damage.IsFake ||
                damage.Target != __instance || __instance.LastHandledDamage != damage)
                return;

            int deficit = BaselineRules.ProtectionDeficit(__instance.MaxHP, value);
            if (deficit == 0)
                return;

            int paid = 0;
            try
            {
                // Transferred damage may not bounce between Life Link and Life Bond.
                transferring = true;
                if ((__instance.IsPet || __instance.IsReallyInFactPet) &&
                    __instance.Master != null && HasActiveComponent<LifeLinkEidolon>(__instance))
                {
                    // As in the original link, the Summoner can die from taking the pet's damage.
                    paid = Transfer(damage, __instance.Master, deficit);
                }
                else if (!__instance.IsPet && !__instance.IsReallyInFactPet && HasActiveComponent<LifeBond>(__instance))
                {
                    var pets = __instance.Pets
                        .Where(p => p.EntityPart?.Type == PetTypeExtensions.Eidolon && p.Entity != null && !p.Entity.State.IsDead)
                        .Select(p => p.Entity).ToArray();
                    // Life Bond only spends durability an eidolon can give while retaining 1 HP.
                    var capacities = pets.Select(p => Math.Max(0, Durability(p) - 1)).ToArray();
                    var amounts = BaselineRules.AllocateProtection(deficit, capacities);
                    for (int i = 0; i < pets.Length; i++)
                        paid += Transfer(damage, pets[i], amounts[i]);
                }
                value -= Math.Min(paid, value - __instance.Damage);
            }
            finally
            {
                transferring = false;
            }
        }
    }
}
