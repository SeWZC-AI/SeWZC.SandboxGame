namespace SeWZC.WorldBox.Core.Tests;

/// <summary>地格生态缓存的真实输入失效边界。</summary>
public sealed class TileQueryCacheTests
{
    /// <summary>动物耗尽后可食物种和采集效率都重新推导。</summary>
    [Fact]
    public void Depleted_animals_invalidate_edible_species_and_harvest()
    {
        var before = new Tile { Terrain = TerrainType.Grass, Fertility = 80, ResourceAmount = 100,
            Plants = new PlantCoverage { Grass = .6 }, Wildlife = WildlifeKind.Deer, WildlifePopulation = 2 };
        var cache = new TileQueryCache();
        Assert.Equal(WildlifeKind.Deer, cache.EdibleAnimal(before, false));
        Assert.True(cache.HarvestEfficiency(before, WildlifeKind.Deer) > 0);
        var after = before.WithAnimalPopulation(WildlifeKind.Deer, 0);

        cache.Invalidate(TileChange.Between(before, after));

        Assert.Equal(WildlifeKind.None, cache.EdibleAnimal(after, false));
        Assert.Equal(0, cache.HarvestEfficiency(after, WildlifeKind.Deer));
        Assert.Equal(2, before.AnimalPopulation(WildlifeKind.Deer));
    }

    /// <summary>灾害和采收分别使栖息容量与植物产量失效。</summary>
    [Fact]
    public void Fire_and_harvest_invalidate_habitat_and_plant_yield()
    {
        var before = new Tile { Terrain = TerrainType.Forest, Fertility = 80, ResourceAmount = 100,
            Plants = new PlantCoverage { Trees = .7 }, NaturalWaterYield = .1 };
        var cache = new TileQueryCache();
        Assert.True(cache.EnvironmentalCapacity(before, WildlifeKind.Deer) > 0);
        Assert.True(cache.PlantSiteYield(before, true) > 0);
        var after = before with { FireTicks = 1, ResourceAmount = 0, Plants = default };

        cache.Invalidate(TileChange.Between(before, after));

        Assert.Equal(0, cache.EnvironmentalCapacity(after, WildlifeKind.Deer));
        Assert.Equal(0, cache.PlantSiteYield(after, true));
    }

    /// <summary>普通可食物种与限定水生物种的查询不串用缓存。</summary>
    [Fact]
    public void Aquatic_and_land_queries_use_separate_species()
    {
        var tile = new Tile { Wildlife = WildlifeKind.Deer, WildlifePopulation = 10 }
            .WithAnimalPopulation(WildlifeKind.Fish, 2);
        var cache = new TileQueryCache();

        Assert.Equal(WildlifeKind.Deer, cache.EdibleAnimal(tile, false));
        Assert.Equal(WildlifeKind.Fish, cache.EdibleAnimal(tile, true));
        Assert.Equal(WildlifeKind.Deer, cache.EdibleAnimal(tile, false));
    }
}
