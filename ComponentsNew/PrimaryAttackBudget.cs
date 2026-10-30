using Kingmaker.Blueprints;
using Kingmaker.Blueprints.JsonSystem;
using Kingmaker.EntitySystem.Entities;
using Kingmaker.PubSubSystem;
using Kingmaker.UnitLogic;
using BlueprintCore.Utils;
using WOTR_MAKING_FRIENDS.GUIDs;

namespace WOTR_MAKING_FRIENDS.ComponentsNew
{
    [TypeId("9e86d1c5b2474f048cc50e5e708bd029")]
    public class PrimaryAttackBudget : UnitFactComponentDelegate, IResourceAmountBonusHandler, IUnitSubscriber, ISubscriber
    {
        public BlueprintAbilityResourceReference m_Resource;

        internal static int ReservedCount(UnitEntityData unit)
        {
            foreach (string evolution in new[] { "EvolutionClaws", "EvolutionPincers", "EvolutionSlam", "EvolutionTentacleMass" })
                foreach (string suffix in new[] { "Feature", "BaseFeature" })
                    if (unit.Progression.Features.GetFact(BlueprintTool.GetRef<BlueprintFeatureReference>(GetGUID.GUIDByName(evolution + suffix)))?.Active == true)
                        return 2;
            return 0;
        }

        public void CalculateMaxResourceAmount(BlueprintAbilityResource resource, ref int bonus)
        {
            if (Fact.Active && resource == m_Resource?.Get())
                bonus -= ReservedCount(Owner);
        }
    }
}
