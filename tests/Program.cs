using Kingmaker.Blueprints;
using Kingmaker.Blueprints.Classes;
using Kingmaker.EntitySystem.Entities;
using WOTR_MAKING_FRIENDS.ComponentsNew;
using WOTR_MAKING_FRIENDS.Enums;
using WOTR_MAKING_FRIENDS.Utilities;

int checks = 0;
void Check(bool condition, string description)
{
    checks++;
    if (!condition) throw new Exception("FAILED: " + description);
}
BlueprintFeatureReference Feature(string id) => new() { Id = id };
bool Has(UnitEntityData unit, string id) => unit.Progression.Features.HasFact(Feature(id));

var gate = new AbilityCasterHasFactRank
{
    m_UnitFact = Feature("growth"), startLevel = 8, numLevelsBetweenRanks = 5, maxRank = 2
};
var unit = new UnitEntityData();
unit.Progression.CharacterLevel = 7;
Check(!gate.IsCasterRestrictionPassed(unit), "Growth unavailable at 7");
unit.Progression.CharacterLevel = 8;
Check(gate.IsCasterRestrictionPassed(unit), "Growth available at 8");
unit.Progression.Features.Add("growth");
unit.Progression.CharacterLevel = 12;
Check(!gate.IsCasterRestrictionPassed(unit), "Huge unavailable at 12");
unit.Progression.CharacterLevel = 13;
Check(gate.IsCasterRestrictionPassed(unit), "Huge available at 13");
unit.Progression.Features.GetFact(Feature("growth")).Rank = 2;
unit.Progression.CharacterLevel = 20;
Check(!gate.IsCasterRestrictionPassed(unit), "No third Growth purchase");
Check(BaselineRules.NextRankLevel(0, 5, 1) == 5, "Natural armour next purchase boundary");
Check(BaselineRules.NextRankLevel(0, 6, 1) == 6, "Ability increase next purchase boundary");

Check(BaselineRules.ProtectionDeficit(30, 29) == 0, "Hit leaving 1 HP needs no transfer");
Check(BaselineRules.ProtectionDeficit(30, 30) == 1, "Hit reaching 0 HP transfers 1");
Check(BaselineRules.ProtectionDeficit(30, 80) == 51, "Overkill is included");
Check(BaselineRules.AllocateProtection(15, [5, 20]).SequenceEqual([5, 10]), "Transfer is distributed without list mutation");
Check(BaselineRules.AllocateProtection(26, [5, 20]).All(x => x == 0), "Insufficient donors cannot grant free protection");
Check(BaselineRules.AllocateProtection(1, [0]).Single() == 0, "A donor at 1 HP cannot pay");
var random = new Random(1047);
for (int sample = 0; sample < 400; sample++)
{
    int[] capacities = Enumerable.Range(0, random.Next(0, 8)).Select(_ => random.Next(0, 100)).ToArray();
    int deficit = random.Next(0, 500);
    int[] amounts = BaselineRules.AllocateProtection(deficit, capacities);
    Check(amounts.Length == capacities.Length && amounts.Zip(capacities).All(p => p.First >= 0 && p.First <= p.Second), "Allocation respects donor limits");
    Check(amounts.Sum() == (capacities.Sum() >= deficit ? deficit : 0), "Allocation pays exactly or declines");
}

var caster = new UnitEntityData();
var target = new UnitEntityData();
caster.Progression.Features.Add("caster-purchase", FeatureGroup.Purchased);
caster.Progression.Features.Add("caster-base", FeatureGroup.Base);
target.Progression.Features.Add("target-purchase", FeatureGroup.Purchased);
target.Progression.Features.Add("target-base", FeatureGroup.Base);
var reset = new RemoveFactWithGroup { featureGroup = FeatureGroup.Purchased, affectCaster = true };
reset.Target.Unit = target;
reset.AbilityContext.Caster = caster;
reset.RunAction();
Check(!Has(caster, "caster-purchase") && !Has(target, "target-purchase"), "Reset uses each unit's own feature collection");
Check(Has(caster, "caster-base") && Has(target, "target-base"), "Reset preserves base features");
reset.RunAction();
Check(Has(caster, "caster-base"), "Repeated reset is harmless");

caster.Progression.Features.Add("original");
target.Progression.Features.Add("replacement");
var restore = new AddFactsOnArray
{
    originalBlueprints = [Feature("original")], newBlueprints = [Feature("replacement")], affectCaster = true
};
restore.Target.Unit = target;
restore.AbilityContext.Caster = caster;
restore.RunAction();
Check(Has(caster, "replacement"), "Caster restoration is independent of target presence");
restore.RunAction();
Check(caster.Progression.Features.Count(f => f.Blueprint.Id == "replacement") == 1, "Restoration does not duplicate features");

