using Kingmaker;
using Kingmaker.Blueprints;
using Kingmaker.Blueprints.JsonSystem;
using Kingmaker.EntitySystem.Entities;
using Kingmaker.UnitLogic.Mechanics;
using Kingmaker.UnitLogic.Mechanics.Actions;
using Kingmaker.Utility;
using UnityEngine;
using UnityEngine.Serialization;
using WOTR_MAKING_FRIENDS.Enums;

namespace WOTR_MAKING_FRIENDS.ComponentsNew
{
    [TypeId("719dcd5a9c0849f88cd9910cf0a98f0e")]
    public class ContextRestoreResourceOnPets : ContextAction
    {
        public bool m_IsFullRestoreAllResources;

        [HideIf("m_IsFullRestoreAllResources")]
        [SerializeField]
        [FormerlySerializedAs("Resource")]
        public BlueprintAbilityResourceReference m_Resource;

        [HideIf("m_IsFullRestoreAllResources")]
        public bool ContextValueRestoration;

        [ShowIf("ContextValueRestoration")]
        public ContextValue Value;

        public BlueprintAbilityResource Resource => m_Resource?.Get();

        public override string GetCaption()
        {
            return "Restore resource";
        }

        public override void RunAction()
        {
            UnitEntityData unit = base.Target.Unit;
            if (unit == null)
            {
                PFLog.Default.Error("Target is missing");
                return;
            }
            foreach (var pet in unit.Pets)
            {
                if (pet.EntityPart?.Type != PetTypeExtensions.Eidolon || pet.Entity == null || Resource == null)
                    continue;

                // Even legacy actions requesting a full restore must only touch the selected resource.
                if (!ContextValueRestoration)
                {
                    pet.Entity.Descriptor.Resources.Restore(Resource);
                }
                else
                {
                    pet.Entity.Descriptor.Resources.Restore(Resource, Value.Calculate(base.Context));
                }
            }
        }
    }
}
