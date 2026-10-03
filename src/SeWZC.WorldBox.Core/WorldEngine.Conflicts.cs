namespace SeWZC.WorldBox.Core;

public sealed partial class WorldEngine
{
    private bool HasResourcePressure(Resident person) => person.Hunger >= 40 && person.Inventory.Food < .5
        && person.Agent.Memory.Any(f => f.Kind == AgentFactKind.FoodSupply && f.Value < 12 && f.Confidence >= .5 && f.SubjectId == person.SettlementId && State.Tick - f.ObservedTick <= 180);

    private void TickLocalConflicts()
    {
        if (State.Tick % 12 != 0) return;
        var residents = State.Residents.ToDictionary(r => r.Id);
        State.Conflicts.RemoveAll(c => c.Stage == ConflictStage.Resolved && State.Tick - c.LastChangedTick > 360
            || !_settlements.ContainsKey(c.SettlementId));
        foreach (var conflict in State.Conflicts.Where(c => c.Stage != ConflictStage.Resolved))
        {
            conflict.Participants.RemoveAll(id => !residents.ContainsKey(id));
            var together = residents.TryGetValue(conflict.FirstResidentId, out var first)
                && residents.TryGetValue(conflict.SecondResidentId, out var second)
                && first.SettlementId == conflict.SettlementId && second.SettlementId == conflict.SettlementId
                && Distance(first.X, first.Y, second.X, second.Y) <= 2
                && Distance(first.X, first.Y, conflict.X, conflict.Y) <= 2;
            var pressured = together && HasResourcePressure(first!) && HasResourcePressure(residents[conflict.SecondResidentId]);
            conflict.Tension = Math.Clamp(conflict.Tension + (pressured && State.Rules.Conflict > 0 ? 4 * State.Rules.Conflict : -12), 0, 100);
            if (conflict.Tension <= 0 || !State.Rules.Wars || State.Rules.Conflict == 0)
            { ChangeConflictStage(conflict, ConflictStage.Resolved, "双方离开争夺地点、获得食物或停止冲突，争执平息"); continue; }
            var elapsed = State.Tick - conflict.StageStartedTick;
            if (pressured && elapsed >= 36 && conflict.Stage == ConflictStage.Dispute && conflict.Tension >= 40)
                ChangeConflictStage(conflict, ConflictStage.Confrontation, "资源争执持续，当事人开始对峙");
            else if (pressured && elapsed >= 48 && conflict.Stage == ConflictStage.Confrontation && conflict.Tension >= 75)
                ChangeConflictStage(conflict, ConflictStage.Violence, "持续对峙未解决，演变为局部斗殴");
            if (pressured && conflict.Stage == ConflictStage.Violence)
            {
                DamageResident(first!, .6 * State.Rules.Conflict, DeathCause.Conflict);
                DamageResident(residents[conflict.SecondResidentId], .6 * State.Rules.Conflict, DeathCause.Conflict);
                EmitVisual(WorldVisualKind.Battle, conflict.X, conflict.Y);
            }
            // Other people participate only after witnessing a persistent dispute locally.
            if (pressured && State.Tick - conflict.StartedTick >= 72 && conflict.Participants.Count < 16)
            {
                var witness = _citizens[conflict.SettlementId].Where(r => r.Age >= 14 && r.ArmyId == 0
                    && !conflict.Participants.Contains(r.Id) && HasResourcePressure(r)
                    && Distance(r.X, r.Y, conflict.X, conflict.Y) <= 2).OrderBy(r => r.Id).FirstOrDefault();
                if (witness is not null) conflict.Participants.Add(witness.Id);
                var scope = conflict.Participants.Count >= 8 && State.Tick - conflict.StartedTick >= 180
                    ? ConflictScope.Settlement : conflict.Participants.Count >= 4 ? ConflictScope.Group : ConflictScope.Individual;
                if (scope != conflict.Scope)
                { conflict.Scope = scope; RecordConflictEvent(conflict, "持续的现场资源争夺吸引更多当事人参与"); }
            }
        }
        if (!State.Rules.Wars || State.Rules.Conflict <= 0) return;
        foreach (var town in State.Settlements)
        {
            if (State.Conflicts.Count >= 128 || State.Conflicts.Any(c => c.SettlementId == town.Id
                && (c.Stage != ConflictStage.Resolved || State.Tick - c.LastChangedTick < 120))) continue;
            var candidates = _citizens[town.Id].Where(r => r.Age >= 14 && r.ArmyId == 0 && HasResourcePressure(r)).OrderBy(r => r.Id).Take(32).ToArray();
            for (var i = 0; i < candidates.Length; i++)
            {
                var first = candidates[i];
                var second = candidates.Skip(i + 1).FirstOrDefault(r => Distance(r.X, r.Y, first.X, first.Y) <= 1);
                if (second is null) continue;
                var conflict = new LocalConflict { Id = NewId(), FirstResidentId = first.Id, SecondResidentId = second.Id,
                    SettlementId = town.Id, X = first.X, Y = first.Y, Tension = 16,
                    StartedTick = State.Tick, StageStartedTick = State.Tick, LastChangedTick = State.Tick,
                    Participants = [first.Id, second.Id] };
                State.Conflicts.Add(conflict); RecordConflictEvent(conflict, "两位缺粮居民在现场为有限的食物发生争执");
                break;
            }
        }
    }

