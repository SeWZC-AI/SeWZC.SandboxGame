using System.Collections.Immutable;
using System.Text.Json;
using SeWZC.WorldBox.Core.Runtime;

namespace SeWZC.WorldBox.Core;

public sealed partial class WorldEngine
{
    /// <summary>查找存活或归档居民，找不到时返回空值。</summary>
    /// <param name="id">居民的稳定 ID。</param>
    public Resident? GetResident(int id)
    {
        return (Residents.FirstOrDefault(r => r.Value.Id == id) ??
                ArchivedResidents.FirstOrDefault(r => r.Value.Id == id))?.Value;
    }

    /// <summary>将指定居民的认知与行动状态序列化为 JSON。</summary>
    /// <param name="id">居民的稳定 ID。</param>
    public string ExportResidentMind(int id)
    {
        return JsonSerializer.Serialize(RequireResident(id).Value.Agent, WorldJsonContext.Default.AgentState);
    }

    /// <summary>将指定居民的经历记录序列化为 JSON。</summary>
    /// <param name="id">居民的稳定 ID。</param>
    public string ExportResidentHistory(int id)
    {
        return JsonSerializer.Serialize(RequireResident(id).Value.History.ToList(),
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

    internal StateReference<Resident> RequireResident(int id)
    {
        return Residents.FirstOrDefault(r => r.Value.Id == id) ??
               ArchivedResidents.FirstOrDefault(r => r.Value.Id == id) ??
               throw new ArgumentException("居民不存在。", nameof(id));
    }

    /// <summary>设置自然灾害和魔法开关，保留已有发展成果。</summary>
    /// <param name="naturalDisasters">是否允许自主自然灾害。</param>
    /// <param name="magicEnabled">是否允许新的魔法发展和施法。</param>
    public void SetWorldRules(bool naturalDisasters, bool magicEnabled)
    {
        NaturalDisasters = naturalDisasters;
        Society = Society with { MagicEnabled = magicEnabled };
        AddEvent(WorldEventKind.Editor, "世界规则已更新；居民通过当地观察了解变化。");
    }

    /// <summary>先校验修改后的副本，再替换存活居民或归档记录；历史编辑只影响未来行为。</summary>
    /// <param name="id">居民的稳定 ID。</param>
    /// <param name="patch">仅替换非空字段的居民修改内容。</param>
    public void EditResident(int id, ResidentEdit patch)
    {
        ArgumentNullException.ThrowIfNull(patch);
        var original = RequireResident(id);
        var liveIndex = Residents.IndexOf(original);
        var isLive = liveIndex >= 0;
        var (candidate, startMission, nextId) = PrepareResidentEdit(original.Value, patch, isLive, State);
        NextId = nextId;
        var edited = new StateReference<Resident>(candidate);
        if (isLive)
            Residents[liveIndex] = edited;
        else
            ArchivedResidents[ArchivedResidents.IndexOf(original)] = edited;
        if (isLive)
        {
            if (startMission && _settlements.TryGetValue(candidate.SettlementId, out var missionHome))
                BeginAgentMission(edited, missionHome);
            Reindex();
            InitializeSociety();
            RefreshTotals();
        }

        var editEvent = AddEvent(WorldEventKind.Editor, $"{candidate.Name}的角色记录已修订；过去的世界结果保持原样。", candidate.X,
            candidate.Y);
        editEvent = PublishEvent(editEvent with { ResidentId = candidate.Id, NationId = candidate.NationId });
    }

    // 候选编辑只转换输入快照；校验通过后由命令提交居民和编号。
    private static (Resident Candidate, bool StartMission, int NextId) PrepareResidentEdit(
        Resident original, ResidentEdit patch, bool isLive, in WorldState state)
    {
        var candidate = original with
        {
            Name = patch.Name?.Trim() ?? original.Name,
            Race = patch.Race ?? original.Race,
            CultureId = patch.CultureId ?? original.CultureId,
        };
        var nextId = state.NextId;
        if (patch.SettlementId is { } townId && (isLive || townId != original.SettlementId))
        {
            if (state.Settlements.FirstOrDefault(town => town.Id == townId) is not { } town)
                throw new ArgumentException("目标聚落不存在。");
            candidate = candidate with { SettlementId = townId, NationId = town.NationId };
            if (townId != original.SettlementId)
            {
                candidate = candidate with { X = town.X, Y = town.Y, ArmyId = 0 };
            }
        }

        candidate = candidate with
        {
            X = patch.X ?? candidate.X,
            Y = patch.Y ?? candidate.Y,
            ArmyId = patch.ArmyId ?? candidate.ArmyId,
            SicknessTicks = patch.SicknessTicks ?? candidate.SicknessTicks,
            Inventory = patch.Inventory ?? candidate.Inventory,
            Profession = patch.Profession ?? candidate.Profession,
            Age = patch.Age ?? candidate.Age,
            Health = patch.Health ?? candidate.Health,
            DeathCause = isLive && patch.Health is <= 0 ? DeathCause.PlayerIntervention : candidate.DeathCause,
            DeathTick = isLive && patch.Health is <= 0 ? state.Tick : candidate.DeathTick,
            Hunger = patch.Hunger ?? candidate.Hunger,
            Thirst = patch.Thirst ?? candidate.Thirst,
            Trait = patch.Trait ?? candidate.Trait,
            Mana = patch.Mana ?? candidate.Mana,
            MagicTalent = patch.MagicTalent ?? candidate.MagicTalent,
            MagicTraining = patch.MagicTraining ?? candidate.MagicTraining,
            History = patch.History?.ToImmutableList() ?? candidate.History,
        };
        var agent = candidate.Agent;
        if (candidate.Profession != original.Profession || candidate.SettlementId != original.SettlementId)
        {
            agent = agent with { WorkplaceId = 0, WorkAreaIndex = -1 };
        }

        if (patch.Trait is not null)
        {
            var personality = agent.Personality;
            agent = agent with
            {
                Personality = patch.Trait switch
                {
                    "勤劳" => personality with { Diligence = .9 },
                    "勇敢" => personality with { Courage = .9 },
                    "好奇" => personality with { Ambition = .9 },
                    "温和" => personality with { Sociability = .9 },
                    _ => personality,
                },
            };
        }

        candidate = candidate with { Agent = patch.Agent ?? agent };

        ValidateResidentV2(candidate, state.Tick, state.Width, state.Height);
        ValidateStoryReferences(candidate, state.NextId);
        if (isLive)
        {
            if (!Coordinates(candidate.X, candidate.Y, state.Width, state.Height) || !CanTraverse(
                    state.Tiles[candidate.Y * state.Width + candidate.X],
                    candidate.TravelMode, candidate.Race))
                throw new ArgumentException("居民必须位于可通行地格。");
            if (candidate.ArmyId != 0 &&
                !state.Armies.Any(a => a.Id == candidate.ArmyId && a.NationId == candidate.NationId))
                throw new ArgumentException("军队不存在或与居民所属国家不一致。");
            if (candidate.CultureId != 0 && !state.Society.Cultures.Any(c => c.Id == candidate.CultureId))
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
            candidate = candidate with
            {
                Agent = candidate.Agent with
                {
                    Personality = personality with
                    {
                        Courage = Math.Clamp(personality.Courage + (achievement - hardship) * 0.1, 0, 1),
                        Sociability =
                        Math.Clamp(personality.Sociability + (kindness - betrayal) * 0.1, 0, 1),
                        Diligence = Math.Clamp(personality.Diligence + learning * 0.1, 0, 1),
                    },
                },
            };
            candidate = candidate with
            {
                History = candidate.History.Select(entry => entry with { PlayerEdited = true }).ToImmutableList(),
            };
        }

        if (patch.Agent is not null && candidate.Agent.Decisions.LastOrDefault() is { } thought &&
            candidate.Agent.Goal.Kind == original.Agent.Goal.Kind &&
            thought.Reason != original.Agent.Decisions.LastOrDefault()?.Reason)
        {
            candidate = candidate with
            {
                Agent = candidate.Agent.WithGoal(candidate.Agent.Goal with
                {
                    Kind = thought.Goal, Reason = thought.Reason, PlayerDirected = true, ReviewTick = state.Tick + 24,
                }),
            };
        }

        candidate = candidate with { Agent = candidate.Agent with { NextThinkTick = state.Tick } };
        if (candidate.X != original.X || candidate.Y != original.Y)
        {
            candidate = candidate with
            {
                FromX = candidate.X, FromY = candidate.Y, MoveStartedTick = state.Tick, MoveDurationTicks = 1,
            };
        }

        var startMission = isLive && patch.Agent is not null &&
                           candidate.Agent.Goal.Kind is AgentGoalKind.DeliverMessage or AgentGoalKind.Trade
                               or AgentGoalKind.Petition
                           && (original.Agent.Goal.Kind != candidate.Agent.Goal.Kind ||
                               original.Agent.DestinationSettlementId != candidate.Agent.Goal.TargetSettlementId);
        if (startMission)
        {
            if (candidate.ArmyId != 0)
                throw new ArgumentException("正在军队服役的居民需要先退役，才能执行民用运输任务。");
            if (state.Settlements.FirstOrDefault(town => town.Id == candidate.Agent.Goal.TargetSettlementId) is not
                { } destination)
                throw new ArgumentException("运输或递送目标需要选择有效聚落编号。");
            if (destination.Id == candidate.SettlementId && candidate.Agent.Goal.Kind == AgentGoalKind.Trade)
                throw new ArgumentException("贸易目标须为另一座聚落。");
            var address = candidate.Agent.Memory.FirstOrDefault(f =>
                f.Kind == AgentFactKind.SettlementLocation && f.SubjectId == destination.Id);
            if (address is null || address.X != destination.X || address.Y != destination.Y)
            {
                var memory = candidate.Agent.Memory;
                if (address is not null)
                    memory = memory.Remove(address);
                if (memory.Length == 16)
                    memory = memory.RemoveAt(0);
                memory = memory.Add(new AgentFact
                {
                    Kind = AgentFactKind.SettlementLocation,
                    SubjectId = destination.Id,
                    X = destination.X,
                    Y = destination.Y,
                    Value = destination.NationId,
                    ObservedTick = state.Tick,
                    LearnedTick = state.Tick,
                    OriginResidentId = candidate.Id,
                    SourceResidentId = candidate.Id,
                    OriginProfession = candidate.Profession,
                    Text = "玩家告知了本次递送的目的地",
                });
                candidate = candidate with { Agent = candidate.Agent with { Memory = memory } };
            }

            candidate = candidate with
            {
                Agent = candidate.Agent with
                {
                    Goal = candidate.Agent.Goal with
                    {
                        TargetX = destination.X, TargetY = destination.Y, StartedTick = state.Tick,
                    },
                    MissionRetryTick = state.Tick,
                },
            };
        }
        else if (patch.Agent is not null && candidate.Agent.Goal.Kind is not AgentGoalKind.Trade
                     and not AgentGoalKind.DeliverMessage and not AgentGoalKind.Petition)
        {
            candidate = candidate with
            {
                Agent = candidate.Agent with { DestinationSettlementId = 0, CarriedMessages = [] },
            };
        }

        // 编辑产生新身份的信息快照，已传播副本保留旧身份，避免修改过去收到的信息。
        if (patch.Agent is not null)
        {
            ValidateResidentV2(candidate, state.Tick, state.Width, state.Height);
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
                    assigned = nextId++;
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
                candidate = candidate with
                {
                    Agent = candidate.Agent with
                    {
                        Memory = candidate.Agent.Memory
                            .Select(item => ReferenceEquals(item, fact) ? revisedFact : item).ToImmutableArray(),
                        CarriedMessages = candidate.Agent.CarriedMessages
                            .Select(item => ReferenceEquals(item, fact) ? revisedFact : item).ToImmutableList(),
                    },
                };
            }

            candidate = candidate with
            {
                Agent = candidate.Agent with
                {
                    Decisions = candidate.Agent.Decisions.Select(decision =>
                        revisions.TryGetValue(decision.EvidenceFactId, out var revised)
                            ? decision with { EvidenceFactId = revised }
                            : decision).ToImmutableList(),
                },
            };
        }

        return (candidate, startMission, nextId);
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
