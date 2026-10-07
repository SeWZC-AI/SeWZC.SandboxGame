using SeWZC.WorldBox.Core;

namespace SeWZC.WorldBox.Core.Tests;

/// <summary>编辑命令所需的最小地图和一名居民，每个用例创建独立实例。</summary>
internal sealed class WorldFixture
{
    internal WorldFixture()
    {
        Engine = WorldEngine.Create(42, 32, 32, false);
        foreach (var tile in Engine.State.Tiles)
            tile.Terrain = TerrainType.Grass;
        Engine.SpawnResidents(16, 16, RaceKind.Human, 1);
        Town = Engine.State.Settlements.Single();
        ResidentId = Engine.State.Residents.Single().Id;
    }

    internal WorldEngine Engine { get; }
    internal Settlement Town { get; }
    internal int ResidentId { get; }
    internal Resident Resident => Engine.GetResident(ResidentId)!;
}
