using System.Text.Json;
using SeWZC.WorldBox.Core.Runtime;

namespace SeWZC.WorldBox.Core;

public sealed partial class WorldEngine
{
    /// <summary>查找存活或归档居民，找不到时返回空值。</summary>
    /// <param name="id">居民的稳定 ID。</param>
    public Resident? GetResident(int id)
    {
        return (Current.Residents.FirstOrDefault(r => r.Id == id) ??
                Current.ArchivedResidents.FirstOrDefault(r => r.Id == id))?.Value;
    }

    /// <summary>将指定居民的认知与行动状态序列化为 JSON。</summary>
    /// <param name="id">居民的稳定 ID。</param>
    public string ExportResidentMind(int id)
    {
        return JsonSerializer.Serialize(RequireResident(id).Agent, WorldJsonContext.Default.AgentState);
    }

    /// <summary>将指定居民的经历记录序列化为 JSON。</summary>
    /// <param name="id">居民的稳定 ID。</param>
    public string ExportResidentHistory(int id)
    {
        return JsonSerializer.Serialize(RequireResident(id).History.ToList(),
            WorldJsonContext.Default.ListResidentHistoryEntry);
    }

    /// <summary>解析并校验认知 JSON 后应用到居民，影响其未来行为。</summary>
    /// <param name="id">居民的稳定 ID。</param>
    /// <param name="json">修改后的居民认知与行动状态 JSON。</param>
    public void EditResidentMindJson(int id, string json)
    {
        if (json.Length > 100_000)
            throw new ArgumentException("角色心智记录过大。");
        try
        {
            EditResident(id,
                new ResidentEdit
                {
                    Agent = JsonSerializer.Deserialize(json, WorldJsonContext.Default.AgentState) ??
                            throw new ArgumentException("心智记录为空。"),
                });
        }
        catch (JsonException ex)
        {
            throw new ArgumentException("心智 JSON 格式无效。", nameof(json), ex);
        }
    }

    /// <summary>解析并校验经历 JSON 后应用到居民，影响其未来性格。</summary>
    /// <param name="id">居民的稳定 ID。</param>
    /// <param name="json">修改后的居民经历记录 JSON。</param>
    public void EditResidentHistoryJson(int id, string json)
    {
        if (json.Length > 100_000)
            throw new ArgumentException("角色历史记录过大。");
        try
        {
            EditResident(id,
                new ResidentEdit
                {
                    History = JsonSerializer.Deserialize(json, WorldJsonContext.Default.ListResidentHistoryEntry) ??
                              throw new ArgumentException("历史记录为空。"),
                });
        }
        catch (JsonException ex)
        {
            throw new ArgumentException("历史 JSON 格式无效。", nameof(json), ex);
        }
    }

    internal ResidentCursor RequireResident(int id)
    {
        return Current.Residents.FirstOrDefault(r => r.Id == id) ??
               Current.ArchivedResidents.FirstOrDefault(r => r.Id == id) ??
               throw new ArgumentException("居民不存在。", nameof(id));
    }

    /// <summary>设置自然灾害和魔法开关，保留已有发展成果。</summary>
    /// <param name="naturalDisasters">是否允许自主自然灾害。</param>
    /// <param name="magicEnabled">是否允许新的魔法发展和施法。</param>
    public void SetWorldRules(bool naturalDisasters, bool magicEnabled)
    {
        Current.NaturalDisasters = naturalDisasters;
        Current.Society.MagicEnabled = magicEnabled;
        AddEvent(WorldEventKind.Editor, "世界规则已更新；居民通过当地观察了解变化。");
    }

