using System.Collections.Immutable;
namespace SeWZC.WorldBox.Core;

/// <summary>聚落的本地研究状态。</summary>
public sealed record SettlementResearch
{
    /// <summary>当前研究项目的进展观测记录。</summary>
    public ProjectObservation Observation { get; init; } = new();

    /// <summary>最近一次完成研究关联的事件 ID。</summary>
    public int LastCompletionEventId { get; init; }

    /// <summary>开展研究的聚落 ID。</summary>
    public int SettlementId { get; init; }

    /// <summary>当前研究项目，空值表示没有进行中的项目。</summary>
    public Advancement? ActiveProject { get; init; }

    /// <summary>当前研究已经累计的工作量。</summary>
    public double Progress { get; init; }

    /// <summary>完成当前研究所需的总工作量。</summary>
    public double RequiredProgress { get; init; }

    /// <summary>本聚落已经研究完成或通过递送掌握的知识。</summary>
    public ImmutableList<Advancement> Completed { get; init; } = [];
}
