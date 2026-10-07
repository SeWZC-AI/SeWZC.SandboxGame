using System.Collections.Immutable;

namespace SeWZC.WorldBox.Core.Runtime;

/// <summary>LocalConflict 的引擎内定位引用；每次写入提交新的不可变状态。</summary>
internal sealed partial class LocalConflictCursor : StateCursor<global::SeWZC.WorldBox.Core.LocalConflict>
{
    public LocalConflictCursor() : this(new()) { }
    public LocalConflictCursor(global::SeWZC.WorldBox.Core.LocalConflict value) : base(value) { }
    public static implicit operator global::SeWZC.WorldBox.Core.LocalConflict(LocalConflictCursor cursor) => cursor.Value;
    public static implicit operator LocalConflictCursor(global::SeWZC.WorldBox.Core.LocalConflict value) => new(value);
    public int Id { get => Value.Id; set { if (!EqualityComparer<int>.Default.Equals(Value.Id, value)) Replace(Value with { Id = value }); } }
    public int FirstResidentId { get => Value.FirstResidentId; set { if (!EqualityComparer<int>.Default.Equals(Value.FirstResidentId, value)) Replace(Value with { FirstResidentId = value }); } }
    public int SecondResidentId { get => Value.SecondResidentId; set { if (!EqualityComparer<int>.Default.Equals(Value.SecondResidentId, value)) Replace(Value with { SecondResidentId = value }); } }
    public int SettlementId { get => Value.SettlementId; set { if (!EqualityComparer<int>.Default.Equals(Value.SettlementId, value)) Replace(Value with { SettlementId = value }); } }
    public int X { get => Value.X; set { if (!EqualityComparer<int>.Default.Equals(Value.X, value)) Replace(Value with { X = value }); } }
    public int Y { get => Value.Y; set { if (!EqualityComparer<int>.Default.Equals(Value.Y, value)) Replace(Value with { Y = value }); } }
    public ConflictScope Scope { get => Value.Scope; set { if (!EqualityComparer<ConflictScope>.Default.Equals(Value.Scope, value)) Replace(Value with { Scope = value }); } }
    public ConflictStage Stage { get => Value.Stage; set { if (!EqualityComparer<ConflictStage>.Default.Equals(Value.Stage, value)) Replace(Value with { Stage = value }); } }
    public double Tension { get => Value.Tension; set { if (!EqualityComparer<double>.Default.Equals(Value.Tension, value)) Replace(Value with { Tension = value }); } }
    public long StartedTick { get => Value.StartedTick; set { if (!EqualityComparer<long>.Default.Equals(Value.StartedTick, value)) Replace(Value with { StartedTick = value }); } }
    public long StageStartedTick { get => Value.StageStartedTick; set { if (!EqualityComparer<long>.Default.Equals(Value.StageStartedTick, value)) Replace(Value with { StageStartedTick = value }); } }
    public long LastChangedTick { get => Value.LastChangedTick; set { if (!EqualityComparer<long>.Default.Equals(Value.LastChangedTick, value)) Replace(Value with { LastChangedTick = value }); } }
    public int LastEventId { get => Value.LastEventId; set { if (!EqualityComparer<int>.Default.Equals(Value.LastEventId, value)) Replace(Value with { LastEventId = value }); } }
    private SnapshotListCursor<int>? _Participants;
    public SnapshotListCursor<int> Participants
    {
        get => _Participants ??= new(Value.Participants, value => Replace(Value with { Participants = value }));
        set { _Participants = null; Replace(Value with { Participants = value.Snapshot }); }
    }
}
