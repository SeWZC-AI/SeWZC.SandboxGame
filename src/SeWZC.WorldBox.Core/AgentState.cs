using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace SeWZC.WorldBox.Core;

/// <summary>居民的认知与自主行动状态。</summary>
public sealed partial record AgentState
{

    /// <summary>认知与行动状态是否已初始化。</summary>
    public bool Initialized { get; init; }

    /// <summary>疲劳程度，越高表示越需要休息。</summary>
    public double Fatigue { get; init; }

    /// <summary>社交需求程度，越高表示越需要交流。</summary>
    public double SocialNeed { get; init; }

    /// <summary>当前用于行动评分的性格权重。</summary>
    public PersonalityProfile Personality { get; init; } = new();

    /// <summary>当前正在执行的行动目标。</summary>
    public AgentGoal Goal { get; init; } = new();

    /// <summary>居民自己观察或收到的信息，可能已经过时。</summary>
    public ImmutableList<AgentFact> Memory { get; init; } = [];

    /// <summary>近期行动决策的记录。</summary>
    public ImmutableList<AgentDecision> Decisions { get; init; } = [];

    /// <summary>下一次自主评估目标的模拟日序。</summary>
    public long NextThinkTick { get; init; }

    /// <summary>最近一次与其他居民交谈的模拟日序。</summary>
    public long LastConversationTick { get; init; }

    /// <summary>当前贸易或递送任务的目的聚落 ID。</summary>
    public int DestinationSettlementId { get; init; }

    /// <summary>居民正在实际携带的信息副本。</summary>
    public ImmutableList<AgentFact> CarriedMessages { get; init; } = [];

    /// <summary>当前贸易或递送任务的出发聚落 ID。</summary>
    public int MissionOriginSettlementId { get; init; }

    /// <summary>当前任务开始的模拟日序。</summary>
    public long MissionStartedTick { get; init; }

    /// <summary>当前任务受阻后允许再次尝试的模拟日序。</summary>
    public long MissionRetryTick { get; init; }

    /// <summary>当前探索朝向，按八个方向编号。</summary>
    [JsonRequired]
    public int ExplorationHeading { get; init; }

    /// <summary>当前优先补充的材料种类，空值表示无指定优先材料。</summary>
    public ResourceKind? MaterialPriority { get; init; }

    /// <summary>最近一次职业分工变化的模拟日序。</summary>
    public long JobChangedTick { get; init; } = -120;
}
