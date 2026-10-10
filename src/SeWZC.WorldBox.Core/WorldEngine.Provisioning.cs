using SeWZC.WorldBox.Core.Runtime;

namespace SeWZC.WorldBox.Core;

public sealed partial class WorldEngine
{
    private readonly List<int> _dailyWaterSources = [];
    private bool _dailyDrinking;
    private double[] _dailyWaterDraws = [];
    private int _dailyWaterVisit;
    private int[] _dailyWaterVisits = [];

    private void BeginDailyDrinking()
    {
        if (_dailyWaterDraws.Length != Tiles.Count)
        {
            _dailyWaterDraws = new double[Tiles.Count];
            _dailyWaterVisits = new int[Tiles.Count];
        }

        if (_dailyWaterVisit == int.MaxValue)
        {
            Array.Clear(_dailyWaterVisits);
            _dailyWaterVisit = 0;
        }

        _dailyWaterVisit++;
        _dailyWaterSources.Clear();
        _dailyDrinking = true;
    }

    private void EndDailyDrinking()
    {
        _dailyDrinking = false;
        foreach (var source in _dailyWaterSources)
        {
            var tile = Tiles[source];
            tile.Replace(tile.Value with { WaterDrawTick = SimulationTick, WaterDrawn = _dailyWaterDraws[source] });
        }

        _dailyWaterSources.Clear();
    }

    /// <summary>计算居民每日所需粮食资源量。</summary>
    /// <param name="person">居民。</param>
    public static double FoodUse(Resident person)
    {
        return FoodUse(person.Age, person.Race);
    }

    private static double FoodUse(StateReference<Resident> person)
    {
        return FoodUse(person.Value.Age, person.Value.Race);
    }

    internal static double FoodUse(double age, RaceKind race)
    {
        return age < 14 ? .02 : race == RaceKind.Orc ? .052 : .04;
    }

    /// <summary>计算居民每日所需饮水资源量。</summary>
    /// <param name="person">居民。</param>
    public static double WaterUse(Resident person)
    {
        return WaterUse(person.Age);
    }

    private static double WaterUse(StateReference<Resident> person)
    {
        return WaterUse(person.Value.Age);
    }

    internal static double WaterUse(double age, Tile? tile = null)
    {
        var use = age < 14 ? .015 : .025;
        return tile is null ? use : use - Math.Min(use * .5, DailyWaterYield(tile));
    }

    /// <summary>计算居民在指定环境中抵扣部分自然口渴后，每日仍需饮用的水量。</summary>
    /// <param name="person">居民。</param>
    /// <param name="tile">居民当前所在环境；最多抵扣一半基础需求。</param>
    public static double WaterUse(Resident person, Tile tile)
    {
        return WaterUse(person.Age, tile);
    }

    private double LocalWaterUse(StateReference<Resident> person)
    {
        return WaterUse(person.Value.Age, Tiles[Index(person.Value.X, person.Value.Y)].Value);
    }

    private double WaterReserve(StateReference<Resident> person)
    {
        return WaterReserve(person.Value);
    }

    private static double WaterReserve(Resident person)
    {
        return Math.Clamp(.75 + Distance(person.X, person.Y,
                person.Agent.Goal.TargetX, person.Agent.Goal.TargetY) * WaterUse(person) * 6 /
            SimulationTime.TicksPerDay,
            .75, 6);
    }

    // 普通居民补足随身储备，承担公共运水的居民再多装三份；不是每个人都需承担整趟公共运输。
    private double WaterCollectionTarget(StateReference<Resident> person)
    {
        var source = person.Value.Agent.Goal.TargetEntityId - 1;
        // 低产水井先保住两日个人备用，避免为装满大瓶长期停留。
        if (person.Value.Agent.Goal.Kind == AgentGoalKind.FetchWater && source >= 0 && source < Tiles.Count
            && GetDailyWaterCapacity(source % Width, source / Width) < .1)
            return WaterUse(person) * 2;
        return WaterReserve(person) + (person.Value.Id % 5 == 0 ? 3 : 0);
    }

    private InventoryTransfer ProvisionAtHome(Resident person, in ResourceStock inventory, Settlement home,
        in ResourceStock warehouse)
    {
        var rules = Rules;
        if (!InventoryTransfer.HasSupplies(warehouse, person.Profession, rules.Hunger, rules.Thirst))
            return new InventoryTransfer(inventory, warehouse);
        return InventoryTransfer.Provision(inventory, warehouse, home.Population, person.Profession,
            rules.Hunger, rules.Thirst, TravelReserve(person), WaterReserve(person), FoodUse(person), WaterUse(person));
    }

