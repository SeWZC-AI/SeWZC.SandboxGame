namespace SeWZC.WorldBox.Core;

/// <summary>存档保留的加权请愿记录；当前制度接收与决策使用 InstitutionReport。</summary>
public sealed class CivicOpinion
{
    /// <summary>请愿依据的信息记录 ID。</summary>
    public int FactId { get; set; }

    /// <summary>关联居民的稳定 ID。</summary>
    public int ResidentId { get; set; }

    /// <summary>请愿涉及的信息议题类别。</summary>
    public AgentFactKind Topic { get; set; }

    /// <summary>请愿记录的议题数值。</summary>
    public double Value { get; set; }

    /// <summary>请愿记录的议题权重。</summary>
    public double Weight { get; set; }

    /// <summary>议题最初被观察的模拟日序。</summary>
    public long ObservedTick { get; set; }

    /// <summary>请愿被接收的模拟日序。</summary>
    public long ReceivedTick { get; set; }
}
