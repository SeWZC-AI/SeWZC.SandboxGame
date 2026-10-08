using SeWZC.WorldBox.Core.Runtime;
namespace SeWZC.WorldBox.Core;

public sealed partial class WorldEngine
{
    private static readonly (int X, int Y, int Distance)[] VisibleResourceOffsets = CreateVisibleResourceOffsets();

    private readonly List<GoalChoice> _goalChoices = [];

    // 缓冲区按可见半径内的地格数分配；可见区域内绕路超过六步时仍须容纳全部地格。
    private readonly (int Index, int First, int Depth)[] _localMoveQueue = new (int, int, int)[85];
    private int _localMoveSearch;
    private int[] _localMoveVisited = [];
    private double[] _localMoveCosts = [];
    private readonly PriorityQueue<(int Index, int First, double Cost), (double Cost, int Index)> _localRoutes =
        new(LocalRoutePriorityComparer.Instance);
    private VisibleAccessCache? _visibleAccessCache;
    private TravelMode _visibleAccessMode;
    private int _visibleAccessOrigin, _visibleAccessSearch, _visibleAccessWidth;
    private RaceKind _visibleAccessRace;
    private long _visibleAccessRevision;

    private static (int X, int Y, int Distance)[] CreateVisibleResourceOffsets()
    {
        var offsets = new List<(int X, int Y, int Distance)>();
        for (var y = -6; y <= 6; y++)
            for (var x = -6; x <= 6; x++)
                if (x * x + y * y <= 36)
                    offsets.Add((x, y, Math.Abs(x) + Math.Abs(y)));
        return offsets.OrderBy(offset => offset.Distance).ThenBy(offset => offset.Y).ThenBy(offset => offset.X)
            .ToArray();
    }

    private void InitializeAgent(ResidentCursor person)
    {
        if (person.Agent.Initialized)
            return;

        // 按身份推导初始性格，避免初始化消耗世界随机序列。
        double Trait(int salt)
        {
            var value = unchecked((uint)(person.Id * 374761393 + Current.Seed * 668265263 + salt));
            value = (value ^ (value >> 13)) * 1274126177;
            return 0.2 + value % 601 / 1000d;
        }

        var agent = person.Agent.Value with { Initialized = true, NextThinkTick = Current.Tick,
            Personality = new PersonalityProfile
        {
            Courage = person.Trait == "勇敢" ? 0.9 : Trait(17),
            Diligence = person.Trait == "勤劳" ? 0.9 : Trait(41),
            Sociability = person.Trait == "温和" ? 0.9 : Trait(83),
            Ambition = person.Trait == "好奇" ? 0.9 : Trait(131),
        } };
        var culture = person.CultureId;
        if (_settlements.TryGetValue(person.SettlementId, out var home))
        {
            if (person.CultureId == 0)
                culture = home.CultureId;
            agent = agent.Remember(MakeAgentFact(person, AgentFactKind.SettlementLocation,
                home.Id, home.X, home.Y, home.NationId, $"我的家园：{home.Name}"), person.SettlementId);
        }
        person.Replace(person.Value with { Agent = agent, CultureId = culture, FromX = person.X, FromY = person.Y });
    }

    private void UpdateAgentNeedsAndActions()
    {
        try
        {
            BeginLocalWorkQueries();
            foreach (var person in Current.Residents)
            {
                InitializeAgent(person);
                if (person.Health <= 0 || !_settlements.TryGetValue(person.SettlementId, out var home))
                    continue;
                // 士兵补给由军队统一扣除，避免与居民系统重复消费。
                if (person.ArmyId != 0)
                {
                    continue;
                }

                var arrivedHome = Distance(person.X, person.Y, home.X, home.Y) <= 1 &&
                                  Walkable(person.X, person.Y, person.Race)
                                  && Current.Tick - person.MoveStartedTick >= person.MoveDurationTicks;
                if (arrivedHome &&
                    !(person.TravelMode == TravelMode.Boat && person.Agent.Goal.Kind == AgentGoalKind.Fish))
                {
                    TransferPersonalProduction(person, home);
                    ProvisionAtHome(person, home);
                }

            }

            // 补给先按实际位置交付，再用一次纯转换结算身体状态和日常需求。
            UpdateResidents();
            // 普通观察按居民序号错峰；非军队居民所在格起火时立即观察，实际取水和劳动自行核实目标。
            var observationInterval = Math.Max(16, (Current.Residents.Count + 127) / 128);
            var observerIndex = 0;
            foreach (var person in Current.Residents)
            {
                var observe = (Current.Tick + observerIndex++) % observationInterval == 0;
                if (person.Health <= 0 || !_settlements.TryGetValue(person.SettlementId, out var home)) continue;
                if (person.ArmyId != 0)
                {
                    if (observe) ObserveAgentEnvironment(person);
                    continue;
                }
                var arrivedHome = Distance(person.X, person.Y, home.X, home.Y) <= 1
                    && Walkable(person.X, person.Y, person.Race)
                    && Current.Tick - person.MoveStartedTick >= person.MoveDurationTicks;
                if (arrivedHome)
                {
                    if ((Current.Tick + person.Id) % 12 == 0)
                        DeliverLocalDiscoveries(person, home);
                }

                var danger = Current.Tiles[Index(person.X, person.Y)].FireTicks > 0;
                if (observe || danger)
                    ObserveAgentEnvironment(person);
                var directed = person.Agent.Goal.PlayerDirected && Current.Tick < person.Agent.Goal.ReviewTick;
                var emergency = danger || (directed || Current.Tick >= person.Agent.Goal.ReviewTick)
                    && ((person.Hunger > 60 && person.Inventory.Food < .05)
                        || (Current.Rules.Thirst && person.Thirst > 80 && person.Inventory.Water < .025));
                if ((!directed && Current.Tick >= person.Agent.NextThinkTick
                               && (Current.Tick + person.Id) % 4 == 0) || emergency)
                    ChooseAgentGoal(person, home, emergency && directed);
                if (person.Agent.Goal.Kind == AgentGoalKind.Idle) continue;
                // 逻辑位置记录已提交的目的地，劳动和递送必须等待实际到达。
                if (Current.Tick - person.MoveStartedTick < person.MoveDurationTicks ||
                    person.FrozenUntilTick > Current.Tick)
                    continue;
                ActOnAgentGoal(person, home);
            }
        }
        finally
        {
            EndLocalWorkQueries();
        }

        ArchiveDeadResidents();
    }

    // 探索口粮来自家乡仓库，不能把额外携带的补给当作新生产货物。
    private double TravelReserve(ResidentCursor person)
    {
        if (person.Profession is Profession.Trader or Profession.Messenger)
        {
            var knowsForeignTown = false;
            foreach (var fact in person.Agent.Value.Memory)
                if (fact.Kind == AgentFactKind.SettlementLocation && fact.Value != person.NationId
                    && Current.Tick - fact.ObservedTick < 1200)
                {
                    knowsForeignTown = true;
                    break;
                }
            if (!knowsForeignTown) return 6;
        }
        return person.Profession is Profession.Lumberjack or Profession.Miner ? 4 : Math.Clamp(
            .8 + Distance(person.X, person.Y, person.Agent.Goal.TargetX, person.Agent.Goal.TargetY) *
            FoodUse(person) * 6, .8, 8);
    }

    private void TransferPersonalProduction(ResidentCursor person, SettlementCursor home)
    {
        // 任务货物在到达约定目的地前仍由居民携带，避免提前入库。
        if (person.Agent.DestinationSettlementId != 0
            && person.Agent.Goal.Kind is AgentGoalKind.DeliverMessage or AgentGoalKind.Trade
                or AgentGoalKind.Petition)
            return;
        // 家乡邻格也可能是工作点，经过时不能卸掉刚领取的生产原料。
        var assigned = person.Agent.Goal.Kind == AgentGoalKind.Work
            ? FindBuilding(person.Agent.Goal.TargetEntityId)
            : null;
        var recipe = assigned is { IsCompleted: true, Enabled: true } ? ProductionRules.For(assigned.Kind) : null;
        if (assigned is not null && assigned.SettlementId == home.Id
                                 && (recipe is not null || IsHusbandry(assigned.Kind) ||
                                     ExpansionSupply(assigned.Kind) is not null || assigned.Health < 50))
            return;
        var unloaded = InventoryTransfer.Unload(person.Inventory, home.Resources,
            WaterReserve(person), TravelReserve(person), person.Profession);
        var agent = person.Agent.Value;
        if (agent.Goal.Kind == AgentGoalKind.ReturnHome && agent.DestinationSettlementId == 0 &&
            agent.MissionOriginSettlementId != 0)
            agent = agent with { MissionOriginSettlementId = 0 };
        if (unloaded.Inventory != person.Inventory || person.TravelMode != TravelMode.Foot ||
            !ReferenceEquals(agent, person.Agent.Value))
            person.Replace(person.Value with { Inventory = unloaded.Inventory, TravelMode = TravelMode.Foot, Agent = agent });
        home.Resources = unloaded.Warehouse;
    }

