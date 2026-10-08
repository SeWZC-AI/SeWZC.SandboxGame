using SeWZC.WorldBox.Core.Runtime;
namespace SeWZC.WorldBox.Core;

public sealed partial class WorldEngine
{
    /// <summary>计算居民每日所需粮食资源量。</summary>
    /// <param name="person">居民。</param>
    public static double FoodUse(Resident person)
    {
        return person.Age < 14 ? .02 : person.Race == RaceKind.Orc ? .052 : .04;
    }

    /// <summary>计算居民每日所需饮水资源量。</summary>
    /// <param name="person">居民。</param>
    public static double WaterUse(Resident person)
    {
        return person.Age < 14 ? .015 : .025;
    }

    private double WaterReserve(ResidentCursor person)
    {
        return Math.Clamp(.75 + Distance(person.X, person.Y,
            person.Agent.Goal.TargetX, person.Agent.Goal.TargetY) * WaterUse(person) * 6, .75, 6);
    }

    private void ProvisionAtHome(ResidentCursor person, SettlementCursor home)
    {
        var inventory = person.Inventory;
        var warehouse = home.Resources;
        if ((!Current.Rules.Hunger || warehouse.Food <= 0) && (!Current.Rules.Thirst || warehouse.Water <= 0)
            && (person.Profession != Profession.Engineer || warehouse.Tools <= 0)
            && (person.Profession != Profession.Physician || warehouse.Medicine <= 0)
            && (person.Profession != Profession.Ranger || warehouse.Ammunition <= 0)) return;
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
        person.Inventory = inventory;
    }

    private void RefillDailyWater(ResidentCursor person)
    {
        var use = WaterUse(person);
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
        var use = WaterUse(person);
        var drink = Math.Min(use, person.Inventory.Water);
        person.Replace(person.Value with { Inventory = person.Inventory with { Water = person.Inventory.Water - drink }, Thirst = Math.Clamp(person.Thirst + (drink >= use - .000001 ? -3 : .6 * (1 - drink / use)), 0, 100) });
        if (person.Thirst > 95)
            DamageResident(person, .25, DeathCause.Dehydration);
    }

    /// <summary>计算此格当日扣除已取水量后的可用供水；淡水水域可为正无穷。</summary>
    /// <param name="x">横向地格坐标。</param>
    /// <param name="y">纵向地格坐标。</param>
    public double AvailableWater(int x, int y)
    {
        if (!InBounds(x, y))
            return 0;
        var tile = Current.Tiles[Index(x, y)];
        if (tile.FireTicks > 0)
            return 0;
        var supply = GetWaterSupply(x, y);
        return Math.Max(0, supply - (tile.WaterDrawTick == Current.Tick ? tile.WaterDrawn : 0));
    }

    // 无限供水只由查询推导，避免把无穷值写入存档资源。
    /// <summary>计算受干旱影响后的每日自然供水量；河湖等淡水水域返回正无穷。</summary>
    /// <param name="tile">要计算自然供水量的地格。</param>
    public static double DailyWaterYield(Tile tile)
    {
        return IsFreshWater(tile)
            ? double.PositiveInfinity
            : tile.NaturalWaterYield * (tile.DroughtTicks > 0 ? .2 : 1);
    }

    /// <summary>计算此格水井的每日增量供水，水域返回零。</summary>
    /// <param name="tile">水井所在的地格。</param>
    public static double WellWaterYield(Tile tile)
    {
        return IsWaterTerrain(tile.Terrain)
            ? 0
            : Math.Max(0, 30 * DailyWaterYield(tile) - .6);
    }

    /// <summary>计算此格自然水源及本地运营供水设施提供的每日总量。</summary>
    /// <param name="x">横向地格坐标。</param>
    /// <param name="y">纵向地格坐标。</param>
    public double GetWaterSupply(int x, int y)
    {
        if (!InBounds(x, y))
            return 0;
        var tile = Current.Tiles[Index(x, y)];
        if (tile.FireTicks > 0)
            return 0;
        var natural = DailyWaterYield(tile);
        if (IsWaterTerrain(tile.Terrain))
            return natural;
        var well = _localWorkQueriesActive
            ? _localWaterWells.GetValueOrDefault(Index(x, y))
            : Current.Society.Buildings.FirstOrDefault(b => b.Kind == BuildingKind.Well && b.X == x && b.Y == y);
        return natural + (well is not null && IsBuildingOperational(well) ? WellWaterYield(tile) : 0);
    }