    private void RefillDailyWater(StateReference<Resident> person)
    {
        var use = LocalWaterUse(person) / SimulationTime.TicksPerDay;
        if (Rules.Thirst && person.Value.Inventory.Water < use &&
            SimulationTick - person.Value.MoveStartedTick >= person.Value.MoveDurationTicks)
            DrawWater(person, Index(person.Value.X, person.Value.Y), use - person.Value.Inventory.Water);
    }

    private void DrinkCarriedWater(StateReference<Resident> person)
    {
        if (!Rules.Thirst)
        {
            person.Replace(person.Value.WithThirst(0));
            return;
        }

        RefillDailyWater(person);
        var use = LocalWaterUse(person) / SimulationTime.TicksPerDay;
        var drink = Math.Min(use, person.Value.Inventory.Water);
        person.Replace(person.Value.WithInventory(person.Value.Inventory with { Water = person.Value.Inventory.Water - drink }));
        person.Replace(person.Value.WithThirst(Math.Clamp(
                person.Value.Thirst + (drink >= use - .000001
                    ? -3d / SimulationTime.TicksPerDay
                    : .6 * (use - drink) / WaterUse(person)), 0, 100)));
        if (person.Value.Thirst > 95)
            DamageResident(person, .25 / SimulationTime.TicksPerDay, DeathCause.Dehydration);
    }

    /// <summary>计算此格当日扣除已取水量后的可打水量；淡水水域可为正无穷，普通陆地为零。</summary>
    /// <param name="x">横向地格坐标。</param>
    /// <param name="y">纵向地格坐标。</param>
    public double AvailableWater(int x, int y)
    {
        if (!InBounds(x, y))
            return 0;
        var index = Index(x, y);
        var tile = Tiles[index].Value;
        if (tile.FireTicks > 0)
            return 0;
        var supply = WaterSupplyAt(index, tile).Supply;
        return Math.Max(0,
            supply - (SimulationTime.DayIndex(tile.WaterDrawTick) == SimulationTime.DayIndex(SimulationTick)
                ? tile.WaterDrawn
                : 0));
    }

    // 无限供水只由查询推导，避免把无穷值写入存档资源。
    /// <summary>计算受干旱影响后的环境供水量，供口渴、生态和水井计算；淡水水域返回正无穷。</summary>
    /// <param name="tile">要计算环境供水量的地格。</param>
    public static double DailyWaterYield(Tile tile)
    {
        return IsFreshWater(tile)
            ? double.PositiveInfinity
            : tile.NaturalWaterYield * (tile.DroughtTicks > 0 ? .2 : 1);
    }

    /// <summary>根据环境供水计算此格水井的每日可打水量，水域返回零。</summary>
    /// <param name="tile">水井所在的地格。</param>
    public static double WellWaterYield(Tile tile)
    {
        return IsWaterTerrain(tile.Terrain)
            ? 0
            : Math.Max(0, 30 * DailyWaterYield(tile) - .6);
    }

    /// <summary>计算此格淡水水域或运营水井的每日可打水量，普通陆地为零。</summary>
    /// <param name="x">横向地格坐标。</param>
    /// <param name="y">纵向地格坐标。</param>
    public double GetDailyWaterCapacity(int x, int y)
    {
        if (!InBounds(x, y))
            return 0;
        var index = Index(x, y);
        var tile = Tiles[index].Value;
        if (tile.FireTicks > 0)
            return 0;
        return WaterSupplyAt(index, tile).Supply;
    }

    // 已知地格只查询一次供水和水井状态；调用方处理边界和火场。
    private (double Supply, bool Reliable) WaterSupplyAt(int index, Tile tile)
    {
        if (IsFreshWater(tile))
            return (double.PositiveInfinity, true);
        if (IsWaterTerrain(tile.Terrain))
            return (0, false);
        var well = _localWorkQueriesActive
            ? _localWaterWells.GetValueOrDefault(index)
            : FindWaterWell(index);
        var operating = well is not null && IsBuildingOperational(well.Value);
        return (operating ? WellWaterYield(tile) : 0, operating);
    }

    private StateReference<Building>? FindWaterWell(int index)
    {
        var x = index % Width;
        var y = index / Width;
        foreach (var building in Buildings)
            if (building.Value.Kind == BuildingKind.Well && building.Value.X == x && building.Value.Y == y)
                return building;
        return null;
    }

