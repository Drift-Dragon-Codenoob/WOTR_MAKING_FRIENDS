// These intentionally small stand-ins exercise the linked production components without Unity.
// Production compilation against the installed assemblies is checked separately.
#pragma warning disable CS9113 // Attribute constructor parameters are deliberately inert stand-ins.
using System.Collections;

namespace Kingmaker.Blueprints
{
    public class BlueprintComponent { }
    public class BlueprintWithComponents
    {
        public BlueprintComponent Component;
        public T GetComponent<T>() where T : BlueprintComponent => Component as T;
    }
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
    public class AllowedOnAttribute(Type type, bool value) : Attribute { }
    public class AllowMultipleComponentsAttribute : Attribute { }
    public class BlueprintUnitFactReference { public string Id; }
    public class BlueprintFeatureReference : BlueprintUnitFactReference { }
    public class BlueprintAbilityResource { public string Id; }
    public class BlueprintAbilityResourceReference
    {
        public BlueprintAbilityResource Resource;
        public BlueprintAbilityResource Get() => Resource;
    }
}
namespace Kingmaker.Blueprints.JsonSystem { public class TypeIdAttribute(string id) : Attribute { } }
namespace Kingmaker.Blueprints.Classes
{
    public enum FeatureGroup { Base, Purchased }
    public class BlueprintFeature
    {
        public string Id;
        public FeatureGroup Group;
        public bool HasGroup(FeatureGroup group) => Group == group;
        public string name => Id;
    }
}
namespace Kingmaker.UnitLogic
{
    using Kingmaker.Blueprints;
    using Kingmaker.Blueprints.Classes;
    public class UnitFactComponentDelegate : Kingmaker.Blueprints.BlueprintComponent
    {
        public Kingmaker.EntitySystem.Entities.UnitEntityData Owner;
        public Feature Fact = new Feature { Active = true, Rank = 1 };
    }
    public class Feature
    {
        public BlueprintFeature Blueprint;
        public bool Active = true;
        public int Rank = 1;
        public int GetRank() => Rank;
    }
    public class FeatureCollection : IEnumerable<Feature>
    {
        private readonly List<Feature> facts = [];
        private static string Id(object obj) => obj switch
        {
            BlueprintUnitFactReference r => r.Id,
            BlueprintFeature f => f.Id,
            Feature f => f.Blueprint.Id,
            _ => throw new ArgumentException()
        };
        public bool HasFact(object obj) => facts.Any(f => f.Blueprint.Id == Id(obj));
        public Feature GetFact(object obj) => facts.FirstOrDefault(f => f.Blueprint.Id == Id(obj));
        public int GetRank(object obj) => GetFact(obj)?.Rank ?? 0;
        public Feature AddFeature(BlueprintUnitFactReference reference)
        {
            var feature = new Feature { Blueprint = new BlueprintFeature { Id = reference.Id, Group = FeatureGroup.Base } };
            facts.Add(feature);
            return feature;
        }
        public Feature Add(string id, FeatureGroup group = FeatureGroup.Base, int rank = 1)
        {
            var feature = new Feature { Blueprint = new BlueprintFeature { Id = id, Group = group }, Rank = rank };
            facts.Add(feature);
            return feature;
        }
        public void RemoveFact(object fact) => facts.Remove(GetFact(fact));
        public IEnumerator<Feature> GetEnumerator() => facts.GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
namespace Kingmaker.EntitySystem.Entities
{
    using Kingmaker.Blueprints;
    public class UnitEntityData
    {
        public ProgressionData Progression = new();
        public DescriptorData Descriptor;
        public UnitEntityData() { Descriptor = new DescriptorData { Progression = Progression }; }
        public bool IsPet;
        public bool IsReallyInFactPet;
        public List<Pet> Pets = [];
        public List<Kingmaker.UnitLogic.Buffs.Buff> Buffs = [];
        public StateData State = new();
        public StatsData Stats = new();
        public UnitEntityData Master;
        public int MaxHP = 100;
        public int Damage;
        public int HPLeft => MaxHP - Damage;
        public Kingmaker.RuleSystem.Rules.Damage.RuleDealDamage LastHandledDamage;
    }
    public class StateData { public bool IsDead; public Kingmaker.Enums.Size Size = Kingmaker.Enums.Size.Medium; }
    public class StatsData { public StatData TemporaryHitPoints = new(); }
    public class StatData { public int ModifiedValue; }
    public class ProgressionData { public int CharacterLevel; public Kingmaker.UnitLogic.FeatureCollection Features = new(); }
    public class DescriptorData { public ProgressionData Progression; public ResourceCollection Resources = new(); }
    public class ResourceCollection
    {
        public Dictionary<string, int> Amounts = [];
        public void Restore(BlueprintAbilityResource resource) => Amounts[resource.Id] = 20;
        public void Restore(BlueprintAbilityResource resource, int amount) => Amounts[resource.Id] = Amounts.GetValueOrDefault(resource.Id) + amount;
        public bool HasEnoughResource(BlueprintAbilityResourceReference resource, int required) => Amounts.GetValueOrDefault(resource.Get().Id) >= required;
    }
    public class Pet : Kingmaker.EntitySystem.EntityPartRef<UnitEntityData, Kingmaker.UnitLogic.Parts.UnitPartPet> { }
    public class PetPart : Kingmaker.UnitLogic.Parts.UnitPartPet { }
}
namespace Kingmaker.EntitySystem { public class EntityPartRef<TEntity, TPart> where TEntity : new() where TPart : new() { public TPart EntityPart = new(); public TEntity Entity = new(); } }
namespace Kingmaker.Enums { public enum Size { Fine, Diminutive, Tiny, Small, Medium, Large, Huge, Gargantuan, Colossal } }
namespace Kingmaker.UnitLogic.Mechanics.Conditions
{
    public abstract class ContextCondition
    {
        public Kingmaker.UnitLogic.Mechanics.Actions.TargetData Target = new();
        public abstract bool CheckCondition();
        public abstract string GetConditionCaption();
    }
}
namespace Kingmaker.UnitLogic.Abilities.Blueprints { public class BlueprintAbility { } }
namespace Kingmaker.UnitLogic.Abilities.Components.Base { public interface IAbilityCasterRestriction { } }
namespace Kingmaker.UnitLogic.Mechanics.Actions
{
    public abstract class ContextAction
    {
        public TargetData Target = new();
        public AbilityData AbilityContext = new();
        public object Context;
        public abstract string GetCaption();
        public abstract void RunAction();
    }
    public class TargetData { public Kingmaker.EntitySystem.Entities.UnitEntityData Unit; }
    public class AbilityData { public Kingmaker.EntitySystem.Entities.UnitEntityData Caster; }
}
namespace Kingmaker.UnitLogic.Mechanics { public class ContextValue { public int Value; public int Calculate(object context) => Value; } }
namespace Kingmaker.PubSubSystem
{
    public interface IResourceAmountBonusHandler { }
    public interface IUnitSubscriber { }
    public interface ISubscriber { }
}
namespace Kingmaker { public static class PFLog { public static Log Default = new(); } public class Log { public void Error(params object[] values) { } } }
namespace Kingmaker.ElementsSystem { public class UnitEvaluator { } }
namespace Kingmaker.Utility { }
namespace Kingmaker.UnitLogic.Parts { public class UnitPartPet { public int Type; } }
namespace Owlcat.QA.Validation { public class ValidateNotNullAttribute : Attribute { } }
namespace UnityEngine
{
    public class SerializeField : Attribute { }
    public class SerializeReference : Attribute { }
    public class HideIfAttribute(string field) : Attribute { }
    public class ShowIfAttribute(string field) : Attribute { }
    public class TooltipAttribute(string text) : Attribute { }
}
namespace UnityEngine.Serialization { public class FormerlySerializedAsAttribute(string name) : Attribute { } }
namespace BlueprintCore.Utils
{
    public static class BlueprintTool
    {
        public static T GetRef<T>(string id) where T : Kingmaker.Blueprints.BlueprintUnitFactReference, new() => new T { Id = id };
    }
}
namespace WOTR_MAKING_FRIENDS.GUIDs { public static class GetGUID { public static string GUIDByName(string name) => name; } }
namespace WOTR_MAKING_FRIENDS.Enums { public static class PetTypeExtensions { public const int Eidolon = 604767; } }
namespace WOTR_MAKING_FRIENDS { public static class Main { public static void Log(object value) { } } }
namespace Kingmaker.Blueprints.Facts { public class BlueprintUnitFact { } }
namespace Kingmaker.UnitLogic.Buffs
{
    public class Buff
    {
        public bool Active = true;
        public Kingmaker.Blueprints.BlueprintWithComponents Blueprint = new();
    }
}
namespace Kingmaker.RuleSystem
{
    public static class Rulebook
    {
        public static RuleContext CurrentContext = new();
        public static void Trigger(Kingmaker.RuleSystem.Rules.Damage.RuleDealDamage rule)
            => DamageHarness.Apply(rule.Target, rule.Amount, rule.Initiator, rule: rule);
    }
    public struct DiceFormula { public static DiceFormula Zero; }
    public class RuleContext { public object CurrentEvent; }
}
namespace Kingmaker.RuleSystem.Rules.Damage
{
    public class RuleDealDamage
    {
        public RuleDealDamage() { }
        public RuleDealDamage(Kingmaker.EntitySystem.Entities.UnitEntityData initiator, Kingmaker.EntitySystem.Entities.UnitEntityData target, DirectDamage damage)
        { Initiator = initiator; Target = target; Amount = damage.Amount; }
        public int Amount;
        public float DifficultyModifier;
        public Kingmaker.EntitySystem.Entities.UnitEntityData Initiator;
        public Kingmaker.EntitySystem.Entities.UnitEntityData Target;
        public bool IsFake;
    }
    public class DirectDamage(Kingmaker.RuleSystem.DiceFormula dice, int amount) { public int Amount = amount; }
}
namespace HarmonyLib
{
    public enum MethodType { Setter }
    public class HarmonyPatch(Type type, string name, MethodType method = MethodType.Setter) : Attribute { }
}
namespace Kingmaker.Designers
{
    public static class GameHelper
    {
        public static void DealDirectDamage(Kingmaker.EntitySystem.Entities.UnitEntityData initiator, Kingmaker.EntitySystem.Entities.UnitEntityData target, int amount)
            => DamageHarness.Apply(target, amount, initiator);
    }
}
public static class DamageHarness
{
    public static float Difficulty = 1f;
    // Recreates only the observed engine contract: final damage, consume temp HP, then commit HP damage.
    // It does not recreate Unity's combat, life-state controllers or other mods' patches.
    public static void Apply(Kingmaker.EntitySystem.Entities.UnitEntityData target, int amount,
        Kingmaker.EntitySystem.Entities.UnitEntityData initiator = null, bool isFake = false,
        Kingmaker.RuleSystem.Rules.Damage.RuleDealDamage rule = null)
    {
        var previous = Kingmaker.RuleSystem.Rulebook.CurrentContext.CurrentEvent;
        rule ??= new Kingmaker.RuleSystem.Rules.Damage.RuleDealDamage { Target = target, Initiator = initiator, IsFake = isFake };
        object[] difficultyArgs = [rule, amount, 0];
        bool useDifficulty = (bool)typeof(WOTR_MAKING_FRIENDS.MechanicsPatches.LifeTransferDamagePatch)
            .GetNestedType("TransferDifficultyPatch", System.Reflection.BindingFlags.NonPublic)
            .GetMethod("Prefix", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic).Invoke(null, difficultyArgs);
        amount = useDifficulty ? Math.Max(1, (int)(amount * Difficulty)) : (int)difficultyArgs[2];
        Kingmaker.RuleSystem.Rulebook.CurrentContext.CurrentEvent = rule;
        target.LastHandledDamage = rule;
        int temporary = Math.Min(amount, target.Stats.TemporaryHitPoints.ModifiedValue);
        target.Stats.TemporaryHitPoints.ModifiedValue -= temporary;
        object[] args = [target, target.Damage + amount - temporary];
        typeof(WOTR_MAKING_FRIENDS.MechanicsPatches.LifeTransferDamagePatch)
            .GetMethod("Prefix", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic).Invoke(null, args);
        if (!isFake) target.Damage = (int)args[1];
        Kingmaker.RuleSystem.Rulebook.CurrentContext.CurrentEvent = previous;
    }
}
