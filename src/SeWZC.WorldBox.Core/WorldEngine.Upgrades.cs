namespace SeWZC.WorldBox.Core;

public sealed partial class WorldEngine
{
    public static ResourceStock FacilityCost(BuildingKind kind, int level = 1)
    {
        var cost = GetBuildingCost(kind);
        if (kind == BuildingKind.Bridge)
            foreach (var resource in AdvancementRules.Resources) cost.Set(resource, cost.Get(resource) * Math.Clamp(level, 1, 3));
        return cost;
    }
    public static string BridgeDirectionName(BridgeDirection direction) => direction == BridgeDirection.Horizontal ? "左右" : "上下";
    public static int BridgeShoreLimit(int level) => Math.Clamp(level, 1, 3) * 2;

    public bool CanTraverseStep(int fromX, int fromY, int toX, int toY, TravelMode mode)
    {
        if (!InBounds(fromX, fromY) || !InBounds(toX, toY) || Distance(fromX, fromY, toX, toY) != 1
            || !CanTraverse(State.Tiles[Index(toX, toY)], mode)) return false;
        if (mode != TravelMode.Foot) return true;
        var horizontal = fromY == toY;
        var from = State.Tiles[Index(fromX, fromY)]; var to = State.Tiles[Index(toX, toY)];
        return (from.Improvement != LandImprovement.Bridge || horizontal == (from.BridgeDirection == BridgeDirection.Horizontal))
            && (to.Improvement != LandImprovement.Bridge || horizontal == (to.BridgeDirection == BridgeDirection.Horizontal));
    }

    public int BridgeShoreDistance(int x, int y, BridgeDirection direction)
    {
        var dx = direction == BridgeDirection.Horizontal ? 1 : 0; var dy = 1 - dx;
        var best = int.MaxValue;
        for (var sign = -1; sign <= 1; sign += 2)
        for (var distance = 1; distance <= 6; distance++)
        {
            var xx = x + dx * sign * distance; var yy = y + dy * sign * distance;
            if (!InBounds(xx, yy)) break;
            var tile = State.Tiles[Index(xx, yy)];
            if (IsWaterTerrain(tile.Terrain)) continue;
            if (tile.IsWalkable && tile.Improvement != LandImprovement.Bridge) best = Math.Min(best, distance);
            break;
        }
        return best;
    }

    public string? BridgePlacementError(int x, int y, BridgeDirection direction, int level = 1)
    {
        if (!Enum.IsDefined(direction) || level is < 1 or > 3) return "桥梁方向或等级无效";
        if (!InBounds(x, y) || State.Tiles[Index(x, y)].Terrain is not (TerrainType.River or TerrainType.Water or TerrainType.Lake)) return "桥梁需要河流、湖泊或浅水";
        if (BridgeShoreDistance(x, y, direction) > BridgeShoreLimit(level)) return $"此方向离自然岸超过 {BridgeShoreLimit(level)} 格，需升级桥梁技术或换址";
        var dx = direction == BridgeDirection.Horizontal ? 1 : 0; var dy = 1 - dx;
        for (var sign = -1; sign <= 1; sign += 2)
        {
            var xx = x + dx * sign; var yy = y + dy * sign;
            if (!InBounds(xx, yy)) continue;
            var tile = State.Tiles[Index(xx, yy)];
            if (tile.IsWalkable && (tile.Improvement != LandImprovement.Bridge || tile.BridgeDirection == direction)) return null;
        }
        return "桥梁需沿选定方向连接陆地或已完工的同向桥段";
    }

    private BridgeDirection InferBridgeDirection(int x, int y) => BridgePlacementError(x, y, BridgeDirection.Horizontal) is null
        ? BridgeDirection.Horizontal : BridgeDirection.Vertical;

    public static ResourceStock GetUpgradeCost(Building building, bool reorient = false)
    {
        var cost = GetBuildingCost(building.Kind);
        var scale = reorient ? .5 : building.Level * .75;
        foreach (var kind in AdvancementRules.Resources) cost.Set(kind, cost.Get(kind) * scale);
        return cost;
    }