    private double DrawWater(StateReference<Resident> person, int source, double wanted)
    {
        var amount = WithdrawWater(person.Value.X, person.Value.Y, person.Value.MoveStartedTick, person.Value.MoveDurationTicks,
            person.Value.Inventory.Water, source, wanted);
        if (amount > 0)
            person.Replace(person.Value.WithInventory(person.Value.Inventory with { Water = person.Value.Inventory.Water + amount }));
        return amount;
    }

    private double WithdrawWater(int x, int y, long moveStarted, int moveDuration, double carriedWater, int source,
        double wanted)
    {
        if ((uint)source >= (uint)Tiles.Count
            || SimulationTick - moveStarted < moveDuration)
            return 0;
        var tile = Tiles[source];
        var before = tile.Value;
        var fresh = IsFreshWater(before);
        if (Distance(x, y, source % Width, source / Width) > (fresh ? 1 : 0)
            || before.FireTicks > 0)
            return 0;
        var supply = WaterSupplyAt(source, before).Supply;
        var drawn = _dailyDrinking && _dailyWaterVisits[source] == _dailyWaterVisit
            ? _dailyWaterDraws[source]
            : SimulationTime.DayIndex(before.WaterDrawTick) == SimulationTime.DayIndex(SimulationTick)
                ? before.WaterDrawn
                : 0;
        var available = Math.Max(0, supply - drawn);
        var amount = Math.Min(Math.Max(0, wanted), available);
        amount = Math.Min(amount, 1_000_000 - carriedWater);
        if (amount <= 0)
            return 0;
        // 身体结算只领取不足一天的饮水，同一水源按居民顺序扣额，最后统一提交地格。
        if (_dailyDrinking && !fresh)
        {
            if (_dailyWaterVisits[source] != _dailyWaterVisit)
            {
                _dailyWaterVisits[source] = _dailyWaterVisit;
                _dailyWaterSources.Add(source);
            }

            _dailyWaterDraws[source] = drawn + amount;
            return amount;
        }

        // 取水额度和采集记录属于同一次现场操作，合并为一个不可变地格更新。
        if (!fresh || amount > .05)
        {
            tile.Replace(before with
            {
                WaterDrawTick = fresh ? before.WaterDrawTick : SimulationTick,
                WaterDrawn = fresh ? before.WaterDrawn : drawn + amount,
                LastHarvestTick = amount > .05 ? SimulationTick : before.LastHarvestTick,
                Harvested = amount > .05 ? Math.Min(1_000_000_000, before.Harvested + amount) : before.Harvested,
            });
        }

        return amount;
    }