    private bool ProductiveGoalContinues(ResidentCursor person, SettlementCursor home)
    {
        var goal = person.Agent.Goal;
        if (Distance(person.X, person.Y, home.X, home.Y) <= 1 && !goal.PlayerDirected
                                                              && ((goal.Kind == AgentGoalKind.Gather &&
                                                                   home.Resources.Food >=
                                                                   ProductionStockTarget(home, ResourceKind.Food) &&
                                                                   person.Inventory.Food >= .3)
                                                                  || (goal.Kind == AgentGoalKind.Work &&
                                                                      goal.TargetEntityId == 0 &&
                                                                      !LocalMaterialsNeeded(person, home))))
            return false;
        if (person.Profession == Profession.Builder && SettlementNeedsClaimArea(home)
                                                    && (FindBuilding(goal.TargetEntityId) is not { } assigned ||
                                                        (assigned.IsCompleted && !assigned.IsUpgrading)))
            return false;
        if (person.Hunger >= 60 || person.Thirst >= 60 || person.Agent.Fatigue >= 60
            || person.Inventory.Food >= Math.Max(4, TravelReserve(person) + 1)
            || (Current.Rules.Thirst && person.Inventory.Water < WaterUse(person) &&
                AvailableWater(person.X, person.Y) < WaterUse(person))
            || person.Inventory.Wood + person.Inventory.Stone + person.Inventory.Ore >= 3)
            return false;
        if (goal.NavigationTarget >= 0 && Current.Tick < goal.NavigationRetryTick)
            return false;
        if (goal.Kind == AgentGoalKind.Work && person.Profession is Profession.Physician or Profession.Firefighter
                                                or Profession.Archivist or Profession.Surveyor or Profession.Gardener
                                            && FindBuilding(goal.TargetEntityId) is { } current &&
                                            PreferredExpansionJob(current.Kind) != person.Profession
                                            && ExpansionJobHasNearbyWork(person))
            return false;
        if (Current.Rules.Hunger && Distance(person.X, person.Y, home.X, home.Y) > 1 && person.Hunger < 20
            && person.Inventory.Food < FoodUse(person) * (Distance(person.X, person.Y, home.X, home.Y) * 4 + 12))
            return false;
        if (goal.Kind == AgentGoalKind.Work && goal.TargetEntityId == 0 && person.Profession == Profession.Miner
            && person.Agent.MaterialPriority is not null && FindVisibleResourceSite(person, Profession.Miner) < 0 &&
            VisibleDepositSite(person) < 0)
            return false;
        if (goal.Kind == AgentGoalKind.Gather || (goal.Kind == AgentGoalKind.Work && goal.TargetEntityId == 0))
        {
            return ResourceSiteYield(Index(goal.TargetX, goal.TargetY),
                       goal.Kind == AgentGoalKind.Gather ? Profession.Farmer : person.Profession) > 0
                   && (person.Profession == Profession.Miner ||
                       NaturalPlantHarvestEfficiency(Current.Tiles[Index(goal.TargetX, goal.TargetY)],
                           goal.Kind == AgentGoalKind.Work && person.Profession == Profession.Lumberjack) >= .25);
        }

        if (goal.Kind is AgentGoalKind.Work or AgentGoalKind.Study or AgentGoalKind.TrainMagic)
        {
            return FindBuilding(goal.TargetEntityId) is { } building && building.SettlementId == home.Id &&
                   BuildingHasWork(building, person);
        }

        return false;
    }

    private bool FoodSupplyNeeded(ResidentCursor person, SettlementCursor home)
    {
        if (person.Inventory.Food < .3 || person.Hunger > 20)
            return true;
        var fact = LatestAgentFact(person.Agent.Memory, AgentFactKind.FoodSupply, home.Id);
        var known = Distance(person.X, person.Y, home.X, home.Y) <= 1 ? home.Resources.Food
            : fact is not null && fact.ReliabilityAt(Current.Tick) >= .5 ? fact.Value : 0;
        return known < ProductionStockTarget(home, ResourceKind.Food);
    }

    private bool LocalMaterialsNeeded(ResidentCursor person, SettlementCursor home)
    {
        // 远处居民须完成返乡后再依据实际仓库供给决策，避免获得远程库存知识。
        if (Distance(person.X, person.Y, home.X, home.Y) > 1)
            return true;
        var reserve = _localWorkQueriesActive
            ? _productionReserves.GetValueOrDefault(home.Id)
            : LocalDevelopmentReserve(home);
        if (person.Profession == Profession.Lumberjack)
            return home.Resources.Wood < Math.Max(80, reserve.Wood);
        if (person.Profession != Profession.Miner)
        {
            return home.Resources.Wood < Math.Max(80, reserve.Wood)
                   || home.Resources.Stone < Math.Max(80, reserve.Stone) ||
                   home.Resources.Ore < Math.Max(80, reserve.Ore);
        }

        return home.Resources.Stone < Math.Max(80, reserve.Stone) || home.Resources.Ore <
                                                                        Math.Max(80, reserve.Ore)
                                                                        || (HasResearch(home.Id,
                                                                                Advancement.Industry) &&
                                                                            home.Resources.Coal < 16)
                                                                        || (HasResearch(home.Id,
                                                                                Advancement.Electrification) &&
                                                                            home.Resources.Oil < 16)
                                                                        || (HasResearch(home.Id,
                                                                                Advancement.AdvancedComputing) &&
                                                                            home.Resources.RareEarth < 16);
    }

    // 初次紧急判断仍立即执行；后续复评对齐个人四日错峰，首次对齐只需三至六日。
    private int GoalReviewInterval(ResidentCursor person)
    {
        if (!(person.Hunger > 60 && person.Inventory.Food < .05)
            && !(Current.Rules.Thirst && person.Thirst > 80 && person.Inventory.Water < .025)) return 24;
        var earliest = Current.Tick + 3;
        var phase = (earliest + person.Id) % 4;
        return 3 + (int)((4 - phase) % 4);
    }

    private void DeferGoalReview(ResidentCursor person, int? interval = null)
    {
        var next = Current.Tick + (interval ?? GoalReviewInterval(person));
        person.Agent.Replace(person.Agent.Value with
        {
            Goal = person.Agent.Goal with { ReviewTick = next },
            NextThinkTick = next,
        });
    }

