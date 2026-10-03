namespace SeWZC.WorldBox.Core;

public sealed partial class WorldEngine
{
    // Rebuilt within the communication phase; never used as authoritative world state.
    private readonly Dictionary<int, Resident> _communicationPeople = [];
    private readonly List<PendingMessage> _messagesToDeliver = [];
    private readonly List<Resident> _conversationNeighbors = [];
    private readonly List<int> _conversationTiles = [];
    private int[] _conversationHeads = [];
    private int[] _conversationNext = [];
    private readonly List<AgentFact> _observedReports = [];
    private readonly List<AgentFact> _missionAddresses = [];
    private static readonly Comparison<Resident> ResidentIdOrder = (first, second) => first.Id.CompareTo(second.Id);

    private AgentFact MakeAgentFact(Resident observer, AgentFactKind kind, int subject, int x, int y, double value, string text) => new()
    {
        Id = NewId(), Kind = kind, SubjectId = subject, X = x, Y = y, Value = value,
        ObservedTick = State.Tick, LearnedTick = State.Tick,
        OriginResidentId = observer.Id, SourceResidentId = observer.Id, OriginProfession = observer.Profession, Confidence = 1, Text = text
    };

    private static AgentFact CopyAgentFact(AgentFact fact) => new()
    {
        Id = fact.Id, EventId = fact.EventId, CampaignEventId = fact.CampaignEventId, WarObjective = fact.WarObjective, Kind = fact.Kind, SubjectId = fact.SubjectId, TargetNationId = fact.TargetNationId, X = fact.X, Y = fact.Y, Value = fact.Value,
        ObservedTick = fact.ObservedTick, LearnedTick = fact.LearnedTick,
        OriginResidentId = fact.OriginResidentId, SourceResidentId = fact.SourceResidentId,
        OriginProfession = fact.OriginProfession,
        Confidence = fact.Confidence, Hops = fact.Hops, Text = fact.Text
    };

    private double AgentFactReliability(AgentFact fact)
    {
        var lifetime = fact.Kind switch
        {
            AgentFactKind.Danger => 24d,
            AgentFactKind.FoodSupply or AgentFactKind.ReliefRequest => 180d,
            AgentFactKind.SettlementLocation => 1200d,
            _ => 600d
        };
        return Math.Clamp(fact.Confidence, 0, 1) * Math.Clamp(1 - (State.Tick - fact.ObservedTick) / lifetime, 0, 1);
    }

    private void RememberAgentFact(Resident person, AgentFact fact, bool copy = true)
    {
        var memory = person.Agent.Memory;
        for (var i = 0; i < memory.Count; i++)
        {
            var old = memory[i];
            if (old.Kind != fact.Kind || old.SubjectId != fact.SubjectId || old.TargetNationId != fact.TargetNationId
                || (fact.Kind is AgentFactKind.Danger or AgentFactKind.Personal) && (old.X != fact.X || old.Y != fact.Y)) continue;
            if (old.ObservedTick > fact.ObservedTick) return;
            if (old.ObservedTick == fact.ObservedTick)
            {
                // Distinct orders issued on the same day still have an authoritative order.
                // A relayed newer command must replace an older command of the same kind,
                // even when the older copy was heard directly with greater confidence.
                if (fact.Kind is AgentFactKind.WarOrder or AgentFactKind.PeaceOrder && old.Id != fact.Id)
                { if (old.Id > fact.Id) return; }
                else if (old.Confidence >= fact.Confidence) return;
            }
            memory.RemoveAt(i);
            break;
        }
        // A fresh observation or delivery copy may transfer ownership; shared evidence must be copied.
        memory.Add(copy ? CopyAgentFact(fact) : fact);
        if (memory.Count <= 16) return;
        var forgotten = 0;
        var lowestPriority = MemoryRetentionPriority(memory[0], person.SettlementId);
        for (var i = 1; i < memory.Count; i++)
        {
            var priority = MemoryRetentionPriority(memory[i], person.SettlementId);
            if (priority >= lowestPriority) continue;
            forgotten = i; lowestPriority = priority;
        }
        memory.RemoveAt(forgotten);
    }