    private (int Bank, int Source) FindWaterSite(StateReference<Resident> person, bool collectionOnly)
    {
        var reachable = 0;
        // 可见范围之外的水源须有实际获知的地址，避免居民凭全图状态找水。
        var bestBank = -1;
        var bestSource = -1;
        var bestScore = double.NegativeInfinity;
        var sufficient = false;
        var required = WaterUse(person);
        var urgent = person.Value.Thirst >= 80 && person.Value.Inventory.Water < required;
        // 一次查询只读一次个人水源记忆，不为每个候选岸边重复扫描整份记忆。
        Span<byte> familiarSources = stackalloc byte[13 * 13];
        familiarSources.Clear();
        foreach (var fact in person.Value.Agent.Memory)
        {
            if (fact.Kind != AgentFactKind.WaterSource || fact.OriginResidentId != person.Value.Id
                                                       || fact.ReliabilityAt(SimulationTick) < .5)
                continue;
            var source = fact.SubjectId - 1;
            var localX = source % Width - person.Value.X + 6;
            var localY = source / Width - person.Value.Y + 6;
            if ((uint)source < Tiles.Count && (uint)localX < 13 && (uint)localY < 13)
                familiarSources[localY * 13 + localX] = 1;
        }

        void ConsiderBank(int bank, int source, double available, bool reliable, bool familiar)
        {
            var fullDay = available >= required;
            if (sufficient && !fullDay)
                return;
            var distance = Distance(person.Value.X, person.Value.Y, bank % Width, bank / Width);
            var bankUpperScore = urgent
                ? -distance + Math.Min(1, available / required) * .01
                : Math.Min(1, available) * (reliable ? 2 : 1) * 1.1 / (1 + distance * .4);
            if (fullDay == sufficient && bankUpperScore <= bestScore)
                return;
            // 严重缺水优先缩短到岸时间；日常取水重视足够装瓶的供水及亲眼确认过的可靠来源。
            var score = urgent
                ? -distance + Math.Min(1, available / required) * .01
                : Math.Min(1, available) * (reliable ? 2 : 1) * (familiar ? 1.1 : 1) / (1 + distance * .4);
            if ((fullDay == sufficient && score <= bestScore)
                || !VisibleSiteReachable(person, bank, ref reachable))
                return;
            bestScore = score;
            bestBank = bank;
            bestSource = source;
            sufficient = fullDay;
        }

        void Consider(int source, ReadOnlySpan<byte> familiarSites)
        {
            if (source < 0 || source >= Tiles.Count)
                return;
            var tile = Tiles[source].Value;
            if (tile.FireTicks > 0)
                return;
            var water = WaterSupplyAt(source, tile);
            // 产量须能保障个人饮水并补出储备，避免在低产水井长期等水。
            if (water.Supply <= WaterUse(person))
                return;
            if (collectionOnly && water.Supply < .1)
                return;
            var available = Math.Max(0,
                water.Supply - (SimulationTime.DayIndex(tile.WaterDrawTick) == SimulationTime.DayIndex(SimulationTick)
                    ? tile.WaterDrawn
                    : 0));
            if (available <= 0)
                return;
            var local = (source / Width - person.Value.Y + 6) * 13 + source % Width - person.Value.X + 6;
            var familiar = familiarSites[local] != 0;
            if (RaceTerrainRules.CanWalk(tile, person.Value.Race))
            {
                ConsiderBank(source, source, available, water.Reliable, familiar);
                return;
            }

            var x = source % Width;
            var y = source / Width;
            foreach (var (dx, dy) in Directions)
            {
                var xx = x + dx;
                var yy = y + dy;
                if (!Walkable(xx, yy, person.Value.Race) || Tiles[Index(xx, yy)].Value.FireTicks > 0)
                    continue;
                ConsiderBank(Index(xx, yy), source, available, water.Reliable, familiar);
            }
        }

        foreach (var offset in VisibleResourceOffsets)
        {
            if (offset.Distance > 6)
                break;
            // 后续水源的岸边至多近一格；最高供水和熟悉加成也不能超过此评分上界。
            var nearestBank = Math.Max(0, offset.Distance - 1);
            var upperScore = urgent ? -nearestBank + .01 : 2.2 / (1 + nearestBank * .4);
            if (sufficient && upperScore <= bestScore)
                break;
            var x = person.Value.X + offset.X;
            var y = person.Value.Y + offset.Y;
            if (InBounds(x, y))
                Consider(Index(x, y), familiarSources);
        }

        // 远方已知水源可引导探索，但当日补水须使用当前可见且可达的岸边。
        if (bestSource >= 0 &&
            Distance(person.Value.X, person.Value.Y, bestSource % Width, bestSource / Width) <= 6)
        {
            AgentFact? existing = null;
            foreach (var fact in person.Value.Agent.Memory)
                if (fact.Kind == AgentFactKind.WaterSource && fact.SubjectId == bestSource + 1)
                {
                    existing = fact;
                    break;
                }

            if (existing is null || SimulationTick - existing.ObservedTick >= SimulationTime.TicksPerYear)
            {
                RememberAgentFact(person, MakeAgentFact(person, AgentFactKind.WaterSource, bestSource + 1,
                    bestSource % Width, bestSource / Width, 1, "实地发现可取水的河湖或运营水井"));
            }
        }

        return (bestBank, bestSource);
    }

