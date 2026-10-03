using System.Text.Json;

namespace SeWZC.WorldBox.Core;

public sealed partial class WorldEngine
{
    public Resident? GetResident(int id) => State.Residents.FirstOrDefault(r => r.Id == id) ?? State.ArchivedResidents.FirstOrDefault(r => r.Id == id);

    public string ExportResidentMind(int id) => JsonSerializer.Serialize(RequireResident(id).Agent, WorldJsonContext.Default.AgentState);
    public string ExportResidentHistory(int id) => JsonSerializer.Serialize(RequireResident(id).History, WorldJsonContext.Default.ListResidentHistoryEntry);

    public void EditResidentMindJson(int id, string json)
    {
        if (json.Length > 100_000) throw new ArgumentException("角色心智记录过大。");
        try { EditResident(id, new ResidentEdit { Agent = JsonSerializer.Deserialize(json, WorldJsonContext.Default.AgentState) ?? throw new ArgumentException("心智记录为空。") }); }
        catch (JsonException ex) { throw new ArgumentException("心智 JSON 格式无效。", nameof(json), ex); }
    }

    public void EditResidentHistoryJson(int id, string json)
    {
        if (json.Length > 100_000) throw new ArgumentException("角色历史记录过大。");
        try { EditResident(id, new ResidentEdit { History = JsonSerializer.Deserialize(json, WorldJsonContext.Default.ListResidentHistoryEntry) ?? throw new ArgumentException("历史记录为空。") }); }
        catch (JsonException ex) { throw new ArgumentException("历史 JSON 格式无效。", nameof(json), ex); }
    }

    private Resident RequireResident(int id) => GetResident(id) ?? throw new ArgumentException("居民不存在。", nameof(id));

    public void SetWorldRules(bool naturalDisasters, bool magicEnabled)
    {
        State.NaturalDisasters = naturalDisasters;
        State.Society.MagicEnabled = magicEnabled;
        AddEvent(WorldEventKind.Editor, "世界规则已更新；居民通过当地观察了解变化。");
    }