    private double DrawWater(ResidentCursor person, int source, double wanted)
    {
        var amount = WithdrawWater(person.Value, source, wanted);
        if (amount > 0) person.Inventory = person.Inventory with { Water = person.Inventory.Water + amount };
        return amount;
    }

    // 日常补水可直接作为需求转换的输入；装瓶取水才另行更新背包。
    private double WithdrawWater(Resident person, int source, double wanted)
    {
        if (source < 0 || source >= Current.Tiles.Count || Current.Tick - person.MoveStartedTick < person.MoveDurationTicks
            || Distance(person.X, person.Y, source % Current.Width, source / Current.Width) >
            (IsFreshWater(Current.Tiles[source]) ? 1 : 0))
            return 0;
        var tile = Current.Tiles[source];
        var amount = Math.Min(Math.Max(0, wanted), AvailableWater(source % Current.Width, source / Current.Width));
        amount = Math.Min(amount, 1_000_000 - person.Inventory.Water);
        if (amount <= 0) return 0;
        if (!IsFreshWater(tile))
        {
            tile.Replace(tile.Value with
            {
                WaterDrawTick = Current.Tick,
                WaterDrawn = (tile.WaterDrawTick == Current.Tick ? tile.WaterDrawn : 0) + amount,
            });
        }

        if (amount > .05)
            RecordHarvest(tile, amount);
        return amount;
    }