var resource = new BlueprintAbilityResource { Id = "evolution" };
var sized = new UnitEntityData();
var physicalCost = new AbilityCasterHasResource
{
    m_Resource = new BlueprintAbilityResourceReference { Resource = resource },
    resourceAmount = 2, doubleForLargePhysicalIncrease = true
};
sized.Descriptor.Resources.Amounts["evolution"] = 2;
Check(physicalCost.IsCasterRestrictionPassed(sized), "Medium physical increase costs two");
sized.State.Size = Kingmaker.Enums.Size.Large;
Check(!physicalCost.IsCasterRestrictionPassed(sized), "Large physical increase rejects only two available points");
sized.Descriptor.Resources.Amounts["evolution"] = 4;
Check(physicalCost.IsCasterRestrictionPassed(sized), "Large physical increase accepts four available points");
sized.State.Size = Kingmaker.Enums.Size.Huge;
Check(physicalCost.IsCasterRestrictionPassed(sized), "Huge physical increase keeps the doubled cost");
physicalCost.doubleForLargePhysicalIncrease = false;
sized.Descriptor.Resources.Amounts["evolution"] = 2;
Check(physicalCost.IsCasterRestrictionPassed(sized), "Other evolutions keep their ordinary cost at Huge size");
var growthSize = new AbilityGrowthSizeRestriction();
sized.State.Size = Kingmaker.Enums.Size.Small;
Check(!growthSize.IsCasterRestrictionPassed(sized), "Growth rejects Small current size");
sized.State.Size = Kingmaker.Enums.Size.Medium;
Check(growthSize.IsCasterRestrictionPassed(sized), "First Growth requires Medium current size");
sized.Progression.Features.Add("EvolutionLargeFeature");
Check(!growthSize.IsCasterRestrictionPassed(sized), "Second Growth rejects Medium current size");
sized.State.Size = Kingmaker.Enums.Size.Large;
Check(growthSize.IsCasterRestrictionPassed(sized), "Second Growth requires Large current size");
sized.Progression.Features.GetFact(Feature("EvolutionLargeFeature")).Rank = 2;
Check(!growthSize.IsCasterRestrictionPassed(sized), "Growth cannot be bought a third time");
var isolatedGrowthSize = new AbilityGrowthSizeRestriction { m_GrowthFeature = Feature("isolated-growth") };
sized.State.Size = Kingmaker.Enums.Size.Medium;
Check(isolatedGrowthSize.IsCasterRestrictionPassed(sized), "Isolated Growth ignores ordinary feature ranks");
sized.Progression.Features.Add("isolated-growth");
Check(!isolatedGrowthSize.IsCasterRestrictionPassed(sized), "Isolated second Growth still rejects Medium size");
sized.State.Size = Kingmaker.Enums.Size.Large;
Check(isolatedGrowthSize.IsCasterRestrictionPassed(sized), "Isolated second Growth uses its own first rank");
sized.Progression.Features.GetFact(Feature("isolated-growth")).Rank = 2;
Check(!isolatedGrowthSize.IsCasterRestrictionPassed(sized), "Isolated Growth rejects a third purchase");
Check(BaselineRules.GrowthReachBonus(0, true) == 0, "No Growth adds no reach");
Check(BaselineRules.GrowthReachBonus(1, true) == 5 && BaselineRules.GrowthReachBonus(2, true) == 10, "Biped Growth reach totals are five and ten feet");
Check(BaselineRules.GrowthReachBonus(1, false) == 0 && BaselineRules.GrowthReachBonus(2, false) == 5, "Other forms gain reach only at Huge size");
Check(BaselineRules.GrowthReachAdjustment(0, true, 7) == 0, "No Growth changes no native reach");
Check(BaselineRules.GrowthReachAdjustment(1, true, 4) == 1, "Large biped compensates the native four-foot size contribution");
Check(BaselineRules.GrowthReachAdjustment(2, true, 7) == 3, "Huge biped compensates the native seven-foot size contribution");
Check(BaselineRules.GrowthReachAdjustment(1, false, 4) == -4, "Large non-biped removes native size reach which the original form does not gain");
Check(BaselineRules.GrowthReachAdjustment(2, false, 7) == -2, "Huge non-biped supplies the original five-foot increase");
Check(5 + 7 + BaselineRules.GrowthReachAdjustment(2, true, 7) + 5 == 20, "Independent five-foot reach bonus survives Growth compensation");
var aspect = new UnitEntityData();
var aspectPet = new Pet { EntityPart = new PetPart { Type = PetTypeExtensions.Eidolon } };
aspect.Pets.Add(aspectPet);
var sharedCost = new AbilityEidolonHasResource
{
    m_Resource = new BlueprintAbilityResourceReference { Resource = resource },
    resourceAmount = 2, doubleForLargePhysicalIncrease = true
};
aspect.State.Size = Kingmaker.Enums.Size.Large;
aspectPet.Entity.Descriptor.Resources.Amounts["evolution"] = 2;
Check(!sharedCost.IsCasterRestrictionPassed(aspect), "Large Aspect physical increase requires four eidolon points");
aspect.Progression.Features.Add("SummonerAspectGreaterFeature");
Check(sharedCost.IsCasterRestrictionPassed(aspect), "Greater Aspect halves the doubled shared cost");
sharedCost.doubleForLargePhysicalIncrease = false;
sharedCost.resourceAmount = 3;
aspectPet.Entity.Descriptor.Resources.Amounts["evolution"] = 1;
Check(!sharedCost.IsCasterRestrictionPassed(aspect), "Greater Aspect rounds odd shared costs up");
aspectPet.Entity.Descriptor.Resources.Amounts["evolution"] = 2;
Check(sharedCost.IsCasterRestrictionPassed(aspect), "Greater Aspect accepts two for a three-point evolution");
sharedCost.resourceAmount = 4;
sharedCost.m_Feature = Feature("EvolutionLargeFeature");
sharedCost.useCostMultiplier = true;
sharedCost.costMultiplierByRank = 2;
aspect.Progression.Features.Add("EvolutionLargeFeature");
Check(!sharedCost.IsCasterRestrictionPassed(aspect), "Second Growth requires three shared points under Greater Aspect");
aspectPet.Entity.Descriptor.Resources.Amounts["evolution"] = 3;
Check(sharedCost.IsCasterRestrictionPassed(aspect), "Second Growth accepts the discounted six-point cost");
var eidolon = new Pet { EntityPart = new PetPart { Type = PetTypeExtensions.Eidolon } };
var otherPet = new Pet { EntityPart = new PetPart { Type = 1 } };
eidolon.Entity.Descriptor.Resources.Amounts["daily-ability"] = 0;
otherPet.Entity.Descriptor.Resources.Amounts["evolution"] = 0;
caster.Pets.AddRange([eidolon, otherPet]);
var refill = new ContextRestoreResourceOnPets
{
    m_Resource = new BlueprintAbilityResourceReference { Resource = resource }, m_IsFullRestoreAllResources = true
};
refill.Target.Unit = caster;
refill.RunAction();
Check(eidolon.Entity.Descriptor.Resources.Amounts["evolution"] == 20, "Selected eidolon resource is restored");
Check(eidolon.Entity.Descriptor.Resources.Amounts["daily-ability"] == 0, "Daily resource is not refilled");
Check(otherPet.Entity.Descriptor.Resources.Amounts["evolution"] == 0, "Unrelated pet is untouched");

