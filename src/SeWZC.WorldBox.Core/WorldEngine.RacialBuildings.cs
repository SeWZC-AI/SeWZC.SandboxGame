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
               || State.Residents.Any(p =>
                   p.SettlementId == settlementId && p.Race == race && p.Health > 0 && p.Age >= 14);
    }

    private static ResourceStock RacialWorkInput(BuildingKind kind)
    {
        return kind == BuildingKind.MiningHall || kind == BuildingKind.HuntingCamp
            ? new ResourceStock { Food = .01 }
            : new ResourceStock { Food = .03, Water = .01 };
    }

    private bool RacialBuildingHasWork(Building building, Resident person)
    {
        if (person.Age < 14 || BuildingRace(building.Kind) != person.Race ||
            !CanBuildRacialFacility(building.SettlementId, building.Kind)) return false;
        if (building.Kind == BuildingKind.SacredGrove &&
            (!IsForestTerrain(State.Tiles[Index(building.X, building.Y)].Terrain)
             || !State.Society.MagicEnabled || person.MagicTalent < 25 || person.MagicTraining >= 100)) return false;
        if (building.Kind == BuildingKind.HerbGarden && FindLocalWorkPatient(building, true) is null) return false;
        if (building.Kind == BuildingKind.MiningHall && FindWorkshopResource(building, Profession.Miner) < 0)
            return false;
        if (building.Kind == BuildingKind.HuntingCamp &&
            !WildlifeSiteProductive(State.Tiles[Index(building.X, building.Y)], false)) return false;
        var input = RacialWorkInput(building.Kind);
        return MissingResources(person.Inventory, input) is null ||
               MissingResources(RequireTown(building.SettlementId).Resources, input) is null;
    }

    private bool ActOnRacialWork(Resident person, Settlement home)
    {
        var goal = person.Agent.Goal;
        if (goal.Kind is not (AgentGoalKind.Work or AgentGoalKind.Study or AgentGoalKind.TrainMagic)) return false;
        var building = FindBuilding(goal.TargetEntityId);
        if (building is null || BuildingRace(building.Kind) is null || ProductionRules.For(building.Kind) is not null
            || !building.IsCompleted || building.IsUpgrading || building.SettlementId != home.Id) return false;
        if (!building.Enabled || building.Health < 50 || !RacialBuildingHasWork(building, person))
        {
            goal.Reason = "种族、物资或现场工作条件未满足";
            person.Agent.NextThinkTick = State.Tick + 1;
            return true;
        }

        var input = RacialWorkInput(building.Kind);
        if (MissingResources(person.Inventory, input) is not null)
        {
            goal.TargetX = home.X;
            goal.TargetY = home.Y;
            goal.Reason = "实地返仓领取" + BuildingName(building.Kind) + "的劳动物资";
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
                home.Resources.Set(kind, home.Resources.Get(kind) - amount);
                person.Inventory.Set(kind, person.Inventory.Get(kind) + amount);
            }
        }

        goal.TargetX = building.X;
        goal.TargetY = building.Y;
        if (Distance(person.X, person.Y, building.X, building.Y) > 0)
        {
            MoveAgentTowards(person, building.X, building.Y);
            person.Activity = ResidentActivity.Delivering;
            return true;
        }

        if (TryWorkAtBuilding(person)) person.Activity = ResidentActivity.Working;
        if (building.Kind is BuildingKind.MiningHall or BuildingKind.HuntingCamp &&
            person.Inventory.Food + person.Inventory.Stone + person.Inventory.Ore > 2)
        {
            person.Agent.Goal = new AgentGoal
            {
                Kind = AgentGoalKind.ReturnHome,
                TargetX = home.X,
                TargetY = home.Y,
                TargetSettlementId = home.Id,
                StartedTick = State.Tick,
                Reason = "亲自运回特殊设施的劳动产出",
            };
            person.Agent.NextThinkTick = State.Tick + 24;
        }

        return true;
    }

    private bool WorkRacialBuilding(Building building, Resident person, double effort)
    {
        if (!RacialBuildingHasWork(building, person) || person.X != building.X || person.Y != building.Y) return false;
        var input = RacialWorkInput(building.Kind);
        if (MissingResources(person.Inventory, input) is not null) return false;
        Spend(person.Inventory, input);
        switch (building.Kind)
        {
            case BuildingKind.AssemblyHall:
                foreach (var other in _citizens[building.SettlementId])
                    if (other.Health > 0 && Distance(other.X, other.Y, building.X, building.Y) <= 2)
                        other.Agent.SocialNeed = Math.Max(0, other.Agent.SocialNeed - effort);
                return true;
            case BuildingKind.TradeGuild: return true; // 运营效果由实际到岗人员提供，不能仅凭建筑建成启用。
            case BuildingKind.SacredGrove:
                person.MagicTraining = Math.Min(100, person.MagicTraining + .1 * effort * State.Rules.MagicRate);
                person.Mana = Math.Min(100, person.Mana + .3 * effort);
                return true;
            case BuildingKind.HerbGarden:
                var patient = FindLocalWorkPatient(building);
                if (patient is null) return false;
                patient.Health = Math.Min(100, patient.Health + .6 * effort);
                patient.SicknessTicks = Math.Max(0, patient.SicknessTicks - 1);
                return true;
            case BuildingKind.MiningHall:
                var source = FindWorkshopResource(building, Profession.Miner);
                if (source < 0) return false;
                var tile = State.Tiles[source];
                var yields = TerrainRules.For(tile.Terrain);
                var amount = Math.Min(tile.ResourceAmount,
                    .3 * effort * State.Rules.GatheringRate * GatheringTerritoryMultiplier(person, tile));
                tile.ResourceAmount -= amount;
                person.Inventory.Stone += amount * yields.StoneYield;
                person.Inventory.Ore += amount * yields.OreYield;
                CapResources(person.Inventory);
                RecordHarvest(tile, amount * (yields.StoneYield + yields.OreYield));
                return true;
            case BuildingKind.HuntingCamp:
                var ground = State.Tiles[Index(person.X, person.Y)];
                var prey = EdibleAnimal(ground);
                if (prey == WildlifeKind.None) return false;
                var caught = WildlifeHarvestAmount(ground, prey,
                    .25 * effort * State.Rules.GatheringRate * GatheringTerritoryMultiplier(person, ground));
                ground.SetAnimalPopulation(prey, ground.AnimalPopulation(prey) - caught);
                var food = caught * AnimalRules.For(prey).BodyMass;
                person.Inventory.Food = Math.Min(1_000_000, person.Inventory.Food + food);
                RecordHarvest(ground, food);
                return true;
            case BuildingKind.WarDrum:
                foreach (var other in _citizens[building.SettlementId])
                    if (other.Health > 0 && Distance(other.X, other.Y, building.X, building.Y) <= 2)
                        other.Agent.Fatigue = Math.Max(0, other.Agent.Fatigue - effort);
                foreach (var army in State.Armies)
                    if (army.NationId == person.NationId && Distance(army.X, army.Y, building.X, building.Y) <= 2)
                        army.Morale = Math.Min(100, army.Morale + .3 * effort);
                return true;
            default: return false;
        }
    }

    private double RacialTravelBonus(Resident person, int x, int y)
    {
        if (person.Profession != Profession.Trader) return 1;
        var bonus = 1d;
        foreach (var building in State.Society.Buildings)
            if (building.SettlementId == person.SettlementId && building.Kind == BuildingKind.TradeGuild
                                                             && Distance(x, y, building.X, building.Y) <= 3 &&
                                                             IsFacilityOperating(building))
                bonus = Math.Max(bonus, 1.15 * building.Efficiency);
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
