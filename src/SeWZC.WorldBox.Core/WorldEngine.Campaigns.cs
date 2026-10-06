namespace SeWZC.WorldBox.Core;

public sealed partial class WorldEngine
{
    /// <summary>返回战役目标的中文名称。</summary>
    /// <param name="objective">战役的军事目标。</param>
    public static string ObjectiveName(WarObjective objective)
    {
        return objective == WarObjective.OccupySettlement ? "有限占领" : "保卫家园";
    }

    /// <summary>返回战役结果或撤退原因的中文说明。</summary>
    /// <param name="outcome">战役结果或撤退原因。</param>
    public static string OutcomeName(WarOutcome outcome)
    {
        return outcome switch
        {
            WarOutcome.ObjectiveReached => "已达成目标", WarOutcome.SupplyShortage => "补给不足",
            WarOutcome.HeavyLosses => "伤亡过重", WarOutcome.TargetChanged => "目标已变化",
            WarOutcome.OrdersReceived => "收到停战命令", WarOutcome.RouteBlocked => "道路受阻",
            WarOutcome.Exhausted => "长期作战，需要休整", _ => "尚在执行",
        };
    }

    private void EndCampaign(Army army, WarOutcome outcome, Resident[] soldiers, int cause = 0)
    {
        if (army.Outcome != WarOutcome.None) return;
        army.Outcome = outcome;
        army.Retreating = true;
        army.Gathering = false;
        army.Status = OutcomeName(outcome) + "，实际返乡并报告";
        var entry = AddEvent(WorldEventKind.War, $"{_nations[army.NationId].Name}的军队{army.Status}。", army.X, army.Y,
            EventAction.Retreat, army.TargetSettlementId, causeEventId: cause > 0 ? cause : army.LastEventId);
        entry.NationId = army.NationId;
        entry.SecondNationId = army.TargetNationId;
        army.LastEventId = entry.Id;
        // 编年史记录世界事实，但战报只能由实际目击者获得。
        var witnesses = soldiers.Where(r => r.Health > 0 && Distance(r.X, r.Y, army.X, army.Y) <= 3).ToArray();
        if (witnesses.Length == 0) return;
        var witness = witnesses.FirstOrDefault(r => r.Id == army.CommanderId) ?? witnesses[0];
        var report = MakeAgentFact(witness, AgentFactKind.WarReport, army.TargetNationId, army.X, army.Y, (int)outcome,
            $"{ObjectiveName(army.Objective)}：{OutcomeName(outcome)}；在场部队剩余 {soldiers.Length}/{army.InitialSoldiers} 人");
        report.TargetNationId = army.NationId;
        report.EventId = entry.Id;
        report.CampaignEventId = army.CampaignEventId;
        report.WarObjective = army.Objective;
        foreach (var person in witnesses)
        {
            RememberAgentFact(person, report);
            RecordLife(person, report.Text, entry,
                outcome == WarOutcome.ObjectiveReached
                    ? PersonalExperienceKind.Achievement
                    : PersonalExperienceKind.Hardship);
        }
    }

    private void RecordBattle(Army army, IEnumerable<Resident> soldiers, IEnumerable<Resident>? defenders = null)
    {
        if (army.BattleRecorded) return;
        army.BattleRecorded = true;
        var entry = AddEvent(WorldEventKind.War, $"{_nations[army.NationId].Name}的军队在目标附近实际交战。", army.X, army.Y,
            EventAction.Battle, army.TargetSettlementId, causeEventId: army.LastEventId);
        entry.NationId = army.NationId;
        entry.SecondNationId = army.TargetNationId;
        army.LastEventId = entry.Id;
        foreach (var person in soldiers.Concat(defenders ?? [])
                     .Where(r => r.Health > 0 && Distance(r.X, r.Y, army.X, army.Y) <= 5))
            RecordLife(person, "亲历交战，战斗结果见关联世界事件。", entry, PersonalExperienceKind.Hardship, EventImportance.Major);
    }

    private void ReceiveWarReport(Settlement town, AgentFact fact)
    {
        if (fact.Kind != AgentFactKind.WarReport || fact.TargetNationId != town.NationId || fact.Confidence < .4
            || !_nations.TryGetValue(town.NationId, out var nation) || nation.CapitalId != town.Id
            || fact.Value != Math.Truncate(fact.Value) || fact.Value < 1 ||
            fact.Value > (int)WarOutcome.Exhausted) return;
        var record = nation.Military;
        if (fact.CampaignEventId != record.CampaignEventId || fact.SubjectId != record.EnemyNationId
                                                           || fact.EventId == record.LastReportEventId ||
                                                           fact.ObservedTick < record.LastReportObservedTick) return;
        record.LastReportEventId = fact.EventId;
        record.LastReportObservedTick = fact.ObservedTick;
        record.LastReportReceivedTick = State.Tick;
        record.ReportedOutcome = (WarOutcome)(int)fact.Value;
        record.Report = fact.Text;
        record.RecoveryUntilTick = Math.Max(record.RecoveryUntilTick, State.Tick + 360);
        var received = AddEvent(WorldEventKind.War, $"{nation.Name}首都实际收到战报：{fact.Text}。", town.X, town.Y,
            EventAction.Report, town.Id, causeEventId: fact.EventId, evidenceFactId: fact.Id);
        received.NationId = nation.Id;
        received.SecondNationId = fact.SubjectId;
        if (!State.Rules.Peace || !_nations.TryGetValue(fact.SubjectId, out var other)) return;
        var relation = Relation(nation.Id, other.Id);
        if (relation.Status != DiplomaticStatus.War) return;
        // 已收到的战报本身足以作为依据，不应再要求额外的新聚落接触。
        ChangeAutonomousDiplomacy(nation, other, relation, fact, DiplomaticStatus.Neutral, "收到撤军战报，停止作战并进入恢复期");
    }
}
