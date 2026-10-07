using System.Collections.Immutable;
namespace SeWZC.WorldBox.Core;

/// <summary>世界编年史中的一条事件记录。</summary>
public sealed record WorldEvent
{
    /// <summary>事件发生的模拟日序。</summary>
    public long Tick { get; init; }

    /// <summary>事件所属类别。</summary>
    public WorldEventKind Kind { get; init; }

    /// <summary>编年史中显示的事件描述。</summary>
    public string Message { get; init; } = "";

    /// <summary>事件地点的横向地格坐标，-1 表示没有具体地点。</summary>
    public int X { get; init; } = -1;

    /// <summary>事件地点的纵向地格坐标，-1 表示没有具体地点。</summary>
    public int Y { get; init; } = -1;

    /// <summary>事件记录的具体行动。</summary>
    public EventAction Action { get; init; }

    /// <summary>事件关联的聚落 ID。</summary>
    public int SettlementId { get; init; }

    /// <summary>事件涉及的另一聚落 ID。</summary>
    public int SecondSettlementId { get; init; }

    /// <summary>事件使用的信息依据 ID。</summary>
    public int EvidenceFactId { get; init; }

    /// <summary>除主要前因外关联的其他前因事件 ID。</summary>
    public ImmutableList<int> AdditionalCauseEventIds { get; init; } = [];

    /// <summary>世界事件的稳定 ID。</summary>
    public int Id { get; init; }

    /// <summary>事件的重要程度，用于展示及容量淘汰。</summary>
    public EventImportance Importance { get; init; } = EventImportance.Notable;

    /// <summary>关联居民的稳定 ID。</summary>
    public int ResidentId { get; init; }

    /// <summary>事件关联的国家 ID。</summary>
    public int NationId { get; init; }

    /// <summary>事件涉及的另一国家 ID。</summary>
    public int SecondNationId { get; init; }

    /// <summary>主要前因事件 ID，0 表示未关联前因。</summary>
    public int CauseEventId { get; init; }
}
