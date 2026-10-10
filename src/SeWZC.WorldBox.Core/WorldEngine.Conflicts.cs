using SeWZC.WorldBox.Core.Runtime;

namespace SeWZC.WorldBox.Core;

public sealed partial class WorldEngine
{
    private bool HasResourcePressure(StateReference<Resident> person)
    {
        return person.Value.Hunger >= 40 && person.Value.Inventory.Food < .5
                                   && person.Value.Agent.Memory.Any(f =>
                                       f.Kind == AgentFactKind.FoodSupply && f.Value < 12 && f.Confidence >= .5 &&
                                       f.SubjectId == person.Value.SettlementId &&
                                       SimulationTick - f.ObservedTick <= 2 * SimulationTime.TicksPerMonth);
    }

    private void TickLocalConflicts()
    {
        if (SimulationTick % 12 != 0)
            return;
        var residents = LiveResidentsById();
        Conflicts = Conflicts.RemoveAll(c =>
            (c.Stage == ConflictStage.Resolved && SimulationTick - c.LastChangedTick > 3 * SimulationTime.TicksPerYear)
            || !_settlements.ContainsKey(c.SettlementId));
        foreach (var previous in Conflicts.Where(c => c.Stage != ConflictStage.Resolved))
        {
            var conflict = previous;
            var together = residents.TryGetValue(conflict.FirstResidentId, out var first)
                           && residents.TryGetValue(conflict.SecondResidentId, out var second)
                           && first.Value.SettlementId == conflict.SettlementId &&
                           second.Value.SettlementId == conflict.SettlementId
                           && Distance(first.Value.X, first.Value.Y, second.Value.X, second.Value.Y) <= 2
                           && Distance(first.Value.X, first.Value.Y, conflict.X, conflict.Y) <= 2;
            var pressured = together && HasResourcePressure(first!) &&
                            HasResourcePressure(residents[conflict.SecondResidentId]);
            conflict = PublishConflict(conflict with
            {
                Participants = conflict.Participants.RemoveAll(id => !residents.ContainsKey(id)),
                Tension = Math.Clamp(
                    conflict.Tension + (pressured && Rules.Conflict > 0 ? 4 * Rules.Conflict : -12),
                    0, 100),
            });
            if (conflict.Tension <= 0 || !Rules.Wars || Rules.Conflict == 0)
            {
                conflict = ChangeConflictStage(conflict, ConflictStage.Resolved, "双方离开争夺地点、获得食物或停止冲突，争执平息");
                continue;
            }

            var elapsed = SimulationTick - conflict.StageStartedTick;
            if (pressured && elapsed >= 36 && conflict.Stage == ConflictStage.Dispute && conflict.Tension >= 40)
                conflict = ChangeConflictStage(conflict, ConflictStage.Confrontation, "资源争执持续，当事人开始对峙");
            else if (pressured && elapsed >= 48 && conflict.Stage == ConflictStage.Confrontation &&
                     conflict.Tension >= 75)
                conflict = ChangeConflictStage(conflict, ConflictStage.Violence, "持续对峙未解决，演变为局部斗殴");
            if (pressured && conflict.Stage == ConflictStage.Violence)
            {
                DamageResident(first!, .6 * Rules.Conflict, DeathCause.Conflict);
                DamageResident(residents[conflict.SecondResidentId], .6 * Rules.Conflict, DeathCause.Conflict);
                EmitVisual(WorldVisualKind.Battle, conflict.X, conflict.Y);
            }

            // 其他居民须在本地目击持续争端后才参与，避免冲突隔空扩散。
            if (pressured && SimulationTick - conflict.StartedTick >= 72 && conflict.Participants.Count < 16)
            {
                var witness = _citizens[conflict.SettlementId].Where(r => r.Value.Age >= 14 && r.Value.ArmyId == 0
                    && !conflict.Participants.Contains(r.Value.Id) && HasResourcePressure(r)
                    && Distance(r.Value.X, r.Value.Y, conflict.X, conflict.Y) <= 2).OrderBy(r => r.Value.Id).FirstOrDefault();
                if (witness is not null)
                    conflict = PublishConflict(conflict with { Participants = conflict.Participants.Add(witness.Value.Id) });
                var scope = conflict.Participants.Count >= 8 &&
                            SimulationTick - conflict.StartedTick >= 2 * SimulationTime.TicksPerMonth
                    ? ConflictScope.Settlement
                    : conflict.Participants.Count >= 4
                        ? ConflictScope.Group
                        : ConflictScope.Individual;
                if (scope != conflict.Scope)
                {
                    conflict = PublishConflict(conflict with { Scope = scope });
                    conflict = RecordConflictEvent(conflict, "持续的现场资源争夺吸引更多当事人参与");
                }
            }
        }

        if (!Rules.Wars || Rules.Conflict <= 0)
            return;
        foreach (var town in Settlements)
        {
            if (Conflicts.Count >= 128 || Conflicts.Any(c => c.SettlementId == town.Value.Id
                                                                             && (c.Stage != ConflictStage.Resolved ||
                                                                                 SimulationTick - c.LastChangedTick <
                                                                                 120)))
                continue;
            var candidates = _citizens[town.Value.Id].Where(r => r.Value.Age >= 14 && r.Value.ArmyId == 0 && HasResourcePressure(r))
                .OrderBy(r => r.Value.Id).Take(32).ToArray();
            for (var i = 0; i < candidates.Length; i++)
            {
                var first = candidates[i];
                var second = candidates.Skip(i + 1).FirstOrDefault(r => Distance(r.Value.X, r.Value.Y, first.Value.X, first.Value.Y) <= 1);
                if (second is null)
                    continue;
                var conflict = new LocalConflict
                {
                    Id = NewId(),
                    FirstResidentId = first.Value.Id,
                    SecondResidentId = second.Value.Id,
                    SettlementId = town.Value.Id,
                    X = first.Value.X,
                    Y = first.Value.Y,
                    Tension = 16,
                    StartedTick = SimulationTick,
                    StageStartedTick = SimulationTick,
                    LastChangedTick = SimulationTick,
                    Participants = [first.Value.Id, second.Value.Id],
                };
                Conflicts = Conflicts.Add(conflict);
                conflict = RecordConflictEvent(conflict, "两位缺粮居民在现场为有限的食物发生争执");
                break;
            }
        }
    }

