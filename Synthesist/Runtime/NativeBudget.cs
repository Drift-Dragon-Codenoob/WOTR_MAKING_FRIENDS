using Kingmaker.Blueprints;
using Kingmaker.EntitySystem.Entities;
using WOTR_MAKING_FRIENDS.Synthesist.Blueprints;
using WOTR_MAKING_FRIENDS.Synthesist.Rules;

namespace WOTR_MAKING_FRIENDS.Synthesist.Runtime
{
    internal sealed class NativeBudget : IAbilityBudget
    {
        private readonly UnitEntityData owner;
        public NativeBudget(UnitEntityData owner) { this.owner = owner; }

        internal static BlueprintAbilityResource Resource(DailyAbility ability)
            => SynthesistBlueprints.Resource(ability);

        public bool TrySpend(DailyAbility ability, int amount)
        {
            if (amount < 1) return false;
            var resource = Resource(ability);
            if (owner.Descriptor.Resources.GetResourceAmount(resource) < amount) return false;
            owner.Descriptor.Resources.Spend(resource, amount);
            return true;
        }
    }
}
