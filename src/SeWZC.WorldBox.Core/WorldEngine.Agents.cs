namespace SeWZC.WorldBox.Core;

public sealed partial class WorldEngine
{
    private sealed record GoalChoice(AgentGoalKind Kind, int X, int Y, double Score, string Reason,
        AgentFact? Evidence = null, int SettlementId = 0, int EntityId = 0);

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
        foreach (var person in State.Residents)
        {
            InitializeAgent(person);
            if (person.Health <= 0 || !_settlements.TryGetValue(person.SettlementId, out var home)) continue;
            person.Agent.SocialNeed = Math.Min(100, person.Agent.SocialNeed + 0.11);
            // Soldiers consume their army's physical provisions in the military system.
            if (person.ArmyId != 0)
            {
                if ((State.Tick + person.Id) % 8 == 0) ObserveAgentEnvironment(person);
                continue;
            }
            var consumption = person.Age < 14 ? 0.025 : person.Race == RaceKind.Orc ? 0.064 : 0.05;
            var meal = Math.Min(consumption, person.Inventory.Food);
            person.Inventory.Food -= meal;
            person.Hunger = Math.Clamp(person.Hunger + (meal >= consumption - 0.000001 ? -3 : 2 * (1 - meal / consumption)), 0, 100);
            if (Distance(person.X, person.Y, home.X, home.Y) <= 1)
            {
                TransferPersonalProduction(person, home);
                if (person.Inventory.Food < 0.3)
                {
                    var ration = Math.Min(home.Resources.Food, 1.2 - person.Inventory.Food);
                    home.Resources.Food -= ration; person.Inventory.Food += ration;
                }
            }
            var danger = State.Tiles[Index(person.X, person.Y)].FireTicks > 0;
            if ((State.Tick + person.Id) % 8 == 0 || person.Agent.Memory.Count < 2 || danger)
                ObserveAgentEnvironment(person);
            var directed = person.Agent.Goal.PlayerDirected && State.Tick < person.Agent.Goal.ReviewTick;
            var emergency = danger || person.Hunger > 85 && person.Inventory.Food < 0.05;
            if ((!directed && (State.Tick >= person.Agent.NextThinkTick || person.Agent.Goal.Kind == AgentGoalKind.Idle)) || emergency)
                ChooseAgentGoal(person, home, emergency && directed);
            ActOnAgentGoal(person, home);
        }
    }

    private void TransferPersonalProduction(Resident person, Settlement home)
    {
        // Mission food stays with the carrier until its recorded destination is reached.
        if (person.Agent.DestinationSettlementId != 0
            && person.Agent.Goal.Kind is AgentGoalKind.Trade or AgentGoalKind.DeliverMessage or AgentGoalKind.Petition) return;
        var food = Math.Max(0, person.Inventory.Food - 1.2);
        home.Resources.Food += food; person.Inventory.Food -= food;
        home.Resources.Wood += person.Inventory.Wood; person.Inventory.Wood = 0;
        home.Resources.Stone += person.Inventory.Stone; person.Inventory.Stone = 0;
        home.Resources.Ore += person.Inventory.Ore; person.Inventory.Ore = 0;
        if (person.Agent.Goal.Kind == AgentGoalKind.ReturnHome && person.Agent.DestinationSettlementId == 0)
            person.Agent.MissionOriginSettlementId = 0;
    }

    private void ChooseAgentGoal(Resident person, Settlement home, bool interrupted)
    {
        var agent = person.Agent;
        var personality = agent.Personality;
        var choices = new List<GoalChoice>();
        var foodFact = agent.Memory.Where(f => f.Kind == AgentFactKind.FoodSupply && f.SubjectId == home.Id)
            .OrderByDescending(f => f.ObservedTick).FirstOrDefault();
        var dangerFact = agent.Memory.Where(f => f.Kind == AgentFactKind.Danger && f.Value > 0
                && AgentFactReliability(f) > 0.25 && State.Tick - f.ObservedTick < 24 && Distance(person.X, person.Y, f.X, f.Y) <= 5)
            .OrderByDescending(f => f.ObservedTick).FirstOrDefault();
        if (dangerFact is not null || State.Tiles[Index(person.X, person.Y)].FireTicks > 0)
        {
            var safe = Circle(person.X, person.Y, 5).Where(i => State.Tiles[i].IsWalkable && State.Tiles[i].FireTicks == 0)
                .OrderByDescending(i => Distance(i % State.Width, i / State.Width, dangerFact?.X ?? person.X, dangerFact?.Y ?? person.Y))
                .ThenBy(i => Distance(i % State.Width, i / State.Width, home.X, home.Y)).FirstOrDefault(-1);
            if (safe >= 0) choices.Add(new(AgentGoalKind.Flee, safe % State.Width, safe / State.Width,
                (180 - personality.Courage * 30) * (dangerFact is null ? 1 : Math.Max(0.6, AgentFactReliability(dangerFact))),
                dangerFact?.OriginResidentId == person.Id ? "亲眼见到附近危险，先离开危险区域" : "可信的近时报告指出附近危险，先离开核实", dangerFact));
        }
        var activeMission = agent.DestinationSettlementId != 0 && State.Tick - agent.MissionStartedTick < 360
            && agent.Goal.Kind is AgentGoalKind.Trade or AgentGoalKind.DeliverMessage or AgentGoalKind.Petition;
        if (activeMission && choices.Count == 0 && !(person.Hunger > 85 && person.Inventory.Food < 0.05))
        {
            // Cargo and travel rations belong to this continuing mission, not a full ordinary work bag.
            agent.NextThinkTick = State.Tick + 6;
            return;
        }
        if (agent.Goal.Kind == AgentGoalKind.ReturnHome && agent.MissionOriginSettlementId != 0
            && Distance(person.X, person.Y, home.X, home.Y) > 1 && choices.Count == 0)
        {
            agent.NextThinkTick = State.Tick + 6;
            return;
        }
        if (person.Inventory.Food < 0.3 && (foodFact is null || foodFact.Value > 0 || AgentFactReliability(foodFact) < 0.5))
            choices.Add(new(AgentGoalKind.Eat, home.X, home.Y, 48 + person.Hunger,
                foodFact is null ? "随身口粮不足，返回家园查看粮仓" : $"口粮不足；上次获知家乡有 {foodFact.Value:0.0} 份粮食", foodFact, home.Id));
        if (person.Inventory.Food >= 4 || person.Inventory.Wood + person.Inventory.Stone + person.Inventory.Ore >= 3)
            choices.Add(new(AgentGoalKind.ReturnHome, home.X, home.Y, 75 + personality.Diligence * 12,
                "背包已有产物，亲自运回家园入库", null, home.Id));
        if (agent.MissionOriginSettlementId != 0 && agent.DestinationSettlementId == 0
            && Distance(person.X, person.Y, home.X, home.Y) > 1)
            choices.Add(new(AgentGoalKind.ReturnHome, home.X, home.Y, 70,
                "递送已结束或中止，带着剩余物资亲自返乡", null, home.Id));
        if (agent.Fatigue > 35)
            choices.Add(new(AgentGoalKind.Rest, home.X, home.Y, agent.Fatigue * 1.15,
                $"疲劳达到 {agent.Fatigue:0}，回家休息", null, home.Id));

        var foodSite = FindVisibleResourceSite(person, Profession.Farmer);
        if (foodSite >= 0)
        {
            var score = person.Profession == Profession.Farmer ? 36 + personality.Diligence * 13 : 12;
            score += person.Hunger * (person.Inventory.Food < 0.3 ? 1.1 : 0.1);
            score *= AgentFoodPolicyMultiplier(person);
            if (foodFact is { Value: < 12 }) score += 18 * AgentFactReliability(foodFact);
            choices.Add(new(AgentGoalKind.Gather, foodSite % State.Width, foodSite / State.Width, score,
                foodFact is { Value: < 12 } ? "已知粮情显示家乡粮少，在可见的可食土地采集" : "眼前土地能产食物，采集后随身携带", foodFact));
        }
        if (person.Age >= 14 && person.Profession is Profession.Lumberjack or Profession.Miner)
        {
            var site = FindVisibleResourceSite(person, person.Profession);
            if (site >= 0) choices.Add(new(AgentGoalKind.Work, site % State.Width, site / State.Width,
                37 + personality.Diligence * 12, person.Profession == Profession.Lumberjack ? "看见可采木材，前往伐木" : "看见矿石露头，前往开采"));
        }
        if (person.Age >= 14 && person.Profession is Profession.Farmer or Profession.Lumberjack or Profession.Miner or Profession.Builder or Profession.Scholar or Profession.Mage
            && TryGetLocalWorkTarget(person, out var workX, out var workY))
        {
            var kind = person.Profession == Profession.Scholar ? AgentGoalKind.Study : person.Profession == Profession.Mage ? AgentGoalKind.TrainMagic : AgentGoalKind.Work;
            choices.Add(new(kind, workX, workY, 42 + personality.Diligence * 12
                + (person.Profession == Profession.Farmer && foodFact is { Value: < 12 } ? 18 * AgentFactReliability(foodFact) : 0),
                kind == AgentGoalKind.Study ? "附近有可参与的研究设施，前往学习" : kind == AgentGoalKind.TrainMagic ? "附近有可训练的魔法设施" : "附近有实际施工或生产工作"));
        }
        if (agent.SocialNeed > 35)
            choices.Add(new(AgentGoalKind.Socialize, home.X, home.Y,
                agent.SocialNeed * (0.6 + personality.Sociability * 0.5), "社交需求较高，去聚落与人交流", null, home.Id));
        AddAgentMissionChoices(person, home, choices);
        choices.Add(new(AgentGoalKind.ReturnHome, home.X, home.Y, 5, "当前看不到合适资源，回到已知家园", null, home.Id));
        var selected = choices.OrderByDescending(c => c.Score).ThenBy(c => c.Kind).First();
        var previous = agent.Goal;
        var continuingMission = previous.Kind == selected.Kind && previous.TargetSettlementId == selected.SettlementId
            && selected.Kind is AgentGoalKind.Trade or AgentGoalKind.DeliverMessage or AgentGoalKind.Petition;
        if (continuingMission)
        {
            agent.NextThinkTick = State.Tick + 6;
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
        agent.NextThinkTick = State.Tick + 6;
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
            agent.MissionRetryTick = State.Tick + 60;
        }
    }

    private int FindVisibleResourceSite(Resident person, Profession profession)
    {
        var best = -1; var bestScore = double.NegativeInfinity;
        foreach (var index in Circle(person.X, person.Y, 6))
        {
            var tile = State.Tiles[index];
            if (!tile.IsWalkable || tile.FireTicks > 0) continue;
            var productivity = ResourceSiteYield(index, profession);
            if (productivity <= 0) continue;
            var score = productivity * 8 - Distance(person.X, person.Y, index % State.Width, index / State.Width);
            if (score <= bestScore) continue;
            bestScore = score; best = index;
        }
        return best;
    }

    private double ResourceSiteYield(int index, Profession profession)
    {
        var tile = State.Tiles[index];
        if (profession == Profession.Farmer)
            return tile.ResourceAmount > 0 && tile.Terrain is TerrainType.Grass or TerrainType.Forest or TerrainType.Wetland or TerrainType.Tundra or TerrainType.Sand
                ? tile.Fertility / 100d * (tile.DroughtTicks > 0 ? 0.15 : 1) : 0;
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

    private void ActOnAgentGoal(Resident person, Settlement home)
    {
        var goal = person.Agent.Goal;
        if (goal.Kind is AgentGoalKind.Trade or AgentGoalKind.DeliverMessage or AgentGoalKind.Petition)
        {
            ActOnAgentMission(person, home);
            return;
        }
        var interactionRange = goal.Kind is AgentGoalKind.Eat or AgentGoalKind.Rest or AgentGoalKind.ReturnHome or AgentGoalKind.Socialize ? 1 : 0;
        if (Distance(person.X, person.Y, goal.TargetX, goal.TargetY) > interactionRange)
        {
            if (MoveAgentTowards(person, goal.TargetX, goal.TargetY)) person.Agent.Fatigue = Math.Min(100, person.Agent.Fatigue + 0.22);
            person.Activity = goal.Kind == AgentGoalKind.Flee ? ResidentActivity.Fleeing : ResidentActivity.Wandering;
            return;
        }
        goal.WorkTicks++;
        switch (goal.Kind)
        {
            case AgentGoalKind.Eat:
                person.Activity = ResidentActivity.Eating;
                if (Distance(person.X, person.Y, home.X, home.Y) <= 1 && person.Inventory.Food < 1.2)
                {
                    var ration = Math.Min(home.Resources.Food, 1.2 - person.Inventory.Food);
                    home.Resources.Food -= ration; person.Inventory.Food += ration;
                    ObserveAgentEnvironment(person);
                }
                person.Agent.NextThinkTick = State.Tick + 1;
                break;
            case AgentGoalKind.Gather:
                GatherActualResources(person, Profession.Farmer);
                break;
            case AgentGoalKind.Work:
                if (TryWorkAtBuilding(person)) person.Activity = ResidentActivity.Working;
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
                break;
            case AgentGoalKind.ReturnHome:
                TransferPersonalProduction(person, home);
                person.Activity = ResidentActivity.Resting;
                person.Agent.Fatigue = Math.Max(0, person.Agent.Fatigue - 0.8);
                person.Agent.NextThinkTick = State.Tick + 1;
                break;
            case AgentGoalKind.Flee:
                person.Activity = ResidentActivity.Fleeing;
                person.Agent.NextThinkTick = State.Tick + 1;
                break;
        }
    }

    private void GatherActualResources(Resident person, Profession profession)
    {
        var index = Index(person.X, person.Y);
        var tile = State.Tiles[index];
        if (tile.FireTicks > 0 || ResourceSiteYield(index, profession) <= 0)
        {
            person.Agent.NextThinkTick = State.Tick + 1;
            return;
        }
        var productivity = (person.SicknessTicks > 0 ? 0.4 : 1) * (person.Hunger > 60 ? 0.55 : 1)
            * (0.75 + person.Agent.Personality.Diligence * 0.5);
        if (profession == Profession.Farmer)
        {
            var amount = Math.Min(tile.ResourceAmount, 0.7 * ResourceSiteYield(index, profession) * productivity * AgentFoodPolicyMultiplier(person));
            tile.ResourceAmount -= amount; person.Inventory.Food += amount;
        }
        else if (profession == Profession.Lumberjack)
        {
            var amount = Math.Min(tile.ResourceAmount, 0.28 * productivity * (person.Race == RaceKind.Elf ? 1.2 : 1));
            tile.ResourceAmount -= amount; person.Inventory.Wood += amount;
        }
        else
        {
            if (tile.Terrain is not TerrainType.Hills and not TerrainType.Snow)
                tile = Directions.Select(d => (X: person.X + d.X, Y: person.Y + d.Y))
                    .Where(p => InBounds(p.X, p.Y)).Select(p => State.Tiles[Index(p.X, p.Y)])
                    .FirstOrDefault(t => t.Terrain == TerrainType.Mountain && t.ResourceAmount > 0) ?? tile;
            var amount = Math.Min(tile.ResourceAmount, 0.24 * productivity * (person.Race == RaceKind.Dwarf ? 1.3 : 1));
            tile.ResourceAmount -= amount; person.Inventory.Stone += amount * 0.7; person.Inventory.Ore += amount * 0.3;
        }
        person.Agent.Fatigue = Math.Min(100, person.Agent.Fatigue + 0.45);
        person.Activity = ResidentActivity.Working;
    }

    /// <summary>Moves one grid cell using only terrain within six visible cells; no unsaved navigation cache.</summary>
    private bool MoveAgentTowards(Resident person, int targetX, int targetY)
    {
        if (!InBounds(targetX, targetY) || person.X == targetX && person.Y == targetY) return false;
        if (State.Tick - person.MoveStartedTick < person.MoveDurationTicks) return false;
        var startDistance = Distance(person.X, person.Y, targetX, targetY);
        var bestStep = -1;
        var bestDistance = startDistance;
        var previous = Index(person.FromX, person.FromY);
        foreach (var (dx, dy) in Directions)
        {
            var x = person.X + dx; var y = person.Y + dy;
            if (!Walkable(x, y) || State.Tiles[Index(x, y)].FireTicks > 0) continue;
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
            var queue = new Queue<(int Index, int First, int Depth)>();
            var seen = new HashSet<int> { start };
            queue.Enqueue((start, -1, 0));
            while (queue.TryDequeue(out var current))
            {
                if (current.Depth >= 6) continue;
                foreach (var (dx, dy) in Directions)
                {
                    var x = current.Index % State.Width + dx; var y = current.Index / State.Width + dy;
                    if (!Walkable(x, y) || State.Tiles[Index(x, y)].FireTicks > 0) continue;
                    var index = Index(x, y);
                    if (current.First < 0 && index == previous) continue;
                    if (!seen.Add(index)) continue;
                    var first = current.First < 0 ? index : current.First;
                    var distance = Distance(x, y, targetX, targetY);
                    if (distance < bestDistance) { bestDistance = distance; bestStep = first; }
                    queue.Enqueue((index, first, current.Depth + 1));
                }
            }
        }
        if (bestStep < 0)
        {
            // When the visible horizon cannot yet improve distance, follow an obstacle edge instead
            // of oscillating between the last two cells or standing still across a small lake.
            var alternatives = Directions.Select(d => (X: person.X + d.X, Y: person.Y + d.Y, DX: d.X, DY: d.Y))
                .Where(p => Walkable(p.X, p.Y) && State.Tiles[Index(p.X, p.Y)].FireTicks == 0).ToArray();
            var forward = alternatives.Where(p => Index(p.X, p.Y) != previous).ToArray();
            if (forward.Length == 0) forward = alternatives;
            if (forward.Length > 0)
            {
                var next = forward.OrderByDescending(p => p.DX * (targetX - person.X) + p.DY * (targetY - person.Y)).First();
                bestStep = Index(next.X, next.Y);
            }
        }
        if (bestStep < 0) return false;
        var xNext = bestStep % State.Width; var yNext = bestStep / State.Width;
        var speed = person.Agent.DestinationSettlementId == 0 ? 1 / GetTerrainMoveCost(xNext, yNext)
            : MessageTravelMultiplier(xNext, yNext, person.NationId);
        var duration = Math.Clamp((int)Math.Round(2 / Math.Max(0.1, speed)), 1, 8);
        person.FromX = person.X; person.FromY = person.Y;
        person.X = bestStep % State.Width; person.Y = bestStep / State.Width;
        person.MoveStartedTick = State.Tick; person.MoveDurationTicks = duration;
        return true;
    }

    private double AgentFoodPolicyMultiplier(Resident person)
    {
        if (!_settlements.TryGetValue(person.SettlementId, out var home)) return 1;
        if (Distance(person.X, person.Y, home.X, home.Y) <= 3) return GetPolicyProductionMultiplier(home.Id);
        var instruction = person.Agent.Memory.Where(f => f.Kind == AgentFactKind.Policy && f.SubjectId == home.Id
            && AgentFactReliability(f) >= 0.5).OrderByDescending(f => f.ObservedTick).FirstOrDefault();
        return instruction?.Value == (int)PolicyKind.FoodSecurity ? 1.25 : 1;
    }
}
