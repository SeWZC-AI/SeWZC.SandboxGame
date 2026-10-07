using System.Collections.Immutable;

namespace SeWZC.WorldBox.Core.Runtime;

/// <summary>AgentState 的引擎内定位引用；每次写入提交新的不可变状态。</summary>
internal sealed partial class AgentStateCursor : StateCursor<global::SeWZC.WorldBox.Core.AgentState>
{
    public AgentStateCursor() : this(new()) { }
    public AgentStateCursor(global::SeWZC.WorldBox.Core.AgentState value) : base(value) { }
    public static implicit operator global::SeWZC.WorldBox.Core.AgentState(AgentStateCursor cursor) => cursor.Value;
    public static implicit operator AgentStateCursor(global::SeWZC.WorldBox.Core.AgentState value) => new(value);
    public bool Initialized { get => Value.Initialized; set { if (!EqualityComparer<bool>.Default.Equals(Value.Initialized, value)) Replace(Value with { Initialized = value }); } }
    public double Fatigue { get => Value.Fatigue; set { if (!EqualityComparer<double>.Default.Equals(Value.Fatigue, value)) Replace(Value with { Fatigue = value }); } }
    public double SocialNeed { get => Value.SocialNeed; set { if (!EqualityComparer<double>.Default.Equals(Value.SocialNeed, value)) Replace(Value with { SocialNeed = value }); } }
    public PersonalityProfile Personality { get => Value.Personality; set { if (!EqualityComparer<PersonalityProfile>.Default.Equals(Value.Personality, value)) Replace(Value with { Personality = value }); } }
    public AgentGoal Goal { get => Value.Goal; set { if (!EqualityComparer<AgentGoal>.Default.Equals(Value.Goal, value)) Replace(Value with { Goal = value }); } }
    private SnapshotListCursor<AgentFact>? _Memory;
    public SnapshotListCursor<AgentFact> Memory
    {
        get => _Memory ??= new(Value.Memory, value => Replace(Value with { Memory = value }));
        set { _Memory = null; Replace(Value with { Memory = value.Snapshot }); }
    }
    private SnapshotListCursor<AgentDecision>? _Decisions;
    public SnapshotListCursor<AgentDecision> Decisions
    {
        get => _Decisions ??= new(Value.Decisions, value => Replace(Value with { Decisions = value }));
        set { _Decisions = null; Replace(Value with { Decisions = value.Snapshot }); }
    }
    public long NextThinkTick { get => Value.NextThinkTick; set { if (!EqualityComparer<long>.Default.Equals(Value.NextThinkTick, value)) Replace(Value with { NextThinkTick = value }); } }
    public long LastConversationTick { get => Value.LastConversationTick; set { if (!EqualityComparer<long>.Default.Equals(Value.LastConversationTick, value)) Replace(Value with { LastConversationTick = value }); } }
    public int DestinationSettlementId { get => Value.DestinationSettlementId; set { if (!EqualityComparer<int>.Default.Equals(Value.DestinationSettlementId, value)) Replace(Value with { DestinationSettlementId = value }); } }
    private SnapshotListCursor<AgentFact>? _CarriedMessages;
    public SnapshotListCursor<AgentFact> CarriedMessages
    {
        get => _CarriedMessages ??= new(Value.CarriedMessages, value => Replace(Value with { CarriedMessages = value }));
        set { _CarriedMessages = null; Replace(Value with { CarriedMessages = value.Snapshot }); }
    }
    public int MissionOriginSettlementId { get => Value.MissionOriginSettlementId; set { if (!EqualityComparer<int>.Default.Equals(Value.MissionOriginSettlementId, value)) Replace(Value with { MissionOriginSettlementId = value }); } }
    public long MissionStartedTick { get => Value.MissionStartedTick; set { if (!EqualityComparer<long>.Default.Equals(Value.MissionStartedTick, value)) Replace(Value with { MissionStartedTick = value }); } }
    public long MissionRetryTick { get => Value.MissionRetryTick; set { if (!EqualityComparer<long>.Default.Equals(Value.MissionRetryTick, value)) Replace(Value with { MissionRetryTick = value }); } }
    public int ExplorationHeading { get => Value.ExplorationHeading; set { if (!EqualityComparer<int>.Default.Equals(Value.ExplorationHeading, value)) Replace(Value with { ExplorationHeading = value }); } }
    public ResourceKind? MaterialPriority { get => Value.MaterialPriority; set { if (!EqualityComparer<ResourceKind?>.Default.Equals(Value.MaterialPriority, value)) Replace(Value with { MaterialPriority = value }); } }
    public long JobChangedTick { get => Value.JobChangedTick; set { if (!EqualityComparer<long>.Default.Equals(Value.JobChangedTick, value)) Replace(Value with { JobChangedTick = value }); } }
}