var budgetUnit = new UnitEntityData();
var budget = new PrimaryAttackBudget { Owner = budgetUnit, m_Resource = new BlueprintAbilityResourceReference { Resource = resource } };
int bonus = 0;
budget.CalculateMaxResourceAmount(resource, ref bonus);
Check(bonus == 0, "No primary set reserves no attacks");
budgetUnit.Progression.Features.Add("EvolutionClawsBaseFeature");
bonus = 0;
budget.CalculateMaxResourceAmount(resource, ref bonus);
Check(bonus == -2, "Base Claws reserves two attacks");
budgetUnit.Progression.Features.Add("EvolutionPincersFeature");
bonus = 0;
budget.CalculateMaxResourceAmount(resource, ref bonus);
Check(bonus == -2, "Alternative primary sets are counted once");
bonus = 0;
budget.CalculateMaxResourceAmount(new BlueprintAbilityResource { Id = "other" }, ref bonus);
Check(bonus == 0, "Primary budget does not affect another resource");

var capacityCheck = new AbilityCasterHasResource
{
    m_Resource = new BlueprintAbilityResourceReference { Resource = resource }, resourceAmount = 2, primaryAttackCount = 2
};
Check(capacityCheck.IsCasterRestrictionPassed(budgetUnit), "An alternate primary is allowed with zero unreserved attacks");
Check(!capacityCheck.IsCasterRestrictionPassed(new UnitEntityData()), "The first primary set needs capacity");
capacityCheck.primaryAttackCount = 0;
Check(!capacityCheck.IsCasterRestrictionPassed(budgetUnit), "Simultaneous extra attacks still need capacity");

var arms = new IncreaseResourceAmountRank { Rank = 2, Value = -2, m_Resource = new BlueprintAbilityResourceReference { Resource = resource } };
foreach ((int rank, int expected) in new[] { (1, 0), (2, -2), (3, -4) })
{
    arms.Fact.Rank = rank;
    bonus = 0;
    arms.CalculateMaxResourceAmount(resource, ref bonus);
    Check(bonus == expected, "Arms charges only the additional pairs");
}

