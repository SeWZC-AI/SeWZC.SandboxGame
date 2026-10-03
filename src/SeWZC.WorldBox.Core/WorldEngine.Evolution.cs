namespace SeWZC.WorldBox.Core;

public enum WorldPreset { Flourishing, LivingWorld, Turbulent }

public sealed record WorldRules
{
    public bool ResourceRegeneration { get; set; } = true;
    public bool FireSpread { get; set; } = true;
    public double GatheringRate { get; set; } = 1;
    public double CombatDamageRate { get; set; } = 1;
    public bool Births { get; set; } = true;
    public bool Aging { get; set; } = true;
    public bool Hunger { get; set; } = true;
    public bool Disease { get; set; } = true;
    public bool Construction { get; set; } = true;
    public bool Research { get; set; } = true;
    public bool Expansion { get; set; } = true;
    public bool Trade { get; set; } = true;
    public bool Alliances { get; set; } = true;
    public bool Wars { get; set; } = true;
    public bool Peace { get; set; } = true;
    public bool Migration { get; set; } = true;
    public bool Secession { get; set; } = true;
    public int Conflict { get; set; } = 1;
    public int DisasterFrequency { get; set; } = 1;
    public int DisasterStrength { get; set; } = 1;
    public double DevelopmentRate { get; set; } = 1;
    public double MagicRate { get; set; } = 1;

    public static WorldRules For(WorldPreset preset) => preset switch
    {
        WorldPreset.Flourishing => new() { Wars = false, Secession = false, Conflict = 0, DisasterFrequency = 0 },
        WorldPreset.Turbulent => new() { Conflict = 3, DisasterFrequency = 2, DisasterStrength = 2 },
        _ => new()
    };
}

public sealed partial class WorldState
{
    public WorldRules Rules { get; set; } = new();
}

public sealed partial class Settlement
{
    public string DevelopmentGoal { get; set; } = "稳定粮食，准备发展";
    public string DevelopmentBlocker { get; set; } = "等待当地居民议事";
    public long LastDevelopmentTick { get; set; }
    public double Unrest { get; set; }
    public long LastPoliticalChangeTick { get; set; }
}

public readonly record struct DevelopmentSummary(string Stage, string Goal, string Blocker, double Progress);

public sealed partial class WorldEngine
{
    private static void ValidateWorldRules(WorldRules? rules)
    {
        if (rules is null || !double.IsFinite(rules.GatheringRate) || rules.GatheringRate is < .25 or > 3
            || !double.IsFinite(rules.CombatDamageRate) || rules.CombatDamageRate is < .25 or > 3 || rules.Conflict is < 0 or > 3 || rules.DisasterFrequency is < 0 or > 3
            || rules.DisasterStrength is < 1 or > 3 || !double.IsFinite(rules.DevelopmentRate)
            || rules.DevelopmentRate is < .5 or > 3 || !double.IsFinite(rules.MagicRate) || rules.MagicRate is < .5 or > 3)
            throw new ArgumentException("世界规则数值超出范围。");
    }

    public void ConfigureWorld(WorldRules rules, bool disasters, bool magic)
    {
        ValidateWorldRules(rules);
        State.Rules = rules with { };
        State.NaturalDisasters = disasters;
        State.Society.MagicEnabled = magic;
        AddEvent(WorldEventKind.Editor, "玩家调整世界规则，新的选择按新规则执行；已有项目与成果保留。");
    }

