namespace SeWZC.WorldBox.Core;

public sealed partial record Tile
{
    /// <summary>按纬度、距水和开局栖息地生成地貌，返回包含肥力与水文的新地格。</summary>
    /// <param name="latitude">从南到北的归一化纬度，范围为负一至一。</param>
    /// <param name="waterDistance">距最近淡水的地格距离。</param>
    /// <param name="demoRace">开局栖息地的种族；空值表示普通地貌。</param>
    /// <param name="variation">开局栖息地的局部噪声；普通地貌不使用。</param>
    internal Tile WithGeneratedBiome(double latitude, int waterDistance, RaceKind? demoRace, double variation)
    {
        if (demoRace is not { } race)
            return WithTerrain(GeneratedTerrain(latitude, waterDistance));
        var fertility = race == RaceKind.Dwarf ? (byte)60 : race == RaceKind.Orc ? (byte)55 : (byte)80;
        var rainfall = race == RaceKind.Elf ? .096 : race == RaceKind.Orc ? .012 : race == RaceKind.Dwarf ? .064 : .036;
        var elevation = Elevation;
        TerrainType terrain;
        if (variation > .58)
        {
            elevation = 120;
            rainfall = .064;
            terrain = TerrainType.Woodland;
        }
        else if (variation < .35 && race is RaceKind.Human or RaceKind.Orc)
        {
            elevation = 160;
            fertility = 50;
            rainfall = .024;
            terrain = TerrainType.Hills;
        }
        else
        {
            terrain = race switch
            {
                RaceKind.Elf => TerrainType.Forest,
                RaceKind.Dwarf => TerrainType.AlpineMeadow,
                RaceKind.Orc => TerrainType.Savanna,
                _ => TerrainType.Meadow,
            };
        }

        return (this with { Elevation = elevation, Fertility = fertility, Rainfall = rainfall, NaturalWaterYield = rainfall })
            .WithTerrain(terrain);
    }

    internal Tile WithSeededPlants() => WithPlants(Terrain switch
        {
            TerrainType.Grass or TerrainType.Hills or TerrainType.Tundra or TerrainType.DryFertile or TerrainType.Meadow
                or TerrainType.Savanna or TerrainType.Scrub or TerrainType.Floodplain
                or TerrainType.AlpineMeadow => new PlantCoverage { Grass = .6, Shrubs = .15 },
            TerrainType.Forest or TerrainType.Woodland or TerrainType.Rainforest => new PlantCoverage
            {
                Trees = .7, Shrubs = .3,
            },
            TerrainType.Wetland => new PlantCoverage { Reeds = .6, Grass = .3 },
            _ => new PlantCoverage(),
        });

    private TerrainType GeneratedTerrain(double latitude, int waterDistance)
    {
        if (Elevation < 79)
            return TerrainType.Sand;
        if (Elevation > 197)
            return TerrainType.Snow;
        if (Elevation > 180)
            return TerrainType.Mountain;
        if (Elevation > 149)
        {
            return Fertility >= 55 && NaturalWaterYield >= .032
                ? TerrainType.AlpineMeadow
                : TerrainType.Hills;
        }

        if (Math.Abs(latitude) > .72)
            return TerrainType.Tundra;
        if (NaturalWaterYield < .016)
        {
            return Fertility >= 65 ? TerrainType.DryFertile :
                Fertility < 30 ? TerrainType.Desert : TerrainType.Savanna;
        }

        if (Fertility < 30)
            return TerrainType.Scrub;
        if (waterDistance <= 3 && Fertility >= 70 && Elevation < 135)
            return TerrainType.Floodplain;
        if (NaturalWaterYield >= .132)
            return Fertility >= 65 ? TerrainType.Rainforest : TerrainType.Wetland;
        if (NaturalWaterYield >= .076)
            return TerrainType.Forest;
        if (NaturalWaterYield >= .044)
            return TerrainType.Woodland;
        return Fertility >= 70 ? TerrainType.Meadow : TerrainType.Grass;
    }
}