void Buff(UnitEntityData owner, Kingmaker.Blueprints.BlueprintComponent component)
    => owner.Buffs.Add(new Kingmaker.UnitLogic.Buffs.Buff { Blueprint = new BlueprintWithComponents { Component = component } });

var summoner = new UnitEntityData();
var linkedPet = new UnitEntityData { IsPet = true, Master = summoner, Damage = 80 };
Buff(linkedPet, new LifeLinkEidolon());
DamageHarness.Apply(linkedPet, 50);
Check(linkedPet.HPLeft == 1 && summoner.HPLeft == 69, "Life Link transfers precisely the overkill before HP commitment");
foreach (float difficulty in new[] { 0.5f, 2f })
{
    summoner.Damage = 0;
    linkedPet.Damage = 90;
    DamageHarness.Difficulty = difficulty;
    DamageHarness.Apply(linkedPet, 40);
    int transferred = (int)(40 * difficulty) - 9;
    Check(linkedPet.HPLeft == 1 && summoner.HPLeft == 100 - transferred, "Transferred HP is not scaled a second time by difficulty");
}
DamageHarness.Difficulty = 1f;

var bonded = new UnitEntityData { Damage = 90 };
var donor = new UnitEntityData { IsPet = true, Master = bonded, Damage = 70 };
bonded.Pets.Add(new Pet { EntityPart = new PetPart { Type = PetTypeExtensions.Eidolon }, Entity = donor });
Buff(bonded, new LifeBond());
Buff(donor, new LifeLinkEidolon());
DamageHarness.Apply(bonded, 20);
Check(bonded.HPLeft == 1 && donor.HPLeft == 19, "Life Bond transfers actual deficit; the reciprocal link cannot bounce damage");

bonded.Damage = 95;
donor.Damage = 97;
DamageHarness.Apply(bonded, 20);
Check(bonded.HPLeft == -15 && donor.HPLeft == 3, "Insufficient donor capacity does not grant immunity");

bonded.Damage = 99;
donor.Damage = 97;
donor.Stats.TemporaryHitPoints.ModifiedValue = 10;
DamageHarness.Apply(bonded, 5);
Check(bonded.HPLeft == 1 && donor.HPLeft == 3 && donor.Stats.TemporaryHitPoints.ModifiedValue == 5, "Donor temporary HP can pay a transfer");

summoner.Damage = 0;
linkedPet.Damage = 50;
linkedPet.Stats.TemporaryHitPoints.ModifiedValue = 30;
DamageHarness.Apply(linkedPet, 20);
Check(linkedPet.HPLeft == 50 && summoner.HPLeft == 100, "Temporary HP absorption does not trigger a transfer");
DamageHarness.Apply(linkedPet, 70);
Check(linkedPet.HPLeft == 1 && summoner.HPLeft == 89, "Transfer uses damage remaining after temporary HP");

var detached = new UnitEntityData { IsPet = true, Damage = 90 };
Buff(detached, new LifeLinkEidolon());
DamageHarness.Apply(detached, 20);
Check(detached.HPLeft == -10, "Detached eidolon cannot transfer to a missing master");

summoner.Damage = 99;
linkedPet.Damage = 90;
DamageHarness.Apply(linkedPet, 50);
Check(linkedPet.HPLeft == 1 && summoner.HPLeft == -40, "Life Link can cost the Summoner its life, as before");
linkedPet.Damage = 90;
DamageHarness.Apply(linkedPet, 50);
Check(linkedPet.HPLeft == -40, "A master with no remaining positive HP cannot fund another rescue");

bonded.Damage = 90;
donor.Damage = 0;
DamageHarness.Apply(bonded, 200, isFake: true);
Check(bonded.HPLeft == 10 && donor.HPLeft == 100, "Fake damage cannot trigger a transfer");

// Direct HP changes and death effects without a RuleDealDamage event remain untouched.
Kingmaker.RuleSystem.Rulebook.CurrentContext.CurrentEvent = new object();
object[] deathArgs = [bonded, 110];
typeof(WOTR_MAKING_FRIENDS.MechanicsPatches.LifeTransferDamagePatch)
    .GetMethod("Prefix", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic).Invoke(null, deathArgs);
Check((int)deathArgs[1] == 110 && donor.HPLeft == 100, "Non-damage death or stat effects are not intercepted");

Console.WriteLine($"PASS: {checks} offline regression assertions. Production component logic linked directly; Unity integration not exercised.");
SynthesistRulesProbes.Run();