    public DevelopmentSummary GetDevelopment(int settlementId)
    {
        var town = RequireTown(settlementId);
        var research = State.Society.Research.First(r => r.SettlementId == town.Id);
        var construction = State.Society.Buildings.FirstOrDefault(b => b.SettlementId == town.Id && !b.IsCompleted);
        var stage = research.Completed.Count >= 3 ? "区域网络" : research.Completed.Count > 0 ? "专业分工" : town.Resources.Food >= town.Population * 2 ? "积累余粮" : "建立家园";
        if (research.Completed.Any(k => AdvancementRules.For(k) is not null)) stage = GetAdvancementStage(town.Id);
        if (construction is not null)
            return new(stage, "修建" + BuildingName(construction.Kind), State.Tick - construction.LastWorkedTick > 12 ? "等待工人实际到场；可查看居民任务" : "工人正在现场施工", construction.ConstructionProgress / construction.ConstructionRequired);
        if (research.ActiveProject is { } project)
        {
            var academy = State.Society.Buildings.FirstOrDefault(b => b.SettlementId == town.Id && b.Kind == BuildingKind.Academy && b.IsCompleted);
            return new(stage, "研究" + ResearchName(project), academy is null ? "需要已建成学院" : State.Tick - academy.LastWorkedTick > 12 ? "等待学者到学院工作" : "学者正在推进研究", research.Progress / Math.Max(1, research.RequiredProgress));
        }
        return new(stage, town.DevelopmentGoal, town.DevelopmentBlocker, 0);
    }

    /// <summary>Read-only validation shared by the map preview and the committing command.</summary>
    public string? FacilityPlacementError(int settlementId, BuildingKind kind, int x, int y, bool gift = false)
    {
        if (!Enum.IsDefined(kind)) return "未知的建筑类型";
        if (!_settlements.TryGetValue(settlementId, out var town)) return "先选择归属聚落";
        if (!InBounds(x, y) || !BuildingTerrainValid(kind, State.Tiles[Index(x, y)])) return kind == BuildingKind.Bridge ? "桥梁需要河流或浅水" : kind == BuildingKind.MountainPass ? "山路需要山地" : "需要可通行的陆地";
        var range = kind is BuildingKind.Bridge or BuildingKind.MountainPass ? 24 : 8;
        if (Distance(x, y, town.X, town.Y) > range) return $"距归属聚落超过 {range} 格";
        if (kind is BuildingKind.Bridge or BuildingKind.MountainPass && !Directions.Any(d => Walkable(x + d.X, y + d.Y))) return "需要相邻的可通行施工位置，逐段向前建设";
        if (kind == BuildingKind.Dock && !Directions.Any(d => InBounds(x + d.X, y + d.Y) && State.Tiles[Index(x + d.X, y + d.Y)].Terrain is TerrainType.Water or TerrainType.River or TerrainType.DeepWater)) return "船坞码头需要紧邻水岸";
        var tile = State.Tiles[Index(x, y)];
        if (tile.FireTicks > 0) return "此处正在燃烧";
        if (tile.NationId != 0 && tile.NationId != town.NationId) return "此处属于其他国家";
        if (State.Society.Buildings.Count >= MaxBuildings) return "世界建筑数量已达上限";
        if (State.Society.Buildings.Any(b => b.X == x && b.Y == y)) return "此处已有建筑";
        if ((kind == BuildingKind.ArcaneSanctum || AdvancementRules.For(kind)?.Magic == true) && !State.Society.MagicEnabled) return "规则已关闭新的魔法发展";
        if (kind == BuildingKind.SignalTower && (!HasResearch(settlementId, ResearchKind.Electrification) || !HasResearch(settlementId, ResearchKind.SignalNetwork))) return "无线信号塔需要电气化与信号网络";
        if (gift) return null;
        if (kind is BuildingKind.Bridge or BuildingKind.MountainPass && !HasResearch(settlementId, ResearchKind.Logistics)) return "需要先掌握驿路运输";
        if (kind == BuildingKind.Waystation && !HasResearch(settlementId, ResearchKind.Logistics)) return "当地尚未掌握驿路运输";
        if (kind == BuildingKind.SignalTower && !HasResearch(settlementId, ResearchKind.SignalNetwork)) return "当地尚未掌握信号网络";
        if (kind == BuildingKind.ArcaneSanctum && !HasResearch(settlementId, ResearchKind.ArcaneArts)) return "当地尚未掌握奥术基础";
        if (AdvancementRules.For(kind) is { } advancement && (!HasResearch(settlementId, advancement.Research)
            || advancement.Prerequisites.Any(p => !HasResearch(settlementId, p)))) return "当地尚未掌握" + ResearchName(advancement.Research) + "及其前置";
        return MissingResources(town.Resources, GetBuildingCost(kind));
    }

