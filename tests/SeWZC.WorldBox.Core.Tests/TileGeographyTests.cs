namespace SeWZC.WorldBox.Core.Tests;

/// <summary>地貌生成的不可变状态转换。</summary>
public sealed class TileGeographyTests
{
    /// <summary>开局栖息地同时返回地形与水文，保留原地格并清除离开河道后的宽度。</summary>
    [Fact]
    public void Demo_biome_returns_complete_hydrology_without_mutating_source()
    {
        var before = new Tile { Terrain = TerrainType.River, RiverWidth = 3, Fertility = 20,
            Elevation = 100, Rainfall = .1, NaturalWaterYield = .2 };

        var after = before.WithGeneratedBiome(0, 4, RaceKind.Human, .2);

        Assert.Equal(TerrainType.Hills, after.Terrain);
        Assert.Equal(160, after.Elevation);
        Assert.Equal(50, after.Fertility);
        Assert.Equal(.024, after.Rainfall);
        Assert.Equal(after.Rainfall, after.NaturalWaterYield);
        Assert.Equal(0, after.RiverWidth);
        Assert.Equal(TerrainType.River, before.Terrain);
        Assert.Equal(3, before.RiverWidth);
        Assert.Equal(.2, before.NaturalWaterYield);
    }
}
