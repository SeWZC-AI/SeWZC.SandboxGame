using SeWZC.WorldBox.Core.Runtime;

namespace SeWZC.WorldBox.Core;

public sealed partial class WorldEngine
{
    /// <summary>根据交通方式、种族和地块改良判断是否可进入地格。</summary>
    /// <param name="tile">准备进入的地格。</param>
    /// <param name="mode">待判断的交通方式。</param>
    /// <param name="race">居民种族。</param>
    public static bool CanTraverse(Tile tile, TravelMode mode, RaceKind race = RaceKind.Human)
    {
        return mode switch
        {
            TravelMode.Boat => tile.IsWalkable || tile.Terrain is TerrainType.DeepWater or TerrainType.Water
                or TerrainType.River or TerrainType.Lake or TerrainType.Stream or TerrainType.LargeRiver,
            TravelMode.Aircraft => true,
            _ => RaceTerrainRules.CanWalk(tile, race),
        };
    }

    /// <summary>返回地块改良的中文名称。</summary>
    /// <param name="kind">地块改良类别。</param>
    public static string ImprovementName(LandImprovement kind)
    {
        return kind switch
        {
            LandImprovement.Farmland => "耕地",
            LandImprovement.MountainPass => "山路",
            LandImprovement.Bridge => "桥梁",
            _ => "自然地块",
        };
    }

    /// <summary>返回开采该类矿藏所需的研究；不属于阶段矿藏时返回空值。</summary>
    /// <param name="kind">资源种类。</param>
    public static Advancement? DepositResearch(ResourceKind kind)
    {
        return kind switch
        {
            ResourceKind.Coal => Advancement.Industry,
            ResourceKind.Oil => Advancement.Electrification,
            ResourceKind.RareEarth => Advancement.AdvancedComputing,
            _ => null,
        };
    }

    private void SeedDeposit(TileCursor tile, int x, int y)
    {
        tile.Replace(tile.Value with { Deposit = null, DepositAmount = 0, DepositDiscovered = false });
        if (tile.Terrain is TerrainType.DeepWater or TerrainType.Water or TerrainType.River or TerrainType.Lake
            or TerrainType.Stream or TerrainType.LargeRiver)
            return;
        var hash = unchecked((uint)(x * 374761393 + y * 668265263 + Current.Seed * 31 + 937));
        hash = (hash ^ (hash >> 13)) * 1274126177;
        tile.Deposit = (hash % 43) switch
        {
            0 or 1 => ResourceKind.Coal,
            2 => ResourceKind.Oil,
            3 => ResourceKind.RareEarth,
            _ => null,
        };
        if (tile.Deposit.HasValue)
            tile.DepositAmount = 120 + hash % 181;
    }

    // 将大片不可通行高地分解为丘陵和小山峰，保留地貌同时避免阻断整个地区。
    private void LimitMountainRanges()
    {
        var visited = new bool[Current.Tiles.Count];
        var queue = new Queue<int>();
        for (var start = 0; start < Current.Tiles.Count; start++)
        {
            if (visited[start] || Current.Tiles[start].Terrain != TerrainType.Mountain)
                continue;
            queue.Enqueue(start);
            visited[start] = true;
            var count = 0;
            while (queue.TryDequeue(out var index))
            {
                if (++count > 32)
                    Current.Tiles[index].Terrain = TerrainType.Hills;
                foreach (var (dx, dy) in Directions)
                {
                    var x = index % Current.Width + dx;
                    var y = index / Current.Width + dy;
                    if (!InBounds(x, y))
                        continue;
                    var next = Index(x, y);
                    if (!visited[next] && Current.Tiles[next].Terrain == TerrainType.Mountain)
                    {
                        visited[next] = true;
                        queue.Enqueue(next);
                    }
                }
            }
        }
    }

    private static bool BuildingTerrainValid(BuildingKind kind, TileCursor tile)
    {
        return kind switch
        {
            BuildingKind.MountainPass => tile.Terrain == TerrainType.Mountain,
            BuildingKind.Bridge => tile.Terrain is TerrainType.Water or TerrainType.River or TerrainType.Lake
                or TerrainType.Stream or TerrainType.LargeRiver,
            BuildingKind.Dock or BuildingKind.Shipyard => tile.Terrain is TerrainType.Water or TerrainType.River
                or TerrainType.Lake or TerrainType.Stream or TerrainType.LargeRiver,
            BuildingKind.SacredGrove => IsForestTerrain(tile.Terrain),
            _ => !IsWaterTerrain(tile.Terrain) && (tile.IsWalkable || tile.Terrain == TerrainType.Mountain),
        };
    }

