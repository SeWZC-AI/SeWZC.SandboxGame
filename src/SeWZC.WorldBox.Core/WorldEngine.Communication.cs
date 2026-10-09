using System.Collections.Immutable;
using SeWZC.WorldBox.Core.Runtime;

namespace SeWZC.WorldBox.Core;

public sealed partial class WorldEngine
{
    // 通信索引只在本阶段有效，不能替代世界中的权威居民状态。
    private readonly List<int> _conversationTiles = [];
    private readonly List<PendingMessage> _messagesToDeliver = [];
    private readonly List<AgentFact> _missionAddresses = [];
    private readonly List<AgentFact> _observedReports = [];
    private readonly List<AgentFact> _receivedFacts = [];
    private int[] _conversationCounts = [], _conversationStarts = [], _conversationPositions = [];
    private ResidentCursor[] _conversationResidents = [];

    private AgentFact MakeAgentFact(ResidentCursor observer, AgentFactKind kind, int subject, int x, int y,
        double value,
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
            ObservedTick = Current.Tick,
            LearnedTick = Current.Tick,
            OriginResidentId = observer.Id,
            SourceResidentId = observer.Id,
            OriginProfession = observer.Profession,
            Confidence = 1,
            Text = text,
        };
    }

    private void RememberAgentFact(ResidentCursor person, AgentFact fact)
    {
        person.Agent = person.Agent.Remember(fact, person.SettlementId);
    }

    /// <summary>将附近的观察和可接触的公开报告记录到该居民自己的记忆中。</summary>
    /// <param name="person">观察信息的居民。</param>
    private void ObserveAgentEnvironment(ResidentCursor person)
    {
        if (Current.Rules.Expansion && person.Profession is Profession.Builder or Profession.Trader
                                        or Profession.Messenger
                                    && _settlements.TryGetValue(person.SettlementId, out var camp) &&
                                    !camp.FoundationPending
                                    && Distance(person.X, person.Y, camp.X, camp.Y) >= MinimumSettlementDistance - 6
                                    && !person.Agent.Memory.Any(f =>
                                        f.Kind == AgentFactKind.FoundingSite && Current.Tick - f.ObservedTick <
                                        SimulationTime.TicksPerYear))
        {
            var site = Circle(person.X, person.Y, 3).Where(i => RaceTerrainRules.CanWalk(Current.Tiles[i].Value, person.Race)
                                                                && !IsWaterTerrain(Current.Tiles[i].Terrain) &&
                                                                Current.Tiles[i].Fertility >= 40
                                                                && Current.Tiles[i].ClaimedSettlementId == 0 &&
                                                                Current.Tiles[i].FireTicks == 0
                                                                && FoundingSiteSuitable(i, [person.Race])
                                                                && Current.Settlements.All(t =>
                                                                    Distance(t.X, t.Y, i % Current.Width,
                                                                        i / Current.Width) >=
                                                                    MinimumSettlementDistance))
                .OrderByDescending(i => Current.Tiles[i].Fertility).ThenBy(i => i).FirstOrDefault(-1);
            if (site >= 0)
            {
                RememberAgentFact(person, MakeAgentFact(person, AgentFactKind.FoundingSite, camp.Id,
                    site % Current.Width,
                    site / Current.Width,
                    Current.Tiles[site].Fertility,
                    $"亲眼勘察到符合间距、周围有至少 {SettlementActivationArea} 格可用陆地的建村地块，肥力 {Current.Tiles[site].Fertility}/100，需带回报告"));
            }
        }

        foreach (var town in Current.Settlements)
        {
            if (Distance(person.X, person.Y, town.X, town.Y) > 3)
                continue;
            RememberAgentFact(person, MakeAgentFact(person, AgentFactKind.SettlementLocation,
                town.Id, town.X, town.Y, town.NationId, $"见到聚落 {town.Name}"));
            if (Distance(person.X, person.Y, town.X, town.Y) > 1)
                continue;
            RememberAgentFact(person, MakeAgentFact(person, AgentFactKind.FoodSupply,
                town.Id, town.X, town.Y, town.Resources.Food, $"在{town.Name}粮仓见到 {town.Resources.Food:0.0} 份粮食"));
            if (town.CultureId > 0)
            {
                RememberAgentFact(person, MakeAgentFact(person, AgentFactKind.Culture,
                    town.Id, town.X, town.Y, town.CultureId, $"在{town.Name}接触当地文化"));
            }

            _observedReports.Clear();
            foreach (var report in town.PublicKnowledge)
            {
                var at = 0;
                while (at < _observedReports.Count && _observedReports[at].LearnedTick >= report.LearnedTick)
                    at++;
                if (at >= 6)
                    continue;
                _observedReports.Insert(at, report);
                if (_observedReports.Count > 6)
                    _observedReports.RemoveAt(6);
            }

            foreach (var report in _observedReports)
            {
                if (report.LearnedTick >= Current.Tick)
                    continue;
                var learned = report with
                {
                    LearnedTick = Current.Tick,
                    Hops = Math.Min(32, report.Hops + 1),
                    Confidence = report.Confidence * 0.98,
                };
                RememberAgentFact(person, learned);
                if (learned.Kind is AgentFactKind.Policy or AgentFactKind.Culture or AgentFactKind.Research
                    or AgentFactKind.DiplomaticNotice)
                    ReceiveSocietyReport(town, person, learned);
            }

            if (town.Id == person.SettlementId && person.Hunger > 35 && town.Resources.Food < 12)
            {
                RememberAgentFact(person, MakeAgentFact(person, AgentFactKind.ReliefRequest,
                    town.Id, town.X, town.Y, person.Hunger, $"亲历饥饿 {person.Hunger:0}，家园粮少，请求救济"));
            }
        }

        var observationRadius = 3;
        foreach (var tower in Current.Buildings)
            if (tower.Value.SettlementId == person.SettlementId && tower.Value.Kind == BuildingKind.Watchtower &&
                IsFacilityOperating(tower.Value)
                && Distance(person.X, person.Y, tower.Value.X, tower.Value.Y) <= 2)
                observationRadius = Math.Max(observationRadius, 3 + tower.Value.Level);
        var dangerIndex = Circle(person.X, person.Y, observationRadius).Where(i => Current.Tiles[i].FireTicks > 0)
            .OrderBy(i => Distance(person.X, person.Y, i % Current.Width, i / Current.Width)).FirstOrDefault(-1);
        if (dangerIndex >= 0)
        {
            RememberAgentFact(person, MakeAgentFact(person, AgentFactKind.Danger,
                0, dangerIndex % Current.Width, dangerIndex / Current.Width,
                Current.Tiles[dangerIndex].FireTicks, "亲眼看见正在燃烧的土地"));
        }

        foreach (var army in Current.Armies)
            if (army.Value.NationId != person.NationId && Distance(person.X, person.Y, army.Value.X, army.Value.Y) <= 4)
            {
                RememberAgentFact(person, MakeAgentFact(person, AgentFactKind.Danger,
                    army.Value.Id, army.Value.X, army.Value.Y, army.Value.Soldiers, "亲眼看见附近有外来军队"));
            }
    }

    private void UpdateLocalCommunication()
    {
        // 递送先于本日交谈处理，新获知的信息须等到后续时刻才能转述，避免同日瞬间传播。
        _messagesToDeliver.Clear();
        for (var i = 0; i < Current.PendingMessages.Count; i++)
        {
            var message = Current.PendingMessages[i];
            if (message.DeliverTick <= Current.Tick)
                _messagesToDeliver.Add(message);
        }

        var tick = Current.Tick;
        Current.PendingMessages = Current.PendingMessages.RemoveAll(message => message.DeliverTick <= tick);
        foreach (var message in _messagesToDeliver)
        {
            if (FindLiveResident(message.RecipientId) is not { } recipient)
                continue;
            if (message.TargetSettlementId != 0
                && (!_settlements.TryGetValue(message.TargetSettlementId, out var endpoint)
                    || Distance(recipient.X, recipient.Y, endpoint.X, endpoint.Y) > 2
                    || FindLiveResident(message.SenderId) is not { } stationSender
                    || !CanRelayInformation(stationSender.SettlementId, endpoint.Id, out _)))
                continue;
            _receivedFacts.Clear();
            var agent = recipient.Agent;
            foreach (var fact in message.Facts)
            {
                var received = fact with
                {
                    LearnedTick = Current.Tick,
                    SourceResidentId = message.SenderId,
                    Hops = Math.Min(32, fact.Hops + 1),
                    Confidence = fact.Confidence * 0.96,
                };
                agent = agent.Remember(received, recipient.SettlementId);
                _receivedFacts.Add(received);
            }

            recipient.Agent = agent;
            foreach (var received in _receivedFacts)
                if (_settlements.TryGetValue(recipient.SettlementId, out var home)
                    && Distance(recipient.X, recipient.Y, home.X, home.Y) <= 1
                    && (recipient.Profession == Profession.Representative || home.RepresentativeId == recipient.Id
                                                                          || received.Kind is AgentFactKind.Policy
                                                                              or AgentFactKind.Culture
                                                                              or AgentFactKind.Research))
                    ReceiveSocietyReport(home, recipient, received);
        }

        _receivedFacts.Clear();
        _messagesToDeliver.Clear();
        if (_conversationCounts.Length != Current.Tiles.Count)
        {
            _conversationCounts = new int[Current.Tiles.Count];
            _conversationStarts = new int[Current.Tiles.Count];
        }
        else
        {
            foreach (var tile in _conversationTiles)
                _conversationCounts[tile] = 0;
        }

        _conversationTiles.Clear();
        if (_conversationPositions.Length < Current.Residents.Count)
        {
            _conversationPositions = new int[MaxPopulation];
            _conversationResidents = new ResidentCursor[MaxPopulation];
        }

        for (var i = 0; i < Current.Residents.Count; i++)
        {
            var resident = Current.Residents[i];
            if (resident.Health <= 0)
                continue;
            var tile = Index(resident.X, resident.Y);
            if (_conversationCounts[tile]++ == 0)
                _conversationTiles.Add(tile);
        }

        var total = 0;
        foreach (var tile in _conversationTiles)
        {
            _conversationStarts[tile] = total;
            total += _conversationCounts[tile];
            _conversationCounts[tile] = 0;
        }

        for (var i = 0; i < Current.Residents.Count; i++)
        {
            var resident = Current.Residents[i];
            if (resident.Health <= 0)
                continue;
            var tile = Index(resident.X, resident.Y);
            var position = _conversationStarts[tile] + _conversationCounts[tile]++;
            _conversationPositions[i] = position;
            _conversationResidents[position] = resident;
        }

        Span<int> nearby = stackalloc int[29];
        // 小世界保留十二 tick 交谈周期；大群体错峰，每 tick 最多启动约三十二次普通交谈。
        var conversationInterval = Math.Max(12, (Current.Residents.Count + 31) / 32);
        for (var senderIndex = 0; senderIndex < Current.Residents.Count; senderIndex++)
        {
            var sender = Current.Residents[senderIndex];
            if ((Current.Tick + senderIndex) % conversationInterval != 0 || Current.Tick -
                                                                         sender.Agent.LastConversationTick < 6
                                                                         || sender.Health <= 0 ||
                                                                         sender.Activity == ResidentActivity.Sleeping)
                continue;
            var conversationRadius = 2;
            foreach (var building in Current.Buildings)
                if (building.Value.Kind is BuildingKind.Market or BuildingKind.AssemblyHall or BuildingKind.TradeGuild
                    && IsFacilityOperating(building.Value)
                    && Distance(sender.X, sender.Y, building.Value.X, building.Value.Y) <= 3)
                {
                    conversationRadius = 3;
                    break;
                }

            var count = 0;
            var cells = 0;
            var ownTile = Index(sender.X, sender.Y);
            foreach (var tile in Circle(sender.X, sender.Y, conversationRadius))
            {
                var local = _conversationCounts[tile] - (tile == ownTile ? 1 : 0);
                if (local == 0)
                    continue;
                nearby[cells++] = tile;
                count += local;
            }

            if (count == 0)
                continue;
            // 先按各格人数定位接收者，再直接读取格内位置；人群再密集也无需枚举整群。
            var rank = (int)((Current.Tick / conversationInterval + sender.Id) % count);
            ResidentCursor? recipient = null;
            foreach (var tile in nearby[..cells])
            {
                var local = _conversationCounts[tile] - (tile == ownTile ? 1 : 0);
                if (rank >= local)
                {
                    rank -= local;
                    continue;
                }

                var position = _conversationStarts[tile] + rank;
                if (tile == ownTile && position >= _conversationPositions[senderIndex])
                    position++;
                recipient = _conversationResidents[position];
                break;
            }

            if (recipient is null || recipient.Activity == ResidentActivity.Sleeping)
                continue;
            var facts = SelectMessageFacts(sender, false);
            if (facts.Count > 0 && Current.PendingMessages.Count < MaxPopulation * 2)
            {
                Current.PendingMessages = Current.PendingMessages.Add(new PendingMessage
                {
                    SenderId = sender.Id,
                    RecipientId = recipient.Id,
                    DeliverTick = Current.Tick + 1,
                    Facts = facts.ToImmutableArray(),
                });
            }

            sender.Agent = sender.Agent with
            {
                LastConversationTick = Current.Tick, SocialNeed = Math.Max(0, sender.Agent.SocialNeed - 14),
            };
            recipient.Agent = recipient.Agent with { SocialNeed = Math.Max(0, recipient.Agent.SocialNeed - 10) };
            ExchangeCulture(sender, recipient);
            if (sender.Agent.Goal.Kind == AgentGoalKind.Socialize)
                sender.Activity = ResidentActivity.Talking;
        }

        RelayKnownAgentMessages();
        // 日内位置在社会阶段保持不变，复用同一分区处理附近医疗和法术，退出模拟步时清除引用。
        if (_knowledgeQueriesActive)
        {
            _nearbyResidentCount = total;
            _nearbyResidentTick = Current.Tick;
            _nearbyResidentRevision = Current.Residents.MembershipRevision;
        }
        else
            Array.Clear(_conversationResidents, 0, total);
    }

    private void RelayKnownAgentMessages()
    {
        foreach (var sender in Current.Residents)
        {
            if (sender.Profession is not Profession.Messenger and not Profession.Representative
                || (Current.Tick + sender.Id) % 24 != 0 || !_settlements.TryGetValue(sender.SettlementId, out var home)
                || Distance(sender.X, sender.Y, home.X, home.Y) > 1)
                continue;
            var addresses = 0;
            foreach (var address in sender.Agent.Memory)
            {
                if (address.Kind != AgentFactKind.SettlementLocation || address.SubjectId == home.Id
                                                                     || address.LearnedTick >= Current.Tick)
                    continue;
                if (++addresses > 3)
                    break;
                if (!CanRelayInformation(home.Id, address.SubjectId, out var travelTicks)
                    || !_settlements.TryGetValue(address.SubjectId, out var destination))
                    continue;
                ResidentCursor? recipient = null;
                foreach (var resident in Current.Residents)
                {
                    if (resident.SettlementId != destination.Id
                        || Distance(resident.X, resident.Y, destination.X, destination.Y) > 2)
                        continue;
                    if (resident.Id == destination.RepresentativeId)
                    {
                        recipient = resident;
                        break;
                    }

                    if (recipient is null || resident.Id < recipient.Id)
                        recipient = resident;
                }

                if (recipient is null)
                    continue;
                var facts = SelectMessageFacts(sender, true);
                if (facts.Count == 0 || Current.PendingMessages.Count >= MaxPopulation * 2)
                    continue;
                Current.PendingMessages = Current.PendingMessages.Add(new PendingMessage
                {
                    SenderId = sender.Id,
                    RecipientId = recipient.Id,
                    TargetSettlementId = destination.Id,
                    DeliverTick = Current.Tick + Math.Max(1, travelTicks),
                    Facts = facts.ToImmutableArray(),
                });
            }
        }
    }

    private List<AgentFact> SelectMessageFacts(ResidentCursor sender, bool relay)
    {
        var rank = _settlements.TryGetValue(sender.SettlementId, out var home) &&
                   Distance(sender.X, sender.Y, home.X, home.Y) <= 3
            ? EffectiveSettlementRank(home)
            : 0;
        var capacity = relay ? 3 : 3 + rank * 2;
        var selected = new List<AgentFact>(capacity);
        foreach (var fact in sender.Agent.Memory)
        {
            if (fact.LearnedTick >= Current.Tick || fact.Confidence <= (relay ? 0.25 : 0.15) ||
                (!relay && fact.Hops >= 12))
                continue;
            var at = 0;
            while (at < selected.Count && !MessageFactPrecedes(fact, selected[at], relay))
                at++;
            if (at >= capacity)
                continue;
            if (selected.Count == capacity)
                selected.RemoveAt(capacity - 1);
            selected.Insert(at, fact);
        }

        return selected;
    }

    private static bool MessageFactPrecedes(AgentFact candidate, AgentFact current, bool relay)
    {
        var candidatePriority = candidate.Topic.PrioritizeMessage(relay);
        var currentPriority = current.Topic.PrioritizeMessage(relay);
        return candidatePriority != currentPriority ? candidatePriority : candidate.ObservedTick > current.ObservedTick;
    }

    private void AddAgentMissionChoices(ResidentCursor person, SettlementCursor home, List<GoalChoice> choices)
    {
        var agent = person.Agent;
        if (person.Age < 16 || Current.Tick < agent.MissionRetryTick)
            return;
        if (agent.DestinationSettlementId != 0 &&
            Current.Tick - agent.MissionStartedTick < 3 * SimulationTime.TicksPerYear)
        {
            choices.Add(new GoalChoice(agent.Goal.Kind, agent.Goal.TargetX, agent.Goal.TargetY, 72,
                "继续完成正在亲自递送的任务", agent.CarriedMessages.FirstOrDefault(), agent.DestinationSettlementId));
            return;
        }

        AgentFact? relief = null;
        foreach (var fact in agent.Memory)
            if (fact.Kind == AgentFactKind.ReliefRequest && fact.Value >= 35 && fact.ReliabilityAt(Current.Tick) > 0.25
                && Current.Tick - fact.ObservedTick < 2 * SimulationTime.TicksPerMonth &&
                (relief is null || fact.ObservedTick > relief.ObservedTick))
                relief = fact;
        if (relief is not null)
        {
            var destination = home.Id;
            if (_nations.TryGetValue(person.NationId, out var nation)
                && agent.Memory.Any(f => f.Kind == AgentFactKind.SettlementLocation && f.SubjectId == nation.Value.CapitalId))
                destination = nation.Value.CapitalId;
            var address = agent.Memory.FirstOrDefault(f =>
                f.Kind == AgentFactKind.SettlementLocation && f.SubjectId == destination);
            if (address is not null)
            {
                choices.Add(new GoalChoice(AgentGoalKind.Petition, address.X, address.Y,
                    (person.Profession == Profession.Representative ? 88 : 35 + agent.Personality.Courage * 20) *
                    relief.ReliabilityAt(Current.Tick),
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
                    Current.Tick - fact.ObservedTick >= 10 * SimulationTime.TicksPerYear)
                    continue;
                var distance = Distance(person.X, person.Y, fact.X, fact.Y);
                var at = 0;
                while (at < addresses.Count &&
                       Distance(person.X, person.Y, addresses[at].X, addresses[at].Y) <= distance)
                    at++;
                addresses.Insert(at, fact);
            }

            for (var i = 0; i < Math.Min(3, addresses.Count); i++)
            {
                var address = addresses[i];
                if (person.Profession == Profession.Trader)
                {
                    if (!Current.Rules.Trade)
                        continue;
                    if (IsKnownHostile(person, (int)address.Value))
                        continue;
                    var knownFood = LatestAgentFact(agent.Memory, AgentFactKind.FoodSupply, address.SubjectId);
                    var ownFood = LatestAgentFact(agent.Memory, AgentFactKind.FoodSupply, home.Id);
                    if (ownFood is null || ownFood.Value < 50 || ownFood.ReliabilityAt(Current.Tick) < 0.25
                        || (knownFood is not null && knownFood.Value >= ownFood.Value * 0.7 &&
                            knownFood.ReliabilityAt(Current.Tick) > 0.5))
                        continue;
                    var reliability = address.ReliabilityAt(Current.Tick) *
                                      (knownFood is null ? 0.75 : knownFood.ReliabilityAt(Current.Tick));
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
                                               f.ReliabilityAt(Current.Tick) >= .5))
                {
                    choices.Add(new GoalChoice(AgentGoalKind.DeliverMessage, address.X, address.Y,
                        (59 + agent.Personality.Sociability * 8) * address.ReliabilityAt(Current.Tick),
                        "带着自己已知的消息，拜访记忆中另一座聚落", address, address.SubjectId));
                }
            }

            if (addresses.Count == 0 && person.Inventory.Food >= 2 && agent.Fatigue < 35
                && Distance(person.X, person.Y, home.X, home.Y) < 18)
            {
                var heading =
                    Directions[
                        (person.Id + (int)(Current.Tick / (3 * SimulationTime.TicksPerYear))) % Directions.Length];
                var frontier = Circle(person.X, person.Y, 6)
                    .Where(i => Current.Tiles[i].IsWalkable && Current.Tiles[i].FireTicks == 0)
                    .OrderByDescending(i =>
                        (i % Current.Width - person.X) * heading.X + (i / Current.Width - person.Y) * heading.Y)
                    .ThenByDescending(i => Distance(i % Current.Width, i / Current.Width, home.X, home.Y))
                    .FirstOrDefault(-1);
                if (frontier >= 0)
                {
                    choices.Add(new GoalChoice(AgentGoalKind.Explore, frontier % Current.Width,
                        frontier / Current.Width,
                        42, "还不知道远方聚落，沿眼前可通行的土地探索"));
                }
            }
        }
    }

    private void BeginAgentMission(ResidentCursor person, SettlementCursor home)
    {
        person.Agent = person.Agent.BeginMission(home.Id, Current.Tick);
        if (person.Agent.Goal.Kind != AgentGoalKind.Trade)
        {
            if (Distance(person.X, person.Y, home.X, home.Y) <= 1)
            {
                var ration = Math.Min(home.Resources.Food, Math.Max(0,
                    1.2 + Distance(home.X, home.Y, person.Agent.Goal.TargetX, person.Agent.Goal.TargetY) * 0.22 -
                    person.Inventory.Food));
                home.Resources = home.Resources with { Food = home.Resources.Food - ration };
                person.Inventory = person.Inventory with { Food = person.Inventory.Food + ration };
            }
        }
    }

    private void ActOnAgentMission(ResidentCursor person, SettlementCursor home)
    {
        var goal = person.Agent.Goal;
        var address = person.Agent.Memory.FirstOrDefault(f =>
            f.Kind == AgentFactKind.SettlementLocation && f.SubjectId == person.Agent.DestinationSettlementId);
        if (address is null || Current.Tick - person.Agent.MissionStartedTick >= 3 * SimulationTime.TicksPerYear)
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

        if (goal.Kind == AgentGoalKind.Trade && person.Agent.CarriedMessages.Count == 0)
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

            home.Resources = home.Resources with { Food = home.Resources.Food - cargo };
            person.Inventory = person.Inventory with { Food = person.Inventory.Food + cargo };
            person.Agent = person.Agent with
            {
                CarriedMessages = person.Agent.Memory.OrderByDescending(f => f.ObservedTick).Take(8)
                    .ToImmutableList(),
            };
            person.Agent = person.Agent.WithGoal(goal = goal with { TargetX = address.X, TargetY = address.Y });
        }

        PrepareJourneyTransport(person, home);
        if (Distance(person.X, person.Y, goal.TargetX, goal.TargetY) > 1 || !Walkable(person.X, person.Y))
        {
            MoveAgentTowards(person, goal.TargetX, goal.TargetY);
            person.Activity = ResidentActivity.Delivering;
            return;
        }

        if (!_settlements.TryGetValue(person.Agent.DestinationSettlementId, out var destination)
            || Distance(person.X, person.Y, destination.X, destination.Y) > 1)
        {
            var agent = person.Agent;
            person.Agent = agent with
            {
                Memory = agent.Memory.RemoveAll(f =>
                    f.Kind == AgentFactKind.SettlementLocation && f.SubjectId == agent.DestinationSettlementId),
            };
            FinishAgentMission(person, "抵达记忆中的地址，却未见原聚落");
            return;
        }

        // 途中归属可能变化，居民只有到场后才能获知当前身份。
        if (goal.Kind == AgentGoalKind.Trade && IsKnownHostile(person, destination.NationId))
        {
            FinishAgentMission(person, "抵达后发现聚落属于已知敌对国家，保留货物返乡");
            return;
        }

        person.Agent = person.Agent.WithGoal(goal = goal.Attend());
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
            person.Inventory = person.Inventory with { Food = person.Inventory.Food - food };
            destination.Resources = destination.Resources with
            {
                Food = destination.Resources.Food + food, Wood = Math.Max(0, destination.Resources.Wood - payment),
            };
            person.Inventory = person.Inventory with { Wood = person.Inventory.Wood + payment };
            if (food > 0)
            {
                tradeEvent = AddEvent(WorldEventKind.Trade,
                    $"{person.Name}抵达{destination.Name}，交付 {food:0.0} 份粮食，携带 {payment:0.0} 份木材返乡。", destination.X,
                    destination.Y, EventAction.Delivery, destination.Id, person.Id);
            }

            if (tradeEvent is not null)
            {
                tradeEvent = PublishEvent(tradeEvent with
                {
                    SecondNationId = person.NationId, SecondSettlementId = home.Id,
                });
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
            outbound = outbound with { EventId = tradeEvent.Id };
            AddPublicFact(destination, outbound);
            var inbound = MakeAgentFact(person, AgentFactKind.TradeExchange, destination.NationId, destination.X,
                destination.Y, 1, "我与另一国家的聚落完成交易，返乡后可报告");
            inbound = inbound with { EventId = tradeEvent.Id };
            RememberAgentFact(person, inbound);
        }

        foreach (var fact in person.Agent.CarriedMessages.ToArray())
        {
            var delivered = fact with
            {
                LearnedTick = Current.Tick,
                SourceResidentId = person.Id,
                Hops = Math.Min(32, fact.Hops + 1),
                Confidence = fact.Confidence * 0.98,
            };
            AddPublicFact(destination, delivered);
            ReceiveSocietyReport(destination, person, delivered);
        }

        if (destination.PublicKnowledge.Count > 24)
        {
            destination.PublicKnowledge =
                destination.PublicKnowledge.OrderByDescending(f => f.LearnedTick).Take(24).ToImmutableList();
        }

        ObserveAgentEnvironment(person);
        FinishAgentMission(person, completionReason);
    }

    private void FinishAgentMission(ResidentCursor person, string reason)
    {
        var goal = _settlements.TryGetValue(person.SettlementId, out var home)
            ? new AgentGoal
            {
                Kind = AgentGoalKind.ReturnHome,
                TargetX = home.X,
                TargetY = home.Y,
                TargetSettlementId = home.Id,
                StartedTick = Current.Tick,
                ReviewTick = Current.Tick + 12,
                Reason = reason,
            }
            : person.Agent.Goal with { Kind = AgentGoalKind.Idle, WorkTicks = 0 };
        person.Agent = person.Agent.FinishMission(goal, reason, Current.Tick, person.Id);
    }
}
