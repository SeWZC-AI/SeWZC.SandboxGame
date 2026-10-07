using System.Text.Json.Serialization;

namespace SeWZC.WorldBox.Core;

/// <summary>居民共同生活和生产的聚居单位。</summary>
public sealed partial class Settlement
{
    /// <summary>聚落的稳定 ID。</summary>
    public int Id { get; set; }

    /// <summary>聚落的显示名称。</summary>
    public string Name { get; set; } = "";

    /// <summary>聚落中心的横向地格坐标。</summary>
    public int X { get; set; }

    /// <summary>聚落中心的纵向地格坐标。</summary>
    public int Y { get; set; }

    /// <summary>聚落所属国家的 ID。</summary>
    public int NationId { get; set; }

    /// <summary>本聚落仓库中的实际资源库存。</summary>
    public ResourceStock Resources { get; set; } = new();

    /// <summary>归属本聚落的存活居民数量。</summary>
    public int Population { get; set; }

    /// <summary>本聚落的基础住房容量，不含已运营住宅的额外容量。</summary>
    public int Housing { get; set; } = 40;

    /// <summary>建村材料是否仍在运输、尚未完成交付。</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public bool FoundationPending { get; set; }

    /// <summary>人口允许的最大占地半径，以地格为单位。</summary>
    [JsonRequired]
    public int MaxClaimRadius { get; set; } = 6;

    /// <summary>文化归属的稳定 ID。</summary>
    public int CultureId { get; set; }

    /// <summary>本聚落代表居民的 ID。</summary>
    public int RepresentativeId { get; set; }

    /// <summary>实际在本地观察或收到的公开信息，可能已经过时。</summary>
    public List<AgentFact> PublicKnowledge { get; set; } = [];

    /// <summary>丰饶祝福剩余模拟日数。</summary>
    public int FertilityBoostTicks { get; set; }

    /// <summary>聚落局部护盾剩余模拟日数。</summary>
    public int ShieldTicks { get; set; }
}