    private void CompleteLandImprovement(BuildingCursor building)
    {
        var tile = Current.Tiles[Index(building.X, building.Y)];
        if (building.Kind is BuildingKind.Farm or BuildingKind.AutomatedFarm or BuildingKind.RunicGarden)
        {
            tile.Improvement = LandImprovement.Farmland;
            if (IsForestTerrain(tile.Terrain))
                tile.Terrain = TerrainType.Grass;
        }
        else if (building.Kind == BuildingKind.MountainPass)
            tile.Replace(tile.Value with
            {
                Improvement = LandImprovement.MountainPass, RoadLevel = (byte)building.Level,
            });
        else if (building.Kind == BuildingKind.Bridge)
            tile.Replace(tile.Value with
            {
                Improvement = LandImprovement.Bridge,
                RoadLevel = (byte)building.Level,
                BridgeDirection = building.Direction,
                BridgeLevel = (byte)building.Level,
            });

        RegisterBuildingGround(building);
    }

    private void PlanVisibleCrossing(ResidentCursor person, int targetX, int targetY)
    {
        if (!Current.Rules.Construction || person.TravelMode != TravelMode.Foot || person.ArmyId != 0 || person.Age < 14
            || !HasResearch(person.SettlementId, Advancement.Logistics)
            || person.Agent.Goal.Kind is AgentGoalKind.Gather or AgentGoalKind.Explore or AgentGoalKind.FetchWater
            || (person.Agent.Goal.TargetSettlementId == 0 && person.Agent.Goal.TargetEntityId == 0))
            return;
        if (Current.Society.Buildings.Any(b =>
                b.SettlementId == person.SettlementId && (!b.IsCompleted || b.IsUpgrading)))
            return;
        // 任务确有需求仍须核对可见陆路，已有通路时不应无故建桥。
        if (Distance(person.X, person.Y, targetX, targetY) <= 6 && VisibleWorkSiteReachable(person, targetX, targetY,
                AgentInteractionRange(person, _settlements.GetValueOrDefault(person.SettlementId)) > 0))
            return;
        foreach (var (dx, dy) in Directions)
        {
            var x = person.X + dx;
            var y = person.Y + dy;
            if (!InBounds(x, y) ||
                Distance(x, y, targetX, targetY) >= Distance(person.X, person.Y, targetX, targetY))
                continue;
            var direction = dx != 0 ? BridgeDirection.Horizontal : BridgeDirection.Vertical;
            var first = Current.Tiles[Index(x, y)];
            if (first.Terrain is not (TerrainType.Water or TerrainType.River or TerrainType.Lake
                    or TerrainType.Stream or TerrainType.LargeRiver) ||
                first.Improvement == LandImprovement.Bridge)
                continue;
            // 两岸及沿途桥段须在同一轴向可见且可用，异向桥段是障碍，不能据此横向接桥。
            var farBank = false;
            var span = 0;
            var unfinished = 0;
            for (var length = 1; length <= 6; length++)
            {
                var xx = person.X + dx * length;
                var yy = person.Y + dy * length;
                if (!InBounds(xx, yy))
                    break;
                var tile = Current.Tiles[Index(xx, yy)];
                if (!IsWaterTerrain(tile.Terrain))
                {
                    farBank = tile.IsWalkable && tile.FireTicks == 0;
                    break;
                }

                if (tile.Terrain == TerrainType.DeepWater || tile.FireTicks > 0
                                                          || (tile.Improvement == LandImprovement.Bridge &&
                                                              tile.BridgeDirection != direction)
                                                          || (tile.NationId != 0 && tile.NationId != person.NationId))
                    break;
                span++;
                if (tile.Improvement != LandImprovement.Bridge)
                    unfinished++;
            }

            if (!farBank || span == 0)
                continue;
            var bankX = person.X + dx * (span + 1);
            var bankY = person.Y + dy * (span + 1);
            if (Distance(bankX, bankY, targetX, targetY) >= Distance(person.X, person.Y, targetX, targetY))
                continue;
            if (Distance(person.X, person.Y, targetX, targetY) <= 6 && !VisibleLandPathConnects(person, bankX, bankY,
                    targetX, targetY,
                    AgentInteractionRange(person, _settlements.GetValueOrDefault(person.SettlementId))))
                continue;
            var level = Math.Clamp(Math.Max((span + 3) / 4, (BridgeShoreDistance(x, y, direction) + 1) / 2), 1, 3);
            var home = _settlements[person.SettlementId];
            var reserve = LocalDevelopmentReserve(home);
            var cost = FacilityCost(BuildingKind.Bridge, level);
            if (ResourceStock.Kinds.Any(k => home.Resources.Get(k) < cost.Get(k) * unfinished + reserve.Get(k)))
                continue;
            if (FacilityPlacementError(home.Id, BuildingKind.Bridge, x, y, direction: direction, bridgeLevel: level) is
                not null)
                continue;
            var reason =
                $"{person.Name}执行{GetResidentTaskSummary(person.Id)}，可见陆路无法到达；已看见两岸，需沿{BridgeDirectionName(direction)}连接 {span} 格水面，整段材料已备齐";
            BuildPlannedFacility(home, BuildingKind.Bridge, x, y, reason, direction, level);
            return;
        }
    }

