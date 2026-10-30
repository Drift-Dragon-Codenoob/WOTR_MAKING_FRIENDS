using Kingmaker.Blueprints;
using Kingmaker.Blueprints.Classes;
using Kingmaker.Blueprints.JsonSystem;
using Kingmaker.EntitySystem.Entities;
using Kingmaker.EntitySystem.Stats;
using Kingmaker.Enums;
using Kingmaker.PubSubSystem;
using Kingmaker.UnitLogic;
using Kingmaker.UnitLogic.Buffs.Blueprints;
using Kingmaker.UnitLogic.Class.LevelUp;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using WOTR_MAKING_FRIENDS.Enums;

namespace WOTR_MAKING_FRIENDS.ComponentsNew
{
    public class BondedSensesData
    {
        [JsonIgnore] public bool Updating;
        [JsonIgnore] public HashSet<ModifiableValue> Watched = new HashSet<ModifiableValue>();
    }

    [TypeId("88b2e594e9024a8a8618d5d9e8ab81fb")]
    [AllowedOn(typeof(BlueprintFeature), false)]
    [AllowedOn(typeof(BlueprintBuff), false)]
    [AllowMultipleComponents]
    public class BondedSensesComponent : UnitFactComponentDelegate<BondedSensesData>,
        IAreaHandler, IUnitLevelUpHandler, IUnitSpawnHandler, IGlobalSubscriber, ISubscriber
    {
        public ModifierDescriptor Descriptor = ModifierDescriptor.UntypedStackable;
        public StatType Stat = StatType.SkillPerception;

        public override void OnTurnOn() => Refresh();
        public override void OnRecalculate() => Refresh();

        private void Refresh()
        {
            if (Data.Updating || !Fact.Active)
                return;
            Data.Updating = true;
            try
            {
                var own = Owner.Stats.GetStat(Stat);
                var pets = Owner.Pets.Where(p => p.EntityPart?.Type == PetTypeExtensions.Eidolon && p.Entity != null)
                    .Select(p => p.Entity.Stats.GetStat(Stat)).ToArray();
                var watched = new HashSet<ModifiableValue>(pets) { own };
                foreach (var stat in Data.Watched.Except(watched).ToArray())
                    stat.RemoveDependentComponent(Runtime);
                foreach (var stat in watched.Except(Data.Watched).ToArray())
                    stat.AddDependentComponent(Runtime);
                Data.Watched = watched;

                own.RemoveModifiersFrom(Runtime);
                int best = pets.Select(p => p.ModifiedValue).DefaultIfEmpty(own.ModifiedValue).Max();
                int difference = Math.Max(0, best - own.ModifiedValue);
                if (difference > 0)
                    own.AddModifier(difference, Runtime, Descriptor);
            }
            finally { Data.Updating = false; }
        }

        public override void OnTurnOff()
        {
            Data.Updating = true;
            try
            {
                foreach (var stat in Data.Watched)
                    stat.RemoveDependentComponent(Runtime);
                Data.Watched.Clear();
                Owner.Stats.GetStat(Stat).RemoveModifiersFrom(Runtime);
            }
            finally { Data.Updating = false; }
        }

        public void OnAreaDidLoad() => Refresh();
        public void OnAreaBeginUnloading() { }
        public void HandleUnitBeforeLevelUp(UnitEntityData unit) { }
        public void HandleUnitAfterLevelUp(UnitEntityData unit, LevelUpController controller)
        {
            if (unit == Owner || unit.Master == Owner) Refresh();
        }
        public void HandleUnitSpawned(UnitEntityData unit)
        {
            if (unit.Master == Owner) Refresh();
        }
        public void HandleUnitDestroyed(UnitEntityData unit) => Refresh();
        public void HandleUnitDeath(UnitEntityData unit) { }
    }
}
