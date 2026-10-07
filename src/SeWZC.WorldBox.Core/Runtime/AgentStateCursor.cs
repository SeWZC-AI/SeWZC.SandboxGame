using System.Collections.Immutable;

namespace SeWZC.WorldBox.Core.Runtime;

/// <summary>AgentState 的引擎内定位引用；每次写入提交新的不可变状态。</summary>
internal sealed partial class AgentStateCursor : StateCursor<global::SeWZC.WorldBox.Core.AgentState>
{
    public AgentStateCursor() : this(new()) { }
    public AgentStateCursor(global::SeWZC.WorldBox.Core.AgentState value) : base(value) { }
    public static implicit operator global::SeWZC.WorldBox.Core.AgentState(AgentStateCursor cursor) => cursor.Value;
    public static implicit operator AgentStateCursor(global::SeWZC.WorldBox.Core.AgentState value) => new(value);
    public bool Initialized { get => Value.Initialized; set { if (!EqualityComparer<bool>.Default.Equals(Value.Initialized, value)) ReplaceChanged(Value with { Initialized = value }); } }
    public double Fatigue { get => Value.Fatigue; set { if (!EqualityComparer<double>.Default.Equals(Value.Fatigue, value)) ReplaceChanged(Value with { Fatigue = value }); } }
    public double SocialNeed { get => Value.SocialNeed; set { if (!EqualityComparer<double>.Default.Equals(Value.SocialNeed, value)) ReplaceChanged(Value with { SocialNeed = value }); } }
    public PersonalityProfile Personality { get => Value.Personality; set { if (!EqualityComparer<PersonalityProfile>.Default.Equals(Value.Personality, value)) ReplaceChanged(Value with { Personality = value }); } }
    public AgentGoal Goal { get => Value.Goal; set { if (!ReferenceEquals(Value.Goal, value) && !Value.Goal.Equals(value)) ReplaceChanged(Value with { Goal = value }); } }
    private SnapshotArrayCursor<AgentFact>? _Memory;
    public SnapshotArrayCursor<AgentFact> Memory
    {
        get => _Memory ??= new(Value.Memory, value => { if (!Value.Memory.Equals(value)) ReplaceChanged(Value with { Memory = value }); });
        set { _Memory = null; Replace(Value with { Memory = value.Snapshot }); }
    }
    private SnapshotListCursor<AgentDecision>? _Decisions;
    public SnapshotListCursor<AgentDecision> Decisions
    {
        get => _Decisions ??= new(Value.Decisions, value => { if (!ReferenceEquals(Value.Decisions, value)) ReplaceChanged(Value with { Decisions = value }); });
        set { _Decisions = null; Replace(Value with { Decisions = value.Snapshot }); }
    }
    public long NextThinkTick { get => Value.NextThinkTick; set { if (!EqualityComparer<long>.Default.Equals(Value.NextThinkTick, value)) ReplaceChanged(Value with { NextThinkTick = value }); } }
    public long LastConversationTick { get => Value.LastConversationTick; set { if (!EqualityComparer<long>.Default.Equals(Value.LastConversationTick, value)) ReplaceChanged(Value with { LastConversationTick = value }); } }
    public int DestinationSettlementId { get => Value.DestinationSettlementId; set { if (!EqualityComparer<int>.Default.Equals(Value.DestinationSettlementId, value)) ReplaceChanged(Value with { DestinationSettlementId = value }); } }
    private SnapshotListCursor<AgentFact>? _CarriedMessages;
    public SnapshotListCursor<AgentFact> CarriedMessages
    {
        get => _CarriedMessages ??= new(Value.CarriedMessages, value => { if (!ReferenceEquals(Value.CarriedMessages, value)) ReplaceChanged(Value with { CarriedMessages = value }); });
        set { _CarriedMessages = null; Replace(Value with { CarriedMessages = value.Snapshot }); }
    }
    public int MissionOriginSettlementId { get => Value.MissionOriginSettlementId; set { if (!EqualityComparer<int>.Default.Equals(Value.MissionOriginSettlementId, value)) ReplaceChanged(Value with { MissionOriginSettlementId = value }); } }
    public long MissionStartedTick { get => Value.MissionStartedTick; set { if (!EqualityComparer<long>.Default.Equals(Value.MissionStartedTick, value)) ReplaceChanged(Value with { MissionStartedTick = value }); } }
    public long MissionRetryTick { get => Value.MissionRetryTick; set { if (!EqualityComparer<long>.Default.Equals(Value.MissionRetryTick, value)) ReplaceChanged(Value with { MissionRetryTick = value }); } }
    public int ExplorationHeading { get => Value.ExplorationHeading; set { if (!EqualityComparer<int>.Default.Equals(Value.ExplorationHeading, value)) ReplaceChanged(Value with { ExplorationHeading = value }); } }
    public ResourceKind? MaterialPriority { get => Value.MaterialPriority; set { if (!EqualityComparer<ResourceKind?>.Default.Equals(Value.MaterialPriority, value)) ReplaceChanged(Value with { MaterialPriority = value }); } }
    public long JobChangedTick { get => Value.JobChangedTick; set { if (!EqualityComparer<long>.Default.Equals(Value.JobChangedTick, value)) ReplaceChanged(Value with { JobChangedTick = value }); } }

    protected override void OnReplace(in global::SeWZC.WorldBox.Core.AgentState before, in global::SeWZC.WorldBox.Core.AgentState after)
    {
        // 自身集合操作先更新 Snapshot 再发布；整体认知转换时才重新绑定定位引用。
        if (_Memory is not null && !_Memory.Snapshot.Equals(after.Memory)) _Memory = null;
        if (_Decisions is not null && !ReferenceEquals(_Decisions.Snapshot, after.Decisions)) _Decisions = null;
        if (_CarriedMessages is not null && !ReferenceEquals(_CarriedMessages.Snapshot, after.CarriedMessages)) _CarriedMessages = null;
    }
}
