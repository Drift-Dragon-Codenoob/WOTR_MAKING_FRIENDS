namespace WOTR_MAKING_FRIENDS.Synthesist.Rules
{
    internal enum DailyAbility { MakersJump, SplitForms, TwinEidolon }

    // The runtime implementation delegates to UnitAbilityResourceCollection. Resources are
    // saved and restored by the engine (including ToyBox); the rules state owns no shadow pool.
    internal interface IAbilityBudget
    {
        bool TrySpend(DailyAbility ability, int amount);
    }
}