    public void EditResident(int id, ResidentEdit patch)
    {
        ArgumentNullException.ThrowIfNull(patch);
        var original = RequireResident(id);
        var liveIndex = State.Residents.IndexOf(original);
        var isLive = liveIndex >= 0;
        var candidate = JsonSerializer.Deserialize(JsonSerializer.Serialize(original, WorldJsonContext.Default.Resident), WorldJsonContext.Default.Resident)!;
        if (patch.Name is not null) candidate.Name = patch.Name.Trim();
        if (patch.Race is { } race) candidate.Race = race;
        if (patch.CultureId is { } culture) candidate.CultureId = culture;
        if (patch.SettlementId is { } townId && (isLive || townId != original.SettlementId))
        {
            if (!_settlements.TryGetValue(townId, out var town)) throw new ArgumentException("目标聚落不存在。");
            candidate.SettlementId = townId; candidate.NationId = town.NationId;
            if (townId != original.SettlementId) { candidate.X = town.X; candidate.Y = town.Y; candidate.ArmyId = 0; }
        }
        if (patch.X is { } x) candidate.X = x;
        if (patch.Y is { } y) candidate.Y = y;
        if (patch.ArmyId is { } army) candidate.ArmyId = army;
        if (patch.SicknessTicks is { } sickness) candidate.SicknessTicks = sickness;
        if (patch.Inventory is { } stock) candidate.Inventory = stock.Copy();
        if (patch.Profession is { } profession) candidate.Profession = profession;
        if (patch.Age is { } age) candidate.Age = age;
        if (patch.Health is { } health)
        {
            candidate.Health = health;
            if (isLive && health <= 0) { candidate.DeathCause = DeathCause.PlayerIntervention; candidate.DeathTick = State.Tick; }
        }
        if (patch.Hunger is { } hunger) candidate.Hunger = hunger;
        if (patch.Thirst is { } thirst) candidate.Thirst = thirst;
        if (patch.Trait is not null)
        {
            candidate.Trait = patch.Trait;
            switch (patch.Trait)
            {
                case "勤劳": candidate.Agent.Personality.Diligence = .9; break;
                case "勇敢": candidate.Agent.Personality.Courage = .9; break;
                case "好奇": candidate.Agent.Personality.Ambition = .9; break;
                case "温和": candidate.Agent.Personality.Sociability = .9; break;
            }
        }
        if (patch.Mana is { } mana) candidate.Mana = mana;
        if (patch.MagicTalent is { } talent) candidate.MagicTalent = talent;
        if (patch.MagicTraining is { } training) candidate.MagicTraining = training;
        if (patch.Agent is not null) candidate.Agent = JsonSerializer.Deserialize(JsonSerializer.Serialize(patch.Agent, WorldJsonContext.Default.AgentState), WorldJsonContext.Default.AgentState)!;
        if (patch.History is not null) candidate.History = JsonSerializer.Deserialize(JsonSerializer.Serialize(patch.History, WorldJsonContext.Default.ListResidentHistoryEntry), WorldJsonContext.Default.ListResidentHistoryEntry)!;
        ValidateResidentV2(candidate, State.Tick, State.Width, State.Height);
        ValidateStoryReferences(candidate, State.NextId);
        if (isLive)
        {
            if (!InBounds(candidate.X, candidate.Y) || !CanTraverse(State.Tiles[Index(candidate.X, candidate.Y)], candidate.TravelMode)) throw new ArgumentException("居民必须位于可通行地格。");
            if (candidate.ArmyId != 0 && !State.Armies.Any(a => a.Id == candidate.ArmyId && a.NationId == candidate.NationId)) throw new ArgumentException("军队不存在或与居民所属国家不一致。");
            if (candidate.CultureId != 0 && !State.Society.Cultures.Any(c => c.Id == candidate.CultureId)) throw new ArgumentException("文化不存在。");
        }
        if (patch.History is not null)
        {
            static double Impact(IEnumerable<ResidentHistoryEntry> entries, PersonalExperienceKind kind) => entries.Where(h => h.Experience == kind).Sum(h => h.Impact);
            var personality = candidate.Agent.Personality;
            var hardship = Impact(candidate.History, PersonalExperienceKind.Hardship) - Impact(original.History, PersonalExperienceKind.Hardship);
            var achievement = Impact(candidate.History, PersonalExperienceKind.Achievement) - Impact(original.History, PersonalExperienceKind.Achievement);
            var kindness = Impact(candidate.History, PersonalExperienceKind.Kindness) - Impact(original.History, PersonalExperienceKind.Kindness);
            var betrayal = Impact(candidate.History, PersonalExperienceKind.Betrayal) - Impact(original.History, PersonalExperienceKind.Betrayal);
            var learning = Impact(candidate.History, PersonalExperienceKind.Learning) - Impact(original.History, PersonalExperienceKind.Learning);
            personality.Courage = Math.Clamp(personality.Courage + (achievement - hardship) * 0.1, 0, 1);
            personality.Sociability = Math.Clamp(personality.Sociability + (kindness - betrayal) * 0.1, 0, 1);
            personality.Diligence = Math.Clamp(personality.Diligence + learning * 0.1, 0, 1);
            foreach (var entry in candidate.History) entry.PlayerEdited = true;
        }
        if (patch.Agent is not null && candidate.Agent.Decisions.LastOrDefault() is { } thought && candidate.Agent.Goal.Kind == original.Agent.Goal.Kind && thought.Reason != original.Agent.Decisions.LastOrDefault()?.Reason)
        {
            candidate.Agent.Goal.Kind = thought.Goal;
            candidate.Agent.Goal.Reason = thought.Reason;
            candidate.Agent.Goal.PlayerDirected = true;
            candidate.Agent.Goal.ReviewTick = State.Tick + 24;
        }
        candidate.Agent.NextThinkTick = State.Tick;
        if (candidate.X != original.X || candidate.Y != original.Y)
        {
            candidate.FromX = candidate.X; candidate.FromY = candidate.Y;
            candidate.MoveStartedTick = State.Tick; candidate.MoveDurationTicks = 1;
        }
        var startMission = isLive && patch.Agent is not null && candidate.Agent.Goal.Kind is AgentGoalKind.Trade or AgentGoalKind.DeliverMessage or AgentGoalKind.Petition
            && (original.Agent.Goal.Kind != candidate.Agent.Goal.Kind || original.Agent.DestinationSettlementId != candidate.Agent.Goal.TargetSettlementId);
        if (startMission)
        {
            if (candidate.ArmyId != 0) throw new ArgumentException("正在军队服役的居民需要先退役，才能执行民用运输任务。");
            if (!_settlements.TryGetValue(candidate.Agent.Goal.TargetSettlementId, out var destination)) throw new ArgumentException("运输或递送目标需要选择有效聚落编号。");
            if (destination.Id == candidate.SettlementId && candidate.Agent.Goal.Kind == AgentGoalKind.Trade) throw new ArgumentException("贸易目标须为另一座聚落。");
            var address = candidate.Agent.Memory.FirstOrDefault(f => f.Kind == AgentFactKind.SettlementLocation && f.SubjectId == destination.Id);
            if (address is null || address.X != destination.X || address.Y != destination.Y)
            {
                if (address is not null) candidate.Agent.Memory.Remove(address);
                if (candidate.Agent.Memory.Count == 16) candidate.Agent.Memory.RemoveAt(0);
                candidate.Agent.Memory.Add(new AgentFact { Kind = AgentFactKind.SettlementLocation, SubjectId = destination.Id, X = destination.X, Y = destination.Y,
                    Value = destination.NationId, ObservedTick = State.Tick, LearnedTick = State.Tick, OriginResidentId = candidate.Id, SourceResidentId = candidate.Id,
                    OriginProfession = candidate.Profession, Text = "玩家告知了本次递送的目的地" });
            }
            candidate.Agent.Goal.TargetX = destination.X; candidate.Agent.Goal.TargetY = destination.Y;
            candidate.Agent.Goal.StartedTick = State.Tick;
            candidate.Agent.MissionRetryTick = State.Tick;
        }
        else if (patch.Agent is not null && candidate.Agent.Goal.Kind is not AgentGoalKind.Trade and not AgentGoalKind.DeliverMessage and not AgentGoalKind.Petition)
        {
            candidate.Agent.DestinationSettlementId = 0;
            candidate.Agent.CarriedMessages.Clear();
        }
        // Edits create new immutable information snapshots. Propagated copies keep their old identities.
        if (patch.Agent is not null)
        {
            ValidateResidentV2(candidate, State.Tick, State.Width, State.Height);
            var changedFacts = candidate.Agent.Memory.Concat(candidate.Agent.CarriedMessages)
                .Where(fact => !original.Agent.Memory.Concat(original.Agent.CarriedMessages)
                    .Any(prior => fact.Id > 0 && prior.Id == fact.Id && SameFactSnapshot(prior, fact))).ToArray();
            var revisedSnapshots = new Dictionary<int, AgentFact>();
            foreach (var fact in changedFacts.Where(fact => fact.Id > 0))
            {
                if (revisedSnapshots.TryGetValue(fact.Id, out var prior) && !SameFactSnapshot(prior, fact))
                    throw new ArgumentException("相同记忆编号不能包含不同的修订内容。");
                revisedSnapshots[fact.Id] = fact;
            }
            var revisions = new Dictionary<int, int>();
            foreach (var fact in changedFacts)
            {
                var oldId = fact.Id;
                if (oldId > 0 && revisions.TryGetValue(oldId, out var assigned)) fact.Id = assigned;
                else
                {
                    fact.Id = NewId();
                    if (oldId > 0) revisions[oldId] = fact.Id;
                }
                if (fact.OriginResidentId == 0) { fact.OriginResidentId = candidate.Id; fact.OriginProfession = candidate.Profession; }
                if (fact.SourceResidentId == 0) fact.SourceResidentId = candidate.Id;
            }
            foreach (var decision in candidate.Agent.Decisions)
                if (revisions.TryGetValue(decision.EvidenceFactId, out var revised)) decision.EvidenceFactId = revised;
        }
        if (isLive) State.Residents[liveIndex] = candidate;
        else State.ArchivedResidents[State.ArchivedResidents.IndexOf(original)] = candidate;
        if (isLive)
        {
            if (startMission && _settlements.TryGetValue(candidate.SettlementId, out var missionHome)) BeginAgentMission(candidate, missionHome);
            Reindex(); InitializeSociety(); RefreshTotals();
        }
        var editEvent = AddEvent(WorldEventKind.Editor, $"{candidate.Name}的角色记录已修订；过去的世界结果保持原样。", candidate.X, candidate.Y);
        editEvent.ResidentId = candidate.Id;
        editEvent.NationId = candidate.NationId;
    }

    private static bool SameFactSnapshot(AgentFact first, AgentFact second) => first.Kind == second.Kind
        && first.EventId == second.EventId && first.CampaignEventId == second.CampaignEventId && first.WarObjective == second.WarObjective
        && first.TargetNationId == second.TargetNationId && first.SubjectId == second.SubjectId && first.X == second.X && first.Y == second.Y
        && first.Value == second.Value && first.ObservedTick == second.ObservedTick
        && first.OriginResidentId == second.OriginResidentId && first.OriginProfession == second.OriginProfession
        && first.Text == second.Text;
}