    private void ChangeConflictStage(LocalConflict conflict, ConflictStage stage, string reason)
    {
        conflict.Stage = stage; conflict.StageStartedTick = State.Tick; RecordConflictEvent(conflict, reason);
    }

    private void RecordConflictEvent(LocalConflict conflict, string reason)
    {
        conflict.LastChangedTick = State.Tick;
        var entry = AddEvent(WorldEventKind.Personal, $"{_settlements[conflict.SettlementId].Name}：{reason}。",
            conflict.X, conflict.Y, settlementId: conflict.SettlementId, residentId: conflict.FirstResidentId, causeEventId: conflict.LastEventId);
        entry.Importance = conflict.Stage == ConflictStage.Violence || conflict.Scope == ConflictScope.Settlement
            ? EventImportance.Major : EventImportance.Notable;
        conflict.LastEventId = entry.Id;
        foreach (var person in State.Residents.Where(r => conflict.Participants.Contains(r.Id)))
            RecordLife(person, reason, entry, conflict.Stage == ConflictStage.Resolved ? PersonalExperienceKind.Kindness : PersonalExperienceKind.Hardship);
    }

    private static void ValidateConflicts(WorldState state)
    {
        if (state.Conflicts is null || state.Conflicts.Count > 128) throw new ArgumentException("无效存档：局部冲突数量无效。");
        var ids = state.Residents.Concat(state.ArchivedResidents).Select(r => r.Id)
            .Concat(state.Settlements.Select(t => t.Id)).Concat(state.Nations.Select(n => n.Id))
            .Concat(state.Armies.Select(a => a.Id)).Concat(state.Society.Buildings.Select(b => b.Id)).ToHashSet();
        foreach (var conflict in state.Conflicts)
        {
            if (conflict is null || conflict.Id <= 0 || conflict.Id >= state.NextId || !ids.Add(conflict.Id)
                || conflict.FirstResidentId <= 0 || conflict.FirstResidentId >= state.NextId
                || conflict.SecondResidentId <= 0 || conflict.SecondResidentId >= state.NextId || conflict.FirstResidentId == conflict.SecondResidentId
                || !state.Settlements.Any(t => t.Id == conflict.SettlementId)
                || conflict.X < 0 || conflict.Y < 0 || conflict.X >= state.Width || conflict.Y >= state.Height
                || !Enum.IsDefined(conflict.Stage) || !Enum.IsDefined(conflict.Scope) || !double.IsFinite(conflict.Tension) || conflict.Tension is < 0 or > 100
                || conflict.StartedTick < 0 || conflict.StageStartedTick < conflict.StartedTick || conflict.LastChangedTick < conflict.StageStartedTick || conflict.LastChangedTick > state.Tick
                || conflict.LastEventId < 0 || conflict.LastEventId >= state.NextId
                || conflict.Participants is null || conflict.Participants.Count > 16 || conflict.Participants.Distinct().Count() != conflict.Participants.Count
                || conflict.Participants.Any(id => id <= 0 || id >= state.NextId)) throw new ArgumentException("无效存档：局部冲突状态无效。");
        }
    }
}
