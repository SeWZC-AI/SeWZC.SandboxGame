using System.Numerics;
using System.Runtime.InteropServices;

namespace SeWZC.WorldBox.Core;

public sealed partial class WorldEngine
{
    private static readonly Comparison<Resident> ResidentIdOrder = (first, second) => first.Id.CompareTo(second.Id);

    // 通信索引只在本阶段有效，不能替代世界中的权威居民状态。
    private readonly Dictionary<int, Resident> _communicationPeople = [];
    private readonly List<Resident> _conversationNeighbors = [];
    private readonly List<int> _conversationTiles = [];
    private readonly List<PendingMessage> _messagesToDeliver = [];
    private readonly List<AgentFact> _missionAddresses = [];
    private readonly List<AgentFact> _observedReports = [];
    private int[] _conversationHeads = [];
    private int[] _conversationNext = [];

    private AgentFact MakeAgentFact(Resident observer, AgentFactKind kind, int subject, int x, int y, double value,
        string text)
    {
        return new AgentFact
        {
            Id = NewId(),
            Kind = kind,
            SubjectId = subject,
            X = x,
            Y = y,
            Value = value,
            ObservedTick = State.Tick,
            LearnedTick = State.Tick,
            OriginResidentId = observer.Id,
            SourceResidentId = observer.Id,
            OriginProfession = observer.Profession,
            Confidence = 1,
            Text = text,
        };
    }

    /// <summary>复制信息记录，保留观察编号、来源和时间戳。</summary>
    /// <param name="fact">需要复制并保留来源的信息记录。</param>
    private static AgentFact CopyAgentFact(AgentFact fact)
    {
        return new AgentFact
        {
            Id = fact.Id,
            EventId = fact.EventId,
            CampaignEventId = fact.CampaignEventId,
            WarObjective = fact.WarObjective,
            Kind = fact.Kind,
            SubjectId = fact.SubjectId,
            TargetNationId = fact.TargetNationId,
            X = fact.X,
            Y = fact.Y,
            Value = fact.Value,
            ObservedTick = fact.ObservedTick,
            LearnedTick = fact.LearnedTick,
            OriginResidentId = fact.OriginResidentId,
            SourceResidentId = fact.SourceResidentId,
            OriginProfession = fact.OriginProfession,
            Confidence = fact.Confidence,
            Hops = fact.Hops,
            Text = fact.Text,
        };
    }

    /// <summary>根据原始观察时间和议题有效期，计算衰减后的报告可信度。</summary>
    /// <param name="fact">需要按观察时间评估的信息记录。</param>
    private double AgentFactReliability(AgentFact fact)
    {
        var lifetime = fact.Kind switch
        {
            AgentFactKind.FoodSupply or AgentFactKind.ReliefRequest => 180d,
            AgentFactKind.Danger => 24d,
            AgentFactKind.SettlementLocation => 1200d,
            _ => 600d,
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
                || (fact.Kind is AgentFactKind.Danger or AgentFactKind.Personal &&
                    (old.X != fact.X || old.Y != fact.Y))) continue;
            if (old.ObservedTick > fact.ObservedTick) return;
            if (old.ObservedTick == fact.ObservedTick)
            {
                // 同日军令仍有编号顺序；新军令必须替换旧军令，不能因旧副本为亲闻或更可信而拒绝更新。
                if (fact.Kind is AgentFactKind.WarOrder or AgentFactKind.PeaceOrder && old.Id != fact.Id)
                {
                    if (old.Id > fact.Id) return;
                }
                else if (old.Confidence >= fact.Confidence) return;
            }

            memory.RemoveAt(i);
            break;
        }

        // 共享信息需复制，独立的观察和消息副本可直接交给接收者。
        memory.Add(copy ? CopyAgentFact(fact) : fact);
        if (memory.Count <= 16) return;
        var forgotten = 0;
        var lowestPriority = MemoryRetentionPriority(memory[0], person.SettlementId);
        for (var i = 1; i < memory.Count; i++)
        {
            var priority = MemoryRetentionPriority(memory[i], person.SettlementId);
            if (priority >= lowestPriority) continue;
            forgotten = i;
            lowestPriority = priority;
        }

