using System.Text.Json.Serialization;

namespace SeWZC.WorldBox.Core;

/// <summary>一个地格的地形、资源、归属、生态及局部灾害状态。</summary>
public sealed partial class Tile
{
    private int _fireTicks;
    private int _nationId;
    private TerrainType _terrain;

    /// <summary>当前地形；改变时使通行及受影响的占领统计失效。</summary>
    public TerrainType Terrain
    {
        get => _terrain;
        set
        {
            if (_terrain == value) return;
            TerritoryCounts?.InvalidateTraversal();
            if (WorldEngine.IsWaterTerrain(_terrain) != WorldEngine.IsWaterTerrain(value))
                TerritoryCounts?.InvalidateClaims();
            _terrain = value;
            if (value is not (TerrainType.Stream or TerrainType.River or TerrainType.LargeRiver)) RiverWidth = 0;
        }
    }

    /// <summary>用于地形和水系生成的海拔记录；运行时移动与劳动遵循地形规则。</summary>
    public byte Elevation { get; set; }

    /// <summary>当前肥力，取值为 0 至 100。</summary>
    public byte Fertility { get; set; }

    [JsonIgnore]
    internal TerritoryCounts? TerritoryCounts { get; set; }

    /// <summary>拥有此地的国家 ID，0 表示无国家归属。</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public int NationId
    {
        get => _nationId;
        set
        {
            if (_nationId == value) return;
            TerritoryCounts?.Change(_nationId, value);
            _nationId = value;
        }
    }

    /// <summary>以此格为中心的聚落 ID，0 表示没有聚落中心。</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public int SettlementId { get; set; }

    /// <summary>火灾剩余模拟日数，0 表示未燃烧。</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public int FireTicks
    {
        get => _fireTicks;
        set
        {
            if (_fireTicks > 0 != value > 0) TerritoryCounts?.InvalidateTraversal();
            _fireTicks = value;
        }
    }

    /// <summary>干旱剩余模拟日数，0 表示未受干旱影响。</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public int DroughtTicks { get; set; }

    /// <summary>按基础步行规则判断地形和改良是否可通行，不含种族特例。</summary>
    [JsonIgnore]
    public bool IsWalkable => Terrain == TerrainType.Mountain
        ? Improvement == LandImprovement.MountainPass
        : Terrain is TerrainType.Water or TerrainType.River or TerrainType.LargeRiver or TerrainType.Lake
            ? Improvement == LandImprovement.Bridge
            : Terrain != TerrainType.DeepWater;

    /// <summary>此格每日降水量，以水资源单位计，不含邻近河湖供水。</summary>
    [JsonPropertyName("rain")]
    [JsonRequired]
    public double Rainfall { get; set; }

    /// <summary>水系生成时记录的河道宽度，以地格为单位。</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public byte RiverWidth { get; set; }

    /// <summary>最近一次记录扑救效果的模拟日序。</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public long FireSuppressionTick { get; set; }

    /// <summary>在最近记录的模拟日内，扑救累计缩短的火灾日数。</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public int FireSuppressed { get; set; }

    /// <summary>道路等级，0 表示没有道路。</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public byte RoadLevel { get; set; }

    /// <summary>可采集的共享自然资源存量。</summary>
    public double ResourceAmount { get; set; } = 100;
}