    private void ChooseAgentGoal(ResidentCursor person, SettlementCursor home, bool interrupted)
    {
        var agent = person.Agent;
        if (person.Profession == Profession.Miner && Distance(person.X, person.Y, home.X, home.Y) <= 1)
        {
            agent.MaterialPriority = home.Resources.Ore < LocalDevelopmentReserve(home).Ore
                ? ResourceKind.Ore
                : HasResearch(home.Id, Advancement.Industry) && home.Resources.Coal < 8
                    ? ResourceKind.Coal
                    : HasResearch(home.Id, Advancement.Electrification) && home.Resources.Oil < 8
                        ? ResourceKind.Oil
                        : HasResearch(home.Id, Advancement.AdvancedComputing) && home.Resources.RareEarth < 8
                            ? ResourceKind.RareEarth
                            : home.Resources.Ore < 30
                                ? ResourceKind.Ore
                                : home.Resources.Stone < 30
                                    ? ResourceKind.Stone
                                    : null;
        }

        var personality = agent.Personality;
        var choices = _goalChoices;
        choices.Clear();
        var foodFact = LatestAgentFact(agent.Memory, AgentFactKind.FoodSupply, home.Id);
        AgentFact? dangerFact = null;
        foreach (var fact in agent.Memory)
            if (fact.Kind == AgentFactKind.Danger && fact.Value > 0 && fact.ReliabilityAt(Current.Tick) > 0.25
                && Current.Tick - fact.ObservedTick < 24 && Distance(person.X, person.Y, fact.X, fact.Y) <= 5
                && (dangerFact is null || fact.ObservedTick > dangerFact.ObservedTick))
                dangerFact = fact;
        if (dangerFact is not null || Current.Tiles[Index(person.X, person.Y)].FireTicks > 0)
        {
            var safe = FindSafeVisibleSite(person, home, dangerFact);
            if (safe >= 0)
            {
                choices.Add(new GoalChoice(AgentGoalKind.Flee, safe % Current.Width, safe / Current.Width,
                    (180 - personality.Courage * 30) *
                    (dangerFact is null ? 1 : Math.Max(0.6, dangerFact.ReliabilityAt(Current.Tick))),
                    dangerFact?.OriginResidentId == person.Id ? "亲眼见到附近危险，先离开危险区域" : "可信的近时报告指出附近危险，先离开核实",
                    dangerFact));
            }
        }

        AddRecoveryChoice(person, home, choices);

        // 正在实地解决缺粮或补水时继续当前劳动；现场枯竭、疲劳和新的身体危险仍会提前复评。
        if (choices.Count == 0 && !interrupted && ProvisionGoalContinues(person))
        {
            DeferGoalReview(person, 24);
            return;
        }

        if (choices.Count == 0 && !interrupted && (
                ProductiveGoalContinues(person, home)
                || (agent.Goal.Kind == AgentGoalKind.Rest && RecoveryGoalContinues(person)
                    && person.Hunger < 60 && person.Thirst < 60)
                || (agent.Goal.Kind == AgentGoalKind.ReturnHome && Distance(person.X, person.Y, home.X, home.Y) > 1
                                                                && Current.Tick < agent.Goal.StartedTick + 240 &&
                                                                Current.Tick >= agent.Goal.NavigationRetryTick
                                                                && person.Hunger < 60 && person.Thirst < 60)))
        {
            DeferGoalReview(person);
            return;
        }

        if (agent.Goal.Kind is AgentGoalKind.ClaimLand or AgentGoalKind.FetchWater or AgentGoalKind.Hunt
                or AgentGoalKind.Fish
            && choices.Count == 0 && Current.Tick - agent.Goal.StartedTick < 48 && person.Hunger < 20 &&
            person.Thirst < 60
            && agent.Fatigue < 60 && Current.Tick >= agent.Goal.NavigationRetryTick
            && (agent.Goal.Kind is not (AgentGoalKind.Hunt or AgentGoalKind.Fish) || WildlifeGoalProductive(person))
            && (agent.Goal.Kind != AgentGoalKind.ClaimLand ||
                CanClaimTile(home, Index(agent.Goal.TargetX, agent.Goal.TargetY), person.Race))
            && (agent.Goal.Kind != AgentGoalKind.FetchWater || person.Thirst >= 10
                                                            || (agent.Goal.TargetEntityId > 0 &&
                                                                DailyWaterYield(
                                                                    Current.Tiles[agent.Goal.TargetEntityId - 1]) >= .1))
            && person.Inventory.Food < TravelReserve(person) + 2
            && (agent.Goal.Kind != AgentGoalKind.FetchWater || person.Inventory.Water < WaterCollectionTarget(person)))
        {
            DeferGoalReview(person);
            return;
        }

        if (agent.Goal.Kind == AgentGoalKind.Migrate && choices.Count == 0 && Current.Tick - agent.Goal.StartedTick < 360
            && person.Hunger < 20 && person.Thirst < 60)
        {
            DeferGoalReview(person);
            return;
        }

        if (agent.Goal.Kind == AgentGoalKind.Work && choices.Count == 0 && person.Hunger < 65 && agent.Fatigue < 60
            && (person.Thirst < 40 || person.Inventory.Water >= .3)
            && FindBuilding(agent.Goal.TargetEntityId) is { } factory && factory.SettlementId == home.Id
            && ProductionRules.For(factory.Kind) is { } recipe && CanProduce(factory, person, recipe)
            && HasProductionInputs(person.Inventory, recipe))
        {
            DeferGoalReview(person);
            return;
        }

        if (agent.Goal.Kind == AgentGoalKind.Explore && choices.Count == 0 && person.Hunger < 65 && agent.Fatigue < 60
            && (person.Thirst < 40 || person.Inventory.Water >= .3)
            && Distance(person.X, person.Y, agent.Goal.TargetX, agent.Goal.TargetY) > 1 &&
            Current.Tick - agent.Goal.StartedTick < 48)
        {
            DeferGoalReview(person);
            return;
        }

        var activeMission = agent.DestinationSettlementId != 0 && Current.Tick - agent.MissionStartedTick < 360
                                                               && agent.Goal.Kind is AgentGoalKind.DeliverMessage
                                                                   or AgentGoalKind.Trade
                                                                   or AgentGoalKind.Petition;
        if (activeMission && choices.Count == 0 && !(person.Hunger > 60 && person.Inventory.Food < 0.05)
            && !(person.Thirst > 80 && person.Inventory.Water < .025))
        {
            // 任务货物和口粮要随本轮任务继续携带，不能按普通工作背包已满而中断旅程。
            DeferGoalReview(person);
            return;
        }

        if (agent.Goal.Kind == AgentGoalKind.ReturnHome && agent.MissionOriginSettlementId != 0
                                                        && Distance(person.X, person.Y, home.X, home.Y) > 1 &&
                                                        choices.Count == 0)
        {
            DeferGoalReview(person);
            return;
        }

        AddFirefightingChoice(person, choices);
        if (AddProvisionChoices(person, home, choices))
        {
            CommitAgentChoice(person, home, choices, interrupted, activeMission);
            return;
        }
        if (person.Inventory.Food < 0.3 &&
            (foodFact is null || foodFact.Value > 0 || foodFact.ReliabilityAt(Current.Tick) < 0.5))
        {
            choices.Add(new GoalChoice(AgentGoalKind.Eat, home.X, home.Y, 65 + person.Hunger,
                foodFact is null ? "随身口粮不足，返回家园查看粮仓" : $"口粮不足；上次获知家乡有 {foodFact.Value:0.0} 份粮食", foodFact, home.Id));
        }

        if (person.Inventory.Food >= Math.Max(4, TravelReserve(person) + 1) || person.Inventory.Wood +
                                                                            person.Inventory.Stone +
                                                                            person.Inventory.Ore >= 3
                                                                            || person.Inventory.Alloy +
                                                                            person.Inventory.EnergyCells +
                                                                            person.Inventory.Crystals > 0
                                                                            || person.Inventory.Coal +
                                                                            person.Inventory.Oil +
                                                                            person.Inventory.RareEarth >= 3 ||
                                                                            (person.TravelMode == TravelMode.Foot &&
                                                                             person.Inventory.Boats +
                                                                             person.Inventory.Aircraft > 0))
        {
            choices.Add(new GoalChoice(AgentGoalKind.ReturnHome, home.X, home.Y, 75 + personality.Diligence * 12,
                "背包已有产物，亲自运回家园入库", null, home.Id));
        }

        if (agent.MissionOriginSettlementId != 0 && agent.DestinationSettlementId == 0
                                                 && Distance(person.X, person.Y, home.X, home.Y) > 1)
        {
            choices.Add(new GoalChoice(AgentGoalKind.ReturnHome, home.X, home.Y, 70,
                "递送已结束或中止，带着剩余物资亲自返乡", null, home.Id));
        }

        if (agent.Fatigue > 35)
        {
            choices.Add(new GoalChoice(AgentGoalKind.Rest, home.X, home.Y, agent.Fatigue * 1.15,
                $"疲劳达到 {agent.Fatigue:0}，回家休息", null, home.Id));
        }

        var depositSite = LocalMaterialsNeeded(person, home) ? VisibleDepositSite(person) : -1;
        if (person.Age >= 14 && depositSite >= 0)
        {
            choices.Add(new GoalChoice(AgentGoalKind.Work, depositSite % Current.Width, depositSite / Current.Width,
                65, "掌握勘探知识后在眼前发现矿藏，实地开采并运回"));
        }

        var foodSite = FoodSupplyNeeded(person, home) ? FindVisibleResourceSite(person, Profession.Farmer) : -1;
        if (foodSite >= 0)
        {
            var score = person.Profession == Profession.Farmer ? 36 + personality.Diligence * 13 : 12;
            score += person.Hunger * (person.Inventory.Food < 0.3 ? 1.1 : 0.1);
            score *= AgentFoodPolicyMultiplier(person);
            if (foodFact is { Value: < 12 })
                score += 18 * foodFact.ReliabilityAt(Current.Tick);
            if (Current.Rules.Hunger && person.Inventory.Food < .3 && person.Hunger >= 20)
                score = Math.Max(score, 100 + person.Hunger);
            choices.Add(new GoalChoice(AgentGoalKind.Gather, foodSite % Current.Width, foodSite / Current.Width, score,
                foodFact is { Value: < 12 } ? "已知粮情显示家乡粮少，在可见的可食土地采集" : "眼前土地能产食物，采集后随身携带", foodFact));
        }

        if (Current.Rules.Hunger && person.Hunger > 60 && person.Inventory.Food < .05
            && choices.Any(choice => choice.Kind is AgentGoalKind.Gather or AgentGoalKind.Hunt or AgentGoalKind.Fish or AgentGoalKind.Eat))
        {
            CommitAgentChoice(person, home, choices, interrupted, activeMission);
            return;
        }

        int? materialSite = null;
        if (person.Age >= 14 && person.Profession is Profession.Lumberjack or Profession.Miner
                             && LocalMaterialsNeeded(person, home))
        {
            var site = FindVisibleResourceSite(person, person.Profession);
            materialSite = site;
            if (site >= 0)
            {
                choices.Add(new GoalChoice(AgentGoalKind.Work, site % Current.Width, site / Current.Width,
                    58 + personality.Diligence * 12,
                    person.Profession == Profession.Lumberjack ? "看见可采木材，前往伐木" : "看见矿石露头，前往开采"));
            }
        }

        var workTarget = person.Age >= 14 ? FindLocalWorkTarget(person) : null;
        if (person.Age >= 14 && (person.Profession is Profession.Farmer or Profession.Lumberjack or Profession.Miner
                                     or Profession.Builder or Profession.Scholar or Profession.Mage
                                     or Profession.Fisher ||
                                 person.Profession >= Profession.Engineer)
                             && workTarget is { } work)
        {
            var kind = work.Kind == BuildingKind.Academy && person.Profession == Profession.Scholar
                ? AgentGoalKind.Study
                : work.Kind is BuildingKind.ArcaneSanctum or BuildingKind.SacredGrove &&
                  person.Profession is Profession.Mage or Profession.Battlemage
                    ? AgentGoalKind.TrainMagic
                    : AgentGoalKind.Work;
            choices.Add(new GoalChoice(kind, work.X, work.Y, 42 + personality.Diligence * 12
                                                                + (person.Profession == Profession.Farmer &&
                                                                   foodFact is { Value: < 12 }
                                                                    ? 18 * foodFact.ReliabilityAt(Current.Tick)
                                                                    : 0),
                kind == AgentGoalKind.Study ? "附近有可参与的研究设施，前往学习" :
                kind == AgentGoalKind.TrainMagic ? "附近有可训练的魔法设施" : "附近有实际施工或生产工作", EntityId: work.Id));
        }

        if (person.Age >= 14 && person.Profession is Profession.Lumberjack or Profession.Miner
                             && (person.Profession == Profession.Lumberjack
                                 ? (materialSite ?? FindVisibleResourceSite(person, Profession.Lumberjack)) < 0
                                 : depositSite < 0 &&
                                   (materialSite ?? FindVisibleResourceSite(person, Profession.Miner)) < 0)
                             && ((Distance(person.X, person.Y, home.X, home.Y) <= 1 &&
                                  (person.Profession == Profession.Lumberjack
                                      ? home.Resources.Wood < 60
                                      : agent.MaterialPriority is not null ||
                                        (HasResearch(home.Id, Advancement.Industry) && home.Resources.Coal < 8) ||
                                        (HasResearch(home.Id, Advancement.Electrification) && home.Resources.Oil < 8)
                                        || (HasResearch(home.Id, Advancement.AdvancedComputing) &&
                                            home.Resources.RareEarth < 8))) ||
                                 agent.Goal.Kind == AgentGoalKind.Explore))
        {
            var offsets = new (int X, int Y)[] { (6, 0), (4, 4), (0, 6), (-4, 4), (-6, 0), (-4, -4), (0, -6), (4, -4) };
            var heading = offsets[agent.ExplorationHeading % offsets.Length];
            var site = Circle(person.X, person.Y, 6)
                .Where(i => Current.Tiles[i].IsWalkable && Current.Tiles[i].FireTicks == 0)
                .OrderBy(i => Distance(i % Current.Width, i / Current.Width, person.X + heading.X, person.Y + heading.Y))
                .FirstOrDefault(-1);
            if (site >= 0 && Distance(person.X, person.Y, home.X, home.Y) < 24)
            {
                choices.Add(new GoalChoice(AgentGoalKind.Explore, site % Current.Width, site / Current.Width, 62,
                    person.Profession == Profession.Lumberjack
                        ? "在家园看到木材短缺，眼前没有可采森林，沿可见陆地寻找下一处材料来源"
                        : "在家园看到石材、矿石或生产燃料不足，沿可见陆地寻找可开采材料"));
            }
            else
            {
                agent.ExplorationHeading = (agent.ExplorationHeading + 3) % 8;
                choices.Add(new GoalChoice(AgentGoalKind.ReturnHome, home.X, home.Y, 70, "勘察距离已达口粮范围，先返回家园补给", null,
                    home.Id));
            }
        }

        if (agent.SocialNeed > 35 && _citizens[home.Id].Count > 1)
        {
            choices.Add(new GoalChoice(AgentGoalKind.Socialize, home.X, home.Y,
                agent.SocialNeed * (0.6 + personality.Sociability * 0.5), "社交需求较高，去聚落与人交流", null, home.Id));
        }

        if (person.Age >= 14 && person.ArmyId == 0 && workTarget is { } useful
            && !choices.Any(c => c.EntityId == useful.Id))
        {
            choices.Add(new GoalChoice(AgentGoalKind.Work, useful.X, useful.Y, 25 + personality.Diligence * 8,
                "本职暂无任务，协助附近实际施工或生产", EntityId: useful.Id));
        }

        if (Distance(person.X, person.Y, home.X, home.Y) <= 1 && choices.Count == 0)
        {
            choices.Add(_citizens[home.Id].Count > 1
                ? new GoalChoice(AgentGoalKind.Socialize, home.X, home.Y, 8, "暂时没有可执行工作，在家园交流消息与恢复精力", null, home.Id)
                : new GoalChoice(AgentGoalKind.Rest, home.X, home.Y, 8, "暂时没有可执行工作或交流对象，在家园休息并等待下一次评估", null, home.Id));
        }

        AddAgentMissionChoices(person, home, choices);
        CommitAgentChoice(person, home, choices, interrupted, activeMission);
    }

