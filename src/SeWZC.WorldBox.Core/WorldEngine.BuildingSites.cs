using SeWZC.WorldBox.Core.Runtime;

namespace SeWZC.WorldBox.Core;

public sealed partial class WorldEngine
{
    /// <summary>每级运营住宅提供的居民容量。</summary>
    public const int HousingCapacityPerLevel = 80;

    private bool VisibleWorkSiteReachable(ResidentCursor person, int x, int y, bool adjacent)
    {
        var search = 0;
        if (VisibleSiteReachable(person, Index(x, y), ref search, TravelMode.Foot))
            return true;
        if (!adjacent)
            return false;
        foreach (var (dx, dy) in Directions)
            if (Walkable(x + dx, y + dy, person.Race) && !IsWaterTerrain(Tiles[Index(x + dx, y + dy)].Value.Terrain)
                                                      && Tiles[Index(x + dx, y + dy)].Value.FireTicks == 0
                                                      && VisibleSiteReachable(person, Index(x + dx, y + dy), ref search,
                                                          TravelMode.Foot))
                return true;
        return false;
    }

    private bool AutomaticSiteUseful(BuildingKind kind, int index)
    {
        var tile = Tiles[index];
        if (kind is BuildingKind.Farm or BuildingKind.AutomatedFarm or BuildingKind.RunicGarden &&
            (tile.Value.Fertility < 25 || tile.Value.DroughtTicks > 0))
            return false;
        if (IsMaterialFacility(kind) || kind == BuildingKind.MiningHall)
        {
            return Circle(index % Width, index / Width, 1).Any(i => i != index &&
                Tiles[i].Value.ResourceAmount > 0
                && (kind == BuildingKind.LumberCamp
                    ? IsForestTerrain(Tiles[i].Value.Terrain)
                    : kind is BuildingKind.Quarry or BuildingKind.MiningHall
                        ? TerrainRules.For(Tiles[i].Value.Terrain).StoneYield +
                        TerrainRules.For(Tiles[i].Value.Terrain).OreYield > 0
                        : TerrainRules.For(Tiles[i].Value.Terrain).WoodYield +
                        TerrainRules.For(Tiles[i].Value.Terrain).StoneYield +
                        TerrainRules.For(Tiles[i].Value.Terrain).OreYield > 0));
        }

        if (IsHusbandry(kind))
        {
            return HusbandryStockAt(index % Width, index / Width, kind == BuildingKind.Aquaculture)
                .Source >= 0;
        }

        if (kind == BuildingKind.Well && WellWaterYield(tile.Value) < .1)
            return false;
        if (kind == BuildingKind.Reservoir && !Circle(index % Width, index / Width, 1)
                .Any(source => IsFreshWater(Tiles[source].Value) && Tiles[source].Value.FireTicks == 0))
            return false;
        if (kind == BuildingKind.HuntingCamp)
            return EdibleAnimal(tile) != WildlifeKind.None;
        return true;
    }

    private string BuildingPurpose(StateReference<Settlement> town, BuildingKind kind)
    {
        return kind switch
        {
            BuildingKind.Farm or BuildingKind.AutomatedFarm or BuildingKind.RunicGarden =>
                $"当地粮食库存 {town.Value.Resources.Food:0.#}，为 {town.Value.Population} 名居民增加粮食供给",
            BuildingKind.Workshop or BuildingKind.LumberCamp or BuildingKind.Quarry or BuildingKind.MiningHall =>
                "利用已观察到的木石矿来源，为当地建设与研究采集材料",
            BuildingKind.Academy => "提供推进当地发展路线所需的研究岗位",
            BuildingKind.Dock or BuildingKind.Shipyard => "利用已观察到的近岸水域，支持舟船运输与捕鱼",
            BuildingKind.Well => $"当地存水 {town.Value.Resources.Water:0.#}，根据地块供水量打水返仓",
            BuildingKind.Housing => $"人口 {town.Value.Population}，住房容量 {GetHousingCapacity(town.Value.Id)}，补充居住空间",
            BuildingKind.HuntingCamp => "利用眼前可食动物补充食物",
            _ => ProductionRules.For(kind) is { } recipe
                ? $"已掌握{recipe.Research.Name}，建立{ResourceStock.Name(recipe.Output)}生产岗位"
                : BuildingDescription(kind),
        };
    }

