namespace SeWZC.WorldBox.Core;

public sealed partial class WorldEngine
{
    public static bool CanTraverse(Tile tile, TravelMode mode, RaceKind race = RaceKind.Human) => mode switch
    {
        TravelMode.Aircraft => true,
        TravelMode.Boat => tile.IsWalkable || tile.Terrain is TerrainType.River or TerrainType.Stream or TerrainType.LargeRiver or TerrainType.Water or TerrainType.DeepWater or TerrainType.Lake,
        _ => RaceTerrainRules.CanWalk(tile, race)
    };

    public static string ImprovementName(LandImprovement kind) => kind switch
    { LandImprovement.Farmland => "耕地", LandImprovement.MountainPass => "山路", LandImprovement.Bridge => "桥梁", _ => "自然地块" };

    public static ResearchKind? DepositResearch(ResourceKind kind) => kind switch
    { ResourceKind.Coal => ResearchKind.Industry, ResourceKind.Oil => ResearchKind.Electrification, ResourceKind.RareEarth => ResearchKind.AdvancedComputing, _ => null };

    private void SeedDeposit(Tile tile, int x, int y)
    {
        tile.Deposit = null; tile.DepositAmount = 0; tile.DepositDiscovered = false;
        if (tile.Terrain is TerrainType.DeepWater or TerrainType.Water or TerrainType.River or TerrainType.Stream or TerrainType.LargeRiver or TerrainType.Lake) return;
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
                { State.Tiles[index].Terrain = TerrainType.Hills; }
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
        BuildingKind.Bridge => tile.Terrain is TerrainType.River or TerrainType.Stream or TerrainType.LargeRiver or TerrainType.Water or TerrainType.Lake,
        BuildingKind.MountainPass => tile.Terrain == TerrainType.Mountain,
        BuildingKind.SacredGrove => IsForestTerrain(tile.Terrain),
        BuildingKind.Dock or BuildingKind.Shipyard => tile.Terrain is TerrainType.River or TerrainType.Stream or TerrainType.LargeRiver or TerrainType.Water or TerrainType.Lake,
        _ => !IsWaterTerrain(tile.Terrain) && (tile.IsWalkable || tile.Terrain == TerrainType.Mountain)
    };

    private void CompleteLandImprovement(Building building)
    {
        var tile = State.Tiles[Index(building.X, building.Y)];
        if (building.Kind is BuildingKind.Farm or BuildingKind.AutomatedFarm or BuildingKind.RunicGarden)
        {
            tile.Improvement = LandImprovement.Farmland;
            if (IsForestTerrain(tile.Terrain)) tile.Terrain = TerrainType.Grass;
        }
        else if (building.Kind == BuildingKind.MountainPass)
        { tile.Improvement = LandImprovement.MountainPass; tile.RoadLevel = (byte)building.Level; }
        else if (building.Kind == BuildingKind.Bridge)
        { tile.Improvement = LandImprovement.Bridge; tile.RoadLevel = (byte)building.Level;
            tile.BridgeDirection = building.Direction; tile.BridgeLevel = (byte)building.Level; }
        RegisterBuildingGround(building);
    }

