namespace SeWZC.WorldBox.Core.Runtime;

/// <summary>AgentState 的引擎内定位引用；每次写入提交新的不可变状态。</summary>
internal sealed class AgentStateCursor(AgentState value) : StateCursor<AgentState>(value)
{
    private SnapshotListCursor<AgentFact>? _carriedMessages;
    private SnapshotListCursor<AgentDecision>? _decisions;
    private SnapshotArrayCursor<AgentFact>? _memory;

    public bool Initialized
    {
        get => Value.Initialized;
    }

    public double Fatigue
    {
        get => Value.Fatigue;
        set
        {
            if (!EqualityComparer<double>.Default.Equals(Value.Fatigue, value))
                ReplaceChanged(Value with { Fatigue = value });
        }
    }

    public double SocialNeed
    {
        get => Value.SocialNeed;
        set
        {
            if (!EqualityComparer<double>.Default.Equals(Value.SocialNeed, value))
                ReplaceChanged(Value with { SocialNeed = value });
        }
    }

    public PersonalityProfile Personality
    {
        get => Value.Personality;
        set
        {
            if (!EqualityComparer<PersonalityProfile>.Default.Equals(Value.Personality, value))
                ReplaceChanged(Value with { Personality = value });
        }
    }

    public AgentGoal Goal
    {
        get => Value.Goal;
        set
        {
            if (!ReferenceEquals(Value.Goal, value) && !Value.Goal.Equals(value))
                ReplaceChanged(Value with { Goal = value });
        }
    }

    public SnapshotArrayCursor<AgentFact> Memory
    {
        get => _memory ??= new SnapshotArrayCursor<AgentFact>(Value.Memory, value =>
        {
            if (!Value.Memory.Equals(value))
                ReplaceChanged(Value with { Memory = value });
        });
        set
        {
            _memory = null;
            Replace(Value with { Memory = value.Snapshot });
        }
    }

    public SnapshotListCursor<AgentDecision> Decisions
    {
        get => _decisions ??= new SnapshotListCursor<AgentDecision>(Value.Decisions, value =>
        {
            if (!ReferenceEquals(Value.Decisions, value))
                ReplaceChanged(Value with { Decisions = value });
        });
        set
        {
            _decisions = null;
            Replace(Value with { Decisions = value.Snapshot });
        }
    }

    public long NextThinkTick
    {
        get => Value.NextThinkTick;
        set
        {
            if (!EqualityComparer<long>.Default.Equals(Value.NextThinkTick, value))
                ReplaceChanged(Value with { NextThinkTick = value });
        }
    }

    public long LastConversationTick
    {
        get => Value.LastConversationTick;
    }

    public int DestinationSettlementId
    {
        get => Value.DestinationSettlementId;
        set
        {
            if (!EqualityComparer<int>.Default.Equals(Value.DestinationSettlementId, value))
                ReplaceChanged(Value with { DestinationSettlementId = value });
        }
    }

    public SnapshotListCursor<AgentFact> CarriedMessages
    {
        get => _carriedMessages ??= new SnapshotListCursor<AgentFact>(Value.CarriedMessages, value =>
        {
            if (!ReferenceEquals(Value.CarriedMessages, value))
                ReplaceChanged(Value with { CarriedMessages = value });
        });
        set
        {
            _carriedMessages = null;
            Replace(Value with { CarriedMessages = value.Snapshot });
        }
    }

    public int MissionOriginSettlementId
    {
        get => Value.MissionOriginSettlementId;
    }

    public long MissionStartedTick
    {
        get => Value.MissionStartedTick;
    }

    public long MissionRetryTick
    {
        get => Value.MissionRetryTick;
        set
        {
            if (!EqualityComparer<long>.Default.Equals(Value.MissionRetryTick, value))
                ReplaceChanged(Value with { MissionRetryTick = value });
        }
    }

    public int ExplorationHeading
    {
        get => Value.ExplorationHeading;
        set
        {
            if (!EqualityComparer<int>.Default.Equals(Value.ExplorationHeading, value))
                ReplaceChanged(Value with { ExplorationHeading = value });
        }
    }

    public ResourceKind? MaterialPriority
    {
        get => Value.MaterialPriority;
        set
        {
            if (!EqualityComparer<ResourceKind?>.Default.Equals(Value.MaterialPriority, value))
                ReplaceChanged(Value with { MaterialPriority = value });
        }
    }

    public long JobChangedTick
    {
        get => Value.JobChangedTick;
        set
        {
            if (!EqualityComparer<long>.Default.Equals(Value.JobChangedTick, value))
                ReplaceChanged(Value with { JobChangedTick = value });
        }
    }

    public int WorkplaceId
    {
        get => Value.WorkplaceId;
        set
        {
            if (Value.WorkplaceId != value)
                ReplaceChanged(Value with { WorkplaceId = value });
        }
    }

    public int WorkAreaIndex
    {
        get => Value.WorkAreaIndex;
        set
        {
            if (Value.WorkAreaIndex != value)
                ReplaceChanged(Value with { WorkAreaIndex = value });
        }
    }

    public static implicit operator AgentState(AgentStateCursor cursor)
    {
        return cursor.Value;
    }

    public static implicit operator AgentStateCursor(AgentState value)
    {
        return new AgentStateCursor(value);
    }

    protected override void OnReplace(in AgentState before, in AgentState after)
    {
        // 自身集合操作先更新 Snapshot 再发布；整体认知转换时才重新绑定定位引用。
        if (_memory is not null && !_memory.Snapshot.Equals(after.Memory))
            _memory = null;
        if (_decisions is not null && !ReferenceEquals(_decisions.Snapshot, after.Decisions))
            _decisions = null;
        if (_carriedMessages is not null && !ReferenceEquals(_carriedMessages.Snapshot, after.CarriedMessages))
            _carriedMessages = null;
    }
}