    private bool AddProvisionChoices(StateReference<Resident> person, StateReference<Settlement> home, List<GoalChoice> choices)
    {
        // 已严重缺粮而每日仍喝得到水时，先找食物；不能为尚不需要的装瓶储备反复搜索。
        var prioritizeFood = Rules.Hunger && person.Value.Hunger >= 60
                                                  && person.Value.Inventory.Food < .05 && person.Value.Thirst < 10;
        var needsWater = person.Value.Inventory.Water < .3
                         && (person.Value.Thirst >= 10 || (person.Value.Inventory.Water < LocalWaterUse(person) * 4
                                                     && GetDailyWaterCapacity(person.Value.X, person.Value.Y) <
                                                     LocalWaterUse(person)));
        var refillReserve = person.Value.Inventory.Water < WaterUse(person) * 4;
        if (Rules.Thirst && !prioritizeFood &&
            (needsWater || refillReserve || (person.Value.Id % 5 == 0 && person.Value.Inventory.Water < WaterReserve(person) + 3)))
        {
            var atHome = Distance(person.Value.X, person.Value.Y, home.Value.X, home.Value.Y) <= 1;
            if (atHome && home.Value.Resources.Water >= .3)
            {
                choices.Add(new GoalChoice(AgentGoalKind.Eat, home.Value.X, home.Value.Y, 75 + person.Value.Thirst, "在家园领取随身饮水与口粮",
                    SettlementId: home.Value.Id));
            }
            else
            {
                var water = FindWaterSite(person, !needsWater);
                if (water.Bank >= 0 && (needsWater ||
                                        GetDailyWaterCapacity(water.Source % Width,
                                            water.Source / Width) >= .1))
                {
                    choices.Add(new GoalChoice(AgentGoalKind.FetchWater, water.Bank % Width,
                        water.Bank / Width,
                        needsWater || refillReserve ? 65 + person.Value.Thirst :
                        atHome && home.Value.Resources.Water < home.Value.Population * .5 ? 72 : 18,
                        person.Value.Thirst >= 80
                            ? "严重缺水，优先就近补充饮水"
                            : "比较眼前可达水源的可打水量与距离，到河湖或运营水井装水，再随身携带并运回家园",
                        EntityId: water.Source + 1));
                }
                else if (needsWater && water.Bank < 0)
                {
                    if (person.Value.Thirst < 40 && Distance(person.Value.X, person.Value.Y, home.Value.X, home.Value.Y) >= 12
                                           && !(person.Value.Agent.Goal.Kind == AgentGoalKind.ReturnHome &&
                                                SimulationTick - person.Value.Agent.Goal.StartedTick >
                                                2 * SimulationTime.TicksPerDay))
                    {
                        choices.Add(new GoalChoice(AgentGoalKind.ReturnHome, home.Value.X, home.Value.Y, 180 + person.Value.Thirst,
                            "寻水已到安全补给边界，返回已知家园后重新安排路线", SettlementId: home.Value.Id));
                        return true;
                    }

                    var heading = Directions[person.Value.Agent.ExplorationHeading % 4];
                    var best = -1;
                    var bestDistance = int.MaxValue;
                    var reachable = 0;
                    foreach (var offset in VisibleResourceOffsets)
                    {
                        var x = person.Value.X + offset.X;
                        var y = person.Value.Y + offset.Y;
                        if (!Walkable(x, y, person.Value.Race) || Tiles[Index(x, y)].Value.FireTicks > 0)
                            continue;
                        var distance = Distance(x, y, person.Value.X + heading.X * 6, person.Value.Y + heading.Y * 6);
                        if (distance < bestDistance && VisibleSiteReachable(person, Index(x, y), ref reachable))
                        {
                            best = Index(x, y);
                            bestDistance = distance;
                        }
                    }

                    if (best >= 0)
                    {
                        choices.Add(new GoalChoice(AgentGoalKind.FetchWater, best % Width, best / Width,
                            50 + person.Value.Thirst, "沿眼前可通行土地勘察水源；尚未发现河湖或运营水井"));
                    }
                }
            }

            // 当前地点连下一日饮水都不能保障时，先执行补水；不为非紧急工作重复评估全套候选。
            if ((needsWater || refillReserve) && (person.Value.Hunger < 60 || person.Value.Thirst >= 80)
                                              && choices.Any(choice =>
                                                  choice.Kind is AgentGoalKind.FetchWater or AgentGoalKind.Eat))
                return true;
        }

        if (Rules.Thirst && person.Value.Inventory.Water >= WaterReserve(person) + 2)
        {
            choices.Add(new GoalChoice(AgentGoalKind.ReturnHome, home.Value.X, home.Value.Y, 110, "携带打来的饮水返仓，为家园补给",
                SettlementId: home.Value.Id));
        }

        var homeDistance = Distance(person.Value.X, person.Value.Y, home.Value.X, home.Value.Y);
        var daysHome = (homeDistance * 4 + 12) / (double)SimulationTime.TicksPerDay;
        if (Rules.Hunger && homeDistance > 1 && person.Value.Hunger < 20
            && person.Value.Inventory.Food < FoodUse(person) * daysHome)
        {
            choices.Add(new GoalChoice(AgentGoalKind.ReturnHome, home.Value.X, home.Value.Y, 85, "口粮接近保守返程需求，先实地返仓补给",
                SettlementId: home.Value.Id));
        }

        if (person.Value.Age < ResidentNeedsRules.MinimumWorkAge)
            return false;
        var foodNeeded = FoodSupplyNeeded(person, home);
        if (foodNeeded)
            AddBoatFishingChoice(person, home, choices);
        var wildlife =
            foodNeeded && (person.Value.Profession is Profession.Farmer or Profession.Fisher ||
                           (person.Value.Hunger > 20 && person.Value.Inventory.Food < .3))
                ? FindHarvestableWildlife(person)
                : (-1, -1, false);
        if (wildlife.Item1 >= 0)
        {
            choices.Add(new GoalChoice(wildlife.Item3 ? AgentGoalKind.Fish : AgentGoalKind.Hunt,
                wildlife.Item1 % Width, wildlife.Item1 / Width,
                (person.Value.Inventory.Food < .3 && person.Value.Hunger > 20 ? 100 :
                    person.Value.Profession is Profession.Farmer or Profession.Fisher ? 43 : 14)
                + (person.Value.Inventory.Food < .3 ? person.Value.Hunger : 0),
                wildlife.Item3 ? "在眼前水岸捕鱼，鱼群数量会实际减少" : "在眼前栖息地狩猎，将食物携带返乡",
                EntityId: wildlife.Item2 + 1));
        }

        var claim = VisibleClaimSite(person, home);
        if (claim >= 0)
        {
            choices.Add(new GoalChoice(AgentGoalKind.ClaimLand, claim % Width, claim / Width,
                (IsSettlementActive(home.Value.Id) ? SettlementNeedsClaimArea(home) ? 58 : home.Value.IsExpanding ? 52 : 25 : 70) +
                person.Value.Agent.Personality.Ambition * 8,
                !IsSettlementActive(home.Value.Id)
                    ? $"城镇占地 {GetSettlementArea(home.Value.Id)}/{SettlementActivationArea} 格，加成尚未生效，先实地登记领地"
                    : SettlementNeedsClaimArea(home)
                        ? $"升级面积 {GetSettlementArea(home.Value.Id)}/{GetSettlementExpansionArea(home.Value.Id)} 格，先实地登记相邻领地"
                        : "实际前往可达的相邻边界地块，登记城镇地盘", SettlementId: home.Value.Id));
        }

        return false;
    }

