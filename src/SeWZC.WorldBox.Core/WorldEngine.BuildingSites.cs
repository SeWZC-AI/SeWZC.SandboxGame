namespace SeWZC.WorldBox.Core;

public sealed partial class WorldEngine
{
    private bool VisibleWorkSiteReachable(Resident person, int x, int y, bool adjacent)
    {
        var search = 0;
        if (VisibleSiteReachable(person, Index(x, y), ref search, TravelMode.Foot)) return true;
        if (!adjacent) return false;
        foreach (var (dx, dy) in Directions)
            if (Walkable(x + dx, y + dy, person.Race) && !IsWaterTerrain(State.Tiles[Index(x + dx, y + dy)].Terrain)
                && State.Tiles[Index(x + dx, y + dy)].FireTicks == 0
                && VisibleSiteReachable(person, Index(x + dx, y + dy), ref search, TravelMode.Foot)) return true;
        return false;
    }

    private bool AutomaticSiteUseful(BuildingKind kind, int index)
    {
        var tile = State.Tiles[index];
        if (kind is BuildingKind.Farm or BuildingKind.AutomatedFarm or BuildingKind.RunicGarden && (tile.Fertility < 25 || tile.DroughtTicks > 0)) return false;
        if (IsMaterialFacility(kind) || kind == BuildingKind.MiningHall)
            return Circle(index % State.Width, index / State.Width, 1).Any(i => i != index && State.Tiles[i].ResourceAmount > 0
                && (kind == BuildingKind.LumberCamp ? IsForestTerrain(State.Tiles[i].Terrain)
                    : kind is BuildingKind.Quarry or BuildingKind.MiningHall ? TerrainRules.For(State.Tiles[i].Terrain).StoneYield + TerrainRules.For(State.Tiles[i].Terrain).OreYield > 0
                    : TerrainRules.For(State.Tiles[i].Terrain).WoodYield + TerrainRules.For(State.Tiles[i].Terrain).StoneYield + TerrainRules.For(State.Tiles[i].Terrain).OreYield > 0));
        if (kind == BuildingKind.HuntingCamp) return EdibleAnimal(tile) != WildlifeKind.None;
        return true;
    }

    private string BuildingPurpose(Settlement town, BuildingKind kind) => kind switch
    {
        BuildingKind.Farm or BuildingKind.AutomatedFarm or BuildingKind.RunicGarden => $"当地粮食库存 {town.Resources.Food:0.#}，为 {town.Population} 名居民增加粮食供给",
        BuildingKind.Housing => $"人口 {town.Population}，住房容量 {GetHousingCapacity(town.Id)}，补充居住空间",
        BuildingKind.Academy => "提供推进当地发展路线所需的研究岗位",
        BuildingKind.Workshop or BuildingKind.LumberCamp or BuildingKind.Quarry or BuildingKind.MiningHall => "利用已观察到的木石矿来源，为当地建设与研究采集材料",
        BuildingKind.Well => $"当地存水 {town.Resources.Water:0.#}，集中收集地块实际供水",
        BuildingKind.Dock or BuildingKind.Shipyard => "利用已观察到的近岸水域，支持舟船运输与捕鱼",
        BuildingKind.HuntingCamp => "利用眼前可食动物补充食物",
        _ => AdvancementRules.For(kind) is { } recipe ? $"已掌握{ResearchName(recipe.Research)}，建立{ResourceStock.Name(recipe.Output)}生产岗位" : BuildingDescription(kind)
    };

    private string BuildingSiteReason(Settlement town, BuildingKind kind, int x, int y) =>
        $"选址 {x}, {y}：距中心 {Distance(x, y, town.X, town.Y)} 格；"
        + (IsPublicInfrastructure(kind) ? "沿实际任务的通行路线建设" : IsWaterfrontBuilding(kind) ? "紧贴本城镇占领陆岸，可从岸边施工" : "本城镇占领地，附近居民有可达施工位置")
        + (kind is BuildingKind.Farm or BuildingKind.AutomatedFarm or BuildingKind.RunicGarden ? $"；肥力 {State.Tiles[Index(x, y)].Fertility}/100" : IsMaterialFacility(kind) ? "；紧邻实际可采材料" : "");

    private int BuildPlannedFacility(Settlement town, BuildingKind kind, int x, int y, string reason, BridgeDirection? direction = null, int level = 1)
    {
        var siteReason = BuildingSiteReason(town, kind, x, y);
        var id = BuildFacility(town.Id, kind, x, y, direction, level);
        var building = FindBuilding(id)!;
        building.PlanningReason = reason;
        building.SiteReason = siteReason;
        return id;
    }
    public static bool IsWaterfrontBuilding(BuildingKind kind) => kind is BuildingKind.Dock or BuildingKind.Shipyard;
    public static bool IsPublicInfrastructure(BuildingKind kind) => kind is BuildingKind.Bridge or BuildingKind.MountainPass;
    private static bool IsMaterialFacility(BuildingKind kind) => kind is BuildingKind.Workshop or BuildingKind.LumberCamp or BuildingKind.Quarry;

