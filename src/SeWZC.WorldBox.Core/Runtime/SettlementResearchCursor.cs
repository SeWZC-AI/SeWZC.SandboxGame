using System.Collections.Immutable;

namespace SeWZC.WorldBox.Core.Runtime;

/// <summary>SettlementResearch 的引擎内定位引用；每次写入提交新的不可变状态。</summary>
internal sealed partial class SettlementResearchCursor : StateCursor<global::SeWZC.WorldBox.Core.SettlementResearch>
{
    public SettlementResearchCursor() : this(new()) { }
    public SettlementResearchCursor(global::SeWZC.WorldBox.Core.SettlementResearch value) : base(value) { }
    public static implicit operator global::SeWZC.WorldBox.Core.SettlementResearch(SettlementResearchCursor cursor) => cursor.Value;
    public static implicit operator SettlementResearchCursor(global::SeWZC.WorldBox.Core.SettlementResearch value) => new(value);
    public ProjectObservation Observation { get => Value.Observation; set { if (!EqualityComparer<ProjectObservation>.Default.Equals(Value.Observation, value)) Replace(Value with { Observation = value }); } }
    public int LastCompletionEventId { get => Value.LastCompletionEventId; set { if (!EqualityComparer<int>.Default.Equals(Value.LastCompletionEventId, value)) Replace(Value with { LastCompletionEventId = value }); } }
    public int SettlementId { get => Value.SettlementId; set { if (!EqualityComparer<int>.Default.Equals(Value.SettlementId, value)) Replace(Value with { SettlementId = value }); } }
    public Advancement? ActiveProject { get => Value.ActiveProject; set { if (!EqualityComparer<Advancement?>.Default.Equals(Value.ActiveProject, value)) Replace(Value with { ActiveProject = value }); } }
    public double Progress { get => Value.Progress; set { if (!EqualityComparer<double>.Default.Equals(Value.Progress, value)) Replace(Value with { Progress = value }); } }
    public double RequiredProgress { get => Value.RequiredProgress; set { if (!EqualityComparer<double>.Default.Equals(Value.RequiredProgress, value)) Replace(Value with { RequiredProgress = value }); } }
    private SnapshotListCursor<Advancement>? _Completed;
    public SnapshotListCursor<Advancement> Completed
    {
        get => _Completed ??= new(Value.Completed, value => Replace(Value with { Completed = value }));
        set { _Completed = null; Replace(Value with { Completed = value.Snapshot }); }
    }
}