    private static long MemoryRetentionPriority(AgentFact fact, int homeId) =>
        (fact.Kind == AgentFactKind.SettlementLocation && fact.SubjectId == homeId ? 100000 : 0)
        + (fact.Kind is AgentFactKind.Research or AgentFactKind.Policy ? 60 : 0) + fact.LearnedTick;

    private void ObserveAgentEnvironment(Resident person)
    {
        foreach (var town in State.Settlements)
        {
            if (Distance(person.X, person.Y, town.X, town.Y) > 3) continue;
            RememberAgentFact(person, MakeAgentFact(person, AgentFactKind.SettlementLocation,
                town.Id, town.X, town.Y, town.NationId, $"见到聚落 {town.Name}"), copy: false);
            if (Distance(person.X, person.Y, town.X, town.Y) > 1) continue;
            RememberAgentFact(person, MakeAgentFact(person, AgentFactKind.FoodSupply,
                town.Id, town.X, town.Y, town.Resources.Food, $"在{town.Name}粮仓见到 {town.Resources.Food:0.0} 份粮食"), copy: false);
            if (town.CultureId > 0)
                RememberAgentFact(person, MakeAgentFact(person, AgentFactKind.Culture,
                    town.Id, town.X, town.Y, town.CultureId, $"在{town.Name}接触当地文化"), copy: false);
            _observedReports.Clear();
            foreach (var report in town.PublicKnowledge)
            {
                var at = 0;
                while (at < _observedReports.Count && _observedReports[at].LearnedTick >= report.LearnedTick) at++;
                if (at >= 6) continue;
                _observedReports.Insert(at, report);
                if (_observedReports.Count > 6) _observedReports.RemoveAt(6);
            }
            foreach (var report in _observedReports)
            {
                if (report.LearnedTick >= State.Tick) continue;
                var learned = CopyAgentFact(report);
                learned.LearnedTick = State.Tick;
                learned.Hops = Math.Min(32, learned.Hops + 1);
                learned.Confidence *= 0.98;
                RememberAgentFact(person, learned, copy: false);
                if (learned.Kind is AgentFactKind.Research or AgentFactKind.Policy or AgentFactKind.Culture or AgentFactKind.DiplomaticNotice)
                    ReceiveSocietyReport(town, person, learned);
            }
            if (town.Id == person.SettlementId && person.Hunger > 35 && town.Resources.Food < 12)
                RememberAgentFact(person, MakeAgentFact(person, AgentFactKind.ReliefRequest,
                    town.Id, town.X, town.Y, person.Hunger, $"亲历饥饿 {person.Hunger:0}，家园粮少，请求救济"), copy: false);
        }
        var dangerIndex = Circle(person.X, person.Y, 3).Where(i => State.Tiles[i].FireTicks > 0)
            .OrderBy(i => Distance(person.X, person.Y, i % State.Width, i / State.Width)).FirstOrDefault(-1);
        if (dangerIndex >= 0)
            RememberAgentFact(person, MakeAgentFact(person, AgentFactKind.Danger,
                0, dangerIndex % State.Width, dangerIndex / State.Width,
                State.Tiles[dangerIndex].FireTicks, "亲眼看见正在燃烧的土地"), copy: false);
        foreach (var army in State.Armies)
            if (army.NationId != person.NationId && Distance(person.X, person.Y, army.X, army.Y) <= 4)
                RememberAgentFact(person, MakeAgentFact(person, AgentFactKind.Danger,
                    army.Id, army.X, army.Y, army.Soldiers, "亲眼看见附近有外来军队"), copy: false);
    }

