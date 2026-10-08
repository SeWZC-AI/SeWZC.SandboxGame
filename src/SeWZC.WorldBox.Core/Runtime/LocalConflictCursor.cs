namespace SeWZC.WorldBox.Core.Runtime;

/// <summary>LocalConflict 的引擎内定位引用；每次写入提交新的不可变状态。</summary>
internal sealed class LocalConflictCursor : StateCursor<LocalConflict>
{
    public LocalConflictCursor() : this(new LocalConflict()) { }
    public LocalConflictCursor(LocalConflict value) : base(value) { }

    public int Id
    {
        get => Value.Id;
        set
        {
            if (!EqualityComparer<int>.Default.Equals(Value.Id, value))
                ReplaceChanged(Value with { Id = value });
        }
    }

    public int FirstResidentId
    {
        get => Value.FirstResidentId;
        set
        {
            if (!EqualityComparer<int>.Default.Equals(Value.FirstResidentId, value))
                ReplaceChanged(Value with { FirstResidentId = value });
        }
    }

    public int SecondResidentId
    {
        get => Value.SecondResidentId;
        set
        {
            if (!EqualityComparer<int>.Default.Equals(Value.SecondResidentId, value))
                ReplaceChanged(Value with { SecondResidentId = value });
        }
    }

    public int SettlementId
    {
        get => Value.SettlementId;
        set
        {
            if (!EqualityComparer<int>.Default.Equals(Value.SettlementId, value))
                ReplaceChanged(Value with { SettlementId = value });
        }
    }

    public int X
    {
        get => Value.X;
        set
        {
            if (!EqualityComparer<int>.Default.Equals(Value.X, value))
                ReplaceChanged(Value with { X = value });
        }
    }

    public int Y
    {
        get => Value.Y;
        set
        {
            if (!EqualityComparer<int>.Default.Equals(Value.Y, value))
                ReplaceChanged(Value with { Y = value });
        }
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
        set
        {
            if (!EqualityComparer<ConflictStage>.Default.Equals(Value.Stage, value))
                ReplaceChanged(Value with { Stage = value });
        }
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
        set
        {
            if (!EqualityComparer<long>.Default.Equals(Value.StartedTick, value))
                ReplaceChanged(Value with { StartedTick = value });
        }
    }

    public long StageStartedTick
    {
        get => Value.StageStartedTick;
        set
        {
            if (!EqualityComparer<long>.Default.Equals(Value.StageStartedTick, value))
                ReplaceChanged(Value with { StageStartedTick = value });
        }
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
        set
        {
            field = null;
            Replace(Value with { Participants = value.Snapshot });
        }
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
