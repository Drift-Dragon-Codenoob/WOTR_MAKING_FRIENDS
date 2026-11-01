using System;
using System.Linq;
using HarmonyLib;
using Kingmaker.Blueprints;
using Kingmaker.Blueprints.JsonSystem;
using Kingmaker.EntitySystem.Entities;
using Kingmaker.EntitySystem.Stats;
using Kingmaker.PubSubSystem;
using Kingmaker.UnitLogic;
using Kingmaker.UnitLogic.Mechanics.Actions;
using Newtonsoft.Json;
using WOTR_MAKING_FRIENDS.Synthesist.Blueprints;
using WOTR_MAKING_FRIENDS.Synthesist.Rules;

namespace WOTR_MAKING_FRIENDS.Synthesist.Runtime
{
    [TypeId("d7493d99a1ca4240a4aa1363a95b09c3")]
    public sealed class UnitPartSummonerFeats : UnitPart
    {
        [JsonProperty] internal StatType CallPreference = StatType.Strength;
        [JsonProperty] internal RendingClawsRound Rending = new();
        [JsonProperty] internal long CallExpiresAt;
        [JsonIgnore] internal readonly System.Collections.Generic.HashSet<Kingmaker.RuleSystem.Rules.RuleAttackRoll> SeenAttacks = new();
    }

    [TypeId("a8dba518a6684630ab216279637ca602")]
    public sealed class ExtraEvolutionPoolBonus : UnitFactComponentDelegate, IResourceAmountBonusHandler, IUnitSubscriber, ISubscriber
    {
        public void CalculateMaxResourceAmount(BlueprintAbilityResource resource, ref int bonus)
        {
            if (resource == BlueprintCore.Utils.BlueprintTool.Get<BlueprintAbilityResource>(SynthesistBlueprints.Id("SummonerEvolutionPointsResource")) && Owner.Master != null)
                bonus += SummonerFeats.ExtraPoints(Owner.Master);
        }
    }

    [TypeId("3a48734685a14f6b859ecff967010fb7")]
    public sealed class ChooseSummonersCall : ContextAction
    {
        public StatType Stat;
        public override string GetCaption() => "Choose the next Summoner's Call bonus";
        public override void RunAction()
        {
            var unit = Context.MaybeCaster;
            if (unit == null || SummonerFeats.CallFeature == null || !unit.Descriptor.HasFact(SummonerFeats.CallFeature)) return;
            unit.Ensure<UnitPartSummonerFeats>().CallPreference = Stat;
        }
    }

    internal static class SummonersCallRuntime
    {
        internal static bool IsCall(Kingmaker.UnitLogic.Buffs.Buff buff) => SummonerFeats.CallBuffs.Values.Contains(buff.Blueprint);
        internal static void Summoned(UnitEntityData caster, UnitEntityData eidolon)
        {
            if (caster == null || eidolon == null || !caster.PreviewOf.IsEmpty ||
                SummonerFeats.CallFeature == null || !caster.Descriptor.HasFact(SummonerFeats.CallFeature)) return;
            foreach (var buff in Projection.Buffs(eidolon).Where(IsCall).ToArray()) buff.Remove();
            var state = caster.Ensure<UnitPartSummonerFeats>();
            state.CallExpiresAt = 0;
            var preference = state.CallPreference;
            if (SummonerFeats.CallBuffs.TryGetValue(preference, out var blueprint))
            {
                eidolon.Buffs.AddBuff(blueprint, caster.Descriptor, TimeSpan.FromMinutes(10));
                state.CallExpiresAt = checked(UnitPartSynthesist.Now + TimeSpan.TicksPerMinute * 10);
            }
        }
        internal static void Expire(UnitEntityData caster, UnitEntityData eidolon)
        {
            var state = caster.Get<UnitPartSummonerFeats>();
            if (state == null || state.CallExpiresAt == 0 || UnitPartSynthesist.Now < state.CallExpiresAt) return;
            // Dormant units may not receive the active-unit buff update. Use the
            // saved native-time deadline once, without scanning buffs each frame.
            foreach (var buff in Projection.Buffs(eidolon).Where(IsCall).ToArray()) buff.Remove();
            state.CallExpiresAt = 0;
        }
        internal static void Project(UnitPartSynthesist part)
        {
            foreach (var buff in Projection.Buffs(part.Eidolon).Where(IsCall).ToArray())
            {
                if (buff.TimeLeft <= TimeSpan.Zero) { buff.Remove(); continue; }
                part.ProjectionFacts.Add(part.Owner.Buffs.AddBuff(buff.Blueprint, part.Owner.Descriptor, buff.TimeLeft));
            }
        }
    }

    // The base mod spawns its ordinary eidolon through AddEidolon rather than
    // RuleSummonUnit. An existing pet being reconciled on load is not a summoning.
    [HarmonyPatch(typeof(ComponentsNew.AddEidolon), "TryUpdatePet")]
    internal static class OrdinarySummonersCall
    {
        private static void Prefix(ComponentsNew.AddEidolon __instance, out UnitEntityData __state) => __state = __instance.Data.SpawnedPetRef.Value;
        private static void Postfix(ComponentsNew.AddEidolon __instance, UnitEntityData __state)
        {
            var pet = __instance.Data.SpawnedPetRef.Value;
            if (__state == null && pet != null && __instance.Type == Enums.PetTypeExtensions.Eidolon)
                SummonersCallRuntime.Summoned(__instance.Owner, pet);
        }
    }
}
