using System.Text.Json.Serialization;

namespace SeWZC.WorldBox.Core;

/// <summary>居民一次行动决策的记录。</summary>
public sealed record AgentDecision
{
    /// <summary>作出目标选择的模拟日序。</summary>
    public long Tick { get; init; }

    /// <summary>当时选择的行动目标类别。</summary>
    public AgentGoalKind Goal { get; init; }

    /// <summary>当时选择该目标的评分。</summary>
    public double Score { get; init; }

    /// <summary>当时选择该目标的理由。</summary>
    public string Reason { get; init; } = "";

    /// <summary>目标评分采用的信息依据 ID。</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public int EvidenceFactId { get; init; }

    /// <summary>依据的信息最初被观察的模拟日序。</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public long KnowledgeObservedTick { get; init; }

    /// <summary>该信息副本的提供者居民 ID。</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public int SourceResidentId { get; init; }
}
