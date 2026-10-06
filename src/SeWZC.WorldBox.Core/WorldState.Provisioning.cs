using System.Text.Json.Serialization;

namespace SeWZC.WorldBox.Core;

/// <summary>桥梁允许通行的轴向。</summary>
public enum BridgeDirection
{
    /// <summary>左右通行。</summary>
    Horizontal,
    /// <summary>上下通行。</summary>
    Vertical,
}

public sealed partial class Building
{
    /// <summary>规划此设施用途的理由。</summary>
    public string PlanningReason { get; set; } = "";
    /// <summary>选择此地建造的理由。</summary>
    public string SiteReason { get; set; } = "";

    /// <summary>设施等级，独立于聚落的村镇城等级。</summary>
    [JsonRequired]
    public int Level { get; set; } = 1;

    /// <summary>本轮升级或改向已累计的施工量。</summary>
    public double UpgradeProgress { get; set; }
    /// <summary>本轮升级或改向所需的总施工量，0 表示没有项目。</summary>
    public double UpgradeRequired { get; set; }
    /// <summary>桥梁当前允许通行的轴向。</summary>
    public BridgeDirection Direction { get; set; }
    /// <summary>改造完成后采用的桥梁轴向，空值表示未安排改向。</summary>
    public BridgeDirection? PendingDirection { get; set; }

    /// <summary>是否有正在进行的升级或改向项目。</summary>
    [JsonIgnore]
    public bool IsUpgrading => UpgradeRequired > 0;

    /// <summary>由设施等级计算的劳动效率倍率。</summary>
    [JsonIgnore]
    public double Efficiency => 1 + (Level - 1) * .25;
}

public sealed partial class Resident
{
    /// <summary>口渴程度，越高表示越缺水。</summary>
    [JsonRequired]
    public double Thirst { get; set; }
}

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
            if (_claimedSettlementId == value) return;
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
            if (_bridgeDirection == value) return;
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

public sealed partial class Settlement
{
    /// <summary>建村材料是否仍在运输、尚未完成交付。</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public bool FoundationPending { get; set; }

    /// <summary>人口允许的最大占地半径，以地格为单位。</summary>
    [JsonRequired]
    public int MaxClaimRadius { get; set; } = 6;
}

/// <summary>乔木、灌木、草本和芦苇占地格植物组成的份额。</summary>
public struct PlantCoverage : IEquatable<PlantCoverage>
{
    /// <summary>乔木在此格植物组成中的份额。</summary>
    [JsonPropertyName("t")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public double Trees { get; set; }

    /// <summary>灌木在此格植物组成中的份额。</summary>
    [JsonPropertyName("s")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public double Shrubs { get; set; }

    /// <summary>草本在此格植物组成中的份额。</summary>
    [JsonPropertyName("g")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public double Grass { get; set; }

    /// <summary>芦苇在此格植物组成中的份额。</summary>
    [JsonPropertyName("r")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public double Reeds { get; set; }

    /// <summary>比较四类自然植物的组成份额是否相等。</summary>
    /// <param name="other">用于比较的同类型值。</param>
    public readonly bool Equals(PlantCoverage other)
    {
        return Trees.Equals(other.Trees) && Shrubs.Equals(other.Shrubs)
                                         && Grass.Equals(other.Grass) && Reeds.Equals(other.Reeds);
    }

    /// <summary>比较四类自然植物的组成份额是否相等。</summary>
    /// <param name="obj">用于比较的对象，空值或其他类型均不相等。</param>
    public readonly override bool Equals(object? obj)
    {
        return obj is PlantCoverage other && Equals(other);
    }

    /// <summary>根据四类自然植物的份额计算哈希值。</summary>
    public readonly override int GetHashCode()
    {
        return HashCode.Combine(Trees, Shrubs, Grass, Reeds);
    }

    /// <summary>四类自然植物组成份额的合计。</summary>
    [JsonIgnore]
    public readonly double Total => Trees + Shrubs + Grass + Reeds;

    /// <summary>读取自然植物的组成份额；作物不存储在此结构中，返回零。</summary>
    /// <param name="kind">植物类别。</param>
    public readonly double Get(PlantKind kind)
    {
        return kind switch
        {
            PlantKind.Trees => Trees, PlantKind.Shrubs => Shrubs, PlantKind.Grass => Grass,
            PlantKind.Reeds => Reeds, _ => 0,
        };
    }

    /// <summary>替换自然植物的组成份额；作物不存储在此结构中，不作修改。</summary>
    /// <param name="kind">植物类别。</param>
    /// <param name="cover">要设置的自然植物组成份额。</param>
    public void Set(PlantKind kind, double cover)
    {
        switch (kind)
        {
            case PlantKind.Trees: Trees = cover; break;
            case PlantKind.Shrubs: Shrubs = cover; break;
            case PlantKind.Grass: Grass = cover; break;
            case PlantKind.Reeds: Reeds = cover; break;
        }
    }
}

public sealed partial class Army
{
    /// <summary>军队实际携带的饮水补给。</summary>
    [JsonRequired]
    public double WaterSupplies { get; set; }
}
