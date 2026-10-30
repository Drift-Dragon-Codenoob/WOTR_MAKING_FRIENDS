using Kingmaker.Blueprints;
using Kingmaker.Blueprints.Facts;
using Kingmaker.Blueprints.JsonSystem;
using Kingmaker.UnitLogic;
using UnityEngine;

namespace WOTR_MAKING_FRIENDS.ComponentsNew
{
    [TypeId("082d1e0ef881457699f7df1870a451b6")]
    [AllowedOn(typeof(BlueprintUnitFact), false)]
    [AllowMultipleComponents]
    public class LifeLinkEidolon : UnitFactComponentDelegate
    {
        // Retained for blueprint compatibility. The baseline link protects at 1 HP.
        [Tooltip("Legacy field; Life Link protects at 1 HP.")]
        public int HealthPercent = 0;
    }
}
