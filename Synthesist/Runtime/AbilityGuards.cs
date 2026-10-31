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
            foreach (var action in list.Actions ?? Array.Empty<GameAction>())
            {
                if (!visited.Add(action)) continue;
                if (action is ContextActionApplyBuff apply && apply.m_Buff.Get()?.GetComponent<Polymorph>() != null) return true;
                foreach (var field in action.GetType().GetFields(BindingFlags.Instance | BindingFlags.Public))
                    if (field.GetValue(action) is ActionList nested && ContainsTransformation(nested, visited)) return true;
            }
            return false;
        }

        internal static string Reason(AbilityData ability)
        {
            var caster = ability.Caster.Unit;
            var part = caster.Get<UnitPartSynthesist>();
            if (part == null)
            {
                part = caster.Master?.Get<UnitPartSynthesist>();
                if (part?.IsPreparing == true && part.Eidolon == caster &&
                    !ability.Blueprint.name.StartsWith("SynthesistPurchase", StringComparison.Ordinal) &&
                    !ability.Blueprint.name.StartsWith("SynthesistEvolution", StringComparison.Ordinal))
                    return "Preparation cannot provide an independent combatant or combat abilities.";
                return null;
            }
            if (part.State == null) return null;
            foreach (string key in SynthesistBlueprints.EvolutionAbilities.Keys)
                if (ability.Blueprint.name == key + "Ability") return "Use the Synthesist preparation controls to allocate evolutions.";
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
            if (!__result || !AbilityGuards.IsTransformation(__instance.Blueprint)) return;
            var part = __0?.Unit?.Get<UnitPartSynthesist>();
            if (part != null && (part.IsFused || part.IsSplit || part.TwinActive)) __result = false;
        }
    }
}