    private void PlanVisibleCrossing(Resident person, int targetX, int targetY)
    {
        if (!State.Rules.Construction || person.TravelMode != TravelMode.Foot || person.ArmyId != 0 || person.Age < 14
            || !HasResearch(person.SettlementId, ResearchKind.Logistics)
            || person.Agent.Goal.Kind is AgentGoalKind.Explore or AgentGoalKind.Gather or AgentGoalKind.FetchWater
            || person.Agent.Goal.TargetSettlementId == 0 && person.Agent.Goal.TargetEntityId == 0) return;
        if (State.Society.Buildings.Any(b => b.SettlementId == person.SettlementId && (!b.IsCompleted || b.IsUpgrading))) return;
        foreach (var (dx, dy) in Directions)
        {
            var x = person.X + dx; var y = person.Y + dy;
            if (!InBounds(x, y) || Distance(x, y, targetX, targetY) >= Distance(person.X, person.Y, targetX, targetY)) continue;
            var direction = dx != 0 ? BridgeDirection.Horizontal : BridgeDirection.Vertical;
            var first = State.Tiles[Index(x, y)];
            if (first.Terrain is not (TerrainType.River or TerrainType.Stream or TerrainType.LargeRiver or TerrainType.Water or TerrainType.Lake) || first.Improvement == LandImprovement.Bridge) continue;
            // Both banks and every intervening section must be seen and usable on one axis.
            // A mismatched completed bridge is an obstacle, never a reason to build sideways.
            var farBank = false; var span = 0; var unfinished = 0;
            for (var length = 1; length <= 6; length++)
            {
                var xx = person.X + dx * length; var yy = person.Y + dy * length;
                if (!InBounds(xx, yy)) break;
                var tile = State.Tiles[Index(xx, yy)];
                if (!IsWaterTerrain(tile.Terrain))
                { farBank = tile.IsWalkable && tile.FireTicks == 0; break; }
                if (tile.Terrain == TerrainType.DeepWater || tile.FireTicks > 0
                    || tile.Improvement == LandImprovement.Bridge && tile.BridgeDirection != direction
                    || tile.NationId != 0 && tile.NationId != person.NationId) break;
                span++;
                if (tile.Improvement != LandImprovement.Bridge) unfinished++;
            }
            if (!farBank || span == 0) continue;
            var level = Math.Clamp(Math.Max((span + 3) / 4, (BridgeShoreDistance(x, y, direction) + 1) / 2), 1, 3);
            var home = _settlements[person.SettlementId];
            var reserve = LocalDevelopmentReserve(home); var cost = FacilityCost(BuildingKind.Bridge, level);
            if (AdvancementRules.Resources.Any(k => home.Resources.Get(k) < cost.Get(k) * unfinished + reserve.Get(k))) continue;
            if (FacilityPlacementError(home.Id, BuildingKind.Bridge, x, y, direction: direction, bridgeLevel: level) is not null) continue;
            BuildFacility(home.Id, BuildingKind.Bridge, x, y, direction, level);
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
            tile.Improvement = LandImprovement.None; tile.RoadLevel = 0; tile.BridgeLevel = 0; changed = true;
        }
        return changed;
    }