    public static string? MissingResources(ResourceStock stock, ResourceStock cost)
    {
        var missing = new List<string>();
        foreach (var kind in AdvancementRules.Resources)
            if (stock.Get(kind) + .000001 < cost.Get(kind)) missing.Add($"{ResourceStock.Name(kind)}缺 {cost.Get(kind) - stock.Get(kind):0.#}");
        return missing.Count == 0 ? null : string.Join(" · ", missing);
    }

    public int GrantFacility(int settlementId, BuildingKind kind, int x, int y) => PlaceFacility(settlementId, kind, x, y, true);

    public bool IsBuildingOperational(Building building) => IsFacilityOperating(building)
        && (building.Kind != BuildingKind.SignalTower || HasResearch(building.SettlementId, ResearchKind.SignalNetwork) && HasResearch(building.SettlementId, ResearchKind.Electrification));

    public string? RoadPlacementError(int settlementId, int x, int y, int radius = 0)
    {
        if (!_settlements.TryGetValue(settlementId, out var town)) return "先选择负责修路的聚落";
        if (!InBounds(x, y) || Distance(x, y, town.X, town.Y) > 24) return "距聚落超过 24 格";
        if (radius is < 0 or > 4) return "道路范围无效";
        var count = Circle(x, y, radius).Count(i => State.Tiles[i].IsWalkable && State.Tiles[i].RoadLevel == 0
            && (State.Tiles[i].NationId == 0 || State.Tiles[i].NationId == town.NationId));
        if (count == 0) return "此处不可修路，或已有道路";
        return MissingResources(town.Resources, new ResourceStock { Wood = count * .5, Stone = count });
    }

    private void DeliverLocalDiscoveries(Resident person, Settlement home)
    {
        foreach (var report in person.Agent.Memory.Where(f => f.Kind == AgentFactKind.WarReport && f.LearnedTick < State.Tick).ToArray())
            ReceiveWarReport(home, report);
        if (person.Profession is not (Profession.Messenger or Profession.Trader or Profession.Representative)) return;
        foreach (var fact in person.Agent.Memory.Where(f => f.LearnedTick < State.Tick && f.Kind is AgentFactKind.SettlementLocation or AgentFactKind.TradeExchange or AgentFactKind.DiplomaticNotice).ToArray())
        {
            var old = home.PublicKnowledge.FirstOrDefault(f => f.Kind == fact.Kind && f.SubjectId == fact.SubjectId && f.TargetNationId == fact.TargetNationId);
            if (old is not null && old.ObservedTick >= fact.ObservedTick) continue;
            var delivered = CopyAgentFact(fact); delivered.LearnedTick = State.Tick; delivered.SourceResidentId = person.Id;
            AddPublicFact(home, delivered);
            ReceiveDiplomaticNotice(home, delivered);
        }
    }