    private void UpdateLocalCommunication()
    {
        var people = _communicationPeople;
        people.Clear();
        foreach (var person in State.Residents) people.Add(person.Id, person);
        // Delivery happens before this tick's conversations. Newly learned facts cannot be forwarded yet.
        _messagesToDeliver.Clear();
        var remaining = 0;
        for (var i = 0; i < State.PendingMessages.Count; i++)
        {
            var message = State.PendingMessages[i];
            if (message.DeliverTick <= State.Tick) _messagesToDeliver.Add(message);
            else State.PendingMessages[remaining++] = message;
        }
        State.PendingMessages.RemoveRange(remaining, State.PendingMessages.Count - remaining);
        foreach (var message in _messagesToDeliver)
        {
            if (!people.TryGetValue(message.RecipientId, out var recipient)) continue;
            if (message.TargetSettlementId != 0
                && (!_settlements.TryGetValue(message.TargetSettlementId, out var endpoint)
                    || Distance(recipient.X, recipient.Y, endpoint.X, endpoint.Y) > 2
                    || !people.TryGetValue(message.SenderId, out var stationSender)
                    || !CanRelayInformation(stationSender.SettlementId, endpoint.Id, out _))) continue;
            foreach (var fact in message.Facts)
            {
                var received = CopyAgentFact(fact);
                received.LearnedTick = State.Tick; received.SourceResidentId = message.SenderId;
                received.Hops = Math.Min(32, fact.Hops + 1); received.Confidence *= 0.96;
                RememberAgentFact(recipient, received, copy: false);
                if (_settlements.TryGetValue(recipient.SettlementId, out var home)
                    && Distance(recipient.X, recipient.Y, home.X, home.Y) <= 1
                    && (recipient.Profession == Profession.Representative || home.RepresentativeId == recipient.Id
                        || received.Kind is AgentFactKind.Research or AgentFactKind.Policy or AgentFactKind.Culture))
                    ReceiveSocietyReport(home, recipient, received);
            }
        }
        _messagesToDeliver.Clear();
        if (_conversationHeads.Length != State.Tiles.Length) _conversationHeads = new int[State.Tiles.Length];
        else foreach (var tile in _conversationTiles) _conversationHeads[tile] = 0;
        _conversationTiles.Clear();
        if (_conversationNext.Length < State.Residents.Count) _conversationNext = new int[MaxPopulation];
        for (var i = 0; i < State.Residents.Count; i++)
        {
            var tile = Index(State.Residents[i].X, State.Residents[i].Y);
            if (_conversationHeads[tile] == 0) _conversationTiles.Add(tile);
            _conversationNext[i] = _conversationHeads[tile];
            _conversationHeads[tile] = i + 1;
        }
        foreach (var sender in State.Residents)
        {
            if ((State.Tick + sender.Id) % 12 != 0 || State.Tick - sender.Agent.LastConversationTick < 6
                || sender.Health <= 0) continue;
            var neighbors = _conversationNeighbors;
            neighbors.Clear();
            foreach (var tile in Circle(sender.X, sender.Y, 2))
                for (var at = _conversationHeads[tile]; at != 0; at = _conversationNext[at - 1])
                {
                    var neighbor = State.Residents[at - 1];
                    if (neighbor.Id != sender.Id && neighbor.Health > 0) neighbors.Add(neighbor);
                }
            if (neighbors.Count == 0) continue;
            neighbors.Sort(ResidentIdOrder);
            var recipient = neighbors[(int)((State.Tick / 12 + sender.Id) % neighbors.Count)];
            var facts = SelectMessageFacts(sender, relay: false);
            if (facts.Count > 0 && State.PendingMessages.Count < MaxPopulation * 2)
                State.PendingMessages.Add(new PendingMessage { SenderId = sender.Id, RecipientId = recipient.Id, DeliverTick = State.Tick + 1, Facts = facts });
            sender.Agent.LastConversationTick = State.Tick;
            sender.Agent.SocialNeed = Math.Max(0, sender.Agent.SocialNeed - 14);
            recipient.Agent.SocialNeed = Math.Max(0, recipient.Agent.SocialNeed - 10);
            ExchangeCulture(sender, recipient);
            if (sender.Agent.Goal.Kind == AgentGoalKind.Socialize) sender.Activity = ResidentActivity.Talking;
        }
        RelayKnownAgentMessages();
        // Keep capacities, but do not retain residents that may die in the following warfare phase.
        people.Clear();
        _conversationNeighbors.Clear();
    }

