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

    /// <summary>判断聚落是否具有建造该种族设施所需的成年居民。</summary>
    /// <param name="settlementId">聚落 ID。</param>
    /// <param name="kind">设施类别。</param>
    public bool CanBuildRacialFacility(int settlementId, BuildingKind kind)
    {
        return BuildingRace(kind) is not { } race
               || ResidentsForLocalWork(settlementId)?.Any(p =>
                   p.SettlementId == settlementId && p.Race == race && p.Health > 0 && p.Age >= 14) == true;
    }

    private static ResourceStock RacialWorkInput(BuildingKind kind)
    {
        return kind == BuildingKind.MiningHall || kind == BuildingKind.HuntingCamp
            ? new ResourceStock { Food = .01 }
            : new ResourceStock { Food = .03, Water = .01 };
    }

    private bool RacialBuildingHasWork(Building building, ResidentCursor person)
    {
        if (person.Age < 14 || BuildingRace(building.Kind) != person.Race ||
            !CanBuildRacialFacility(building.SettlementId, building.Kind))
            return false;
        if (building.Kind == BuildingKind.SacredGrove &&
            (!IsForestTerrain(Current.Tiles[Index(building.X, building.Y)].Terrain)
             || !Current.Society.MagicEnabled || person.MagicTalent < 25 || person.MagicTraining >= 100))
            return false;
        if (building.Kind == BuildingKind.HerbGarden && FindLocalWorkPatient(building, true) is null)
            return false;
        if (building.Kind == BuildingKind.MiningHall && FindWorkshopResource(building, Profession.Miner) < 0)
            return false;
        if (building.Kind == BuildingKind.HuntingCamp &&
            !WildlifeSiteProductive(Current.Tiles[Index(building.X, building.Y)], false))
            return false;
        var input = RacialWorkInput(building.Kind);
        return MissingResources(person.Inventory, input) is null ||
               MissingResources(RequireTown(building.SettlementId).Resources, input) is null;
    }

    private bool ActOnRacialWork(ResidentCursor person, SettlementCursor home)
    {
        var goal = person.Agent.Goal;
        if (goal.Kind is not (AgentGoalKind.Work or AgentGoalKind.Study or AgentGoalKind.TrainMagic))
            return false;
        var building = FindBuilding(goal.TargetEntityId);
        if (building is null || BuildingRace(building.Value.Kind) is null || ProductionRules.For(building.Value.Kind) is not null
            || !building.Value.IsCompleted || building.Value.IsUpgrading || building.Value.SettlementId != home.Id)
            return false;
        if (!building.Value.Enabled || building.Value.Health < 50 || !RacialBuildingHasWork(building.Value, person))
        {
            person.Agent = person.Agent.WithGoal(goal = goal with { Reason = "种族、物资或现场工作条件未满足" });
            person.Agent = person.Agent with { NextThinkTick = Current.Tick + 1 };
            return true;
        }

        var input = RacialWorkInput(building.Value.Kind);
        if (MissingResources(person.Inventory, input) is not null)
        {
            person.Agent = person.Agent.WithGoal(goal = goal with
            {
                TargetX = home.X,
                TargetY = home.Y,
                Reason = "实地返仓领取" + BuildingName(building.Value.Kind) + "的劳动物资",
            });
            if (Distance(person.X, person.Y, home.X, home.Y) > 1)
            {
                MoveAgentTowards(person, home.X, home.Y);
                person.Activity = ResidentActivity.Delivering;
                return true;
            }

            foreach (var kind in ResourceStock.Kinds)
            {
                var amount = Math.Max(0, input.Get(kind) * 8 - person.Inventory.Get(kind));
                amount = Math.Min(amount, home.Resources.Get(kind));
                home.UpdateResources(home.Resources.WithAmount(kind, home.Resources.Get(kind) - amount));
                person.Inventory = person.Inventory.WithAmount(kind, person.Inventory.Get(kind) + amount);
            }
        }

        person.Agent = person.Agent.WithGoal(goal = goal with { TargetX = building.Value.X, TargetY = building.Value.Y });
        if (Distance(person.X, person.Y, building.Value.X, building.Value.Y) > 0)
        {
            MoveAgentTowards(person, building.Value.X, building.Value.Y);
            person.Activity = ResidentActivity.Delivering;
            return true;
        }

        if (TryWorkAtBuilding(person))
            person.Activity = ResidentActivity.Working;
        if (building.Value.Kind is BuildingKind.MiningHall or BuildingKind.HuntingCamp &&
            person.Inventory.Food + person.Inventory.Stone + person.Inventory.Ore > 2)
        {
            var returning = new AgentGoal
            {
                Kind = AgentGoalKind.ReturnHome,
                TargetX = home.X,
                TargetY = home.Y,
                TargetSettlementId = home.Id,
                StartedTick = Current.Tick,
                Reason = "亲自运回特殊设施的劳动产出",
            };
            ChangeWorkReservation(person.Agent.Goal, returning);
            person.Replace(person.Value with
            {
                Agent = person.Agent with { Goal = returning, NextThinkTick = Current.Tick + 24 },
            });
        }

        return true;
    }

    private bool WorkRacialBuilding(StateReference<Building> building, ResidentCursor person, double effort)
    {
        if (!RacialBuildingHasWork(building.Value, person) || person.X != building.Value.X || person.Y != building.Value.Y)
            return false;
        var input = RacialWorkInput(building.Value.Kind);
        if (MissingResources(person.Inventory, input) is not null)
            return false;
        person.Inventory = Spend(person.Inventory, input);
        switch (building.Value.Kind)
        {
            case BuildingKind.AssemblyHall:
                foreach (var other in _citizens[building.Value.SettlementId])
                    if (other.Health > 0 && Distance(other.X, other.Y, building.Value.X, building.Value.Y) <= 2)
                        other.Agent = other.Agent with { SocialNeed = Math.Max(0, other.Agent.SocialNeed - effort) };
                return true;
            case BuildingKind.TradeGuild:
                return true; // 运营效果由实际到岗人员提供，不能仅凭建筑建成启用。
            case BuildingKind.SacredGrove:
                person.Replace(person.Value with { MagicTraining = Math.Min(100, person.MagicTraining + .1 * effort * Current.Rules.MagicRate) });
                person.Mana = Math.Min(100, person.Mana + .3 * effort);
                return true;
            case BuildingKind.HerbGarden:
                var patient = FindLocalWorkPatient(building.Value);
                if (patient is null)
                    return false;
                patient.Health = Math.Min(100, patient.Health + .6 * effort);
                patient.SicknessTicks = Math.Max(0, patient.SicknessTicks - SimulationTime.TicksPerDay);
                return true;
            case BuildingKind.MiningHall:
                var source = FindWorkshopResource(building.Value, Profession.Miner);
                if (source < 0)
                    return false;
                var tile = Current.Tiles[source];
                var yields = TerrainRules.For(tile.Terrain);
                var amount = Math.Min(tile.ResourceAmount,
                    .3 * effort * Current.Rules.GatheringRate * GatheringTerritoryMultiplier(person.SettlementId, person.NationId, tile.Value));
                tile.ResourceAmount -= amount;
                person.Inventory = person.Inventory with
                {
                    Stone = person.Inventory.Stone + amount * yields.StoneYield,
                    Ore = person.Inventory.Ore + amount * yields.OreYield,
                };
                person.Inventory = person.Inventory.Clamp(1_000_000);
                RecordHarvest(tile, amount * (yields.StoneYield + yields.OreYield));
                return true;
            case BuildingKind.HuntingCamp:
                var ground = Current.Tiles[Index(person.X, person.Y)];
                var prey = EdibleAnimal(ground);
                if (prey == WildlifeKind.None)
                    return false;
                var caught = WildlifeHarvestAmount(ground, prey,
                    .25 * effort * Current.Rules.GatheringRate * GatheringTerritoryMultiplier(person.SettlementId, person.NationId, ground.Value));
                ground.SetAnimalPopulation(prey, ground.AnimalPopulation(prey) - caught);
                var food = caught * AnimalRules.For(prey).BodyMass;
                person.Inventory = person.Inventory with { Food = Math.Min(1_000_000, person.Inventory.Food + food) };
                RecordHarvest(ground, food);
                return true;
            case BuildingKind.WarDrum:
                foreach (var other in _citizens[building.Value.SettlementId])
                    if (other.Health > 0 && Distance(other.X, other.Y, building.Value.X, building.Value.Y) <= 2)
                        other.Agent = other.Agent with { Fatigue = Math.Max(0, other.Agent.Fatigue - effort) };
                foreach (var army in Current.Armies)
                    if (army.Value.NationId == person.NationId && Distance(army.Value.X, army.Value.Y, building.Value.X, building.Value.Y) <= 2)
                        army.Replace(army.Value with { Morale = Math.Min(100, army.Value.Morale + .3 * effort) });
                return true;
            default:
                return false;
        }
    }

    private double RacialTravelBonus(ResidentCursor person, int x, int y)
    {
        if (person.Profession != Profession.Trader)
            return 1;
        var bonus = 1d;
        foreach (var building in Current.Buildings)
            if (building.Value.SettlementId == person.SettlementId && building.Value.Kind == BuildingKind.TradeGuild
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
