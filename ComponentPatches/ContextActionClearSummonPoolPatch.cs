using HarmonyLib;
using Kingmaker;
using Kingmaker.AreaLogic.SummonPool;
using Kingmaker.Blueprints.Root;
using Kingmaker.EntitySystem.Entities;
using Kingmaker.UnitLogic;
using Kingmaker.UnitLogic.Buffs;
using Kingmaker.UnitLogic.Mechanics.Actions;
using System;
using System.Linq;
using WOTR_MAKING_FRIENDS.GUIDs;

namespace WOTR_MAKING_FRIENDS.ComponentAndPatches
{
    [HarmonyPatch(typeof(ContextActionClearSummonPool), "RunAction")]
    public class ContextActionClearSummonPoolPatch
    {
        private static bool Prefix(ContextActionClearSummonPool __instance)
        {
            var blueprint = __instance.SummonPool;
            if (blueprint == null)
                return true;
            var id = blueprint.AssetGuid.m_Guid;
            if (id != Guid.Parse(GetGUID.GUIDByName("SummonerPool")) &&
                id != Guid.Parse(GetGUID.GUIDByName("DraconicAllyPool")))
                return true;

            var caster = __instance.AbilityContext?.MaybeCaster;
            if (caster == null)
                return true;

            ISummonPool pool = Game.Instance.SummonPools.GetPool(blueprint);
            if (pool == null)
            {
                return false;
            }

            foreach (UnitEntityData unit in pool.Units.ToArray())
            {
                var summon = unit?.Descriptor.GetFact(BlueprintRoot.Instance.SystemMechanics.SummonedUnitBuff) as Buff;
                if (summon?.Context?.MaybeCaster == caster)
                    unit.Descriptor.RemoveFact(BlueprintRoot.Instance.SystemMechanics.SummonedUnitBuff);
            }

            return false; // Skip the original method.
        }
    }
}
