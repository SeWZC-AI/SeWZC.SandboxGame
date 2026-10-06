namespace SeWZC.WorldBox.Core;

public sealed partial class WorldEngine
{
    private static readonly (int X, int Y, int Distance)[] VisibleResourceOffsets = CreateVisibleResourceOffsets();

    private readonly List<GoalChoice> _goalChoices = [];

    // 缓冲区按可见半径内的地格数分配；可见区域内绕路超过六步时仍须容纳全部地格。
    private readonly (int Index, int First, int Depth)[] _localMoveQueue = new (int, int, int)[85];
    private int _localMoveSearch;
    private int[] _localMoveVisited = [];
    private VisibleAccessCache? _visibleAccessCache;
    private TravelMode _visibleAccessMode;
    private int _visibleAccessOrigin, _visibleAccessSearch, _visibleAccessWidth;
    private RaceKind _visibleAccessRace;
    private long _visibleAccessTick, _visibleAccessRevision;

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

    private void InitializeAgent(Resident person)
    {
        if (person.Agent.Initialized) return;

        // 按身份推导初始性格，避免初始化消耗世界随机序列。
        double Trait(int salt)
        {
            var value = unchecked((uint)(person.Id * 374761393 + State.Seed * 668265263 + salt));
            value = (value ^ (value >> 13)) * 1274126177;
            return 0.2 + value % 601 / 1000d;
        }

        person.Agent.Initialized = true;
        person.Agent.Personality = new PersonalityProfile
        {
            Courage = person.Trait == "勇敢" ? 0.9 : Trait(17),
            Diligence = person.Trait == "勤劳" ? 0.9 : Trait(41),
            Sociability = person.Trait == "温和" ? 0.9 : Trait(83),
            Ambition = person.Trait == "好奇" ? 0.9 : Trait(131),
        };
        person.Agent.NextThinkTick = State.Tick;
        person.FromX = person.X;
        person.FromY = person.Y;
        if (_settlements.TryGetValue(person.SettlementId, out var home))
        {
            if (person.CultureId == 0) person.CultureId = home.CultureId;
            RememberAgentFact(person, MakeAgentFact(person, AgentFactKind.SettlementLocation,
                home.Id, home.X, home.Y, home.NationId, $"我的家园：{home.Name}"));
        }
    }