    /// <summary>尝试从当前任务的实际水源取水放入随身库存；返回是否取到水。</summary>
    /// <param name="person">居民。</param>
    public bool TryFetchWater(Resident person)

    {
        return TryFetchWater(RequireResident(person.Id));
    }

    private bool TryFetchWater(StateReference<Resident> person)
    {
        if (person.Value.Agent.Goal.Kind != AgentGoalKind.FetchWater || person.Value.Health <= 0
            || !ResidentNeedsRules.CanWork(person.Value))
            return false;
        var source = person.Value.Agent.Goal.TargetEntityId - 1;
        if (source >= 0 && (source >= Tiles.Count
                            || GetDailyWaterCapacity(source % Width, source / Width)
                            <= WaterUse(person)))
        {
            person.Replace(person.Value.WithAgent(person.Value.Agent.WithGoal(new AgentGoal
            {
                TargetX = person.Value.X, TargetY = person.Value.Y, ReviewTick = SimulationTick, Reason = "此处无法持续补充饮水，重新寻找河湖或运营水井",
            })));
            person.Replace(person.Value.WithAgent(person.Value.Agent with { NextThinkTick = SimulationTick }));
            return false;
        }

        var target = WaterCollectionTarget(person);
        var amount = DrawWater(person, source,
            Math.Min(
                source >= 0 && source < Tiles.Count
                    ? GatheringTerritoryMultiplier(person.Value.SettlementId, person.Value.NationId, Tiles[source].Value)
                        * ResidentNeedsRules.WorkEfficiency(person.Value)
                    : 0, target - person.Value.Inventory.Water));
        if (amount > 0)
            person.Replace(person.Value.WithAgent(person.Value.Agent with
            {
                Fatigue = ResidentNeedsRules.ExertionFatigue(person.Value, ResidentNeedsRules.PhysicalWorkCostPerTick),
            }));
        if (source >= 0 && person.Value.Inventory.Water >= target - .000001)
        {
            // 当场完成后结束取水目标，避免下一日喝掉少量水又被当成尚未完成。
            var home = _settlements.GetValueOrDefault(person.Value.SettlementId);
            var deliver = person.Value.Id % 5 == 0 && home is not null
                                             && person.Value.Inventory.Water >= WaterReserve(person) + 2;
            person.Replace(person.Value.WithAction(person.Value.Agent with
            {
                Goal = new AgentGoal
                {
                    Kind = deliver ? AgentGoalKind.ReturnHome : AgentGoalKind.Idle,
                    TargetX = deliver ? home!.Value.X : person.Value.X,
                    TargetY = deliver ? home!.Value.Y : person.Value.Y,
                    TargetSettlementId = deliver ? home!.Value.Id : 0,
                    StartedTick = SimulationTick,
                    ReviewTick = SimulationTick,
                    Reason = deliver ? "装好公共补给后实地运回家园" : "已补足随身饮水，重新安排其他事务",
                },
                NextThinkTick = SimulationTick,
            }, ResidentActivity.Working));
            return amount > 0;
        }

        if (source < 0 || amount <= 0)
        {
            // 供水仍会每日恢复时，额度已用完只是当日受阻；保留水源并等待，避免每天转向和重新搜索。
            if (source >= 0 && person.Value.Inventory.Water < target
                            && GetDailyWaterCapacity(source % Width, source / Width) > 0)
            {
                person.Replace(person.Value.WithActivity(ResidentActivity.Resting));
                return false;
            }

            // 完成探索段后继续向外，边界受阻才转向，避免每次到达都转向而绕同一小圈。
            if (source >= 0 || person.Value.Agent.Goal.WorkTicks > 1)
                person.Replace(person.Value.WithAgent(person.Value.Agent with { ExplorationHeading = (person.Value.Agent.ExplorationHeading + 1) % 8 }));
            person.Replace(person.Value.WithAgent(person.Value.Agent with { NextThinkTick = SimulationTick }));
        }

        person.Replace(person.Value.WithActivity(ResidentActivity.Working));
        return amount > 0;
    }

