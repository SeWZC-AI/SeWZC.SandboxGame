using SeWZC.WorldBox.Core.Runtime;
namespace SeWZC.WorldBox.Core;

public sealed partial class WorldEngine
{
    private bool _dailyDrinking;
    private double[] _dailyWaterDraws = [];
    private int[] _dailyWaterVisits = [];
    private int _dailyWaterVisit;
    private readonly List<int> _dailyWaterSources = [];

    private void BeginDailyDrinking()
    {
        if (_dailyWaterDraws.Length != Current.Tiles.Count)
        {
            _dailyWaterDraws = new double[Current.Tiles.Count];
            _dailyWaterVisits = new int[Current.Tiles.Count];
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
            var tile = Current.Tiles[source];
            tile.Replace(tile.Value with { WaterDrawTick = Current.Tick, WaterDrawn = _dailyWaterDraws[source] });
        }
        _dailyWaterSources.Clear();
    }
    /// <summary>计算居民每日所需粮食资源量。</summary>
    /// <param name="person">居民。</param>
    public static double FoodUse(Resident person)
        => FoodUse(person.Age, person.Race);

    private static double FoodUse(ResidentCursor person) => FoodUse(person.Age, person.Race);
    private static double FoodUse(double age, RaceKind race) => age < 14 ? .02 : race == RaceKind.Orc ? .052 : .04;

    /// <summary>计算居民每日所需饮水资源量。</summary>
    /// <param name="person">居民。</param>
    public static double WaterUse(Resident person)
        => WaterUse(person.Age);

    private static double WaterUse(ResidentCursor person) => WaterUse(person.Age);
    internal static double WaterUse(double age, Tile? tile = null)
    {
        var use = age < 14 ? .015 : .025;
        return tile is null ? use : use - Math.Min(use * .5, DailyWaterYield(tile));
    }

    /// <summary>计算居民在指定环境中抵扣部分自然口渴后，每日仍需饮用的水量。</summary>
    /// <param name="person">居民。</param>
    /// <param name="tile">居民当前所在环境；最多抵扣一半基础需求。</param>
    public static double WaterUse(Resident person, Tile tile) => WaterUse(person.Age, tile);

    private double LocalWaterUse(ResidentCursor person) => WaterUse(person.Age, Current.Tiles[Index(person.X, person.Y)]);

    private double WaterReserve(ResidentCursor person)
    {
        return Math.Clamp(.75 + Distance(person.X, person.Y,
            person.Agent.Goal.TargetX, person.Agent.Goal.TargetY) * WaterUse(person) * 6, .75, 6);
    }

    // 普通居民补足随身储备，承担公共运水的居民再多装三份；不是每个人都需承担整趟公共运输。
    private double WaterCollectionTarget(ResidentCursor person)
    {
        var source = person.Agent.Goal.TargetEntityId - 1;
        // 低产水井先保住两日个人备用，避免为装满大瓶长期停留。
        if (person.Agent.Goal.Kind == AgentGoalKind.FetchWater && source >= 0 && source < Current.Tiles.Count
            && GetDailyWaterCapacity(source % Current.Width, source / Current.Width) < .1)
            return WaterUse(person) * 2;
        return WaterReserve(person) + (person.Id % 5 == 0 ? 3 : 0);
    }