    private void RelayKnownAgentMessages()
    {
        foreach (var sender in State.Residents)
        {
            if (sender.Profession is not Profession.Messenger and not Profession.Representative
                || (State.Tick + sender.Id) % 24 != 0 || !_settlements.TryGetValue(sender.SettlementId, out var home)
                || Distance(sender.X, sender.Y, home.X, home.Y) > 1) continue;
            foreach (var address in sender.Agent.Memory.Where(f => f.Kind == AgentFactKind.SettlementLocation
                && f.SubjectId != home.Id && f.LearnedTick < State.Tick).Take(3))
            {
                if (!CanRelayInformation(home.Id, address.SubjectId, out var travelTicks)
                    || !_settlements.TryGetValue(address.SubjectId, out var destination)) continue;
                var recipient = State.Residents.Where(r => r.SettlementId == destination.Id
                    && Distance(r.X, r.Y, destination.X, destination.Y) <= 2)
                    .OrderByDescending(r => r.Id == destination.RepresentativeId).ThenBy(r => r.Id).FirstOrDefault();
                if (recipient is null) continue;
                var facts = SelectMessageFacts(sender, relay: true);
                if (facts.Count == 0 || State.PendingMessages.Count >= MaxPopulation * 2) continue;
                State.PendingMessages.Add(new PendingMessage { SenderId = sender.Id, RecipientId = recipient.Id,
                    TargetSettlementId = destination.Id, DeliverTick = State.Tick + Math.Max(1, travelTicks), Facts = facts });
            }
        }
    }

    private List<AgentFact> SelectMessageFacts(Resident sender, bool relay)
    {
        AgentFact? first = null, second = null, third = null;
        foreach (var fact in sender.Agent.Memory)
        {
            if (fact.LearnedTick >= State.Tick || fact.Confidence <= (relay ? 0.25 : 0.15) || !relay && fact.Hops >= 12) continue;
            if (first is null || MessageFactPrecedes(fact, first, relay)) { third = second; second = first; first = fact; }
            else if (second is null || MessageFactPrecedes(fact, second, relay)) { third = second; second = fact; }
            else if (third is null || MessageFactPrecedes(fact, third, relay)) third = fact;
        }
        var selected = new List<AgentFact>(3);
        if (first is not null) selected.Add(CopyAgentFact(first));
        if (second is not null) selected.Add(CopyAgentFact(second));
        if (third is not null) selected.Add(CopyAgentFact(third));
        return selected;
    }

    private static bool MessageFactPrecedes(AgentFact candidate, AgentFact current, bool relay)
    {
        static bool Priority(AgentFact fact, bool relay) => relay
            ? fact.Kind is AgentFactKind.WarReport or AgentFactKind.DiplomaticNotice or AgentFactKind.WarOrder or AgentFactKind.PeaceOrder or AgentFactKind.ReliefRequest or AgentFactKind.Research
            : fact.Kind is AgentFactKind.WarReport or AgentFactKind.DiplomaticNotice or AgentFactKind.WarOrder or AgentFactKind.PeaceOrder or AgentFactKind.ReliefRequest or AgentFactKind.Research or AgentFactKind.Danger;
        var candidatePriority = Priority(candidate, relay); var currentPriority = Priority(current, relay);
        return candidatePriority != currentPriority ? candidatePriority : candidate.ObservedTick > current.ObservedTick;
    }

