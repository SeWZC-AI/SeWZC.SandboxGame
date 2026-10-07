using System.Collections.Immutable;

namespace SeWZC.WorldBox.Core.Runtime;

/// <summary>WorldEvent 的引擎内定位引用；每次写入提交新的不可变状态。</summary>
internal sealed partial class WorldEventCursor : StateCursor<global::SeWZC.WorldBox.Core.WorldEvent>
{
    public WorldEventCursor() : this(new()) { }
    public WorldEventCursor(global::SeWZC.WorldBox.Core.WorldEvent value) : base(value) { }
    public static implicit operator global::SeWZC.WorldBox.Core.WorldEvent(WorldEventCursor cursor) => cursor.Value;
    public static implicit operator WorldEventCursor(global::SeWZC.WorldBox.Core.WorldEvent value) => new(value);
    public long Tick { get => Value.Tick; set { if (!EqualityComparer<long>.Default.Equals(Value.Tick, value)) Replace(Value with { Tick = value }); } }
    public WorldEventKind Kind { get => Value.Kind; set { if (!EqualityComparer<WorldEventKind>.Default.Equals(Value.Kind, value)) Replace(Value with { Kind = value }); } }
    public string Message { get => Value.Message; set { if (!EqualityComparer<string>.Default.Equals(Value.Message, value)) Replace(Value with { Message = value }); } }
    public int X { get => Value.X; set { if (!EqualityComparer<int>.Default.Equals(Value.X, value)) Replace(Value with { X = value }); } }
    public int Y { get => Value.Y; set { if (!EqualityComparer<int>.Default.Equals(Value.Y, value)) Replace(Value with { Y = value }); } }
    public EventAction Action { get => Value.Action; set { if (!EqualityComparer<EventAction>.Default.Equals(Value.Action, value)) Replace(Value with { Action = value }); } }
    public int SettlementId { get => Value.SettlementId; set { if (!EqualityComparer<int>.Default.Equals(Value.SettlementId, value)) Replace(Value with { SettlementId = value }); } }
    public int SecondSettlementId { get => Value.SecondSettlementId; set { if (!EqualityComparer<int>.Default.Equals(Value.SecondSettlementId, value)) Replace(Value with { SecondSettlementId = value }); } }
    public int EvidenceFactId { get => Value.EvidenceFactId; set { if (!EqualityComparer<int>.Default.Equals(Value.EvidenceFactId, value)) Replace(Value with { EvidenceFactId = value }); } }
    private SnapshotListCursor<int>? _AdditionalCauseEventIds;
    public SnapshotListCursor<int> AdditionalCauseEventIds
    {
        get => _AdditionalCauseEventIds ??= new(Value.AdditionalCauseEventIds, value => Replace(Value with { AdditionalCauseEventIds = value }));
        set { _AdditionalCauseEventIds = null; Replace(Value with { AdditionalCauseEventIds = value.Snapshot }); }
    }
    public int Id { get => Value.Id; set { if (!EqualityComparer<int>.Default.Equals(Value.Id, value)) Replace(Value with { Id = value }); } }
    public EventImportance Importance { get => Value.Importance; set { if (!EqualityComparer<EventImportance>.Default.Equals(Value.Importance, value)) Replace(Value with { Importance = value }); } }
    public int ResidentId { get => Value.ResidentId; set { if (!EqualityComparer<int>.Default.Equals(Value.ResidentId, value)) Replace(Value with { ResidentId = value }); } }
    public int NationId { get => Value.NationId; set { if (!EqualityComparer<int>.Default.Equals(Value.NationId, value)) Replace(Value with { NationId = value }); } }
    public int SecondNationId { get => Value.SecondNationId; set { if (!EqualityComparer<int>.Default.Equals(Value.SecondNationId, value)) Replace(Value with { SecondNationId = value }); } }
    public int CauseEventId { get => Value.CauseEventId; set { if (!EqualityComparer<int>.Default.Equals(Value.CauseEventId, value)) Replace(Value with { CauseEventId = value }); } }
}
