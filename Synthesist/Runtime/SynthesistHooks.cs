using HarmonyLib;
using Kingmaker;
using Kingmaker.Blueprints;
using Kingmaker.EntitySystem.Entities;
using Kingmaker.EntitySystem.Stats;
using Kingmaker.RuleSystem;
using Kingmaker.RuleSystem.Rules.Damage;
using Kingmaker.UnitLogic.Abilities;
using Kingmaker.UnitLogic.Abilities.Blueprints;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityModManagerNet;
using WOTR_MAKING_FRIENDS.Synthesist.Blueprints;
using WOTR_MAKING_FRIENDS.Synthesist.Rules;

namespace WOTR_MAKING_FRIENDS.Synthesist.Runtime
{
    [HarmonyPatch(typeof(Kingmaker.Items.Slots.ItemSlot), "OnActiveChanged")]
    internal static class FusionArmourLoadGuard
    {
        private static bool Prefix(Kingmaker.Items.Slots.ItemSlot __instance)
        {
            var owner = __instance.Owner?.Unit;
            var part = owner?.Get<UnitPartSynthesist>();
            // Slot activity is saved, but the deactivation counter is rebuilt. A saved
            // suppressed armour item has already lost its wearer and item effects.
            // Releasing/reapplying the form must not unequip that item a second time.
            return part == null || (!part.IsFused && !part.TwinActive) ||
                __instance != owner.Body.Armor || __instance.Active ||
                __instance.MaybeItem == null || __instance.MaybeItem.Wielder != null;
        }
    }

    internal static class SynthesistLifecycle
    {
        private static readonly HashSet<string> reported = new();
        internal static void Update(UnityModManager.ModEntry entry, float delta)
        {
            if (!SynthesistBlueprints.Ready || Game.Instance?.Player == null) return;
            if (Game.Instance.IsLoadingSave || Game.Instance.IsUnloading ||
                Kingmaker.EntitySystem.Persistence.LoadingProcess.Instance.IsLoadingInProcess ||
                Game.Instance.CurrentMode == Kingmaker.GameModes.GameModeType.None)
            {
                ToyBoxAfterCombatCompatibility.ClearPending();
                return;
            }
#if SYNTHESIST_ACCEPTANCE
            RuntimeAcceptance.Poll();
            var timing = RuntimePerformance.Measure("Lifecycle.Update");
#endif
            foreach (var unit in Game.Instance.Player.AllCrossSceneUnits.Concat(Game.Instance.Player.PartyAndPets).Distinct().ToArray())
            {
                var part = unit.Get<UnitPartSynthesist>();
                if (part == null) continue;
                try
                {
                    if (part.State == null)
                    {
                        foreach (EidolonForm form in Enum.GetValues(typeof(EidolonForm)))
                            if (unit.Progression.Features.HasFact(SynthesistBlueprints.ExistingFeature("Synthesist" + form + "FormFeature")))
                                part.Initialise(form);
                    }
                    part.Tick();
                }
                catch (Exception error)
                {
                    if (reported.Add(unit.UniqueId + error.Message)) Main.Log("Synthesist runtime: " + unit.CharacterName, error);
                }
            }
            ToyBoxAfterCombatCompatibility.ProcessPending();
#if SYNTHESIST_ACCEPTANCE
            // Dispose before writing the sample so this frame's callback is included.
            timing.Dispose();
            RuntimePerformance.EndFrame();
#endif
        }
    }

    [HarmonyPatch(typeof(ModifiableValueTemporaryHitPoints), nameof(ModifiableValueTemporaryHitPoints.HandleDamage))]
    internal static class FusionDamageHook
    {
        [ThreadStatic] private static bool handling;
        [ThreadStatic] private static Dictionary<RuleDealDamage, int> payments;
        internal static int TakePayment(RuleDealDamage damage)
        {
            if (payments == null || !payments.TryGetValue(damage, out int amount)) return 0;
            payments.Remove(damage);
            return amount;
        }
        private static void Prefix(ModifiableValueTemporaryHitPoints __instance, out UnitPartSynthesist __state)
        {
            __state = null;
            if (handling || Rulebook.CurrentContext?.CurrentEvent is not RuleDealDamage damage || damage.IsFake) return;
            var owner = __instance.Owner.Unit;
            if (damage.Target != owner) return;
            var part = owner.Get<UnitPartSynthesist>();
            if (part?.IsFused != true || part.TemporaryPool == null) return;
            handling = true;
            __state = part;
            // Let native processing consume unrelated temporary HP first. The source-owned
            // eidolon pool is processed once afterwards and restored with its remaining HP.
            part.TemporaryPool.Remove();
            part.TemporaryPool = null;
        }

