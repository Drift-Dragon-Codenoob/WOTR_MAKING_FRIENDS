using Kingmaker.Blueprints;
using Kingmaker.Blueprints.Facts;
using Kingmaker.Blueprints.JsonSystem;
using Kingmaker.UnitLogic;

namespace WOTR_MAKING_FRIENDS.ComponentsNew
{
    [TypeId("c14ee9ac16fb43ed8869b2791420d437")]
    [AllowedOn(typeof(BlueprintUnitFact), false)]
    [AllowMultipleComponents]
    public class LifeBond : UnitFactComponentDelegate
    {
        // Marker consumed by LifeTransferDamagePatch at HP-damage commitment.
        // No Infallible flag: death effects and ability damage retain engine behaviour.
    }
}
