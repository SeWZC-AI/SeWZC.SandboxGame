namespace SeWZC.WorldBox.Core.Runtime;

/// <summary>SettlementResearch 的引擎内定位引用；每次写入提交新的不可变状态。</summary>
internal sealed class SettlementResearchCursor : StateCursor<SettlementResearch>
{
    public SettlementResearchCursor(SettlementResearch value) : base(value) { }

    public ProjectObservation Observation
    {
        get => Value.Observation;
        set
        {
            if (!EqualityComparer<ProjectObservation>.Default.Equals(Value.Observation, value))
                ReplaceChanged(Value with { Observation = value });
        }
    }

    public int LastCompletionEventId
    {
        get => Value.LastCompletionEventId;
        set
        {
            if (!EqualityComparer<int>.Default.Equals(Value.LastCompletionEventId, value))
                ReplaceChanged(Value with { LastCompletionEventId = value });
        }
    }

    public int SettlementId
    {
        get => Value.SettlementId;
    }

    public Advancement? ActiveProject
    {
        get => Value.ActiveProject;
    }

    public double Progress
    {
        get => Value.Progress;
        set
        {
            if (!EqualityComparer<double>.Default.Equals(Value.Progress, value))
                ReplaceChanged(Value with { Progress = value });
        }
    }

    public double RequiredProgress
    {
        get => Value.RequiredProgress;
    }

    public SnapshotListCursor<Advancement> Completed
    {
        get => field ??= new SnapshotListCursor<Advancement>(Value.Completed, value =>
        {
            if (!ReferenceEquals(Value.Completed, value))
                ReplaceChanged(Value with { Completed = value });
        });
    }

    public static implicit operator SettlementResearch(SettlementResearchCursor cursor)
    {
        return cursor.Value;
    }

    public static implicit operator SettlementResearchCursor(SettlementResearch value)
    {
        return new SettlementResearchCursor(value);
    }
}