    private bool VisibleLandPathConnects(ResidentCursor observer, int originX, int originY, int targetX, int targetY,
        int range)
    {
        if (_localMoveVisited.Length != Current.Tiles.Count)
            _localMoveVisited = new int[Current.Tiles.Count];
        if (_localMoveSearch == int.MaxValue)
        {
            Array.Clear(_localMoveVisited);
            _localMoveSearch = 0;
        }

        var search = ++_localMoveSearch;
        var start = Index(originX, originY);
        _localMoveVisited[start] = search;
        _localMoveQueue[0] = (start, -1, 0);
        var head = 0;
        var tail = 1;
        while (head < tail)
        {
            var current = _localMoveQueue[head++];
            var x = current.Index % Current.Width;
            var y = current.Index / Current.Width;
            if (Distance(x, y, targetX, targetY) <= range)
                return true;
            foreach (var (dx, dy) in Directions)
            {
                var xx = x + dx;
                var yy = y + dy;
                if (!CanTraverseStep(x, y, xx, yy, TravelMode.Foot, observer.Race)
                    || Distance(xx, yy, observer.X, observer.Y) > 6 ||
                    Current.Tiles[Index(xx, yy)].FireTicks > 0)
                    continue;
                var index = Index(xx, yy);
                if (_localMoveVisited[index] == search)
                    continue;
                _localMoveVisited[index] = search;
                _localMoveQueue[tail++] = (index, -1, current.Depth + 1);
            }
        }

        return false;
    }

    private bool RemoveFailedCrossings()
    {
        var changed = false;
        foreach (var building in Current.Society.Buildings)
        {
            if (building.Kind != BuildingKind.Bridge || building.Health > 0 ||
                !InBounds(building.X, building.Y))
                continue;
            var tile = Current.Tiles[Index(building.X, building.Y)];
            if (tile.Improvement != LandImprovement.Bridge)
                continue;
            tile.Replace(tile.Value with { Improvement = LandImprovement.None, RoadLevel = 0, BridgeLevel = 0 });
            changed = true;
        }

        return changed;
    }

    private void RecordHarvest(TileCursor tile, double amount)
    {
        if (amount <= 0)
            return;
        tile.Replace(tile.Value with
        {
            LastHarvestTick = Current.Tick, Harvested = Math.Min(1_000_000_000, tile.Harvested + amount),
        });
    }

