using System;
using System.Collections.Generic;
using BlueprintCore.Blueprints.CustomConfigurators.UnitLogic.Buffs;
using BlueprintCore.Blueprints.CustomConfigurators.UnitLogic.Abilities;
using BlueprintCore.Utils;
using Kingmaker.Blueprints;
using Kingmaker.UnitLogic.Buffs.Blueprints;
using Kingmaker.UnitLogic;
using Kingmaker.Blueprints.JsonSystem;
using Kingmaker.ElementsSystem;
using Kingmaker.EntitySystem.Entities;
using Kingmaker.UnitLogic.Abilities.Components.Base;
using Kingmaker.UnitLogic.Mechanics.Actions;
using System.Linq;
using WOTR_MAKING_FRIENDS.Synthesist.Rules;
using WOTR_MAKING_FRIENDS.Synthesist.Runtime;
using WOTR_MAKING_FRIENDS.Utilities;

namespace WOTR_MAKING_FRIENDS.Synthesist.Blueprints
{
    internal sealed class EidolonAppearance
    {
        internal readonly string Key;
        internal readonly string Name;
        internal readonly EidolonForm Category;
        internal EidolonAppearance(EidolonAppearanceDefinition definition)
        { Key = definition.Key; Name = definition.Name; Category = definition.Category; }
        internal BlueprintUnit Source => Key switch
        {
            "Nabasu" => BlueprintCore.Blueprints.References.UnitRefs.CR12_NabasuAdvancedSummoned.Reference.Get(),
            "Gargoyle" => BlueprintCore.Blueprints.References.UnitRefs.CR4_GargoyleStandard.Reference.Get(),
            _ => BlueprintCore.Utils.BlueprintTool.Get<BlueprintUnit>(SynthesistBlueprints.Id("Eidolon" + Key))
        };
        internal string BuffName => "SynthesistFusionAppearance" + Key + "Buff";
    }

