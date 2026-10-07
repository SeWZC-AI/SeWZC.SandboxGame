namespace SeWZC.WorldBox.Core;

/// <summary>居民观察或获知的不可变信息快照，可能已经过时。</summary>
public sealed record AgentFact
{
    /// <summary>关联的世界事件 ID，0 表示未关联事件。</summary>
    public int EventId { get; init; }

    /// <summary>该信息关联的战役起始事件 ID。</summary>
    public int CampaignEventId { get; init; }

    /// <summary>战争信息中的军事目标。</summary>
    public WarObjective WarObjective { get; init; }

    /// <summary>战争信息中的目标国家 ID。</summary>
    public int TargetNationId { get; init; }

    /// <summary>信息记录的稳定 ID。</summary>
    public int Id { get; init; }

    /// <summary>信息议题类别。</summary>
    public AgentFactKind Kind { get; init; }

    /// <summary>信息关联主体的 ID，具体含义由议题类别决定。</summary>
    public int SubjectId { get; init; }

    /// <summary>信息所指地点的横向地格坐标。</summary>
    public int X { get; init; }

    /// <summary>信息所指地点的纵向地格坐标。</summary>
    public int Y { get; init; }

    /// <summary>观察得到的数值，具体含义由议题类别决定。</summary>
    public double Value { get; init; }

    /// <summary>最初观察发生的时间；转述时保留该时间。</summary>
    public long ObservedTick { get; init; }

    /// <summary>持有本副本的一方获知信息的时间。</summary>
    public long LearnedTick { get; init; }

    /// <summary>最初观察者的居民 ID。</summary>
    public int OriginResidentId { get; init; }

    /// <summary>最初观察者当时的职业。</summary>
    public Profession OriginProfession { get; init; }

    /// <summary>将本副本提供给持有者的居民 ID。</summary>
    public int SourceResidentId { get; init; }

    /// <summary>本副本的可信度，范围为 0 至 1。</summary>
    public double Confidence { get; init; } = 1;

    /// <summary>信息已经转述的次数。</summary>
    public int Hops { get; init; }

    /// <summary>观察或转述的内容说明。</summary>
    public string Text { get; init; } = "";

    /// <summary>根据原始观察时间和议题有效期，计算指定日序的可信度。</summary>
    /// <param name="tick">评估信息的模拟日序。</param>
    public double ReliabilityAt(long tick) =>
        Math.Clamp(Confidence, 0, 1) * Math.Clamp(1 - (tick - ObservedTick) / Topic.Lifetime, 0, 1);

    /// <summary>判断两条信息是否描述同一议题主体及地点。</summary>
    /// <param name="other">用于比较的信息快照。</param>
    public bool HasSameSubject(AgentFact other) =>
        Kind == other.Kind && SubjectId == other.SubjectId && TargetNationId == other.TargetNationId &&
        (!Topic.DistinguishesLocation || (X == other.X && Y == other.Y));

    internal AgentFactTopic Topic => AgentFactTopic.For(Kind);

    internal bool Supersedes(AgentFact old) =>
        ObservedTick > old.ObservedTick || (ObservedTick == old.ObservedTick &&
            (Topic.OrdersSameDayById && Id != old.Id ? Id > old.Id : Confidence > old.Confidence));

    internal long RetentionPriority(int homeId) => LearnedTick + Topic.RetentionBonus(this, homeId);
}