    private bool ProvisionGoalContinues(ResidentCursor person)
    {
        var goal = person.Agent.Goal;
        if (person.Agent.Fatigue >= 60 || person.Thirst >= 60
            || Current.Tick < goal.NavigationRetryTick
            || Distance(person.X, person.Y, goal.TargetX, goal.TargetY) > 6)
            return false;
        if (goal.Kind == AgentGoalKind.FetchWater)
        {
            var source = goal.TargetEntityId - 1;
            return source >= 0 && source < Current.Tiles.Count && person.Hunger < 60
                && person.Inventory.Water < WaterCollectionTarget(person)
                && GetWaterSupply(source % Current.Width, source / Current.Width) >= .1;
        }
        if (person.Hunger < 60 || person.Inventory.Food >= TravelReserve(person) + 2
            || Current.Rules.Thirst && person.Inventory.Water < WaterUse(person)
                && GetWaterSupply(person.X, person.Y) < WaterUse(person))
            return false;
        if (goal.Kind == AgentGoalKind.Gather)
        {
            var source = Index(goal.TargetX, goal.TargetY);
            var tile = Current.Tiles[source];
            var efficiency = NaturalPlantHarvestEfficiency(tile);
            if (efficiency < .25) return false;
            var dailyYield = .7 * ResourceSiteYield(source, Profession.Farmer, efficiency, out _)
                * RaceTerrainRules.For(person.Race, tile.Terrain).Productivity * GatheringCondition(person)
                * (.75 + person.Agent.Personality.Diligence * .5) * Current.Rules.GatheringRate
                * AgentFoodPolicyMultiplier(person) * GatheringTerritoryMultiplier(person, tile);
            return dailyYield >= FoodUse(person);
        }
        if (goal.Kind is not (AgentGoalKind.Hunt or AgentGoalKind.Fish) || !WildlifeGoalProductive(person))
            return false;
        var wildlifeSource = Current.Tiles[goal.TargetEntityId - 1];
        var animal = EdibleAnimal(wildlifeSource, goal.Kind == AgentGoalKind.Fish);
        var interval = WorkInterval(person);
        var harvest = WildlifeHarvestAmount(wildlifeSource, animal,
            interval * .15 * Current.Rules.GatheringRate * GatheringCondition(person)
            * GatheringTerritoryMultiplier(person, wildlifeSource));
        return harvest * AnimalRules.For(animal).BodyMass / interval >= FoodUse(person);
    }

    private void CommitAgentChoice(ResidentCursor person, SettlementCursor home, List<GoalChoice> choices,
        bool interrupted, bool activeMission)
    {
        var agent = person.Agent;
        if (Current.Tick < agent.Goal.NavigationRetryTick)
            choices.RemoveAll(c => Index(c.X, c.Y) == agent.Goal.NavigationTarget);
        choices.Add(new GoalChoice(AgentGoalKind.ReturnHome, home.X, home.Y, 5, "当前看不到合适资源，回到已知家园", null, home.Id));
        var selected = choices[0];
        for (var i = 1; i < choices.Count; i++)
        {
            var candidate = choices[i];
            var comparison = candidate.Score.CompareTo(selected.Score);
            if (comparison > 0 || (comparison == 0 && (int)candidate.Kind < (int)selected.Kind))
                selected = candidate;
        }

        var previous = agent.Goal;
        var continuingMission = previous.Kind == selected.Kind && previous.TargetSettlementId == selected.SettlementId
                                                               && previous.TargetEntityId == selected.EntityId &&
                                                               previous.TargetX == selected.X &&
                                                               previous.TargetY == selected.Y;
        if (continuingMission)
        {
            agent.Replace(agent.Value with
            {
                Goal = (previous.NavigationRetryTick > 0 && Current.Tick >= previous.NavigationRetryTick
                    ? previous.ResetNavigation() : previous) with
                {
                    ReviewTick = Current.Tick + GoalReviewInterval(person),
                    PlayerDirected = previous.PlayerDirected && !interrupted,
                },
                NextThinkTick = Current.Tick + GoalReviewInterval(person),
            });
            return;
        }

        var reason = (interrupted ? "紧急需要打断原意愿：" : "") + selected.Reason
                                                       + (activeMission && selected.Kind is not AgentGoalKind.Trade
                                                           and not AgentGoalKind.DeliverMessage
                                                           and not AgentGoalKind.Petition
                                                           ? "；递送中止，物资仍随身携带"
                                                           : "");
        var nextGoal = new AgentGoal
        {
            Kind = selected.Kind,
            TargetX = selected.X,
            TargetY = selected.Y,
            TargetSettlementId = selected.SettlementId,
            TargetEntityId = selected.EntityId,
            StartedTick = Current.Tick,
            ReviewTick = Current.Tick + GoalReviewInterval(person),
            Reason = reason,
        };
        var decisions = agent.Value.Decisions.Add(new AgentDecision
        {
            Tick = Current.Tick,
            Goal = selected.Kind,
            Score = selected.Score,
            Reason = reason,
            EvidenceFactId = selected.Evidence?.Id ?? 0,
            KnowledgeObservedTick = selected.Evidence?.ObservedTick ?? Current.Tick,
            SourceResidentId = selected.Evidence?.SourceResidentId ?? person.Id,
        });
        if (decisions.Count > 6) decisions = decisions.RemoveAt(0);
        ChangeWorkReservation(previous, nextGoal);
        agent.Replace(agent.Value with { Goal = nextGoal, NextThinkTick = Current.Tick + GoalReviewInterval(person), Decisions = decisions });
        if (selected.Kind is AgentGoalKind.DeliverMessage or AgentGoalKind.Trade or AgentGoalKind.Petition)
            BeginAgentMission(person, home);
        else if (agent.DestinationSettlementId != 0)
        {
            agent.DestinationSettlementId = 0;
            agent.CarriedMessages.Clear();
            agent.MissionRetryTick = Current.Tick + 120;
        }
    }