    private void AddAgentMissionChoices(Resident person, Settlement home, List<GoalChoice> choices)
    {
        var agent = person.Agent;
        if (person.Age < 16 || State.Tick < agent.MissionRetryTick) return;
        if (agent.DestinationSettlementId != 0 && State.Tick - agent.MissionStartedTick < 360)
        {
            choices.Add(new(agent.Goal.Kind, agent.Goal.TargetX, agent.Goal.TargetY, 72,
                "继续完成正在亲自递送的任务", agent.CarriedMessages.FirstOrDefault(), agent.DestinationSettlementId));
            return;
        }
        AgentFact? relief = null;
        foreach (var fact in agent.Memory)
            if (fact.Kind == AgentFactKind.ReliefRequest && fact.Value >= 35 && AgentFactReliability(fact) > 0.25
                && State.Tick - fact.ObservedTick < 180 && (relief is null || fact.ObservedTick > relief.ObservedTick)) relief = fact;
        if (relief is not null)
        {
            var destination = home.Id;
            if (_nations.TryGetValue(person.NationId, out var nation)
                && agent.Memory.Any(f => f.Kind == AgentFactKind.SettlementLocation && f.SubjectId == nation.CapitalId)) destination = nation.CapitalId;
            var address = agent.Memory.FirstOrDefault(f => f.Kind == AgentFactKind.SettlementLocation && f.SubjectId == destination);
            if (address is not null) choices.Add(new(AgentGoalKind.Petition, address.X, address.Y,
                (person.Profession == Profession.Representative ? 88 : 35 + agent.Personality.Courage * 20) * AgentFactReliability(relief),
                "带着已收到且可信的缺粮报告，去向本地代表请求救济", relief, destination));
        }
        if (person.Profession is Profession.Trader or Profession.Messenger or Profession.Representative)
        {
            var addresses = _missionAddresses;
            addresses.Clear();
            foreach (var fact in agent.Memory)
            {
                if (fact.Kind != AgentFactKind.SettlementLocation || fact.SubjectId == home.Id || State.Tick - fact.ObservedTick >= 1200) continue;
                var distance = Distance(person.X, person.Y, fact.X, fact.Y);
                var at = 0;
                while (at < addresses.Count && Distance(person.X, person.Y, addresses[at].X, addresses[at].Y) <= distance) at++;
                addresses.Insert(at, fact);
            }
            for (var i = 0; i < Math.Min(3, addresses.Count); i++)
            {
                var address = addresses[i];
                if (person.Profession == Profession.Trader)
                {
                    if (!State.Rules.Trade) continue;
                    if (IsKnownHostile(person, (int)address.Value)) continue;
                    var knownFood = LatestAgentFact(agent.Memory, AgentFactKind.FoodSupply, address.SubjectId);
                    var ownFood = LatestAgentFact(agent.Memory, AgentFactKind.FoodSupply, home.Id);
                    if (ownFood is null || ownFood.Value < 50 || AgentFactReliability(ownFood) < 0.25
                        || knownFood is not null && knownFood.Value >= ownFood.Value * 0.7 && AgentFactReliability(knownFood) > 0.5) continue;
                    var reliability = AgentFactReliability(address) * (knownFood is null ? 0.75 : AgentFactReliability(knownFood));
                    if (reliability < 0.4)
                        choices.Add(new(AgentGoalKind.DeliverMessage, address.X, address.Y, 24 + agent.Personality.Ambition * 9,
                            "外地粮情过旧或来源存疑，先亲自核实，暂不据此装货", knownFood ?? address, address.SubjectId));
                    else choices.Add(new(AgentGoalKind.Trade, home.X, home.Y, (61 + agent.Personality.Ambition * 8) * reliability,
                        knownFood is null ? "已知聚落地址且家乡有余粮，带货亲自探访" : "有可信且尚新的聚落粮情，先在家园装粮，再亲自交换",
                        knownFood ?? address, address.SubjectId));
                }
                else choices.Add(new(AgentGoalKind.DeliverMessage, address.X, address.Y,
                    (59 + agent.Personality.Sociability * 8) * AgentFactReliability(address),
                    "带着自己已知的消息，拜访记忆中另一座聚落", address, address.SubjectId));
            }
            if (addresses.Count == 0)
            {
                var heading = Directions[(person.Id + (int)(State.Tick / 360)) % Directions.Length];
                var frontier = Circle(person.X, person.Y, 6).Where(i => State.Tiles[i].IsWalkable && State.Tiles[i].FireTicks == 0)
                    .OrderByDescending(i => (i % State.Width - person.X) * heading.X + (i / State.Width - person.Y) * heading.Y)
                    .ThenByDescending(i => Distance(i % State.Width, i / State.Width, home.X, home.Y)).FirstOrDefault(-1);
                if (frontier >= 0) choices.Add(new(AgentGoalKind.Idle, frontier % State.Width, frontier / State.Width,
                    42, "还不知道远方聚落，沿眼前可通行的土地探索"));
            }
        }
    }