    private (int Site, int Source, bool Fishing) FindHarvestableWildlife(StateReference<Resident> person)
    {
        var reachable = 0;
        // 先寻找距离最近且种群仍可持续的猎场，不穷举比较整个视野的最大产量。
        foreach (var index in VisibleWildlifeSites(person))
        {
            var x = index % Width;
            var y = index / Width;
            var tile = Tiles[index];
            if (tile.Value.FireTicks > 0)
                continue;
            var aquatic = IsWaterTerrain(tile.Value.Terrain);
            var kind = EdibleAnimal(tile, aquatic);
            var efficiency = WildlifeHarvestEfficiency(tile, kind);
            if (kind == WildlifeKind.None || efficiency < .25)
                continue;
            if (Rules.Hunger && person.Value.Hunger > 20 && person.Value.Inventory.Food < .3)
            {
                var interval = WorkInterval(person);
                var food = WildlifeHarvestAmount(tile, kind, interval * .15 * Rules.GatheringRate
                                                             * GatheringCondition(person.Value.SicknessTicks, person.Value.Hunger, person.Value.Thirst) *
                                                             GatheringTerritoryMultiplier(person.Value.SettlementId, person.Value.NationId, tile.Value))
                    * AnimalRules.For(kind).BodyMass / interval;
                if (food < FoodUse(person))
                    continue;
            }

            if (!aquatic && RaceTerrainRules.CanWalk(tile.Value, person.Value.Race)
                         && VisibleSiteReachable(person, index, ref reachable))
                return (index, index, false);

            if (!aquatic)
                continue;
            foreach (var (dx, dy) in Directions)
                if (Walkable(x + dx, y + dy, person.Value.Race) && Tiles[Index(x + dx, y + dy)].Value.FireTicks == 0
                                                          && VisibleSiteReachable(person, Index(x + dx, y + dy),
                                                              ref reachable))
                    return (Index(x + dx, y + dy), index, true);
        }

        return (-1, -1, false);
    }

