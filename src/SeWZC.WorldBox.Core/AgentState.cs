using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace SeWZC.WorldBox.Core;

/// <summary>居民的认知与自主行动状态。</summary>
public sealed partial record AgentState
{
    /// <summary>个人熟路记忆最多保留的不同地格数。</summary>
    public const int MaximumFamiliarTiles = 48;

    /// <summary>认知与行动状态是否已初始化。</summary>
    public bool Initialized
    {
        get => _identity.Initialized;
        init
        {
            if (!(_identity.Initialized == value))
                _identity = _identity with { Initialized = value };
        }
    }

    /// <summary>已消耗体力的百分比，越高表示越需要休息。</summary>
    public double Fatigue { get; init; }

    /// <summary>剩余睡眠储备的百分比，清醒和睡眠期间均持续消耗。</summary>
    [JsonRequired]
    public double Sleep { get; init; } = ResidentNeedsRules.MaximumPercent;

    /// <summary>社交需求程度，越高表示越需要交流。</summary>
    public double SocialNeed { get; init; }

    /// <summary>当前用于行动评分的性格权重。</summary>
    public PersonalityProfile Personality
    {
        get => _identity.Personality;
        init
        {
            if (!ReferenceEquals(_identity.Personality, value))
                _identity = _identity with { Personality = value };
        }
    }

    /// <summary>当前正在执行的行动目标。</summary>
    public AgentGoal Goal { get; init; } = new();

    /// <summary>夜间返家时暂存的白天目标，晨起后恢复；途中任务不切换目标。</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public AgentGoal? DaytimeGoal { get; init; }

    /// <summary>当日预先安排的需求复评与返家时间。</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public ResidentDailyPlan? DailyPlan { get; init; }

    /// <summary>居民自己观察或收到的信息，可能已经过时。</summary>
    public ImmutableArray<AgentFact> Memory { get; init; } = [];

    /// <summary>按最近到访顺序保留的实际走过地格；不共享给其他居民。</summary>
    [JsonRequired]
    public ImmutableArray<int> FamiliarTiles { get; init; } = [];

    /// <summary>近期行动决策的记录。</summary>
    public ImmutableList<AgentDecision> Decisions
    {
        get => _identity.Decisions;
        init
        {
            if (!ReferenceEquals(_identity.Decisions, value))
                _identity = _identity with { Decisions = value };
        }
    }

    /// <summary>下一次自主评估目标的模拟 tick 序。</summary>
    public long NextThinkTick { get; init; }

    /// <summary>最近一次与其他居民交谈的模拟 tick 序。</summary>
    public long LastConversationTick
    {
        get => _identity.LastConversationTick;
        init
        {
            if (!(_identity.LastConversationTick == value))
                _identity = _identity with { LastConversationTick = value };
        }
    }

    /// <summary>当前贸易或递送任务的目的聚落 ID。</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public int DestinationSettlementId
    {
        get => _identity.DestinationSettlementId;
        init
        {
            if (!(_identity.DestinationSettlementId == value))
                _identity = _identity with { DestinationSettlementId = value };
        }
    }

    /// <summary>居民正在实际携带的信息副本。</summary>
    public ImmutableList<AgentFact> CarriedMessages
    {
        get => _identity.CarriedMessages;
        init
        {
            if (!ReferenceEquals(_identity.CarriedMessages, value))
                _identity = _identity with { CarriedMessages = value };
        }
    }

    /// <summary>当前贸易或递送任务的出发聚落 ID。</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public int MissionOriginSettlementId
    {
        get => _identity.MissionOriginSettlementId;
        init
        {
            if (!(_identity.MissionOriginSettlementId == value))
                _identity = _identity with { MissionOriginSettlementId = value };
        }
    }

    /// <summary>当前任务开始的模拟 tick 序。</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public long MissionStartedTick
    {
        get => _identity.MissionStartedTick;
        init
        {
            if (!(_identity.MissionStartedTick == value))
                _identity = _identity with { MissionStartedTick = value };
        }
    }

    /// <summary>当前任务受阻后允许再次尝试的模拟 tick 序。</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public long MissionRetryTick
    {
        get => _identity.MissionRetryTick;
        init
        {
            if (!(_identity.MissionRetryTick == value))
                _identity = _identity with { MissionRetryTick = value };
        }
    }

    /// <summary>当前探索朝向，按八个方向编号。</summary>
    [JsonRequired]
    public int ExplorationHeading
    {
        get => _identity.ExplorationHeading;
        init
        {
            if (!(_identity.ExplorationHeading == value))
                _identity = _identity with { ExplorationHeading = value };
        }
    }

    /// <summary>当前优先补充的材料种类，空值表示无指定优先材料。</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public ResourceKind? MaterialPriority
    {
        get => _identity.MaterialPriority;
        init
        {
            if (!EqualityComparer<ResourceKind?>.Default.Equals(_identity.MaterialPriority, value))
                _identity = _identity with { MaterialPriority = value };
        }
    }

    /// <summary>最近一次职业分工变化的模拟 tick 序。</summary>
    public long JobChangedTick
    {
        get => _identity.JobChangedTick;
        init
        {
            if (!(_identity.JobChangedTick == value))
                _identity = _identity with { JobChangedTick = value };
        }
    }

    /// <summary>在家园当面接受的固定工作设施 ID；零表示没有固定设施。</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public int WorkplaceId
    {
        get => _identity.WorkplaceId;
        init
        {
            if (_identity.WorkplaceId != value)
                _identity = _identity with { WorkplaceId = value };
        }
    }

    /// <summary>在家园接受的自然劳动地块索引；负一表示没有固定采集范围。</summary>
    public int WorkAreaIndex
    {
        get => _identity.WorkAreaIndex;
        init
        {
            if (_identity.WorkAreaIndex != value)
                _identity = _identity with { WorkAreaIndex = value };
        }
    }
}