    private void BeginAgentMission(Resident person, Settlement home)
    {
        var agent = person.Agent;
        agent.DestinationSettlementId = agent.Goal.TargetSettlementId;
        agent.MissionOriginSettlementId = home.Id;
        agent.MissionStartedTick = State.Tick;
        agent.CarriedMessages.Clear();
        if (agent.Goal.Kind != AgentGoalKind.Trade)
        {
            agent.CarriedMessages = agent.Memory.OrderByDescending(f => f.Kind is AgentFactKind.WarReport or AgentFactKind.DiplomaticNotice or AgentFactKind.WarOrder or AgentFactKind.PeaceOrder or AgentFactKind.ReliefRequest ? 1 : 0)
                .ThenByDescending(f => f.ObservedTick).Take(8).Select(CopyAgentFact).ToList();
            if (Distance(person.X, person.Y, home.X, home.Y) <= 1)
            {
                var ration = Math.Min(home.Resources.Food, Math.Max(0,
                    1.2 + Distance(home.X, home.Y, agent.Goal.TargetX, agent.Goal.TargetY) * 0.22 - person.Inventory.Food));
                home.Resources.Food -= ration; person.Inventory.Food += ration;
            }
        }
    }

    private void ActOnAgentMission(Resident person, Settlement home)
    {
        var agent = person.Agent;
        var goal = agent.Goal;
        var address = agent.Memory.FirstOrDefault(f => f.Kind == AgentFactKind.SettlementLocation && f.SubjectId == agent.DestinationSettlementId);
        if (address is null || State.Tick - agent.MissionStartedTick >= 360)
        {
            FinishAgentMission(person, address is null ? "缺少可靠目的地地址，暂缓递送" : "长时间未能到达，暂缓递送并返乡补给");
            return;
        }
        // A distant declaration is not knowledge. Only an order actually received by this
        // merchant can stop a journey towards the nation in its remembered address.
        if (goal.Kind == AgentGoalKind.Trade && IsKnownHostile(person, (int)address.Value))
        {
            FinishAgentMission(person, "已获知目的地国家敌对，停止交易并携带现有物资返乡");
            return;
        }
        if (goal.Kind == AgentGoalKind.Trade && agent.CarriedMessages.Count == 0)
        {
            if (Distance(person.X, person.Y, home.X, home.Y) > 1)
            {
                MoveAgentTowards(person, home.X, home.Y);
                person.Activity = ResidentActivity.Delivering;
                return;
            }
            var distance = Distance(home.X, home.Y, address.X, address.Y);
            // Recheck the warehouse on arrival: earlier surplus reports may be stale, and
            // earlier merchants may already have loaded. Keep a modest local food reserve.
            var surplus = Math.Max(0, home.Resources.Food - Math.Max(12, home.Population));
            var cargo = Math.Min(surplus, Math.Max(0, Math.Min(40, 8 + distance * 0.22) - person.Inventory.Food));
            if (cargo <= 1)
            {
                FinishAgentMission(person, "抵达粮仓后发现没有可装运余粮，取消交易");
                return;
            }
            home.Resources.Food -= cargo; person.Inventory.Food += cargo;
            agent.CarriedMessages = agent.Memory.OrderByDescending(f => f.ObservedTick).Take(8).Select(CopyAgentFact).ToList();
            goal.TargetX = address.X; goal.TargetY = address.Y;
        }
        PrepareJourneyTransport(person, home);
        if (Distance(person.X, person.Y, goal.TargetX, goal.TargetY) > 1 || !Walkable(person.X, person.Y))
        {
            MoveAgentTowards(person, goal.TargetX, goal.TargetY);
            person.Activity = ResidentActivity.Delivering;
            return;
        }
        if (!_settlements.TryGetValue(agent.DestinationSettlementId, out var destination)
            || Distance(person.X, person.Y, destination.X, destination.Y) > 1)
        {
            agent.Memory.RemoveAll(f => f.Kind == AgentFactKind.SettlementLocation && f.SubjectId == agent.DestinationSettlementId);
            FinishAgentMission(person, "抵达记忆中的地址，却未见原聚落");
            return;
        }
        // Ownership can have changed en route; its current identity is visible only here.
        if (goal.Kind == AgentGoalKind.Trade && IsKnownHostile(person, destination.NationId))
        {
            FinishAgentMission(person, "抵达后发现聚落属于已知敌对国家，保留货物返乡");
            return;
        }
        goal.WorkTicks++;
        if (goal.WorkTicks < 3) { person.Activity = ResidentActivity.Talking; return; }
        WorldEvent? tradeEvent = null;
        var completionReason = goal.Kind == AgentGoalKind.Petition ? "意见已当面交给聚落代表" : "已实际抵达目的地并递送所携消息";
        if (goal.Kind == AgentGoalKind.Trade)
        {
            const double woodPerFood = 0.4;
            var reserve = 1.2 + Distance(destination.X, destination.Y, home.X, home.Y) * 0.11;
            var availableFood = Math.Max(0, person.Inventory.Food - reserve);
            var food = Math.Min(availableFood, Math.Min(destination.Resources.Wood / woodPerFood,
                Math.Min(Math.Max(0, 1_000_000 - destination.Resources.Food), Math.Max(0, 1_000_000 - person.Inventory.Wood) / woodPerFood)));
            var payment = food * woodPerFood;
            person.Inventory.Food -= food; destination.Resources.Food += food;
            destination.Resources.Wood = Math.Max(0, destination.Resources.Wood - payment); person.Inventory.Wood += payment;
            if (food > 0) tradeEvent = AddEvent(WorldEventKind.Trade,
                $"{person.Name}抵达{destination.Name}，交付 {food:0.0} 份粮食，携带 {payment:0.0} 份木材返乡。", destination.X, destination.Y, EventAction.Delivery, destination.Id, person.Id);
            if (tradeEvent is not null) { tradeEvent.SecondNationId = person.NationId; tradeEvent.SecondSettlementId = home.Id; }
            completionReason = food > 0
                ? $"实际交换 {food:0.0} 份粮食与 {payment:0.0} 份木材，携带所得木材及剩余粮食返乡"
                : availableFood <= 0 ? "剩余粮食需作返程口粮，没有可交换货物，返乡补给"
                : destination.Resources.Wood <= 0 ? "当地没有可支付的木材，保留货物返乡"
                : "当地粮仓或随身木材已达容量上限，保留货物返乡";
        }
        if (tradeEvent is not null && destination.NationId != person.NationId)
        {
            var outbound = MakeAgentFact(person, AgentFactKind.TradeExchange, person.NationId, destination.X, destination.Y, 1, "商旅实际抵达并完成粮木交换");
            outbound.EventId = tradeEvent.Id;
            AddPublicFact(destination, outbound);
            var inbound = MakeAgentFact(person, AgentFactKind.TradeExchange, destination.NationId, destination.X, destination.Y, 1, "我与另一国家的聚落完成交易，返乡后可报告");
            inbound.EventId = tradeEvent.Id; RememberAgentFact(person, inbound);
        }
        foreach (var fact in agent.CarriedMessages.ToArray())
        {
            var delivered = CopyAgentFact(fact);
            delivered.LearnedTick = State.Tick; delivered.SourceResidentId = person.Id;
            delivered.Hops = Math.Min(32, fact.Hops + 1); delivered.Confidence *= 0.98;
            AddPublicFact(destination, delivered);
            ReceiveSocietyReport(destination, person, delivered);
        }
        if (destination.PublicKnowledge.Count > 24)
            destination.PublicKnowledge = destination.PublicKnowledge.OrderByDescending(f => f.LearnedTick).Take(24).ToList();
        ObserveAgentEnvironment(person);
        FinishAgentMission(person, completionReason);
    }

    private void FinishAgentMission(Resident person, string reason)
    {
        var agent = person.Agent;
        var goal = agent.Goal;
        agent.Decisions.Add(new AgentDecision
        {
            Tick = State.Tick, Goal = AgentGoalKind.ReturnHome, Reason = reason,
            Score = 80, KnowledgeObservedTick = State.Tick, SourceResidentId = person.Id
        });
        if (agent.Decisions.Count > 6) agent.Decisions.RemoveAt(0);
        agent.DestinationSettlementId = 0; agent.CarriedMessages.Clear();
        agent.MissionRetryTick = State.Tick + 90;
        if (_settlements.TryGetValue(person.SettlementId, out var home))
            agent.Goal = new AgentGoal { Kind = AgentGoalKind.ReturnHome, TargetX = home.X, TargetY = home.Y,
                TargetSettlementId = home.Id, StartedTick = State.Tick, ReviewTick = State.Tick + 12, Reason = reason };
        else goal.Kind = AgentGoalKind.Idle;
        agent.NextThinkTick = State.Tick + 12;
    }
}