    private void ReceiveDiplomaticNotice(Settlement town, AgentFact fact)
    {
        if (fact.Kind != AgentFactKind.DiplomaticNotice || fact.SubjectId == town.NationId || fact.Value is < 0 or > 2
            || !_nations.ContainsKey(fact.SubjectId) || fact.Confidence < .4 || town.Id != _nations[town.NationId].CapitalId) return;
        // Only a delivered declaration addressed to this nation can become a local military order.
        if (fact.TargetNationId != town.NationId) return;
        var status = (DiplomaticStatus)(int)fact.Value;
        if (status == DiplomaticStatus.Allied)
        {
            var relation = Relation(town.NationId, fact.SubjectId);
            if (!State.Rules.Alliances || relation.Status != DiplomaticStatus.Neutral || relation.AllianceOfferNationId != fact.SubjectId
                || relation.AllianceOfferTick != fact.ObservedTick || State.Tick - fact.ObservedTick > 600) return;
            var knowsSender = town.PublicKnowledge.Any(f => f.Kind == AgentFactKind.SettlementLocation && (int)f.Value == fact.SubjectId && f.Confidence >= .4 && State.Tick - f.ObservedTick < 1200);
            if (!knowsSender) return;
            relation.Status = DiplomaticStatus.Allied; relation.LastChangedTick = State.Tick; relation.AllianceOfferNationId = 0;
            relation.Reason = "结盟提议已实际送达，对方依据已有接触消息接受";
            var alliance = AddEvent(WorldEventKind.Diplomacy, $"{_nations[town.NationId].Name}收到并接受{_nations[fact.SubjectId].Name}的结盟提议。", town.X, town.Y);
            alliance.SecondNationId = fact.SubjectId; alliance.CauseEventId = relation.LastEventId; relation.LastEventId = alliance.Id;
            return;
        }
        var prior = town.PublicKnowledge.Where(f => f.SubjectId == fact.SubjectId && f.Kind is AgentFactKind.WarOrder or AgentFactKind.PeaceOrder)
            .OrderByDescending(f => f.ObservedTick).FirstOrDefault();
        if (prior is not null && prior.ObservedTick >= fact.ObservedTick) return;
        if (status == DiplomaticStatus.Neutral) SetLocalOpinion(Relation(town.NationId, fact.SubjectId), town.NationId, 0);
        PublishDiplomaticOrder(town.NationId, fact.SubjectId, status, fact.X, fact.Y, fact.ObservedTick, eventId: fact.EventId, objective: WarObjective.DefendHomeland);
    }

    private static int LocalOpinion(DiplomaticRelation relation, int nationId) => nationId == relation.FirstNationId
        ? relation.FirstOpinion : relation.SecondOpinion;

    private static void SetLocalOpinion(DiplomaticRelation relation, int nationId, int opinion)
    {
        if (nationId == relation.FirstNationId) relation.FirstOpinion = Math.Clamp(opinion, -100, 100);
        else relation.SecondOpinion = Math.Clamp(opinion, -100, 100);
        relation.Opinion = (int)Math.Round((relation.FirstOpinion + relation.SecondOpinion) / 2d, MidpointRounding.AwayFromZero);
    }

    private sealed record DiplomaticAssessment(Nation Nation, Nation Other, Settlement Capital,
        AgentFact Contact, DiplomaticRelation Relation, double Food, int Change, string Reason, AgentFact? TradeReport);

