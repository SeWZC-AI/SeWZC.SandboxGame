using SeWZC.WorldBox.Core.Runtime;

namespace SeWZC.WorldBox.Core;

public sealed partial class WorldEngine
{
    /// <summary>返回种族专属设施所需的种族，普通设施返回空值。</summary>
    /// <param name="kind">设施类别。</param>
    public static RaceKind? BuildingRace(BuildingKind kind)
    {
        return kind switch
        {
            BuildingKind.AssemblyHall or BuildingKind.TradeGuild => RaceKind.Human,
            BuildingKind.SacredGrove or BuildingKind.HerbGarden => RaceKind.Elf,
            BuildingKind.DwarvenForge or BuildingKind.MiningHall => RaceKind.Dwarf,
            BuildingKind.HuntingCamp or BuildingKind.WarDrum => RaceKind.Orc,
            _ => null,
        };
    }

    /// <summary>判断聚落是否具有建造该种族设施所需的达到劳动年龄的居民。</summary>
    /// <param name="settlementId">聚落 ID。</param>
    /// <param name="kind">设施类别。</param>
    public bool CanBuildRacialFacility(int settlementId, BuildingKind kind)
    {
        if (BuildingRace(kind) is not { } race)
            return true;
        var residents = ResidentsForLocalWork(settlementId);
        if (residents is null)
            return false;
        for (var index = 0; index < residents.Count; index++)
        {
            var resident = residents[index];
            if (resident.Value.SettlementId == settlementId && resident.Value.Race == race && resident.Value.Health > 0 && resident.Value.Age >= ResidentNeedsRules.MinimumWorkAge)
                return true;
        }

        return false;
    }

    private static ResourceStock RacialWorkInput(BuildingKind kind)
    {
        return kind == BuildingKind.MiningHall || kind == BuildingKind.HuntingCamp
            ? new ResourceStock { Food = .01 }
            : new ResourceStock { Food = .03, Water = .01 };
    }

    private bool RacialBuildingHasWork(Building building, StateReference<Resident> person)
    {
        if (person.Value.Age < ResidentNeedsRules.MinimumWorkAge || BuildingRace(building.Kind) != person.Value.Race ||
            !CanBuildRacialFacility(building.SettlementId, building.Kind))
            return false;
        if (building.Kind == BuildingKind.SacredGrove &&
            (!IsForestTerrain(Tiles[Index(building.X, building.Y)].Value.Terrain)
             || !Society.MagicEnabled || person.Value.MagicTalent < 25 || person.Value.MagicTraining >= 100))
            return false;
        if (building.Kind == BuildingKind.HerbGarden && FindLocalWorkPatient(building, true) is null)
            return false;
        if (building.Kind == BuildingKind.MiningHall && FindWorkshopResource(building, Profession.Miner) < 0)
            return false;
        if (building.Kind == BuildingKind.HuntingCamp &&
            !WildlifeSiteProductive(Tiles[Index(building.X, building.Y)], false))
            return false;
        var input = RacialWorkInput(building.Kind);
        return MissingResources(person.Value.Inventory, input) is null ||
               MissingResources(RequireTown(building.SettlementId).Value.Resources, input) is null;
    }