    private void UpdateAgentNeedsAndActions()
    {
        try
        {
            BeginLocalWorkQueries();
            foreach (var person in State.Residents)
            {
                InitializeAgent(person);
                if (person.Health <= 0 || !_settlements.TryGetValue(person.SettlementId, out var home)) continue;
                person.Agent.SocialNeed = Math.Min(100, person.Agent.SocialNeed + 0.07);
                // 士兵补给由军队统一扣除，避免与居民系统重复消费。
                if (person.ArmyId != 0)
                {
                    if ((State.Tick + person.Id) % 16 == 0) ObserveAgentEnvironment(person);
                    continue;
                }

                var arrivedHome = Distance(person.X, person.Y, home.X, home.Y) <= 1 &&
                                  Walkable(person.X, person.Y, person.Race)
                                  && State.Tick - person.MoveStartedTick >= person.MoveDurationTicks;
                if (arrivedHome &&
                    !(person.TravelMode == TravelMode.Boat && person.Agent.Goal.Kind == AgentGoalKind.Fish))
                {
                    TransferPersonalProduction(person, home);
                    ProvisionAtHome(person, home);
                }

                DrinkCarriedWater(person);
                if (person.Health <= 0) continue;
                var consumption = !State.Rules.Hunger ? 0 : FoodUse(person);
                var meal = Math.Min(consumption, person.Inventory.Food);
                person.Inventory.Food -= meal;
                person.Hunger =
                    Math.Clamp(person.Hunger + (meal >= consumption - 0.000001 ? -3 : .8 * (1 - meal / consumption)), 0,
                        100);
                if (State.Rules.Hunger && person.Hunger > 80) DamageResident(person, .30, DeathCause.Starvation);
                if (person.Health <= 0) continue;
                if (arrivedHome)
                {
                    if ((State.Tick + person.Id) % 12 == 0)
                        DeliverLocalDiscoveries(person, home);
                }

                var danger = State.Tiles[Index(person.X, person.Y)].FireTicks > 0;
                if ((State.Tick + person.Id) % 16 == 0 || person.Agent.Memory.Count < 2 || danger)
                    ObserveAgentEnvironment(person);
                var directed = person.Agent.Goal.PlayerDirected && State.Tick < person.Agent.Goal.ReviewTick;
                var emergency = danger || (person.Hunger > 60 && person.Inventory.Food < 0.05) ||
                                (State.Rules.Thirst && person.Thirst > 80 && person.Inventory.Water < .025);
                if ((!directed && (State.Tick >= person.Agent.NextThinkTick ||
                                   person.Agent.Goal.Kind == AgentGoalKind.Idle)) || emergency)
                    ChooseAgentGoal(person, home, emergency && directed);
                // 逻辑位置记录已提交的目的地，劳动和递送必须等待实际到达。
                if (State.Tick - person.MoveStartedTick < person.MoveDurationTicks ||
                    person.FrozenUntilTick > State.Tick) continue;
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
    private double TravelReserve(Resident person)
    {
        return person.Profession is Profession.Messenger or Profession.Trader
               && !person.Agent.Memory.Any(f => f.Kind == AgentFactKind.SettlementLocation && f.Value != person.NationId
                   && State.Tick - f.ObservedTick < 1200) ? 6
            : person.Profession is Profession.Lumberjack or Profession.Miner ? 4
            : Math.Clamp(
                .8 + Distance(person.X, person.Y, person.Agent.Goal.TargetX, person.Agent.Goal.TargetY) *
                FoodUse(person) * 6, .8, 8);
    }

    private void TransferPersonalProduction(Resident person, Settlement home)
    {
        // 任务货物在到达约定目的地前仍由居民携带，避免提前入库。
        if (person.Agent.DestinationSettlementId != 0
            && person.Agent.Goal.Kind is AgentGoalKind.Trade or AgentGoalKind.DeliverMessage
                or AgentGoalKind.Petition) return;
        // 家乡邻格也可能是工作点，经过时不能卸掉刚领取的生产原料。
        var assigned = person.Agent.Goal.Kind == AgentGoalKind.Work
            ? FindBuilding(person.Agent.Goal.TargetEntityId)
            : null;
        var recipe = assigned is { IsCompleted: true, Enabled: true } ? AdvancementRules.For(assigned.Kind) : null;
        if (assigned is not null && assigned.SettlementId == home.Id
                                 && (recipe is not null || IsHusbandry(assigned.Kind) ||
                                     ExpansionSupply(assigned.Kind) is not null || assigned.Health < 50)) return;
        var water = Math.Max(0, person.Inventory.Water - WaterReserve(person));
        home.Resources.Water += water;
        person.Inventory.Water -= water;
        var food = Math.Max(0, person.Inventory.Food - TravelReserve(person));
        home.Resources.Food += food;
        person.Inventory.Food -= food;
        home.Resources.Wood += person.Inventory.Wood;
        person.Inventory.Wood = 0;
        home.Resources.Stone += person.Inventory.Stone;
        person.Inventory.Stone = 0;
        home.Resources.Ore += person.Inventory.Ore;
        person.Inventory.Ore = 0;
        home.Resources.Alloy += person.Inventory.Alloy;
        person.Inventory.Alloy = 0;
        home.Resources.EnergyCells += person.Inventory.EnergyCells;
        person.Inventory.EnergyCells = 0;
        home.Resources.Crystals += person.Inventory.Crystals;
        person.Inventory.Crystals = 0;
        foreach (var kind in MineralAndVehicleResources)
        {
            if (kind == ResourceKind.Water) continue;
            home.Resources.Set(kind, home.Resources.Get(kind) + person.Inventory.Get(kind));
            person.Inventory.Set(kind, 0);
        }

        foreach (var kind in new[] { ResourceKind.Tools, ResourceKind.Medicine, ResourceKind.Ammunition })
        {
            var reserve = person.Profession == Profession.Engineer && kind == ResourceKind.Tools ? .5
                : person.Profession == Profession.Physician && kind == ResourceKind.Medicine ? 2
                : person.Profession == Profession.Ranger && kind == ResourceKind.Ammunition ? 8 : 0;
            var amount = Math.Max(0, person.Inventory.Get(kind) - reserve);
            home.Resources.Set(kind, home.Resources.Get(kind) + amount);
            person.Inventory.Set(kind, person.Inventory.Get(kind) - amount);
        }

        person.TravelMode = TravelMode.Foot;
        if (person.Agent.Goal.Kind == AgentGoalKind.ReturnHome && person.Agent.DestinationSettlementId == 0)
            person.Agent.MissionOriginSettlementId = 0;
    }

    private bool ProductiveGoalContinues(Resident person, Settlement home)
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
                                                        (assigned.IsCompleted && !assigned.IsUpgrading))) return false;
        if (person.Hunger >= 60 || person.Thirst >= 60 || person.Agent.Fatigue >= 60
            || person.Inventory.Food >= Math.Max(4, TravelReserve(person) + 1)
            || (State.Rules.Thirst && person.Inventory.Water < WaterUse(person) &&
                AvailableWater(person.X, person.Y) < WaterUse(person))
            || person.Inventory.Wood + person.Inventory.Stone + person.Inventory.Ore >= 3) return false;
        if (goal.NavigationTarget >= 0 && State.Tick < goal.NavigationRetryTick) return false;
        if (goal.Kind == AgentGoalKind.Work && person.Profession is Profession.Physician or Profession.Archivist
                                                or Profession.Surveyor or Profession.Firefighter or Profession.Gardener
                                            && FindBuilding(goal.TargetEntityId) is { } current &&
                                            PreferredExpansionJob(current.Kind) != person.Profession
                                            && ExpansionJobHasNearbyWork(person)) return false;
        if (State.Rules.Hunger && Distance(person.X, person.Y, home.X, home.Y) > 1 && person.Hunger < 20
            && person.Inventory.Food < FoodUse(person) * (Distance(person.X, person.Y, home.X, home.Y) * 4 + 12))
            return false;
        if (goal.Kind == AgentGoalKind.Work && goal.TargetEntityId == 0 && person.Profession == Profession.Miner
            && person.Agent.MaterialPriority is not null && FindVisibleResourceSite(person, Profession.Miner) < 0 &&
            VisibleDepositSite(person) < 0) return false;
        if (goal.Kind == AgentGoalKind.Gather || (goal.Kind == AgentGoalKind.Work && goal.TargetEntityId == 0))
        {
            return ResourceSiteYield(Index(goal.TargetX, goal.TargetY),
                       goal.Kind == AgentGoalKind.Gather ? Profession.Farmer : person.Profession) > 0
                   && (person.Profession == Profession.Miner ||
                       NaturalPlantHarvestEfficiency(State.Tiles[Index(goal.TargetX, goal.TargetY)],
                           goal.Kind == AgentGoalKind.Work && person.Profession == Profession.Lumberjack) >= .25);
        }

        if (goal.Kind is AgentGoalKind.Work or AgentGoalKind.Study or AgentGoalKind.TrainMagic)
        {
            return FindBuilding(goal.TargetEntityId) is { } building && building.SettlementId == home.Id &&
                   BuildingHasWork(building, person);
        }

        return false;
    }

    private bool FoodSupplyNeeded(Resident person, Settlement home)
    {
        if (person.Inventory.Food < .3 || person.Hunger > 20) return true;
        var fact = LatestAgentFact(person.Agent.Memory, AgentFactKind.FoodSupply, home.Id);
        var known = Distance(person.X, person.Y, home.X, home.Y) <= 1 ? home.Resources.Food
            : fact is not null && AgentFactReliability(fact) >= .5 ? fact.Value : 0;
        return known < ProductionStockTarget(home, ResourceKind.Food);
    }

    private bool LocalMaterialsNeeded(Resident person, Settlement home)
    {
        // 远处居民须完成返乡后再依据实际仓库供给决策，避免获得远程库存知识。
        if (Distance(person.X, person.Y, home.X, home.Y) > 1) return true;
        var reserve = _localWorkQueriesActive
            ? _productionReserves.GetValueOrDefault(home.Id)
            : LocalDevelopmentReserve(home);
        if (person.Profession == Profession.Lumberjack) return home.Resources.Wood < Math.Max(80, reserve?.Wood ?? 0);
        if (person.Profession != Profession.Miner)
        {
            return home.Resources.Wood < Math.Max(80, reserve?.Wood ?? 0)
                   || home.Resources.Stone < Math.Max(80, reserve?.Stone ?? 0) ||
                   home.Resources.Ore < Math.Max(80, reserve?.Ore ?? 0);
        }

        return home.Resources.Stone < Math.Max(80, reserve?.Stone ?? 0) || home.Resources.Ore <
                                                                        Math.Max(80, reserve?.Ore ?? 0)
                                                                        || (HasResearch(home.Id,
                                                                                ResearchKind.Industry) &&
                                                                            home.Resources.Coal < 16)
                                                                        || (HasResearch(home.Id,
                                                                                ResearchKind.Electrification) &&
                                                                            home.Resources.Oil < 16)
                                                                        || (HasResearch(home.Id,
                                                                                ResearchKind.AdvancedComputing) &&
                                                                            home.Resources.RareEarth < 16);
    }