    private void TickDiplomacy()
    {
        if (State.Tick % 60 != 0) return;
        var assessments = new List<DiplomaticAssessment>();
        foreach (var nation in State.Nations)
        {
            if (!_settlements.TryGetValue(nation.CapitalId, out var capital)) continue;
            var contacts = capital.PublicKnowledge.Where(f => f.Kind == AgentFactKind.SettlementLocation && f.LearnedTick < State.Tick
                && f.Confidence >= .4 && State.Tick - f.ObservedTick <= 1200 && f.Value != nation.Id && f.Value > 0)
                .GroupBy(f => (int)f.Value).Select(g => g.OrderByDescending(f => f.ObservedTick).First()).OrderBy(f => f.Value).ToArray();
            foreach (var contact in contacts)
            {
                var otherId = (int)contact.Value;
                if (!_nations.TryGetValue(otherId, out var other)) continue;
                var relation = Relation(nation.Id, otherId);
                if (relation.LastEvaluatedTick == State.Tick) continue;
                var ownFood = capital.Resources.Food;
                var cooperation = GetCulture(capital.CultureId).Cooperation;
                var tradeReport = capital.PublicKnowledge.Where(f => f.Kind == AgentFactKind.TradeExchange && f.SubjectId == otherId && State.Tick - f.ObservedTick <= 360).OrderByDescending(f => f.ObservedTick).FirstOrDefault();
                var trade = tradeReport is not null;
                var nearby = Distance(capital.X, capital.Y, contact.X, contact.Y) <= 28;
                var pressure = nearby && (ownFood < capital.Population || GetLocalPolicy(capital.Id) == PolicyKind.Defense);
                var change = trade ? 10 : pressure ? -(3 + State.Rules.Conflict * 3) : cooperation >= .55 ? 4 : State.Rules.Conflict >= 2 && nearby ? -5 : 1;
                var reason = trade ? "收到实际贸易交付的报告，往来改善关系" : pressure ? "收到邻近聚落的消息，本地粮食或防务压力加剧竞争" : "依据已经送达的聚落消息与当地合作倾向评估关系";
                assessments.Add(new(nation, other, capital, contact, relation, ownFood, change, reason, tradeReport));
            }
        }
        // Each capital remembers only its own assessments. The displayed pair average is never
        // an input to a nation's choices, and ordering uses the locations in delivered reports.
        foreach (var group in assessments.GroupBy(a => a.Relation)
            .OrderBy(group => group.Key.FirstNationId).ThenBy(group => group.Key.SecondNationId))
        {
            var sides = group.OrderBy(a => a.Capital.X).ThenBy(a => a.Capital.Y)
                .ThenBy(a => a.Contact.X).ThenBy(a => a.Contact.Y).ThenBy(a => a.Nation.Id).ToArray();
            var relation = group.Key;
            relation.LastEvaluatedTick = State.Tick;
            relation.LastContactTick = Math.Max(relation.LastContactTick, sides.Max(a => a.Contact.ObservedTick));
            foreach (var side in sides) SetLocalOpinion(relation, side.Nation.Id, LocalOpinion(relation, side.Nation.Id) + side.Change);
            relation.Reason = sides.Length == 1 ? sides[0].Reason : "双方各自依据已送达消息与当地情况累计态度；所示关系为双方态度均值";
            if (State.Tick - relation.LastChangedTick < 360) continue;
            // Resolve at most one action: either side can end an existing war; otherwise a war
            // declaration takes precedence over an alliance offer. No same-tick reversal follows.
            if (relation.Status == DiplomaticStatus.War)
            {
                var peacemaker = sides.Where(a => State.Rules.Peace && (State.Tick - relation.LastChangedTick >= 720
                    || a.Food < Math.Max(10, a.Capital.Population * .5))).OrderBy(a => a.Food / Math.Max(10, a.Capital.Population * .5)).FirstOrDefault();
                if (peacemaker is not null)
                    ChangeAutonomousDiplomacy(peacemaker.Nation, peacemaker.Other, relation, peacemaker.Contact,
                        DiplomaticStatus.Neutral, "战事持续或本地补给不足，宣布停战并休养");
                continue;
            }
            var declarer = sides.Where(a => State.Rules.Wars && State.Tick >= a.Nation.Military.RecoveryUntilTick && State.Rules.Conflict > 0 && LocalOpinion(relation, a.Nation.Id) <= -55
                && a.Food > 20 && a.Capital.Population >= 18).OrderBy(a => LocalOpinion(relation, a.Nation.Id)).FirstOrDefault();
            if (declarer is not null)
            {
                ChangeAutonomousDiplomacy(declarer.Nation, declarer.Other, relation, declarer.Contact, DiplomaticStatus.War, declarer.Reason);
                continue;
            }
            if (!State.Rules.Alliances || relation.Status != DiplomaticStatus.Neutral
                || relation.AllianceOfferNationId != 0 && State.Tick - relation.AllianceOfferTick <= 600) continue;
            var proposer = sides.Where(a => LocalOpinion(relation, a.Nation.Id) >= 55)
                .OrderByDescending(a => LocalOpinion(relation, a.Nation.Id)).FirstOrDefault();
            if (proposer is null) continue;
            var nation = proposer.Nation; var other = proposer.Other; var capital = proposer.Capital;
            relation.AllianceOfferNationId = nation.Id; relation.AllianceOfferTick = State.Tick;
            relation.Reason = "友好往来促成结盟提议，等待实际送达与回应";
            AddPublicFact(capital, new AgentFact { Id = NewId(), Kind = AgentFactKind.DiplomaticNotice, SubjectId = nation.Id,
                TargetNationId = other.Id, Value = (int)DiplomaticStatus.Allied, X = capital.X, Y = capital.Y,
                ObservedTick = State.Tick, LearnedTick = State.Tick, OriginResidentId = capital.RepresentativeId,
                SourceResidentId = capital.RepresentativeId, OriginProfession = Profession.Representative, Text = "友好往来促成结盟提议，请对方议事回应" });
            var proposal = AddEvent(WorldEventKind.Diplomacy, $"{nation.Name}向{other.Name}提出结盟，等待消息实际送达。", capital.X, capital.Y);
            proposal.Action = EventAction.Declaration; proposal.SettlementId = capital.Id; proposal.EvidenceFactId = proposer.TradeReport?.Id ?? proposer.Contact.Id;
            proposal.SecondNationId = other.Id; proposal.CauseEventId = proposer.TradeReport?.EventId > 0 ? proposer.TradeReport.EventId : relation.LastEventId; relation.LastEventId = proposal.Id;
        }
    }

