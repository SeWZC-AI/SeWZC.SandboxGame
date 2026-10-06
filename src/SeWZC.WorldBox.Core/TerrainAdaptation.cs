using System.Text.Json.Serialization;

namespace SeWZC.WorldBox.Core;

public sealed partial class Tile
{

    /// <summary>此格每日降水量，以水资源单位计，不含邻近河湖供水。</summary>
    [JsonPropertyName("rain")]
    [JsonRequired]
    public double Rainfall { get; set; }

    /// <summary>水系生成时记录的河道宽度，以地格为单位。</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public byte RiverWidth { get; set; }
}

/// <summary>种族对地形的宜居性、移动成本和劳动效率修正。</summary>
/// <param name="Habitable">该地形是否适合此种族定居。</param>
/// <param name="Movement">此种族在该地形上的移动成本倍率，越低越快。</param>
/// <param name="Productivity">此种族在该地形上的劳动产量倍率。</param>
public readonly record struct TerrainAdaptation(bool Habitable, double Movement, double Productivity);

/// <summary>查询种族对各类地形的适应程度及步行通行能力。</summary>
public static class RaceTerrainRules
{
    /// <summary>查询种族在指定地形上的宜居性、移动成本和劳动效率修正。</summary>
    /// <param name="race">居民种族，用于应用对应的通行或劳动规则。</param>
    /// <param name="terrain">待查询或设置的地形类别。</param>
    public static TerrainAdaptation For(RaceKind race, TerrainType terrain)
    {
        var common = terrain is TerrainType.Grass or TerrainType.Meadow or TerrainType.Woodland
            or TerrainType.Floodplain;
        var suitable = race switch
        {
            RaceKind.Elf => common || terrain is TerrainType.Forest or TerrainType.Rainforest or TerrainType.Wetland,
            RaceKind.Dwarf => common || terrain is TerrainType.Hills or TerrainType.Mountain or TerrainType.AlpineMeadow
                or TerrainType.Tundra or TerrainType.Scrub,
            RaceKind.Orc => common || terrain is TerrainType.Hills or TerrainType.Savanna or TerrainType.DryFertile
                or TerrainType.Scrub or TerrainType.Forest,
            _ => common || terrain is TerrainType.Forest or TerrainType.Hills or TerrainType.Savanna
                or TerrainType.DryFertile,
        };
        var favored = race switch
        {
            RaceKind.Elf => terrain is TerrainType.Forest or TerrainType.Rainforest or TerrainType.Woodland
                or TerrainType.Wetland,
            RaceKind.Dwarf => terrain is TerrainType.Hills or TerrainType.Mountain or TerrainType.AlpineMeadow,
            RaceKind.Orc => terrain is TerrainType.Savanna or TerrainType.DryFertile or TerrainType.Scrub,
            _ => terrain is TerrainType.Grass or TerrainType.Meadow or TerrainType.Floodplain,
        };
        return new TerrainAdaptation(suitable, favored ? .8 : suitable ? 1 : 1.25, favored ? 1.15 : suitable ? 1 : .75);
    }

    /// <summary>按基础通行和种族特例判断居民能否步行进入地格。</summary>
    /// <param name="tile">待查询或操作的地格状态。</param>
    /// <param name="race">居民种族，用于应用对应的通行或劳动规则。</param>
    public static bool CanWalk(Tile tile, RaceKind race)
    {
        return tile.IsWalkable || (race == RaceKind.Dwarf && tile.Terrain == TerrainType.Mountain);
    }
}