    private string BuildingSiteReason(StateReference<Settlement> town, BuildingKind kind, int x, int y)
    {
        return $"选址 {x}, {y}：距中心 {Distance(x, y, town.Value.X, town.Value.Y)} 格；"
               + (IsPublicInfrastructure(kind) ? "沿实际任务的通行路线建设" :
                   IsWaterfrontBuilding(kind) ? "紧贴本城镇占领陆岸，可从岸边施工" : "本城镇占领地，附近居民有可达施工位置")
               + (kind is BuildingKind.Farm or BuildingKind.AutomatedFarm or BuildingKind.RunicGarden
                   ? $"；肥力 {Tiles[Index(x, y)].Value.Fertility}/100"
                   : IsMaterialFacility(kind)
                       ? "；紧邻实际可采材料"
                       : "");
    }

    private int BuildPlannedFacility(StateReference<Settlement> town, BuildingKind kind, int x, int y, string reason,
        BridgeDirection? direction = null, int level = 1)
    {
        var siteReason = BuildingSiteReason(town, kind, x, y);
        var id = BuildFacility(town.Value.Id, kind, x, y, direction, level);
        var building = FindBuilding(id)!;
        building.Replace(building.Value with { PlanningReason = reason, SiteReason = siteReason });
        return id;
    }

    /// <summary>判断设施是否必须建在水中并贴近陆岸。</summary>
    /// <param name="kind">设施类别。</param>
    public static bool IsWaterfrontBuilding(BuildingKind kind)
    {
        return kind is BuildingKind.Dock or BuildingKind.Shipyard;
    }

    /// <summary>判断设施是否为供所有人通行的桥梁或山路。</summary>
    /// <param name="kind">设施类别。</param>
    public static bool IsPublicInfrastructure(BuildingKind kind)
    {
        return kind is BuildingKind.MountainPass or BuildingKind.Bridge;
    }

    private static bool IsMaterialFacility(BuildingKind kind)
    {
        return kind is BuildingKind.Workshop or BuildingKind.LumberCamp or BuildingKind.Quarry;
    }

    /// <summary>按资源、地形、道路和邻近设施计算选址评分；聚落或地点不存在时返回负无穷。</summary>
    /// <param name="settlementId">聚落 ID。</param>
    /// <param name="kind">设施类别。</param>
    /// <param name="x">横向地格坐标。</param>
    /// <param name="y">纵向地格坐标。</param>
    public double BuildingSiteScore(int settlementId, BuildingKind kind, int x, int y)
    {
        if (!_settlements.TryGetValue(settlementId, out var town) || !InBounds(x, y))
            return double.NegativeInfinity;
        var tile = Tiles[Index(x, y)];
        var score = -Distance(x, y, town.Value.X, town.Value.Y) * .25 + (tile.Value.RoadLevel > 0 ? 1 : 0);
        if (kind is BuildingKind.Farm or BuildingKind.AutomatedFarm or BuildingKind.RunicGarden)
            score += tile.Value.Fertility / 10d - (tile.Value.DroughtTicks > 0 ? 8 : 0);
        if (IsMaterialFacility(kind))
        {
            foreach (var i in Circle(x, y, 1))
            {
                if (i == Index(x, y))
                    continue;
                var source = Tiles[i];
                var yields = TerrainRules.For(source.Value.Terrain);
                var yield = kind == BuildingKind.LumberCamp ? yields.WoodYield :
                    kind == BuildingKind.Quarry ? yields.StoneYield + yields.OreYield :
                    Math.Max(yields.WoodYield, yields.StoneYield + yields.OreYield);
                score += Math.Min(1, source.Value.ResourceAmount / 25) * yield * 12;
            }
        }

        if (kind == BuildingKind.Well)
            score += Math.Min(3, WellWaterYield(tile.Value)) * 15;
        if (kind == BuildingKind.Reservoir)
        {
            score += Circle(x, y, 1).Where(i => i == Index(x, y) || IsFreshWater(Tiles[i].Value))
                .Select(i => Math.Min(3, GetDailyWaterCapacity(i % Width, i / Width)) * 20)
                .DefaultIfEmpty().Max();
        }

        if (kind is BuildingKind.SignalTower or BuildingKind.Watchtower)
            score += tile.Value.Terrain is TerrainType.Mountain or TerrainType.Hills ? 5 : 0;
        if (kind == BuildingKind.ArcaneSanctum)
            score += TerrainRules.For(tile.Value.Terrain).ManaRate * 4;
        foreach (var building in Buildings)
        {
            var distance = Distance(x, y, building.Value.X, building.Value.Y);
            if (distance <= 2)
                score -= distance == 1 ? 4 : 1;
            if (kind is BuildingKind.Infirmary or BuildingKind.Granary or BuildingKind.Market && distance <= 4)
                score += 1.5;
        }

        return score;
    }

