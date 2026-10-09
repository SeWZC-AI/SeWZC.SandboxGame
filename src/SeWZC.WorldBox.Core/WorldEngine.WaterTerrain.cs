using SeWZC.WorldBox.Core.Runtime;

namespace SeWZC.WorldBox.Core;

public sealed partial class WorldEngine
{
    /// <summary>判断是否为水域地形。</summary>
    /// <param name="terrain">地形类别。</param>
    public static bool IsWaterTerrain(TerrainType terrain)
    {
        return terrain is TerrainType.DeepWater or TerrainType.Water or TerrainType.River or TerrainType.Lake
            or TerrainType.Stream or TerrainType.LargeRiver;
    }

    /// <summary>判断地格是否为淡水水域。</summary>
    /// <param name="tile">要判断水域类型的地格。</param>
    public static bool IsFreshWater(Tile tile)
    {
        return tile.Terrain is TerrainType.River or TerrainType.Lake or TerrainType.Stream or TerrainType.LargeRiver;
    }

    /// <summary>判断地格是否有自然环境供水，不表示此处可打水。</summary>
    /// <param name="tile">要判断环境供水能力的地格。</param>
    public static bool IsWaterSource(Tile tile)
    {
        return IsFreshWater(tile) || tile.NaturalWaterYield > 0;
    }

    private void GenerateLakesAndWater()
    {
        GenerateHydrology();
    }


    private void TickPlants()
    {
        if (!Rules.ResourceRegeneration)
            return;
        // 将每年植物复评分摊到每 tick 各行，避免年末全图更新峰值。
        var band = (int)((SimulationTick - 1) % SimulationTime.TicksPerYear);
        var firstRow = band * Height / SimulationTime.TicksPerYear;
        var lastRow = (band + 1) * Height / SimulationTime.TicksPerYear;
        Span<double> nearby = stackalloc double[4];
        for (var y = firstRow; y < lastRow; y++)
        for (var x = 0; x < Width; x++)
        {
            var tile = Tiles[Index(x, y)];
            if (!tile.Value.IsWalkable || tile.Value.Improvement != LandImprovement.None)
                continue;
            if (tile.Value.FireTicks > 0)
            {
                tile.Replace(tile.Value.WithPlants(new PlantCoverage()));
                continue;
            }

            var plants = tile.Value.Plants;
            nearby.Clear();
            if (x + 1 < Width)
                Include(Tiles[Index(x + 1, y)].Value.Plants, nearby);
            if (y + 1 < Height)
                Include(Tiles[Index(x, y + 1)].Value.Plants, nearby);
            if (x > 0)
                Include(Tiles[Index(x - 1, y)].Value.Plants, nearby);
            if (y > 0)
                Include(Tiles[Index(x, y - 1)].Value.Plants, nearby);
            for (var species = 0; species < 4; species++)
            {
                var kind = (PlantKind)species;
                var suitable = kind == PlantKind.Reeds ? tile.Value.NaturalWaterYield >= .01
                    : kind == PlantKind.Trees ? tile.Value.NaturalWaterYield >= .004 && tile.Value.Fertility >= 40
                    : tile.Value.Fertility >= 15;
                var value = plants.Get(kind);
                var capacity = suitable ? tile.Value.Fertility / 100d * (tile.Value.DroughtTicks > 0 ? .3 : 1) : 0;
                value = suitable
                    ? value + .06 * value * (1 - value / Math.Max(.01, capacity)) + nearby[species] * .012
                    : value * .8;
                plants = plants.WithCoverage(kind, Math.Clamp(value, 0, 1));
            }

            var total = plants.Total;
            if (total > 1)
            {
                for (var species = 0; species < 4; species++)
                    plants = plants.WithCoverage((PlantKind)species, plants.Get((PlantKind)species) / total);
            }

            tile.Replace(tile.Value.WithPlants(plants));
            if (tile.Value.Terrain is TerrainType.Grass or TerrainType.DryFertile &&
                plants.Trees * Math.Min(1, tile.Value.ResourceAmount / 100) >= .5 && tile.Value.ClaimedSettlementId == 0)
                tile.Replace(tile.Value.WithTerrain(TerrainType.Forest));
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