    /// <summary>先校验修改后的副本，再替换存活居民或归档记录；历史编辑只影响未来行为。</summary>
    /// <param name="id">居民的稳定 ID。</param>
    /// <param name="patch">仅替换非空字段的居民修改内容。</param>
    public void EditResident(int id, ResidentEdit patch)
    {
        ArgumentNullException.ThrowIfNull(patch);
        var original = RequireResident(id);
        var liveIndex = Current.Residents.IndexOf(original);
        var isLive = liveIndex >= 0;
        var candidate = new ResidentCursor(original.Value);
        if (patch.Name is not null)
            candidate.Name = patch.Name.Trim();
        if (patch.Race is { } race)
            candidate.Race = race;
        if (patch.CultureId is { } culture)
            candidate.CultureId = culture;
        if (patch.SettlementId is { } townId && (isLive || townId != original.SettlementId))
        {
            if (!_settlements.TryGetValue(townId, out var town))
                throw new ArgumentException("目标聚落不存在。");
            candidate.Replace(candidate.Value with { SettlementId = townId, NationId = town.NationId });
            if (townId != original.SettlementId)
                candidate.Replace(candidate.Value with { X = town.X, Y = town.Y, ArmyId = 0 });
        }

        if (patch.X is { } x)
            candidate.X = x;
        if (patch.Y is { } y)
            candidate.Y = y;
        if (patch.ArmyId is { } army)
            candidate.ArmyId = army;
        if (patch.SicknessTicks is { } sickness)
            candidate.SicknessTicks = sickness;
        if (patch.Inventory is { } stock)
            candidate.Inventory = stock;
        if (patch.Profession is { } profession)
            candidate.Profession = profession;
        if (candidate.Profession != original.Profession || candidate.SettlementId != original.SettlementId)
        {
            candidate.Agent.WorkplaceId = 0;
            candidate.Agent.WorkAreaIndex = -1;
        }

        if (patch.Age is { } age)
            candidate.Age = age;
        if (patch.Health is { } health)
        {
            candidate.Health = health;
            if (isLive && health <= 0)
                candidate.Replace(candidate.Value with
                {
                    DeathCause = DeathCause.PlayerIntervention, DeathTick = Current.Tick,
                });
        }

        if (patch.Hunger is { } hunger)
            candidate.Hunger = hunger;
        if (patch.Thirst is { } thirst)
            candidate.Thirst = thirst;
        if (patch.Trait is not null)
        {
            candidate.Trait = patch.Trait;
            switch (patch.Trait)
            {
                case "勤劳":
                    candidate.Agent.Personality = candidate.Agent.Personality with { Diligence = .9 };
                    break;
                case "勇敢":
                    candidate.Agent.Personality = candidate.Agent.Personality with { Courage = .9 };
                    break;
                case "好奇":
                    candidate.Agent.Personality = candidate.Agent.Personality with { Ambition = .9 };
                    break;
                case "温和":
                    candidate.Agent.Personality = candidate.Agent.Personality with { Sociability = .9 };
                    break;
            }
        }

        if (patch.Mana is { } mana)
            candidate.Mana = mana;
        if (patch.MagicTalent is { } talent)
            candidate.MagicTalent = talent;
        if (patch.MagicTraining is { } training)
            candidate.MagicTraining = training;
        if (patch.Agent is not null)
            candidate.Agent = new AgentStateCursor(patch.Agent);

        if (patch.History is not null)
            candidate.History = patch.History.ToList();

        ValidateResidentV2(candidate, Current.Tick, Current.Width, Current.Height);
        ValidateStoryReferences(candidate, Current.NextId);
        if (isLive)
        {
            if (!InBounds(candidate.X, candidate.Y) || !CanTraverse(Current.Tiles[Index(candidate.X, candidate.Y)],
                    candidate.TravelMode, candidate.Race))
                throw new ArgumentException("居民必须位于可通行地格。");
            if (candidate.ArmyId != 0 &&
                !Current.Armies.Any(a => a.Id == candidate.ArmyId && a.NationId == candidate.NationId))
                throw new ArgumentException("军队不存在或与居民所属国家不一致。");
            if (candidate.CultureId != 0 && !Current.Society.Cultures.Any(c => c.Id == candidate.CultureId))
                throw new ArgumentException("文化不存在。");
        }

        if (patch.History is not null)
        {
            static double Impact(IEnumerable<ResidentHistoryEntry> entries, PersonalExperienceKind kind)
            {
                return entries.Where(h => h.Experience == kind).Sum(h => h.Impact);
            }

            var personality = candidate.Agent.Personality;
            var hardship = Impact(candidate.History, PersonalExperienceKind.Hardship) -
                           Impact(original.History, PersonalExperienceKind.Hardship);
            var achievement = Impact(candidate.History, PersonalExperienceKind.Achievement) -
                              Impact(original.History, PersonalExperienceKind.Achievement);
            var kindness = Impact(candidate.History, PersonalExperienceKind.Kindness) -
                           Impact(original.History, PersonalExperienceKind.Kindness);
            var betrayal = Impact(candidate.History, PersonalExperienceKind.Betrayal) -
                           Impact(original.History, PersonalExperienceKind.Betrayal);
            var learning = Impact(candidate.History, PersonalExperienceKind.Learning) -
                           Impact(original.History, PersonalExperienceKind.Learning);
            candidate.Agent.Personality = personality with
            {
                Courage = Math.Clamp(personality.Courage + (achievement - hardship) * 0.1, 0, 1),
                Sociability = Math.Clamp(personality.Sociability + (kindness - betrayal) * 0.1, 0, 1),
                Diligence = Math.Clamp(personality.Diligence + learning * 0.1, 0, 1),
            };
            candidate.History = candidate.History.Select(entry => entry with { PlayerEdited = true }).ToList();
        }

        if (patch.Agent is not null && candidate.Agent.Decisions.LastOrDefault() is { } thought &&
            candidate.Agent.Goal.Kind == original.Agent.Goal.Kind &&
            thought.Reason != original.Agent.Decisions.LastOrDefault()?.Reason)
        {
            candidate.Agent.Goal = candidate.Agent.Goal with
            {
                Kind = thought.Goal, Reason = thought.Reason, PlayerDirected = true, ReviewTick = Current.Tick + 24,
            };
        }

        candidate.Agent.NextThinkTick = Current.Tick;
        if (candidate.X != original.X || candidate.Y != original.Y)
            candidate.Replace(candidate.Value with
            {
                FromX = candidate.X, FromY = candidate.Y, MoveStartedTick = Current.Tick, MoveDurationTicks = 1,
            });

        var startMission = isLive && patch.Agent is not null &&
                           candidate.Agent.Goal.Kind is AgentGoalKind.DeliverMessage or AgentGoalKind.Trade
                               or AgentGoalKind.Petition
                           && (original.Agent.Goal.Kind != candidate.Agent.Goal.Kind ||
                               original.Agent.DestinationSettlementId != candidate.Agent.Goal.TargetSettlementId);
        if (startMission)
        {
            if (candidate.ArmyId != 0)
                throw new ArgumentException("正在军队服役的居民需要先退役，才能执行民用运输任务。");
            if (!_settlements.TryGetValue(candidate.Agent.Goal.TargetSettlementId, out var destination))
                throw new ArgumentException("运输或递送目标需要选择有效聚落编号。");
            if (destination.Id == candidate.SettlementId && candidate.Agent.Goal.Kind == AgentGoalKind.Trade)
                throw new ArgumentException("贸易目标须为另一座聚落。");
            var address = candidate.Agent.Memory.FirstOrDefault(f =>
                f.Kind == AgentFactKind.SettlementLocation && f.SubjectId == destination.Id);
            if (address is null || address.X != destination.X || address.Y != destination.Y)
            {
                if (address is not null)
                    candidate.Agent.Memory.Remove(address);
                if (candidate.Agent.Memory.Count == 16)
                    candidate.Agent.Memory.RemoveAt(0);
                candidate.Agent.Memory.Add(new AgentFact
                {
                    Kind = AgentFactKind.SettlementLocation,
                    SubjectId = destination.Id,
                    X = destination.X,
                    Y = destination.Y,
                    Value = destination.NationId,
                    ObservedTick = Current.Tick,
                    LearnedTick = Current.Tick,
                    OriginResidentId = candidate.Id,
                    SourceResidentId = candidate.Id,
                    OriginProfession = candidate.Profession,
                    Text = "玩家告知了本次递送的目的地",
                });
            }

            candidate.Replace(candidate.Value with
            {
                Agent = candidate.Agent.Value with
                {
                    Goal = candidate.Agent.Goal with
                    {
                        TargetX = destination.X, TargetY = destination.Y, StartedTick = Current.Tick,
                    },
                    MissionRetryTick = Current.Tick,
                },
            });
        }
        else if (patch.Agent is not null && candidate.Agent.Goal.Kind is not AgentGoalKind.Trade
                     and not AgentGoalKind.DeliverMessage and not AgentGoalKind.Petition)
        {
            candidate.Agent.DestinationSettlementId = 0;
            candidate.Agent.CarriedMessages.Clear();
        }

        // 编辑产生新身份的信息快照，已传播副本保留旧身份，避免修改过去收到的信息。
        if (patch.Agent is not null)
        {
            ValidateResidentV2(candidate, Current.Tick, Current.Width, Current.Height);
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
                if (oldId <= 0 || !revisions.TryGetValue(oldId, out var assigned))
                {
                    assigned = NewId();
                    if (oldId > 0)
                        revisions[oldId] = assigned;
                }

                var revisedFact = fact with
                {
                    Id = assigned,
                    OriginResidentId = fact.OriginResidentId == 0 ? candidate.Id : fact.OriginResidentId,
                    OriginProfession = fact.OriginResidentId == 0 ? candidate.Profession : fact.OriginProfession,
                    SourceResidentId = fact.SourceResidentId == 0 ? candidate.Id : fact.SourceResidentId,
                };
                candidate.Agent.Memory = candidate.Agent.Memory
                    .Select(item => ReferenceEquals(item, fact) ? revisedFact : item).ToList();
                candidate.Agent.CarriedMessages = candidate.Agent.CarriedMessages
                    .Select(item => ReferenceEquals(item, fact) ? revisedFact : item).ToList();
            }

            candidate.Agent.Decisions = candidate.Agent.Decisions.Select(decision =>
                revisions.TryGetValue(decision.EvidenceFactId, out var revised)
                    ? decision with { EvidenceFactId = revised }
                    : decision).ToList();
        }

