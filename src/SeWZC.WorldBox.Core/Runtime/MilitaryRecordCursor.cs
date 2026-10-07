using System.Collections.Immutable;

namespace SeWZC.WorldBox.Core.Runtime;

/// <summary>MilitaryRecord 的引擎内定位引用；每次写入提交新的不可变状态。</summary>
internal sealed partial class MilitaryRecordCursor : StateCursor<global::SeWZC.WorldBox.Core.MilitaryRecord>
{
    public MilitaryRecordCursor() : this(new()) { }
    public MilitaryRecordCursor(global::SeWZC.WorldBox.Core.MilitaryRecord value) : base(value) { }
    public static implicit operator global::SeWZC.WorldBox.Core.MilitaryRecord(MilitaryRecordCursor cursor) => cursor.Value;
    public static implicit operator MilitaryRecordCursor(global::SeWZC.WorldBox.Core.MilitaryRecord value) => new(value);
    public int CampaignEventId { get => Value.CampaignEventId; set { if (!EqualityComparer<int>.Default.Equals(Value.CampaignEventId, value)) Replace(Value with { CampaignEventId = value }); } }
    public int EnemyNationId { get => Value.EnemyNationId; set { if (!EqualityComparer<int>.Default.Equals(Value.EnemyNationId, value)) Replace(Value with { EnemyNationId = value }); } }
    public WarObjective Objective { get => Value.Objective; set { if (!EqualityComparer<WarObjective>.Default.Equals(Value.Objective, value)) Replace(Value with { Objective = value }); } }
    public int TargetSettlementId { get => Value.TargetSettlementId; set { if (!EqualityComparer<int>.Default.Equals(Value.TargetSettlementId, value)) Replace(Value with { TargetSettlementId = value }); } }
    public int TargetX { get => Value.TargetX; set { if (!EqualityComparer<int>.Default.Equals(Value.TargetX, value)) Replace(Value with { TargetX = value }); } }
    public int TargetY { get => Value.TargetY; set { if (!EqualityComparer<int>.Default.Equals(Value.TargetY, value)) Replace(Value with { TargetY = value }); } }
    public long StartedTick { get => Value.StartedTick; set { if (!EqualityComparer<long>.Default.Equals(Value.StartedTick, value)) Replace(Value with { StartedTick = value }); } }
    public int LastMobilizedOrderId { get => Value.LastMobilizedOrderId; set { if (!EqualityComparer<int>.Default.Equals(Value.LastMobilizedOrderId, value)) Replace(Value with { LastMobilizedOrderId = value }); } }
    public long RecoveryUntilTick { get => Value.RecoveryUntilTick; set { if (!EqualityComparer<long>.Default.Equals(Value.RecoveryUntilTick, value)) Replace(Value with { RecoveryUntilTick = value }); } }
    public int LastReportEventId { get => Value.LastReportEventId; set { if (!EqualityComparer<int>.Default.Equals(Value.LastReportEventId, value)) Replace(Value with { LastReportEventId = value }); } }
    public long LastReportObservedTick { get => Value.LastReportObservedTick; set { if (!EqualityComparer<long>.Default.Equals(Value.LastReportObservedTick, value)) Replace(Value with { LastReportObservedTick = value }); } }
    public long LastReportReceivedTick { get => Value.LastReportReceivedTick; set { if (!EqualityComparer<long>.Default.Equals(Value.LastReportReceivedTick, value)) Replace(Value with { LastReportReceivedTick = value }); } }
    public WarOutcome ReportedOutcome { get => Value.ReportedOutcome; set { if (!EqualityComparer<WarOutcome>.Default.Equals(Value.ReportedOutcome, value)) Replace(Value with { ReportedOutcome = value }); } }
    public string Report { get => Value.Report; set { if (!EqualityComparer<string>.Default.Equals(Value.Report, value)) Replace(Value with { Report = value }); } }
}
