using SeWZC.WorldBox.Core.Runtime;

namespace SeWZC.WorldBox.Core;

public sealed partial class WorldEngine
{
    /// <summary>返回设施建造成本，桥梁按指定等级调整材料量。</summary>
    /// <param name="kind">设施类别。</param>
    /// <param name="level">桥梁等级，计算时限制在 1 至 3；普通建筑忽略此参数。</param>
    public static ResourceStock FacilityCost(BuildingKind kind, int level = 1)
    {
        var cost = GetBuildingCost(kind);
        if (kind == BuildingKind.Bridge)
            cost = cost.Scale(Math.Clamp(level, 1, 3));

        return cost;
    }

    /// <summary>返回桥梁通行轴向的中文名称。</summary>
    /// <param name="direction">桥梁的通行轴向。</param>
    public static string BridgeDirectionName(BridgeDirection direction)
    {
        return direction == BridgeDirection.Horizontal ? "左右" : "上下";
    }

    /// <summary>计算桥梁等级允许的最大自然离岸距离，以地格计。</summary>
    /// <param name="level">桥梁等级，计算时限制在 1 至 3。</param>
    public static int BridgeShoreLimit(int level)
    {
        return Math.Clamp(level, 1, 3) * 2;
    }

    /// <summary>检查是否可按指定交通方式跨越一个相邻地格，步行同时检查桥梁轴向。</summary>
    /// <param name="fromX">起点的横向地格坐标。</param>
    /// <param name="fromY">起点的纵向地格坐标。</param>
    /// <param name="toX">终点的横向地格坐标。</param>
    /// <param name="toY">终点的纵向地格坐标。</param>
    /// <param name="mode">待判断的交通方式。</param>
    /// <param name="race">居民种族。</param>
    public bool CanTraverseStep(int fromX, int fromY, int toX, int toY, TravelMode mode, RaceKind race = RaceKind.Human)
    {
        if (!InBounds(fromX, fromY) || !InBounds(toX, toY) || Distance(fromX, fromY, toX, toY) != 1)
            return false;
        return CanTraverseAdjacentTiles(Current.Tiles[Index(fromX, fromY)].Value, Current.Tiles[Index(toX, toY)].Value,
            fromY == toY, mode, race);
    }

    // 已确认相邻且在地图内的搜索地格直接共用通行规则，不重复转换坐标和验证距离。
    private static bool CanTraverseAdjacentTiles(Tile from, Tile to, bool horizontal, TravelMode mode, RaceKind race)
    {
        if (!CanTraverse(to, mode, race))
            return false;
        if (mode != TravelMode.Foot)
            return true;
        return (from.Improvement != LandImprovement.Bridge ||
                horizontal == (from.BridgeDirection == BridgeDirection.Horizontal))
               && (to.Improvement != LandImprovement.Bridge ||
                   horizontal == (to.BridgeDirection == BridgeDirection.Horizontal));
    }

    /// <summary>沿桥梁轴向在六格内寻找最近自然岸，未找到时返回 <c>int.MaxValue</c>。</summary>
    /// <param name="x">横向地格坐标。</param>
    /// <param name="y">纵向地格坐标。</param>
    /// <param name="direction">寻找自然岸时使用的桥梁通行轴向。</param>
    public int BridgeShoreDistance(int x, int y, BridgeDirection direction)
    {
        var dx = direction == BridgeDirection.Horizontal ? 1 : 0;
        var dy = 1 - dx;
        var best = int.MaxValue;
        for (var sign = -1; sign <= 1; sign += 2)
        for (var distance = 1; distance <= 6; distance++)
        {
            var xx = x + dx * sign * distance;
            var yy = y + dy * sign * distance;
            if (!InBounds(xx, yy))
                break;
            var tile = Current.Tiles[Index(xx, yy)];
            if (IsWaterTerrain(tile.Terrain))
                continue;
            if (tile.IsWalkable && tile.Improvement != LandImprovement.Bridge)
                best = Math.Min(best, distance);
            break;
        }

        return best;
    }

