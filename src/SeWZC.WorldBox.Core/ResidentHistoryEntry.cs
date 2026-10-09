namespace SeWZC.WorldBox.Core;

/// <summary>居民档案中的一条个人经历。</summary>
public sealed record ResidentHistoryEntry
{
    /// <summary>关联的世界事件 ID，0 表示未关联事件。</summary>
    public int EventId { get; init; }

    /// <summary>本次经历关联的聚落 ID。</summary>
    public int SettlementId { get; init; }

    /// <summary>本次经历关联的国家 ID。</summary>
    public int NationId { get; init; }

    /// <summary>本次经历关联的信息依据 ID。</summary>
    public int EvidenceFactId { get; init; }

    /// <summary>经历发生的模拟 tick 序。</summary>
    public long Tick { get; init; }

    /// <summary>经历的重要程度。</summary>
    public EventImportance Importance { get; init; } = EventImportance.Notable;

    /// <summary>档案中显示的经历描述。</summary>
    public string Text { get; init; } = "";

    /// <summary>该经历是否经玩家编辑。</summary>
    public bool PlayerEdited { get; init; }

    /// <summary>用于未来性格调整的结构化经历类别。</summary>
    public PersonalExperienceKind Experience { get; init; }

    /// <summary>结构化经历对未来性格的影响强度。</summary>
    public double Impact { get; init; }
}