    private void AddBoatFishingChoice(StateReference<Resident> person, StateReference<Settlement> home, List<GoalChoice> choices)
    {
        if (person.Value.Profession != Profession.Fisher || person.Value.Age < ResidentNeedsRules.MinimumWorkAge
                                                   || (person.Value.TravelMode != TravelMode.Boat &&
                                                       !(Distance(person.Value.X, person.Value.Y, home.Value.X, home.Value.Y) <= 1
                                                         && HasResearch(home.Value.Id, Advancement.Logistics) &&
                                                         home.Value.Resources.Boats >= 1)))
            return;
        var reachable = 0;
        var best = -1;
        var score = double.NegativeInfinity;
        foreach (var offset in VisibleResourceOffsets)
        {
            if (offset.Distance > 6)
                break;
            var x = person.Value.X + offset.X;
            var y = person.Value.Y + offset.Y;
            if (!InBounds(x, y))
                continue;
            var index = Index(x, y);
            var tile = Tiles[index];
            var animal = EdibleAnimal(tile, true);
            var efficiency = WildlifeHarvestEfficiency(tile, animal);
            if (animal == WildlifeKind.None || efficiency < .25 || tile.Value.FireTicks > 0)
                continue;
            var value = Math.Min(tile.Value.AnimalPopulation(animal) * .1, .15 * efficiency) *
                AnimalRules.For(animal).BodyMass / (1 + offset.Distance * .2);
            if (value > score && VisibleSiteReachable(person, index, ref reachable, TravelMode.Boat))
            {
                best = index;
                score = value;
            }
        }

        if (best >= 0)
        {
            choices.Add(new GoalChoice(AgentGoalKind.Fish, best % Width, best / Width, 60,
                "领取家园舟船，前往眼前鱼群捕鱼，装满后返岸交付鱼获与舟船", EntityId: best + 1));
        }
        else if (person.Value.TravelMode == TravelMode.Boat)
        {
            choices.Add(new GoalChoice(AgentGoalKind.ReturnHome, home.Value.X, home.Value.Y, 90,
                "附近鱼群已不足，返岸交付鱼获与舟船", SettlementId: home.Value.Id));
        }
    }


    /// <summary>尝试在当前位置狩猎或捕鱼，将实际减少的动物转为随身粮食；返回是否取得产物。</summary>
    /// <param name="person">居民。</param>
    public bool TryHarvestWildlife(Resident person)

    {
        return TryHarvestWildlife(RequireResident(person.Id));
    }

    private bool TryHarvestWildlife(StateReference<Resident> person)
    {
        var goal = person.Value.Agent.Goal;
        var source = goal.TargetEntityId - 1;
        if (person.Value.Health <= 0 || !ResidentNeedsRules.CanWork(person.Value) || source < 0 || source >= Tiles.Count
            || goal.Kind is not (AgentGoalKind.Hunt or AgentGoalKind.Fish)
            || SimulationTick - person.Value.MoveStartedTick < person.Value.MoveDurationTicks
            || Distance(person.Value.X, person.Value.Y, source % Width, source / Width) >
            (goal.Kind == AgentGoalKind.Fish ? 1 : 0))
            return false;
        var tile = Tiles[source];
        var kind = EdibleAnimal(tile, goal.Kind == AgentGoalKind.Fish);
        if (kind == WildlifeKind.None || tile.Value.FireTicks > 0)
        {
            person.Replace(person.Value.WithAgent(person.Value.Agent with { NextThinkTick = SimulationTick }));
            return false;
        }

        var yield = AnimalRules.For(kind).BodyMass;
        var amount = WildlifeHarvestAmount(tile, kind,
            WorkDays(person) * .15 * Rules.GatheringRate * GatheringCondition(person.Value.SicknessTicks, person.Value.Hunger, person.Value.Thirst) *
            GatheringTerritoryMultiplier(person.Value.SettlementId, person.Value.NationId, tile.Value));
        amount = Math.Min(amount, (1_000_000 - person.Value.Inventory.Food) / yield);
        tile.Replace(tile.Value.WithAnimalPopulation(kind, tile.Value.AnimalPopulation(kind) - amount));
        person.Replace(person.Value.WithInventory(person.Value.Inventory with { Food = person.Value.Inventory.Food + amount * yield }));
        RecordHarvest(tile, amount * yield);
        person.Replace(person.Value.WithAction(person.Value.Agent with
        {
            Fatigue = ResidentNeedsRules.ExertionFatigue(person.Value, ResidentNeedsRules.PhysicalWorkCostPerTick * WorkInterval(person)),
        }, ResidentActivity.Working));
        if (!goal.PlayerDirected && !WildlifeSiteProductive(tile, goal.Kind == AgentGoalKind.Fish))
            person.Replace(person.Value.WithAgent(person.Value.Agent with { NextThinkTick = SimulationTick + 1 }));
        return amount > 0;
    }
}
