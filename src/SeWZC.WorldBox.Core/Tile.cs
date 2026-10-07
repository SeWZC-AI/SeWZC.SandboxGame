using System.Text.Json.Serialization;

namespace SeWZC.WorldBox.Core;

/// <summary>世界地图的基本空间单元。</summary>
public sealed partial record Tile
{

    /// <summary>当前地形。</summary>
    public TerrainType Terrain { get; init; }

    /// <summary>用于地形和水系生成的海拔记录；运行时移动与劳动遵循地形规则。</summary>
    public byte Elevation { get; init; }

    /// <summary>当前肥力，取值为 0 至 100。</summary>
    public byte Fertility { get; init; }


    /// <summary>拥有此地的国家 ID，0 表示无国家归属。</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public int NationId { get; init; }

    /// <summary>以此格为中心的聚落 ID，0 表示没有聚落中心。</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public int SettlementId { get; init; }

    /// <summary>火灾剩余模拟日数，0 表示未燃烧。</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public int FireTicks { get; init; }

    /// <summary>干旱剩余模拟日数，0 表示未受干旱影响。</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public int DroughtTicks { get; init; }

    /// <summary>按基础步行规则判断地形和改良是否可通行，不含种族特例。</summary>
    [JsonIgnore]
    public bool IsWalkable => Terrain == TerrainType.Mountain
        ? Improvement == LandImprovement.MountainPass
        : Terrain is TerrainType.Water or TerrainType.River or TerrainType.Lake or TerrainType.LargeRiver
            ? Improvement == LandImprovement.Bridge
            : Terrain != TerrainType.DeepWater;

    /// <summary>此格每日降水量，以水资源单位计，不含邻近河湖供水。</summary>
    [JsonPropertyName("rain")]
    [JsonRequired]
    public double Rainfall { get; init; }

    /// <summary>水系生成时记录的河道宽度，以地格为单位。</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public byte RiverWidth { get; init; }

    /// <summary>最近一次记录扑救效果的模拟日序。</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public long FireSuppressionTick { get; init; }

    /// <summary>在最近记录的模拟日内，扑救累计缩短的火灾日数。</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public int FireSuppressed { get; init; }

    /// <summary>道路等级，0 表示没有道路。</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public byte RoadLevel { get; init; }

    /// <summary>可采集的共享自然资源存量。</summary>
    public double ResourceAmount { get; init; } = 100;
    /// <summary>返回改变地形后的地格；离开河道时清除河道宽度。</summary>
    /// <param name="terrain">新的地形。</param>
    public Tile WithTerrain(TerrainType terrain) => Terrain == terrain ? this : this with
    {
        Terrain = terrain,
        RiverWidth = terrain is TerrainType.River or TerrainType.Stream or TerrainType.LargeRiver ? RiverWidth : (byte)0,
    };
}