        private static void Postfix(ref int __result, UnitPartSynthesist __state)
        {
            if (__state == null) return;
            try
            {
                var hit = __state.State.TakeFusedHit(__result, __state.Owner.HPLeft, UnitPartSynthesist.Day);
                if (hit.PersonalHpSacrificed > 0 && Rulebook.CurrentContext?.CurrentEvent is RuleDealDamage damage)
                {
                    payments ??= new Dictionary<RuleDealDamage, int>();
                    payments[damage] = checked((payments.TryGetValue(damage, out int spent) ? spent : 0) + hit.PersonalHpSacrificed);
                }
                __result = hit.OverflowDamage;
                __state.SetPool(__state.State.Durability.Remaining);
                if (hit.EidolonSlain) __state.CollapsePending = true;
            }
            finally { handling = false; }
        }

        private static Exception Finalizer(Exception __exception, UnitPartSynthesist __state)
        {
            if (__state != null && handling)
            {
                handling = false;
                __state.SetPool(__state.State.Durability.Remaining);
            }
            return __exception;
        }
    }

    [HarmonyPatch(typeof(RuleDealDamage), nameof(RuleDealDamage.OnTrigger))]
    internal static class FusionDamageCompleteHook
    {
        private static void Postfix(RuleDealDamage __instance)
        {
            int payment = FusionDamageHook.TakePayment(__instance);
            if (payment > 0) __instance.Target.Damage = checked(__instance.Target.Damage + payment);
            var part = __instance.Target.Get<UnitPartSynthesist>();
            if (part?.CollapsePending == true) { part.CollapsePending = false; part.Dismiss(); }
        }
        private static Exception Finalizer(RuleDealDamage __instance, Exception __exception)
        {
            // A later native/mod rule handler can throw before this postfix runs.
            // Do not retain its event, target and scene through the thread cache.
            if (__exception != null) FusionDamageHook.TakePayment(__instance);
            return __exception;
        }
    }

    [HarmonyPatch(typeof(Kingmaker.UnitLogic.Mechanics.Actions.ContextActionKill), "RunAction")]
    internal static class FusionBanishmentHook
    {
        private static bool Prefix(Kingmaker.UnitLogic.Mechanics.Actions.ContextActionKill __instance)
        {
            var part = __instance.Target.Unit?.Get<UnitPartSynthesist>();
            if (part?.IsFused != true) return true;
            var source = __instance.Context?.SourceAbility;
            if (source == BlueprintCore.Blueprints.References.AbilityRefs.Dismissal.Reference.Get() ||
                source == BlueprintCore.Blueprints.References.AbilityRefs.Banishment.Reference.Get())
            {
                part.Dismiss();
                return false;
            }
            return true;
        }
    }

    [HarmonyPatch(typeof(Kingmaker.Controllers.Rest.RestController), "ApplyRest", typeof(Kingmaker.UnitLogic.UnitDescriptor))]
    internal static class FusionRestHook
    {
        private static void Prefix(Kingmaker.UnitLogic.UnitDescriptor __0)
        {
            var part = __0?.Unit?.Get<UnitPartSynthesist>();
            if (part == null) return;
            if (part.IsSplit) part.State.SetSplitWounds(part.Eidolon.Damage, UnitPartSynthesist.Day);
            part.Dismiss();
        }
    }

    [HarmonyPatch]
    internal static class RespecCompatibilityHook
    {
        private static IEnumerable<MethodBase> TargetMethods()
        {
            var helper = typeof(Kingmaker.UnitLogic.UnitHelper);
            foreach (var method in helper.GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
                .Where(m => m.Name == "Respec")) yield return method;
            var external = AccessTools.TypeByName("RespecWrath.RespecClass");
            if (external != null)
                foreach (var method in external.GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
                    .Where(m => m.Name == "Respecialize")) yield return method;
        }
        private static bool Prefix(UnitEntityData __0)
        {
            var original = __0?.PreviewOf.Entity ?? __0;
            var part = original?.Get<UnitPartSynthesist>();
            if (part == null) return true;
            if (!part.IsFused && !part.IsSplit && !part.TwinActive && !part.IsPreparing && !part.IsRitualActive) return true;
            Main.Log("Dismiss the eidolon form and close preparation before starting respec.");
            return false;
        }
    }

