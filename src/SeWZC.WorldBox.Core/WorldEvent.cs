namespace SeWZC.WorldBox.Core;

/// <summary>世界编年史中的事件记录，关联主体、位置和已记录的前因。</summary>
public sealed class WorldEvent
{
    /// <summary>事件发生的模拟日序。</summary>
    public long Tick { get; set; }

    /// <summary>事件所属类别。</summary>
    public WorldEventKind Kind { get; set; }

    /// <summary>编年史中显示的事件描述。</summary>
    public string Message { get; set; } = "";

    /// <summary>事件地点的横向地格坐标，-1 表示没有具体地点。</summary>
    public int X { get; set; } = -1;

    /// <summary>事件地点的纵向地格坐标，-1 表示没有具体地点。</summary>
    public int Y { get; set; } = -1;

    /// <summary>事件记录的具体行动。</summary>
    public EventAction Action { get; set; }

    /// <summary>关联或归属聚落的稳定 ID。</summary>
    public int SettlementId { get; set; }

    /// <summary>事件涉及的另一聚落 ID。</summary>
    public int SecondSettlementId { get; set; }

    /// <summary>事件使用的信息依据 ID。</summary>
    public int EvidenceFactId { get; set; }

    /// <summary>除主要前因外关联的其他前因事件 ID。</summary>
    public List<int> AdditionalCauseEventIds { get; set; } = [];

    /// <summary>世界事件的稳定 ID。</summary>
    public int Id { get; set; }

    /// <summary>事件的重要程度，用于展示及容量淘汰。</summary>
    public EventImportance Importance { get; set; } = EventImportance.Notable;

    /// <summary>关联居民的稳定 ID。</summary>
    public int ResidentId { get; set; }

    /// <summary>关联或归属国家的稳定 ID。</summary>
    public int NationId { get; set; }

    /// <summary>事件涉及的另一国家 ID。</summary>
    public int SecondNationId { get; set; }

    /// <summary>主要前因事件 ID，0 表示未关联前因。</summary>
    public int CauseEventId { get; set; }
}
