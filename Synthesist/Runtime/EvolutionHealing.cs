using System;
using BlueprintCore.Blueprints.CustomConfigurators.UnitLogic.Buffs;
using BlueprintCore.Blueprints.References;
using Kingmaker.UnitLogic.Buffs;
using WOTR_MAKING_FRIENDS.Synthesist.Blueprints;

namespace WOTR_MAKING_FRIENDS.Synthesist.Runtime
{
    internal static class EvolutionHealing
    {
        internal static void Create()
        {
            for (int rank = 1; rank <= 5; rank++)
            {
                int amount = rank;
                string name = "SynthesistFastHealing" + rank + "Buff";
                BuffConfigurator.New(name, SynthesistBlueprints.Id(name))
                    .CopyFrom(BuffRefs.FastHealing1, c => false)
                    .SetDisplayName(SynthesistBlueprints.Text(name + ".name", "Fast healing " + rank))
                    .SetDescription(SynthesistBlueprints.Text(name + ".description", "Restore personal hit points each round. This does not restore fused eidolon temporary HP."))
                    .AddComponent<Kingmaker.UnitLogic.Buffs.Components.AddEffectFastHealing>(c =>
                    { c.Heal = amount; c.Bonus = new Kingmaker.UnitLogic.Mechanics.ContextValue(); })
                    .Configure();
            }
        }

        internal static void Refresh(UnitPartSynthesist part)
        {
            int eidolon = EvolutionStatistics.Rank(part.Eidolon, "EvolutionFastHealing");
            int personal = EvolutionStatistics.Rank(part.Owner, "EvolutionFastHealing");
            if (part.IsFused || part.IsSplit || part.TwinActive) personal = Math.Max(personal, eidolon);
            Update(part.Owner, personal, ref part.PersonalEvolutionHealing);
            Update(part.Eidolon, part.IsSplit ? eidolon : 0, ref part.PetEvolutionHealing);
        }

        private static void Update(Kingmaker.EntitySystem.Entities.UnitEntityData unit, int rank, ref Buff owned)
        {
            var blueprint = rank == 0 ? null : SynthesistBlueprints.Buff("SynthesistFastHealing" + Math.Min(5, rank) + "Buff");
            if (owned != null && owned.Blueprint == blueprint && owned.IsActive) return;
            owned?.Remove(); owned = null;
            if (blueprint != null) owned = unit.Buffs.AddBuff(blueprint, unit.Descriptor, null);
        }
    }
}
