namespace SeWZC.WorldBox.Core;

/// <summary>已记录的目标选择，包含当时的评分、理由和所用信息。</summary>
public sealed class AgentDecision
{
    /// <summary>作出目标选择的模拟日序。</summary>
    public long Tick { get; set; }

    /// <summary>当时选择的行动目标类别。</summary>
    public AgentGoalKind Goal { get; set; }

    /// <summary>当时选择该目标的评分。</summary>
    public double Score { get; set; }

    /// <summary>当时选择该目标的理由。</summary>
    public string Reason { get; set; } = "";

    /// <summary>目标评分采用的信息依据 ID。</summary>
    public int EvidenceFactId { get; set; }

    /// <summary>依据的信息最初被观察的模拟日序。</summary>
    public long KnowledgeObservedTick { get; set; }

    /// <summary>该信息副本的提供者居民 ID。</summary>
    public int SourceResidentId { get; set; }
}
