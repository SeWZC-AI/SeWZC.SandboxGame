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

    private void SeedPlants(TileCursor tile)
    {
        tile.Plants = tile.Terrain switch
        {
            TerrainType.Grass or TerrainType.Hills or TerrainType.Tundra or TerrainType.DryFertile or TerrainType.Meadow
                or TerrainType.Savanna or TerrainType.Scrub or TerrainType.Floodplain
                or TerrainType.AlpineMeadow => new PlantCoverage { Grass = .6, Shrubs = .15 },
            TerrainType.Forest or TerrainType.Woodland or TerrainType.Rainforest => new PlantCoverage
            {
                Trees = .7,
                Shrubs = .3,
            },
            TerrainType.Wetland => new PlantCoverage { Reeds = .6, Grass = .3 },
            _ => new PlantCoverage(),
        };
    }

    private void TickPlants()
    {
        if (!Current.Rules.ResourceRegeneration)
            return;
        // 将每年植物复评分摊到每日各行，避免年末全图更新峰值。
        var band = (int)((Current.Tick - 1) % 120);
        var firstRow = band * Current.Height / 120;
        var lastRow = (band + 1) * Current.Height / 120;
        Span<double> nearby = stackalloc double[4];
        for (var y = firstRow; y < lastRow; y++)
            for (var x = 0; x < Current.Width; x++)
            {
                var tile = Current.Tiles[Index(x, y)];
                if (!tile.IsWalkable || tile.Improvement != LandImprovement.None)
                    continue;
                if (tile.FireTicks > 0)
                {
                    tile.Plants = new PlantCoverage();
                    continue;
                }

                var plants = tile.Plants;
                nearby.Clear();
                if (x + 1 < Current.Width)
                    Include(Current.Tiles[Index(x + 1, y)].Plants, nearby);
                if (y + 1 < Current.Height)
                    Include(Current.Tiles[Index(x, y + 1)].Plants, nearby);
                if (x > 0)
                    Include(Current.Tiles[Index(x - 1, y)].Plants, nearby);
                if (y > 0)
                    Include(Current.Tiles[Index(x, y - 1)].Plants, nearby);
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
                    plants = plants.WithCoverage(kind, Math.Clamp(value, 0, 1));
                }

                var total = plants.Total;
                if (total > 1)
                {
                    for (var species = 0; species < 4; species++)
                        plants = plants.WithCoverage((PlantKind)species, plants.Get((PlantKind)species) / total);
                }

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