    private void ChangeAutonomousDiplomacy(Nation nation, Nation other, DiplomaticRelation relation, AgentFact contact, DiplomaticStatus status, string reason)
    {
        var previous = relation.LastEventId;
        relation.Status = status; relation.LastChangedTick = State.Tick; relation.Reason = reason;
        if (status == DiplomaticStatus.Neutral) SetLocalOpinion(relation, nation.Id, 0);
        var capital = _settlements[nation.CapitalId];
        var entry = AddEvent(status == DiplomaticStatus.War ? WorldEventKind.War : WorldEventKind.Diplomacy,
            $"{nation.Name}与{other.Name}{(status == DiplomaticStatus.War ? "开战" : status == DiplomaticStatus.Allied ? "结盟" : "停战")}：{reason}。消息须实际传往对方与前线。", capital.X, capital.Y);
        entry.SecondNationId = other.Id; entry.CauseEventId = contact.EventId > 0 ? contact.EventId : previous;
        if (previous > 0 && previous != entry.CauseEventId) entry.AdditionalCauseEventIds.Add(previous);
        entry.Importance = EventImportance.Major; entry.Action = EventAction.Declaration; entry.SettlementId = capital.Id; entry.EvidenceFactId = contact.Id;
        PublishDiplomaticOrder(nation.Id, other.Id, status, contact.X, contact.Y,
            targetSettlementId: contact.Kind == AgentFactKind.SettlementLocation ? contact.SubjectId : 0, eventId: entry.Id);
        AddPublicFact(capital, new AgentFact { Id = NewId(), EventId = entry.Id, Kind = AgentFactKind.DiplomaticNotice, SubjectId = nation.Id,
            TargetNationId = other.Id, Value = (int)status, X = capital.X, Y = capital.Y, ObservedTick = State.Tick, LearnedTick = State.Tick,
            OriginResidentId = capital.RepresentativeId, SourceResidentId = capital.RepresentativeId, OriginProfession = Profession.Representative,
            Text = $"{nation.Name}的外交声明：{reason}" });

        relation.LastEventId = entry.Id;
    }