    private ResourceStock ProvisionAtHome(ResidentCursor person, SettlementCursor home)
    {
        var inventory = person.Inventory;
        var warehouse = home.Resources;
        if ((!Current.Rules.Hunger || warehouse.Food <= 0) && (!Current.Rules.Thirst || warehouse.Water <= 0)
            && (person.Profession != Profession.Engineer || warehouse.Tools <= 0)
            && (person.Profession != Profession.Physician || warehouse.Medicine <= 0)
            && (person.Profession != Profession.Ranger || warehouse.Ammunition <= 0)) return inventory;
        // 先保障当日进食并为其他居民保留共同口粮，避免先处理的居民囤走全体食物。
        var available = Math.Max(Math.Min(warehouse.Food, FoodUse(person)),
            warehouse.Food - home.Population * .06);
        var food = Current.Rules.Hunger
            ? Math.Min(available, Math.Max(0, TravelReserve(person) - person.Inventory.Food))
            : 0;
        warehouse = warehouse with { Food = warehouse.Food - food };
        inventory = inventory with { Food = inventory.Food + food };
        available = Math.Max(Math.Min(warehouse.Water, WaterUse(person)),
            warehouse.Water - home.Population * .03);
        var water = Current.Rules.Thirst
            ? Math.Min(available, Math.Max(0, WaterReserve(person) - person.Inventory.Water))
            : 0;
        warehouse = warehouse with { Water = warehouse.Water - water };
        inventory = inventory with { Water = inventory.Water + water };

        void TakeJobSupply(ResourceKind kind, double target)
        {
            var take = Math.Min(warehouse.Get(kind), Math.Max(0, target - inventory.Get(kind)));
            warehouse = warehouse.WithAmount(kind, warehouse.Get(kind) - take);
            inventory = inventory.WithAmount(kind, inventory.Get(kind) + take);
        }

        if (person.Profession == Profession.Engineer)
            TakeJobSupply(ResourceKind.Tools, .5);
        if (person.Profession == Profession.Physician)
            TakeJobSupply(ResourceKind.Medicine, 2);
        if (person.Profession == Profession.Ranger)
            TakeJobSupply(ResourceKind.Ammunition, 8);
        home.Resources = warehouse;
        return inventory;
    }

    private void RefillDailyWater(ResidentCursor person)
    {
        var use = LocalWaterUse(person);
        if (Current.Rules.Thirst && person.Inventory.Water < use &&
            Current.Tick - person.MoveStartedTick >= person.MoveDurationTicks)
            DrawWater(person, Index(person.X, person.Y), use - person.Inventory.Water);
    }

    private void DrinkCarriedWater(ResidentCursor person)
    {
        if (!Current.Rules.Thirst)
        {
            person.Thirst = 0;
            return;
        }

        RefillDailyWater(person);
        var use = LocalWaterUse(person);
        var drink = Math.Min(use, person.Inventory.Water);
        person.Inventory = person.Inventory with { Water = person.Inventory.Water - drink };
        person.Thirst = Math.Clamp(person.Thirst + (drink >= use - .000001 ? -3 : .6 * (use - drink) / WaterUse(person)), 0, 100);
        if (person.Thirst > 95)
            DamageResident(person, .25, DeathCause.Dehydration);
    }

    /// <summary>计算此格当日扣除已取水量后的可打水量；淡水水域可为正无穷，普通陆地为零。</summary>
    /// <param name="x">横向地格坐标。</param>
    /// <param name="y">纵向地格坐标。</param>
    public double AvailableWater(int x, int y)
    {
        if (!InBounds(x, y))
            return 0;
        var index = Index(x, y);
        var tile = Current.Tiles[index].Value;
        if (tile.FireTicks > 0)
            return 0;
        var supply = WaterSupplyAt(index, tile).Supply;
        return Math.Max(0, supply - (tile.WaterDrawTick == Current.Tick ? tile.WaterDrawn : 0));
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
        var tile = Current.Tiles[index].Value;
        if (tile.FireTicks > 0)
            return 0;
        return WaterSupplyAt(index, tile).Supply;
    }

    // 已知地格只查询一次供水和水井状态；调用方处理边界和火场。
    private (double Supply, bool Reliable) WaterSupplyAt(int index, Tile tile)
    {
        if (IsFreshWater(tile)) return (double.PositiveInfinity, true);
        if (IsWaterTerrain(tile.Terrain)) return (0, false);
        var well = _localWorkQueriesActive
            ? _localWaterWells.GetValueOrDefault(index)
            : FindWaterWell(index);
        var operating = well is not null && IsBuildingOperational(well);
        return (operating ? WellWaterYield(tile) : 0, operating);
    }

