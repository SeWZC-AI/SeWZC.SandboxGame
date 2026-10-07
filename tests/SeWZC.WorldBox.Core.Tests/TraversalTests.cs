using SeWZC.WorldBox.Core;

namespace SeWZC.WorldBox.Core.Tests;

/// <summary>通行规则对地形、改良、种族和交通方式的判定。</summary>
public sealed class TraversalTests
{
    /// <summary>步行遵循真实地形和改良。</summary>
    [Theory]
    [InlineData(TerrainType.Grass, LandImprovement.None, RaceKind.Human, true)]
    [InlineData(TerrainType.Stream, LandImprovement.None, RaceKind.Human, true)]
    [InlineData(TerrainType.DeepWater, LandImprovement.None, RaceKind.Human, false)]
    [InlineData(TerrainType.Water, LandImprovement.None, RaceKind.Human, false)]
    [InlineData(TerrainType.River, LandImprovement.None, RaceKind.Human, false)]
    [InlineData(TerrainType.Lake, LandImprovement.None, RaceKind.Human, false)]
    [InlineData(TerrainType.LargeRiver, LandImprovement.None, RaceKind.Human, false)]
    [InlineData(TerrainType.River, LandImprovement.Bridge, RaceKind.Human, true)]
    [InlineData(TerrainType.Mountain, LandImprovement.None, RaceKind.Human, false)]
    [InlineData(TerrainType.Mountain, LandImprovement.MountainPass, RaceKind.Human, true)]
    [InlineData(TerrainType.Mountain, LandImprovement.None, RaceKind.Dwarf, true)]
    [InlineData(TerrainType.Mountain, LandImprovement.None, RaceKind.Elf, false)]
    public void Walking_respects_terrain_improvements_and_race(
        TerrainType terrain, LandImprovement improvement, RaceKind race, bool expected)
    {
        var tile = new Tile { Terrain = terrain, Improvement = improvement };

        Assert.Equal(expected, WorldEngine.CanTraverse(tile, TravelMode.Foot, race));
    }

    /// <summary>舟船可以进入水域和可步行岸地。</summary>
    [Theory]
    [InlineData(TerrainType.Grass, true)]
    [InlineData(TerrainType.DeepWater, true)]
    [InlineData(TerrainType.Water, true)]
    [InlineData(TerrainType.River, true)]
    [InlineData(TerrainType.Stream, true)]
    [InlineData(TerrainType.Lake, true)]
    [InlineData(TerrainType.LargeRiver, true)]
    [InlineData(TerrainType.Mountain, false)]
    public void Boats_use_water_and_walkable_shore(TerrainType terrain, bool expected)
    {
        Assert.Equal(expected, WorldEngine.CanTraverse(new Tile { Terrain = terrain }, TravelMode.Boat));
    }

    /// <summary>飞行不受地形阻挡。</summary>
    [Theory]
    [InlineData(TerrainType.DeepWater)]
    [InlineData(TerrainType.Mountain)]
    public void Aircraft_can_cross_inaccessible_terrain(TerrainType terrain)
    {
        Assert.True(WorldEngine.CanTraverse(new Tile { Terrain = terrain }, TravelMode.Aircraft));
    }

    /// <summary>生成用海拔不参与运行时步行。</summary>
    [Theory]
    [InlineData((byte)0)]
    [InlineData(byte.MaxValue)]
    public void Elevation_does_not_change_grass_traversal(byte elevation)
    {
        Assert.True(WorldEngine.CanTraverse(new Tile { Terrain = TerrainType.Grass, Elevation = elevation },
            TravelMode.Foot));
    }
}
