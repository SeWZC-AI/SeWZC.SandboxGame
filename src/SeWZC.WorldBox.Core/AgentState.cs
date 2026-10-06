using System.Text.Json.Serialization;

namespace SeWZC.WorldBox.Core;

/// <summary>居民的需求、性格、记忆，以及当前行动或递送任务。</summary>
public sealed class AgentState
{
    /// <summary>是否已经初始化需求、性格和初始认知。</summary>
    public bool Initialized { get; set; }

    /// <summary>疲劳程度，越高表示越需要休息。</summary>
    public double Fatigue { get; set; }

    /// <summary>社交需求程度，越高表示越需要交流。</summary>
    public double SocialNeed { get; set; }

    /// <summary>当前用于行动评分的性格权重。</summary>
    public PersonalityProfile Personality { get; set; } = new();

    /// <summary>当前行动目标及其导航进度。</summary>
    public AgentGoal Goal { get; set; } = new();

    /// <summary>居民自己观察或收到的信息，可能已经过时。</summary>
    public List<AgentFact> Memory { get; set; } = [];

    /// <summary>近期目标选择的评分、理由和依据记录。</summary>
    public List<AgentDecision> Decisions { get; set; } = [];

    /// <summary>下一次自主评估目标的模拟日序。</summary>
    public long NextThinkTick { get; set; }

    /// <summary>最近一次与其他居民交谈的模拟日序。</summary>
    public long LastConversationTick { get; set; }

    /// <summary>当前贸易或递送任务的目的聚落 ID。</summary>
    public int DestinationSettlementId { get; set; }

    /// <summary>居民正在实际携带的信息副本。</summary>
    public List<AgentFact> CarriedMessages { get; set; } = [];

    /// <summary>当前贸易或递送任务的出发聚落 ID。</summary>
    public int MissionOriginSettlementId { get; set; }

    /// <summary>当前任务开始的模拟日序。</summary>
    public long MissionStartedTick { get; set; }

    /// <summary>当前任务受阻后允许再次尝试的模拟日序。</summary>
    public long MissionRetryTick { get; set; }

    /// <summary>当前探索朝向，按八个方向编号。</summary>
    [JsonRequired]
    public int ExplorationHeading { get; set; }

    /// <summary>当前优先补充的材料种类，空值表示无指定优先材料。</summary>
    public ResourceKind? MaterialPriority { get; set; }

    /// <summary>最近一次职业分工变化的模拟日序。</summary>
    public long JobChangedTick { get; set; } = -120;
}