    private int FindVisibleResourceSite(ResidentCursor person, Profession profession)
    {
        if (profession == Profession.Miner &&
            person.Agent.MaterialPriority is ResourceKind.Coal or ResourceKind.Oil or ResourceKind.RareEarth)
            return -1;
        var reachable = 0;
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
            if (!RaceTerrainRules.CanWalk(tile, person.Race) || tile.FireTicks > 0)
                continue;
            double? plantEfficiency = null;
            if (profession != Profession.Miner)
            {
                plantEfficiency = NaturalPlantHarvestEfficiency(tile, profession == Profession.Lumberjack);
                if (plantEfficiency < .25)
                    continue;
            }

            var productivity = ResourceSiteYield(index, profession, plantEfficiency, out var oreAvailable) *
                               GatheringTerritoryMultiplier(person, tile);
            if (productivity <= 0)
                continue;
            if (profession == Profession.Miner && person.Agent.MaterialPriority == ResourceKind.Ore
                                               && !oreAvailable)
                continue;
            if (profession == Profession.Farmer && Current.Rules.Hunger && person.Hunger > 20
                && .7 * productivity * RaceTerrainRules.For(person.Race, tile.Terrain).Productivity *
                GatheringCondition(person) * (.75 + person.Agent.Personality.Diligence * .5) *
                Current.Rules.GatheringRate < FoodUse(person))
                continue;
            if (!VisibleSiteReachable(person, index, ref reachable))
                continue;
            return index;
        }

