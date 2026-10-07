namespace SeWZC.WorldBox.Core;

/// <summary>种族的地形适应规则。</summary>
public static class RaceTerrainRules
{
    /// <summary>查询种族对指定地形的适应参数。</summary>
    /// <param name="race">居民种族。</param>
    /// <param name="terrain">地形类别。</param>
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
    /// <param name="tile">准备步行进入的地格。</param>
    /// <param name="race">居民种族。</param>
    public static bool CanWalk(Tile tile, RaceKind race)
    {
        return tile.IsWalkable || (race == RaceKind.Dwarf && tile.Terrain == TerrainType.Mountain);
    }
}
