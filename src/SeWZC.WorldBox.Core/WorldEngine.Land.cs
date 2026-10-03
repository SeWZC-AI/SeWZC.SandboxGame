namespace SeWZC.WorldBox.Core;

public sealed partial class WorldEngine
{
    public static bool CanTraverse(Tile tile, TravelMode mode) => mode switch
    {
        TravelMode.Aircraft => true,
        TravelMode.Boat => tile.IsWalkable || tile.Terrain is TerrainType.River or TerrainType.Water or TerrainType.DeepWater,
        _ => tile.IsWalkable
    };

    public static string ImprovementName(LandImprovement kind) => kind switch
    { LandImprovement.Farmland => "耕地", LandImprovement.MountainPass => "山路", LandImprovement.Bridge => "桥梁", _ => "自然地块" };

    public static ResearchKind? DepositResearch(ResourceKind kind) => kind switch
    { ResourceKind.Coal => ResearchKind.Industry, ResourceKind.Oil => ResearchKind.Electrification, ResourceKind.RareEarth => ResearchKind.AdvancedComputing, _ => null };

    private void SeedDeposit(Tile tile, int x, int y)
    {
        tile.Deposit = null; tile.DepositAmount = 0; tile.DepositDiscovered = false;
        if (tile.Terrain is TerrainType.DeepWater or TerrainType.Water or TerrainType.River) return;
        var hash = unchecked((uint)(x * 374761393 + y * 668265263 + State.Seed * 31 + 937));
        hash = (hash ^ (hash >> 13)) * 1274126177;
        tile.Deposit = (hash % 43) switch { 0 or 1 => ResourceKind.Coal, 2 => ResourceKind.Oil, 3 => ResourceKind.RareEarth, _ => null };
        if (tile.Deposit.HasValue) tile.DepositAmount = 120 + hash % 181;
    }

    // Keep highlands, but break large impassable components into hills and small peaks.
    private void LimitMountainRanges()
    {
        var visited = new bool[State.Tiles.Length];
        var queue = new Queue<int>();
        for (var start = 0; start < State.Tiles.Length; start++)
        {
            if (visited[start] || State.Tiles[start].Terrain != TerrainType.Mountain) continue;
            queue.Enqueue(start); visited[start] = true; var count = 0;
            while (queue.TryDequeue(out var index))
            {
                if (++count > 32)
                { State.Tiles[index].Terrain = TerrainType.Hills; State.Tiles[index].Fertility = TerrainRules.Fertility(TerrainType.Hills); }
                foreach (var (dx, dy) in Directions)
                {
                    var x = index % State.Width + dx; var y = index / State.Width + dy;
                    if (!InBounds(x, y)) continue;
                    var next = Index(x, y);
                    if (!visited[next] && State.Tiles[next].Terrain == TerrainType.Mountain)
                    { visited[next] = true; queue.Enqueue(next); }
                }
            }
        }
    }

    private static bool BuildingTerrainValid(BuildingKind kind, Tile tile) => kind switch
    {
        BuildingKind.Bridge => tile.Terrain is TerrainType.River or TerrainType.Water,
        BuildingKind.MountainPass => tile.Terrain == TerrainType.Mountain,
        _ => tile.IsWalkable
    };

    private void CompleteLandImprovement(Building building)
    {
        var tile = State.Tiles[Index(building.X, building.Y)];
        if (building.Kind is BuildingKind.Farm or BuildingKind.AutomatedFarm or BuildingKind.RunicGarden)
        {
            tile.Improvement = LandImprovement.Farmland;
            if (tile.Terrain == TerrainType.Forest) tile.Terrain = TerrainType.Grass;
        }
        else if (building.Kind == BuildingKind.MountainPass)
        { tile.Improvement = LandImprovement.MountainPass; tile.RoadLevel = Math.Max((byte)1, tile.RoadLevel); }
        else if (building.Kind == BuildingKind.Bridge)
        { tile.Improvement = LandImprovement.Bridge; tile.RoadLevel = Math.Max((byte)1, tile.RoadLevel); }
    }

    private void PlanVisibleCrossing(Resident person, int targetX, int targetY)
    {
        if (!State.Rules.Construction || person.TravelMode != TravelMode.Foot || person.ArmyId != 0 || person.Age < 14
            || !HasResearch(person.SettlementId, ResearchKind.Logistics)) return;
        var distance = Distance(person.X, person.Y, targetX, targetY);
        foreach (var (dx, dy) in Directions)
        {
            var x = person.X + dx; var y = person.Y + dy;
            if (!InBounds(x, y) || Distance(x, y, targetX, targetY) >= distance) continue;
            var tile = State.Tiles[Index(x, y)];
            if (tile.IsWalkable || tile.Terrain is not (TerrainType.River or TerrainType.Water or TerrainType.Mountain)) continue;
            if (State.Society.Buildings.Any(b => b.SettlementId == person.SettlementId && !b.IsCompleted)) return;
            var kind = tile.Terrain == TerrainType.Mountain ? BuildingKind.MountainPass : BuildingKind.Bridge;
            if (FacilityPlacementError(person.SettlementId, kind, x, y) is null) BuildFacility(person.SettlementId, kind, x, y);
            return;
        }
    }