    private void ChooseAgentGoal(Resident person, Settlement home, bool interrupted)
    {
        var agent = person.Agent;
        if (person.Profession == Profession.Miner && Distance(person.X, person.Y, home.X, home.Y) <= 1)
        {
            agent.MaterialPriority = home.Resources.Ore < LocalDevelopmentReserve(home).Ore
                ? ResourceKind.Ore
                : HasResearch(home.Id, ResearchKind.Industry) && home.Resources.Coal < 8
                    ? ResourceKind.Coal
                    : HasResearch(home.Id, ResearchKind.Electrification) && home.Resources.Oil < 8
                        ? ResourceKind.Oil
                        : HasResearch(home.Id, ResearchKind.AdvancedComputing) && home.Resources.RareEarth < 8
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
            if (fact.Kind == AgentFactKind.Danger && fact.Value > 0 && AgentFactReliability(fact) > 0.25
                && State.Tick - fact.ObservedTick < 24 && Distance(person.X, person.Y, fact.X, fact.Y) <= 5
                && (dangerFact is null || fact.ObservedTick > dangerFact.ObservedTick))
                dangerFact = fact;
        if (dangerFact is not null || State.Tiles[Index(person.X, person.Y)].FireTicks > 0)
        {
            var safe = Circle(person.X, person.Y, 5)
                .Where(i => State.Tiles[i].IsWalkable && State.Tiles[i].FireTicks == 0)
                .OrderByDescending(i => Distance(i % State.Width, i / State.Width, dangerFact?.X ?? person.X,
                    dangerFact?.Y ?? person.Y))
                .ThenBy(i => Distance(i % State.Width, i / State.Width, home.X, home.Y)).FirstOrDefault(-1);
            if (safe >= 0)
            {
                choices.Add(new GoalChoice(AgentGoalKind.Flee, safe % State.Width, safe / State.Width,
                    (180 - personality.Courage * 30) *
                    (dangerFact is null ? 1 : Math.Max(0.6, AgentFactReliability(dangerFact))),
                    dangerFact?.OriginResidentId == person.Id ? "亲眼见到附近危险，先离开危险区域" : "可信的近时报告指出附近危险，先离开核实",
                    dangerFact));
            }
        }

        if (choices.Count == 0 && !interrupted && (
                ProductiveGoalContinues(person, home)
                || (agent.Goal.Kind == AgentGoalKind.Rest && agent.Fatigue > 8 && person.Hunger < 60 &&
                    person.Thirst < 60)
                || (agent.Goal.Kind == AgentGoalKind.ReturnHome && Distance(person.X, person.Y, home.X, home.Y) > 1
                                                                && State.Tick < agent.Goal.StartedTick + 240 &&
                                                                State.Tick >= agent.Goal.NavigationRetryTick
                                                                && person.Hunger < 60 && person.Thirst < 60)))
        {
            agent.NextThinkTick = State.Tick + 12;
            return;
        }

        if (agent.Goal.Kind is AgentGoalKind.ClaimLand or AgentGoalKind.FetchWater or AgentGoalKind.Hunt
                or AgentGoalKind.Fish
            && choices.Count == 0 && State.Tick - agent.Goal.StartedTick < 48 && person.Hunger < 20 &&
            person.Thirst < 60
            && agent.Fatigue < 60 && State.Tick >= agent.Goal.NavigationRetryTick
            && (agent.Goal.Kind is not (AgentGoalKind.Hunt or AgentGoalKind.Fish) || WildlifeGoalProductive(person))
            && (agent.Goal.Kind != AgentGoalKind.ClaimLand ||
                CanClaimTile(home, Index(agent.Goal.TargetX, agent.Goal.TargetY), person.Race))
            && (agent.Goal.Kind != AgentGoalKind.FetchWater || person.Thirst >= 10
                                                            || (agent.Goal.TargetEntityId > 0 &&
                                                                DailyWaterYield(
                                                                    State.Tiles[agent.Goal.TargetEntityId - 1]) >= .1))
            && person.Inventory.Food < TravelReserve(person) + 2
            && (agent.Goal.Kind != AgentGoalKind.FetchWater || person.Inventory.Water < WaterReserve(person) + 2))
        {
            agent.NextThinkTick = State.Tick + 12;
            return;
        }

        if (agent.Goal.Kind == AgentGoalKind.Migrate && choices.Count == 0 && State.Tick - agent.Goal.StartedTick < 360
            && person.Hunger < 20 && person.Thirst < 60)
        {
            agent.NextThinkTick = State.Tick + 12;
            return;
        }

        if (agent.Goal.Kind == AgentGoalKind.Work && choices.Count == 0 && person.Hunger < 65 && agent.Fatigue < 60
            && (person.Thirst < 40 || person.Inventory.Water >= .3)
            && FindBuilding(agent.Goal.TargetEntityId) is { } factory && factory.SettlementId == home.Id
            && AdvancementRules.For(factory.Kind) is { } recipe && CanProduce(factory, person, recipe)
            && HasProductionInputs(person.Inventory, recipe))
        {
            agent.NextThinkTick = State.Tick + 12;
            return;
        }

        if (agent.Goal.Kind == AgentGoalKind.Explore && choices.Count == 0 && person.Hunger < 65 && agent.Fatigue < 60
            && (person.Thirst < 40 || person.Inventory.Water >= .3)
            && Distance(person.X, person.Y, agent.Goal.TargetX, agent.Goal.TargetY) > 1 &&
            State.Tick - agent.Goal.StartedTick < 48)
        {
            agent.NextThinkTick = State.Tick + 12;
            return;
        }

        var activeMission = agent.DestinationSettlementId != 0 && State.Tick - agent.MissionStartedTick < 360
                                                               && agent.Goal.Kind is AgentGoalKind.Trade
                                                                   or AgentGoalKind.DeliverMessage
                                                                   or AgentGoalKind.Petition;
        if (activeMission && choices.Count == 0 && !(person.Hunger > 60 && person.Inventory.Food < 0.05)
            && !(person.Thirst > 80 && person.Inventory.Water < .025))
        {
            // 任务货物和口粮要随本轮任务继续携带，不能按普通工作背包已满而中断旅程。
            agent.NextThinkTick = State.Tick + 12;
            return;
        }

        if (agent.Goal.Kind == AgentGoalKind.ReturnHome && agent.MissionOriginSettlementId != 0
                                                        && Distance(person.X, person.Y, home.X, home.Y) > 1 &&
                                                        choices.Count == 0)
        {
            agent.NextThinkTick = State.Tick + 12;
            return;
        }

        AddFirefightingChoice(person, choices);
        AddProvisionChoices(person, home, choices);
        if (person.Inventory.Food < 0.3 &&
            (foodFact is null || foodFact.Value > 0 || AgentFactReliability(foodFact) < 0.5))
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
            choices.Add(new GoalChoice(AgentGoalKind.Work, depositSite % State.Width, depositSite / State.Width,
                65, "掌握勘探知识后在眼前发现矿藏，实地开采并运回"));
        }

        var foodSite = FoodSupplyNeeded(person, home) ? FindVisibleResourceSite(person, Profession.Farmer) : -1;
        if (foodSite >= 0)
        {
            var score = person.Profession == Profession.Farmer ? 36 + personality.Diligence * 13 : 12;
            score += person.Hunger * (person.Inventory.Food < 0.3 ? 1.1 : 0.1);
            score *= AgentFoodPolicyMultiplier(person);
            if (foodFact is { Value: < 12 }) score += 18 * AgentFactReliability(foodFact);
            if (State.Rules.Hunger && person.Inventory.Food < .3 && person.Hunger >= 20)
                score = Math.Max(score, 100 + person.Hunger);
            choices.Add(new GoalChoice(AgentGoalKind.Gather, foodSite % State.Width, foodSite / State.Width, score,
                foodFact is { Value: < 12 } ? "已知粮情显示家乡粮少，在可见的可食土地采集" : "眼前土地能产食物，采集后随身携带", foodFact));
        }

        int? materialSite = null;
        if (person.Age >= 14 && person.Profession is Profession.Lumberjack or Profession.Miner
                             && LocalMaterialsNeeded(person, home))
        {
            var site = FindVisibleResourceSite(person, person.Profession);
            materialSite = site;
            if (site >= 0)
            {
                choices.Add(new GoalChoice(AgentGoalKind.Work, site % State.Width, site / State.Width,
                    58 + personality.Diligence * 12,
                    person.Profession == Profession.Lumberjack ? "看见可采木材，前往伐木" : "看见矿石露头，前往开采"));
            }
        }

        if (person.Age >= 14 && (person.Profession is Profession.Farmer or Profession.Fisher or Profession.Lumberjack
                                     or Profession.Miner or Profession.Builder or Profession.Scholar
                                     or Profession.Mage ||
                                 person.Profession >= Profession.Engineer)
                             && FindLocalWorkTarget(person) is { } work)
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
                                                                    ? 18 * AgentFactReliability(foodFact)
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
                                        (HasResearch(home.Id, ResearchKind.Industry) && home.Resources.Coal < 8) ||
                                        (HasResearch(home.Id, ResearchKind.Electrification) && home.Resources.Oil < 8)
                                        || (HasResearch(home.Id, ResearchKind.AdvancedComputing) &&
                                            home.Resources.RareEarth < 8))) ||
                                 agent.Goal.Kind == AgentGoalKind.Explore))
        {
            var offsets = new (int X, int Y)[] { (6, 0), (4, 4), (0, 6), (-4, 4), (-6, 0), (-4, -4), (0, -6), (4, -4) };
            var heading = offsets[agent.ExplorationHeading % offsets.Length];
            var site = Circle(person.X, person.Y, 6)
                .Where(i => State.Tiles[i].IsWalkable && State.Tiles[i].FireTicks == 0)
                .OrderBy(i => Distance(i % State.Width, i / State.Width, person.X + heading.X, person.Y + heading.Y))
                .FirstOrDefault(-1);
            if (site >= 0 && Distance(person.X, person.Y, home.X, home.Y) < 24)
            {
                choices.Add(new GoalChoice(AgentGoalKind.Explore, site % State.Width, site / State.Width, 62,
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

        if (person.Age >= 14 && person.ArmyId == 0 && FindLocalWorkTarget(person) is { } useful
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
        if (State.Tick < agent.Goal.NavigationRetryTick)
            choices.RemoveAll(c => Index(c.X, c.Y) == agent.Goal.NavigationTarget);
        choices.Add(new GoalChoice(AgentGoalKind.ReturnHome, home.X, home.Y, 5, "当前看不到合适资源，回到已知家园", null, home.Id));
        var selected = choices[0];
        for (var i = 1; i < choices.Count; i++)
        {
            var candidate = choices[i];
            var comparison = candidate.Score.CompareTo(selected.Score);
            if (comparison > 0 || (comparison == 0 && (int)candidate.Kind < (int)selected.Kind)) selected = candidate;
        }

        var previous = agent.Goal;
        var continuingMission = previous.Kind == selected.Kind && previous.TargetSettlementId == selected.SettlementId
                                                               && previous.TargetEntityId == selected.EntityId &&
                                                               previous.TargetX == selected.X &&
                                                               previous.TargetY == selected.Y;
        if (continuingMission)
        {
            if (previous.NavigationRetryTick > 0 && State.Tick >= previous.NavigationRetryTick)
            {
                previous.NavigationTarget = -1;
                previous.NavigationVisited.Clear();
                previous.NavigationWithoutProgress = 0;
                previous.NavigationRetryTick = 0;
            }

            agent.NextThinkTick = State.Tick + 12;
            return;
        }

        var reason = (interrupted ? "紧急需要打断原意愿：" : "") + selected.Reason
                                                       + (activeMission && selected.Kind is not AgentGoalKind.Trade
                                                           and not AgentGoalKind.DeliverMessage
                                                           and not AgentGoalKind.Petition
                                                           ? "；递送中止，物资仍随身携带"
                                                           : "");
        agent.Goal = new AgentGoal
        {
            Kind = selected.Kind,
            TargetX = selected.X,
            TargetY = selected.Y,
            TargetSettlementId = selected.SettlementId,
            TargetEntityId = selected.EntityId,
            StartedTick = State.Tick,
            ReviewTick = State.Tick + 12,
            Reason = reason,
        };
        ChangeWorkReservation(previous, agent.Goal);
        agent.NextThinkTick = State.Tick + 12;
        agent.Decisions.Add(new AgentDecision
        {
            Tick = State.Tick,
            Goal = selected.Kind,
            Score = selected.Score,
            Reason = reason,
            EvidenceFactId = selected.Evidence?.Id ?? 0,
            KnowledgeObservedTick = selected.Evidence?.ObservedTick ?? State.Tick,
            SourceResidentId = selected.Evidence?.SourceResidentId ?? person.Id,
        });
        if (agent.Decisions.Count > 6) agent.Decisions.RemoveAt(0);
        if (selected.Kind is AgentGoalKind.Trade or AgentGoalKind.DeliverMessage or AgentGoalKind.Petition)
            BeginAgentMission(person, home);
        else if (agent.DestinationSettlementId != 0)
        {
            agent.DestinationSettlementId = 0;
            agent.CarriedMessages.Clear();
            agent.MissionRetryTick = State.Tick + 120;
        }
    }

    private int FindVisibleResourceSite(Resident person, Profession profession)
    {
        if (profession == Profession.Miner &&
            person.Agent.MaterialPriority is ResourceKind.Coal or ResourceKind.Oil or ResourceKind.RareEarth) return -1;
        var reachable = 0;
        var best = -1;
        var bestScore = double.NegativeInfinity;
        foreach (var offset in VisibleResourceOffsets)
        {
            // 评分上界由肥力和产量上界确定；同分候选仍保留，以维持原行序选择的最小地格索引。
            if (offset.Distance > 6 || 8 - offset.Distance < bestScore) break;
            var x = person.X + offset.X;
            var y = person.Y + offset.Y;
            if (!InBounds(x, y)) continue;
            var index = Index(x, y);
            var tile = State.Tiles[index];
            if (!RaceTerrainRules.CanWalk(tile, person.Race) || tile.FireTicks > 0) continue;
            double? plantEfficiency = null;
            if (profession != Profession.Miner)
            {
                plantEfficiency = NaturalPlantHarvestEfficiency(tile, profession == Profession.Lumberjack);
                if (plantEfficiency < .25) continue;
            }

            var productivity = ResourceSiteYield(index, profession, plantEfficiency, out var oreAvailable) *
                               GatheringTerritoryMultiplier(person, tile);
            if (productivity <= 0) continue;
            if (profession == Profession.Miner && person.Agent.MaterialPriority == ResourceKind.Ore
                                               && !oreAvailable) continue;
            if (profession == Profession.Farmer && State.Rules.Hunger && person.Hunger > 20
                && .7 * productivity * RaceTerrainRules.For(person.Race, tile.Terrain).Productivity *
                GatheringCondition(person) * (.75 + person.Agent.Personality.Diligence * .5) *
                State.Rules.GatheringRate < FoodUse(person)) continue;
            var score = productivity * 8 - offset.Distance;
            if (score < bestScore || (score == bestScore && index >= best)) continue;
            if (!VisibleSiteReachable(person, index, ref reachable)) continue;
            bestScore = score;
            best = index;
        }

        return best;
    }

    private double ResourceSiteYield(int index, Profession profession)
    {
        return ResourceSiteYield(index, profession, null, out _);
    }

    private double ResourceSiteYield(int index, Profession profession, double? plantEfficiency, out bool oreAvailable)
    {
        var tile = State.Tiles[index];
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
            var x = index % State.Width;
            var y = index / State.Width;
            foreach (var (dx, dy) in Directions)
                if (InBounds(x + dx, y + dy) && State.Tiles[Index(x + dx, y + dy)] is
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
    private int MarkVisibleReachable(Resident person, TravelMode? requestedMode = null)
    {
        _territoryCounts.Bind(State.Tiles);
        // 地形、桥梁或火情可能在同一天改变，因此不能只按时间判断缓存路径是否有效。
        var revision = _territoryCounts.TraversalRevision;
        var mode = requestedMode ?? person.TravelMode;
        var start = Index(person.X, person.Y);
        if (_visibleAccessSearch != 0 && _visibleAccessSearch == _localMoveSearch && _visibleAccessRevision == revision
            && _visibleAccessOrigin == start && _visibleAccessTick == State.Tick && _visibleAccessWidth == State.Width
            && _visibleAccessMode == mode && _visibleAccessRace == person.Race)
            return _visibleAccessSearch;
        if (_localMoveVisited.Length != State.Tiles.Length) _localMoveVisited = new int[State.Tiles.Length];
        if (_localMoveSearch == int.MaxValue)
        {
            Array.Clear(_localMoveVisited);
            _localMoveSearch = 0;
        }

        var search = ++_localMoveSearch;
        _visibleAccessOrigin = start;
        _visibleAccessTick = State.Tick;
        _visibleAccessRevision = revision;
        _visibleAccessWidth = State.Width;
        _visibleAccessMode = mode;
        _visibleAccessRace = person.Race;
        _visibleAccessSearch = search;
        var cache = _visibleAccessCache ??= new VisibleAccessCache();
        var key = start * 12 + (int)person.Race * 3 + (int)mode;
        var slot = cache.Slot(key, State.Width);
        if (cache.TryMark(slot, key, State.Tick, revision, _localMoveVisited, search)) return search;
        _localMoveVisited[start] = search;
        var head = 0;
        var tail = 1;
        _localMoveQueue[0] = (start, -1, 0);
        while (head < tail)
        {
            var current = _localMoveQueue[head++];
            var currentX = current.Index % State.Width;
            var currentY = current.Index / State.Width;
            var from = State.Tiles[current.Index];
            foreach (var (dx, dy) in Directions)
            {
                var x = currentX + dx;
                var y = currentY + dy;
                if (!InBounds(x, y) || Distance(person.X, person.Y, x, y) > 6) continue;
                var index = Index(x, y);
                if (_localMoveVisited[index] == search) continue;
                var to = State.Tiles[index];
                var horizontal = dy == 0;
                if (to.FireTicks > 0 || !CanTraverse(to, mode, person.Race)
                                     || (mode == TravelMode.Foot &&
                                         ((from.Improvement == LandImprovement.Bridge && horizontal !=
                                              (from.BridgeDirection == BridgeDirection.Horizontal))
                                          || (to.Improvement == LandImprovement.Bridge && horizontal !=
                                              (to.BridgeDirection == BridgeDirection.Horizontal))))) continue;
                _localMoveVisited[index] = search;
                _localMoveQueue[tail++] = (index, -1, current.Depth + 1);
            }
        }

        cache.Store(slot, key, State.Tick, revision, _localMoveQueue, tail);
        return search;
    }

    private bool VisibleSiteReachable(Resident person, int index, ref int search, TravelMode? requestedMode = null)
    {
        var mode = requestedMode ?? person.TravelMode;
        var x = index % State.Width;
        var y = index / State.Width;
        var distance = Distance(person.X, person.Y, x, y);
        if (distance == 0) return true;
        if (distance == 1) return CanTraverseStep(person.X, person.Y, x, y, mode, person.Race);
        if (distance > 6) return false;
        if (search == 0) search = MarkVisibleReachable(person, mode);
        return _localMoveVisited[index] == search;
    }

    private void ActOnAgentGoal(Resident person, Settlement home)
    {
        var goal = person.Agent.Goal;
        if (goal.Kind == AgentGoalKind.Migrate)
        {
            ActOnMigration(person);
            return;
        }

        if (goal.Kind is AgentGoalKind.Trade or AgentGoalKind.DeliverMessage or AgentGoalKind.Petition)
        {
            ActOnAgentMission(person, home);
            return;
        }

        if (ActOnBuildingRepair(person, home) || ActOnHusbandry(person, home) || ActOnProduction(person, home) ||
            ActOnRacialWork(person, home) || ActOnExpansionFacility(person, home)) return;
        if (goal.Kind == AgentGoalKind.Fish) PrepareJourneyTransport(person, home);
        var interactionRange = AgentInteractionRange(person, home);
        if (Distance(person.X, person.Y, goal.TargetX, goal.TargetY) > interactionRange ||
            !CanTraverse(State.Tiles[Index(person.X, person.Y)], person.TravelMode, person.Race))
        {
            if (MoveAgentTowards(person, goal.TargetX, goal.TargetY))
                person.Agent.Fatigue = Math.Min(100, person.Agent.Fatigue + 0.15);
            person.Activity = goal.Kind == AgentGoalKind.Flee ? ResidentActivity.Fleeing : ResidentActivity.Wandering;
            return;
        }

        if (State.Tick - person.MoveStartedTick < person.MoveDurationTicks) return;
        goal.WorkTicks++;
        switch (goal.Kind)
        {
            case AgentGoalKind.Explore:
                // 完成一段探索后保持向外前进，补给或受阻时才转向，避免反复绕同一小圈。
                if (Distance(person.X, person.Y, goal.TargetX, goal.TargetY) == 0 && goal.TargetX == person.FromX &&
                    goal.TargetY == person.FromY)
                    person.Agent.ExplorationHeading = (person.Agent.ExplorationHeading + 1) % 8;
                person.Agent.NextThinkTick = State.Tick + 1;
                person.Activity = ResidentActivity.Working;
                break;
            case AgentGoalKind.Eat:
                person.Activity = ResidentActivity.Eating;
                if (Distance(person.X, person.Y, home.X, home.Y) <= 1) ProvisionAtHome(person, home);
                RememberAgentFact(person, MakeAgentFact(person, AgentFactKind.FoodSupply, home.Id,
                    home.X, home.Y, home.Resources.Food, $"实地查看粮仓：{home.Resources.Food:0.0} 份粮食"), false);
                person.Agent.NextThinkTick = State.Tick + 1;
                break;
            case AgentGoalKind.ClaimLand:
                person.Activity = ResidentActivity.Working;
                TryClaimLand(person);
                break;
            case AgentGoalKind.FetchWater:
                TryFetchWater(person); break;
            case AgentGoalKind.ExtinguishFire:
                TryExtinguishFire(person); break;
            case AgentGoalKind.Hunt:
            case AgentGoalKind.Fish:
                TryHarvestWildlife(person); break;
            case AgentGoalKind.Gather:
                GatherActualResources(person, Profession.Farmer);
                break;
            case AgentGoalKind.Work:
                if (goal.TargetEntityId == 0 &&
                    person.Profession is Profession.Farmer or Profession.Lumberjack or Profession.Miner)
                    GatherActualResources(person, person.Profession);
                else if (TryWorkAtBuilding(person)) person.Activity = ResidentActivity.Working;
                else if (person.Profession is Profession.Farmer or Profession.Lumberjack or Profession.Miner)
                    GatherActualResources(person, person.Profession);
                else person.Agent.NextThinkTick = State.Tick + 1;
                break;
            case AgentGoalKind.Study:
            case AgentGoalKind.TrainMagic:
                if (TryWorkAtBuilding(person)) person.Activity = ResidentActivity.Studying;
                else person.Agent.NextThinkTick = State.Tick + 1;
                break;
            case AgentGoalKind.Rest:
                person.Activity = ResidentActivity.Resting;
                person.Agent.Fatigue = Math.Max(0, person.Agent.Fatigue - 2.2 * HomeRestMultiplier(person));
                break;
            case AgentGoalKind.Socialize:
                person.Activity = ResidentActivity.Talking;
                person.Agent.Fatigue = Math.Max(0, person.Agent.Fatigue - .4);
                break;
            case AgentGoalKind.ReturnHome:
                TransferPersonalProduction(person, home);
                FinishFoundation(person, home);
                person.Activity = ResidentActivity.Resting;
                person.Agent.Fatigue = Math.Max(0, person.Agent.Fatigue - 0.8 * HomeRestMultiplier(person));
                person.Agent.NextThinkTick = home.FoundationPending ? State.Tick + 4 : State.Tick + 1;
                break;
            case AgentGoalKind.Flee:
                person.Activity = ResidentActivity.Fleeing;
                person.Agent.NextThinkTick = State.Tick + 1;
                break;
        }
    }

    private int AgentInteractionRange(Resident person, Settlement? home)
    {
        var goal = person.Agent.Goal;
        if (home?.FoundationPending == true && goal.Kind == AgentGoalKind.ReturnHome) return 0;
        if (goal.Kind == AgentGoalKind.ExtinguishFire) return 1;
        if (goal.Kind is AgentGoalKind.FetchWater or AgentGoalKind.Hunt or AgentGoalKind.Fish
            or AgentGoalKind.ClaimLand) return 0;
        if (goal.Kind is AgentGoalKind.Eat or AgentGoalKind.Rest or AgentGoalKind.ReturnHome
            or AgentGoalKind.Socialize) return 1;
        return goal.Kind is AgentGoalKind.Work or AgentGoalKind.Study or AgentGoalKind.TrainMagic
               && FindBuilding(goal.TargetEntityId) is { } building
               && (!building.IsCompleted || building.IsUpgrading || IsWaterfrontBuilding(building.Kind) ||
                   building.Kind == BuildingKind.TownCenter)
            ? 1
            : 0;
    }

    private void GatherActualResources(Resident person, Profession profession)
    {
        if (profession == Profession.Miner && TryGatherDeposit(person)) return;
        var index = Index(person.X, person.Y);
        var tile = State.Tiles[index];
        var siteYield = tile.FireTicks > 0 ? 0 : ResourceSiteYield(index, profession);
        if (siteYield <= 0)
        {
            person.Agent.NextThinkTick = State.Tick + 1;
            return;
        }

        var productivity = RaceTerrainRules.For(person.Race, tile.Terrain).Productivity * GatheringCondition(person)
            * (0.75 + person.Agent.Personality.Diligence * 0.5) * State.Rules.GatheringRate
            * (profession is Profession.Lumberjack or Profession.Miner &&
               HasResearch(person.SettlementId, ResearchKind.Forestry)
                ? 1.25
                : 1);
        if (profession == Profession.Farmer)
        {
            var amount = HarvestPlants(tile,
                0.7 * siteYield * productivity * AgentFoodPolicyMultiplier(person) *
                GatheringTerritoryMultiplier(person, tile));
            person.Inventory.Food += amount;
            RecordHarvest(tile, amount);
        }
        else if (profession == Profession.Lumberjack)
        {
            var amount = HarvestPlants(tile,
                0.28 * siteYield * productivity * (person.Race == RaceKind.Elf ? 1.2 : 1) *
                GatheringTerritoryMultiplier(person, tile), true);
            person.Inventory.Wood += amount;
            RecordHarvest(tile, amount);
            FinishLogging(tile, person.X, person.Y);
        }
        else
        {
            if (tile.ResourceAmount <= 0 ||
                TerrainRules.For(tile.Terrain).StoneYield + TerrainRules.For(tile.Terrain).OreYield <= 0)
            {
                tile = Directions.Select(d => (X: person.X + d.X, Y: person.Y + d.Y))
                    .Where(p => InBounds(p.X, p.Y)).Select(p => State.Tiles[Index(p.X, p.Y)])
                    .FirstOrDefault(t => t.Terrain == TerrainType.Mountain && t.ResourceAmount > 0) ?? tile;
            }

            var amount = Math.Min(tile.ResourceAmount,
                0.24 *
                Math.Min(1, TerrainRules.For(tile.Terrain).StoneYield + TerrainRules.For(tile.Terrain).OreYield) *
                productivity * (person.Race == RaceKind.Dwarf ? 1.3 : 1) * GatheringTerritoryMultiplier(person, tile));
            tile.ResourceAmount -= amount;
            RecordHarvest(tile, amount);
            var minerals = TerrainRules.For(tile.Terrain);
            var oreRatio = minerals.OreYield / Math.Max(.001, minerals.StoneYield + minerals.OreYield);
            person.Inventory.Stone += amount * (1 - oreRatio);
            person.Inventory.Ore += amount * oreRatio;
        }

        if (profession != Profession.Miner && !person.Agent.Goal.PlayerDirected &&
            NaturalPlantHarvestEfficiency(tile, profession == Profession.Lumberjack) < .25)
            person.Agent.NextThinkTick = State.Tick + 1;
        person.Agent.Fatigue = Math.Min(100, person.Agent.Fatigue + 0.30);
        person.Activity = ResidentActivity.Working;
    }

    /// <summary>仅依据六格可见范围内的地形推进一步，并保留居民自身的导航进度。</summary>
    /// <param name="person">参与当前操作的居民状态。</param>
    /// <param name="targetX">当前导航目标的横向地格坐标。</param>
    /// <param name="targetY">当前导航目标的纵向地格坐标。</param>
    private bool MoveAgentTowards(Resident person, int targetX, int targetY)
    {
        if (person.FrozenUntilTick > State.Tick || !InBounds(targetX, targetY) ||
            (person.X == targetX && person.Y == targetY)) return false;
        if (State.Tick - person.MoveStartedTick < person.MoveDurationTicks) return false;
        if (person.Agent.Goal.NavigationTarget == Index(targetX, targetY) &&
            State.Tick < person.Agent.Goal.NavigationRetryTick) return false;
        var bestStep = SelectAgentStep(person, targetX, targetY);
        if (State.Rules.Construction && person.TravelMode == TravelMode.Foot &&
            HasResearch(person.SettlementId, ResearchKind.Logistics)
            && Distance(person.X, person.Y, targetX, targetY) <= 6 &&
            !IsWaterfrontBuilding(FindBuilding(person.Agent.Goal.TargetEntityId)?.Kind ?? BuildingKind.Farm))
        {
            var visible = MarkVisibleReachable(person);
            if (_localMoveVisited[Index(targetX, targetY)] != visible) PlanVisibleCrossing(person, targetX, targetY);
        }

        if (bestStep < 0)
        {
            PlanVisibleCrossing(person, targetX, targetY);
            person.Agent.Goal.NavigationRetryTick = State.Tick + 24;
            person.Agent.Goal.Reason = "可见范围内没有可用路线，等待通道或重新选择任务";
            if (!person.Agent.Goal.PlayerDirected)
            {
                person.Agent.Goal.ReviewTick = State.Tick;
                person.Agent.NextThinkTick = State.Tick;
            }

            return false;
        }

        var xNext = bestStep % State.Width;
        var yNext = bestStep / State.Width;
        var terrain = State.Tiles[bestStep];
        var speed = person.TravelMode == TravelMode.Aircraft
            ? 2
            : person.TravelMode == TravelMode.Boat && !terrain.IsWalkable
                ? 1.5 * BoatTravelMultiplier(xNext, yNext, person.NationId)
                : person.Agent.DestinationSettlementId == 0
                    ? 1 / GetTerrainMoveCost(xNext, yNext, person.Race)
                    : MessageTravelMultiplier(xNext, yNext, person.NationId, person.Race);
        speed *= RacialTravelBonus(person, xNext, yNext);
        var duration = Math.Clamp((int)Math.Round(2 / Math.Max(0.1, speed)), 1, 8);
        person.FromX = person.X;
        person.FromY = person.Y;
        person.X = bestStep % State.Width;
        person.Y = bestStep / State.Width;
        person.MoveStartedTick = State.Tick;
        person.MoveDurationTicks = duration;
        var remaining = Distance(person.X, person.Y, targetX, targetY);
        if (remaining < person.Agent.Goal.NavigationBestDistance)
        {
            person.Agent.Goal.NavigationBestDistance = remaining;
            person.Agent.Goal.NavigationWithoutProgress = 0;
        }
        else
            person.Agent.Goal.NavigationWithoutProgress = Math.Min(64, person.Agent.Goal.NavigationWithoutProgress + 1);

        return true;
    }

    private int SelectAgentStep(Resident person, int targetX, int targetY)
    {
        var goal = person.Agent.Goal;
        var target = Index(targetX, targetY);
        var start = Index(person.X, person.Y);
        if (goal.NavigationTarget != target)
        {
            goal.NavigationTarget = target;
            goal.NavigationVisited.Clear();
            goal.NavigationBestDistance = Distance(person.X, person.Y, targetX, targetY);
            goal.NavigationWithoutProgress = 0;
            goal.NavigationRetryTick = 0;
        }

        if (State.Tick < goal.NavigationRetryTick) return -1;
        if (!goal.NavigationVisited.Contains(start))
        {
            if (goal.NavigationVisited.Count >= 256) return -1;
            goal.NavigationVisited.Add(start);
        }

        foreach (var (dx, dy) in Directions)
        {
            var x = person.X + dx;
            var y = person.Y + dy;
            if (CanTraverseStep(person.X, person.Y, x, y, person.TravelMode, person.Race)
                && (person.TravelMode == TravelMode.Aircraft || State.Tiles[Index(x, y)].FireTicks == 0)
                && Distance(x, y, targetX, targetY) < Distance(person.X, person.Y, targetX, targetY)
                && !goal.NavigationVisited.Contains(Index(x, y))) return Index(x, y);
        }

        // 导航仅使用六格可见地形，桥梁轴向与实际移动及军队寻路共用规则，避免预览可走却无法通行。
        if (_localMoveVisited.Length != State.Tiles.Length) _localMoveVisited = new int[State.Tiles.Length];
        if (_localMoveSearch == int.MaxValue)
        {
            Array.Clear(_localMoveVisited);
            _localMoveSearch = 0;
        }

        var search = ++_localMoveSearch;
        _localMoveVisited[start] = search;
        var head = 0;
        var tail = 1;
        _localMoveQueue[0] = (start, -1, 0);
        var bestStep = -1;
        var bestScore = double.PositiveInfinity;
        while (head < tail)
        {
            var current = _localMoveQueue[head++];
            if (current.Depth >= 6) continue;
            foreach (var (dx, dy) in Directions)
            {
                var x = current.Index % State.Width + dx;
                var y = current.Index / State.Width + dy;
                if (!CanTraverseStep(current.Index % State.Width, current.Index / State.Width, x, y, person.TravelMode,
                        person.Race)
                    || (person.TravelMode != TravelMode.Aircraft && State.Tiles[Index(x, y)].FireTicks > 0)) continue;
                var index = Index(x, y);
                if (_localMoveVisited[index] == search) continue;
                if (goal.NavigationVisited.Contains(index)) continue;
                _localMoveVisited[index] = search;
                var first = current.First < 0 ? index : current.First;
                if (index == target) return first;
                var score = Distance(x, y, targetX, targetY) + (current.Depth + 1) * .05;
                if (!goal.NavigationVisited.Contains(index) && score < bestScore)
                {
                    bestStep = first;
                    bestScore = score;
                }

                _localMoveQueue[tail++] = (index, first, current.Depth + 1);
            }
        }

        return goal.NavigationWithoutProgress >= 64 ? -1 : bestStep;
    }

    private double AgentFoodPolicyMultiplier(Resident person)
    {
        if (!_settlements.TryGetValue(person.SettlementId, out var home)) return 1;
        if (Distance(person.X, person.Y, home.X, home.Y) <= 3) return GetPolicyProductionMultiplier(home.Id);
        AgentFact? instruction = null;
        foreach (var fact in person.Agent.Memory)
            if (fact.Kind == AgentFactKind.Policy && fact.SubjectId == home.Id && AgentFactReliability(fact) >= 0.5
                && (instruction is null || fact.ObservedTick > instruction.ObservedTick))
                instruction = fact;
        return instruction?.Value == (int)PolicyKind.FoodSecurity ? 1.25 : 1;
    }

    private static AgentFact? LatestAgentFact(List<AgentFact> memory, AgentFactKind kind, int subjectId)
    {
        AgentFact? latest = null;
        foreach (var fact in memory)
            if (fact.Kind == kind && fact.SubjectId == subjectId
                                  && (latest is null || fact.ObservedTick > latest.ObservedTick))
                latest = fact;
        return latest;
    }

    /// <summary>参与评分的候选任务，记录位置、目标实体及作为依据的记忆。</summary>
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

    /// <summary>在同一模拟日和通行修订号下，按起点、种族和交通方式复用的有界可达性缓存。</summary>
    private sealed class VisibleAccessCache
    {
        private const int Slots = 1024, Cells = 85;
        private readonly int[] _keys = new int[Slots], _counts = new int[Slots], _indices = new int[Slots * Cells];
        private readonly long[] _ticks = new long[Slots], _revisions = new long[Slots];
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

        public bool TryMark(int slot, int key, long tick, long revision, int[] visited, int search)
        {
            if (_counts[slot] == 0 || _keys[slot] != key || _ticks[slot] != tick ||
                _revisions[slot] != revision) return false;
            var first = slot * Cells;
            for (var i = 0; i < _counts[slot]; i++) visited[_indices[first + i]] = search;
            return true;
        }

        public void Store(int slot, int key, long tick, long revision, (int Index, int First, int Depth)[] queue,
            int count)
        {
            _keys[slot] = key;
            _ticks[slot] = tick;
            _revisions[slot] = revision;
            _counts[slot] = count;
            var first = slot * Cells;
            for (var i = 0; i < count; i++) _indices[first + i] = queue[i].Index;
        }
    }
}
