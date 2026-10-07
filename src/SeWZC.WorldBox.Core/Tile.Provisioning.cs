using System.Text.Json.Serialization;

namespace SeWZC.WorldBox.Core;

public sealed partial class Tile
{
    private BridgeDirection _bridgeDirection;
    private int _claimedSettlementId;

    /// <summary>独占登记此格的聚落 ID，0 表示尚未登记。</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public int ClaimedSettlementId
    {
        get => _claimedSettlementId;
        set
        {
            if (_claimedSettlementId == value)
                return;
            _claimedSettlementId = value;
            TerritoryCounts?.InvalidateClaims();
        }
    }

    /// <summary>此格桥梁允许通行的轴向。</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public BridgeDirection BridgeDirection
    {
        get => _bridgeDirection;
        set
        {
            if (_bridgeDirection == value)
                return;
            _bridgeDirection = value;
            TerritoryCounts?.InvalidateTraversal();
        }
    }

    /// <summary>此格桥梁等级，0 表示没有桥梁。</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public byte BridgeLevel { get; set; }

    /// <summary>最近记录取水量的模拟日序。</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public long WaterDrawTick { get; set; }

    /// <summary>最近记录的模拟日内，已从此格取出的水量。</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public double WaterDrawn { get; set; }

    /// <summary>此格每日基础自然供水量，包含降水及邻近河湖影响。</summary>
    [JsonPropertyName("w")]
    [JsonRequired]
    public double NaturalWaterYield { get; set; }

    /// <summary>自然植物各类别的组成份额。</summary>
    [JsonPropertyName("p")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public PlantCoverage Plants { get; set; }
}
