namespace SeWZC.WorldBox.Core;

/// <summary>居民档案中的一条个人经历。</summary>
public sealed class ResidentHistoryEntry
{
    /// <summary>关联的世界事件 ID，0 表示未关联事件。</summary>
    public int EventId { get; set; }

    /// <summary>本次经历关联的聚落 ID。</summary>
    public int SettlementId { get; set; }

    /// <summary>本次经历关联的国家 ID。</summary>
    public int NationId { get; set; }

    /// <summary>本次经历关联的信息依据 ID。</summary>
    public int EvidenceFactId { get; set; }

    /// <summary>经历发生的模拟日序。</summary>
    public long Tick { get; set; }

    /// <summary>经历的重要程度。</summary>
    public EventImportance Importance { get; set; } = EventImportance.Notable;

    /// <summary>档案中显示的经历描述。</summary>
    public string Text { get; set; } = "";

    /// <summary>该经历是否经玩家编辑。</summary>
    public bool PlayerEdited { get; set; }

    /// <summary>用于未来性格调整的结构化经历类别。</summary>
    public PersonalExperienceKind Experience { get; set; }

    /// <summary>结构化经历对未来性格的影响强度。</summary>
    public double Impact { get; set; }
}
