using SeWZC.WorldBox.Core.Runtime;

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
            WarOutcome.ObjectiveReached => "已达成目标",
            WarOutcome.SupplyShortage => "补给不足",
            WarOutcome.HeavyLosses => "伤亡过重",
            WarOutcome.TargetChanged => "目标已变化",
            WarOutcome.OrdersReceived => "收到停战命令",
            WarOutcome.RouteBlocked => "道路受阻",
            WarOutcome.Exhausted => "长期作战，需要休整",
            _ => "尚在执行",
        };
    }

    private void EndCampaign(StateReference<Army> army, WarOutcome outcome, ResidentCursor[] soldiers, int cause = 0)
    {
        if (army.Value.Outcome != WarOutcome.None)
            return;
        army.Replace(army.Value with
        {
            Outcome = outcome,
            Retreating = true,
            Gathering = false,
            Status = OutcomeName(outcome) + "，实际返乡并报告",
        });
        var entry = AddEvent(WorldEventKind.War, $"{_nations[army.Value.NationId].Value.Name}的军队{army.Value.Status}。", army.Value.X, army.Value.Y,
            EventAction.Retreat, army.Value.TargetSettlementId, causeEventId: cause > 0 ? cause : army.Value.LastEventId);
        entry = PublishEvent(entry with { NationId = army.Value.NationId, SecondNationId = army.Value.TargetNationId });
        army.Replace(army.Value with { LastEventId = entry.Id });
        // 编年史记录世界事实，但战报只能由实际目击者获得。
        var witnesses = soldiers.Where(r => r.Health > 0 && Distance(r.X, r.Y, army.Value.X, army.Value.Y) <= 3).ToArray();
        if (witnesses.Length == 0)
            return;
        var witness = witnesses.FirstOrDefault(r => r.Id == army.Value.CommanderId) ?? witnesses[0];
        var report = MakeAgentFact(witness, AgentFactKind.WarReport, army.Value.TargetNationId, army.Value.X, army.Value.Y, (int)outcome,
            $"{ObjectiveName(army.Value.Objective)}：{OutcomeName(outcome)}；在场部队剩余 {soldiers.Length}/{army.Value.InitialSoldiers} 人");
        report = report with
        {
            TargetNationId = army.Value.NationId,
            EventId = entry.Id,
            CampaignEventId = army.Value.CampaignEventId,
            WarObjective = army.Value.Objective,
        };
        foreach (var person in witnesses)
        {
            RememberAgentFact(person, report);
            RecordLife(person, report.Text, entry,
                outcome == WarOutcome.ObjectiveReached
                    ? PersonalExperienceKind.Achievement
                    : PersonalExperienceKind.Hardship);
        }
    }

    private void RecordBattle(StateReference<Army> army, IEnumerable<ResidentCursor> soldiers,
        IEnumerable<ResidentCursor>? defenders = null)
    {
        if (army.Value.BattleRecorded)
            return;
        army.Replace(army.Value with { BattleRecorded = true });
        var entry = AddEvent(WorldEventKind.War, $"{_nations[army.Value.NationId].Value.Name}的军队在目标附近实际交战。", army.Value.X, army.Value.Y,
            EventAction.Battle, army.Value.TargetSettlementId, causeEventId: army.Value.LastEventId);
        entry = PublishEvent(entry with { NationId = army.Value.NationId, SecondNationId = army.Value.TargetNationId });
        army.Replace(army.Value with { LastEventId = entry.Id });
        foreach (var person in soldiers.Concat(defenders ?? [])
                     .Where(r => r.Health > 0 && Distance(r.X, r.Y, army.Value.X, army.Value.Y) <= 5))
            RecordLife(person, "亲历交战，战斗结果见关联世界事件。", entry, PersonalExperienceKind.Hardship, EventImportance.Major);
    }

    internal void ReceiveWarReport(StateReference<Settlement> town, AgentFact fact)
    {
        if (fact.Kind != AgentFactKind.WarReport || fact.TargetNationId != town.Value.NationId || fact.Confidence < .4
            || !_nations.TryGetValue(town.Value.NationId, out var nation) || nation.Value.CapitalId != town.Value.Id
            || fact.Value != Math.Truncate(fact.Value) || fact.Value < 1 ||
            fact.Value > (int)WarOutcome.Exhausted)
            return;
        if (fact.CampaignEventId != nation.Value.Military.CampaignEventId || fact.SubjectId != nation.Value.Military.EnemyNationId
                                                                    || fact.EventId ==
                                                                    nation.Value.Military.LastReportEventId ||
                                                                    fact.ObservedTick <
                                                                    nation.Value.Military.LastReportObservedTick)
            return;
        nation.Replace(nation.Value with
        {
            Military = nation.Value.Military with
        {
            LastReportEventId = fact.EventId,
            LastReportObservedTick = fact.ObservedTick,
            LastReportReceivedTick = SimulationTick,
            ReportedOutcome = (WarOutcome)(int)fact.Value,
            Report = fact.Text,
            RecoveryUntilTick = Math.Max(nation.Value.Military.RecoveryUntilTick,
                SimulationTick + 3 * SimulationTime.TicksPerYear),
            }
        });
        var received = AddEvent(WorldEventKind.War, $"{nation.Value.Name}首都实际收到战报：{fact.Text}。", town.Value.X, town.Value.Y,
            EventAction.Report, town.Value.Id, causeEventId: fact.EventId, evidenceFactId: fact.Id);
        received = PublishEvent(received with { NationId = nation.Value.Id, SecondNationId = fact.SubjectId });
        if (!Rules.Peace || !_nations.TryGetValue(fact.SubjectId, out var other))
            return;
        var relation = Relation(nation.Value.Id, other.Value.Id);
        if (relation.Status != DiplomaticStatus.War)
            return;
        // 已收到的战报本身足以作为依据，不应再要求额外的新聚落接触。
        ChangeAutonomousDiplomacy(nation, other, relation, fact, DiplomaticStatus.Neutral, "收到撤军战报，停止作战并进入恢复期");
    }
}
