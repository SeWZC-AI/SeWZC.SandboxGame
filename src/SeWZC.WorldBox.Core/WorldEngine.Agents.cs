namespace SeWZC.WorldBox.Core;

public sealed partial class WorldEngine
{
    private readonly record struct GoalChoice(AgentGoalKind Kind, int X, int Y, double Score, string Reason,
        AgentFact? Evidence = null, int SettlementId = 0, int EntityId = 0);
    private readonly List<GoalChoice> _goalChoices = [];
    private static readonly (int X, int Y, int Distance)[] VisibleResourceOffsets = CreateVisibleResourceOffsets();
    // Four-direction search at depth six visits at most 1 + 2 * 6 * 7 cells.
    private readonly (int Index, int First, int Depth)[] _localMoveQueue = new (int, int, int)[85];
    private int[] _localMoveVisited = [];
    private int _localMoveSearch;
    private int _visibleAccessResident, _visibleAccessOrigin, _visibleAccessSearch;
    private long _visibleAccessTick;
    private TravelMode _visibleAccessMode;

    private static (int X, int Y, int Distance)[] CreateVisibleResourceOffsets()
    {
        var offsets = new List<(int X, int Y, int Distance)>();
        for (var y = -6; y <= 6; y++)
        for (var x = -6; x <= 6; x++)
            if (x * x + y * y <= 36) offsets.Add((x, y, Math.Abs(x) + Math.Abs(y)));
        return offsets.OrderBy(offset => offset.Distance).ThenBy(offset => offset.Y).ThenBy(offset => offset.X).ToArray();
    }