    /// <summary>检查桥梁地形、方向、等级及连岸条件；可放置时返回空值，否则返回原因。</summary>
    /// <param name="x">横向地格坐标。</param>
    /// <param name="y">纵向地格坐标。</param>
    /// <param name="direction">要建造的桥梁通行轴向。</param>
    /// <param name="level">要建造的桥梁等级，范围为 1 至 3。</param>
    public string? BridgePlacementError(int x, int y, BridgeDirection direction, int level = 1)
    {
        if (!Enum.IsDefined(direction) || level is < 1 or > 3)
            return "桥梁方向或等级无效";
        if (!InBounds(x, y) || Current.Tiles[Index(x, y)].Terrain is not (TerrainType.Water or TerrainType.River
                or TerrainType.Lake or TerrainType.Stream or TerrainType.LargeRiver))
            return "桥梁需要河流、湖泊或浅水";
        if (BridgeShoreDistance(x, y, direction) > BridgeShoreLimit(level))
            return $"此方向离自然岸超过 {BridgeShoreLimit(level)} 格，需升级桥梁技术或换址";
        var dx = direction == BridgeDirection.Horizontal ? 1 : 0;
        var dy = 1 - dx;
        for (var sign = -1; sign <= 1; sign += 2)
        {
            var xx = x + dx * sign;
            var yy = y + dy * sign;
            if (!InBounds(xx, yy))
                continue;
            var tile = Current.Tiles[Index(xx, yy)];
            if (tile.IsWalkable &&
                (tile.Improvement != LandImprovement.Bridge || tile.BridgeDirection == direction))
                return null;
        }

        return "桥梁需沿选定方向连接陆地或已完工的同向桥段";
    }

    private BridgeDirection InferBridgeDirection(int x, int y)
    {
        return BridgePlacementError(x, y, BridgeDirection.Horizontal) is null
            ? BridgeDirection.Horizontal
            : BridgeDirection.Vertical;
    }

    /// <summary>计算设施升级或桥梁改向所需的资源成本。</summary>
    /// <param name="building">准备升级或改向的建筑。</param>
    /// <param name="reorient">是否计算桥梁改向成本，关闭时计算升级成本。</param>
    public static ResourceStock GetUpgradeCost(Building building, bool reorient = false)
    {
        var cost = GetBuildingCost(building.Kind);
        var scale = reorient ? .5 : building.Level * .75;
        return cost.Scale(scale);
    }

    /// <summary>检查建筑升级或改向的条件；可执行时返回空值，否则返回原因。</summary>
    /// <param name="id">建筑的稳定 ID。</param>
    /// <param name="gift">是否按直接赐予校验，跳过材料检查及相关运输研究要求。</param>
    /// <param name="direction">桥梁改向的目标轴向，空值表示升级一级。</param>
    public string? BuildingUpgradeError(int id, bool gift = false, BridgeDirection? direction = null)
    {
        var building = Current.Buildings.FirstOrDefault(b => b.Value.Id == id);
        if (building is null)
            return "建筑已不存在";
        if (!building.Value.IsCompleted || building.Value.Health < 50)
            return "需先完工并修复建筑";
        if (building.Value.IsUpgrading)
            return "已有升级或改向项目";
        if (building.Value.Kind == BuildingKind.TownCenter && RequireTown(building.Value.SettlementId).IsExpanding)
            return "中心正在组织城镇扩充，完成后可单独升级建筑";
        if (Current.Tiles[Index(building.Value.X, building.Value.Y)].FireTicks > 0)
            return "所在地正在燃烧";
        if (direction.HasValue && (building.Value.Kind != BuildingKind.Bridge || direction == building.Value.Direction))
            return "只能将桥梁改为另一方向";
        if (!direction.HasValue && building.Value.Level >= 3)
            return "建筑已达到 3 级";
        if (building.Value.Kind == BuildingKind.Bridge && BridgePlacementError(building.Value.X, building.Value.Y,
                direction ?? building.Value.Direction,
                direction.HasValue ? building.Value.Level : building.Value.Level + 1) is { } crossingError)
            return crossingError;
        if (!gift && building.Value.Kind is BuildingKind.MountainPass or BuildingKind.Bridge &&
            !HasResearch(building.Value.SettlementId, Advancement.Logistics))
            return "需要先掌握驿路运输";
        return gift
            ? null
            : MissingResources(RequireTown(building.Value.SettlementId).Resources,
                GetUpgradeCost(building.Value, direction.HasValue));
    }

