using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using SeWZC.WorldBox.Core.Runtime;

namespace SeWZC.WorldBox.Core;

public sealed partial class WorldEngine
{
    private static readonly (int X, int Y, int Distance)[] VisibleResourceOffsets = CreateVisibleResourceOffsets();

    private readonly List<GoalChoice> _goalChoices = [];

    // 缓冲区按可见半径内的地格数分配；可见区域内绕路超过六步时仍须容纳全部地格。
    private readonly (int Index, int First, int Depth)[] _localMoveQueue = new (int, int, int)[85];

    private readonly PriorityQueue<(int Index, int First, double Cost), (double Cost, int Index)> _localRoutes =
        new(LocalRoutePriorityComparer.Instance);

    private int _localMoveSearch;
    private int[] _localMoveVisited = [];
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
            var value = unchecked((uint)(person.Id * 374761393 + Seed * 668265263 + salt));
            value = (value ^ (value >> 13)) * 1274126177;
            return 0.2 + value % 601 / 1000d;
        }

        var agent = person.Agent with
        {
            Initialized = true,
            NextThinkTick = SimulationTick,
            Personality = new PersonalityProfile
            {
                Courage = person.Trait == "勇敢" ? 0.9 : Trait(17),
                Diligence = person.Trait == "勤劳" ? 0.9 : Trait(41),
                Sociability = person.Trait == "温和" ? 0.9 : Trait(83),
                Ambition = person.Trait == "好奇" ? 0.9 : Trait(131),
            },
        };
        var culture = person.CultureId;
        if (_settlements.TryGetValue(person.SettlementId, out var home))
        {
            if (person.CultureId == 0)
                culture = home.Value.CultureId;
            agent = agent.Remember(MakeAgentFact(person, AgentFactKind.SettlementLocation,
                home.Value.Id, home.Value.X, home.Value.Y, home.Value.NationId, $"我的家园：{home.Value.Name}"), person.SettlementId);
        }

        person.Replace(person.Value with { Agent = agent, CultureId = culture, FromX = person.X, FromY = person.Y });
    }

    private void UpdateAgentNeedsAndActions()
    {
        try
        {
            BeginLocalWorkQueries();
            // 补给先按实际位置交付，再用一次纯转换结算身体状态和日常需求。
            UpdateResidents();
            // 普通观察按居民序号错峰；非军队居民所在格起火时立即观察，实际取水和劳动自行核实目标。
            var observationInterval = Math.Max(32, (Residents.Count + 31) / 32);
            var observerIndex = 0;
            foreach (var person in Residents)
            {
                var observe = (SimulationTick + observerIndex++) % observationInterval == 0;
                if (person.Health <= 0 || !_settlements.TryGetValue(person.SettlementId, out var home))
                    continue;
                if (person.ArmyId != 0)
                {
                    if (observe)
                        ObserveAgentEnvironment(person);
                    continue;
                }

                var arrivedHome = Distance(person.X, person.Y, home.Value.X, home.Value.Y) <= 1
                                  && Walkable(person.X, person.Y, person.Race)
                                  && SimulationTick - person.MoveStartedTick >= person.MoveDurationTicks;
                if (arrivedHome)
                {
                    if ((SimulationTick + person.Id) % 12 == 0)
                        DeliverLocalDiscoveries(person, home);
                }

                var danger = Tiles[Index(person.X, person.Y)].Value.FireTicks > 0;
                var arrived = SimulationTick - person.MoveStartedTick >= person.MoveDurationTicks;
                if (observe || danger)
                    ObserveAgentEnvironment(person);
                // 非危险中的在途居民先完成当前移动区段；身体危机请求保留，到场后再评估新工作。
                if (!arrived && !danger)
                    continue;
                var directed = person.Agent.Goal.PlayerDirected && SimulationTick < person.Agent.Goal.ReviewTick;
                var emergency = danger || ((directed || SimulationTick >= person.Agent.Goal.ReviewTick)
                                           && ((person.Hunger > 60 && person.Inventory.Food < .05)
                                               || (Rules.Thirst && person.Thirst > 80 &&
                                                   person.Inventory.Water < .025)));
                var survival = danger || (Rules.Hunger && person.Hunger > 60 && person.Inventory.Food < .05)
                                      || (Rules.Thirst && person.Thirst > 80 && person.Inventory.Water < .025);
                if (FollowDailyRoutine(person, home, survival))
                    continue;
                if ((!directed && SimulationTick >= person.Agent.NextThinkTick
                               && (SimulationTick + person.Id) % 4 == 0) || emergency)
                    ChooseAgentGoal(person, home, emergency && directed);
                if (person.Agent.Goal.Kind == AgentGoalKind.Idle)
                    continue;
                // 逻辑位置记录已提交的目的地，劳动和递送必须等待实际到达。
                if (SimulationTick - person.MoveStartedTick < person.MoveDurationTicks ||
                    person.FrozenUntilTick > SimulationTick)
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
            foreach (var fact in person.Agent.Memory)
                if (fact.Kind == AgentFactKind.SettlementLocation && fact.Value != person.NationId
                                                                  && SimulationTick - fact.ObservedTick <
                                                                  10 * SimulationTime.TicksPerYear)
                {
                    knowsForeignTown = true;
                    break;
                }

            if (!knowsForeignTown)
                return 6;
        }

        return person.Profession is Profession.Lumberjack or Profession.Miner
            ? 4
            : Math.Clamp(
                .8 + Distance(person.X, person.Y, person.Agent.Goal.TargetX, person.Agent.Goal.TargetY) *
                FoodUse(person) * 6 / SimulationTime.TicksPerDay, .8, 8);
    }

    private void TransferPersonalProduction(ResidentCursor person, StateReference<Settlement> home)
    {
        home.Replace(home.Value.WithResources(UnloadAtHome(person, home.Value.Id, home.Value.Resources)));
    }

    private ResourceStock UnloadAtHome(ResidentCursor person, int settlementId, in ResourceStock warehouse)
    {
        // 任务货物在到达约定目的地前仍由居民携带，避免提前入库。
        if (person.Agent.DestinationSettlementId != 0
            && person.Agent.Goal.Kind is AgentGoalKind.DeliverMessage or AgentGoalKind.Trade
                or AgentGoalKind.Petition)
            return warehouse;
        // 家乡邻格也可能是工作点，经过时不能卸掉刚领取的生产原料。
        var assigned = person.Agent.Goal.Kind == AgentGoalKind.Work
            ? FindBuilding(person.Agent.Goal.TargetEntityId)
            : null;
        var recipe = assigned?.Value is { IsCompleted: true, Enabled: true } ? ProductionRules.For(assigned.Value.Kind) : null;
        if (assigned is not null && assigned.Value.SettlementId == settlementId
                                 && (recipe is not null || IsHusbandry(assigned.Value.Kind) ||
                                     ExpansionSupply(assigned.Value.Kind) is not null || assigned.Value.Health < 50))
            return warehouse;
        var unloaded = InventoryTransfer.Unload(person.Inventory, warehouse,
            WaterReserve(person), TravelReserve(person), person.Profession);
        var agent = person.Agent;
        if (agent.Goal.Kind == AgentGoalKind.ReturnHome && agent.DestinationSettlementId == 0 &&
            agent.MissionOriginSettlementId != 0)
            agent = agent with { MissionOriginSettlementId = 0 };
        if (unloaded.Inventory != person.Inventory || person.TravelMode != TravelMode.Foot ||
            !ReferenceEquals(agent, person.Agent))
        {
            person.Inventory = unloaded.Inventory;
            person.TravelMode = TravelMode.Foot;
            person.Agent = agent;
        }

        return unloaded.Warehouse;
    }

    private bool ProductiveGoalContinues(ResidentCursor person, StateReference<Settlement> home)
    {
        var goal = person.Agent.Goal;
        if (Distance(person.X, person.Y, home.Value.X, home.Value.Y) <= 1 && !goal.PlayerDirected
                                                              && ((goal.Kind == AgentGoalKind.Gather &&
                                                                   home.Value.Resources.Food >=
                                                                   ProductionStockTarget(home, ResourceKind.Food) &&
                                                                   person.Inventory.Food >= .3)
                                                                  || (goal.Kind == AgentGoalKind.Work &&
                                                                      goal.TargetEntityId == 0 &&
                                                                      !LocalMaterialsNeeded(person, home))))
            return false;
        if (person.Profession == Profession.Builder && SettlementNeedsClaimArea(home)
                                                    && (FindBuilding(goal.TargetEntityId) is not { } assigned ||
                                                        (assigned.Value.IsCompleted && !assigned.Value.IsUpgrading)))
            return false;
        if (person.Hunger >= 60 || person.Thirst >= 60 || person.Agent.Fatigue >= 60
            || person.Inventory.Food >= Math.Max(4, TravelReserve(person) + 1)
            || (Rules.Thirst && person.Inventory.Water < LocalWaterUse(person) &&
                GetDailyWaterCapacity(person.X, person.Y) < LocalWaterUse(person))
            || person.Inventory.Wood + person.Inventory.Stone + person.Inventory.Ore >= 3)
            return false;
        if (goal.NavigationTarget >= 0 && SimulationTick < goal.NavigationRetryTick)
            return false;
        if (goal.Kind == AgentGoalKind.Work && person.Profession is Profession.Physician or Profession.Firefighter
                                                or Profession.Archivist or Profession.Surveyor or Profession.Gardener
                                            && FindBuilding(goal.TargetEntityId) is { } current &&
                                            PreferredExpansionJob(current.Value.Kind) != person.Profession
                                            && ExpansionJobHasNearbyWork(person))
            return false;
        if (Rules.Hunger && Distance(person.X, person.Y, home.Value.X, home.Value.Y) > 1 && person.Hunger < 20
            && person.Inventory.Food < FoodUse(person) * (Distance(person.X, person.Y, home.Value.X, home.Value.Y) * 4 + 12)
            / SimulationTime.TicksPerDay)
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
                       NaturalPlantHarvestEfficiency(Tiles[Index(goal.TargetX, goal.TargetY)],
                           goal.Kind == AgentGoalKind.Work && person.Profession == Profession.Lumberjack) >= .25);
        }

        if (goal.Kind is AgentGoalKind.Work or AgentGoalKind.Study or AgentGoalKind.TrainMagic)
        {
            return FindBuilding(goal.TargetEntityId) is { } building && building.Value.SettlementId == home.Value.Id &&
                   BuildingHasWork(building.Value, person);
        }

        return false;
    }

    private bool FoodSupplyNeeded(ResidentCursor person, StateReference<Settlement> home)
    {
        if (person.Inventory.Food < .3 || person.Hunger > 20)
            return true;
        var fact = LatestAgentFact(person.Agent.Memory, AgentFactKind.FoodSupply, home.Value.Id);
        var known = Distance(person.X, person.Y, home.Value.X, home.Value.Y) <= 1 ? home.Value.Resources.Food
            : fact is not null && fact.ReliabilityAt(SimulationTick) >= .5 ? fact.Value : 0;
        return known < ProductionStockTarget(home, ResourceKind.Food);
    }

    private bool LocalMaterialsNeeded(ResidentCursor person, StateReference<Settlement> home)
    {
        // 远处居民须完成返乡后再依据实际仓库供给决策，避免获得远程库存知识。
        if (Distance(person.X, person.Y, home.Value.X, home.Value.Y) > 1)
            return true;
        var reserve = LocalDevelopmentReserve(home);
        if (person.Profession == Profession.Lumberjack)
            return home.Value.Resources.Wood < Math.Max(80, reserve.Wood);
        if (person.Profession != Profession.Miner)
        {
            return home.Value.Resources.Wood < Math.Max(80, reserve.Wood)
                   || home.Value.Resources.Stone < Math.Max(80, reserve.Stone) ||
                   home.Value.Resources.Ore < Math.Max(80, reserve.Ore);
        }

        return home.Value.Resources.Stone < Math.Max(80, reserve.Stone) || home.Value.Resources.Ore <
                                                                  Math.Max(80, reserve.Ore)
                                                                  || (HasResearch(home.Value.Id,
                                                                          Advancement.Industry) &&
                                                                      home.Value.Resources.Coal < 16)
                                                                  || (HasResearch(home.Value.Id,
                                                                          Advancement.Electrification) &&
                                                                      home.Value.Resources.Oil < 16)
                                                                  || (HasResearch(home.Value.Id,
                                                                          Advancement.AdvancedComputing) &&
                                                                      home.Value.Resources.RareEarth < 16);
    }

    // 初次紧急判断仍立即执行；后续复评对齐个人四 tick 错峰，首次对齐只需三至六 tick。
    private int GoalReviewInterval(ResidentCursor person)
    {
        if (!(person.Hunger > 60 && person.Inventory.Food < .05)
            && !(Rules.Thirst && person.Thirst > 80 && person.Inventory.Water < .025))
            return 2 * SimulationTime.TicksPerDay;
        var earliest = SimulationTick + 3;
        var phase = (earliest + person.Id) % 4;
        return 3 + (int)((4 - phase) % 4);
    }

    private void DeferGoalReview(ResidentCursor person, int? interval = null)
    {
        var next = SimulationTick + (interval ?? GoalReviewInterval(person));
        person.Agent = person.Agent with { Goal = person.Agent.Goal with { ReviewTick = next }, NextThinkTick = next };
    }

    private void ChooseAgentGoal(ResidentCursor person, StateReference<Settlement> home, bool interrupted)
    {
        if (person.Profession == Profession.Miner && Distance(person.X, person.Y, home.Value.X, home.Value.Y) <= 1)
        {
            var reserve = LocalDevelopmentReserve(home);
            var stoneDeficit = Math.Max(0, Math.Max(80, reserve.Stone) - home.Value.Resources.Stone);
            var oreDeficit = Math.Max(0, Math.Max(30, reserve.Ore) - home.Value.Resources.Ore);
            person.Agent = person.Agent with
            {
                MaterialPriority = home.Value.Resources.Ore < reserve.Ore
                    ? ResourceKind.Ore
                    : stoneDeficit > oreDeficit
                        ? ResourceKind.Stone
                        : HasResearch(home.Value.Id, Advancement.Industry) && home.Value.Resources.Coal < 8
                            ? ResourceKind.Coal
                            : HasResearch(home.Value.Id, Advancement.Electrification) && home.Value.Resources.Oil < 8
                                ? ResourceKind.Oil
                                : HasResearch(home.Value.Id, Advancement.AdvancedComputing) && home.Value.Resources.RareEarth < 8
                                    ? ResourceKind.RareEarth
                                    : home.Value.Resources.Ore < 30
                                        ? ResourceKind.Ore
                                        : home.Value.Resources.Stone < 30
                                            ? ResourceKind.Stone
                                            : null,
            };
        }

        var personality = person.Agent.Personality;
        var choices = _goalChoices;
        choices.Clear();
        var foodFact = LatestAgentFact(person.Agent.Memory, AgentFactKind.FoodSupply, home.Value.Id);
        AgentFact? dangerFact = null;
        foreach (var fact in person.Agent.Memory)
            if (fact.Kind == AgentFactKind.Danger && fact.Value > 0 && fact.ReliabilityAt(SimulationTick) > 0.25
                && SimulationTick - fact.ObservedTick < 24 && Distance(person.X, person.Y, fact.X, fact.Y) <= 5
                && (dangerFact is null || fact.ObservedTick > dangerFact.ObservedTick))
                dangerFact = fact;
        if (dangerFact is not null || Tiles[Index(person.X, person.Y)].Value.FireTicks > 0)
        {
            var safe = FindSafeVisibleSite(person, home, dangerFact);
            if (safe >= 0)
            {
                choices.Add(new GoalChoice(AgentGoalKind.Flee, safe % Width, safe / Width,
                    (180 - personality.Courage * 30) *
                    (dangerFact is null ? 1 : Math.Max(0.6, dangerFact.ReliabilityAt(SimulationTick))),
                    dangerFact?.OriginResidentId == person.Id ? "亲眼见到附近危险，先离开危险区域" : "可信的近时报告指出附近危险，先离开核实",
                    dangerFact));
            }
        }

        if (choices.Count == 0 && !interrupted && person.Agent.Goal.Kind == AgentGoalKind.Rest
            && RecoveryGoalContinues(person) && person.Hunger < 60 && person.Thirst < 60)
        {
            DeferGoalReview(person);
            return;
        }

        AddRecoveryChoice(person, home, choices);

        // 正在实地解决缺粮或补水时继续当前劳动；现场枯竭、疲劳和新的身体危险仍会提前复评。
        if (choices.Count == 0 && !interrupted && ProvisionGoalContinues(person))
        {
            DeferGoalReview(person, SimulationTime.TicksPerDay);
            return;
        }

        if (choices.Count == 0 && !interrupted && (
                ProductiveGoalContinues(person, home)
                || (person.Agent.Goal.Kind == AgentGoalKind.Rest && RecoveryGoalContinues(person)
                                                                 && person.Hunger < 60 && person.Thirst < 60)
                || (person.Agent.Goal.Kind == AgentGoalKind.ReturnHome && Distance(person.X, person.Y, home.Value.X, home.Value.Y) >
                                                                       1
                                                                       && SimulationTick < person.Agent.Goal.StartedTick +
                                                                       10 * SimulationTime.TicksPerDay &&
                                                                       SimulationTick >= person.Agent.Goal
                                                                           .NavigationRetryTick
                                                                       && person.Hunger < 60 && person.Thirst < 60)))
        {
            DeferGoalReview(person);
            return;
        }

        if (person.Agent.Goal.Kind is AgentGoalKind.ClaimLand or AgentGoalKind.FetchWater or AgentGoalKind.Hunt
                or AgentGoalKind.Fish
            && choices.Count == 0 && SimulationTick - person.Agent.Goal.StartedTick < 2 * SimulationTime.TicksPerDay &&
            person.Hunger < 20 &&
            person.Thirst < 60
            && person.Agent.Fatigue < 60 && SimulationTick >= person.Agent.Goal.NavigationRetryTick
            && (person.Agent.Goal.Kind is not (AgentGoalKind.Hunt or AgentGoalKind.Fish) ||
                WildlifeGoalProductive(person))
            && (person.Agent.Goal.Kind != AgentGoalKind.ClaimLand ||
                CanClaimTile(home, Index(person.Agent.Goal.TargetX, person.Agent.Goal.TargetY), person.Race))
            && (person.Agent.Goal.Kind != AgentGoalKind.FetchWater ||
                (person.Agent.Goal.TargetEntityId > 0 && person.Agent.Goal.TargetEntityId <= Tiles.Count &&
                 GetDailyWaterCapacity((person.Agent.Goal.TargetEntityId - 1) % Width,
                     (person.Agent.Goal.TargetEntityId - 1) / Width) >= .1))
            && person.Inventory.Food < TravelReserve(person) + 2
            && (person.Agent.Goal.Kind != AgentGoalKind.FetchWater ||
                person.Inventory.Water < WaterCollectionTarget(person)))
        {
            DeferGoalReview(person);
            return;
        }

        if (person.Agent.Goal.Kind == AgentGoalKind.Migrate && choices.Count == 0 &&
            SimulationTick - person.Agent.Goal.StartedTick < 3 * SimulationTime.TicksPerYear
            && person.Hunger < 20 && person.Thirst < 60)
        {
            DeferGoalReview(person);
            return;
        }

        if (person.Agent.Goal.Kind == AgentGoalKind.Work && choices.Count == 0 && person.Hunger < 65 &&
            person.Agent.Fatigue < 60
            && (person.Thirst < 40 || person.Inventory.Water >= .3)
            && FindBuilding(person.Agent.Goal.TargetEntityId) is { } factory && factory.Value.SettlementId == home.Value.Id
            && ProductionRules.For(factory.Value.Kind) is { } recipe && CanProduce(factory.Value, person, recipe)
            && HasProductionInputs(person.Inventory, recipe))
        {
            DeferGoalReview(person);
            return;
        }

        if (person.Agent.Goal.Kind == AgentGoalKind.Explore && choices.Count == 0 && person.Hunger < 65 &&
            person.Agent.Fatigue < 60
            && (person.Thirst < 40 || person.Inventory.Water >= .3)
            && Distance(person.X, person.Y, person.Agent.Goal.TargetX, person.Agent.Goal.TargetY) > 1 &&
            SimulationTick - person.Agent.Goal.StartedTick < 2 * SimulationTime.TicksPerDay)
        {
            DeferGoalReview(person);
            return;
        }

        var activeMission =
            person.Agent.DestinationSettlementId != 0 && SimulationTick - person.Agent.MissionStartedTick <
                                                      3 * SimulationTime.TicksPerYear
                                                      && person.Agent.Goal.Kind is AgentGoalKind.DeliverMessage
                                                          or AgentGoalKind.Trade
                                                          or AgentGoalKind.Petition;
        if (activeMission && choices.Count == 0 && !(person.Hunger > 60 && person.Inventory.Food < 0.05)
            && !(person.Thirst > 80 && person.Inventory.Water < .025))
        {
            // 任务货物和口粮要随本轮任务继续携带，不能按普通工作背包已满而中断旅程。
            DeferGoalReview(person);
            return;
        }

        if (person.Agent.Goal.Kind == AgentGoalKind.ReturnHome && person.Agent.MissionOriginSettlementId != 0
                                                               && Distance(person.X, person.Y, home.Value.X, home.Value.Y) > 1 &&
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

        var homeFoodKnown = Distance(person.X, person.Y, home.Value.X, home.Value.Y) <= 1;
        if (person.Inventory.Food < 0.3 &&
            (homeFoodKnown
                ? home.Value.Resources.Food >= FoodUse(person)
                : foodFact is null || foodFact.Value > 0 || foodFact.ReliabilityAt(SimulationTick) < 0.5))
        {
            choices.Add(new GoalChoice(AgentGoalKind.Eat, home.Value.X, home.Value.Y, 65 + person.Hunger,
                foodFact is null ? "随身口粮不足，返回家园查看粮仓" : $"口粮不足；上次获知家乡有 {foodFact.Value:0.0} 份粮食", foodFact, home.Value.Id));
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
            choices.Add(new GoalChoice(AgentGoalKind.ReturnHome, home.Value.X, home.Value.Y, 75 + personality.Diligence * 12,
                "背包已有产物，亲自运回家园入库", null, home.Value.Id));
        }

        if (person.Agent.MissionOriginSettlementId != 0 && person.Agent.DestinationSettlementId == 0
                                                        && Distance(person.X, person.Y, home.Value.X, home.Value.Y) > 1)
        {
            choices.Add(new GoalChoice(AgentGoalKind.ReturnHome, home.Value.X, home.Value.Y, 70,
                "递送已结束或中止，带着剩余物资亲自返乡", null, home.Value.Id));
        }

        if (person.Agent.Fatigue > 35)
        {
            choices.Add(new GoalChoice(AgentGoalKind.Rest, home.Value.X, home.Value.Y, person.Agent.Fatigue * 1.15,
                $"疲劳达到 {person.Agent.Fatigue:0}，回家休息", null, home.Value.Id));
        }

        var depositSite = LocalMaterialsNeeded(person, home) ? VisibleDepositSite(person) : -1;
        if (person.Age >= 14 && depositSite >= 0)
        {
            choices.Add(new GoalChoice(AgentGoalKind.Work, depositSite % Width, depositSite / Width,
                65, "掌握勘探知识后在眼前发现矿藏，实地开采并运回"));
        }

        var foodSite = FoodSupplyNeeded(person, home) ? FindVisibleResourceSite(person, Profession.Farmer) : -1;
        if (foodSite >= 0)
        {
            var score = person.Profession == Profession.Farmer ? 36 + personality.Diligence * 13 : 12;
            score += person.Hunger * (person.Inventory.Food < 0.3 ? 1.1 : 0.1);
            score *= AgentFoodPolicyMultiplier(person.Agent, person.SettlementId, person.X, person.Y);
            if (foodFact is { Value: < 12 })
                score += 18 * foodFact.ReliabilityAt(SimulationTick);
            if (Rules.Hunger && person.Inventory.Food < .3 && person.Hunger >= 20)
                score = Math.Max(score, 100 + person.Hunger);
            choices.Add(new GoalChoice(AgentGoalKind.Gather, foodSite % Width, foodSite / Width, score,
                foodFact is { Value: < 12 } ? "已知粮情显示家乡粮少，在可见的可食土地采集" : "眼前土地能产食物，采集后随身携带", foodFact));
        }

        if (Rules.Hunger && person.Hunger > 60 && person.Inventory.Food < .05
            && choices.Any(choice =>
                choice.Kind is AgentGoalKind.Gather or AgentGoalKind.Hunt or AgentGoalKind.Fish or AgentGoalKind.Eat))
        {
            CommitAgentChoice(person, home, choices, interrupted, activeMission);
            return;
        }

        if (Rules.Hunger && person.Age >= 14 && person.Hunger > 20 && person.Inventory.Food < .3
            && !choices.Any(choice =>
                choice.Kind is AgentGoalKind.Gather or AgentGoalKind.Hunt or AgentGoalKind.Fish or AgentGoalKind.Eat))
        {
            AddFoodExplorationChoice(person, choices);
            if (person.Hunger > 60 && choices.Any(choice => choice.Kind == AgentGoalKind.Explore))
            {
                CommitAgentChoice(person, home, choices, interrupted, activeMission);
                return;
            }
        }

        int? materialSite = null;
        if (person.Age >= 14 && person.Profession is Profession.Lumberjack or Profession.Miner
                             && LocalMaterialsNeeded(person, home))
        {
            var site = FindVisibleResourceSite(person, person.Profession);
            materialSite = site;
            if (site >= 0)
            {
                choices.Add(new GoalChoice(AgentGoalKind.Work, site % Width, site / Width,
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
            var kind = work.Value.Kind == BuildingKind.Academy && person.Profession == Profession.Scholar
                ? AgentGoalKind.Study
                : work.Value.Kind is BuildingKind.ArcaneSanctum or BuildingKind.SacredGrove &&
                  person.Profession is Profession.Mage or Profession.Battlemage
                    ? AgentGoalKind.TrainMagic
                    : AgentGoalKind.Work;
            choices.Add(new GoalChoice(kind, work.Value.X, work.Value.Y, 42 + personality.Diligence * 12
                                                                + (person.Profession == Profession.Farmer &&
                                                                   foodFact is { Value: < 12 }
                                                                    ? 18 * foodFact.ReliabilityAt(SimulationTick)
                                                                    : 0),
                kind == AgentGoalKind.Study ? "附近有可参与的研究设施，前往学习" :
                kind == AgentGoalKind.TrainMagic ? "附近有可训练的魔法设施" : "附近有实际施工或生产工作", EntityId: work.Value.Id));
        }

        if (person.Age >= 14 && person.Profession is Profession.Lumberjack or Profession.Miner
                             && (person.Profession == Profession.Lumberjack
                                 ? (materialSite ?? FindVisibleResourceSite(person, Profession.Lumberjack)) < 0
                                 : depositSite < 0 &&
                                   (materialSite ?? FindVisibleResourceSite(person, Profession.Miner)) < 0)
                             && ((Distance(person.X, person.Y, home.Value.X, home.Value.Y) <= 1 &&
                                  (person.Profession == Profession.Lumberjack
                                      ? home.Value.Resources.Wood < 60
                                      : person.Agent.MaterialPriority is not null ||
                                        (HasResearch(home.Value.Id, Advancement.Industry) && home.Value.Resources.Coal < 8) ||
                                        (HasResearch(home.Value.Id, Advancement.Electrification) && home.Value.Resources.Oil < 8)
                                        || (HasResearch(home.Value.Id, Advancement.AdvancedComputing) &&
                                            home.Value.Resources.RareEarth < 8))) ||
                                 person.Agent.Goal.Kind == AgentGoalKind.Explore))
        {
            var offsets = new (int X, int Y)[] { (6, 0), (4, 4), (0, 6), (-4, 4), (-6, 0), (-4, -4), (0, -6), (4, -4) };
            var heading = offsets[person.Agent.ExplorationHeading % offsets.Length];
            var site = Circle(person.X, person.Y, 6)
                .Where(i => Tiles[i].Value.IsWalkable && Tiles[i].Value.FireTicks == 0)
                .OrderBy(i =>
                    Distance(i % Width, i / Width, person.X + heading.X, person.Y + heading.Y))
                .FirstOrDefault(-1);
            if (site >= 0 && Distance(person.X, person.Y, home.Value.X, home.Value.Y) < 24)
            {
                choices.Add(new GoalChoice(AgentGoalKind.Explore, site % Width, site / Width, 62,
                    person.Profession == Profession.Lumberjack
                        ? "在家园看到木材短缺，眼前没有可采森林，沿可见陆地寻找下一处材料来源"
                        : "在家园看到石材、矿石或生产燃料不足，沿可见陆地寻找可开采材料"));
            }
            else
            {
                person.Agent = person.Agent with { ExplorationHeading = (person.Agent.ExplorationHeading + 3) % 8 };
                choices.Add(new GoalChoice(AgentGoalKind.ReturnHome, home.Value.X, home.Value.Y, 70, "勘察距离已达口粮范围，先返回家园补给", null,
                    home.Value.Id));
            }
        }

        if (person.Agent.SocialNeed > 35 && _citizens[home.Value.Id].Count > 1)
        {
            choices.Add(new GoalChoice(AgentGoalKind.Socialize, home.Value.X, home.Value.Y,
                person.Agent.SocialNeed * (0.6 + personality.Sociability * 0.5), "社交需求较高，去聚落与人交流", null, home.Value.Id));
        }

        if (person.Age >= 14 && person.ArmyId == 0 && workTarget is { } useful
            && !choices.Any(c => c.EntityId == useful.Value.Id))
        {
            choices.Add(new GoalChoice(AgentGoalKind.Work, useful.Value.X, useful.Value.Y, 25 + personality.Diligence * 8,
                "本职暂无任务，协助附近实际施工或生产", EntityId: useful.Value.Id));
        }

        if (Distance(person.X, person.Y, home.Value.X, home.Value.Y) <= 1 && choices.Count == 0)
        {
            choices.Add(_citizens[home.Value.Id].Count > 1
                ? new GoalChoice(AgentGoalKind.Socialize, home.Value.X, home.Value.Y, 8, "暂时没有可执行工作，在家园交流消息与恢复精力", null, home.Value.Id)
                : new GoalChoice(AgentGoalKind.Rest, home.Value.X, home.Value.Y, 8, "暂时没有可执行工作或交流对象，在家园休息并等待下一次评估", null, home.Value.Id));
        }

        AddAgentMissionChoices(person, home, choices);
        CommitAgentChoice(person, home, choices, interrupted, activeMission);
    }

    private bool ProvisionGoalContinues(ResidentCursor person)
    {
        var goal = person.Agent.Goal;
        if (person.Agent.Fatigue >= 60 || person.Thirst >= 60
                                       || SimulationTick < goal.NavigationRetryTick
                                       || Distance(person.X, person.Y, goal.TargetX, goal.TargetY) > 6)
            return false;
        if (goal.Kind == AgentGoalKind.FetchWater)
        {
            var source = goal.TargetEntityId - 1;
            return source >= 0 && source < Tiles.Count && person.Hunger < 60
                   && person.Inventory.Water < WaterCollectionTarget(person)
                   && GetDailyWaterCapacity(source % Width, source / Width) >= .1;
        }

        if (person.Hunger < 60 || person.Inventory.Food >= TravelReserve(person) + 2
                               || (Rules.Thirst && person.Inventory.Water < LocalWaterUse(person)
                                                        && GetDailyWaterCapacity(person.X, person.Y) <
                                                        LocalWaterUse(person)))
            return false;
        if (goal.Kind == AgentGoalKind.Gather)
        {
            var source = Index(goal.TargetX, goal.TargetY);
            var tile = Tiles[source];
            var efficiency = NaturalPlantHarvestEfficiency(tile);
            if (efficiency < .25)
                return false;
            var dailyYield = .7 * ResourceSiteYield(source, Profession.Farmer, out _)
                                * RaceTerrainRules.For(person.Race, tile.Value.Terrain).Productivity *
                                GatheringCondition(person.SicknessTicks, person.Hunger, person.Thirst)
                                * (.75 + person.Agent.Personality.Diligence * .5) * Rules.GatheringRate
                                * AgentFoodPolicyMultiplier(person.Agent, person.SettlementId, person.X, person.Y) * GatheringTerritoryMultiplier(person.SettlementId, person.NationId, tile.Value);
            return dailyYield >= FoodUse(person);
        }

        if (goal.Kind is not (AgentGoalKind.Hunt or AgentGoalKind.Fish) || !WildlifeGoalProductive(person))
            return false;
        var wildlifeSource = Tiles[goal.TargetEntityId - 1];
        var animal = EdibleAnimal(wildlifeSource, goal.Kind == AgentGoalKind.Fish);
        var interval = WorkInterval(person);
        var harvest = WildlifeHarvestAmount(wildlifeSource, animal,
            interval * .15 * Rules.GatheringRate * GatheringCondition(person.SicknessTicks, person.Hunger, person.Thirst)
            * GatheringTerritoryMultiplier(person.SettlementId, person.NationId, wildlifeSource.Value));
        return harvest * AnimalRules.For(animal).BodyMass / interval >= FoodUse(person);
    }

    private void AddFoodExplorationChoice(ResidentCursor person, List<GoalChoice> choices)
    {
        var heading = Directions[(person.Id + person.Agent.ExplorationHeading) % Directions.Length];
        var best = -1;
        var bestDistance = int.MaxValue;
        var reachable = 0;
        foreach (var offset in VisibleResourceOffsets)
        {
            if (offset.Distance > 6)
                break;
            if (offset.Distance == 0)
                continue;
            var x = person.X + offset.X;
            var y = person.Y + offset.Y;
            if (!Walkable(x, y, person.Race) || Tiles[Index(x, y)].Value.FireTicks > 0)
                continue;
            var distance = Distance(x, y, person.X + heading.X * 6, person.Y + heading.Y * 6);
            if (distance >= bestDistance || !VisibleSiteReachable(person, Index(x, y), ref reachable))
                continue;
            best = Index(x, y);
            bestDistance = distance;
        }

        if (best < 0)
            return;
        choices.Add(new GoalChoice(AgentGoalKind.Explore, best % Width, best / Width,
            100 + person.Hunger, "眼前没有足够食物，不在空仓反复等待；沿可见通路勘察下一片土地"));
    }

    private void CommitAgentChoice(ResidentCursor person, StateReference<Settlement> home, List<GoalChoice> choices,
        bool interrupted, bool activeMission)
    {
        if (SimulationTick < person.Agent.Goal.NavigationRetryTick)
            choices.RemoveAll(c => Index(c.X, c.Y) == person.Agent.Goal.NavigationTarget);
        choices.Add(new GoalChoice(AgentGoalKind.ReturnHome, home.Value.X, home.Value.Y, 5, "当前看不到合适资源，回到已知家园", null, home.Value.Id));
        var selected = choices[0];
        for (var i = 1; i < choices.Count; i++)
        {
            var candidate = choices[i];
            var comparison = candidate.Score.CompareTo(selected.Score);
            if (comparison > 0 || (comparison == 0 && (int)candidate.Kind < (int)selected.Kind))
                selected = candidate;
        }

        var previous = person.Agent.Goal;
        var continuingMission = previous.Kind == selected.Kind && previous.TargetSettlementId == selected.SettlementId
                                                               && previous.TargetEntityId == selected.EntityId &&
                                                               previous.TargetX == selected.X &&
                                                               previous.TargetY == selected.Y;
        if (continuingMission)
        {
            person.Agent = person.Agent with
            {
                Goal = (previous.NavigationRetryTick > 0 && SimulationTick >= previous.NavigationRetryTick
                        ? previous.ResetNavigation()
                        : previous) with
                    {
                        ReviewTick = SimulationTick + GoalReviewInterval(person),
                        PlayerDirected = previous.PlayerDirected && !interrupted,
                    },
                NextThinkTick = SimulationTick + GoalReviewInterval(person),
            };
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
            StartedTick = SimulationTick,
            ReviewTick = SimulationTick + GoalReviewInterval(person),
            Reason = reason,
        };
        var decisions = person.Agent.Decisions.Add(new AgentDecision
        {
            Tick = SimulationTick,
            Goal = selected.Kind,
            Score = selected.Score,
            Reason = reason,
            EvidenceFactId = selected.Evidence?.Id ?? 0,
            KnowledgeObservedTick = selected.Evidence?.ObservedTick ?? SimulationTick,
            SourceResidentId = selected.Evidence?.SourceResidentId ?? person.Id,
        });
        if (decisions.Count > 6)
            decisions = decisions.RemoveAt(0);
        ChangeWorkReservation(previous, nextGoal);
        person.Agent = person.Agent with
        {
            DaytimeGoal = null,
            Goal = nextGoal,
            NextThinkTick = SimulationTick + GoalReviewInterval(person),
            Decisions = decisions,
        };
        if (selected.Kind is AgentGoalKind.DeliverMessage or AgentGoalKind.Trade or AgentGoalKind.Petition)
            BeginAgentMission(person, home);
        else if (person.Agent.DestinationSettlementId != 0)
        {
            person.Agent = person.Agent with
            {
                DestinationSettlementId = 0,
                CarriedMessages = [],
                MissionRetryTick = SimulationTick + SimulationTime.TicksPerMonth,
            };
        }
    }

    private int FindVisibleResourceSite(ResidentCursor person, Profession profession)
    {
        if (profession == Profession.Miner &&
            person.Agent.MaterialPriority is ResourceKind.Coal or ResourceKind.Oil or ResourceKind.RareEarth)
            return -1;
        var reachable = 0;
        var area = person.Profession == profession ? person.Agent.WorkAreaIndex : -1;
        var survival = Rules.Hunger && person.Hunger > 20 && person.Inventory.Food < .3;
        if (area >= 0 && !survival)
        {
            var distance = Distance(person.X, person.Y, area % Width, area / Width);
            // 地址来自本人接受的分工；视野外只返回已知地址，不读取远方的实际存量。
            if (distance > 6)
                return area;
            if (SuitableSite(area))
                return area;
        }

        foreach (var offset in VisibleResourceOffsets)
        {
            if (offset.Distance > 6)
                break;
            var x = person.X + offset.X;
            var y = person.Y + offset.Y;
            if (!InBounds(x, y) || (area >= 0 && !survival
                                              && Distance(x, y, area % Width, area / Width) > 3))
                continue;
            var index = Index(x, y);
            if (SuitableSite(index))
                return index;
        }

        return -1;

        bool SuitableSite(int index)
        {
            var tile = Tiles[index];
            if (!RaceTerrainRules.CanWalk(tile.Value, person.Race) || tile.Value.FireTicks > 0)
                return false;
            if (profession != Profession.Miner
                && NaturalPlantHarvestEfficiency(tile, profession == Profession.Lumberjack) < .25)
                return false;
            var productivity = ResourceSiteYield(index, profession, out var oreAvailable)
                               * GatheringTerritoryMultiplier(person.SettlementId, person.NationId, tile.Value);
            if (productivity <= 0 || (profession == Profession.Miner
                                      && person.Agent.MaterialPriority == ResourceKind.Ore && !oreAvailable))
                return false;
            if (profession == Profession.Farmer && survival
                                                && .7 * productivity * RaceTerrainRules.For(person.Race, tile.Value.Terrain)
                                                    .Productivity
                                                * GatheringCondition(person.SicknessTicks, person.Hunger, person.Thirst) *
                                                (.75 + person.Agent.Personality.Diligence * .5)
                                                * Rules.GatheringRate < FoodUse(person))
                return false;
            return VisibleSiteReachable(person, index, ref reachable);
        }
    }

    private double ResourceSiteYield(int index, Profession profession)
    {
        return ResourceSiteYield(index, profession, out _);
    }

    private double ResourceSiteYield(int index, Profession profession, out bool oreAvailable)
    {
        var tile = Tiles[index];
        oreAvailable = false;
        if (profession == Profession.Farmer)
            return PlantSiteYield(tile, false);

        if (profession == Profession.Lumberjack)
            return PlantSiteYield(tile, true);

        if (profession == Profession.Miner)
        {
            var source = NaturalMiningSource(index, false);
            oreAvailable = NaturalMiningSource(index, true) >= 0;
            return source < 0
                ? 0
                : Math.Min(1, TerrainRules.For(Tiles[source].Value.Terrain).StoneYield
                              + TerrainRules.For(Tiles[source].Value.Terrain).OreYield);
        }

        return 0;
    }

    // 评分和实地开采使用同一露头；脚下少量石材不能遮住居民已看见且可在邻格开采的山体。
    private int NaturalMiningSource(int index, bool requireOre)
    {
        var selected = -1;
        var bestYield = 0d;
        Consider(index);
        foreach (var (dx, dy) in Directions)
        {
            var x = index % Width + dx;
            var y = index / Width + dy;
            if (InBounds(x, y) && Tiles[Index(x, y)].Value.Terrain == TerrainType.Mountain)
                Consider(Index(x, y));
        }

        return selected;

        void Consider(int candidate)
        {
            var source = Tiles[candidate];
            ref readonly var minerals = ref TerrainRules.For(source.Value.Terrain);
            if (source.Value.FireTicks > 0 || source.Value.ResourceAmount <= 0 || (requireOre && minerals.OreYield <= 0))
                return;
            var yield = minerals.StoneYield + minerals.OreYield;
            if (yield > bestYield)
            {
                selected = candidate;
                bestYield = yield;
            }
        }
    }

    /// <summary>标记六格可见半径内的可达地格，并返回本次搜索的标记编号。</summary>
    /// <param name="person">提供搜索起点和种族通行规则的居民。</param>
    /// <param name="requestedMode">待评估的交通方式，不修改居民状态；默认使用居民当前的方式。</param>
    private int MarkVisibleReachable(ResidentCursor person, TravelMode? requestedMode = null)
    {
        _territoryCounts.Bind(Tiles);
        // 地形、桥梁或火情可能在同一天改变，因此不能只按时间判断缓存路径是否有效。
        var revision = _territoryCounts.VisibleTraversalRevision(person.X, person.Y, Width);
        var mode = requestedMode ?? person.TravelMode;
        var start = Index(person.X, person.Y);
        if (_visibleAccessSearch != 0 && _visibleAccessSearch == _localMoveSearch && _visibleAccessRevision == revision
            && _visibleAccessOrigin == start && _visibleAccessWidth == Width
            && _visibleAccessMode == mode && _visibleAccessRace == person.Race)
            return _visibleAccessSearch;
        if (_localMoveVisited.Length != Tiles.Count)
            _localMoveVisited = new int[Tiles.Count];
        if (_localMoveSearch == int.MaxValue)
        {
            Array.Clear(_localMoveVisited);
            _localMoveSearch = 0;
        }

        var search = ++_localMoveSearch;
        _visibleAccessOrigin = start;
        _visibleAccessRevision = revision;
        _visibleAccessWidth = Width;
        _visibleAccessMode = mode;
        _visibleAccessRace = person.Race;
        _visibleAccessSearch = search;
        var cache = _visibleAccessCache ??= new VisibleAccessCache();
        var key = start * 12 + (int)person.Race * 3 + (int)mode;
        var slot = cache.Slot(key, Width);
        if (cache.TryMark(slot, key, revision, _localMoveVisited, search))
            return search;
        _localMoveVisited[start] = search;
        var head = 0;
        var tail = 1;
        _localMoveQueue[0] = (start, -1, 0);
        while (head < tail)
        {
            var current = _localMoveQueue[head++];
            var currentX = current.Index % Width;
            var currentY = current.Index / Width;
            var from = Tiles[current.Index];
            foreach (var (dx, dy) in Directions)
            {
                var x = currentX + dx;
                var y = currentY + dy;
                if (!InBounds(x, y) || Distance(person.X, person.Y, x, y) > 6)
                    continue;
                var index = Index(x, y);
                if (_localMoveVisited[index] == search)
                    continue;
                var to = Tiles[index];
                var horizontal = dy == 0;
                if (to.Value.FireTicks > 0 || !CanTraverse(to.Value, mode, person.Race)
                                     || (mode == TravelMode.Foot &&
                                         ((from.Value.Improvement == LandImprovement.Bridge && horizontal !=
                                              (from.Value.BridgeDirection == BridgeDirection.Horizontal))
                                          || (to.Value.Improvement == LandImprovement.Bridge && horizontal !=
                                              (to.Value.BridgeDirection == BridgeDirection.Horizontal)))))
                    continue;
                _localMoveVisited[index] = search;
                _localMoveQueue[tail++] = (index, -1, current.Depth + 1);
            }
        }

        cache.Store(slot, key, revision, _localMoveQueue, tail);
        return search;
    }

    private bool VisibleSiteReachable(ResidentCursor person, int index, ref int search,
        TravelMode? requestedMode = null)
    {
        var mode = requestedMode ?? person.TravelMode;
        var x = index % Width;
        var y = index / Width;
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

    private void ActOnAgentGoal(ResidentCursor person, StateReference<Settlement> home)
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
        var interactionRange = AgentInteractionRange(person.Agent.Goal, home.Value.FoundationPending);
        if (Distance(person.X, person.Y, goal.TargetX, goal.TargetY) > interactionRange ||
            !CanTraverse(Tiles[Index(person.X, person.Y)].Value, person.TravelMode, person.Race))
        {
            var activity = goal.Kind == AgentGoalKind.Flee ? ResidentActivity.Fleeing : ResidentActivity.Wandering;
            if (!MoveAgentTowards(person, goal.TargetX, goal.TargetY, activity))
                person.Activity = activity;
            return;
        }

        if (SimulationTick - person.MoveStartedTick < person.MoveDurationTicks)
            return;
        goal = goal.Attend();
        switch (goal.Kind)
        {
            case AgentGoalKind.Eat:
                // 到场补给已在每日需求前结算；等待期间不重复生成同一粮情或重设评估时间。
                var inspectFood = person.Activity != ResidentActivity.Eating || goal.StartedTick == SimulationTick
                                                                             || SimulationTick >=
                                                                             person.Agent.NextThinkTick;
                person.Activity = ResidentActivity.Eating;
                if (inspectFood)
                {
                    var reviewInterval = GoalReviewInterval(person);
                    var next = Math.Min(person.Agent.NextThinkTick,
                        SimulationTick + (reviewInterval < SimulationTime.TicksPerDay ? reviewInterval : 4));
                    var observed = person.Agent.Remember(MakeAgentFact(person, AgentFactKind.FoodSupply, home.Value.Id,
                        home.Value.X, home.Value.Y, home.Value.Resources.Food, $"实地查看粮仓：{home.Value.Resources.Food:0.0} 份粮食"), home.Value.Id);
                    person.Agent = observed with { Goal = goal with { ReviewTick = next }, NextThinkTick = next };
                }

                break;
            case AgentGoalKind.Gather:
                person.Agent = person.Agent.WithGoal(goal);
                GatherActualResources(person, Profession.Farmer);
                break;
            case AgentGoalKind.Work:
                person.Agent = person.Agent.WithGoal(goal);
                if (goal.TargetEntityId == 0 &&
                    person.Profession is Profession.Farmer or Profession.Lumberjack or Profession.Miner)
                    GatherActualResources(person, person.Profession);
                else if (TryWorkAtBuilding(person))
                    person.Activity = ResidentActivity.Working;
                else if (person.Profession is Profession.Farmer or Profession.Lumberjack or Profession.Miner)
                    GatherActualResources(person, person.Profession);
                else
                    person.Agent = person.Agent with { NextThinkTick = SimulationTick + 1 };
                break;
            case AgentGoalKind.Rest:
                if (person.Agent.Fatigue == 0 && person.Activity == ResidentActivity.Resting)
                    break;
                person.Agent = person.Agent with
                {
                    Goal = goal, Fatigue = Math.Max(0, person.Agent.Fatigue - 2.2 * HomeRestMultiplier(person.SettlementId, person.X, person.Y)),
                };
                person.Activity = ResidentActivity.Resting;
                break;
            case AgentGoalKind.Sleep:
                person.Activity = ResidentActivity.Sleeping;
                person.Agent = person.Agent with { Fatigue = Math.Max(0, person.Agent.Fatigue - 2.2) };
                break;
            case AgentGoalKind.Flee:
                person.Agent = person.Agent with { Goal = goal, NextThinkTick = SimulationTick + 1 };
                person.Activity = ResidentActivity.Fleeing;
                break;
            case AgentGoalKind.Socialize:
                if (person.Agent.Fatigue == 0 && person.Activity == ResidentActivity.Talking)
                    break;
                person.Agent = person.Agent with { Goal = goal, Fatigue = Math.Max(0, person.Agent.Fatigue - .4) };
                person.Activity = ResidentActivity.Talking;
                break;
            case AgentGoalKind.Study:
            case AgentGoalKind.TrainMagic:
                person.Agent = person.Agent.WithGoal(goal);
                if (TryWorkAtBuilding(person))
                    person.Activity = ResidentActivity.Studying;
                else
                    person.Agent = person.Agent with { NextThinkTick = SimulationTick + 1 };
                break;
            case AgentGoalKind.ReturnHome:
                person.Agent = person.Agent.WithGoal(goal);
                TransferPersonalProduction(person, home);
                FinishFoundation(person, home);
                var fatigue = Math.Max(0, person.Agent.Fatigue - .8 * HomeRestMultiplier(person.SettlementId, person.X, person.Y));
                var nextReview = goal.WorkTicks == 1
                    ? SimulationTick + (home.Value.FoundationPending ? 4 : GoalReviewInterval(person))
                    : person.Agent.NextThinkTick;
                if (person.Activity != ResidentActivity.Resting || fatigue != person.Agent.Fatigue
                                                                || nextReview != person.Agent.NextThinkTick)
                {
                    person.Agent = person.Agent with { Fatigue = fatigue, NextThinkTick = nextReview };
                    person.Activity = ResidentActivity.Resting;
                }

                break;
            case AgentGoalKind.Explore:
                // 完成一段探索后保持向外前进，补给或受阻时才转向，避免反复绕同一小圈。
                var turn = Distance(person.X, person.Y, goal.TargetX, goal.TargetY) == 0 &&
                           goal.TargetX == person.FromX && goal.TargetY == person.FromY;
                person.Agent = person.Agent with
                {
                    Goal = goal,
                    ExplorationHeading =
                    turn ? (person.Agent.ExplorationHeading + 1) % 8 : person.Agent.ExplorationHeading,
                    NextThinkTick = SimulationTick + 1,
                };
                person.Activity = ResidentActivity.Working;
                break;
            case AgentGoalKind.ClaimLand:
                person.Agent = person.Agent.WithGoal(goal);
                person.Activity = ResidentActivity.Working;
                TryClaimLand(person);
                break;
            case AgentGoalKind.FetchWater:
                person.Agent = person.Agent.WithGoal(goal);
                TryFetchWater(person);
                break;
            case AgentGoalKind.Hunt:
            case AgentGoalKind.Fish:
                person.Agent = person.Agent.WithGoal(goal);
                if (!IsWorkDay(person))
                    break;
                TryHarvestWildlife(person);
                break;
            case AgentGoalKind.ExtinguishFire:
                person.Agent = person.Agent.WithGoal(goal);
                TryExtinguishFire(person);
                break;
            default:
                person.Agent = person.Agent.WithGoal(goal);
                break;
        }
    }

    private int AgentInteractionRange(AgentGoal goal, bool foundationPending)
    {
        if (foundationPending && goal.Kind == AgentGoalKind.ReturnHome)
            return 0;
        if (goal.Kind == AgentGoalKind.ExtinguishFire)
            return 1;
        if (goal.Kind is AgentGoalKind.ClaimLand or AgentGoalKind.FetchWater or AgentGoalKind.Hunt
            or AgentGoalKind.Fish)
            return 0;
        if (goal.Kind is AgentGoalKind.Eat or AgentGoalKind.Rest or AgentGoalKind.Socialize
            or AgentGoalKind.ReturnHome or AgentGoalKind.Sleep)
            return 1;
        return goal.Kind is AgentGoalKind.Work or AgentGoalKind.Study or AgentGoalKind.TrainMagic
               && FindBuilding(goal.TargetEntityId) is { } building
               && (!building.Value.IsCompleted || building.Value.IsUpgrading || IsWaterfrontBuilding(building.Value.Kind) ||
                   building.Value.Kind == BuildingKind.TownCenter)
            ? 1
            : 0;
    }

    private void GatherActualResources(ResidentCursor person, Profession profession)
    {
        if (!IsWorkDay(person))
            return;
        if (profession == Profession.Miner && TryGatherDeposit(person))
            return;
        var index = Index(person.X, person.Y);
        var tile = Tiles[index];
        var siteYield = tile.Value.FireTicks > 0 ? 0 : ResourceSiteYield(index, profession);
        if (siteYield <= 0)
        {
            person.Agent = person.Agent with { NextThinkTick = SimulationTick + 1 };
            return;
        }

        var productivity = WorkDays(person) * RaceTerrainRules.For(person.Race, tile.Value.Terrain).Productivity *
                           GatheringCondition(person.SicknessTicks, person.Hunger, person.Thirst)
                           * (0.75 + person.Agent.Personality.Diligence * 0.5) * Rules.GatheringRate
                           * (profession is Profession.Lumberjack or Profession.Miner &&
                              HasResearch(person.SettlementId, Advancement.Forestry)
                               ? 1.25
                               : 1);
        var inventory = person.Inventory;
        if (profession == Profession.Farmer)
        {
            var amount = HarvestPlants(tile,
                0.7 * siteYield * productivity * AgentFoodPolicyMultiplier(person.Agent, person.SettlementId, person.X, person.Y) *
                GatheringTerritoryMultiplier(person.SettlementId, person.NationId, tile.Value));
            inventory = inventory with { Food = inventory.Food + amount };
            RecordHarvest(tile, amount);
        }
        else if (profession == Profession.Lumberjack)
        {
            var amount = HarvestPlants(tile,
                0.28 * siteYield * productivity * (person.Race == RaceKind.Elf ? 1.2 : 1) *
                GatheringTerritoryMultiplier(person.SettlementId, person.NationId, tile.Value), true);
            inventory = inventory with { Wood = inventory.Wood + amount };
            RecordHarvest(tile, amount);
            FinishLogging(tile, person.X, person.Y);
        }
        else
        {
            var source = NaturalMiningSource(index, person.Agent.MaterialPriority == ResourceKind.Ore);
            if (source < 0)
                source = NaturalMiningSource(index, false);
            if (source < 0)
                return;
            tile = Tiles[source];

            var amount = Math.Min(tile.Value.ResourceAmount,
                0.24 *
                Math.Min(1, TerrainRules.For(tile.Value.Terrain).StoneYield + TerrainRules.For(tile.Value.Terrain).OreYield) *
                productivity * (person.Race == RaceKind.Dwarf ? 1.3 : 1) * GatheringTerritoryMultiplier(person.SettlementId, person.NationId, tile.Value));
            tile.Replace(tile.Value.WithResourceAmount(tile.Value.ResourceAmount - (amount)));
            RecordHarvest(tile, amount);
            var minerals = TerrainRules.For(tile.Value.Terrain);
            var oreRatio = minerals.OreYield / Math.Max(.001, minerals.StoneYield + minerals.OreYield);
            inventory = inventory with
            {
                Stone = inventory.Stone + amount * (1 - oreRatio), Ore = inventory.Ore + amount * oreRatio,
            };
        }

        var agent = person.Agent;
        var nextThink = profession != Profession.Miner && !agent.Goal.PlayerDirected
                                                       && NaturalPlantHarvestEfficiency(tile,
                                                           profession == Profession.Lumberjack) < .25
            ? SimulationTick + 1
            : agent.NextThinkTick;
        person.Inventory = inventory;
        person.Agent = agent with
        {
            Fatigue = Math.Min(100, agent.Fatigue + 0.30 * WorkInterval(person) / SimulationTime.TicksPerDay),
            NextThinkTick = nextThink,
        };
        person.Activity = ResidentActivity.Working;
    }

    /// <summary>仅依据六格可见范围内的地形推进一步，并保留居民自身的导航进度。</summary>
    /// <param name="person">移动的居民。</param>
    /// <param name="targetX">当前导航目标的横向地格坐标。</param>
    /// <param name="targetY">当前导航目标的纵向地格坐标。</param>
    /// <param name="walkingActivity">普通自主行走的活动状态，同时计入原有疲劳；其他调用保留自身活动和补给规则。</param>
    private bool MoveAgentTowards(ResidentCursor person, int targetX, int targetY,
        ResidentActivity? walkingActivity = null)
    {
        if (person.FrozenUntilTick > SimulationTick || !InBounds(targetX, targetY) ||
            (person.X == targetX && person.Y == targetY))
            return false;
        if (SimulationTick - person.MoveStartedTick < person.MoveDurationTicks)
            return false;
        if (person.Agent.Goal.NavigationTarget == Index(targetX, targetY) &&
            SimulationTick < person.Agent.Goal.NavigationRetryTick)
            return false;
        var bestStep = SelectAgentStep(person, targetX, targetY, out var navigation);
        if (Rules.Construction && person.TravelMode == TravelMode.Foot &&
            HasResearch(person.SettlementId, Advancement.Logistics)
            && Distance(person.X, person.Y, targetX, targetY) <= 6 &&
            !IsWaterfrontBuilding(FindBuilding(person.Agent.Goal.TargetEntityId)?.Value.Kind ?? BuildingKind.Farm))
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
                NavigationRetryTick = SimulationTick + SimulationTime.TicksPerDay, Reason = "可见范围内没有可用路线，等待通道或重新选择任务",
            };
            if (!blocked.PlayerDirected)
                blocked = blocked with { ReviewTick = SimulationTick };
            person.Agent = person.Agent with
            {
                Goal = blocked, NextThinkTick = blocked.PlayerDirected ? person.Agent.NextThinkTick : SimulationTick,
            };

            return false;
        }

        var xNext = bestStep % Width;
        var yNext = bestStep / Width;
        var duration = AgentMoveDuration(person, bestStep);
        var remaining = Distance(xNext, yNext, targetX, targetY);
        var goal = remaining < navigation.NavigationBestDistance
            ? navigation with
            {
                NavigationBestDistance = remaining, NavigationWithoutProgress = 0, NavigationVisited = [],
            }
            : navigation with
            {
                NavigationWithoutProgress = Math.Min(64, navigation.NavigationWithoutProgress + 1),
                NavigationVisited = navigation.NavigationVisited.Contains(Index(person.X, person.Y))
                    ? navigation.NavigationVisited
                    : navigation.NavigationVisited.Add(Index(person.X, person.Y)),
            };
        var agent = person.Agent.RememberRouteTile(Index(person.X, person.Y)) with
        {
            Goal = goal,
            Fatigue = walkingActivity.HasValue ? Math.Min(100, person.Agent.Fatigue + .15) : person.Agent.Fatigue,
        };
        person.BeginMove(xNext, yNext, SimulationTick, duration,
            walkingActivity ?? (person.ArmyId != 0 ? ResidentActivity.Marching : ResidentActivity.Wandering), agent);

        return true;
    }

    // 选路与实际移动共用耗时，舟船、信使和种族设施的修正不能只影响其中一端。
    private int AgentMoveDuration(ResidentCursor person, int index)
    {
        var terrain = Tiles[index].Value;
        if (person.TravelMode == TravelMode.Foot && person.Agent.DestinationSettlementId == 0
                                                 && person.Profession != Profession.Trader)
            return MoveDurationForSpeed(1 / TerrainMoveCost(terrain, person.Race));
        var x = index % Width;
        var y = index / Width;
        var speed = person.TravelMode == TravelMode.Aircraft
            ? 2
            : person.TravelMode == TravelMode.Boat && !terrain.IsWalkable
                ? 1.5 * BoatTravelMultiplier(x, y, person.NationId)
                : person.Agent.DestinationSettlementId == 0
                    ? 1 / TerrainMoveCost(terrain, person.Race)
                    : MessageTravelMultiplier(x, y, person.NationId, person.Race);
        speed *= RacialTravelBonus(person, x, y);
        return MoveDurationForSpeed(speed);
    }

    private static int MoveDurationForSpeed(double speed)
    {
        return Math.Clamp((int)Math.Round(2 / Math.Max(.1, speed)), 1, 8);
    }

    private int SelectAgentStep(ResidentCursor person, int targetX, int targetY, out AgentGoal goal)
    {
        var originX = person.X;
        var originY = person.Y;
        var width = Width;
        var mode = person.TravelMode;
        var race = person.Race;
        var target = Index(targetX, targetY);
        var start = Index(originX, originY);
        goal = person.Agent.Goal;
        if (goal.NavigationTarget != target)
            goal = goal.BeginNavigation(target, Distance(originX, originY, targetX, targetY));

        if (SimulationTick < goal.NavigationRetryTick)
            return -1;
        var route = goal.NavigationRoute;
        var routeOffset = goal.NavigationRouteOffset;
        if (routeOffset > 0 && routeOffset < route.Length && route[routeOffset - 1] == start
            && goal.NavigationRouteMode == mode)
        {
            var next = route[routeOffset];
            var nextX = next % width;
            var nextY = next / width;
            if (CanTraverseStep(originX, originY, nextX, nextY, mode, race)
                && (mode == TravelMode.Aircraft || Tiles[next].Value.FireTicks == 0)
                && !goal.NavigationVisited.Contains(next))
            {
                if (goal.NavigationWithoutProgress >= 64 || goal.NavigationVisited.Length >= 256)
                    return -1;
                goal = goal with { NavigationRouteOffset = routeOffset + 1 };
                return next;
            }
        }

        // 飞行无需观察地面绕行；相邻且可直接抵达的目标也无需展开搜索。
        if (mode == TravelMode.Aircraft || Distance(originX, originY, targetX, targetY) == 1)
        {
            foreach (var (dx, dy) in Directions)
            {
                var x = originX + dx;
                var y = originY + dy;
                if (Distance(x, y, targetX, targetY) < Distance(originX, originY, targetX, targetY)
                    && CanTraverseStep(originX, originY, x, y, mode, race)
                    && (mode == TravelMode.Aircraft || Tiles[Index(x, y)].Value.FireTicks == 0)
                    && !goal.NavigationVisited.Contains(Index(x, y)))
                    return Index(x, y);
            }
        }

        // 取得新的最短目标距离后清除旧绕路；暂时离开目标方向时仍记住已走地点，防止反复折返。
        if (!goal.NavigationVisited.Contains(start) && goal.NavigationVisited.Length >= 256)
            return -1;

        var result = SearchVisibleAgentStep(person, targetX, targetY, goal);
        goal = result.Goal;
        return result.Step;
    }

    // 只有需要重新寻路时才创建搜索缓冲和成本计算闭包；复用路线的步骤直接返回。
    // 费用由标记保护，父节点随入队写入；只清空标记，数值缓冲始终先写后读。
    [SkipLocalsInit]
    private (int Step, AgentGoal Goal) SearchVisibleAgentStep(ResidentCursor person, int targetX, int targetY,
        AgentGoal goal)
    {
        var originX = person.X;
        var originY = person.Y;
        var width = Width;
        var height = Height;
        var mode = person.TravelMode;
        var race = person.Race;
        var ordinaryWalking = mode == TravelMode.Foot && person.Agent.DestinationSettlementId == 0
                                                      && person.Profession != Profession.Trader;
        var target = Index(targetX, targetY);
        var start = Index(originX, originY);

        // 导航仅使用六格可见地形，桥梁轴向与实际移动及军队寻路共用规则，避免预览可走却无法通行。
        // 单次导航内的记忆标记和进入成本固定；栈上缓冲只覆盖六格视野，不跨居民或世界步骤复用。
        const int diameter = 13;
        Span<byte> flags = stackalloc byte[diameter * diameter];
        flags.Clear();
        Span<double> entryCosts = stackalloc double[diameter * diameter];
        Span<double> pathCosts = stackalloc double[diameter * diameter];
        Span<int> routeParents = stackalloc int[diameter * diameter];
        foreach (var index in person.Agent.FamiliarTiles)
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
                if (x != targetX && (horizontalFirst || y == targetY))
                    nextX += Math.Sign(targetX - x);
                else
                    nextY += Math.Sign(targetY - y);
                var index = Index(nextX, nextY);
                var localIndex = (nextY - originY + 6) * diameter + nextX - originX + 6;
                var tile = Tiles[index].Value;
                if ((routeFlags[localIndex] & 2) != 0 || tile.FireTicks > 0
                                                      || !CanTraverseStep(x, y, nextX, nextY, mode, race))
                    return double.PositiveInfinity;
                if ((routeFlags[localIndex] & 8) == 0)
                {
                    routeCosts[localIndex] = EntryDuration(index, tile) * ((routeFlags[localIndex] & 1) != 0 ? .92 : 1);
                    routeFlags[localIndex] |= 8;
                }

                cost += routeCosts[localIndex];
                x = nextX;
                y = nextY;
            }

            return cost;
        }

        int EntryDuration(int index, Tile tile)
        {
            return ordinaryWalking
                ? MoveDurationForSpeed(1 / TerrainMoveCost(tile, race))
                : AgentMoveDuration(person, index);
        }

        flags[6 * diameter + 6] |= 16;
        pathCosts[6 * diameter + 6] = 0;
        _localRoutes.Clear();
        _localRoutes.Enqueue((start, -1, 0), (0, start));
        var bestStep = -1;
        var bestDestination = -1;
        var bestScore = double.PositiveInfinity;
        while (_localRoutes.TryDequeue(out var current, out var priority))
        {
            var currentX = current.Index % width;
            var currentY = current.Index / width;
            var currentLocal = (currentY - originY + 6) * diameter + currentX - originX + 6;
            if (current.Cost > pathCosts[currentLocal])
                continue;
            if (!targetVisible && priority.Cost > bestScore + .000000001)
                break;
            if (current.Index == target)
            {
                return (current.First,
                    PlanAgentRoute(goal, start, target, originX, originY, width, mode, routeParents));
            }

            var fromTile = Tiles[current.Index].Value;
            flags[(currentY - originY + 6) * diameter + currentX - originX + 6] |= 4;
            if (current.First >= 0)
            {
                // 视野外只估计剩余距离，不读取远方地形；熟悉程度是轻微偏好，不能抵消明显绕远。
                var score = Distance(currentX, currentY, targetX, targetY) * 2
                            + current.Cost;
                if (score < bestScore || (score == bestScore && current.First == bestStep
                                                             && Distance(originX, originY, currentX, currentY)
                                                             > Distance(originX, originY, bestDestination % width,
                                                                 bestDestination / width)))
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
                var toTile = Tiles[index].Value;
                if (!CanTraverseAdjacentTiles(fromTile, toTile, dy == 0, mode, race)
                    || (mode != TravelMode.Aircraft && toTile.FireTicks > 0))
                    continue;
                var first = current.First < 0 ? index : current.First;
                if ((flags[localIndex] & 8) == 0)
                {
                    entryCosts[localIndex] = EntryDuration(index, toTile) * ((flags[localIndex] & 1) != 0 ? .92 : 1);
                    flags[localIndex] |= 8;
                }

                var cost = current.Cost + entryCosts[localIndex];
                var targetDistance = Distance(x, y, targetX, targetY);
                if (targetVisible && cost + targetDistance * .92 > upperCost + .000000001)
                    continue;
                // 视野外目标仍比较原有的「已走成本 + 剩余距离 × 2」；后续每步至少花费 .92。
                // 三角不等式给出下界：cost + .92 × 当前剩余距离 + 1.08 × 视野内最小剩余距离。
                if (!targetVisible &&
                    cost + targetDistance * .92 + minimumTargetDistance * 1.08 > bestScore + .000000001)
                    continue;
                if ((flags[localIndex] & 16) != 0 && pathCosts[localIndex] <= cost)
                    continue;
                flags[localIndex] |= 16;
                pathCosts[localIndex] = cost;
                routeParents[localIndex] = current.Index;
                // 视野外也按所有可见终点的评分下界出队；已有更好的路线时不再展开其余分支。
                var estimated = targetDistance * .92 + (targetVisible ? 0 : minimumTargetDistance * 1.08);
                _localRoutes.Enqueue((index, first, cost), (cost + estimated, index));
            }
        }

        if (goal.NavigationWithoutProgress >= 64)
            return (-1, goal);
        if (bestDestination >= 0)
            goal = PlanAgentRoute(goal, start, bestDestination, originX, originY, width, mode, routeParents);
        return (bestStep, goal);
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
        for (var position = 1; position < route.Length; position++)
            route[position] = reverse[count - position];
        return goal with
        {
            NavigationRoute = ImmutableCollectionsMarshal.AsImmutableArray(route),
            NavigationRouteOffset = 2,
            NavigationRouteMode = mode,
        };
    }

    private double AgentFoodPolicyMultiplier(AgentState agent, int settlementId, int x, int y)
    {
        if (!_settlements.TryGetValue(settlementId, out var home))
            return 1;
        if (Distance(x, y, home.Value.X, home.Value.Y) <= 3)
            return GetPolicyProductionMultiplier(home.Value.Id);
        return agent.FoodPolicyMultiplier(home.Value.Id, SimulationTick);
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

        public int Compare((double Cost, int Index) left, (double Cost, int Index) right)
        {
            return left.Cost < right.Cost ? -1 : left.Cost > right.Cost ? 1 : left.Index.CompareTo(right.Index);
        }
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
        private const int Slots = 8192, Cells = 85;
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

            return (int)(unchecked((uint)key * 2654435761u) >> 19);
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
