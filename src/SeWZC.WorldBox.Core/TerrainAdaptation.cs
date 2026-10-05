using System.Text.Json.Serialization;

namespace SeWZC.WorldBox.Core;

public sealed partial class Tile
{
    // Resource units per tile per day. NaturalWaterYield includes riparian supply.
    [JsonPropertyName("rain")]
    [JsonRequired]
    public double Rainfall { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public byte RiverWidth { get; set; }
}

public readonly record struct TerrainAdaptation(bool Habitable, double Movement, double Productivity);

public static class RaceTerrainRules
{
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

    public static bool CanWalk(Tile tile, RaceKind race)
    {
        return tile.IsWalkable || (race == RaceKind.Dwarf && tile.Terrain == TerrainType.Mountain);
    }
}
