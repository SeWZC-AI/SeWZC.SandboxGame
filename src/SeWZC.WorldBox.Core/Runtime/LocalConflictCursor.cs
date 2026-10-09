namespace SeWZC.WorldBox.Core.Runtime;

/// <summary>LocalConflict 的引擎内定位引用；每次写入提交新的不可变状态。</summary>
internal sealed class LocalConflictCursor : StateCursor<LocalConflict>
{
    public LocalConflictCursor(LocalConflict value) : base(value) { }

    public int FirstResidentId
    {
        get => Value.FirstResidentId;
    }

    public int SecondResidentId
    {
        get => Value.SecondResidentId;
    }

    public int SettlementId
    {
        get => Value.SettlementId;
    }

    public int X
    {
        get => Value.X;
    }

    public int Y
    {
        get => Value.Y;
    }

    public ConflictScope Scope
    {
        get => Value.Scope;
        set
        {
            if (!EqualityComparer<ConflictScope>.Default.Equals(Value.Scope, value))
                ReplaceChanged(Value with { Scope = value });
        }
    }

    public ConflictStage Stage
    {
        get => Value.Stage;
    }

    public double Tension
    {
        get => Value.Tension;
        set
        {
            if (!EqualityComparer<double>.Default.Equals(Value.Tension, value))
                ReplaceChanged(Value with { Tension = value });
        }
    }

    public long StartedTick
    {
        get => Value.StartedTick;
    }

    public long StageStartedTick
    {
        get => Value.StageStartedTick;
    }

    public long LastChangedTick
    {
        get => Value.LastChangedTick;
        set
        {
            if (!EqualityComparer<long>.Default.Equals(Value.LastChangedTick, value))
                ReplaceChanged(Value with { LastChangedTick = value });
        }
    }

    public int LastEventId
    {
        get => Value.LastEventId;
        set
        {
            if (!EqualityComparer<int>.Default.Equals(Value.LastEventId, value))
                ReplaceChanged(Value with { LastEventId = value });
        }
    }

    public SnapshotListCursor<int> Participants
    {
        get => field ??= new SnapshotListCursor<int>(Value.Participants, value =>
        {
            if (!ReferenceEquals(Value.Participants, value))
                ReplaceChanged(Value with { Participants = value });
        });
    }

    public static implicit operator LocalConflict(LocalConflictCursor cursor)
    {
        return cursor.Value;
    }

    public static implicit operator LocalConflictCursor(LocalConflict value)
    {
        return new LocalConflictCursor(value);
    }
}