    private int VisibleDepositSite(ResidentCursor person)
    {
        if (person.Profession != Profession.Miner ||
            !_settlements.TryGetValue(person.SettlementId, out var home))
            return -1;
        var coal = HasResearch(home.Id, Advancement.Industry);
        var oil = HasResearch(home.Id, Advancement.Electrification);
        var rare = HasResearch(home.Id, Advancement.AdvancedComputing);
        if (!coal && !oil && !rare)
            return -1;
        var best = -1;
        var bestDistance = int.MaxValue;
        foreach (var offset in VisibleResourceOffsets)
        {
            var x = person.X + offset.X;
            var y = person.Y + offset.Y;
            if (!InBounds(x, y))
                continue;
            var tile = Current.Tiles[Index(x, y)];
            if (tile.Deposit is not { } resource || tile.DepositAmount <= 0 || tile.FireTicks > 0
                || !(resource == ResourceKind.Coal ? coal : resource == ResourceKind.Oil ? oil : rare))
                continue;
            if (!tile.DepositDiscovered)
            {
                tile.DepositDiscovered = true;
                AddEvent(WorldEventKind.Research, $"{person.Name}在实地勘探中发现{ResourceStock.Name(resource)}。", x, y,
                    EventAction.General, home.Id, person.Id);
            }

            if (home.Resources.Get(resource) >= 80 ||
                (person.Agent.MaterialPriority is { } needed && needed != resource))
                continue;
            var site = tile.IsWalkable
                ? Index(x, y)
                : Directions.Select(d => (X: x + d.X, Y: y + d.Y))
                    .Where(p => Walkable(p.X, p.Y) && Current.Tiles[Index(p.X, p.Y)].FireTicks == 0)
                    .OrderBy(p => Distance(person.X, person.Y, p.X, p.Y)).Select(p => Index(p.X, p.Y))
                    .FirstOrDefault(-1);
            if (site < 0)
                continue;
            var distance = Distance(person.X, person.Y, site % Current.Width, site / Current.Width);
            if (distance < bestDistance)
            {
                best = site;
                bestDistance = distance;
            }
        }

        return best;
    }

    private bool TryGatherDeposit(ResidentCursor person)
    {
        if (!_settlements.TryGetValue(person.SettlementId, out var home))
            return false;
        foreach (var index in Circle(person.X, person.Y, 1))
        {
            var tile = Current.Tiles[index];
            if (tile.Deposit is not { } kind || tile.DepositAmount <= 0 || tile.FireTicks > 0
                || DepositResearch(kind) is not { } research || !HasResearch(home.Id, research) ||
                home.Resources.Get(kind) >= 80
                || (person.Agent.MaterialPriority is { } needed && needed != kind))
                continue;
            tile.DepositDiscovered = true;
            var amount = Math.Min(tile.DepositAmount,
                WorkInterval(person) * .4 * Current.Rules.GatheringRate * GatheringCondition(person) *
                GatheringTerritoryMultiplier(person, tile) *
                (HasResearch(person.SettlementId, Advancement.Forestry) ? 1.25 : 1));
            amount = Math.Min(amount, 1_000_000 - person.Inventory.Get(kind));
            tile.DepositAmount -= amount;
            person.Inventory = person.Inventory.WithAmount(kind, person.Inventory.Get(kind) + amount);
            RecordHarvest(tile, amount);
            person.Activity = ResidentActivity.Working;
            person.Agent.Fatigue = Math.Min(100, person.Agent.Fatigue + .45 * WorkInterval(person));
            return amount > 0;
        }

        return false;
    }

    /// <summary>返回地格的生态概况。</summary>
    /// <param name="x">横向地格坐标。</param>
    /// <param name="y">纵向地格坐标。</param>
    public string GetTileEcologySummary(int x, int y)
    {
        if (!InBounds(x, y))
            return "地格不存在";
        var tile = Current.Tiles[Index(x, y)];
        return "动物种群\n" + string.Join("\n", AnimalRules.Species.Where(k => tile.AnimalPopulation(k) >= .001)
                   .Select(k => $"{WildlifeName(k)}  {tile.AnimalPopulation(k):0.###}")) + "\n植物存量\n"
               + string.Join("\n",
                   PlantResources.At(tile).Select(p =>
                       $"{PlantResources.Name(p.Kind)}  {p.Quantity:0.###} 份   覆盖 {p.Cover:P0}"));
    }

