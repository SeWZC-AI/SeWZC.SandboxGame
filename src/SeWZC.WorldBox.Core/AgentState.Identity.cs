using System.Collections.Immutable;

namespace SeWZC.WorldBox.Core;

public sealed partial record AgentState
{
    // 任务身份与历史随日常疲劳、需求和目标转换共享，只有相应字段变化才复制。
    private readonly Identity _identity = Identity.Default;

    private sealed record Identity
    {
        internal static readonly Identity Default = new();

        public bool Initialized { get; init; }
        public ImmutableList<AgentDecision> Decisions { get; init; } = [];
        public long LastConversationTick { get; init; }
        public int DestinationSettlementId { get; init; }
        public ImmutableList<AgentFact> CarriedMessages { get; init; } = [];
        public int MissionOriginSettlementId { get; init; }
        public long MissionStartedTick { get; init; }
        public long MissionRetryTick { get; init; }
        public int ExplorationHeading { get; init; }
        public ResourceKind? MaterialPriority { get; init; }
        public long JobChangedTick { get; init; } = -120;
        public int WorkplaceId { get; init; }
        public int WorkAreaIndex { get; init; } = -1;
    }
}