    private int BestBuildingSite(StateReference<Settlement> town, BuildingKind kind, bool founding = false)
    {
        var radius = founding ? _creatingDemo ? 3 : 6 : Math.Max(8, town.Value.MaxClaimRadius);
        return Circle(town.Value.X, town.Value.Y, radius)
            .Where(i => FacilityPlacementError(town.Value.Id, kind, i % Width, i / Width, founding,
                            founding: founding) is null
                        && (founding || (AutomaticSiteUseful(kind, i) && _citizens[town.Value.Id].Any(p => p.Health > 0 &&
                            p.Age >= 14 && p.ArmyId == 0
                            && Distance(p.X, p.Y, i % Width, i / Width) <= 6
                            && VisibleWorkSiteReachable(p, i % Width, i / Width, true)))))
            .OrderByDescending(i => BuildingSiteScore(town.Value.Id, kind, i % Width, i / Width))
            .ThenBy(i => i)
            .FirstOrDefault(-1);
    }

    private bool BuildingGroundOwned(Building building)
    {
        return IsPublicInfrastructure(building.Kind)
               || (Tiles[Index(building.X, building.Y)].Value.ClaimedSettlementId == building.SettlementId
                   && _settlements.TryGetValue(building.SettlementId, out var town)
                   && Tiles[Index(building.X, building.Y)].Value.NationId == town.Value.NationId);
    }

    private bool PassiveFacility(Building building)
    {
        return building.Kind is BuildingKind.Granary or BuildingKind.Housing or BuildingKind.Watchtower;
    }

    /// <summary>计算本地基础住房与正在运营的住宅提供的总容量。</summary>
    /// <param name="settlementId">聚落 ID。</param>
    public int GetHousingCapacity(int settlementId)
    {
        var town = RequireTown(settlementId);
        return Math.Min(20_000, town.Value.Housing + Buildings
            .Where(b => b.Value.SettlementId == settlementId && b.Value.Kind == BuildingKind.Housing && IsFacilityOperating(b.Value))
            .Sum(b => b.Value.Level * HousingCapacityPerLevel));
    }

    private double GranaryRestBonus(int settlementId)
    {
        IEnumerable<StateReference<Building>>? buildings = _localWorkQueriesActive
            ? _localWorkBuildings.GetValueOrDefault(settlementId)
            : Buildings;
        var bonus = 1d;
        if (buildings is null)
            return bonus;
        foreach (var building in buildings)
            if (building.Value.SettlementId == settlementId && building.Value.Kind == BuildingKind.Granary &&
                IsFacilityOperating(building.Value))
                bonus = Math.Max(bonus, 1 + .1 * building.Value.Level);
        return bonus;
    }

    /// <summary>计算附近本国运营码头对舟船速度提供的倍率。</summary>
    /// <param name="x">横向地格坐标。</param>
    /// <param name="y">纵向地格坐标。</param>
    /// <param name="nationId">国家 ID。</param>
    public double BoatTravelMultiplier(int x, int y, int nationId)
    {
        var bonus = 1d;
        foreach (var dock in Buildings)
            if (dock.Value.Kind == BuildingKind.Dock && IsFacilityOperating(dock.Value) &&
                RequireTown(dock.Value.SettlementId).Value.NationId == nationId
                && Distance(x, y, dock.Value.X, dock.Value.Y) <= 3)
                bonus = Math.Max(bonus, 1 + .15 * dock.Value.Level);
        return bonus;
    }
}