    [HarmonyPatch(typeof(Kingmaker.UnitLogic.Class.LevelUp.LevelUpController), "Start")]
    internal static class FusionLevelUpGuard
    {
        private static bool Prefix(UnitEntityData __0, Kingmaker.UnitLogic.Class.LevelUp.LevelUpState.CharBuildMode __1, out UnitPartSynthesist __state)
        {
            __state = null;
            var original = __0?.PreviewOf.Entity ?? __0;
            var part = original?.Get<UnitPartSynthesist>();
            if (part == null) return true;
            if (part.IsFused || part.IsSplit || part.TwinActive || part.IsPreparing || part.IsRitualActive) return false;
            // A respec options dialog may be cancelled before any controller is
            // created. Detach only when the actual respec transaction starts.
            if (__1 == Kingmaker.UnitLogic.Class.LevelUp.LevelUpState.CharBuildMode.Respec)
            { __state = part; part.SuspendForRespec(); }
            return true;
        }
        private static void Postfix(Kingmaker.UnitLogic.Class.LevelUp.LevelUpController __result, UnitPartSynthesist __state)
        {
            if (__result == null) __state?.ResumeAfterRespec();
            else if (!__result.Committed) __state?.SuspendForRespec();
        }
        private static Exception Finalizer(Exception __exception, UnitPartSynthesist __state)
        { if (__exception != null) __state?.ResumeAfterRespec(); return __exception; }
    }

    [HarmonyPatch(typeof(Kingmaker.UnitLogic.Class.LevelUp.LevelUpController), "Stop")]
    internal static class FusionRespecCancelled
    {
        private static void Postfix(Kingmaker.UnitLogic.Class.LevelUp.LevelUpController __instance)
        {
            if (__instance.Committed) return;
            var unit = __instance.Unit;
            (unit?.PreviewOf.Entity ?? unit)?.Get<UnitPartSynthesist>()?.ResumeAfterRespec();
        }
    }

    [HarmonyPatch(typeof(Kingmaker.UnitLogic.Class.LevelUp.Actions.LevelUpHelper), "GetTotalSkillPoints")]
    internal static class OriginalEidolonSkillRanks
    {
        private static void Postfix(Kingmaker.UnitLogic.UnitDescriptor __0, int __1, ref int __result)
        {
            if (!SynthesistBlueprints.Ready || __0?.Unit == null) return;
            foreach (EidolonForm form in Enum.GetValues(typeof(EidolonForm)))
                if (__0.Unit.OriginalBlueprint == SynthesistBlueprints.Pet(form))
                {
                    __result = Math.Max(1, 6 + __0.Stats.Intelligence.Bonus) * __1;
                    return;
                }
        }
    }

    [HarmonyPatch]
    internal static class ToyBoxAfterCombatCompatibility
    {
        private static bool restorePending;
        private static bool sawCombat;
        internal static void ClearPending() { restorePending = false; sawCombat = false; }
        private static MethodBase TargetMethod() => AccessTools.Method("Kingmaker.GameHistoryLog:HandlePartyCombatStateChanged");
        private static bool Prepare() => TargetMethod() != null;
        private static void Postfix(bool __0)
        {
            if (__0) { sawCombat = true; return; }
            if (!sawCombat) return;
            sawCombat = false;
            restorePending = true;
        }
        internal static void ProcessPending()
        {
            if (!restorePending || Game.Instance.Player.Party.Any(unit => unit.IsInCombat)) return;
            // Run after the combat event and every mod's postfix has completed. Coalesce
            // duplicate notifications in the same update, then consult the live setting.
            restorePending = false;
            if (!ToyBoxRestoresAfterCombat()) return;
            foreach (var unit in Game.Instance.Player.Party)
            {
                var pet = unit.Get<UnitPartSynthesist>()?.Eidolon;
                if (pet == null || pet.IsInGame) continue;
                // Only ability resources: never call Rest(), heal HP or change the recovery day.
                pet.Descriptor.Resources.FullRestoreAll();
            }
        }
        private static bool ToyBoxRestoresAfterCombat()
        {
            var main = AccessTools.TypeByName("ToyBox.Main");
            if (main == null || AccessTools.Field(main, "Enabled")?.GetValue(null) is not true) return false;
            var settings = AccessTools.Field(main, "Settings")?.GetValue(null);
            return settings != null && AccessTools.Field(settings.GetType(), "toggleRestoreSpellsAbilitiesAfterCombat")?.GetValue(settings) is true;
        }
    }
}