        return -1;
    }

    private double ResourceSiteYield(int index, Profession profession)
    {
        return ResourceSiteYield(index, profession, null, out _);
    }

    private double ResourceSiteYield(int index, Profession profession, double? plantEfficiency, out bool oreAvailable)
    {
        var tile = Current.Tiles[index];
        oreAvailable = false;
        if (profession == Profession.Farmer)
        {
            return tile.ResourceAmount > 0 && tile.IsWalkable
                ? Math.Min(1, TerrainRules.For(tile.Terrain).FoodYield / .7) * tile.Fertility / 100d *
                  (tile.DroughtTicks > 0 ? 0.15 : 1) * (plantEfficiency ?? NaturalPlantHarvestEfficiency(tile))
                : 0;
        }

        if (profession == Profession.Lumberjack)
        {
            return tile.ResourceAmount > 0 && IsForestTerrain(tile.Terrain)
                ? TerrainRules.For(tile.Terrain).WoodYield *
                  (plantEfficiency ?? NaturalPlantHarvestEfficiency(tile, true))
                : 0;
        }

        if (profession == Profession.Miner)
        {
            var best = tile.ResourceAmount > 0
                ? Math.Min(1, TerrainRules.For(tile.Terrain).StoneYield + TerrainRules.For(tile.Terrain).OreYield)
                : 0;
            oreAvailable = TerrainRules.For(tile.Terrain).OreYield > 0;
            var x = index % Current.Width;
            var y = index / Current.Width;
            foreach (var (dx, dy) in Directions)
                if (InBounds(x + dx, y + dy) && Current.Tiles[Index(x + dx, y + dy)] is
                    { Terrain: TerrainType.Mountain, ResourceAmount: > 0 } mountain)
                {
                    oreAvailable = true;
                    best = Math.Max(best,
                        Math.Min(1,
                            TerrainRules.For(mountain.Terrain).StoneYield +
                            TerrainRules.For(mountain.Terrain).OreYield));
                }

            return best;
        }

        return 0;
    }

    /// <summary>标记六格可见半径内的可达地格，并返回本次搜索的标记编号。</summary>
    /// <param name="person">提供搜索起点和种族通行规则的居民。</param>
    /// <param name="requestedMode">待评估的交通方式，不修改居民状态；默认使用居民当前的方式。</param>
    private int MarkVisibleReachable(ResidentCursor person, TravelMode? requestedMode = null)
    {
        _territoryCounts.Bind(Current.Tiles);
        // 地形、桥梁或火情可能在同一天改变，因此不能只按时间判断缓存路径是否有效。
        var revision = _territoryCounts.TraversalRevision;
        var mode = requestedMode ?? person.TravelMode;
        var start = Index(person.X, person.Y);
        if (_visibleAccessSearch != 0 && _visibleAccessSearch == _localMoveSearch && _visibleAccessRevision == revision
            && _visibleAccessOrigin == start && _visibleAccessWidth == Current.Width
            && _visibleAccessMode == mode && _visibleAccessRace == person.Race)
            return _visibleAccessSearch;
        if (_localMoveVisited.Length != Current.Tiles.Count)
            _localMoveVisited = new int[Current.Tiles.Count];
        if (_localMoveSearch == int.MaxValue)
        {
            Array.Clear(_localMoveVisited);
            _localMoveSearch = 0;
        }

        var search = ++_localMoveSearch;
        _visibleAccessOrigin = start;
        _visibleAccessRevision = revision;
        _visibleAccessWidth = Current.Width;
        _visibleAccessMode = mode;
        _visibleAccessRace = person.Race;
        _visibleAccessSearch = search;
        var cache = _visibleAccessCache ??= new VisibleAccessCache();
        var key = start * 12 + (int)person.Race * 3 + (int)mode;
        var slot = cache.Slot(key, Current.Width);
        if (cache.TryMark(slot, key, revision, _localMoveVisited, search))
            return search;
        _localMoveVisited[start] = search;
        var head = 0;
        var tail = 1;
        _localMoveQueue[0] = (start, -1, 0);
        while (head < tail)
        {
            var current = _localMoveQueue[head++];
            var currentX = current.Index % Current.Width;
            var currentY = current.Index / Current.Width;
            var from = Current.Tiles[current.Index];
            foreach (var (dx, dy) in Directions)
            {
                var x = currentX + dx;
                var y = currentY + dy;
                if (!InBounds(x, y) || Distance(person.X, person.Y, x, y) > 6)
                    continue;
                var index = Index(x, y);
                if (_localMoveVisited[index] == search)
                    continue;
                var to = Current.Tiles[index];
                var horizontal = dy == 0;
                if (to.FireTicks > 0 || !CanTraverse(to, mode, person.Race)
                                     || (mode == TravelMode.Foot &&
                                         ((from.Improvement == LandImprovement.Bridge && horizontal !=
                                              (from.BridgeDirection == BridgeDirection.Horizontal))
                                          || (to.Improvement == LandImprovement.Bridge && horizontal !=
                                              (to.BridgeDirection == BridgeDirection.Horizontal)))))
                    continue;
                _localMoveVisited[index] = search;
                _localMoveQueue[tail++] = (index, -1, current.Depth + 1);
            }
        }

        cache.Store(slot, key, revision, _localMoveQueue, tail);
        return search;
    }

    private bool VisibleSiteReachable(ResidentCursor person, int index, ref int search, TravelMode? requestedMode = null)
    {
        var mode = requestedMode ?? person.TravelMode;
        var x = index % Current.Width;
        var y = index / Current.Width;
        var distance = Distance(person.X, person.Y, x, y);
        if (distance == 0)
            return true;
        if (distance == 1)
            return CanTraverseStep(person.X, person.Y, x, y, mode, person.Race);
        if (distance > 6)
            return false;
        if (search == 0)
            search = MarkVisibleReachable(person, mode);
        return _localMoveVisited[index] == search;
    }

    private void ActOnAgentGoal(ResidentCursor person, SettlementCursor home)
    {
        var goal = person.Agent.Goal;
        if (goal.Kind == AgentGoalKind.Migrate)
        {
            ActOnMigration(person);
            return;
        }

        if (goal.Kind is AgentGoalKind.DeliverMessage or AgentGoalKind.Trade or AgentGoalKind.Petition)
        {
            ActOnAgentMission(person, home);
            return;
        }

        if (goal.Kind is AgentGoalKind.Work or AgentGoalKind.Study or AgentGoalKind.TrainMagic
            && (ActOnBuildingRepair(person, home) || ActOnHusbandry(person, home) || ActOnProduction(person, home) ||
                ActOnRacialWork(person, home) || ActOnExpansionFacility(person, home)))
            return;
        if (goal.Kind == AgentGoalKind.Fish)
            PrepareJourneyTransport(person, home);
        var interactionRange = AgentInteractionRange(person, home);
        if (Distance(person.X, person.Y, goal.TargetX, goal.TargetY) > interactionRange ||
            !CanTraverse(Current.Tiles[Index(person.X, person.Y)], person.TravelMode, person.Race))
        {
            var activity = goal.Kind == AgentGoalKind.Flee ? ResidentActivity.Fleeing : ResidentActivity.Wandering;
            if (!MoveAgentTowards(person, goal.TargetX, goal.TargetY, activity))
                person.Activity = activity;
            return;
        }

        if (Current.Tick - person.MoveStartedTick < person.MoveDurationTicks)
            return;
        goal = goal.Attend();
        switch (goal.Kind)
        {
            case AgentGoalKind.Eat:
                // 到场补给已在每日需求前结算；等待期间不重复生成同一粮情或重设评估时间。
                var inspectFood = person.Activity != ResidentActivity.Eating || goal.StartedTick == Current.Tick
                    || Current.Tick >= person.Agent.NextThinkTick;
                person.Activity = ResidentActivity.Eating;
                if (inspectFood)
                {
                    var reviewInterval = GoalReviewInterval(person);
                    var next = Math.Min(person.Agent.NextThinkTick,
                        Current.Tick + (reviewInterval < 24 ? reviewInterval : 4));
                    var observed = person.Agent.Value.Remember(MakeAgentFact(person, AgentFactKind.FoodSupply, home.Id,
                        home.X, home.Y, home.Resources.Food, $"实地查看粮仓：{home.Resources.Food:0.0} 份粮食"), home.Id);
                    person.Agent.Replace(observed with { Goal = goal with { ReviewTick = next }, NextThinkTick = next });
                }
                break;
            case AgentGoalKind.Gather:
                person.Agent.Goal = goal;
                GatherActualResources(person, Profession.Farmer);
                break;
            case AgentGoalKind.Work:
                person.Agent.Goal = goal;
                if (goal.TargetEntityId == 0 &&
                    person.Profession is Profession.Farmer or Profession.Lumberjack or Profession.Miner)
                    GatherActualResources(person, person.Profession);
                else if (TryWorkAtBuilding(person))
                    person.Activity = ResidentActivity.Working;
                else if (person.Profession is Profession.Farmer or Profession.Lumberjack or Profession.Miner)
                    GatherActualResources(person, person.Profession);
                else
                    person.Agent.NextThinkTick = Current.Tick + 1;
                break;
            case AgentGoalKind.Rest:
                if (person.Agent.Fatigue == 0 && person.Activity == ResidentActivity.Resting) break;
                person.Replace(person.Value with
                {
                    Activity = ResidentActivity.Resting,
                    Agent = person.Agent.Value with
                    {
                        Goal = goal,
                        Fatigue = Math.Max(0, person.Agent.Fatigue - 2.2 * HomeRestMultiplier(person)),
                    },
                });
                break;
            case AgentGoalKind.Flee:
                person.Replace(person.Value with
                {
                    Activity = ResidentActivity.Fleeing,
                    Agent = person.Agent.Value with { Goal = goal, NextThinkTick = Current.Tick + 1 },
                });
                break;
            case AgentGoalKind.Socialize:
                if (person.Agent.Fatigue == 0 && person.Activity == ResidentActivity.Talking) break;
                person.Replace(person.Value with
                {
                    Activity = ResidentActivity.Talking,
                    Agent = person.Agent.Value with { Goal = goal, Fatigue = Math.Max(0, person.Agent.Fatigue - .4) },
                });
                break;
            case AgentGoalKind.Study:
            case AgentGoalKind.TrainMagic:
                person.Agent.Goal = goal;
                if (TryWorkAtBuilding(person))
                    person.Activity = ResidentActivity.Studying;
                else
                    person.Agent.NextThinkTick = Current.Tick + 1;
                break;
            case AgentGoalKind.ReturnHome:
                person.Agent.Goal = goal;
                TransferPersonalProduction(person, home);
                FinishFoundation(person, home);
                var fatigue = Math.Max(0, person.Agent.Fatigue - .8 * HomeRestMultiplier(person));
                var nextReview = goal.WorkTicks == 1
                    ? Current.Tick + (home.FoundationPending ? 4 : GoalReviewInterval(person))
                    : person.Agent.NextThinkTick;
                if (person.Activity != ResidentActivity.Resting || fatigue != person.Agent.Fatigue
                    || nextReview != person.Agent.NextThinkTick)
                    person.Replace(person.Value with
                {
                    Activity = ResidentActivity.Resting,
                    Agent = person.Agent.Value with
                    {
                        Fatigue = fatigue,
                        NextThinkTick = nextReview,
                    },
                });
                break;
            case AgentGoalKind.Explore:
                // 完成一段探索后保持向外前进，补给或受阻时才转向，避免反复绕同一小圈。
                var turn = Distance(person.X, person.Y, goal.TargetX, goal.TargetY) == 0 &&
                           goal.TargetX == person.FromX && goal.TargetY == person.FromY;
                person.Replace(person.Value with
                {
                    Activity = ResidentActivity.Working,
                    Agent = person.Agent.Value with
                    {
                        Goal = goal,
                        ExplorationHeading = turn ? (person.Agent.ExplorationHeading + 1) % 8 : person.Agent.ExplorationHeading,
                        NextThinkTick = Current.Tick + 1,
                    },
                });
                break;
            case AgentGoalKind.ClaimLand:
                person.Agent.Goal = goal;
                person.Activity = ResidentActivity.Working;
                TryClaimLand(person);
                break;
            case AgentGoalKind.FetchWater:
                person.Agent.Goal = goal;
                TryFetchWater(person);
                break;
            case AgentGoalKind.Hunt:
            case AgentGoalKind.Fish:
                person.Agent.Goal = goal;
                if (!IsWorkDay(person)) break;
                TryHarvestWildlife(person);
                break;
            case AgentGoalKind.ExtinguishFire:
                person.Agent.Goal = goal;
                TryExtinguishFire(person);
                break;
            default:
                person.Agent.Goal = goal;
                break;
        }
    }

    private int AgentInteractionRange(ResidentCursor person, SettlementCursor? home)
    {
        var goal = person.Agent.Goal;
        if (home?.FoundationPending == true && goal.Kind == AgentGoalKind.ReturnHome)
            return 0;
        if (goal.Kind == AgentGoalKind.ExtinguishFire)
            return 1;
        if (goal.Kind is AgentGoalKind.ClaimLand or AgentGoalKind.FetchWater or AgentGoalKind.Hunt
            or AgentGoalKind.Fish)
            return 0;
        if (goal.Kind is AgentGoalKind.Eat or AgentGoalKind.Rest or AgentGoalKind.Socialize
            or AgentGoalKind.ReturnHome)
            return 1;
        return goal.Kind is AgentGoalKind.Work or AgentGoalKind.Study or AgentGoalKind.TrainMagic
               && FindBuilding(goal.TargetEntityId) is { } building
               && (!building.IsCompleted || building.IsUpgrading || IsWaterfrontBuilding(building.Kind) ||
                   building.Kind == BuildingKind.TownCenter)
            ? 1
            : 0;
    }

    private void GatherActualResources(ResidentCursor person, Profession profession)
    {
        if (!IsWorkDay(person)) return;
        if (profession == Profession.Miner && TryGatherDeposit(person))
            return;
        var index = Index(person.X, person.Y);
        var tile = Current.Tiles[index];
        var siteYield = tile.FireTicks > 0 ? 0 : ResourceSiteYield(index, profession);
        if (siteYield <= 0)
        {
            person.Agent.NextThinkTick = Current.Tick + 1;
            return;
        }

        var productivity = WorkInterval(person) * RaceTerrainRules.For(person.Race, tile.Terrain).Productivity * GatheringCondition(person)
            * (0.75 + person.Agent.Personality.Diligence * 0.5) * Current.Rules.GatheringRate
            * (profession is Profession.Lumberjack or Profession.Miner &&
               HasResearch(person.SettlementId, Advancement.Forestry)
                ? 1.25
                : 1);
        var inventory = person.Inventory;
        if (profession == Profession.Farmer)
        {
            var amount = HarvestPlants(tile,
                0.7 * siteYield * productivity * AgentFoodPolicyMultiplier(person) *
                GatheringTerritoryMultiplier(person, tile));
            inventory = inventory with { Food = inventory.Food + amount };
            RecordHarvest(tile, amount);
        }
        else if (profession == Profession.Lumberjack)
        {
            var amount = HarvestPlants(tile,
                0.28 * siteYield * productivity * (person.Race == RaceKind.Elf ? 1.2 : 1) *
                GatheringTerritoryMultiplier(person, tile), true);
            inventory = inventory with { Wood = inventory.Wood + amount };
            RecordHarvest(tile, amount);
            FinishLogging(tile, person.X, person.Y);
        }
        else
        {
            if (tile.ResourceAmount <= 0 ||
                TerrainRules.For(tile.Terrain).StoneYield + TerrainRules.For(tile.Terrain).OreYield <= 0)
            {
                foreach (var (dx, dy) in Directions)
                    if (InBounds(person.X + dx, person.Y + dy)
                        && Current.Tiles[Index(person.X + dx, person.Y + dy)] is
                            { Terrain: TerrainType.Mountain, ResourceAmount: > 0 } mountain)
                    {
                        tile = mountain;
                        break;
                    }
            }

            var amount = Math.Min(tile.ResourceAmount,
                0.24 *
                Math.Min(1, TerrainRules.For(tile.Terrain).StoneYield + TerrainRules.For(tile.Terrain).OreYield) *
                productivity * (person.Race == RaceKind.Dwarf ? 1.3 : 1) * GatheringTerritoryMultiplier(person, tile));
            tile.ResourceAmount -= amount;
            RecordHarvest(tile, amount);
            var minerals = TerrainRules.For(tile.Terrain);
            var oreRatio = minerals.OreYield / Math.Max(.001, minerals.StoneYield + minerals.OreYield);
            inventory = inventory with
            {
                Stone = inventory.Stone + amount * (1 - oreRatio),
                Ore = inventory.Ore + amount * oreRatio,
            };
        }

        var agent = person.Agent.Value;
        var nextThink = profession != Profession.Miner && !agent.Goal.PlayerDirected
            && NaturalPlantHarvestEfficiency(tile, profession == Profession.Lumberjack) < .25
            ? Current.Tick + 1 : agent.NextThinkTick;
        person.Replace(person.Value with
        {
            Inventory = inventory,
            Agent = agent with { Fatigue = Math.Min(100, agent.Fatigue + 0.30 * WorkInterval(person)), NextThinkTick = nextThink },
            Activity = ResidentActivity.Working,
        });
    }

    /// <summary>仅依据六格可见范围内的地形推进一步，并保留居民自身的导航进度。</summary>
    /// <param name="person">移动的居民。</param>
    /// <param name="targetX">当前导航目标的横向地格坐标。</param>
    /// <param name="targetY">当前导航目标的纵向地格坐标。</param>
    /// <param name="walkingActivity">普通自主行走的活动状态，同时计入原有疲劳；其他调用保留自身活动和补给规则。</param>
    private bool MoveAgentTowards(ResidentCursor person, int targetX, int targetY, ResidentActivity? walkingActivity = null)
    {
        if (person.FrozenUntilTick > Current.Tick || !InBounds(targetX, targetY) ||
            (person.X == targetX && person.Y == targetY))
            return false;
        if (Current.Tick - person.MoveStartedTick < person.MoveDurationTicks)
            return false;
        if (person.Agent.Goal.NavigationTarget == Index(targetX, targetY) &&
            Current.Tick < person.Agent.Goal.NavigationRetryTick)
            return false;
        var bestStep = SelectAgentStep(person, targetX, targetY, out var navigation);
        if (Current.Rules.Construction && person.TravelMode == TravelMode.Foot &&
            HasResearch(person.SettlementId, Advancement.Logistics)
            && Distance(person.X, person.Y, targetX, targetY) <= 6 &&
            !IsWaterfrontBuilding(FindBuilding(person.Agent.Goal.TargetEntityId)?.Kind ?? BuildingKind.Farm))
        {
            var visible = MarkVisibleReachable(person);
            if (_localMoveVisited[Index(targetX, targetY)] != visible)
                PlanVisibleCrossing(person, targetX, targetY);
        }

        if (bestStep < 0)
        {
            PlanVisibleCrossing(person, targetX, targetY);
            var blocked = navigation with
            {
                NavigationRetryTick = Current.Tick + 24,
                Reason = "可见范围内没有可用路线，等待通道或重新选择任务",
            };
            if (!blocked.PlayerDirected) blocked = blocked with { ReviewTick = Current.Tick };
            person.Agent.Replace(person.Agent.Value with
            {
                Goal = blocked,
                NextThinkTick = blocked.PlayerDirected ? person.Agent.NextThinkTick : Current.Tick,
            });

            return false;
        }

        var xNext = bestStep % Current.Width;
        var yNext = bestStep / Current.Width;
        var duration = AgentMoveDuration(person, bestStep);
        var remaining = Distance(xNext, yNext, targetX, targetY);
        var goal = remaining < navigation.NavigationBestDistance
            ? navigation with { NavigationBestDistance = remaining, NavigationWithoutProgress = 0, NavigationVisited = [] }
            : navigation with
            {
                NavigationWithoutProgress = Math.Min(64, navigation.NavigationWithoutProgress + 1),
                NavigationVisited = navigation.NavigationVisited.Contains(Index(person.X, person.Y))
                    ? navigation.NavigationVisited : navigation.NavigationVisited.Add(Index(person.X, person.Y)),
            };
        person.Replace(person.Value with
        {
            FromX = person.X, FromY = person.Y, X = xNext, Y = yNext,
            MoveStartedTick = Current.Tick, MoveDurationTicks = duration,
            Activity = walkingActivity ?? person.Activity,
            Agent = person.Agent.Value.RememberRouteTile(Index(person.X, person.Y)) with
            {
                Goal = goal,
                Fatigue = walkingActivity.HasValue ? Math.Min(100, person.Agent.Fatigue + .15) : person.Agent.Fatigue,
            },
        });

        return true;
    }

    // 选路与实际移动共用耗时，舟船、信使和种族设施的修正不能只影响其中一端。
    private int AgentMoveDuration(ResidentCursor person, int index)
    {
        var terrain = Current.Tiles[index].Value;
        if (person.TravelMode == TravelMode.Foot && person.Agent.DestinationSettlementId == 0
            && person.Profession != Profession.Trader)
            return MoveDurationForSpeed(1 / TerrainMoveCost(terrain, person.Race));
        var x = index % Current.Width;
        var y = index / Current.Width;
        var speed = person.TravelMode == TravelMode.Aircraft ? 2
            : person.TravelMode == TravelMode.Boat && !terrain.IsWalkable
                ? 1.5 * BoatTravelMultiplier(x, y, person.NationId)
                : person.Agent.DestinationSettlementId == 0 ? 1 / TerrainMoveCost(terrain, person.Race)
                    : MessageTravelMultiplier(x, y, person.NationId, person.Race);
        speed *= RacialTravelBonus(person, x, y);
        return MoveDurationForSpeed(speed);
    }

    private static int MoveDurationForSpeed(double speed) => Math.Clamp((int)Math.Round(2 / Math.Max(.1, speed)), 1, 8);

    private int SelectAgentStep(ResidentCursor person, int targetX, int targetY, out AgentGoal goal)
    {
        var originX = person.X;
        var originY = person.Y;
        var width = Current.Width;
        var height = Current.Height;
        var mode = person.TravelMode;
        var race = person.Race;
        var target = Index(targetX, targetY);
        var start = Index(originX, originY);
        goal = person.Agent.Goal;
        if (goal.NavigationTarget != target)
            goal = goal.BeginNavigation(target, Distance(originX, originY, targetX, targetY));

        if (Current.Tick < goal.NavigationRetryTick)
        {
            return -1;
        }
        var route = goal.NavigationRoute;
        var routeOffset = goal.NavigationRouteOffset;
        if (routeOffset > 0 && routeOffset < route.Length && route[routeOffset - 1] == start
            && goal.NavigationRouteMode == mode)
        {
            var next = route[routeOffset];
            var nextX = next % width;
            var nextY = next / width;
            if (CanTraverseStep(originX, originY, nextX, nextY, mode, race)
                && (mode == TravelMode.Aircraft || Current.Tiles[next].FireTicks == 0)
                && !goal.NavigationVisited.Contains(next))
            {
                if (goal.NavigationWithoutProgress >= 64 || goal.NavigationVisited.Length >= 256) return -1;
                goal = goal with { NavigationRouteOffset = routeOffset + 1 };
                return next;
            }
        }
        // 飞行无需观察地面绕行；相邻且可直接抵达的目标也无需展开搜索。
        if ((mode == TravelMode.Aircraft || Distance(originX, originY, targetX, targetY) == 1))
            foreach (var (dx, dy) in Directions)
            {
                var x = originX + dx;
                var y = originY + dy;
                if (Distance(x, y, targetX, targetY) < Distance(originX, originY, targetX, targetY)
                    && CanTraverseStep(originX, originY, x, y, mode, race)
                    && (mode == TravelMode.Aircraft || Current.Tiles[Index(x, y)].FireTicks == 0)
                    && !goal.NavigationVisited.Contains(Index(x, y)))
                    return Index(x, y);
            }

        // 取得新的最短目标距离后清除旧绕路；暂时离开目标方向时仍记住已走地点，防止反复折返。
        if (!goal.NavigationVisited.Contains(start) && goal.NavigationVisited.Length >= 256) return -1;

        // 导航仅使用六格可见地形，桥梁轴向与实际移动及军队寻路共用规则，避免预览可走却无法通行。
        if (_localMoveVisited.Length != Current.Tiles.Count)
            _localMoveVisited = new int[Current.Tiles.Count];
        if (_localMoveCosts.Length != Current.Tiles.Count)
            _localMoveCosts = new double[Current.Tiles.Count];
        if (_localMoveSearch == int.MaxValue)
        {
            Array.Clear(_localMoveVisited);
            _localMoveSearch = 0;
        }

        var search = ++_localMoveSearch;
        // 单次导航内的记忆标记和进入成本固定；栈上缓冲只覆盖六格视野，不跨居民或世界步骤复用。
        const int diameter = 13;
        Span<byte> flags = stackalloc byte[diameter * diameter];
        flags.Clear();
        Span<double> entryCosts = stackalloc double[diameter * diameter];
        Span<int> routeParents = stackalloc int[diameter * diameter];
        foreach (var index in person.Agent.Value.FamiliarTiles)
        {
            var localX = index % width - originX + 6;
            var localY = index / width - originY + 6;
            if ((uint)localX < diameter && (uint)localY < diameter)
                flags[localY * diameter + localX] |= 1;
        }
        foreach (var index in goal.NavigationVisited)
        {
            var localX = index % width - originX + 6;
            var localY = index / width - originY + 6;
            if ((uint)localX < diameter && (uint)localY < diameter)
                flags[localY * diameter + localX] |= 2;
        }
        // 可见目标先试两条实际可走的折线路径，取得成本上界；只剪去不可能优于它们的分支。
        // 视野内按实际成本加剩余距离下界出队，仍依据实际耗时选择最短路。
        var targetVisible = Distance(originX, originY, targetX, targetY) <= 6;
        var minimumTargetDistance = Math.Max(0, Distance(originX, originY, targetX, targetY) - 6);
        var upperCost = targetVisible
            ? Math.Min(DirectCost(true, flags, entryCosts), DirectCost(false, flags, entryCosts))
            : double.PositiveInfinity;

        double DirectCost(bool horizontalFirst, Span<byte> routeFlags, Span<double> routeCosts)
        {
            var x = originX;
            var y = originY;
            var cost = 0d;
            while (x != targetX || y != targetY)
            {
                var nextX = x;
                var nextY = y;
                if (x != targetX && (horizontalFirst || y == targetY)) nextX += Math.Sign(targetX - x);
                else nextY += Math.Sign(targetY - y);
                var index = Index(nextX, nextY);
                var localIndex = (nextY - originY + 6) * diameter + nextX - originX + 6;
                if ((routeFlags[localIndex] & 2) != 0 || Current.Tiles[index].FireTicks > 0
                    || !CanTraverseStep(x, y, nextX, nextY, mode, race))
                    return double.PositiveInfinity;
                if ((routeFlags[localIndex] & 8) == 0)
                {
                    routeCosts[localIndex] = AgentMoveDuration(person, index) * ((routeFlags[localIndex] & 1) != 0 ? .92 : 1);
                    routeFlags[localIndex] |= 8;
                }
                cost += routeCosts[localIndex];
                x = nextX;
                y = nextY;
            }
            return cost;
        }
        _localMoveVisited[start] = search;
        _localMoveCosts[start] = 0;
        _localRoutes.Clear();
        _localRoutes.Enqueue((start, -1, 0), (0, start));
        var bestStep = -1;
        var bestDestination = -1;
        var bestScore = double.PositiveInfinity;
        while (_localRoutes.TryDequeue(out var current, out _))
        {
            if (current.Cost > _localMoveCosts[current.Index])
                continue;
            if (current.Index == target)
            {
                goal = PlanAgentRoute(goal, start, target, originX, originY, width, mode, routeParents);
                return current.First;
            }
            var currentX = current.Index % width;
            var currentY = current.Index / width;
            var fromTile = Current.Tiles[current.Index].Value;
            flags[(currentY - originY + 6) * diameter + currentX - originX + 6] |= 4;
            if (current.First >= 0)
            {
                // 视野外只估计剩余距离，不读取远方地形；熟悉程度是轻微偏好，不能抵消明显绕远。
                var score = Distance(currentX, currentY, targetX, targetY) * 2
                    + current.Cost;
                if (score < bestScore || score == bestScore && current.First == bestStep
                    && Distance(originX, originY, currentX, currentY)
                        > Distance(originX, originY, bestDestination % width, bestDestination / width))
                {
                    bestStep = current.First;
                    bestDestination = current.Index;
                    bestScore = score;
                }
            }
            foreach (var (dx, dy) in Directions)
            {
                var x = currentX + dx;
                var y = currentY + dy;
                if (Distance(originX, originY, x, y) > 6)
                    continue;
                var localIndex = (y - originY + 6) * diameter + x - originX + 6;
                if ((flags[localIndex] & 6) != 0 || (uint)x >= width || (uint)y >= height)
                    continue;
                var index = y * width + x;
                var toTile = Current.Tiles[index].Value;
                if (!CanTraverseAdjacentTiles(fromTile, toTile, dy == 0, mode, race)
                    || mode != TravelMode.Aircraft && toTile.FireTicks > 0)
                    continue;
                var first = current.First < 0 ? index : current.First;
                if ((flags[localIndex] & 8) == 0)
                {
                    entryCosts[localIndex] = AgentMoveDuration(person, index) * ((flags[localIndex] & 1) != 0 ? .92 : 1);
                    flags[localIndex] |= 8;
                }
                var cost = current.Cost + entryCosts[localIndex];
                var targetDistance = Distance(x, y, targetX, targetY);
                if (targetVisible && cost + targetDistance * .92 > upperCost + .000000001)
                    continue;
                // 视野外目标仍比较原有的「已走成本 + 剩余距离 × 2」；后续每步至少花费 .92。
                // 三角不等式给出下界：cost + .92 × 当前剩余距离 + 1.08 × 视野内最小剩余距离。
                if (!targetVisible && cost + targetDistance * .92 + minimumTargetDistance * 1.08 > bestScore + .000000001)
                    continue;
                if (_localMoveVisited[index] == search && _localMoveCosts[index] <= cost)
                    continue;
                _localMoveVisited[index] = search;
                _localMoveCosts[index] = cost;
                routeParents[localIndex] = current.Index;
                // 视野内使用不超过实际最小步时的估计引导搜索，仍保证最短实际耗时。
                var estimated = targetVisible ? targetDistance * .92 : 0;
                _localRoutes.Enqueue((index, first, cost), (cost + estimated, index));
            }
        }

        if (goal.NavigationWithoutProgress >= 64) return -1;
        if (bestDestination >= 0)
            goal = PlanAgentRoute(goal, start, bestDestination, originX, originY, width, mode, routeParents);
        return bestStep;
    }

    private static AgentGoal PlanAgentRoute(AgentGoal goal, int start, int destination,
        int originX, int originY, int width, TravelMode mode, ReadOnlySpan<int> parents)
    {
        Span<int> reverse = stackalloc int[85];
        var count = 0;
        while (destination != start && count < reverse.Length)
        {
            reverse[count++] = destination;
            var local = (destination / width - originY + 6) * 13 + destination % width - originX + 6;
            destination = parents[local];
        }
        var route = new int[Math.Min(count, 6) + 1];
        route[0] = start;
        for (var position = 1; position < route.Length; position++) route[position] = reverse[count - position];
        return goal with
        {
            NavigationRoute = System.Runtime.InteropServices.ImmutableCollectionsMarshal.AsImmutableArray(route),
            NavigationRouteOffset = 2,
            NavigationRouteMode = mode,
        };
    }

    private double AgentFoodPolicyMultiplier(ResidentCursor person)
    {
        if (!_settlements.TryGetValue(person.SettlementId, out var home))
            return 1;
        if (Distance(person.X, person.Y, home.X, home.Y) <= 3)
            return GetPolicyProductionMultiplier(home.Id);
        AgentFact? instruction = null;
        foreach (var fact in person.Agent.Memory)
            if (fact.Kind == AgentFactKind.Policy && fact.SubjectId == home.Id && fact.ReliabilityAt(Current.Tick) >= 0.5
                && (instruction is null || fact.ObservedTick > instruction.ObservedTick))
                instruction = fact;
        return instruction?.Value == (int)PolicyKind.FoodSecurity ? 1.25 : 1;
    }

    private static AgentFact? LatestAgentFact(IReadOnlyList<AgentFact> memory, AgentFactKind kind, int subjectId)
    {
        AgentFact? latest = null;
        foreach (var fact in memory)
            if (fact.Kind == kind && fact.SubjectId == subjectId
                                  && (latest is null || fact.ObservedTick > latest.ObservedTick))
                latest = fact;
        return latest;
    }

    /// <summary>有限导航成本按耗时、地格索引排序，直接比较两个字段。</summary>
    private sealed class LocalRoutePriorityComparer : IComparer<(double Cost, int Index)>
    {
        public static readonly LocalRoutePriorityComparer Instance = new();

        public int Compare((double Cost, int Index) left, (double Cost, int Index) right) =>
            left.Cost < right.Cost ? -1 : left.Cost > right.Cost ? 1 : left.Index.CompareTo(right.Index);
    }

    /// <summary>参与居民行动决策评分的候选目标。</summary>
    /// <param name="Kind">候选行动目标类别。</param>
    /// <param name="X">候选目标的横向地格坐标。</param>
    /// <param name="Y">候选目标的纵向地格坐标。</param>
    /// <param name="Score">候选目标的综合评分。</param>
    /// <param name="Reason">候选目标的实际评分理由。</param>
    /// <param name="Evidence">作为评分依据的居民记忆，空值表示不依赖信息记录。</param>
    /// <param name="SettlementId">候选目标关联的聚落 ID。</param>
    /// <param name="EntityId">候选目标对象的编号，含义由目标类别决定。</param>
    private readonly record struct GoalChoice(
        AgentGoalKind Kind,
        int X,
        int Y,
        double Score,
        string Reason,
        AgentFact? Evidence = null,
        int SettlementId = 0,
        int EntityId = 0);

    /// <summary>居民可见范围内的可达地格缓存。</summary>
    private sealed class VisibleAccessCache
    {
        private const int Slots = 1024, Cells = 85;
        private readonly int[] _keys = new int[Slots], _counts = new int[Slots], _indices = new int[Slots * Cells];
        private readonly long[] _revisions = new long[Slots];
        private int _width;

        public int Slot(int key, int width)
        {
            if (_width != width)
            {
                Array.Clear(_counts);
                _width = width;
            }

            return (int)(unchecked((uint)key * 2654435761u) >> 22);
        }

        public bool TryMark(int slot, int key, long revision, int[] visited, int search)
        {
            if (_counts[slot] == 0 || _keys[slot] != key ||
                _revisions[slot] != revision)
                return false;
            var first = slot * Cells;
            for (var i = 0; i < _counts[slot]; i++)
                visited[_indices[first + i]] = search;
            return true;
        }

        public void Store(int slot, int key, long revision, (int Index, int First, int Depth)[] queue, int count)
        {
            _keys[slot] = key;
            _revisions[slot] = revision;
            _counts[slot] = count;
            var first = slot * Cells;
            for (var i = 0; i < count; i++)
                _indices[first + i] = queue[i].Index;
        }
    }
}
