using BlueprintCore.Actions.Builder;
using BlueprintCore.Blueprints.CustomConfigurators.UnitLogic.Abilities;
using BlueprintCore.Blueprints.References;
using BlueprintCore.Utils;
using Kingmaker.ElementsSystem;
using Kingmaker.Blueprints;
using Kingmaker.UnitLogic.Abilities.Blueprints;
using Kingmaker.UnitLogic.Abilities.Components;
using WOTR_MAKING_FRIENDS.Synthesist.Runtime;
using WOTR_MAKING_FRIENDS.Utilities;

namespace WOTR_MAKING_FRIENDS.Synthesist.Blueprints
{
    internal static class HealingIntegration
    {
        internal static void Create()
        {
            int[] dice = { 1, 3, 5 };
            int[] caps = { 5, 10, 20 };
            string[] names = { "Lesser Rejuvenate Eidolon", "Rejuvenate Eidolon", "Greater Rejuvenate Eidolon" };
            for (int i = 0; i < 3; i++)
            {
                string name = "SynthesistRejuvenate" + (i + 1) + "Spell";
                var spell = AbilityConfigurator.New(name, SynthesistBlueprints.Id(name))
                    .CopyFrom(AbilityRefs.CureLightWounds, c => false)
                    .SetDisplayName(SynthesistBlueprints.Text(name + ".name", names[i]))
                    .SetDescription(SynthesistBlueprints.Text(name + ".description", $"Restore {dice[i]}d10 + caster level (maximum {caps[i]}) eidolon HP. This does not heal personal HP or revive a slain eidolon."))
                    .SetRange(AbilityRange.Touch).SetCanTargetSelf(true).SetCanTargetFriends(true).SetCanTargetEnemies(false)
                    .SetSpellResistance(false)
                    .SetLocalizedSavingThrow(SynthesistBlueprints.Text("rejuvenate.save", "None"))
                    .AddComponent<RejuvenateTarget>()
                    .AddAbilityEffectRunAction(new ActionList
                    { Actions = new GameAction[] { new RejuvenateEidolon { DiceCount = dice[i], MaximumCasterLevel = caps[i] } } })
                    .AddToSpellList(i * 2 + 1, SynthesistBlueprints.Spellbook.SpellList)
                    .ConfigureWithLogging();
                spell.AddComponents(IsolatedCopy.Components(AbilityRefs.CureLightWounds.Reference.Get(), c => c is not AbilityEffectRunAction &&
                    c is not Kingmaker.Blueprints.Classes.Spells.SpellListComponent && c is not Kingmaker.UnitLogic.Mechanics.Components.ContextRankConfig &&
                    c.GetType().Name != "CraftInfoComponent"));
            }
        }
    }
}
