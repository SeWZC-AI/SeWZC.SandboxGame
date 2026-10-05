namespace SeWZC.WorldBox.Core;

public sealed partial class WorldEngine
{
    public static bool IsWaterTerrain(TerrainType terrain)
    {
        return terrain is TerrainType.Water or TerrainType.DeepWater or TerrainType.River or TerrainType.Stream
            or TerrainType.LargeRiver or TerrainType.Lake;
    }

    public static bool IsFreshWater(Tile tile)
    {
        return tile.Terrain is TerrainType.River or TerrainType.Stream or TerrainType.LargeRiver or TerrainType.Lake;
    }

    public static bool IsWaterSource(Tile tile)
    {
        return IsFreshWater(tile) || tile.NaturalWaterYield > 0;
    }

    private void GenerateLakesAndWater()
    {
        GenerateHydrology();
    }

    private void SeedPlants(Tile tile)
    {
        tile.Plants = tile.Terrain switch
        {
            TerrainType.Forest or TerrainType.Rainforest or TerrainType.Woodland => new PlantCoverage
                { Trees = .7, Shrubs = .3 },
            TerrainType.Grass or TerrainType.DryFertile or TerrainType.Hills or TerrainType.Tundra or TerrainType.Meadow
                or TerrainType.Savanna or TerrainType.Scrub or TerrainType.Floodplain
                or TerrainType.AlpineMeadow => new PlantCoverage { Grass = .6, Shrubs = .15 },
            TerrainType.Wetland => new PlantCoverage { Reeds = .6, Grass = .3 },
            _ => new PlantCoverage(),
        };
    }

    private void TickPlants()
    {
        if (!State.Rules.ResourceRegeneration) return;
        // Each row is updated once per 120-day year, with no yearly full-map spike.
        var band = (int)((State.Tick - 1) % 120);
        var firstRow = band * State.Height / 120;
        var lastRow = (band + 1) * State.Height / 120;
        Span<double> nearby = stackalloc double[4];
        for (var y = firstRow; y < lastRow; y++)
        for (var x = 0; x < State.Width; x++)
        {
            var tile = State.Tiles[Index(x, y)];
            if (!tile.IsWalkable || tile.Improvement != LandImprovement.None) continue;
            if (tile.FireTicks > 0)
            {
                tile.Plants = new PlantCoverage();
                continue;
            }

            var plants = tile.Plants;
            nearby.Clear();
            if (x + 1 < State.Width) Include(State.Tiles[Index(x + 1, y)].Plants, nearby);
            if (y + 1 < State.Height) Include(State.Tiles[Index(x, y + 1)].Plants, nearby);
            if (x > 0) Include(State.Tiles[Index(x - 1, y)].Plants, nearby);
            if (y > 0) Include(State.Tiles[Index(x, y - 1)].Plants, nearby);
            for (var species = 0; species < 4; species++)
            {
                var kind = (PlantKind)species;
                var suitable = kind == PlantKind.Reeds ? tile.NaturalWaterYield >= .01
                    : kind == PlantKind.Trees ? tile.NaturalWaterYield >= .004 && tile.Fertility >= 40
                    : tile.Fertility >= 15;
                var value = plants.Get(kind);
                var capacity = suitable ? tile.Fertility / 100d * (tile.DroughtTicks > 0 ? .3 : 1) : 0;
                value = suitable
                    ? value + .06 * value * (1 - value / Math.Max(.01, capacity)) + nearby[species] * .012
                    : value * .8;
                plants.Set(kind, Math.Clamp(value, 0, 1));
            }

            var total = plants.Total;
            if (total > 1)
                for (var species = 0; species < 4; species++)
                    plants.Set((PlantKind)species, plants.Get((PlantKind)species) / total);
            tile.Plants = plants;
            if (tile.Terrain is TerrainType.Grass or TerrainType.DryFertile &&
                plants.Trees * Math.Min(1, tile.ResourceAmount / 100) >= .5 && tile.ClaimedSettlementId == 0)
                tile.Terrain = TerrainType.Forest;
        }

        static void Include(PlantCoverage plants, Span<double> nearby)
        {
            nearby[0] = Math.Max(nearby[0], plants.Trees);
            nearby[1] = Math.Max(nearby[1], plants.Shrubs);
            nearby[2] = Math.Max(nearby[2], plants.Grass);
            nearby[3] = Math.Max(nearby[3], plants.Reeds);
        }
    }
}