    private bool ActOnRacialWork(StateReference<Resident> person, StateReference<Settlement> home)
    {
        var goal = person.Value.Agent.Goal;
        if (goal.Kind is not (AgentGoalKind.Work or AgentGoalKind.Study or AgentGoalKind.TrainMagic))
            return false;
        var building = FindBuilding(goal.TargetEntityId);
        if (building is null || BuildingRace(building.Value.Kind) is null || ProductionRules.For(building.Value.Kind) is not null
            || !building.Value.IsCompleted || building.Value.IsUpgrading || building.Value.SettlementId != home.Value.Id)
            return false;
        if (!building.Value.Enabled || building.Value.Health < 50 || !RacialBuildingHasWork(building.Value, person))
        {
            person.Replace(person.Value.WithAgent(person.Value.Agent.WithGoal(goal = goal with { Reason = "种族、物资或现场工作条件未满足" })));
            person.Replace(person.Value.WithAgent(person.Value.Agent with { NextThinkTick = SimulationTick + 1 }));
            return true;
        }

        var input = RacialWorkInput(building.Value.Kind);
        if (MissingResources(person.Value.Inventory, input) is not null)
        {
            person.Replace(person.Value.WithAgent(person.Value.Agent.WithGoal(goal = goal with
            {
                TargetX = home.Value.X,
                TargetY = home.Value.Y,
                Reason = "实地返仓领取" + BuildingName(building.Value.Kind) + "的劳动物资",
            })));
            if (Distance(person.Value.X, person.Value.Y, home.Value.X, home.Value.Y) > 1)
            {
                MoveAgentTowards(person, home.Value.X, home.Value.Y);
                person.Replace(person.Value.WithActivity(ResidentActivity.Delivering));
                return true;
            }

            foreach (var kind in ResourceStock.Kinds)
            {
                var amount = Math.Max(0, input.Get(kind) * 8 - person.Value.Inventory.Get(kind));
                amount = Math.Min(amount, home.Value.Resources.Get(kind));
                home.Replace(home.Value.WithResources(home.Value.Resources.WithAmount(kind, home.Value.Resources.Get(kind) - amount)));
                person.Replace(person.Value.WithInventory(person.Value.Inventory.WithAmount(kind, person.Value.Inventory.Get(kind) + amount)));
            }
        }

        person.Replace(person.Value.WithAgent(person.Value.Agent.WithGoal(goal = goal with { TargetX = building.Value.X, TargetY = building.Value.Y })));
        if (Distance(person.Value.X, person.Value.Y, building.Value.X, building.Value.Y) > 0)
        {
            MoveAgentTowards(person, building.Value.X, building.Value.Y);
            person.Replace(person.Value.WithActivity(ResidentActivity.Delivering));
            return true;
        }

        if (TryWorkAtBuilding(person))
            person.Replace(person.Value.WithActivity(ResidentActivity.Working));
        if (building.Value.Kind is BuildingKind.MiningHall or BuildingKind.HuntingCamp &&
            person.Value.Inventory.Food + person.Value.Inventory.Stone + person.Value.Inventory.Ore > 2)
        {
            var returning = new AgentGoal
            {
                Kind = AgentGoalKind.ReturnHome,
                TargetX = home.Value.X,
                TargetY = home.Value.Y,
                TargetSettlementId = home.Value.Id,
                StartedTick = SimulationTick,
                Reason = "亲自运回特殊设施的劳动产出",
            };
            ChangeWorkReservation(person.Value.Agent.Goal, returning);
            person.Replace(person.Value with
            {
                Agent = person.Value.Agent with { Goal = returning, NextThinkTick = SimulationTick + 24 },
            });
        }

        return true;
    }