    private void InitializeAgent(Resident person)
    {
        if (person.Agent.Initialized) return;
        // Identity-derived traits do not consume the world's simulation RNG during migration.
        double Trait(int salt)
        {
            var value = unchecked((uint)(person.Id * 374761393 + State.Seed * 668265263 + salt));
            value = (value ^ (value >> 13)) * 1274126177;
            return 0.2 + (value % 601) / 1000d;
        }
        person.Agent.Initialized = true;
        person.Agent.Personality = new PersonalityProfile
        {
            Courage = person.Trait == "勇敢" ? 0.9 : Trait(17),
            Diligence = person.Trait == "勤劳" ? 0.9 : Trait(41),
            Sociability = person.Trait == "温和" ? 0.9 : Trait(83),
            Ambition = person.Trait == "好奇" ? 0.9 : Trait(131)
        };
        person.Agent.NextThinkTick = State.Tick;
        person.FromX = person.X; person.FromY = person.Y;
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
                // Soldiers consume their army's physical provisions in the military system.
                if (person.ArmyId != 0)
                {
                    if ((State.Tick + person.Id) % 16 == 0) ObserveAgentEnvironment(person);
                    continue;
                }
                var arrivedHome = Distance(person.X, person.Y, home.X, home.Y) <= 1 && Walkable(person.X, person.Y)
                    && State.Tick - person.MoveStartedTick >= person.MoveDurationTicks;
                if (arrivedHome)
                {
                    TransferPersonalProduction(person, home);
                    ProvisionAtHome(person, home);
                }
                DrinkCarriedWater(person);
                if (person.Health <= 0) continue;
                var consumption = !State.Rules.Hunger ? 0 : FoodUse(person);
                var meal = Math.Min(consumption, person.Inventory.Food);
                person.Inventory.Food -= meal;
                person.Hunger = Math.Clamp(person.Hunger + (meal >= consumption - 0.000001 ? -3 : .8 * (1 - meal / consumption)), 0, 100);
                if (State.Rules.Hunger && person.Hunger > 80) DamageResident(person, .30, DeathCause.Starvation);
                if (person.Health <= 0) continue;
                if (arrivedHome)
                {
                    if ((State.Tick + person.Id) % 12 == 0) DeliverLocalDiscoveries(person, home);
                }
                var danger = State.Tiles[Index(person.X, person.Y)].FireTicks > 0;
                if ((State.Tick + person.Id) % 16 == 0 || person.Agent.Memory.Count < 2 || danger)
                    ObserveAgentEnvironment(person);
                var directed = person.Agent.Goal.PlayerDirected && State.Tick < person.Agent.Goal.ReviewTick;
                var emergency = danger || person.Hunger > 60 && person.Inventory.Food < 0.05 || State.Rules.Thirst && person.Thirst > 80 && person.Inventory.Water < .025;
                if ((!directed && (State.Tick >= person.Agent.NextThinkTick || person.Agent.Goal.Kind == AgentGoalKind.Idle)) || emergency)
                    ChooseAgentGoal(person, home, emergency && directed);
                // Position records a committed destination; work and delivery wait for arrival.
                if (State.Tick - person.MoveStartedTick < person.MoveDurationTicks) continue;
                ActOnAgentGoal(person, home);
            }
        }
        finally
        {
            EndLocalWorkQueries();
        }
        ArchiveDeadResidents();
    }

    // Explorers carry food taken from the home warehouse; extra provisions are not produced cargo.
    private double TravelReserve(Resident person) => person.Profession is Profession.Messenger or Profession.Trader
        && !person.Agent.Memory.Any(f => f.Kind == AgentFactKind.SettlementLocation && f.Value != person.NationId
            && State.Tick - f.ObservedTick < 1200) ? 6
        : person.Profession is Profession.Lumberjack or Profession.Miner ? 4 : Math.Clamp(.8 + Distance(person.X, person.Y, person.Agent.Goal.TargetX, person.Agent.Goal.TargetY) * FoodUse(person) * 6, .8, 8);

    private void TransferPersonalProduction(Resident person, Settlement home)
    {
        // Mission food stays with the carrier until its recorded destination is reached.
        if (person.Agent.DestinationSettlementId != 0
            && person.Agent.Goal.Kind is AgentGoalKind.Trade or AgentGoalKind.DeliverMessage or AgentGoalKind.Petition) return;
        var water = Math.Max(0, person.Inventory.Water - WaterReserve(person));
        home.Resources.Water += water; person.Inventory.Water -= water;
        var food = Math.Max(0, person.Inventory.Food - TravelReserve(person));
        home.Resources.Food += food; person.Inventory.Food -= food;
        home.Resources.Wood += person.Inventory.Wood; person.Inventory.Wood = 0;
        home.Resources.Stone += person.Inventory.Stone; person.Inventory.Stone = 0;
        home.Resources.Ore += person.Inventory.Ore; person.Inventory.Ore = 0;
        home.Resources.Alloy += person.Inventory.Alloy; person.Inventory.Alloy = 0;
        home.Resources.EnergyCells += person.Inventory.EnergyCells; person.Inventory.EnergyCells = 0;
        home.Resources.Crystals += person.Inventory.Crystals; person.Inventory.Crystals = 0;
        foreach (var kind in MineralAndVehicleResources)
        { if (kind == ResourceKind.Water) continue; home.Resources.Set(kind, home.Resources.Get(kind) + person.Inventory.Get(kind)); person.Inventory.Set(kind, 0); }
        person.TravelMode = TravelMode.Foot;
        if (person.Agent.Goal.Kind == AgentGoalKind.ReturnHome && person.Agent.DestinationSettlementId == 0)
            person.Agent.MissionOriginSettlementId = 0;
    }

    private void ChooseAgentGoal(Resident person, Settlement home, bool interrupted)
    {
        var agent = person.Agent;
        var personality = agent.Personality;
        var choices = _goalChoices;
        choices.Clear();
        var foodFact = LatestAgentFact(agent.Memory, AgentFactKind.FoodSupply, home.Id);
        AgentFact? dangerFact = null;
        foreach (var fact in agent.Memory)
            if (fact.Kind == AgentFactKind.Danger && fact.Value > 0 && AgentFactReliability(fact) > 0.25
                && State.Tick - fact.ObservedTick < 24 && Distance(person.X, person.Y, fact.X, fact.Y) <= 5
                && (dangerFact is null || fact.ObservedTick > dangerFact.ObservedTick)) dangerFact = fact;
        if (dangerFact is not null || State.Tiles[Index(person.X, person.Y)].FireTicks > 0)
        {
            var safe = Circle(person.X, person.Y, 5).Where(i => State.Tiles[i].IsWalkable && State.Tiles[i].FireTicks == 0)
                .OrderByDescending(i => Distance(i % State.Width, i / State.Width, dangerFact?.X ?? person.X, dangerFact?.Y ?? person.Y))
                .ThenBy(i => Distance(i % State.Width, i / State.Width, home.X, home.Y)).FirstOrDefault(-1);
            if (safe >= 0) choices.Add(new(AgentGoalKind.Flee, safe % State.Width, safe / State.Width,
                (180 - personality.Courage * 30) * (dangerFact is null ? 1 : Math.Max(0.6, AgentFactReliability(dangerFact))),
                dangerFact?.OriginResidentId == person.Id ? "亲眼见到附近危险，先离开危险区域" : "可信的近时报告指出附近危险，先离开核实", dangerFact));
        }
        if (agent.Goal.Kind is AgentGoalKind.ClaimLand or AgentGoalKind.FetchWater or AgentGoalKind.Hunt or AgentGoalKind.Fish
            && choices.Count == 0 && State.Tick - agent.Goal.StartedTick < 48 && person.Hunger < 20 && person.Thirst < 60
            && (agent.Goal.Kind != AgentGoalKind.ClaimLand || CanClaimTile(home, Index(agent.Goal.TargetX, agent.Goal.TargetY)))
            && person.Inventory.Food < TravelReserve(person) + 2 && person.Inventory.Water < WaterReserve(person) + 2)
        { agent.NextThinkTick = State.Tick + 12; return; }
        if (agent.Goal.Kind == AgentGoalKind.Migrate && choices.Count == 0 && State.Tick - agent.Goal.StartedTick < 360
            && person.Hunger < 20 && person.Thirst < 60)
        { agent.NextThinkTick = State.Tick + 12; return; }
        if (agent.Goal.Kind == AgentGoalKind.Work && choices.Count == 0 && person.Hunger < 65 && agent.Fatigue < 60
            && (person.Thirst < 40 || person.Inventory.Water >= .3)
            && FindBuilding(agent.Goal.TargetEntityId) is { } factory && factory.SettlementId == home.Id
            && AdvancementRules.For(factory.Kind) is { } recipe && CanProduce(factory, person, recipe)
            && MissingResources(person.Inventory, recipe.Input) is null)
        { agent.NextThinkTick = State.Tick + 12; return; }
        if (agent.Goal.Kind == AgentGoalKind.Explore && choices.Count == 0 && person.Hunger < 65 && agent.Fatigue < 60
            && (person.Thirst < 40 || person.Inventory.Water >= .3)
            && Distance(person.X, person.Y, agent.Goal.TargetX, agent.Goal.TargetY) > 1 && State.Tick - agent.Goal.StartedTick < 48)
        { agent.NextThinkTick = State.Tick + 12; return; }
        var activeMission = agent.DestinationSettlementId != 0 && State.Tick - agent.MissionStartedTick < 360
            && agent.Goal.Kind is AgentGoalKind.Trade or AgentGoalKind.DeliverMessage or AgentGoalKind.Petition;
        if (activeMission && choices.Count == 0 && !(person.Hunger > 60 && person.Inventory.Food < 0.05)
            && !(person.Thirst > 80 && person.Inventory.Water < .025))
        {
            // Cargo and travel rations belong to this continuing mission, not a full ordinary work bag.
            agent.NextThinkTick = State.Tick + 12;
            return;
        }
        if (agent.Goal.Kind == AgentGoalKind.ReturnHome && agent.MissionOriginSettlementId != 0
            && Distance(person.X, person.Y, home.X, home.Y) > 1 && choices.Count == 0)
        {
            agent.NextThinkTick = State.Tick + 12;
            return;
        }
        AddFirefightingChoice(person, choices);
        AddProvisionChoices(person, home, choices);
        if (person.Inventory.Food < 0.3 && (foodFact is null || foodFact.Value > 0 || AgentFactReliability(foodFact) < 0.5))
            choices.Add(new(AgentGoalKind.Eat, home.X, home.Y, 65 + person.Hunger,
                foodFact is null ? "随身口粮不足，返回家园查看粮仓" : $"口粮不足；上次获知家乡有 {foodFact.Value:0.0} 份粮食", foodFact, home.Id));
        if (person.Inventory.Food >= Math.Max(4, TravelReserve(person) + 1) || person.Inventory.Wood + person.Inventory.Stone + person.Inventory.Ore >= 3
            || person.Inventory.Alloy + person.Inventory.EnergyCells + person.Inventory.Crystals > 0
            || person.Inventory.Coal + person.Inventory.Oil + person.Inventory.RareEarth >= 3 || person.Inventory.Boats + person.Inventory.Aircraft > 0)
            choices.Add(new(AgentGoalKind.ReturnHome, home.X, home.Y, 75 + personality.Diligence * 12,
                "背包已有产物，亲自运回家园入库", null, home.Id));
        if (agent.MissionOriginSettlementId != 0 && agent.DestinationSettlementId == 0
            && Distance(person.X, person.Y, home.X, home.Y) > 1)
            choices.Add(new(AgentGoalKind.ReturnHome, home.X, home.Y, 70,
                "递送已结束或中止，带着剩余物资亲自返乡", null, home.Id));
        if (agent.Fatigue > 35)
            choices.Add(new(AgentGoalKind.Rest, home.X, home.Y, agent.Fatigue * 1.15,
                $"疲劳达到 {agent.Fatigue:0}，回家休息", null, home.Id));

        var depositSite = VisibleDepositSite(person);
        if (person.Age >= 14 && depositSite >= 0) choices.Add(new(AgentGoalKind.Work, depositSite % State.Width, depositSite / State.Width,
            65, "掌握勘探知识后在眼前发现矿藏，实地开采并运回"));
        var foodSite = FindVisibleResourceSite(person, Profession.Farmer);
        if (foodSite >= 0)
        {
            var score = person.Profession == Profession.Farmer ? 36 + personality.Diligence * 13 : 12;
            score += person.Hunger * (person.Inventory.Food < 0.3 ? 1.1 : 0.1);
            score *= AgentFoodPolicyMultiplier(person);
            if (foodFact is { Value: < 12 }) score += 18 * AgentFactReliability(foodFact);
            if (State.Rules.Hunger && person.Inventory.Food < .3 && person.Hunger >= 20)
                score = Math.Max(score, 100 + person.Hunger);
            choices.Add(new(AgentGoalKind.Gather, foodSite % State.Width, foodSite / State.Width, score,
                foodFact is { Value: < 12 } ? "已知粮情显示家乡粮少，在可见的可食土地采集" : "眼前土地能产食物，采集后随身携带", foodFact));
        }
        if (person.Age >= 14 && person.Profession is Profession.Lumberjack or Profession.Miner)
        {
            var site = FindVisibleResourceSite(person, person.Profession);
            if (site >= 0) choices.Add(new(AgentGoalKind.Work, site % State.Width, site / State.Width,
                58 + personality.Diligence * 12, person.Profession == Profession.Lumberjack ? "看见可采木材，前往伐木" : "看见矿石露头，前往开采"));
        }
        if (person.Age >= 14 && person.Profession is Profession.Farmer or Profession.Lumberjack or Profession.Miner or Profession.Builder or Profession.Scholar or Profession.Mage
            && FindLocalWorkTarget(person) is { } work)
        {
            var kind = AdvancementRules.For(work.Kind) is not null ? AgentGoalKind.Work : person.Profession == Profession.Scholar ? AgentGoalKind.Study : person.Profession == Profession.Mage ? AgentGoalKind.TrainMagic : AgentGoalKind.Work;
            choices.Add(new(kind, work.X, work.Y, 42 + personality.Diligence * 12
                + (person.Profession == Profession.Farmer && foodFact is { Value: < 12 } ? 18 * AgentFactReliability(foodFact) : 0),
                kind == AgentGoalKind.Study ? "附近有可参与的研究设施，前往学习" : kind == AgentGoalKind.TrainMagic ? "附近有可训练的魔法设施" : "附近有实际施工或生产工作", EntityId: work.Id));
        }
        if (person.Age >= 14 && person.Profession is Profession.Lumberjack or Profession.Miner
            && (person.Profession == Profession.Lumberjack ? FindVisibleResourceSite(person, Profession.Lumberjack) < 0 : depositSite < 0)
            && (Distance(person.X, person.Y, home.X, home.Y) <= 1 && (person.Profession == Profession.Lumberjack ? home.Resources.Wood < 60
                : HasResearch(home.Id, ResearchKind.Industry) && home.Resources.Coal < 8 || HasResearch(home.Id, ResearchKind.Electrification) && home.Resources.Oil < 8
                    || HasResearch(home.Id, ResearchKind.AdvancedComputing) && home.Resources.RareEarth < 8) || agent.Goal.Kind == AgentGoalKind.Explore))
        {
            var offsets = new (int X, int Y)[] { (6, 0), (4, 4), (0, 6), (-4, 4), (-6, 0), (-4, -4), (0, -6), (4, -4) };
            var heading = offsets[agent.ExplorationHeading % offsets.Length];
            var site = Circle(person.X, person.Y, 6).Where(i => State.Tiles[i].IsWalkable && State.Tiles[i].FireTicks == 0)
                .OrderBy(i => Distance(i % State.Width, i / State.Width, person.X + heading.X, person.Y + heading.Y)).FirstOrDefault(-1);
            if (site >= 0 && Distance(person.X, person.Y, home.X, home.Y) < 24)
                choices.Add(new(AgentGoalKind.Explore, site % State.Width, site / State.Width, 62,
                    person.Profession == Profession.Lumberjack ? "在家园看到木材短缺，眼前没有可采森林，沿可见陆地寻找下一处材料来源" : "在家园看到生产燃料不足，沿可见陆地寻找当前知识能够开采的矿藏"));
            else { agent.ExplorationHeading = (agent.ExplorationHeading + 3) % 8; choices.Add(new(AgentGoalKind.ReturnHome, home.X, home.Y, 70, "勘察距离已达口粮范围，先返回家园补给", null, home.Id)); }
        }
        if (agent.SocialNeed > 35 && _citizens[home.Id].Count > 1)
            choices.Add(new(AgentGoalKind.Socialize, home.X, home.Y,
                agent.SocialNeed * (0.6 + personality.Sociability * 0.5), "社交需求较高，去聚落与人交流", null, home.Id));
        if (person.Age >= 14 && person.ArmyId == 0 && FindLocalWorkTarget(person) is { } useful
            && !choices.Any(c => c.EntityId == useful.Id))
            choices.Add(new(AgentGoalKind.Work, useful.X, useful.Y, 25 + personality.Diligence * 8, "本职暂无任务，协助附近实际施工或生产", EntityId: useful.Id));
        if (Distance(person.X, person.Y, home.X, home.Y) <= 1 && choices.Count == 0)
            choices.Add(_citizens[home.Id].Count > 1
                ? new(AgentGoalKind.Socialize, home.X, home.Y, 8, "暂时没有可执行工作，在家园交流消息与恢复精力", null, home.Id)
                : new(AgentGoalKind.Rest, home.X, home.Y, 8, "暂时没有可执行工作或交流对象，在家园休息并等待下一次评估", null, home.Id));
        AddAgentMissionChoices(person, home, choices);
        choices.Add(new(AgentGoalKind.ReturnHome, home.X, home.Y, 5, "当前看不到合适资源，回到已知家园", null, home.Id));
        var selected = choices[0];
        for (var i = 1; i < choices.Count; i++)
        {
            var candidate = choices[i];
            var comparison = candidate.Score.CompareTo(selected.Score);
            if (comparison > 0 || comparison == 0 && (int)candidate.Kind < (int)selected.Kind) selected = candidate;
        }
        var previous = agent.Goal;
        var continuingMission = previous.Kind == selected.Kind && previous.TargetSettlementId == selected.SettlementId
            && previous.TargetEntityId == selected.EntityId && previous.TargetX == selected.X && previous.TargetY == selected.Y;
        if (continuingMission)
        {
            agent.NextThinkTick = State.Tick + 12;
            return;
        }
        var reason = (interrupted ? "紧急需要打断原意愿：" : "") + selected.Reason
            + (activeMission && selected.Kind is not AgentGoalKind.Trade and not AgentGoalKind.DeliverMessage and not AgentGoalKind.Petition
                ? "；递送中止，物资仍随身携带" : "");
        agent.Goal = new AgentGoal
        {
            Kind = selected.Kind, TargetX = selected.X, TargetY = selected.Y,
            TargetSettlementId = selected.SettlementId, TargetEntityId = selected.EntityId,
            StartedTick = State.Tick, ReviewTick = State.Tick + 12, Reason = reason
        };
        agent.NextThinkTick = State.Tick + 12;
        agent.Decisions.Add(new AgentDecision
        {
            Tick = State.Tick, Goal = selected.Kind, Score = selected.Score, Reason = reason,
            EvidenceFactId = selected.Evidence?.Id ?? 0,
            KnowledgeObservedTick = selected.Evidence?.ObservedTick ?? State.Tick,
            SourceResidentId = selected.Evidence?.SourceResidentId ?? person.Id
        });
        if (agent.Decisions.Count > 6) agent.Decisions.RemoveAt(0);
        if (selected.Kind is AgentGoalKind.Trade or AgentGoalKind.DeliverMessage or AgentGoalKind.Petition)
            BeginAgentMission(person, home);
        else if (agent.DestinationSettlementId != 0)
        {
            agent.DestinationSettlementId = 0; agent.CarriedMessages.Clear();
            agent.MissionRetryTick = State.Tick + 120;
        }
    }

    private int FindVisibleResourceSite(Resident person, Profession profession)
    {
        var reachable = 0;
        var best = -1; var bestScore = double.NegativeInfinity;
        foreach (var offset in VisibleResourceOffsets)
        {
            // Valid fertility is at most 100 and ResourceSiteYield is at most 1.
            // Keep equal-score candidates: the original row order chose the lowest tile index.
            if (8 - offset.Distance < bestScore) break;
            var x = person.X + offset.X; var y = person.Y + offset.Y;
            if (!InBounds(x, y)) continue;
            var index = Index(x, y);
            var tile = State.Tiles[index];
            if (!tile.IsWalkable || tile.FireTicks > 0) continue;
            var productivity = ResourceSiteYield(index, profession);
            if (productivity <= 0) continue;
            if (profession == Profession.Farmer && State.Rules.Hunger && person.Hunger > 20
                && .7 * productivity * GatheringCondition(person) * (.75 + person.Agent.Personality.Diligence * .5) * State.Rules.GatheringRate < FoodUse(person)) continue;
            var score = productivity * 8 - offset.Distance;
            if (score < bestScore || score == bestScore && index >= best) continue;
            if (!VisibleSiteReachable(person, index, ref reachable)) continue;
            bestScore = score; best = index;
        }
        return best;
    }

    private double ResourceSiteYield(int index, Profession profession)
    {
        var tile = State.Tiles[index];
        if (profession == Profession.Farmer)
            return tile.ResourceAmount > 0 && tile.IsWalkable
                ? Math.Min(1, TerrainRules.For(tile.Terrain).FoodYield / .5) * tile.Fertility / 100d * (tile.DroughtTicks > 0 ? 0.15 : 1) : 0;
        if (profession == Profession.Lumberjack) return tile.ResourceAmount > 0 && tile.Terrain == TerrainType.Forest ? 1 : 0;
        if (profession == Profession.Miner)
        {
            if (tile.ResourceAmount > 0 && tile.Terrain is TerrainType.Hills or TerrainType.Snow) return 1;
            var x = index % State.Width; var y = index / State.Width;
            foreach (var (dx, dy) in Directions)
                if (InBounds(x + dx, y + dy) && State.Tiles[Index(x + dx, y + dy)] is { Terrain: TerrainType.Mountain, ResourceAmount: > 0 }) return 1;
        }
        return 0;
    }

    private int MarkVisibleReachable(Resident person)
    {
        var start = Index(person.X, person.Y);
        if (_visibleAccessSearch != 0 && _visibleAccessSearch == _localMoveSearch && _visibleAccessResident == person.Id
            && _visibleAccessOrigin == start && _visibleAccessTick == State.Tick && _visibleAccessMode == person.TravelMode)
            return _visibleAccessSearch;
        if (_localMoveVisited.Length != State.Tiles.Length) _localMoveVisited = new int[State.Tiles.Length];
        if (_localMoveSearch == int.MaxValue) { Array.Clear(_localMoveVisited); _localMoveSearch = 0; }
        var search = ++_localMoveSearch;
        _visibleAccessResident = person.Id; _visibleAccessOrigin = start; _visibleAccessTick = State.Tick;
        _visibleAccessMode = person.TravelMode; _visibleAccessSearch = search;
        _localMoveVisited[start] = search; var head = 0; var tail = 1;
        _localMoveQueue[0] = (start, -1, 0);
        while (head < tail)
        {
            var current = _localMoveQueue[head++]; if (current.Depth >= 6) continue;
            foreach (var (dx, dy) in Directions)
            {
                var x = current.Index % State.Width + dx; var y = current.Index / State.Width + dy;
                if (!CanTraverseStep(current.Index % State.Width, current.Index / State.Width, x, y, person.TravelMode)
                    || State.Tiles[Index(x, y)].FireTicks > 0) continue;
                var index = Index(x, y); if (_localMoveVisited[index] == search) continue;
                _localMoveVisited[index] = search; _localMoveQueue[tail++] = (index, -1, current.Depth + 1);
            }
        }
        return search;
    }

    private bool VisibleSiteReachable(Resident person, int index, ref int search)
    {
        var x = index % State.Width; var y = index / State.Width;
        var distance = Distance(person.X, person.Y, x, y);
        if (distance == 0) return true;
        if (distance == 1) return CanTraverseStep(person.X, person.Y, x, y, person.TravelMode);
        if (distance > 6) return false;
        if (search == 0) search = MarkVisibleReachable(person);
        return _localMoveVisited[index] == search;
    }

    private void ActOnAgentGoal(Resident person, Settlement home)
    {
        var goal = person.Agent.Goal;
        if (goal.Kind == AgentGoalKind.Migrate)
        {
            ActOnMigration(person); return;
        }
        if (goal.Kind is AgentGoalKind.Trade or AgentGoalKind.DeliverMessage or AgentGoalKind.Petition)
        {
            ActOnAgentMission(person, home);
            return;
        }
        if (ActOnProduction(person, home)) return;
        var interactionRange = home.FoundationPending && goal.Kind == AgentGoalKind.ReturnHome ? 0 : goal.Kind == AgentGoalKind.ExtinguishFire ? 1 : goal.Kind is AgentGoalKind.FetchWater or AgentGoalKind.Hunt or AgentGoalKind.Fish or AgentGoalKind.ClaimLand ? 0 : goal.Kind is AgentGoalKind.Eat or AgentGoalKind.Rest or AgentGoalKind.ReturnHome or AgentGoalKind.Socialize ? 1 : goal.TargetEntityId != 0 && FindBuilding(goal.TargetEntityId) is { } project && (!project.IsCompleted || project.IsUpgrading) ? 1 : 0;
        if (Distance(person.X, person.Y, goal.TargetX, goal.TargetY) > interactionRange || !Walkable(person.X, person.Y))
        {
            if (MoveAgentTowards(person, goal.TargetX, goal.TargetY)) person.Agent.Fatigue = Math.Min(100, person.Agent.Fatigue + 0.15);
            person.Activity = goal.Kind == AgentGoalKind.Flee ? ResidentActivity.Fleeing : ResidentActivity.Wandering;
            return;
        }
        if (State.Tick - person.MoveStartedTick < person.MoveDurationTicks) return;
        goal.WorkTicks++;
        switch (goal.Kind)
        {
            case AgentGoalKind.Explore:
                // Keep the outward heading across completed legs; rotate only after resupply or a blocked leg.
                if (Distance(person.X, person.Y, goal.TargetX, goal.TargetY) == 0 && goal.TargetX == person.FromX && goal.TargetY == person.FromY)
                    person.Agent.ExplorationHeading = (person.Agent.ExplorationHeading + 1) % 8;
                person.Agent.NextThinkTick = State.Tick + 1;
                person.Activity = ResidentActivity.Working;
                break;
            case AgentGoalKind.Eat:
                person.Activity = ResidentActivity.Eating;
                if (Distance(person.X, person.Y, home.X, home.Y) <= 1) ProvisionAtHome(person, home);
                RememberAgentFact(person, MakeAgentFact(person, AgentFactKind.FoodSupply, home.Id,
                    home.X, home.Y, home.Resources.Food, $"实地查看粮仓：{home.Resources.Food:0.0} 份粮食"), copy: false);
                person.Agent.NextThinkTick = State.Tick + 1;
                break;
            case AgentGoalKind.ClaimLand:
                person.Activity = ResidentActivity.Working; TryClaimLand(person); break;
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
                if (goal.TargetEntityId == 0 && person.Profession is Profession.Farmer or Profession.Lumberjack or Profession.Miner)
                    GatherActualResources(person, person.Profession);
                else if (TryWorkAtBuilding(person)) person.Activity = ResidentActivity.Working;
                else if (person.Profession is Profession.Farmer or Profession.Lumberjack or Profession.Miner) GatherActualResources(person, person.Profession);
                else person.Agent.NextThinkTick = State.Tick + 1;
                break;
            case AgentGoalKind.Study:
            case AgentGoalKind.TrainMagic:
                if (TryWorkAtBuilding(person)) person.Activity = ResidentActivity.Studying;
                else person.Agent.NextThinkTick = State.Tick + 1;
                break;
            case AgentGoalKind.Rest:
                person.Activity = ResidentActivity.Resting;
                person.Agent.Fatigue = Math.Max(0, person.Agent.Fatigue - 2.2);
                break;
            case AgentGoalKind.Socialize:
                person.Activity = ResidentActivity.Talking;
                person.Agent.Fatigue = Math.Max(0, person.Agent.Fatigue - .4);
                break;
            case AgentGoalKind.ReturnHome:
                FinishFoundation(person, home);
                TransferPersonalProduction(person, home);
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

    private void GatherActualResources(Resident person, Profession profession)
    {
        if (profession == Profession.Miner && TryGatherDeposit(person)) return;
        var index = Index(person.X, person.Y);
        var tile = State.Tiles[index];
        if (tile.FireTicks > 0 || ResourceSiteYield(index, profession) <= 0)
        {
            person.Agent.NextThinkTick = State.Tick + 1;
            return;
        }
        var productivity = GatheringCondition(person)
            * (0.75 + person.Agent.Personality.Diligence * 0.5) * State.Rules.GatheringRate;
        if (profession == Profession.Farmer)
        {
            var amount = Math.Min(tile.ResourceAmount, 0.7 * ResourceSiteYield(index, profession) * productivity * AgentFoodPolicyMultiplier(person));
            tile.ResourceAmount -= amount; person.Inventory.Food += amount; RecordHarvest(tile, amount);
        }
        else if (profession == Profession.Lumberjack)
        {
            var amount = Math.Min(tile.ResourceAmount, 0.28 * productivity * (person.Race == RaceKind.Elf ? 1.2 : 1));
            tile.ResourceAmount -= amount; person.Inventory.Wood += amount; RecordHarvest(tile, amount);
            FinishLogging(tile, person.X, person.Y);
        }
        else
        {
            if (tile.ResourceAmount <= 0 || tile.Terrain is not TerrainType.Hills and not TerrainType.Snow)
                tile = Directions.Select(d => (X: person.X + d.X, Y: person.Y + d.Y))
                    .Where(p => InBounds(p.X, p.Y)).Select(p => State.Tiles[Index(p.X, p.Y)])
                    .FirstOrDefault(t => t.Terrain == TerrainType.Mountain && t.ResourceAmount > 0) ?? tile;
            var amount = Math.Min(tile.ResourceAmount, 0.24 * productivity * (person.Race == RaceKind.Dwarf ? 1.3 : 1));
            tile.ResourceAmount -= amount; RecordHarvest(tile, amount); person.Inventory.Stone += amount * 0.7; person.Inventory.Ore += amount * 0.3;
        }
        person.Agent.Fatigue = Math.Min(100, person.Agent.Fatigue + 0.30);
        person.Activity = ResidentActivity.Working;
    }

    /// <summary>Moves one grid cell using only terrain within six visible cells; no unsaved navigation cache.</summary>
    private bool MoveAgentTowards(Resident person, int targetX, int targetY)
    {
        if (!InBounds(targetX, targetY) || person.X == targetX && person.Y == targetY) return false;
        if (State.Tick - person.MoveStartedTick < person.MoveDurationTicks) return false;
        PlanVisibleCrossing(person, targetX, targetY);
        var bestStep = SelectAgentStep(person, targetX, targetY);
        if (bestStep < 0) return false;
        var xNext = bestStep % State.Width; var yNext = bestStep / State.Width;
        var terrain = State.Tiles[bestStep];
        var speed = person.TravelMode == TravelMode.Aircraft ? 2
            : person.TravelMode == TravelMode.Boat && !terrain.IsWalkable ? 1.5
            : person.Agent.DestinationSettlementId == 0 ? 1 / GetTerrainMoveCost(xNext, yNext)
            : MessageTravelMultiplier(xNext, yNext, person.NationId);
        var duration = Math.Clamp((int)Math.Round(2 / Math.Max(0.1, speed)), 1, 8);
        person.FromX = person.X; person.FromY = person.Y;
        person.X = bestStep % State.Width; person.Y = bestStep / State.Width;
        person.MoveStartedTick = State.Tick; person.MoveDurationTicks = duration;
        return true;
    }

    private int SelectAgentStep(Resident person, int targetX, int targetY)
    {
        var startDistance = Distance(person.X, person.Y, targetX, targetY);
        var bestStep = -1;
        var bestDistance = startDistance;
        var previous = Index(person.FromX, person.FromY);
        foreach (var (dx, dy) in Directions)
        {
            var x = person.X + dx; var y = person.Y + dy;
            if (!CanTraverseStep(person.X, person.Y, x, y, person.TravelMode) || person.TravelMode != TravelMode.Aircraft && State.Tiles[Index(x, y)].FireTicks > 0) continue;
            var distance = Distance(x, y, targetX, targetY);
            if (distance > bestDistance || distance == bestDistance && (bestStep < 0 || bestStep != previous)) continue;
            bestDistance = distance; bestStep = Index(x, y);
        }
        if (bestStep == previous && (person.FromX != targetX || person.FromY != targetY))
        { bestStep = -1; bestDistance = startDistance; }
        if (bestStep < 0)
        {
            // A bounded local search can walk around visible obstacles without revealing remote terrain.
            var start = Index(person.X, person.Y);
            if (_localMoveVisited.Length != State.Tiles.Length) _localMoveVisited = new int[State.Tiles.Length];
            if (_localMoveSearch == int.MaxValue) { Array.Clear(_localMoveVisited); _localMoveSearch = 0; }
            var search = ++_localMoveSearch;
            _localMoveVisited[start] = search;
            var head = 0; var tail = 1;
            _localMoveQueue[0] = (start, -1, 0);
            while (head < tail)
            {
                var current = _localMoveQueue[head++];
                if (current.Depth >= 6) continue;
                foreach (var (dx, dy) in Directions)
                {
                    var x = current.Index % State.Width + dx; var y = current.Index / State.Width + dy;
                    if (!CanTraverseStep(current.Index % State.Width, current.Index / State.Width, x, y, person.TravelMode) || person.TravelMode != TravelMode.Aircraft && State.Tiles[Index(x, y)].FireTicks > 0) continue;
                    var index = Index(x, y);
                    if (current.First < 0 && index == previous) continue;
                    if (_localMoveVisited[index] == search) continue;
                    _localMoveVisited[index] = search;
                    var first = current.First < 0 ? index : current.First;
                    var distance = Distance(x, y, targetX, targetY);
                    if (distance < bestDistance) { bestDistance = distance; bestStep = first; }
                    _localMoveQueue[tail++] = (index, first, current.Depth + 1);
                }
            }
        }
        if (bestStep < 0)
        {
            // When the visible horizon cannot yet improve distance, follow an obstacle edge instead
            // of oscillating between the last two cells or standing still across a small lake.
            var bestAny = -1; var bestForward = -1;
            var bestAnyScore = int.MinValue; var bestForwardScore = int.MinValue;
            foreach (var (dx, dy) in Directions)
            {
                var x = person.X + dx; var y = person.Y + dy;
                if (!CanTraverseStep(person.X, person.Y, x, y, person.TravelMode) || person.TravelMode != TravelMode.Aircraft && State.Tiles[Index(x, y)].FireTicks > 0) continue;
                var index = Index(x, y);
                var score = dx * (targetX - person.X) + dy * (targetY - person.Y);
                if (score > bestAnyScore) { bestAny = index; bestAnyScore = score; }
                if (index != previous && score > bestForwardScore) { bestForward = index; bestForwardScore = score; }
            }
            bestStep = bestForward >= 0 ? bestForward : bestAny;
        }
        return bestStep;
    }

    private double AgentFoodPolicyMultiplier(Resident person)
    {
        if (!_settlements.TryGetValue(person.SettlementId, out var home)) return 1;
        if (Distance(person.X, person.Y, home.X, home.Y) <= 3) return GetPolicyProductionMultiplier(home.Id);
        AgentFact? instruction = null;
        foreach (var fact in person.Agent.Memory)
            if (fact.Kind == AgentFactKind.Policy && fact.SubjectId == home.Id && AgentFactReliability(fact) >= 0.5
                && (instruction is null || fact.ObservedTick > instruction.ObservedTick)) instruction = fact;
        return instruction?.Value == (int)PolicyKind.FoodSecurity ? 1.25 : 1;
    }

    private static AgentFact? LatestAgentFact(List<AgentFact> memory, AgentFactKind kind, int subjectId)
    {
        AgentFact? latest = null;
        foreach (var fact in memory)
            if (fact.Kind == kind && fact.SubjectId == subjectId
                && (latest is null || fact.ObservedTick > latest.ObservedTick)) latest = fact;
        return latest;
    }
}