        if (isLive)
            Current.Residents[liveIndex] = candidate;
        else
            Current.ArchivedResidents[Current.ArchivedResidents.IndexOf(original)] = candidate;
        if (isLive)
        {
            if (startMission && _settlements.TryGetValue(candidate.SettlementId, out var missionHome))
                BeginAgentMission(candidate, missionHome);
            Reindex();
            InitializeSociety();
            RefreshTotals();
        }

        var editEvent = AddEvent(WorldEventKind.Editor, $"{candidate.Name}的角色记录已修订；过去的世界结果保持原样。", candidate.X,
            candidate.Y);
        editEvent.Replace(editEvent.Value with { ResidentId = candidate.Id, NationId = candidate.NationId });
    }

    private static bool SameFactSnapshot(AgentFact first, AgentFact second)
    {
        return first.Kind == second.Kind
               && first.EventId == second.EventId && first.CampaignEventId == second.CampaignEventId &&
               first.WarObjective == second.WarObjective
               && first.TargetNationId == second.TargetNationId && first.SubjectId == second.SubjectId &&
               first.X == second.X && first.Y == second.Y
               && first.Value == second.Value && first.ObservedTick == second.ObservedTick
               && first.OriginResidentId == second.OriginResidentId && first.OriginProfession == second.OriginProfession
               && first.Text == second.Text;
    }
}
