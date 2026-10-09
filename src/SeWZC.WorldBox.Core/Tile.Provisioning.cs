using System.Text.Json.Serialization;

namespace SeWZC.WorldBox.Core;

public sealed partial record Tile
{
    /// <summary>独占登记此格的聚落 ID，0 表示尚未登记。</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public int ClaimedSettlementId { get; init; }

    /// <summary>此格桥梁允许通行的轴向。</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public BridgeDirection BridgeDirection { get; init; }

    /// <summary>此格桥梁等级，0 表示没有桥梁。</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public byte BridgeLevel { get; init; }

    /// <summary>最近记录取水量的模拟 tick 序。</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public long WaterDrawTick { get; init; }

    /// <summary>最近记录的模拟日内，已从此格取出的水量。</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public double WaterDrawn { get; init; }

    /// <summary>此格基础环境供水量，包含降水及邻近河湖影响；用于口渴抵扣、生态与水井产量。</summary>
    [JsonPropertyName("w")]
    [JsonRequired]
    public double NaturalWaterYield { get; init; }

    /// <summary>自然植物各类别的组成份额。</summary>
    [JsonPropertyName("p")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public PlantCoverage Plants { get; init; }
}