    public string? BuildingUpgradeError(int id, bool gift = false, BridgeDirection? direction = null)
    {
        var building = State.Society.Buildings.FirstOrDefault(b => b.Id == id);
        if (building is null) return "建筑已不存在";
        if (!building.IsCompleted || building.Health < 50) return "需先完工并修复建筑";
        if (building.IsUpgrading) return "已有升级或改向项目";
        if (building.Kind == BuildingKind.TownCenter && RequireTown(building.SettlementId).IsExpanding) return "中心正在组织城镇扩充，完成后可单独升级建筑";
        if (State.Tiles[Index(building.X, building.Y)].FireTicks > 0) return "所在地正在燃烧";
        if (direction.HasValue && (building.Kind != BuildingKind.Bridge || direction == building.Direction)) return "只能将桥梁改为另一方向";
        if (!direction.HasValue && building.Level >= 3) return "建筑已达到 3 级";
        if (building.Kind == BuildingKind.Bridge && BridgePlacementError(building.X, building.Y, direction ?? building.Direction,
            direction.HasValue ? building.Level : building.Level + 1) is { } crossingError) return crossingError;
        if (!gift && building.Kind is BuildingKind.Bridge or BuildingKind.MountainPass && !HasResearch(building.SettlementId, ResearchKind.Logistics)) return "需要先掌握驿路运输";
        return gift ? null : MissingResources(RequireTown(building.SettlementId).Resources, GetUpgradeCost(building, direction.HasValue));
    }

    public void UpgradeBuilding(int id, bool gift = false, BridgeDirection? direction = null)
    {
        if (BuildingUpgradeError(id, gift, direction) is { } error) throw new InvalidOperationException(error);
        var building = State.Society.Buildings.First(b => b.Id == id);
        if (!gift) Spend(RequireTown(building.SettlementId).Resources, GetUpgradeCost(building, direction.HasValue));
        building.PendingDirection = direction;
        building.UpgradeProgress = 0; building.UpgradeRequired = direction.HasValue ? 15 : 30 * building.Level;
        building.Workers.Clear(); building.LastWorkedTick = -100;
        var entry = AddEvent(WorldEventKind.Construction, $"{BuildingName(building.Kind)}开始{(direction.HasValue ? "改向" : "升级")}，等待实地施工。",
            building.X, building.Y, EventAction.Started, building.SettlementId);
        building.Observation = new ProjectObservation { StartEventId = entry.Id };
        if (gift) FinishBuildingUpgrade(building);
    }

    private void FinishBuildingUpgrade(Building building)
    {
        if (building.PendingDirection is { } direction) building.Direction = direction;
        else building.Level++;
        building.PendingDirection = null; building.UpgradeProgress = 0; building.UpgradeRequired = 0;
        building.WorkSlots = Math.Min(20, (building.Kind == BuildingKind.Farm ? 5 : 3) + building.Level - 1);
        CompleteLandImprovement(building);
        AddEvent(WorldEventKind.Construction, $"{BuildingName(building.Kind)}施工完成，当前 {building.Level} 级。",
            building.X, building.Y, EventAction.Completed, building.SettlementId, causeEventId: building.Observation.StartEventId);
        _armyPaths.Clear();
    }

    private bool PlanBuildingUpgrade(Settlement town, Building[] buildings)
    {
        if (!State.Rules.Construction || buildings.Any(b => !b.IsCompleted || b.IsUpgrading)) return false;
        var reserve = LocalDevelopmentReserve(town);
        foreach (var building in buildings.OrderBy(b => b.Level).ThenBy(b => b.Id))
        {
            if (building.Level >= 3 || State.Tick - building.LastWorkedTick > 24 || BuildingUpgradeError(building.Id) is not null) continue;
            var cost = GetUpgradeCost(building);
            if (AdvancementRules.Resources.Any(k => town.Resources.Get(k) < cost.Get(k) + reserve.Get(k) + (k == ResourceKind.Food ? town.Population * 2 : 0))) continue;
            UpgradeBuilding(building.Id); town.DevelopmentGoal = "升级" + BuildingName(building.Kind);
            town.DevelopmentBlocker = "材料已投入，等待居民到场升级"; return true;
        }
        return false;
    }
}