    private void TickMigrationAndSecession()
    {
        if (State.Tick % 60 != 0) return;
        foreach (var town in State.Settlements.ToArray())
        {
            var reports = State.Society.Reports.Where(r => r.RecipientSettlementId == town.Id && r.Confidence >= .5 && State.Tick - r.ObservedTick < 180).ToArray();
            var hardship = reports.Any(r => r.Topic == AgentFactKind.ReliefRequest && r.Value > 55);
            town.Unrest = Math.Clamp(town.Unrest + (hardship ? 5 + State.Rules.Conflict : -4), 0, 100);
            if (State.Rules.Secession && town.Unrest >= 80 && State.Tick - town.LastPoliticalChangeTick >= 1200
                && State.Nations.Count < 64 && _nations[town.NationId].CapitalId != town.Id && town.Population >= 12
                && State.Settlements.Count(t => t.NationId == town.NationId) > 1)
            {
                var parent = town.NationId;
                var id = SplitSettlement(town.Id, town.Name + "自由邦");
                town.Unrest = 20; town.LastPoliticalChangeTick = State.Tick;
                var entry = AddEvent(WorldEventKind.Founding, $"{town.Name}长期收到未解决的困苦诉求，宣布自治建国。", town.X, town.Y);
                entry.NationId = id; entry.SecondNationId = parent; entry.Action = EventAction.Secession; entry.SettlementId = town.Id;
                var evidence = reports.Where(r => r.Topic == AgentFactKind.ReliefRequest && r.Value > 55).OrderByDescending(r => r.ObservedTick).FirstOrDefault();
                entry.EvidenceFactId = evidence?.FactId ?? 0; entry.CauseEventId = evidence?.EventId ?? 0;
            }
        }
        if (!State.Rules.Migration) return;
        foreach (var person in State.Residents.Where(r => r.Age >= 16 && r.ArmyId == 0 && r.Hunger > 65 && r.Agent.DestinationSettlementId == 0
                     && !r.Agent.Goal.PlayerDirected && r.Agent.Goal.Kind != AgentGoalKind.Migrate).OrderBy(r => r.Id).ToArray())
        {
            var destination = person.Agent.Memory.Where(f => f.Kind == AgentFactKind.FoodSupply && f.SubjectId != person.SettlementId
                    && f.Value > 50 && AgentFactReliability(f) >= .5).OrderByDescending(f => f.Value).FirstOrDefault();
            if (destination is null || !_settlements.TryGetValue(destination.SubjectId, out var town)) continue;
            if (IsKnownHostile(person, town.NationId)) continue;
            person.Agent.Goal = new AgentGoal { Kind = AgentGoalKind.Migrate, TargetX = destination.X, TargetY = destination.Y,
                TargetSettlementId = town.Id, StartedTick = State.Tick, ReviewTick = State.Tick + 360, EvidenceFactId = destination.Id, CauseEventId = destination.EventId,
                Reason = "长期饥饿，依据收到的粮情步行寻找可接纳的新家园" };
            person.Agent.NextThinkTick = State.Tick + 6;
        }
    }
    private void ActOnMigration(Resident person)
    {
        var goal = person.Agent.Goal;
        if (Distance(person.X, person.Y, goal.TargetX, goal.TargetY) > 1)
        { MoveAgentTowards(person, goal.TargetX, goal.TargetY); person.Activity = ResidentActivity.Wandering; return; }
        if (!_settlements.TryGetValue(goal.TargetSettlementId, out var town) || Distance(person.X, person.Y, town.X, town.Y) > 1
            || town.Resources.Food < 10 || _citizens[town.Id].Count >= town.Housing || IsKnownHostile(person, town.NationId))
        {
            person.Agent.Goal.Kind = AgentGoalKind.Idle; person.Agent.NextThinkTick = State.Tick; return;
        }
        var old = person.SettlementId;
        person.SettlementId = town.Id; person.NationId = town.NationId;
        UpdateLocalWorkMembership(person, old);
        person.Agent.Goal = new AgentGoal { Kind = AgentGoalKind.ReturnHome, TargetX = town.X, TargetY = town.Y,
            TargetSettlementId = town.Id, StartedTick = State.Tick, Reason = "实地抵达后确认新家园可以接纳" };
        RememberAgentFact(person, MakeAgentFact(person, AgentFactKind.SettlementLocation, town.Id, town.X, town.Y, town.NationId, "步行抵达的新家园"));
        if (_citizens.TryGetValue(old, out var previous)) previous.Remove(person);
        _citizens[town.Id].Add(person);
        var entry = AddEvent(WorldEventKind.Growth, $"{person.Name}依据获知的粮情，步行迁入{town.Name}。", town.X, town.Y);
        entry.ResidentId = person.Id; entry.Action = EventAction.Migration; entry.SettlementId = town.Id; entry.SecondSettlementId = old;
        entry.EvidenceFactId = goal.EvidenceFactId; entry.CauseEventId = goal.CauseEventId;
        RecordLife(person, $"从原家园步行迁入{town.Name}，抵达后获接纳。", entry);
    }

}
