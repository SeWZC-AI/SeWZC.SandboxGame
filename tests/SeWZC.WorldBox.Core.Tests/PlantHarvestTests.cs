namespace SeWZC.WorldBox.Core.Tests;

/// <summary>植物采集的数量限制、生物量守恒和输入隔离。</summary>
public sealed class PlantHarvestTests
{
    /// <summary>野外采集至多取走目标植物一成，其他植物保留原有生物量。</summary>
    [Theory]
    [InlineData(true, 4)]
    [InlineData(false, 6)]
    public void Natural_harvest_preserves_other_plants_and_original_tile(bool wood, double expectedAmount)
    {
        var original = new Tile
        {
            ResourceAmount = 100,
            Plants = new PlantCoverage { Trees = .4, Shrubs = .2, Grass = .3, Reeds = .1 },
        };

        var harvest = original.HarvestPlants(100, wood);

        Assert.Equal(expectedAmount, harvest.Amount);
        Assert.Equal(100 - expectedAmount, harvest.Tile.ResourceAmount);
        Assert.Equal(100, original.ResourceAmount);
        Assert.Equal(.4, original.Plants.Trees);
        Assert.Equal(original.HarvestPlants(100, wood), harvest);
        if (wood)
        {
            Assert.Equal(36, harvest.Tile.Plants.Trees * harvest.Tile.ResourceAmount, 10);
            Assert.Equal(20, harvest.Tile.Plants.Shrubs * harvest.Tile.ResourceAmount, 10);
            Assert.Equal(30, harvest.Tile.Plants.Grass * harvest.Tile.ResourceAmount, 10);
            Assert.Equal(10, harvest.Tile.Plants.Reeds * harvest.Tile.ResourceAmount, 10);
        }
        else
        {
            Assert.Equal(40, harvest.Tile.Plants.Trees * harvest.Tile.ResourceAmount, 10);
            Assert.Equal(54, harvest.Tile.PlantStock(false), 10);
        }
    }

    /// <summary>农田允许收获全部现存作物，耗尽后的采集返回原地格。</summary>
    [Fact]
    public void Farmland_harvest_is_limited_by_stock_and_empty_harvest_reuses_tile()
    {
        var original = new Tile { ResourceAmount = 20, Improvement = LandImprovement.Farmland };

        var harvest = original.HarvestPlants(100, false);
        var empty = harvest.Tile.HarvestPlants(100, false);

        Assert.Equal(20, harvest.Amount);
        Assert.Equal(0, harvest.Tile.ResourceAmount);
        Assert.Equal(20, original.ResourceAmount);
        Assert.Equal(0, empty.Amount);
        Assert.Same(harvest.Tile, empty.Tile);
    }
}
