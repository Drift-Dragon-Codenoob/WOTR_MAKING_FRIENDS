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
        internal readonly EidolonForm Form;
        internal readonly bool SupportsWeapons;
        internal EidolonAppearance(string key, string name, EidolonForm form, bool weapons)
        { Key = key; Name = name; Form = form; SupportsWeapons = weapons; }
        internal BlueprintUnit Source => BlueprintCore.Utils.BlueprintTool.Get<BlueprintUnit>(SynthesistBlueprints.Id("Eidolon" + Key));
        internal string BuffName => "SynthesistFusionAppearance" + Key + "Buff";
    }

    internal static class AppearanceIntegration
    {
        // These are visual templates only, never subtype/progression packages.
        // Animation acceptance belongs to the player test checklist.
        internal static readonly EidolonAppearance[] Models =
        {
            new("Incubus", "Incubus", EidolonForm.Biped, true),
            new("Succubus", "Succubus", EidolonForm.Biped, true),
            new("MovanicDeva", "Movanic deva", EidolonForm.Biped, true),
            new("HellHound", "Hellhound", EidolonForm.Quadruped, false),
            new("Nightmare", "Nightmare", EidolonForm.Quadruped, false),
            new("Manticore", "Manticore", EidolonForm.Quadruped, false),
            new("Vavakia", "Vavakia (armed form)", EidolonForm.Quadruped, true),
            new("Marilith", "Marilith", EidolonForm.Serpentine, true),
            new("Lillend", "Lillend", EidolonForm.Serpentine, true),
            new("Salamander", "Salamander", EidolonForm.Serpentine, true)
        };
        internal static EidolonAppearance Find(string key) => Array.Find(Models, m => m.Key == key);
        internal static bool Compatible(CharacterBuild build, EidolonAppearance model)
            => model != null && build.CanUseAppearance(model.Form, model.SupportsWeapons);
        internal static bool Compatible(UnitPartSynthesist part)
        {
            var model = Find(part.Owner.Get<UnitPartSynthesistBuild>()?.SelectedAppearance);
            return model == null || model.Form == part.State.Form && (!Projection.HasArms(part) || model.SupportsWeapons);
        }
        internal static void Create()
        {
            foreach (var model in Models)
            {
                var buff = BuffConfigurator.New(model.BuffName, SynthesistBlueprints.Id(model.BuffName))
                    .CopyFrom(SynthesistBlueprints.Buff("SynthesistFusion" + model.Form + "Buff"), c => false)
                    .ConfigureWithLogging();
                buff.AddComponents(IsolatedCopy.Components(SynthesistBlueprints.Buff("SynthesistFusion" + model.Form + "Buff"), c => true));
                var polymorph = buff.GetComponent<Kingmaker.UnitLogic.Buffs.Polymorph>();
                polymorph.m_Prefab = model.Source.Prefab;
                polymorph.m_PrefabFemale = model.Source.Prefab;
                string abilityName = "SynthesistChooseAppearance" + model.Key + "Ability";
                AbilityConfigurator.New(abilityName, SynthesistBlueprints.Id(abilityName))
                    .SetDisplayName(SynthesistBlueprints.Text(abilityName + ".name", model.Name))
                    .SetDescription(SynthesistBlueprints.Text(abilityName + ".description", "Choose this cosmetic model during preparation. It grants no evolutions or subtype powers."))
                    .SetType(Kingmaker.UnitLogic.Abilities.Blueprints.AbilityType.Special)
                    .SetRange(Kingmaker.UnitLogic.Abilities.Blueprints.AbilityRange.Personal)
                    .SetCanTargetSelf(true)
                    .SetActionType(Kingmaker.UnitLogic.Commands.Base.UnitCommand.CommandType.Free)
                    .AddComponent<AppearanceRestriction>(c => c.Model = model.Key)
                    .AddAbilityEffectRunAction(new ActionList { Actions = new GameAction[] { new ChooseAppearance { Model = model.Key } } })
                    .ConfigureWithLogging();
            }
            AbilityConfigurator.New("SynthesistAppearanceAbility", SynthesistBlueprints.Id("SynthesistAppearanceAbility"))
                .SetDisplayName(SynthesistBlueprints.Text("appearance.control.name", "Choose eidolon appearance"))
                .SetDescription(SynthesistBlueprints.Text("appearance.control.description", "Choose a compatible cosmetic model during preparation. Evolution allocation remains locked except after advancement or applicable retraining."))
                .SetType(Kingmaker.UnitLogic.Abilities.Blueprints.AbilityType.Special)
                .SetRange(Kingmaker.UnitLogic.Abilities.Blueprints.AbilityRange.Personal)
                .SetCanTargetSelf(true)
                .SetActionType(Kingmaker.UnitLogic.Commands.Base.UnitCommand.CommandType.Free)
                .AddAbilityVariants(Models.Select(m => (Blueprint<BlueprintAbilityReference>)BlueprintTool.GetRef<BlueprintAbilityReference>(SynthesistBlueprints.Id("SynthesistChooseAppearance" + m.Key + "Ability"))).ToList())
                .ConfigureWithLogging();
        }
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
            return part?.IsPreparing == true && model != null && model.Form == part.State.Form &&
                (!Projection.HasArms(part) || model.SupportsWeapons);
        }
        public string GetAbilityCasterRestrictionUIText() => "Enter preparation and choose a model compatible with the eidolon's base form and arms.";
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
                throw new InvalidOperationException("The appearance is incompatible or preparation is closed.");
            caster.Ensure<UnitPartSynthesistBuild>().AppearanceOverride = Model;
            AppearanceIntegration.ApplyCanonical(caster.Get<UnitPartSynthesist>());
        }
    }
}