    private bool WorkRacialBuilding(StateReference<Building> building, StateReference<Resident> person, double effort)
    {
        if (!RacialBuildingHasWork(building.Value, person) || person.Value.X != building.Value.X || person.Value.Y != building.Value.Y)
            return false;
        var input = RacialWorkInput(building.Value.Kind);
        if (MissingResources(person.Value.Inventory, input) is not null)
            return false;
        person.Replace(person.Value.WithInventory(Spend(person.Value.Inventory, input)));
        switch (building.Value.Kind)
        {
            case BuildingKind.AssemblyHall:
                foreach (var other in _citizens[building.Value.SettlementId])
                    if (other.Value.Health > 0 && Distance(other.Value.X, other.Value.Y, building.Value.X, building.Value.Y) <= 2)
                        other.Replace(other.Value.WithAgent(other.Value.Agent with { SocialNeed = Math.Max(0, other.Value.Agent.SocialNeed - effort) }));
                return true;
            case BuildingKind.TradeGuild:
                return true; // 运营效果由实际到岗人员提供，不能仅凭建筑建成启用。
            case BuildingKind.SacredGrove:
                person.Replace(person.Value with { MagicTraining = Math.Min(100, person.Value.MagicTraining + .1 * effort * Rules.MagicRate) });
                person.Replace(person.Value.WithMana(Math.Min(100, person.Value.Mana + .3 * effort)));
                return true;
            case BuildingKind.HerbGarden:
                var patient = FindLocalWorkPatient(building.Value);
                if (patient is null)
                    return false;
                patient.Replace(patient.Value.WithHealth(Math.Min(100, patient.Value.Health + .6 * effort)));
                patient.Replace(patient.Value.WithSicknessTicks(Math.Max(0, patient.Value.SicknessTicks
                    - ServiceDurationTicks(person.Value, building.Value, SimulationTime.TicksPerDay))));
                return true;
            case BuildingKind.MiningHall:
                var source = FindWorkshopResource(building.Value, Profession.Miner);
                if (source < 0)
                    return false;
                var tile = Tiles[source];
                var yields = TerrainRules.For(tile.Value.Terrain);
                var amount = Math.Min(tile.Value.ResourceAmount,
                    .3 * effort * Rules.GatheringRate * GatheringTerritoryMultiplier(person.Value.SettlementId, person.Value.NationId, tile.Value));
                tile.Replace(tile.Value.WithResourceAmount(tile.Value.ResourceAmount - (amount)));
                person.Replace(person.Value.WithInventory(person.Value.Inventory with
                {
                    Stone = person.Value.Inventory.Stone + amount * yields.StoneYield,
                    Ore = person.Value.Inventory.Ore + amount * yields.OreYield,
                }));
                person.Replace(person.Value.WithInventory(person.Value.Inventory.Clamp(1_000_000)));
                RecordHarvest(tile, amount * (yields.StoneYield + yields.OreYield));
                return true;
            case BuildingKind.HuntingCamp:
                var ground = Tiles[Index(person.Value.X, person.Value.Y)];
                var prey = EdibleAnimal(ground);
                if (prey == WildlifeKind.None)
                    return false;
                var caught = WildlifeHarvestAmount(ground, prey,
                    .25 * effort * Rules.GatheringRate * GatheringTerritoryMultiplier(person.Value.SettlementId, person.Value.NationId, ground.Value));
                ground.Replace(ground.Value.WithAnimalPopulation(prey, ground.Value.AnimalPopulation(prey) - caught));
                var food = caught * AnimalRules.For(prey).BodyMass;
                person.Replace(person.Value.WithInventory(person.Value.Inventory with { Food = Math.Min(1_000_000, person.Value.Inventory.Food + food) }));
                RecordHarvest(ground, food);
                return true;
            case BuildingKind.WarDrum:
                foreach (var other in _citizens[building.Value.SettlementId])
                    if (other.Value.Health > 0 && Distance(other.Value.X, other.Value.Y, building.Value.X, building.Value.Y) <= 2)
                        other.Replace(other.Value.WithAgent(other.Value.Agent with { Fatigue = Math.Max(0, other.Value.Agent.Fatigue - effort) }));
                foreach (var army in Armies)
                    if (army.Value.NationId == person.Value.NationId && Distance(army.Value.X, army.Value.Y, building.Value.X, building.Value.Y) <= 2)
                        army.Replace(army.Value with { Morale = Math.Min(100, army.Value.Morale + .3 * effort) });
                return true;
            default:
                return false;
        }
    }

    private double RacialTravelBonus(StateReference<Resident> person, int x, int y)
    {
        if (person.Value.Profession != Profession.Trader)
            return 1;
        var bonus = 1d;
        foreach (var building in Buildings)
            if (building.Value.SettlementId == person.Value.SettlementId && building.Value.Kind == BuildingKind.TradeGuild
                                                             && Distance(x, y, building.Value.X, building.Value.Y) <= 3 &&
                                                             IsFacilityOperating(building.Value))
                bonus = Math.Max(bonus, 1.15 * building.Value.Efficiency);
        return bonus;
    }

    /// <summary>判断地形是否属于森林、疏林或雨林。</summary>
    /// <param name="terrain">地形类别。</param>
    public static bool IsForestTerrain(TerrainType terrain)
    {
        return terrain is TerrainType.Forest or TerrainType.Woodland or TerrainType.Rainforest;
    }

    private static bool PreserveBuildingForest(BuildingKind kind)
    {
        return kind is BuildingKind.LumberCamp or BuildingKind.SacredGrove or BuildingKind.HuntingCamp
            or BuildingKind.Pasture;
    }
}