    public double BuildingSiteScore(int settlementId, BuildingKind kind, int x, int y)
    {
        if (!_settlements.TryGetValue(settlementId, out var town) || !InBounds(x, y)) return double.NegativeInfinity;
        var tile = State.Tiles[Index(x, y)];
        var score = -Distance(x, y, town.X, town.Y) * .25 + (tile.RoadLevel > 0 ? 1 : 0);
        if (kind is BuildingKind.Farm or BuildingKind.AutomatedFarm or BuildingKind.RunicGarden)
            score += tile.Fertility / 10d - (tile.DroughtTicks > 0 ? 8 : 0);
        if (IsMaterialFacility(kind))
            foreach (var i in Circle(x, y, 1))
            {
                if (i == Index(x, y)) continue;
                var source = State.Tiles[i]; var yields = TerrainRules.For(source.Terrain);
                var yield = kind == BuildingKind.LumberCamp ? yields.WoodYield : kind == BuildingKind.Quarry ? yields.StoneYield + yields.OreYield : Math.Max(yields.WoodYield, yields.StoneYield + yields.OreYield);
                score += Math.Min(1, source.ResourceAmount / 25) * yield * 12;
            }
        if (kind == BuildingKind.Well) score += Math.Min(1, DailyWaterYield(tile)) * 15;
        if (kind == BuildingKind.Reservoir)
            score += Circle(x, y, 1).Where(i => i == Index(x, y) || IsFreshWater(State.Tiles[i]))
                .Select(i => Math.Min(3, DailyWaterYield(State.Tiles[i])) * 20).DefaultIfEmpty().Max();
        if (kind is BuildingKind.SignalTower or BuildingKind.Watchtower) score += tile.Terrain is TerrainType.Hills or TerrainType.Mountain ? 5 : 0;
        if (kind == BuildingKind.ArcaneSanctum) score += TerrainRules.For(tile.Terrain).ManaRate * 4;
        foreach (var building in State.Society.Buildings)
        {
            var distance = Distance(x, y, building.X, building.Y);
            if (distance <= 2) score -= distance == 1 ? 4 : 1;
            if (kind is BuildingKind.Market or BuildingKind.Granary or BuildingKind.Infirmary && distance <= 4) score += 1.5;
        }
        return score;
    }

    private int BestBuildingSite(Settlement town, BuildingKind kind, bool founding = false)
    {
        var radius = founding ? (_creatingDemo ? 3 : 6) : Math.Max(8, town.MaxClaimRadius);
        return Circle(town.X, town.Y, radius)
            .Where(i => FacilityPlacementError(town.Id, kind, i % State.Width, i / State.Width, founding, founding: founding) is null
                && (founding || AutomaticSiteUseful(kind, i) && _citizens[town.Id].Any(p => p.Health > 0 && p.Age >= 14 && p.ArmyId == 0
                    && Distance(p.X, p.Y, i % State.Width, i / State.Width) <= 6
                    && VisibleWorkSiteReachable(p, i % State.Width, i / State.Width, adjacent: true))))
            .OrderByDescending(i => BuildingSiteScore(town.Id, kind, i % State.Width, i / State.Width)).ThenBy(i => i).FirstOrDefault(-1);
    }

    private bool BuildingGroundOwned(Building building) => IsPublicInfrastructure(building.Kind)
        || State.Tiles[Index(building.X, building.Y)].ClaimedSettlementId == building.SettlementId
        && _settlements.TryGetValue(building.SettlementId, out var town)
        && State.Tiles[Index(building.X, building.Y)].NationId == town.NationId;

    private bool PassiveFacility(Building building) => building.Kind is BuildingKind.Housing or BuildingKind.Granary or BuildingKind.Watchtower;

    public int GetHousingCapacity(int settlementId)
    {
        var town = RequireTown(settlementId);
        return Math.Min(20_000, town.Housing + State.Society.Buildings
            .Where(b => b.SettlementId == settlementId && b.Kind == BuildingKind.Housing && IsFacilityOperating(b)).Sum(b => b.Level * 20));
    }

    private double GranaryRestBonus(int settlementId)
    {
        IEnumerable<Building>? buildings = _localWorkQueriesActive ? _localWorkBuildings.GetValueOrDefault(settlementId) : State.Society.Buildings;
        var bonus = 1d;
        if (buildings is null) return bonus;
        foreach (var building in buildings)
            if (building.SettlementId == settlementId && building.Kind == BuildingKind.Granary && IsFacilityOperating(building))
                bonus = Math.Max(bonus, 1 + .1 * building.Level);
        return bonus;
    }

    public double BoatTravelMultiplier(int x, int y, int nationId)
    {
        var bonus = 1d;
        foreach (var dock in State.Society.Buildings)
            if (dock.Kind == BuildingKind.Dock && IsFacilityOperating(dock) && RequireTown(dock.SettlementId).NationId == nationId
                && Distance(x, y, dock.X, dock.Y) <= 3) bonus = Math.Max(bonus, 1 + .15 * dock.Level);
        return bonus;
    }
}
