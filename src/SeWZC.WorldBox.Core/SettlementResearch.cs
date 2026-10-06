namespace SeWZC.WorldBox.Core;

/// <summary>聚落已掌握的研究及当前项目进度，包含从外地收到的知识。</summary>
public sealed class SettlementResearch
{
    /// <summary>当前研究项目的事件、贡献者和进度采样记录。</summary>
    public ProjectObservation Observation { get; set; } = new();
    /// <summary>最近一次完成研究关联的事件 ID。</summary>
    public int LastCompletionEventId { get; set; }
    /// <summary>关联或归属聚落的稳定 ID。</summary>
    public int SettlementId { get; set; }
    /// <summary>当前研究项目，空值表示没有进行中的项目。</summary>
    public ResearchKind? ActiveProject { get; set; }
    /// <summary>当前研究已经累计的工作量。</summary>
    public double Progress { get; set; }
    /// <summary>完成当前研究所需的总工作量。</summary>
    public double RequiredProgress { get; set; }
    /// <summary>本聚落已经研究完成或通过递送掌握的知识。</summary>
    public List<ResearchKind> Completed { get; set; } = [];
}