    private void RecordHarvest(Tile tile, double amount)
    {
        if (amount <= 0) return;
        if (tile.ResourceAmount < 1 && tile.Improvement != LandImprovement.Farmland) tile.Plants = new();
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
            var amount = Math.Min(tile.DepositAmount, .4 * State.Rules.GatheringRate * GatheringCondition(person));
            amount = Math.Min(amount, 1_000_000 - person.Inventory.Get(kind));
            tile.DepositAmount -= amount; person.Inventory.Set(kind, person.Inventory.Get(kind) + amount);
            RecordHarvest(tile, amount); person.Activity = ResidentActivity.Working;
            person.Agent.Fatigue = Math.Min(100, person.Agent.Fatigue + .45);
            return amount > 0;
        }
        return false;
    }

    public string GetTileProductionSummary(int x, int y, ResourceVisibility visibility = ResourceVisibility.Researched)
    {
        if (!InBounds(x, y)) return "地格不存在";
        var tile = State.Tiles[Index(x, y)];
        var products = new List<string>();
        if (tile.Improvement is LandImprovement.Bridge or LandImprovement.MountainPass) products.Add("通行设施");
        else
        {
            if (ResourceSiteYield(Index(x, y), Profession.Farmer) > 0) products.Add(tile.Improvement == LandImprovement.Farmland ? "耕种粮食" : "野生食物");
            if (ResourceSiteYield(Index(x, y), Profession.Lumberjack) > 0) products.Add("木材");
            if (ResourceSiteYield(Index(x, y), Profession.Miner) > 0) products.Add("石材、矿石");
        }
        var lines = new List<string> { products.Count > 0 ? "可采产出：" + string.Join("、", products) : tile.ResourceAmount < 1 && tile.IsWalkable ? "资源暂已采尽，等待自然恢复" : "此地暂无直接采集产出" };
        lines.Add(IsFreshWater(tile) ? "淡水源：无限供水，需到岸边打水并携带返仓"
            : $"每日可取水 {DailyWaterYield(tile):0.###}   今日剩余 {AvailableWater(x, y):0.###}（未取用的水不累积）"
                + (DailyWaterYield(tile) < .025 ? "\n供水不足一名成年居民每日所需的 0.025，建议到河湖岸边打水" : ""));
        if (tile.ClaimedSettlementId != 0) lines.Add("实际地盘：" + _settlements.GetValueOrDefault(tile.ClaimedSettlementId)?.Name);
        lines.Add($"生成海拔 {tile.Elevation} / 255   降水 {tile.Rainfall:0.000000} / 格 / 日");
        lines.Add($"河湖补水 {Math.Max(0, tile.NaturalWaterYield - tile.Rainfall):0.000000} / 日");
        if (tile.RiverWidth > 0) lines.Add($"水道宽度 {tile.RiverWidth} 格；{(tile.Terrain == TerrainType.Stream ? "小溪可减速步行" : "需桥梁或舟船通行")}");
        foreach (var race in Enum.GetValues<RaceKind>())
        {
            var adaptation = RaceTerrainRules.For(race, tile.Terrain);
            lines.Add($"{RaceNames[(int)race]}：{(adaptation.Habitable ? "宜居" : "不宜居")}   {(RaceTerrainRules.CanWalk(tile, race) ? "可步行" : "需通道或载具")}   地形生产 ×{adaptation.Productivity:0.00}");
        }
        if (tile.IsWalkable) lines.Add($"可采储量 {tile.ResourceAmount:0.#}   肥力 {tile.Fertility}%");
        if (tile.Improvement == LandImprovement.Farmland) lines.Add("耕地：需要居民到场耕作，产物随身运回家园");
        if (tile.FireTicks > 0) lines.Add($"正在燃烧：剩余 {tile.FireTicks} 日，暂停生产");
        else if (tile.DroughtTicks > 0) lines.Add($"干旱：剩余 {tile.DroughtTicks} 日，粮食减产");
        else if (tile.ResourceAmount >= 1 && tile.IsWalkable) lines.Add("状态：可以采收，自然资源持续恢复");
        if (IsDepositVisible(tile, visibility) && tile.Deposit is { } kind)
            lines.Add($"{ResourceStock.Name(kind)}矿藏：{tile.DepositAmount:0.#}（不可再生）");
        if (tile.Harvested > 0) lines.Add($"累计采收 {tile.Harvested:0.#}   最近劳动距今 {Math.Max(0, State.Tick - tile.LastHarvestTick)} 日");
        var plants = PlantResources.At(tile).ToArray();
        if (plants.Length > 0) lines.Add("植物：" + string.Join("、", plants.Select(p => $"{PlantResources.Name(p.Kind)} 覆盖 {p.Cover:P0}")) + "（共享可采储量）");
        for (var species = 1; species < AnimalRules.SpeciesCount; species++)
        {
            var speciesKind = (WildlifeKind)species; var population = tile.AnimalPopulation(speciesKind);
            if (population > 0) lines.Add($"野生动物：{WildlifeName(speciesKind)}（{AnimalRules.For(speciesKind).Size switch { AnimalSize.Small => "小型", AnimalSize.Medium => "中型", _ => "大型" }}{(AnimalRules.For(speciesKind).Diet == AnimalDiet.Carnivore ? "食肉" : "食草")}）   数量 {population:0.0} / 容量 {WildlifeCapacity(tile, speciesKind):0.0}");
        }
        return string.Join("\n", lines);
    }
}