        memory.RemoveAt(forgotten);
    }

    private static long MemoryRetentionPriority(AgentFact fact, int homeId)
    {
        return (fact.Kind == AgentFactKind.SettlementLocation && fact.SubjectId == homeId ? 100000 : 0)
               + (fact.Kind is AgentFactKind.Policy or AgentFactKind.Research ? 60 : 0) + fact.LearnedTick;
    }

    /// <summary>将附近的观察和可接触的公开报告记录到该居民自己的记忆中。</summary>
    /// <param name="person">观察信息的居民。</param>
    private void ObserveAgentEnvironment(Resident person)
    {
        if (State.Rules.Expansion && person.Profession is Profession.Builder or Profession.Trader
                                      or Profession.Messenger
                                  && _settlements.TryGetValue(person.SettlementId, out var camp) &&
                                  !camp.FoundationPending
                                  && Distance(person.X, person.Y, camp.X, camp.Y) >= MinimumSettlementDistance - 6)
        {
            var site = Circle(person.X, person.Y, 3).Where(i => RaceTerrainRules.CanWalk(State.Tiles[i], person.Race)
                                                                && !IsWaterTerrain(State.Tiles[i].Terrain) &&
                                                                State.Tiles[i].Fertility >= 40
                                                                && State.Tiles[i].ClaimedSettlementId == 0 &&
                                                                State.Tiles[i].FireTicks == 0
                                                                && FoundingSiteSuitable(i, [person.Race])
                                                                && State.Settlements.All(t =>
                                                                    Distance(t.X, t.Y, i % State.Width,
                                                                        i / State.Width) >= MinimumSettlementDistance))
                .OrderByDescending(i => State.Tiles[i].Fertility).ThenBy(i => i).FirstOrDefault(-1);
            if (site >= 0 && !person.Agent.Memory.Any(f =>
                    f.Kind == AgentFactKind.FoundingSite && State.Tick - f.ObservedTick < 120))
            {
                RememberAgentFact(person, MakeAgentFact(person, AgentFactKind.FoundingSite, camp.Id, site % State.Width,
                        site / State.Width,
                        State.Tiles[site].Fertility,
                        $"亲眼勘察到符合间距、周围有至少 {SettlementActivationArea} 格可用陆地的建村地块，肥力 {State.Tiles[site].Fertility}/100，需带回报告"),
                    false);
            }
        }

        foreach (var town in State.Settlements)
        {
            if (Distance(person.X, person.Y, town.X, town.Y) > 3) continue;
            RememberAgentFact(person, MakeAgentFact(person, AgentFactKind.SettlementLocation,
                town.Id, town.X, town.Y, town.NationId, $"见到聚落 {town.Name}"), false);
            if (Distance(person.X, person.Y, town.X, town.Y) > 1) continue;
            RememberAgentFact(person, MakeAgentFact(person, AgentFactKind.FoodSupply,
                    town.Id, town.X, town.Y, town.Resources.Food, $"在{town.Name}粮仓见到 {town.Resources.Food:0.0} 份粮食"),
                false);
            if (town.CultureId > 0)
            {
                RememberAgentFact(person, MakeAgentFact(person, AgentFactKind.Culture,
                    town.Id, town.X, town.Y, town.CultureId, $"在{town.Name}接触当地文化"), false);
            }

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
                RememberAgentFact(person, learned, false);
                if (learned.Kind is AgentFactKind.Policy or AgentFactKind.Culture or AgentFactKind.Research
                    or AgentFactKind.DiplomaticNotice)
                    ReceiveSocietyReport(town, person, learned);
            }

            if (town.Id == person.SettlementId && person.Hunger > 35 && town.Resources.Food < 12)
            {
                RememberAgentFact(person, MakeAgentFact(person, AgentFactKind.ReliefRequest,
                    town.Id, town.X, town.Y, person.Hunger, $"亲历饥饿 {person.Hunger:0}，家园粮少，请求救济"), false);
            }
        }

        var observationRadius = 3;
        foreach (var tower in State.Society.Buildings)
            if (tower.SettlementId == person.SettlementId && tower.Kind == BuildingKind.Watchtower &&
                IsFacilityOperating(tower)
                && Distance(person.X, person.Y, tower.X, tower.Y) <= 2)
                observationRadius = Math.Max(observationRadius, 3 + tower.Level);
        var dangerIndex = Circle(person.X, person.Y, observationRadius).Where(i => State.Tiles[i].FireTicks > 0)
            .OrderBy(i => Distance(person.X, person.Y, i % State.Width, i / State.Width)).FirstOrDefault(-1);
        if (dangerIndex >= 0)
        {
            RememberAgentFact(person, MakeAgentFact(person, AgentFactKind.Danger,
                0, dangerIndex % State.Width, dangerIndex / State.Width,
                State.Tiles[dangerIndex].FireTicks, "亲眼看见正在燃烧的土地"), false);
        }

        foreach (var army in State.Armies)
            if (army.NationId != person.NationId && Distance(person.X, person.Y, army.X, army.Y) <= 4)
            {
                RememberAgentFact(person, MakeAgentFact(person, AgentFactKind.Danger,
                    army.Id, army.X, army.Y, army.Soldiers, "亲眼看见附近有外来军队"), false);
            }
    }

    private void UpdateLocalCommunication()
    {
        var people = _communicationPeople;
        people.Clear();
        foreach (var person in State.Residents) people.Add(person.Id, person);
        // 递送先于本日交谈处理，新获知的信息须等到后续时刻才能转述，避免同日瞬间传播。
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
                received.LearnedTick = State.Tick;
                received.SourceResidentId = message.SenderId;
                received.Hops = Math.Min(32, fact.Hops + 1);
                received.Confidence *= 0.96;
                RememberAgentFact(recipient, received, false);
                if (_settlements.TryGetValue(recipient.SettlementId, out var home)
                    && Distance(recipient.X, recipient.Y, home.X, home.Y) <= 1
                    && (recipient.Profession == Profession.Representative || home.RepresentativeId == recipient.Id
                                                                          || received.Kind is AgentFactKind.Policy
                                                                              or AgentFactKind.Culture
                                                                              or AgentFactKind.Research))
                    ReceiveSocietyReport(home, recipient, received);
            }
        }

        _messagesToDeliver.Clear();
        if (_conversationHeads.Length != State.Tiles.Length) _conversationHeads = new int[State.Tiles.Length];
        else
        {
            foreach (var tile in _conversationTiles)
                _conversationHeads[tile] = 0;
        }

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
            _conversationNeighbors.Clear();
            var conversationRadius = State.Society.Buildings.Any(b =>
                b.Kind is BuildingKind.Market or BuildingKind.AssemblyHall or BuildingKind.TradeGuild &&
                IsFacilityOperating(b)
                && Distance(sender.X, sender.Y, b.X, b.Y) <= 3)
                ? 3
                : 2;
            foreach (var tile in Circle(sender.X, sender.Y, conversationRadius))
                for (var at = _conversationHeads[tile]; at != 0; at = _conversationNext[at - 1])
                {
                    var neighbor = State.Residents[at - 1];
                    if (neighbor.Id != sender.Id && neighbor.Health > 0) _conversationNeighbors.Add(neighbor);
                }

            if (_conversationNeighbors.Count == 0) continue;
            var recipient =
                SelectConversationRecipient((int)((State.Tick / 12 + sender.Id) % _conversationNeighbors.Count));
            var facts = SelectMessageFacts(sender, false);
            if (facts.Count > 0 && State.PendingMessages.Count < MaxPopulation * 2)
            {
                State.PendingMessages.Add(new PendingMessage
                {
                    SenderId = sender.Id, RecipientId = recipient.Id, DeliverTick = State.Tick + 1, Facts = facts,
                });
            }

            sender.Agent.LastConversationTick = State.Tick;
            sender.Agent.SocialNeed = Math.Max(0, sender.Agent.SocialNeed - 14);
            recipient.Agent.SocialNeed = Math.Max(0, recipient.Agent.SocialNeed - 10);
            ExchangeCulture(sender, recipient);
            if (sender.Agent.Goal.Kind == AgentGoalKind.Socialize) sender.Activity = ResidentActivity.Talking;
        }

        RelayKnownAgentMessages();
        people.Clear();
        _conversationNeighbors.Clear();
    }

    private Resident SelectConversationRecipient(int rank)
    {
        // 按 ID 选取第 rank 个接收者；分区过大时回退为排序。
        var left = 0;
        var right = _conversationNeighbors.Count - 1;
        var budget = 2 * BitOperations.Log2((uint)_conversationNeighbors.Count);
        while (left < right)
        {
            if (right - left < 16 || budget-- == 0)
            {
                CollectionsMarshal.AsSpan(_conversationNeighbors)
                    .Slice(left, right - left + 1).Sort(ResidentIdOrder);
                return _conversationNeighbors[rank];
            }

            var first = _conversationNeighbors[left].Id;
            var middle = _conversationNeighbors[(left + right) / 2].Id;
            var last = _conversationNeighbors[right].Id;
            var pivot = first < middle ? middle < last ? middle : Math.Max(first, last)
                : first < last ? first : Math.Max(middle, last);
            var lower = left;
            var at = left;
            var upper = right;
            while (at <= upper)
            {
                var id = _conversationNeighbors[at].Id;
                if (id < pivot)
                {
                    (_conversationNeighbors[lower], _conversationNeighbors[at]) =
                        (_conversationNeighbors[at], _conversationNeighbors[lower]);
                    lower++;
                    at++;
                }
                else if (id > pivot)
                {
                    (_conversationNeighbors[at], _conversationNeighbors[upper]) =
                        (_conversationNeighbors[upper], _conversationNeighbors[at]);
                    upper--;
                }
                else at++;
            }

            if (rank < lower) right = lower - 1;
            else if (rank > upper) left = upper + 1;
            else return _conversationNeighbors[rank];
        }

        return _conversationNeighbors[rank];
    }

    private void RelayKnownAgentMessages()
    {
        foreach (var sender in State.Residents)
        {
            if (sender.Profession is not Profession.Messenger and not Profession.Representative
                || (State.Tick + sender.Id) % 24 != 0 || !_settlements.TryGetValue(sender.SettlementId, out var home)
                || Distance(sender.X, sender.Y, home.X, home.Y) > 1) continue;
            foreach (var address in sender.Agent.Memory.Where(f => f.Kind == AgentFactKind.SettlementLocation
                                                                   && f.SubjectId != home.Id &&
                                                                   f.LearnedTick < State.Tick).Take(3))
            {
                if (!CanRelayInformation(home.Id, address.SubjectId, out var travelTicks)
                    || !_settlements.TryGetValue(address.SubjectId, out var destination)) continue;
                var recipient = State.Residents.Where(r => r.SettlementId == destination.Id
                                                           && Distance(r.X, r.Y, destination.X, destination.Y) <= 2)
                    .OrderByDescending(r => r.Id == destination.RepresentativeId).ThenBy(r => r.Id).FirstOrDefault();
                if (recipient is null) continue;
                var facts = SelectMessageFacts(sender, true);
                if (facts.Count == 0 || State.PendingMessages.Count >= MaxPopulation * 2) continue;
                State.PendingMessages.Add(new PendingMessage
                {
                    SenderId = sender.Id,
                    RecipientId = recipient.Id,
                    TargetSettlementId = destination.Id,
                    DeliverTick = State.Tick + Math.Max(1, travelTicks),
                    Facts = facts,
                });
            }
        }
    }

    private List<AgentFact> SelectMessageFacts(Resident sender, bool relay)
    {
        var rank = _settlements.TryGetValue(sender.SettlementId, out var home) &&
                   Distance(sender.X, sender.Y, home.X, home.Y) <= 3
            ? EffectiveSettlementRank(home)
            : 0;
        var capacity = relay ? 3 : 3 + rank * 2;
        var selected = new List<AgentFact>(capacity);
        foreach (var fact in sender.Agent.Memory)
        {
            if (fact.LearnedTick >= State.Tick || fact.Confidence <= (relay ? 0.25 : 0.15) ||
                (!relay && fact.Hops >= 12)) continue;
            var at = 0;
            while (at < selected.Count && !MessageFactPrecedes(fact, selected[at], relay)) at++;
            if (at >= capacity) continue;
            if (selected.Count == capacity) selected.RemoveAt(capacity - 1);
            selected.Insert(at, fact);
        }

        for (var i = 0; i < selected.Count; i++) selected[i] = CopyAgentFact(selected[i]);
        return selected;
    }

    private static bool MessageFactPrecedes(AgentFact candidate, AgentFact current, bool relay)
    {
        static bool Priority(AgentFact fact, bool relay)
        {
            return relay
                ? fact.Kind is AgentFactKind.ReliefRequest or AgentFactKind.WarOrder or AgentFactKind.PeaceOrder
                    or AgentFactKind.Research or AgentFactKind.DiplomaticNotice or AgentFactKind.WarReport
                : fact.Kind is AgentFactKind.Danger or AgentFactKind.ReliefRequest or AgentFactKind.WarOrder
                    or AgentFactKind.PeaceOrder or AgentFactKind.Research or AgentFactKind.DiplomaticNotice
                    or AgentFactKind.WarReport;
        }

        var candidatePriority = Priority(candidate, relay);
        var currentPriority = Priority(current, relay);
        return candidatePriority != currentPriority ? candidatePriority : candidate.ObservedTick > current.ObservedTick;
    }

    private void AddAgentMissionChoices(Resident person, Settlement home, List<GoalChoice> choices)
    {
        var agent = person.Agent;
        if (person.Age < 16 || State.Tick < agent.MissionRetryTick) return;
        if (agent.DestinationSettlementId != 0 && State.Tick - agent.MissionStartedTick < 360)
        {
            choices.Add(new GoalChoice(agent.Goal.Kind, agent.Goal.TargetX, agent.Goal.TargetY, 72,
                "继续完成正在亲自递送的任务", agent.CarriedMessages.FirstOrDefault(), agent.DestinationSettlementId));
            return;
        }

        AgentFact? relief = null;
        foreach (var fact in agent.Memory)
            if (fact.Kind == AgentFactKind.ReliefRequest && fact.Value >= 35 && AgentFactReliability(fact) > 0.25
                && State.Tick - fact.ObservedTick < 180 && (relief is null || fact.ObservedTick > relief.ObservedTick))
                relief = fact;
        if (relief is not null)
        {
            var destination = home.Id;
            if (_nations.TryGetValue(person.NationId, out var nation)
                && agent.Memory.Any(f => f.Kind == AgentFactKind.SettlementLocation && f.SubjectId == nation.CapitalId))
                destination = nation.CapitalId;
            var address = agent.Memory.FirstOrDefault(f =>
                f.Kind == AgentFactKind.SettlementLocation && f.SubjectId == destination);
            if (address is not null)
            {
                choices.Add(new GoalChoice(AgentGoalKind.Petition, address.X, address.Y,
                    (person.Profession == Profession.Representative ? 88 : 35 + agent.Personality.Courage * 20) *
                    AgentFactReliability(relief),
                    "带着已收到且可信的缺粮报告，去向本地代表请求救济", relief, destination));
            }
        }

        if (person.Profession is Profession.Trader or Profession.Messenger)
        {
            var addresses = _missionAddresses;
            addresses.Clear();
            foreach (var fact in agent.Memory)
            {
                if (fact.Kind != AgentFactKind.SettlementLocation || fact.SubjectId == home.Id ||
                    State.Tick - fact.ObservedTick >= 1200) continue;
                var distance = Distance(person.X, person.Y, fact.X, fact.Y);
                var at = 0;
                while (at < addresses.Count &&
                       Distance(person.X, person.Y, addresses[at].X, addresses[at].Y) <= distance) at++;
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
                        || (knownFood is not null && knownFood.Value >= ownFood.Value * 0.7 &&
                            AgentFactReliability(knownFood) > 0.5)) continue;
                    var reliability = AgentFactReliability(address) *
                                      (knownFood is null ? 0.75 : AgentFactReliability(knownFood));
                    if (reliability < 0.4)
                    {
                        choices.Add(new GoalChoice(AgentGoalKind.DeliverMessage, address.X, address.Y,
                            24 + agent.Personality.Ambition * 9,
                            "外地粮情过旧或来源存疑，先亲自核实，暂不据此装货", knownFood ?? address, address.SubjectId));
                    }
                    else
                    {
                        choices.Add(new GoalChoice(AgentGoalKind.Trade, home.X, home.Y,
                            (61 + agent.Personality.Ambition * 8) * reliability,
                            knownFood is null ? "已知聚落地址且家乡有余粮，带货亲自探访" : "有可信且尚新的聚落粮情，先在家园装粮，再亲自交换",
                            knownFood ?? address, address.SubjectId));
                    }
                }
                else if (agent.Memory.Any(f => f.Kind != AgentFactKind.SettlementLocation
                                               && f.ObservedTick > agent.MissionStartedTick &&
                                               AgentFactReliability(f) >= .5))
                {
                    choices.Add(new GoalChoice(AgentGoalKind.DeliverMessage, address.X, address.Y,
                        (59 + agent.Personality.Sociability * 8) * AgentFactReliability(address),
                        "带着自己已知的消息，拜访记忆中另一座聚落", address, address.SubjectId));
                }
            }

            if (addresses.Count == 0 && person.Inventory.Food >= 2 && agent.Fatigue < 35
                && Distance(person.X, person.Y, home.X, home.Y) < 18)
            {
                var heading = Directions[(person.Id + (int)(State.Tick / 360)) % Directions.Length];
                var frontier = Circle(person.X, person.Y, 6)
                    .Where(i => State.Tiles[i].IsWalkable && State.Tiles[i].FireTicks == 0)
                    .OrderByDescending(i =>
                        (i % State.Width - person.X) * heading.X + (i / State.Width - person.Y) * heading.Y)
                    .ThenByDescending(i => Distance(i % State.Width, i / State.Width, home.X, home.Y))
                    .FirstOrDefault(-1);
                if (frontier >= 0)
                {
                    choices.Add(new GoalChoice(AgentGoalKind.Explore, frontier % State.Width, frontier / State.Width,
                        42, "还不知道远方聚落，沿眼前可通行的土地探索"));
                }
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
            agent.CarriedMessages = agent.Memory.OrderByDescending(f =>
                    f.Kind is AgentFactKind.ReliefRequest or AgentFactKind.WarOrder or AgentFactKind.PeaceOrder
                        or AgentFactKind.DiplomaticNotice or AgentFactKind.WarReport
                        ? 1
                        : 0)
                .ThenByDescending(f => f.ObservedTick).Take(8).Select(CopyAgentFact).ToList();
            if (Distance(person.X, person.Y, home.X, home.Y) <= 1)
            {
                var ration = Math.Min(home.Resources.Food, Math.Max(0,
                    1.2 + Distance(home.X, home.Y, agent.Goal.TargetX, agent.Goal.TargetY) * 0.22 -
                    person.Inventory.Food));
                home.Resources.Food -= ration;
                person.Inventory.Food += ration;
            }
        }
    }

    private void ActOnAgentMission(Resident person, Settlement home)
    {
        var agent = person.Agent;
        var goal = agent.Goal;
        var address = agent.Memory.FirstOrDefault(f =>
            f.Kind == AgentFactKind.SettlementLocation && f.SubjectId == agent.DestinationSettlementId);
        if (address is null || State.Tick - agent.MissionStartedTick >= 360)
        {
            FinishAgentMission(person, address is null ? "缺少可靠目的地地址，暂缓递送" : "长时间未能到达，暂缓递送并返乡补给");
            return;
        }

        // 远方宣战不等于商人已知；只有实际收到且对应记忆目的国家的军令才能中止旅程。
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
            // 抵达后重新核对库存，旧余粮报告和先到商人可能已改变供给，同时保留本地基本口粮。
            var surplus = Math.Max(0, home.Resources.Food - Math.Max(12, home.Population));
            var cargo = Math.Min(surplus, Math.Max(0, Math.Min(40, 8 + distance * 0.22) - person.Inventory.Food));
            if (cargo <= 1)
            {
                FinishAgentMission(person, "抵达粮仓后发现没有可装运余粮，取消交易");
                return;
            }

            home.Resources.Food -= cargo;
            person.Inventory.Food += cargo;
            agent.CarriedMessages = agent.Memory.OrderByDescending(f => f.ObservedTick).Take(8).Select(CopyAgentFact)
                .ToList();
            goal.TargetX = address.X;
            goal.TargetY = address.Y;
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
            agent.Memory.RemoveAll(f =>
                f.Kind == AgentFactKind.SettlementLocation && f.SubjectId == agent.DestinationSettlementId);
            FinishAgentMission(person, "抵达记忆中的地址，却未见原聚落");
            return;
        }

        // 途中归属可能变化，居民只有到场后才能获知当前身份。
        if (goal.Kind == AgentGoalKind.Trade && IsKnownHostile(person, destination.NationId))
        {
            FinishAgentMission(person, "抵达后发现聚落属于已知敌对国家，保留货物返乡");
            return;
        }

        goal.WorkTicks++;
        if (goal.WorkTicks < 3)
        {
            person.Activity = ResidentActivity.Talking;
            return;
        }

        WorldEvent? tradeEvent = null;
        var completionReason = goal.Kind == AgentGoalKind.Petition ? "意见已当面交给聚落代表" : "已实际抵达目的地并递送所携消息";
        if (goal.Kind == AgentGoalKind.Trade)
        {
            const double woodPerFood = 0.4;
            var reserve = 1.2 + Distance(destination.X, destination.Y, home.X, home.Y) * 0.11;
            var availableFood = Math.Max(0, person.Inventory.Food - reserve);
            var food = Math.Min(availableFood, Math.Min(destination.Resources.Wood / woodPerFood,
                Math.Min(Math.Max(0, 1_000_000 - destination.Resources.Food),
                    Math.Max(0, 1_000_000 - person.Inventory.Wood) / woodPerFood)));
            var payment = food * woodPerFood;
            person.Inventory.Food -= food;
            destination.Resources.Food += food;
            destination.Resources.Wood = Math.Max(0, destination.Resources.Wood - payment);
            person.Inventory.Wood += payment;
            if (food > 0)
            {
                tradeEvent = AddEvent(WorldEventKind.Trade,
                    $"{person.Name}抵达{destination.Name}，交付 {food:0.0} 份粮食，携带 {payment:0.0} 份木材返乡。", destination.X,
                    destination.Y, EventAction.Delivery, destination.Id, person.Id);
            }

            if (tradeEvent is not null)
            {
                tradeEvent.SecondNationId = person.NationId;
                tradeEvent.SecondSettlementId = home.Id;
            }

            completionReason = food > 0
                ? $"实际交换 {food:0.0} 份粮食与 {payment:0.0} 份木材，携带所得木材及剩余粮食返乡"
                : availableFood <= 0
                    ? "剩余粮食需作返程口粮，没有可交换货物，返乡补给"
                    : destination.Resources.Wood <= 0
                        ? "当地没有可支付的木材，保留货物返乡"
                        : "当地粮仓或随身木材已达容量上限，保留货物返乡";
        }

        if (tradeEvent is not null && destination.NationId != person.NationId)
        {
            var outbound = MakeAgentFact(person, AgentFactKind.TradeExchange, person.NationId, destination.X,
                destination.Y, 1, "商旅实际抵达并完成粮木交换");
            outbound.EventId = tradeEvent.Id;
            AddPublicFact(destination, outbound);
            var inbound = MakeAgentFact(person, AgentFactKind.TradeExchange, destination.NationId, destination.X,
                destination.Y, 1, "我与另一国家的聚落完成交易，返乡后可报告");
            inbound.EventId = tradeEvent.Id;
            RememberAgentFact(person, inbound);
        }

        foreach (var fact in agent.CarriedMessages.ToArray())
        {
            var delivered = CopyAgentFact(fact);
            delivered.LearnedTick = State.Tick;
            delivered.SourceResidentId = person.Id;
            delivered.Hops = Math.Min(32, fact.Hops + 1);
            delivered.Confidence *= 0.98;
            AddPublicFact(destination, delivered);
            ReceiveSocietyReport(destination, person, delivered);
        }

        if (destination.PublicKnowledge.Count > 24)
        {
            destination.PublicKnowledge =
                destination.PublicKnowledge.OrderByDescending(f => f.LearnedTick).Take(24).ToList();
        }

        ObserveAgentEnvironment(person);
        FinishAgentMission(person, completionReason);
    }

    private void FinishAgentMission(Resident person, string reason)
    {
        var agent = person.Agent;
        var goal = agent.Goal;
        agent.Decisions.Add(new AgentDecision
        {
            Tick = State.Tick,
            Goal = AgentGoalKind.ReturnHome,
            Reason = reason,
            Score = 80,
            KnowledgeObservedTick = State.Tick,
            SourceResidentId = person.Id,
        });
        if (agent.Decisions.Count > 6) agent.Decisions.RemoveAt(0);
        agent.DestinationSettlementId = 0;
        agent.CarriedMessages.Clear();
        agent.MissionRetryTick = State.Tick + 90;
        if (_settlements.TryGetValue(person.SettlementId, out var home))
        {
            agent.Goal = new AgentGoal
            {
                Kind = AgentGoalKind.ReturnHome,
                TargetX = home.X,
                TargetY = home.Y,
                TargetSettlementId = home.Id,
                StartedTick = State.Tick,
                ReviewTick = State.Tick + 12,
                Reason = reason,
            };
        }
        else goal.Kind = AgentGoalKind.Idle;

        agent.NextThinkTick = State.Tick + 12;
    }
}
