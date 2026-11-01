using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using Kingmaker.Blueprints;
using Kingmaker.ElementsSystem;
using Kingmaker.EntitySystem.Entities;
using Kingmaker.UnitLogic.Abilities;
using Kingmaker.UnitLogic.Abilities.Blueprints;
using Kingmaker.UnitLogic.Abilities.Components;
using Kingmaker.UnitLogic.Buffs;
using Kingmaker.UnitLogic.Mechanics.Actions;
using Kingmaker.Utility;
using WOTR_MAKING_FRIENDS.Synthesist.Blueprints;

namespace WOTR_MAKING_FRIENDS.Synthesist.Runtime
{
    internal static class AbilityGuards
    {
        private static readonly Dictionary<BlueprintAbility, bool> transformations = new();
        internal static bool IsTransformation(BlueprintAbility ability)
        {
            if (ability == null) return false;
            if (transformations.TryGetValue(ability, out bool result)) return result;
            var effects = ability.GetComponent<AbilityEffectRunAction>();
            result = effects != null && ContainsTransformation(effects.Actions, new HashSet<GameAction>());
            transformations[ability] = result;
            return result;
        }

        private static bool ContainsTransformation(ActionList list, HashSet<GameAction> visited)
        {
            foreach (var action in list?.Actions ?? Array.Empty<GameAction>())
            {
                if (action == null || !visited.Add(action)) continue;
                if (action is ContextActionApplyBuff apply && apply.m_Buff?.Get()?.GetComponent<Polymorph>() != null) return true;
                foreach (var field in action.GetType().GetFields(BindingFlags.Instance | BindingFlags.Public))
                    if (field.GetValue(action) is ActionList nested && ContainsTransformation(nested, visited)) return true;
            }
            return false;
        }

        internal static string Reason(AbilityData ability)
        {
#if SYNTHESIST_ACCEPTANCE
            using var timing = RuntimePerformance.Measure("AbilityGuards.Reason");
#endif
            var caster = ability?.Caster?.Unit;
            if (caster == null || !SynthesistBlueprints.Ready) return null;
            var part = caster.Get<UnitPartSynthesist>();
            if (part == null)
            {
                part = caster.Master?.Get<UnitPartSynthesist>();
                if (part?.IsPreparing == true && part.Eidolon == caster &&
                    !ability.Blueprint.name.StartsWith("SynthesistPurchase", StringComparison.Ordinal) &&
                    !ability.Blueprint.name.StartsWith("SynthesistEvolution", StringComparison.Ordinal))
                    return "Preparation cannot provide an independent combatant or combat abilities.";
                if (part?.IsSplit == true && part.Eidolon == caster && IsTransformation(ability.Blueprint))
                    return "Dismiss the eidolon form before another transformation.";
                return null;
            }
            if (part.State == null) return null;
            // Combat AI and action bars query availability frequently. Comparing the
            // registered blueprints avoids creating one string per evolution per query.
            if (SynthesistBlueprints.LegacyEvolutionAbilities.Contains(ability.Blueprint))
                return "Use the Synthesist preparation controls to allocate evolutions.";
            if ((part.IsFused || part.IsSplit) && ability.Blueprint.name.StartsWith("SummonerSummonMonster", StringComparison.Ordinal))
                return "Dismiss the eidolon before using Summon Monster.";
            if ((part.IsFused || part.IsSplit || part.TwinActive) && IsTransformation(ability.Blueprint))
                return "Dismiss the eidolon form before another transformation.";
            if ((part.IsFused || part.TwinActive) && !Projection.HasArms(part) && ability.Blueprint.IsSpell &&
                !Rules.PfSpellComponents.CanCastWithoutArms(ability.Blueprint.name))
                return "The eidolon needs appropriate limbs to cast a spell with somatic components.";
            return null;
        }
    }

    [HarmonyPatch(typeof(AbilityData), "get_CanBeCastByCaster")]
    internal static class FusionAbilityGuard
    {
        private static void Postfix(AbilityData __instance, ref bool __result)
        { if (__result && AbilityGuards.Reason(__instance) != null) __result = false; }
    }

    [HarmonyPatch(typeof(AbilityData), nameof(AbilityData.GetUnavailableReason))]
    internal static class FusionAbilityReason
    {
        private static void Postfix(AbilityData __instance, ref string __result)
        { string reason = AbilityGuards.Reason(__instance); if (reason != null) __result = reason; }
    }

    [HarmonyPatch(typeof(AbilityData), nameof(AbilityData.CanTarget))]
    internal static class FusionTransformationTargetGuard
    {
        private static void Postfix(AbilityData __instance, TargetWrapper __0, ref bool __result)
        {
#if SYNTHESIST_ACCEPTANCE
            using var timing = RuntimePerformance.Measure("TransformationTargetGuard");
#endif
            if (!__result) return;
            var target = __0?.Unit;
            var part = target?.Get<UnitPartSynthesist>();
            if (part == null)
            {
                var master = target?.Master?.Get<UnitPartSynthesist>();
                if (master?.Eidolon == target && master?.IsSplit == true) part = master;
            }
            if (part == null || (!part.IsFused && !part.IsSplit && !part.TwinActive)) return;
            if (AbilityGuards.IsTransformation(__instance.Blueprint)) __result = false;
        }
    }
}