    private LocalConflict ChangeConflictStage(LocalConflict conflict, ConflictStage stage, string reason)
    {
        conflict = conflict with { Stage = stage, StageStartedTick = SimulationTick };
        return RecordConflictEvent(conflict, reason);
    }

    private LocalConflict RecordConflictEvent(LocalConflict conflict, string reason)
    {
        conflict = conflict with { LastChangedTick = SimulationTick };
        var entry = AddEvent(WorldEventKind.Personal, $"{_settlements[conflict.SettlementId].Value.Name}：{reason}。",
            conflict.X, conflict.Y, settlementId: conflict.SettlementId, residentId: conflict.FirstResidentId,
            causeEventId: conflict.LastEventId);
        entry = PublishEvent(entry with
        {
            Importance = conflict.Stage == ConflictStage.Violence || conflict.Scope == ConflictScope.Settlement
                ? EventImportance.Major
                : EventImportance.Notable,
        });
        conflict = PublishConflict(conflict with { LastEventId = entry.Id });
        foreach (var person in Residents.Where(r => conflict.Participants.Contains(r.Value.Id)))
            RecordLife(person, reason, entry,
                conflict.Stage == ConflictStage.Resolved
                    ? PersonalExperienceKind.Kindness
                    : PersonalExperienceKind.Hardship);
        return conflict;
    }

    private static void ValidateConflicts(WorldState state)
    {
        if (state.Conflicts is null || state.Conflicts.Count > 128)
            throw new ArgumentException("无效存档：局部冲突数量无效。");
        var ids = state.Residents.Concat(state.ArchivedResidents).Select(r => r.Id)
            .Concat(state.Settlements.Select(t => t.Id)).Concat(state.Nations.Select(n => n.Id))
            .Concat(state.Armies.Select(a => a.Id)).Concat(state.Society.Buildings.Select(b => b.Id)).ToHashSet();
        foreach (var conflict in state.Conflicts)
            if (conflict is null || conflict.Id <= 0 || conflict.Id >= state.NextId || !ids.Add(conflict.Id)
                || conflict.FirstResidentId <= 0 || conflict.FirstResidentId >= state.NextId
                || conflict.SecondResidentId <= 0 || conflict.SecondResidentId >= state.NextId ||
                conflict.FirstResidentId == conflict.SecondResidentId
                || !state.Settlements.Any(t => t.Id == conflict.SettlementId)
                || conflict.X < 0 || conflict.Y < 0 || conflict.X >= state.Width || conflict.Y >= state.Height
                || !Enum.IsDefined(conflict.Stage) || !Enum.IsDefined(conflict.Scope) ||
                !double.IsFinite(conflict.Tension) || conflict.Tension is < 0 or > 100
                || conflict.StartedTick < 0 || conflict.StageStartedTick < conflict.StartedTick ||
                conflict.LastChangedTick < conflict.StageStartedTick || conflict.LastChangedTick > state.Tick
                || conflict.LastEventId < 0 || conflict.LastEventId >= state.NextId
                || conflict.Participants is null || conflict.Participants.Count > 16 ||
                conflict.Participants.Distinct().Count() != conflict.Participants.Count
                || conflict.Participants.Any(id => id <= 0 || id >= state.NextId))
                throw new ArgumentException("无效存档：局部冲突状态无效。");
    }
}