    private bool RemoveFailedCrossings()
    {
        var changed = false;
        foreach (var building in State.Society.Buildings)
        {
            if (building.Kind != BuildingKind.Bridge || building.Health > 0 || !InBounds(building.X, building.Y)) continue;
            var tile = State.Tiles[Index(building.X, building.Y)];
            if (tile.Improvement != LandImprovement.Bridge) continue;
            tile.Improvement = LandImprovement.None; tile.RoadLevel = 0; changed = true;
        }
        return changed;
    }

    private void RecordHarvest(Tile tile, double amount)
    {
        if (amount <= 0) return;
        tile.LastHarvestTick = State.Tick;
        tile.Harvested = Math.Min(1_000_000_000, tile.Harvested + amount);
    }

    private int VisibleDepositSite(Resident person)
    {
        if (person.Profession != Profession.Miner || !_settlements.TryGetValue(person.SettlementId, out var home)) return -1;
        var coal = HasResearch(home.Id, ResearchKind.Industry); var oil = HasResearch(home.Id, ResearchKind.Electrification);
        var rare = HasResearch(home.Id, ResearchKind.AdvancedComputing);
        if (!coal && !oil && !rare) return -1;
        var best = -1; var bestDistance = int.MaxValue;
        foreach (var offset in VisibleResourceOffsets)
        {
            var x = person.X + offset.X; var y = person.Y + offset.Y;
            if (!InBounds(x, y)) continue;
            var tile = State.Tiles[Index(x, y)];
            if (tile.Deposit is not { } resource || tile.DepositAmount <= 0 || tile.FireTicks > 0
                || !(resource == ResourceKind.Coal ? coal : resource == ResourceKind.Oil ? oil : rare)) continue;
            if (!tile.DepositDiscovered)
            {
                tile.DepositDiscovered = true;
                AddEvent(WorldEventKind.Research, $"{person.Name}在实地勘探中发现{ResourceStock.Name(resource)}。", x, y,
                    EventAction.General, home.Id, person.Id);
            }
            if (home.Resources.Get(resource) >= 80) continue;
            var site = tile.IsWalkable ? Index(x, y) : Directions.Select(d => (X: x + d.X, Y: y + d.Y))
                .Where(p => Walkable(p.X, p.Y) && State.Tiles[Index(p.X, p.Y)].FireTicks == 0)
                .OrderBy(p => Distance(person.X, person.Y, p.X, p.Y)).Select(p => Index(p.X, p.Y)).FirstOrDefault(-1);
            if (site < 0) continue;
            var distance = Distance(person.X, person.Y, site % State.Width, site / State.Width);
            if (distance < bestDistance) { best = site; bestDistance = distance; }
        }
        return best;
    }

    private bool TryGatherDeposit(Resident person)
    {
        if (!_settlements.TryGetValue(person.SettlementId, out var home)) return false;
        foreach (var index in Circle(person.X, person.Y, 1))
        {
            var tile = State.Tiles[index];
            if (tile.Deposit is not { } kind || tile.DepositAmount <= 0 || tile.FireTicks > 0
                || DepositResearch(kind) is not { } research || !HasResearch(home.Id, research) || home.Resources.Get(kind) >= 80) continue;
            tile.DepositDiscovered = true;
            var amount = Math.Min(tile.DepositAmount, .4 * State.Rules.GatheringRate * (person.SicknessTicks > 0 ? .4 : 1));
            amount = Math.Min(amount, 1_000_000 - person.Inventory.Get(kind));
            tile.DepositAmount -= amount; person.Inventory.Set(kind, person.Inventory.Get(kind) + amount);
            RecordHarvest(tile, amount); person.Activity = ResidentActivity.Working;
            person.Agent.Fatigue = Math.Min(100, person.Agent.Fatigue + .45);
            return amount > 0;
        }
        return false;
    }

    public string GetTileProductionSummary(int x, int y)
    {
        if (!InBounds(x, y)) return "地格不存在";
        var tile = State.Tiles[Index(x, y)];
        var yields = TerrainRules.For(tile.Terrain);
        var products = tile.Improvement is LandImprovement.Bridge or LandImprovement.MountainPass ? "通行设施，不直接生产资源"
            : tile.Improvement == LandImprovement.Farmland ? "粮食（居民耕作，受肥力和干旱影响）"
            : tile.Terrain is TerrainType.Water or TerrainType.DeepWater or TerrainType.River ? "暂无直接采集产出"
            : tile.Terrain == TerrainType.Forest ? "木材、野生食物" : yields.StoneYield >= .3 ? "石材、矿石"
            : ResourceSiteYield(Index(x, y), Profession.Farmer) > 0 ? "野生食物" : "少量自然材料";
        var deposit = tile.DepositDiscovered && tile.Deposit is { } kind
            ? $"{ResourceStock.Name(kind)}矿藏：{tile.DepositAmount:0.#}（不可再生）" : "深层资源：尚未发现，需相应科技与实地勘探";
        return $"{ImprovementName(tile.Improvement)} · 产出：{products}\n自然资源 {tile.ResourceAmount:0.#} · 肥力 {tile.Fertility}%\n{deposit}\n累计采收 {tile.Harvested:0.#} · 最近劳动日 {tile.LastHarvestTick}\n"
            + (tile.FireTicks > 0 ? "火灾中，暂停生产" : tile.DroughtTicks > 0 ? "干旱中，粮食减产" : "环境正常")
            + (tile.IsWalkable ? $" · 步行耗时系数 {GetTerrainMoveCost(x, y):0.##}" : " · 地面受阻，可修桥／山路或使用运输工具");
    }
}