    internal static class AppearanceIntegration
    {
        // These are visual templates only, never subtype/progression packages.
        // Animation acceptance belongs to the player test checklist.
        internal static readonly EidolonAppearance[] Models = EidolonAppearances.All.Select(d => new EidolonAppearance(d)).ToArray();
        internal static EidolonAppearance Find(string key) => Array.Find(Models, m => m.Key == key);
        internal static bool Compatible(CharacterBuild build, EidolonAppearance model)
            => build != null && model != null;
        internal static bool Compatible(UnitPartSynthesist part)
        {
            var model = Find(part.Owner.Get<UnitPartSynthesistBuild>()?.SelectedAppearance);
            return part.Owner.Get<UnitPartSynthesistBuild>()?.SelectedAppearance == null || model != null;
        }
        internal static void Create()
        {
            foreach (var model in Models)
            {
                var buff = BuffConfigurator.New(model.BuffName, SynthesistBlueprints.Id(model.BuffName))
                    .CopyFrom(SynthesistBlueprints.Buff("SynthesistFusionBipedBuff"), c => false)
                    .ConfigureWithLogging();
                buff.AddComponents(IsolatedCopy.Components(SynthesistBlueprints.Buff("SynthesistFusionBipedBuff"), c => true));
                var polymorph = buff.GetComponent<Kingmaker.UnitLogic.Buffs.Polymorph>();
                if (model.Source?.Prefab == null || string.IsNullOrEmpty(model.Source.Prefab.AssetId))
                    throw new InvalidOperationException("Missing eidolon appearance prefab: " + model.Key);
                polymorph.m_Prefab = model.Source.Prefab;
                polymorph.m_PrefabFemale = model.Source.Prefab;
                string abilityName = "SynthesistChooseAppearance" + model.Key + "Ability";
                AbilityConfigurator.New(abilityName, SynthesistBlueprints.Id(abilityName))
                    .SetDisplayName(SynthesistBlueprints.Text(abilityName + ".name", model.Name))
                    .SetDescription(SynthesistBlueprints.Text(abilityName + ".description", Description(model)))
                    .SetType(Kingmaker.UnitLogic.Abilities.Blueprints.AbilityType.Special)
                    .SetRange(Kingmaker.UnitLogic.Abilities.Blueprints.AbilityRange.Personal)
                    .SetCanTargetSelf(true)
                    .SetActionType(Kingmaker.UnitLogic.Commands.Base.UnitCommand.CommandType.Free)
                    .AddComponent<AppearanceRestriction>(c => c.Model = model.Key)
                    .AddAbilityEffectRunAction(new ActionList { Actions = new GameAction[] { new ChooseAppearance { Model = model.Key } } })
                    .ConfigureWithLogging();
            }
            var groups = new List<Blueprint<BlueprintAbilityReference>>();
            foreach (EidolonForm category in Enum.GetValues(typeof(EidolonForm)))
            {
                string name = "SynthesistAppearance" + category + "Menu";
                var menu = AbilityConfigurator.New(name, SynthesistBlueprints.Id(name))
                    .SetDisplayName(SynthesistBlueprints.Text(name + ".name", category + " appearances"))
                    .SetDescription(SynthesistBlueprints.Text(name + ".description", "Browse this visual group. Every model can be used with any mechanical base form."))
                    .SetType(Kingmaker.UnitLogic.Abilities.Blueprints.AbilityType.Special)
                    .SetRange(Kingmaker.UnitLogic.Abilities.Blueprints.AbilityRange.Personal).SetCanTargetSelf(true)
                    .SetActionType(Kingmaker.UnitLogic.Commands.Base.UnitCommand.CommandType.Free)
                    .AddAbilityVariants(Models.Where(m => m.Category == category).Select(m =>
                        (Blueprint<BlueprintAbilityReference>)BlueprintTool.GetRef<BlueprintAbilityReference>(SynthesistBlueprints.Id("SynthesistChooseAppearance" + m.Key + "Ability"))).ToList())
                    .ConfigureWithLogging();
                groups.Add(menu.ToReference<BlueprintAbilityReference>());
            }
            AbilityConfigurator.New("SynthesistAppearanceAbility", SynthesistBlueprints.Id("SynthesistAppearanceAbility"))
                .SetDisplayName(SynthesistBlueprints.Text("appearance.control.name", "Choose eidolon appearance"))
                .SetDescription(SynthesistBlueprints.Text("appearance.control.description", "Browse Biped, Quadruped and Serpentine appearances during preparation. All groups are available to every build. Use the model's built-in visuals; equipped items are not overlaid. Evolution allocation remains locked except after advancement or applicable retraining."))
                .SetType(Kingmaker.UnitLogic.Abilities.Blueprints.AbilityType.Special)
                .SetRange(Kingmaker.UnitLogic.Abilities.Blueprints.AbilityRange.Personal)
                .SetCanTargetSelf(true)
                .SetActionType(Kingmaker.UnitLogic.Commands.Base.UnitCommand.CommandType.Free)
                .AddAbilityVariants(groups)
                .ConfigureWithLogging();
        }
        internal static string Description(EidolonAppearance model) => model.Category + " browsing group; usable with any base form, with or without arms. " +
            "Only the model's built-in visual details are used; your equipped items are not overlaid. Visible equipment is cosmetic. " +
            "Appearance grants no statistics, limbs, attacks, flight or subtype powers. Unusual shapes are grouped approximately. Model animations await player testing.";
        internal static BlueprintBuff FusionBuff(UnitPartSynthesist part)
        {
            var model = Find(part.Owner.Get<UnitPartSynthesistBuild>()?.SelectedAppearance);
            return model == null ? SynthesistBlueprints.Buff("SynthesistFusion" + part.State.Form + "Buff") : SynthesistBlueprints.Buff(model.BuffName);
        }
        internal static void ApplyCanonical(UnitPartSynthesist part)
        {
            var model = Find(part.Owner.Get<UnitPartSynthesistBuild>()?.SelectedAppearance);
            if (model == null || part.Eidolon == null) return; // Existing saves retain their initial template until a build is chosen.
            string prefab = model.Source.Prefab.AssetId;
            if (part.Eidolon.Descriptor.CustomPrefabGuid == prefab) return;
            part.Eidolon.Descriptor.CustomPrefabGuid = prefab;
            if (part.Eidolon.View != null) part.Eidolon.RecreateView();
        }
    }

    [TypeId("f042387fd60649278e8308aaf2279ea3")]
    public sealed class AppearanceRestriction : BlueprintComponent, IAbilityCasterRestriction
    {
        public string Model;
        public bool IsCasterRestrictionPassed(UnitEntityData caster)
        {
            var part = caster.Get<UnitPartSynthesist>();
            var model = AppearanceIntegration.Find(Model);
            return part?.IsPreparing == true && model != null;
        }
        public string GetAbilityCasterRestrictionUIText() => "Enter preparation and choose an appearance.";
    }

    [TypeId("25c28b70457b4a6ca9e8248ac0ba183d")]
    public sealed class ChooseAppearance : ContextAction
    {
        public string Model;
        public override string GetCaption() => "Choose eidolon appearance";
        public override void RunAction()
        {
            var caster = Context.MaybeCaster;
            if (!new AppearanceRestriction { Model = Model }.IsCasterRestrictionPassed(caster))
                throw new InvalidOperationException("The appearance is unknown or preparation is closed.");
            caster.Ensure<UnitPartSynthesistBuild>().AppearanceOverride = Model;
            AppearanceIntegration.ApplyCanonical(caster.Get<UnitPartSynthesist>());
        }
    }
}