    /// <summary>返回地格当前资源与按显示策略可见的矿藏摘要。</summary>
    /// <param name="x">横向地格坐标。</param>
    /// <param name="y">纵向地格坐标。</param>
    /// <param name="visibility">观察者的矿藏显示策略。</param>
    public string GetTileResourceSummary(int x, int y, ResourceVisibility visibility = ResourceVisibility.Researched)
    {
        if (!InBounds(x, y))
            return "地格不存在";
        var tile = Current.Tiles[Index(x, y)];
        var resources = PlantResources.At(tile).OrderByDescending(p => p.Quantity)
            .Select(p => $"{PlantResources.Name(p.Kind)} {p.Quantity:0.###} 份").ToList();
        var minerals = TerrainRules.For(tile.Terrain);
        var total = minerals.StoneYield + minerals.OreYield;
        if (total > 0 && tile.ResourceAmount > 0)
        {
            if (minerals.StoneYield > 0)
                resources.Add($"石材 {tile.ResourceAmount * minerals.StoneYield / total:0.###} 份");
            if (minerals.OreYield > 0)
                resources.Add($"矿石 {tile.ResourceAmount * minerals.OreYield / total:0.###} 份");
        }

        if (IsDepositVisible(tile, visibility) && tile.Deposit is { } kind)
            resources.Add($"{ResourceStock.Name(kind)} {tile.DepositAmount:0.###} 份");
        return resources.Count > 0 ? string.Join("，", resources) : "暂无植物或矿物";
    }

    /// <summary>返回地格的生产条件摘要，矿藏按显示策略筛选。</summary>
    /// <param name="x">横向地格坐标。</param>
    /// <param name="y">纵向地格坐标。</param>
    /// <param name="visibility">观察者的矿藏显示策略。</param>
    public string GetTileProductionSummary(int x, int y, ResourceVisibility visibility = ResourceVisibility.Researched)
    {
        if (!InBounds(x, y))
            return "地格不存在";
        var tile = Current.Tiles[Index(x, y)];
        var plants = PlantResources.At(tile).ToArray();
        var products = new List<string>();
        if (tile.Improvement is LandImprovement.MountainPass or LandImprovement.Bridge)
            products.Add("通行设施");
        else
        {
            if (ResourceSiteYield(Index(x, y), Profession.Farmer) > 0)
            {
                products.AddRange(plants.Where(p => p.Kind != PlantKind.Trees)
                    .Select(p => PlantResources.ProductName(p.Kind)));
            }

            if (ResourceSiteYield(Index(x, y), Profession.Lumberjack) > 0)
                products.Add("木材");
            if (ResourceSiteYield(Index(x, y), Profession.Miner) > 0)
                products.Add("石材、矿石");
        }

        var lines = new List<string> { "资源：" + GetTileResourceSummary(x, y, visibility) };
        if (products.Count > 0)
            lines.Add("采集产物：" + string.Join("、", products));
        if (IsFreshWater(tile))
            lines.Add("每日可打水量 无限\n需到岸边打水并携带返仓");
        else if (!IsWaterTerrain(tile.Terrain))
        {
            var natural = DailyWaterYield(tile);
            lines.Add($"地块供水量 {natural:0.###}\n自然口渴消耗抵扣 {Math.Min(.5, natural / .025):0%}（成人）");
            if (FindWaterWell(Index(x, y)) is not null)
                lines.Add($"每日可打水量 {GetDailyWaterCapacity(x, y):0.###}\n今日剩余可打水量 {AvailableWater(x, y):0.###}");
        }

        if (IsWaterTerrain(tile.Terrain))
            lines.Add(tile.Terrain == TerrainType.Stream ? "通行：可涉水，速度较慢" : "通行：需要桥梁或舟船");
        if (tile.IsWalkable)
            lines.Add($"肥力 {tile.Fertility}%");
        if (tile.Improvement == LandImprovement.Farmland)
            lines.Add("耕地：需要居民到场耕作，产物随身运回家园");
        if (tile.FireTicks > 0)
            lines.Add($"正在燃烧：剩余 {tile.FireTicks} 日，暂停生产");
        else if (tile.DroughtTicks > 0)
            lines.Add($"干旱：剩余 {tile.DroughtTicks} 日，粮食减产");
        else if (tile.ResourceAmount >= 1 && tile.IsWalkable)
            lines.Add("状态：可以采收");
        if (IsDepositVisible(tile, visibility) && tile.Deposit is { } kind)
            lines.Add($"{ResourceStock.Name(kind)}矿藏：{tile.DepositAmount:0.#}（不可再生）");
        var animals = AnimalRules.Species.Where(k => tile.AnimalPopulation(k) >= .001)
            .Select(k => $"{WildlifeName(k)} {tile.AnimalPopulation(k):0.###}").ToArray();
        if (animals.Length > 0)
            lines.Add("动物：" + string.Join("、", animals));
        return string.Join("\n", lines);
    }
}