    private (int Bank, int Source) FindWaterSite(ResidentCursor person)
    {
        var reachable = 0;
        // 可见范围之外的水源须有实际获知的地址，避免居民凭全图状态找水。
        var bestBank = -1;
        var bestSource = -1;
        var bestScore = double.NegativeInfinity;
        var sufficient = false;
        var required = WaterUse(person);
        var urgent = person.Thirst >= 80 && person.Inventory.Water < required;

        void ConsiderBank(int bank, int source, double available, bool reliable)
        {
            var fullDay = available >= required;
            if (sufficient && !fullDay) return;
            var distance = Distance(person.X, person.Y, bank % Current.Width, bank / Current.Width);
            var familiar = person.Agent.Memory.Any(fact => fact.Kind == AgentFactKind.WaterSource
                && fact.SubjectId == source + 1 && fact.OriginResidentId == person.Id
                && fact.ReliabilityAt(Current.Tick) >= .5);
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

        void Consider(int source)
        {
            if (source < 0 || source >= Current.Tiles.Count || !IsWaterSource(Current.Tiles[source]) ||
                Current.Tiles[source].FireTicks > 0)
                return;
            var x = source % Current.Width;
            var y = source / Current.Width;
            var available = AvailableWater(x, y);
            if (available <= 0)
                return;
            var well = _localWorkQueriesActive ? _localWaterWells.GetValueOrDefault(source)
                : Current.Society.Buildings.FirstOrDefault(building => building.Kind == BuildingKind.Well
                    && building.X == x && building.Y == y);
            var reliable = IsFreshWater(Current.Tiles[source]) || well is not null && IsBuildingOperational(well);
            if (RaceTerrainRules.CanWalk(Current.Tiles[source], person.Race))
            {
                ConsiderBank(source, source, available, reliable);
                return;
            }

            foreach (var (dx, dy) in Directions)
            {
                var xx = x + dx;
                var yy = y + dy;
                if (!Walkable(xx, yy, person.Race) || Current.Tiles[Index(xx, yy)].FireTicks > 0)
                    continue;
                ConsiderBank(Index(xx, yy), source, available, reliable);
            }
        }

        foreach (var offset in VisibleResourceOffsets)
        {
            if (offset.Distance > 6) break;
            var x = person.X + offset.X;
            var y = person.Y + offset.Y;
            if (InBounds(x, y))
                Consider(Index(x, y));
        }

        // 远方已知水源可引导探索，但当日补水须使用当前可见且可达的岸边。
        if (bestSource >= 0 && Distance(person.X, person.Y, bestSource % Current.Width, bestSource / Current.Width) <= 6)
        {
            var existing = person.Agent.Memory.FirstOrDefault(f =>
                f.Kind == AgentFactKind.WaterSource && f.SubjectId == bestSource + 1);
            if (existing is null || Current.Tick - existing.ObservedTick >= 120)
            {
                RememberAgentFact(person, MakeAgentFact(person, AgentFactKind.WaterSource, bestSource + 1,
                    bestSource % Current.Width, bestSource / Current.Width, 1, "实地发现可取水的河湖、水井或有供水的陆地"));
            }
        }

        return (bestBank, bestSource);
    }

    private bool AddProvisionChoices(ResidentCursor person, SettlementCursor home, List<GoalChoice> choices)
    {
        var needsWater = person.Inventory.Water < .3
                         && (person.Thirst >= 10 || person.Inventory.Water < WaterUse(person) * 4
                             || AvailableWater(person.X, person.Y) < WaterUse(person));
        if (Current.Rules.Thirst &&
            (needsWater || (person.Id % 5 == 0 && person.Inventory.Water < WaterReserve(person) + 3)))
        {
            var atHome = Distance(person.X, person.Y, home.X, home.Y) <= 1;
            if (atHome && home.Resources.Water >= .3)
            {
                choices.Add(new GoalChoice(AgentGoalKind.Eat, home.X, home.Y, 75 + person.Thirst, "在家园领取随身饮水与口粮",
                    SettlementId: home.Id));
            }
            else
            {
                var water = FindWaterSite(person);
                if (water.Bank >= 0 && (needsWater ||
                                        GetWaterSupply(water.Source % Current.Width, water.Source / Current.Width) >= .1))
                {
                    choices.Add(new GoalChoice(AgentGoalKind.FetchWater, water.Bank % Current.Width,
                        water.Bank / Current.Width,
                        needsWater ? 65 + person.Thirst :
                        atHome && home.Resources.Water < home.Population * .5 ? 72 : 18,
                        person.Thirst >= 80 ? "严重缺水，优先就近补充饮水"
                            : "比较眼前可达水源的供水量与距离，优先到河湖、水井或熟悉水源装水，再随身携带并运回家园",
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
                            50 + person.Thirst, "沿眼前可通行土地勘察水源；尚未发现河湖或有供水的陆地"));
                    }
                }
            }

            // 当前地点连下一日饮水都不能保障时，先执行补水；不为非紧急工作重复评估全套候选。
            if (needsWater && (person.Hunger < 60 || person.Thirst >= 80)
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
        var amount = DrawWater(person, source,
            Math.Min(
                source >= 0 && source < Current.Tiles.Count
                    ? GatheringTerritoryMultiplier(person, Current.Tiles[source])
                    : 0, WaterReserve(person) + 3 - person.Inventory.Water));
        if (source < 0 || amount <= 0)
        {
            // 供水仍会每日恢复时，额度已用完只是当日受阻；保留水源并等待，避免每天转向和重新搜索。
            if (source >= 0 && person.Inventory.Water < WaterReserve(person) + 3
                && GetWaterSupply(source % Current.Width, source / Current.Width) > 0)
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
        foreach (var offset in VisibleResourceOffsets)
        {
            // 岸边可达距离为六步时，相邻鱼源可在第七格；陆地猎物仍须在六格可见范围内。
            if (offset.Distance > 7)
                break;
            var x = person.X + offset.X;
            var y = person.Y + offset.Y;
            if (!InBounds(x, y))
                continue;
            var index = Index(x, y);
            var tile = Current.Tiles[index];
            if (tile.FireTicks > 0)
                continue;
            var aquatic = IsWaterTerrain(tile.Terrain);
            var kind = EdibleAnimal(tile, aquatic);
            var efficiency = WildlifeHarvestEfficiency(tile, kind);
            if (kind == WildlifeKind.None || efficiency < .25)
                continue;
            if (!aquatic && offset.Distance <= 6 && RaceTerrainRules.CanWalk(tile, person.Race)
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
        person.Replace(person.Value with { Agent = person.Agent.Value with { Fatigue = Math.Min(100, person.Agent.Fatigue + .3 * WorkInterval(person)) }, Activity = ResidentActivity.Working });
        if (!goal.PlayerDirected && !WildlifeSiteProductive(tile, goal.Kind == AgentGoalKind.Fish))
            person.Agent.NextThinkTick = Current.Tick + 1;
        return amount > 0;
    }
}
