namespace SeWZC.WorldBox.Core;

/// <summary>聚落机构已收到的议题，保留最初观察者、其当时职业和观察时间。</summary>
public sealed class InstitutionReport
{
    /// <summary>关联的世界事件 ID，0 表示未关联事件。</summary>
    public int EventId { get; set; }

    /// <summary>实际接收报告的聚落 ID。</summary>
    public int RecipientSettlementId { get; set; }

    /// <summary>报告所依据的信息记录 ID。</summary>
    public int FactId { get; set; }

    /// <summary>最初观察者的居民 ID。</summary>
    public int OriginResidentId { get; set; }

    /// <summary>将报告递交给机构的代表居民 ID。</summary>
    public int RepresentativeId { get; set; }

    /// <summary>最初观察者当时的职业，用于议题权重计算。</summary>
    public Profession ReportedProfession { get; set; }

    /// <summary>报告的议题类别。</summary>
    public AgentFactKind Topic { get; set; }

    /// <summary>议题关联主体的 ID，含义由议题类别决定。</summary>
    public int SubjectId { get; set; }

    /// <summary>议题的观测值，含义由议题类别决定。</summary>
    public double Value { get; set; }

    /// <summary>报告的可信度。</summary>
    public double Confidence { get; set; }

    /// <summary>最初观察发生的模拟日序。</summary>
    public long ObservedTick { get; set; }

    /// <summary>机构实际收到报告的模拟日序。</summary>
    public long ReceivedTick { get; set; }
}
