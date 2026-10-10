using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace SeWZC.WorldBox.Core;

/// <summary>居民共同生活和生产的聚居单位。</summary>
public sealed partial record Settlement
{
    /// <summary>聚落的稳定 ID。</summary>
    public int Id { get; init; }

    /// <summary>聚落的显示名称。</summary>
    public string Name { get; init; } = "";

    /// <summary>聚落中心的横向地格坐标。</summary>
    public int X { get; init; }

    /// <summary>聚落中心的纵向地格坐标。</summary>
    public int Y { get; init; }

    /// <summary>聚落所属国家的 ID。</summary>
    public int NationId { get; init; }

    /// <summary>本聚落仓库中的实际资源库存。</summary>
    public ResourceStock Resources { get; init; } = new();

    /// <summary>归属本聚落的存活居民数量。</summary>
    public int Population { get; init; }

    /// <summary>建村材料是否仍在运输、尚未完成交付。</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public bool FoundationPending { get; init; }

    /// <summary>人口允许的最大占地半径，以地格为单位。</summary>
    [JsonRequired]
    public int MaxClaimRadius { get; init; } = 6;

    /// <summary>文化归属的稳定 ID。</summary>
    public int CultureId { get; init; }

    /// <summary>本聚落代表居民的 ID。</summary>
    public int RepresentativeId { get; init; }

    /// <summary>实际在本地观察或收到的公开信息，可能已经过时。</summary>
    public ImmutableList<AgentFact> PublicKnowledge { get; init; } = [];

    /// <summary>丰饶祝福剩余模拟 tick 数。</summary>
    public int FertilityBoostTicks { get; init; }

    /// <summary>聚落局部护盾剩余模拟 tick 数。</summary>
    public int ShieldTicks { get; init; }
}