    /// <summary>校验后启动升级或桥梁改向施工；赐予时直接完工。</summary>
    /// <param name="id">建筑的稳定 ID。</param>
    /// <param name="gift">是否直接赐予，跳过材料支出并直接完工。</param>
    /// <param name="direction">桥梁改向的目标轴向，空值表示升级一级。</param>
    public void UpgradeBuilding(int id, bool gift = false, BridgeDirection? direction = null)
    {
        if (BuildingUpgradeError(id, gift, direction) is { } error)
            throw new InvalidOperationException(error);
        var building = Current.Buildings.First(b => b.Value.Id == id);
        if (!gift)
        {
            RequireTown(building.Value.SettlementId).Resources = Spend(RequireTown(building.Value.SettlementId).Resources,
                GetUpgradeCost(building.Value, direction.HasValue));
        }

        building.Replace(building.Value with
        {
            PendingDirection = direction,
            UpgradeProgress = 0,
            UpgradeRequired = direction.HasValue ? 15 : 30 * building.Value.Level,
            Workers = building.Value.Workers.Clear(),
            LastWorkedTick = -100,
        });
        var entry = AddEvent(WorldEventKind.Construction,
            $"{BuildingName(building.Value.Kind)}开始{(direction.HasValue ? "改向" : "升级")}，等待实地施工。",
            building.Value.X, building.Value.Y, EventAction.Started, building.Value.SettlementId);
        building.Replace(building.Value with { Observation = new ProjectObservation { StartEventId = entry.Id } });
        if (gift)
            FinishBuildingUpgrade(building);
    }

    private void FinishBuildingUpgrade(StateReference<Building> building)
    {
        if (building.Value.PendingDirection is { } direction)
            building.Replace(building.Value with { Direction = direction });
        else
            building.Replace(building.Value with { Level = building.Value.Level + 1 });
        building.Replace(building.Value with
        {
            PendingDirection = null,
            UpgradeProgress = 0,
            UpgradeRequired = 0,
            WorkSlots = Math.Min(20, (building.Value.Kind == BuildingKind.Farm ? 5 : 3) + building.Value.Level - 1),
        });
        CompleteLandImprovement(building.Value);
        AddEvent(WorldEventKind.Construction, $"{BuildingName(building.Value.Kind)}施工完成，当前 {building.Value.Level} 级。",
            building.Value.X, building.Value.Y, EventAction.Completed, building.Value.SettlementId,
            causeEventId: building.Value.Observation.StartEventId);
    }

    private bool PlanBuildingUpgrade(SettlementCursor town, StateReference<Building>[] buildings)
    {
        if (!Current.Rules.Construction || buildings.Any(b => !b.Value.IsCompleted || b.Value.IsUpgrading))
            return false;
        var reserve = LocalDevelopmentReserve(town);
        var demand = InspectLocalDemand(town, buildings);
        foreach (var building in buildings.OrderBy(b => b.Value.Level).ThenBy(b => b.Value.Id))
        {
            if (!FacilityNeeded(demand, building.Value.Kind) || (building.Value.Kind != BuildingKind.TownCenter
                                                           && building.Value.Kind != BuildingKind.Housing &&
                                                           building.Value.Workers.Count < building.Value.WorkSlots))
                continue;
            if (building.Value.Level >= 3 || Current.Tick - building.Value.LastWorkedTick > 24 ||
                BuildingUpgradeError(building.Value.Id) is not null)
                continue;
            var cost = GetUpgradeCost(building.Value);
            if (ResourceStock.Kinds.Any(k => town.Resources.Get(k) < cost.Get(k) + reserve.Get(k) +
                    (k == ResourceKind.Food && building.Value.Kind is not (BuildingKind.Farm or BuildingKind.AutomatedFarm
                        or BuildingKind.RunicGarden or BuildingKind.Pasture or BuildingKind.Aquaculture)
                        ? town.Population * 2
                        : 0)))
                continue;
            UpgradeBuilding(building.Value.Id);
            town.DevelopmentGoal = "升级" + BuildingName(building.Value.Kind);
            town.DevelopmentBlocker = "材料已投入，等待居民到场升级";
            return true;
        }

        return false;
    }
}
