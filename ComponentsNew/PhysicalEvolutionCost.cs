using Kingmaker.Blueprints;
using Kingmaker.Blueprints.JsonSystem;
using Kingmaker.EntitySystem.Entities;
using Kingmaker.Enums;
using Kingmaker.UnitLogic.Abilities.Components.Base;
using Kingmaker.UnitLogic.Mechanics.Conditions;
using BlueprintCore.Utils;
using WOTR_MAKING_FRIENDS.GUIDs;

namespace WOTR_MAKING_FRIENDS.ComponentsNew
{
    internal static class PhysicalEvolutionCost
    {
        internal static bool IsDoubled(UnitEntityData unit)
            => unit != null && unit.State.Size >= Size.Large;
    }

    [TypeId("04d6905419974b168b5dccd5873b5806")]
    public class ContextConditionLargePhysicalEvolution : ContextCondition
    {
        public override bool CheckCondition() => PhysicalEvolutionCost.IsDoubled(Target.Unit);
        public override string GetConditionCaption() => "Target is Large or larger.";
    }

    [TypeId("1d780cfc456149ae9c73e5e1c170359e")]
    public class AbilityGrowthSizeRestriction : BlueprintComponent, IAbilityCasterRestriction
    {
        public bool IsCasterRestrictionPassed(UnitEntityData caster)
        {
            if (caster == null) return false;
            int rank = caster.Progression.Features.GetRank(BlueprintTool.GetRef<BlueprintFeatureReference>(GetGUID.GUIDByName("EvolutionLargeFeature")));
            if (rank >= 2) return false;
            return caster.State.Size == (rank == 0 ? Size.Medium : Size.Large);
        }
        public string GetAbilityCasterRestrictionUIText() => "Growth requires Medium size; its second purchase requires Large size.";
    }
}