    private BuildingCursor? FindWaterWell(int index)
    {
        var x = index % Current.Width;
        var y = index / Current.Width;
        foreach (var building in Current.Society.Buildings)
            if (building.Kind == BuildingKind.Well && building.X == x && building.Y == y) return building;
        return null;
    }

    private double DrawWater(ResidentCursor person, int source, double wanted)
    {
        var amount = WithdrawWater(person.X, person.Y, person.MoveStartedTick, person.MoveDurationTicks,
            person.Inventory.Water, source, wanted);
        if (amount > 0) person.Inventory = person.Inventory with { Water = person.Inventory.Water + amount };
        return amount;
    }

    // 日常补水可直接作为需求转换的输入；装瓶取水才另行更新背包。
    private double WithdrawWater(Resident person, int source, double wanted)
        => WithdrawWater(person.X, person.Y, person.MoveStartedTick, person.MoveDurationTicks, person.Inventory.Water, source, wanted);

    private double WithdrawWater(int x, int y, long moveStarted, int moveDuration, double carriedWater, int source, double wanted)
    {
        if ((uint)source >= (uint)Current.Tiles.Count
            || Current.Tick - moveStarted < moveDuration)
            return 0;
        var tile = Current.Tiles[source];
        var before = tile.Value;
        var fresh = IsFreshWater(before);
        if (Distance(x, y, source % Current.Width, source / Current.Width) > (fresh ? 1 : 0)
            || before.FireTicks > 0) return 0;
        var supply = WaterSupplyAt(source, before).Supply;
        var drawn = _dailyDrinking && _dailyWaterVisits[source] == _dailyWaterVisit
            ? _dailyWaterDraws[source]
            : before.WaterDrawTick == Current.Tick ? before.WaterDrawn : 0;
        var available = Math.Max(0, supply - drawn);
        var amount = Math.Min(Math.Max(0, wanted), available);
        amount = Math.Min(amount, 1_000_000 - carriedWater);
        if (amount <= 0) return 0;
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
                WaterDrawTick = fresh ? before.WaterDrawTick : Current.Tick,
                WaterDrawn = fresh ? before.WaterDrawn : drawn + amount,
                LastHarvestTick = amount > .05 ? Current.Tick : before.LastHarvestTick,
                Harvested = amount > .05 ? Math.Min(1_000_000_000, before.Harvested + amount) : before.Harvested,
            });
        }
        return amount;
    }

    private (int Bank, int Source) FindWaterSite(ResidentCursor person, bool collectionOnly)
    {
        var reachable = 0;
        // 可见范围之外的水源须有实际获知的地址，避免居民凭全图状态找水。
        var bestBank = -1;
        var bestSource = -1;
        var bestScore = double.NegativeInfinity;
        var sufficient = false;
        var required = WaterUse(person);
        var urgent = person.Thirst >= 80 && person.Inventory.Water < required;
        // 一次查询只读一次个人水源记忆，不为每个候选岸边重复扫描整份记忆。
        Span<byte> familiarSources = stackalloc byte[13 * 13];
        familiarSources.Clear();
        foreach (var fact in person.Agent.Value.Memory)
        {
            if (fact.Kind != AgentFactKind.WaterSource || fact.OriginResidentId != person.Id
                || fact.ReliabilityAt(Current.Tick) < .5) continue;
            var source = fact.SubjectId - 1;
            var localX = source % Current.Width - person.X + 6;
            var localY = source / Current.Width - person.Y + 6;
            if ((uint)source < Current.Tiles.Count && (uint)localX < 13 && (uint)localY < 13)
                familiarSources[localY * 13 + localX] = 1;
        }

        void ConsiderBank(int bank, int source, double available, bool reliable, bool familiar)
        {
            var fullDay = available >= required;
            if (sufficient && !fullDay) return;
            var distance = Distance(person.X, person.Y, bank % Current.Width, bank / Current.Width);
            var bankUpperScore = urgent ? -distance + Math.Min(1, available / required) * .01
                : Math.Min(1, available) * (reliable ? 2 : 1) * 1.1 / (1 + distance * .4);
            if (fullDay == sufficient && bankUpperScore <= bestScore) return;
            // 严重缺水优先缩短到岸时间；日常取水重视足够装瓶的供水及亲眼确认过的可靠来源。
            var score = urgent ? -distance + Math.Min(1, available / required) * .01
                : Math.Min(1, available) * (reliable ? 2 : 1) * (familiar ? 1.1 : 1) / (1 + distance * .4);
            if (fullDay == sufficient && score <= bestScore
                || !VisibleSiteReachable(person, bank, ref reachable)) return;
            bestScore = score;
            bestBank = bank;
            bestSource = source;
            sufficient = fullDay;
        }

        void Consider(int source, ReadOnlySpan<byte> familiarSites)
        {
            if (source < 0 || source >= Current.Tiles.Count)
                return;
            var tile = Current.Tiles[source].Value;
            if (tile.FireTicks > 0) return;
            var water = WaterSupplyAt(source, tile);
            // 产量须能保障个人饮水并补出储备，避免在低产水井长期等水。
            if (water.Supply <= WaterUse(person)) return;
            if (collectionOnly && water.Supply < .1) return;
            var available = Math.Max(0, water.Supply - (tile.WaterDrawTick == Current.Tick ? tile.WaterDrawn : 0));
            if (available <= 0)
                return;
            var local = (source / Current.Width - person.Y + 6) * 13 + source % Current.Width - person.X + 6;
            var familiar = familiarSites[local] != 0;
            if (RaceTerrainRules.CanWalk(tile, person.Race))
            {
                ConsiderBank(source, source, available, water.Reliable, familiar);
                return;
            }

            var x = source % Current.Width;
            var y = source / Current.Width;
            foreach (var (dx, dy) in Directions)
            {
                var xx = x + dx;
                var yy = y + dy;
                if (!Walkable(xx, yy, person.Race) || Current.Tiles[Index(xx, yy)].FireTicks > 0)
                    continue;
                ConsiderBank(Index(xx, yy), source, available, water.Reliable, familiar);
            }
        }

        foreach (var offset in VisibleResourceOffsets)
        {
            if (offset.Distance > 6) break;
            // 后续水源的岸边至多近一格；最高供水和熟悉加成也不能超过此评分上界。
            var nearestBank = Math.Max(0, offset.Distance - 1);
            var upperScore = urgent ? -nearestBank + .01 : 2.2 / (1 + nearestBank * .4);
            if (sufficient && upperScore <= bestScore) break;
            var x = person.X + offset.X;
            var y = person.Y + offset.Y;
            if (InBounds(x, y))
                Consider(Index(x, y), familiarSources);
        }

        // 远方已知水源可引导探索，但当日补水须使用当前可见且可达的岸边。
        if (bestSource >= 0 && Distance(person.X, person.Y, bestSource % Current.Width, bestSource / Current.Width) <= 6)
        {
            AgentFact? existing = null;
            foreach (var fact in person.Agent.Value.Memory)
                if (fact.Kind == AgentFactKind.WaterSource && fact.SubjectId == bestSource + 1)
                {
                    existing = fact;
                    break;
                }
            if (existing is null || Current.Tick - existing.ObservedTick >= 120)
            {
                RememberAgentFact(person, MakeAgentFact(person, AgentFactKind.WaterSource, bestSource + 1,
                    bestSource % Current.Width, bestSource / Current.Width, 1, "实地发现可取水的河湖或运营水井"));
            }
        }

        return (bestBank, bestSource);
    }

    private bool AddProvisionChoices(ResidentCursor person, SettlementCursor home, List<GoalChoice> choices)
    {
        // 已严重缺粮而每日仍喝得到水时，先找食物；不能为尚不需要的装瓶储备反复搜索。
        var prioritizeFood = Current.Rules.Hunger && person.Hunger >= 60
            && person.Inventory.Food < .05 && person.Thirst < 10;
        var needsWater = person.Inventory.Water < .3
                         && (person.Thirst >= 10 || person.Inventory.Water < LocalWaterUse(person) * 4
                             && GetDailyWaterCapacity(person.X, person.Y) < LocalWaterUse(person));
        var refillReserve = person.Inventory.Water < WaterUse(person) * 4;
        if (Current.Rules.Thirst && !prioritizeFood &&
            (needsWater || refillReserve || (person.Id % 5 == 0 && person.Inventory.Water < WaterReserve(person) + 3)))
        {
            var atHome = Distance(person.X, person.Y, home.X, home.Y) <= 1;
            if (atHome && home.Resources.Water >= .3)
            {
                choices.Add(new GoalChoice(AgentGoalKind.Eat, home.X, home.Y, 75 + person.Thirst, "在家园领取随身饮水与口粮",
                    SettlementId: home.Id));
            }
            else
            {
                var water = FindWaterSite(person, !needsWater);
                if (water.Bank >= 0 && (needsWater ||
                                        GetDailyWaterCapacity(water.Source % Current.Width, water.Source / Current.Width) >= .1))
                {
                    choices.Add(new GoalChoice(AgentGoalKind.FetchWater, water.Bank % Current.Width,
                        water.Bank / Current.Width,
                        needsWater || refillReserve ? 65 + person.Thirst :
                        atHome && home.Resources.Water < home.Population * .5 ? 72 : 18,
                        person.Thirst >= 80 ? "严重缺水，优先就近补充饮水"
                            : "比较眼前可达水源的可打水量与距离，到河湖或运营水井装水，再随身携带并运回家园",
                        EntityId: water.Source + 1));
                }
                else if (needsWater && water.Bank < 0)
                {
                    if (person.Thirst < 40 && Distance(person.X, person.Y, home.X, home.Y) >= 12
                                           && !(person.Agent.Goal.Kind == AgentGoalKind.ReturnHome &&
                                                Current.Tick - person.Agent.Goal.StartedTick > 48))
                    {
                        choices.Add(new GoalChoice(AgentGoalKind.ReturnHome, home.X, home.Y, 180 + person.Thirst,
                            "寻水已到安全补给边界，返回已知家园后重新安排路线", SettlementId: home.Id));
                        return true;
                    }

                    var heading = Directions[person.Agent.ExplorationHeading % 4];
                    var best = -1;
                    var bestDistance = int.MaxValue;
                    var reachable = 0;
                    foreach (var offset in VisibleResourceOffsets)
                    {
                        var x = person.X + offset.X;
                        var y = person.Y + offset.Y;
                        if (!Walkable(x, y, person.Race) || Current.Tiles[Index(x, y)].FireTicks > 0)
                            continue;
                        var distance = Distance(x, y, person.X + heading.X * 6, person.Y + heading.Y * 6);
                        if (distance < bestDistance && VisibleSiteReachable(person, Index(x, y), ref reachable))
                        {
                            best = Index(x, y);
                            bestDistance = distance;
                        }
                    }

                    if (best >= 0)
                    {
                        choices.Add(new GoalChoice(AgentGoalKind.FetchWater, best % Current.Width, best / Current.Width,
                            50 + person.Thirst, "沿眼前可通行土地勘察水源；尚未发现河湖或运营水井"));
                    }
                }
            }

            // 当前地点连下一日饮水都不能保障时，先执行补水；不为非紧急工作重复评估全套候选。
            if ((needsWater || refillReserve) && (person.Hunger < 60 || person.Thirst >= 80)
                && choices.Any(choice => choice.Kind is AgentGoalKind.FetchWater or AgentGoalKind.Eat))
                return true;
        }

        if (Current.Rules.Thirst && person.Inventory.Water >= WaterReserve(person) + 2)
        {
            choices.Add(new GoalChoice(AgentGoalKind.ReturnHome, home.X, home.Y, 110, "携带打来的饮水返仓，为家园补给",
                SettlementId: home.Id));
        }

        var homeDistance = Distance(person.X, person.Y, home.X, home.Y);
        var daysHome = homeDistance * 4 + 12;
        if (Current.Rules.Hunger && homeDistance > 1 && person.Hunger < 20
            && person.Inventory.Food < FoodUse(person) * daysHome)
        {
            choices.Add(new GoalChoice(AgentGoalKind.ReturnHome, home.X, home.Y, 85, "口粮接近保守返程需求，先实地返仓补给",
                SettlementId: home.Id));
        }

        if (person.Age < 14)
            return false;
        var foodNeeded = FoodSupplyNeeded(person, home);
        if (foodNeeded)
            AddBoatFishingChoice(person, home, choices);
        var wildlife =
            foodNeeded && (person.Profession is Profession.Farmer or Profession.Fisher ||
                           (person.Hunger > 20 && person.Inventory.Food < .3))
                ? FindHarvestableWildlife(person)
                : (-1, -1, false);
        if (wildlife.Item1 >= 0)
        {
            choices.Add(new GoalChoice(wildlife.Item3 ? AgentGoalKind.Fish : AgentGoalKind.Hunt,
                wildlife.Item1 % Current.Width, wildlife.Item1 / Current.Width,
                (person.Inventory.Food < .3 && person.Hunger > 20 ? 100 :
                    person.Profession is Profession.Farmer or Profession.Fisher ? 43 : 14)
                + (person.Inventory.Food < .3 ? person.Hunger : 0),
                wildlife.Item3 ? "在眼前水岸捕鱼，鱼群数量会实际减少" : "在眼前栖息地狩猎，将食物携带返乡",
                EntityId: wildlife.Item2 + 1));
        }

        var claim = VisibleClaimSite(person, home);
        if (claim >= 0)
        {
            choices.Add(new GoalChoice(AgentGoalKind.ClaimLand, claim % Current.Width, claim / Current.Width,
                (IsSettlementActive(home.Id) ? SettlementNeedsClaimArea(home) ? 58 : home.IsExpanding ? 52 : 25 : 70) +
                person.Agent.Personality.Ambition * 8,
                !IsSettlementActive(home.Id)
                    ? $"城镇占地 {GetSettlementArea(home.Id)}/{SettlementActivationArea} 格，加成尚未生效，先实地登记领地"
                    : SettlementNeedsClaimArea(home)
                        ? $"升级面积 {GetSettlementArea(home.Id)}/{GetSettlementExpansionArea(home.Id)} 格，先实地登记相邻领地"
                        : "实际前往可达的相邻边界地块，登记城镇地盘", SettlementId: home.Id));
        }
        return false;
    }

    /// <summary>尝试从当前任务的实际水源取水放入随身库存；返回是否取到水。</summary>
    /// <param name="person">居民。</param>
    public bool TryFetchWater(Resident person)

    {
        return TryFetchWater(RequireResident(person.Id));
    }
    private bool TryFetchWater(ResidentCursor person)
    {
        if (person.Agent.Goal.Kind != AgentGoalKind.FetchWater || person.Health <= 0)
            return false;
        var source = person.Agent.Goal.TargetEntityId - 1;
        if (source >= 0 && (source >= Current.Tiles.Count
            || GetDailyWaterCapacity(source % Current.Width, source / Current.Width)
                <= WaterUse(person)))
        {
            person.Agent.Goal = new AgentGoal
            {
                TargetX = person.X, TargetY = person.Y, ReviewTick = Current.Tick,
                Reason = "此处无法持续补充饮水，重新寻找河湖或运营水井",
            };
            person.Agent.NextThinkTick = Current.Tick;
            return false;
        }
        var target = WaterCollectionTarget(person);
        var amount = DrawWater(person, source,
            Math.Min(
                source >= 0 && source < Current.Tiles.Count
                    ? GatheringTerritoryMultiplier(person, Current.Tiles[source])
                    : 0, target - person.Inventory.Water));
        if (source >= 0 && person.Inventory.Water >= target - .000001)
        {
            // 当场完成后结束取水目标，避免下一日喝掉少量水又被当成尚未完成。
            var home = _settlements.GetValueOrDefault(person.SettlementId);
            var deliver = person.Id % 5 == 0 && home is not null
                && person.Inventory.Water >= WaterReserve(person) + 2;
            person.Agent.Replace(person.Agent.Value with
            {
                Goal = new AgentGoal
                {
                    Kind = deliver ? AgentGoalKind.ReturnHome : AgentGoalKind.Idle,
                    TargetX = deliver ? home!.X : person.X,
                    TargetY = deliver ? home!.Y : person.Y,
                    TargetSettlementId = deliver ? home!.Id : 0,
                    StartedTick = Current.Tick,
                    ReviewTick = Current.Tick,
                    Reason = deliver ? "装好公共补给后实地运回家园" : "已补足随身饮水，重新安排其他事务",
                },
                NextThinkTick = Current.Tick,
            });
            person.Activity = ResidentActivity.Working;
            return amount > 0;
        }
        if (source < 0 || amount <= 0)
        {
            // 供水仍会每日恢复时，额度已用完只是当日受阻；保留水源并等待，避免每天转向和重新搜索。
            if (source >= 0 && person.Inventory.Water < target
                && GetDailyWaterCapacity(source % Current.Width, source / Current.Width) > 0)
            {
                person.Activity = ResidentActivity.Resting;
                return false;
            }
            // 完成探索段后继续向外，边界受阻才转向，避免每次到达都转向而绕同一小圈。
            if (source >= 0 || person.Agent.Goal.WorkTicks > 1)
                person.Agent.ExplorationHeading = (person.Agent.ExplorationHeading + 1) % 8;
            person.Agent.NextThinkTick = Current.Tick;
        }

        person.Activity = ResidentActivity.Working;
        return amount > 0;
    }

    private (int Site, int Source, bool Fishing) FindHarvestableWildlife(ResidentCursor person)
    {
        var reachable = 0;
        // 先寻找距离最近且种群仍可持续的猎场，不穷举比较整个视野的最大产量。
        foreach (var index in VisibleWildlifeSites(person))
        {
            var x = index % Current.Width;
            var y = index / Current.Width;
            var tile = Current.Tiles[index];
            if (tile.FireTicks > 0)
                continue;
            var aquatic = IsWaterTerrain(tile.Terrain);
            var kind = EdibleAnimal(tile, aquatic);
            var efficiency = WildlifeHarvestEfficiency(tile, kind);
            if (kind == WildlifeKind.None || efficiency < .25)
                continue;
            if (Current.Rules.Hunger && person.Hunger > 20 && person.Inventory.Food < .3)
            {
                var interval = WorkInterval(person);
                var food = WildlifeHarvestAmount(tile, kind, interval * .15 * Current.Rules.GatheringRate
                    * GatheringCondition(person) * GatheringTerritoryMultiplier(person, tile))
                    * AnimalRules.For(kind).BodyMass / interval;
                if (food < FoodUse(person)) continue;
            }
            if (!aquatic && RaceTerrainRules.CanWalk(tile, person.Race)
                && VisibleSiteReachable(person, index, ref reachable))
            {
                return (index, index, false);
            }

            if (!aquatic)
                continue;
            foreach (var (dx, dy) in Directions)
                if (Walkable(x + dx, y + dy, person.Race) && Current.Tiles[Index(x + dx, y + dy)].FireTicks == 0
                                                          && VisibleSiteReachable(person, Index(x + dx, y + dy),
                                                              ref reachable))
                {
                    return (Index(x + dx, y + dy), index, true);
                }
        }

        return (-1, -1, false);
    }

    private void AddBoatFishingChoice(ResidentCursor person, SettlementCursor home, List<GoalChoice> choices)
    {
        if (person.Profession != Profession.Fisher || person.Age < 14
                                                   || (person.TravelMode != TravelMode.Boat &&
                                                       !(Distance(person.X, person.Y, home.X, home.Y) <= 1
                                                         && HasResearch(home.Id, Advancement.Logistics) &&
                                                         home.Resources.Boats >= 1)))
            return;
        var reachable = 0;
        var best = -1;
        var score = double.NegativeInfinity;
        foreach (var offset in VisibleResourceOffsets)
        {
            if (offset.Distance > 6)
                break;
            var x = person.X + offset.X;
            var y = person.Y + offset.Y;
            if (!InBounds(x, y))
                continue;
            var index = Index(x, y);
            var tile = Current.Tiles[index];
            var animal = EdibleAnimal(tile, true);
            var efficiency = WildlifeHarvestEfficiency(tile, animal);
            if (animal == WildlifeKind.None || efficiency < .25 || tile.FireTicks > 0)
                continue;
            var value = Math.Min(tile.AnimalPopulation(animal) * .1, .15 * efficiency) *
                AnimalRules.For(animal).BodyMass / (1 + offset.Distance * .2);
            if (value > score && VisibleSiteReachable(person, index, ref reachable, TravelMode.Boat))
            {
                best = index;
                score = value;
            }
        }

        if (best >= 0)
        {
            choices.Add(new GoalChoice(AgentGoalKind.Fish, best % Current.Width, best / Current.Width, 60,
                "领取家园舟船，前往眼前鱼群捕鱼，装满后返岸交付鱼获与舟船", EntityId: best + 1));
        }
        else if (person.TravelMode == TravelMode.Boat)
        {
            choices.Add(new GoalChoice(AgentGoalKind.ReturnHome, home.X, home.Y, 90,
                "附近鱼群已不足，返岸交付鱼获与舟船", SettlementId: home.Id));
        }
    }

    private static WildlifeKind EdibleAnimal(TileCursor tile, bool aquatic = false)
    {
        if (aquatic != IsWaterTerrain(tile.Terrain))
            return WildlifeKind.None;
        return tile.EdibleAnimal(aquatic);
    }

    /// <summary>尝试在当前位置狩猎或捕鱼，将实际减少的动物转为随身粮食；返回是否取得产物。</summary>
    /// <param name="person">居民。</param>
    public bool TryHarvestWildlife(Resident person)

    {
        return TryHarvestWildlife(RequireResident(person.Id));
    }
    private bool TryHarvestWildlife(ResidentCursor person)
    {
        var goal = person.Agent.Goal;
        var source = goal.TargetEntityId - 1;
        if (person.Health <= 0 || person.Age < 14 || source < 0 || source >= Current.Tiles.Count
            || goal.Kind is not (AgentGoalKind.Hunt or AgentGoalKind.Fish)
            || Current.Tick - person.MoveStartedTick < person.MoveDurationTicks
            || Distance(person.X, person.Y, source % Current.Width, source / Current.Width) >
            (goal.Kind == AgentGoalKind.Fish ? 1 : 0))
            return false;
        var tile = Current.Tiles[source];
        var kind = EdibleAnimal(tile, goal.Kind == AgentGoalKind.Fish);
        if (kind == WildlifeKind.None || tile.FireTicks > 0)
        {
            person.Agent.NextThinkTick = Current.Tick;
            return false;
        }

        var yield = AnimalRules.For(kind).BodyMass;
        var amount = WildlifeHarvestAmount(tile, kind,
            WorkInterval(person) * .15 * Current.Rules.GatheringRate * GatheringCondition(person) * GatheringTerritoryMultiplier(person, tile));
        amount = Math.Min(amount, (1_000_000 - person.Inventory.Food) / yield);
        tile.SetAnimalPopulation(kind, tile.AnimalPopulation(kind) - amount);
        person.Inventory = person.Inventory with { Food = person.Inventory.Food + amount * yield };
        RecordHarvest(tile, amount * yield);
        person.Agent.Fatigue = Math.Min(100, person.Agent.Fatigue + .3 * WorkInterval(person));
        person.Activity = ResidentActivity.Working;
        if (!goal.PlayerDirected && !WildlifeSiteProductive(tile, goal.Kind == AgentGoalKind.Fish))
            person.Agent.NextThinkTick = Current.Tick + 1;
        return amount > 0;
    }
}
