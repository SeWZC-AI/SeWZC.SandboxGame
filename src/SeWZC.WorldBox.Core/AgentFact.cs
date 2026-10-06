namespace SeWZC.WorldBox.Core;

/// <summary>亲眼观察或转述得到的信息，保留原始依据和本副本的获知记录，内容可能已经过时。</summary>
public sealed class AgentFact
{
    /// <summary>关联的世界事件 ID，0 表示未关联事件。</summary>
    public int EventId { get; set; }
    /// <summary>该信息关联的战役起始事件 ID。</summary>
    public int CampaignEventId { get; set; }
    /// <summary>战争信息中的军事目标。</summary>
    public WarObjective WarObjective { get; set; }
    /// <summary>战争信息中的目标国家 ID。</summary>
    public int TargetNationId { get; set; }
    /// <summary>信息记录的稳定 ID。</summary>
    public int Id { get; set; }
    /// <summary>信息议题类别。</summary>
    public AgentFactKind Kind { get; set; }
    /// <summary>信息关联主体的 ID，具体含义由议题类别决定。</summary>
    public int SubjectId { get; set; }
    /// <summary>所在地点的横向地格坐标。</summary>
    public int X { get; set; }
    /// <summary>所在地点的纵向地格坐标。</summary>
    public int Y { get; set; }
    /// <summary>观察得到的数值，具体含义由议题类别决定。</summary>
    public double Value { get; set; }

    /// <summary>最初观察发生的时间；转述时保留该时间。</summary>
    public long ObservedTick { get; set; }

    /// <summary>持有本副本的一方获知信息的时间。</summary>
    public long LearnedTick { get; set; }

    /// <summary>最初观察者的居民 ID。</summary>
    public int OriginResidentId { get; set; }
    /// <summary>最初观察者当时的职业。</summary>
    public Profession OriginProfession { get; set; }
    /// <summary>将本副本提供给持有者的居民 ID。</summary>
    public int SourceResidentId { get; set; }
    /// <summary>本副本的可信度，范围为 0 至 1。</summary>
    public double Confidence { get; set; } = 1;
    /// <summary>信息已经转述的次数。</summary>
    public int Hops { get; set; }
    /// <summary>观察或转述的内容说明。</summary>
    public string Text { get; set; } = "";
}
