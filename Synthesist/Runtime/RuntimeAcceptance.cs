#if SYNTHESIST_ACCEPTANCE
using System;
using System.IO;
using System.Linq;
using Kingmaker;
using Kingmaker.Blueprints;
using Kingmaker.Blueprints.Classes;
using Kingmaker.EntitySystem.Entities;
using Kingmaker.EntitySystem.Stats;
using Kingmaker.RuleSystem;
using Kingmaker.RuleSystem.Rules.Damage;
using Kingmaker.UnitLogic.Abilities;
using Newtonsoft.Json;
using WOTR_MAKING_FRIENDS.Synthesist.Blueprints;
using WOTR_MAKING_FRIENDS.Synthesist.Rules;

namespace WOTR_MAKING_FRIENDS.Synthesist.Runtime
{
    // Development builds only. A deliberately narrow integration-test interface; no arbitrary
    // code, file paths, normal campaigns or UI input can be executed through this interface.
    internal static class RuntimeAcceptance
    {
        private sealed class Request { public string Id; public string Command; public int Amount; }
        private static DateTime nextPoll;
        private static Kingmaker.UnitLogic.Commands.Base.UnitCommand lastPurchase;
        private static UnitEntityData lastOpponent;
        internal static void Poll()
        {
            if (DateTime.UtcNow < nextPoll || Game.Instance?.Player == null || Game.Instance.Player.MainCharacter.Value == null) return;
            nextPoll = DateTime.UtcNow.AddSeconds(1);
            string file = Path.Combine(Main.modInfo.Path, "synthesist-test-request.json");
            if (!File.Exists(file)) return;
            var request = JsonConvert.DeserializeObject<Request>(File.ReadAllText(file));
            File.Delete(file);
            object result;
            try
            {
                UnitEntityData unit = Game.Instance.Player.MainCharacter.Value;
                if (!unit.CharacterName.StartsWith("SYNTHTEST-", StringComparison.Ordinal))
                    throw new InvalidOperationException("Acceptance commands require a separate SYNTHTEST- character.");
                var part = unit.Get<UnitPartSynthesist>() ?? throw new InvalidOperationException("No Synthesist unit part.");
                switch (request.Command)
                {
                    case "inspect": break;
                    case "performance": RuntimePerformance.Start(request.Id); break;
                    case "prepare": part.BeginPreparation(); break;
                    case "confirm": part.ConfirmPreparation(); break;
                    case "reset-evolutions": part.ResetEvolutions(); part.SyncProgression(); break;
                    case "fuse": part.EnterFusion(); break;
                    case "dismiss": part.Dismiss(); break;
                    case "split": part.BeginSplit(); break;
                    case "rejoin": part.EndSplit(); break;
                    case "twin": part.BeginTwin(); break;
                    case "end-twin": part.EndTwin(); break;
                    case "link": part.State.LinkEnabled = request.Amount != 0; break;
                    case "combat-probe":
                        // A fixed native opponent in the isolated test campaign. Damage,
                        // combat entry/exit and mod restoration use the real game path.
                        if (unit.IsInCombat) throw new InvalidOperationException("Finish the current test combat first.");
                        var enemyBlueprint = BlueprintCore.Utils.BlueprintTool.Get<BlueprintUnit>("fbd199907bdc8b948a5afc7313ac29f5");
                        var enemy = Game.Instance.EntityCreator.SpawnUnit(enemyBlueprint,
                            unit.Position + new UnityEngine.Vector3(3, 0, 0), UnityEngine.Quaternion.identity, Game.Instance.LoadedAreaState.MainState);
                        lastOpponent = enemy;
                        if (request.Amount != 0)
                        {
                            if (request.Amount < 100 || request.Amount > 1000) throw new ArgumentOutOfRangeException("Amount");
                            enemy.Stats.HitPoints.BaseValue = request.Amount;
                        }
                        enemy.CombatState.Engage(unit);
                        unit.CombatState.Engage(enemy);
                        enemy.CombatState.JoinCombat(false);
                        unit.CombatState.JoinCombat(false);
                        unit.Commands.Run(new Kingmaker.UnitLogic.Commands.UnitAttack(enemy, null));
                        break;
                    case "end-combat-probe":
                        if (lastOpponent == null || lastOpponent.State.IsDead) throw new InvalidOperationException("No living fixed test opponent.");
                        Rulebook.Trigger(new RuleDealDamage(unit, lastOpponent, new DirectDamage(DiceFormula.Zero, 100000)));
                        break;
                    case "retrain-evolution-probe":
                        // Adapter probe only: this deliberately does not claim to test
                        // Transmogrify's native spell, material cost or casting time.
                        if (!part.IsPreparing) throw new InvalidOperationException("Enter preparation before the retraining adapter probe.");
                        part.State.OpenEvolutionRetraining();
                        break;
                    case "purchase-managed":
                    case "purchase-aspect-managed":
                        string[] managed = { "EvolutionNaturalArmor", "EvolutionLarge", "EvolutionFastHealing", "EvolutionAbilityIncreaseStrength", "EvolutionAbilityIncreaseConstitution",
                            "EvolutionAbilityIncreaseIntelligence", "EvolutionAbilityIncreaseWisdom", "EvolutionAbilityIncreaseCharisma" };
                        if (request.Amount < 0 || request.Amount >= managed.Length || !part.IsPreparing)
                            throw new InvalidOperationException("Choose a supported managed evolution during preparation.");
                        var purchaser = request.Command == "purchase-aspect-managed" ? unit : part.Eidolon;
                        var menu = BlueprintCore.Utils.BlueprintTool.Get<Kingmaker.UnitLogic.Abilities.Blueprints.BlueprintAbility>(
                            SynthesistBlueprints.Id("SynthesistEvolution" + EvolutionIntegration.Costs[managed[request.Amount]] + "Menu"));
                        var menuFact = purchaser.Descriptor.Abilities.GetAbility(menu) ?? throw new InvalidOperationException("The native evolution menu is not granted.");
                        // Variants inherit their native menu fact, exactly as the UI does.
                        // A bare AbilityData has no fact and native execution rejects it.
                        var purchase = new Kingmaker.UnitLogic.Abilities.AbilityData(new Kingmaker.UnitLogic.Abilities.AbilityData(menuFact),
                            SynthesistBlueprints.EvolutionAbilities[managed[request.Amount]]);
                        if (!purchase.CanBeCastByCaster) throw new InvalidOperationException(purchase.GetUnavailableReason());
                        if (!purchase.CanTarget(purchaser)) throw new InvalidOperationException("The native purchase cannot target its build owner.");
                        lastPurchase = new Kingmaker.UnitLogic.Commands.UnitUseAbility(purchase, purchaser);
                        purchaser.Commands.Run(lastPurchase);
                        break;
                    case "purchase-dr":
                    case "purchase-aspect-dr":
                        var drOptions = new[] { "Good", "Evil", "Lawful", "Chaotic" };
                        if (request.Amount < 0 || request.Amount >= drOptions.Length || !part.IsPreparing)
                            throw new InvalidOperationException("Choose a damage-reduction alignment during preparation.");
                        var drPurchaser = request.Command == "purchase-aspect-dr" ? unit : part.Eidolon;
                        var drMenu = BlueprintCore.Utils.BlueprintTool.Get<Kingmaker.UnitLogic.Abilities.Blueprints.BlueprintAbility>(SynthesistBlueprints.Id("SynthesistEvolution3Menu"));
                        var drMenuFact = drPurchaser.Descriptor.Abilities.GetAbility(drMenu) ?? throw new InvalidOperationException("The native evolution menu is not granted.");
                        var drPurchase = new AbilityData(new AbilityData(drMenuFact), DamageReductionIntegration.Purchases[drOptions[request.Amount]]);
                        if (!drPurchase.CanBeCastByCaster) throw new InvalidOperationException(drPurchase.GetUnavailableReason());
                        if (!drPurchase.CanTarget(drPurchaser)) throw new InvalidOperationException("The native DR purchase cannot target its build owner.");
                        lastPurchase = new Kingmaker.UnitLogic.Commands.UnitUseAbility(drPurchase, drPurchaser);
                        drPurchaser.Commands.Run(lastPurchase);
                        break;
                    case "advance":
                        if (part.IsFused || part.IsSplit || part.TwinActive || part.IsPreparing) throw new InvalidOperationException("Dismiss before accelerated advancement.");
                        if (request.Amount < part.Level || request.Amount > 20) throw new ArgumentOutOfRangeException("Amount");
                        var levels = new AddClassLevels
                        {
                            m_CharacterClass = SynthesistBlueprints.SummonerClass.ToReference<BlueprintCharacterClassReference>(),
                            m_Archetypes = new[] { BlueprintCore.Utils.BlueprintTool.GetRef<BlueprintArchetypeReference>(SynthesistBlueprints.Id("SynthesistArchetype")) },
                            LevelsStat = StatType.Charisma, RaceStat = StatType.Unknown,
                            Skills = new[] { StatType.SkillPersuasion, StatType.SkillPerception, StatType.SkillUseMagicDevice },
                            Selections = Array.Empty<SelectionEntry>(), m_SelectSpells = Array.Empty<BlueprintAbilityReference>(), m_MemorizeSpells = Array.Empty<BlueprintAbilityReference>()
                        };
                        unit.Progression.AdvanceExperienceTo(Kingmaker.Blueprints.Root.BlueprintRoot.Instance.Progression.XPTable.GetBonus(request.Amount), false, true);
                        var selections = new System.Collections.Generic.Dictionary<SelectionEntry, System.Collections.Generic.HashSet<int>>();
                        // The NPC LevelUp wrapper obeys the protagonist blueprint's creation
                        // level cap. Exercise actual native level transactions directly instead.
                        int count = request.Amount - part.Level;
                        for (int i = 0; i < count; i++)
                            AddClassLevels.AddLevel(levels, unit.Descriptor, selections, unit.Descriptor.MainFact, true);
                        if (part.Level != request.Amount) throw new InvalidOperationException("Native accelerated advancement did not reach the requested level.");
                        part.SyncProgression();
                        AddClassLevels.LevelUp(part.Eidolon.Blueprint.GetComponent<AddClassLevels>(), part.Eidolon.Descriptor,
                            EidolonProgression.At(part.Level).HitDice - part.Eidolon.Progression.CharacterLevel, part.Eidolon.Descriptor.MainFact);
                        part.SyncProgression();
                        if (part.Eidolon.Progression.CharacterLevel != EidolonProgression.At(part.Level).HitDice)
                            throw new InvalidOperationException("Native eidolon advancement did not reach the original HD target.");
                        break;
                    case "damage": Rulebook.Trigger(new RuleDealDamage(unit, unit, new DirectDamage(new DiceFormula(0, DiceType.Zero), request.Amount))); break;
                    case "physical-damage":
                    case "physical-damage-pet":
                        if (request.Amount is not (0 or 1 or 2 or 4 or 8)) throw new ArgumentOutOfRangeException("Amount");
                        if (request.Command == "physical-damage-pet" && !part.IsSplit)
                            throw new InvalidOperationException("Independent eidolon damage requires Split Forms.");
                        var physical = new PhysicalDamage(new Kingmaker.RuleSystem.Rules.ModifiableDiceFormula(DiceFormula.Zero), 20, Kingmaker.Enums.Damage.PhysicalDamageForm.Slashing);
                        physical.AddAlignment((Kingmaker.Enums.Damage.DamageAlignment)request.Amount);
                        Rulebook.Trigger(new RuleDealDamage(unit, request.Command == "physical-damage-pet" ? part.Eidolon : unit, physical));
                        break;
                    case "heal-personal": Rulebook.Trigger(new RuleHealDamage(unit, unit, request.Amount)); break;
                    case "damage-pet":
                        if (!part.IsSplit) throw new InvalidOperationException("Independent eidolon damage requires Split Forms.");
                        Rulebook.Trigger(new RuleDealDamage(unit, part.Eidolon, new DirectDamage(new DiceFormula(0, DiceType.Zero), request.Amount)));
                        break;
                    case "heal-pet":
                        if (!part.IsSplit) throw new InvalidOperationException("Independent eidolon healing requires Split Forms.");
                        Rulebook.Trigger(new RuleHealDamage(unit, part.Eidolon, request.Amount));
                        break;
                    default: throw new ArgumentException("Unknown acceptance command.");
                }
                result = new { request.Id, Success = true, State = part.State, unit.UniqueId, unit.CharacterName,
                    SummonerLevel = part.Level, part.TwinActive, part.IsRitualActive,
                    unit.IsInCombat,
                    CurrentMode = Game.Instance.CurrentMode.ToString(), Game.Instance.IsPaused,
                    GameTime = UnitPartSynthesist.Now,
                    LastPurchase = lastPurchase == null ? null : new { Result = lastPurchase.Result.ToString(), lastPurchase.IsFinished, lastPurchase.IsRunning, lastPurchase.DidRun },
                    Resources = Enum.GetValues(typeof(DailyAbility)).Cast<DailyAbility>().Select(a => new
                    { Ability = a.ToString(), Remaining = unit.Descriptor.Resources.GetResourceAmount(SynthesistBlueprints.Resource(a)) }).ToArray(),
                    PersonalHP = unit.HPLeft, MaximumHP = unit.MaxHP, Damage = unit.Damage,
                    Strength = unit.Stats.Strength.ModifiedValue, Dexterity = unit.Stats.Dexterity.ModifiedValue,
                    Constitution = unit.Stats.Constitution.ModifiedValue, BAB = unit.Stats.BaseAttackBonus.ModifiedValue,
                    Intelligence = unit.Stats.Intelligence.ModifiedValue, Wisdom = unit.Stats.Wisdom.ModifiedValue,
                    Charisma = unit.Stats.Charisma.ModifiedValue,
                    Size = unit.State.Size.ToString(), ReachBonus = unit.Stats.GetStat(StatType.Reach).ModifiedValue,
                    TemporaryHP = unit.Stats.TemporaryHitPoints.ModifiedValue,
                    ArmourClass = unit.Stats.AC.ModifiedValue,
                    ArmourModifiers = unit.Stats.AC.Modifiers.Select(m => new { Descriptor = m.ModDescriptor.ToString(), m.ModValue, Source = m.Source?.Blueprint?.name }).ToArray(),
                    IsPolymorphed = unit.Body.IsPolymorphed, View = unit.View?.name,
                    Buffs = Projection.Buffs(unit).Select(b => new { b.Blueprint.name, b.IsActive, b.IsTurnedOn }).ToArray(),
                    PetId = part.Eidolon?.UniqueId, PetLevel = part.Eidolon?.Progression.CharacterLevel, PetMaxHP = part.Eidolon?.MaxHP,
                    PetActive = part.Eidolon?.IsInGame, PetClass = part.Eidolon?.Progression.Classes.Select(c => c.CharacterClass.name).ToArray(),
                    PetView = part.Eidolon?.View?.name, PetCanCast = part.Eidolon?.State.CanCast,
                    PetCommands = part.Eidolon?.Commands.m_Commands.Where(c => c != null).Select(c => new
                    { Command = c.GetType().Name, Result = c.Result.ToString(), c.IsFinished, c.IsRunning, c.DidRun }).ToArray(),
                    PetHP = part.Eidolon?.HPLeft, PetDamage = part.Eidolon?.Damage, PetDead = part.Eidolon?.State.IsDead,
                    PetHPBase = part.Eidolon?.Stats.HitPoints.BaseValue,
                    PetHPModifiers = part.Eidolon?.Stats.HitPoints.Modifiers.Select(m => new
                    { Descriptor = m.ModDescriptor.ToString(), m.ModValue, Source = m.Source?.Blueprint?.name, m.SourceComponent }).ToArray(),
                    PetHitDice = part.Eidolon?.Progression.Classes.Select(c => new
                    { c.Level, Declared = c.CharacterClass.HitDie.ToString(), Effective = c.CharacterClass.GetHitDie(part.Eidolon.Descriptor).ToString() }).ToArray(),
                    EvolutionPoints = part.Eidolon?.Descriptor.Resources.GetResourceAmount(BlueprintCore.Utils.BlueprintTool.Get<BlueprintAbilityResource>(SynthesistBlueprints.Id("SummonerEvolutionPointsResource"))),
                    AspectPoints = unit.Descriptor.Resources.GetResourceAmount(BlueprintCore.Utils.BlueprintTool.Get<BlueprintAbilityResource>(SynthesistBlueprints.Id("SummonerEvolutionPointsResource"))),
                    Purchased = SynthesistBlueprints.PurchasedFeatures.Select(p => new { Name = p.Key, Rank = part.Eidolon.Progression.Features.GetRank(p.Value) }).ToArray(),
                    LegacyEvolutionGuards = SynthesistBlueprints.LegacyEvolutionAbilities.Select(blueprint => new
                    {
                        blueprint.name,
                        BlockedForSynthesist = AbilityGuards.Reason(new AbilityData(blueprint, unit.Descriptor)) != null,
                        BlockedForOrdinaryCompanion = Game.Instance.Player.Party.Where(other => other != unit && other.Get<UnitPartSynthesist>() == null)
                            .Any(other => AbilityGuards.Reason(new AbilityData(blueprint, other.Descriptor)) != null)
                    }).ToArray(),
                    PetStrength = part.Eidolon?.Stats.Strength.BaseValue, PetDexterity = part.Eidolon?.Stats.Dexterity.BaseValue,
                    PetIntelligence = part.Eidolon?.Stats.Intelligence.ModifiedValue,
                    PetWisdom = part.Eidolon?.Stats.Wisdom.ModifiedValue, PetCharisma = part.Eidolon?.Stats.Charisma.ModifiedValue,
                    Alignment = unit.Descriptor.Alignment.ValueRaw.ToString(),
                    DamageReductionChoices = DamageReductionIntegration.Purchases.Select(p => new
                    {
                        Alignment = p.Key,
                        CanonicalRank = part.Eidolon.Progression.Features.GetRank(DamageReductionIntegration.Choices[p.Key]),
                        PersonalRank = unit.Progression.Features.GetRank(DamageReductionIntegration.Choices[p.Key]),
                        AvailableForCanonical = DamageReductionIntegration.CanPurchase(part.Eidolon, p.Key),
                        AvailableForPersonal = DamageReductionIntegration.CanPurchase(unit, p.Key)
                    }).ToArray(),
                    PetSize = part.Eidolon?.State.Size.ToString(), PetReachBonus = part.Eidolon?.Stats.GetStat(StatType.Reach).ModifiedValue,
                    Facts = unit.Progression.Features.Enumerable.Select(f => new { f.Blueprint.name, f.Rank }).ToArray(),
                    PetFacts = part.Eidolon?.Progression.Features.Enumerable.Select(f => new { f.Blueprint.name, f.Rank }).ToArray(),
                    ProjectionFacts = part.ProjectionFacts.Count, ProjectionModifiers = part.ProjectionModifiers.Count };
            }
            catch (Exception error) { result = new { request.Id, Success = false, Error = error.ToString() }; }
            File.WriteAllText(Path.Combine(Main.modInfo.Path, "synthesist-test-response.json"), JsonConvert.SerializeObject(result, Formatting.Indented));
        }
    }
}
#endif
