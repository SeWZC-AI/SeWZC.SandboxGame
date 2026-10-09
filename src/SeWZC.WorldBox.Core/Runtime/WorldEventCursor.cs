namespace SeWZC.WorldBox.Core.Runtime;

/// <summary>WorldEvent 的引擎内定位引用；每次写入提交新的不可变状态。</summary>
internal sealed class WorldEventCursor : StateCursor<WorldEvent>
{
    public WorldEventCursor(WorldEvent value) : base(value) { }

    public int EvidenceFactId
    {
        get => Value.EvidenceFactId;
    }

    public SnapshotListCursor<int> AdditionalCauseEventIds
    {
        get => field ??= new SnapshotListCursor<int>(Value.AdditionalCauseEventIds, value =>
        {
            if (!ReferenceEquals(Value.AdditionalCauseEventIds, value))
                ReplaceChanged(Value with { AdditionalCauseEventIds = value });
        });
    }

    public int Id
    {
        get => Value.Id;
    }

    public EventImportance Importance
    {
        get => Value.Importance;
        set
        {
            if (!EqualityComparer<EventImportance>.Default.Equals(Value.Importance, value))
                ReplaceChanged(Value with { Importance = value });
        }
    }

    public int NationId
    {
        get => Value.NationId;
        set
        {
            if (!EqualityComparer<int>.Default.Equals(Value.NationId, value))
                ReplaceChanged(Value with { NationId = value });
        }
    }

    public int SecondNationId
    {
        get => Value.SecondNationId;
        set
        {
            if (!EqualityComparer<int>.Default.Equals(Value.SecondNationId, value))
                ReplaceChanged(Value with { SecondNationId = value });
        }
    }

    public int CauseEventId
    {
        get => Value.CauseEventId;
    }

    public static implicit operator WorldEvent(WorldEventCursor cursor)
    {
        return cursor.Value;
    }

    public static implicit operator WorldEventCursor(WorldEvent value)
    {
        return new WorldEventCursor(value);
    }
}
