using SeWZC.WorldBox.Core.Runtime;

namespace SeWZC.WorldBox.Core.Tests;

/// <summary>编辑命令所需的最小地图和一名居民，每个用例创建独立实例。</summary>
internal sealed class WorldFixture
{
    internal WorldFixture()
    {
        Engine = WorldEngine.Create(42, 32, 32, false);
        foreach (var tile in Engine.Tiles)
            tile.Replace(tile.Value.WithTerrain(TerrainType.Grass));
        Engine.SpawnResidents(16, 16, RaceKind.Human, 1);
        Town = Engine.Settlements.Single();
        ResidentId = Engine.Residents.Single().Id;
    }

    internal WorldEngine Engine { get; }
    internal StateReference<Settlement> Town { get; }
    internal int ResidentId { get; }
    internal ResidentCursor Resident => Engine.RequireResident(ResidentId)!;

    internal int AddWell(int x, int y, double naturalWater)
    {
        Engine.Buildings.RemoveAll(building =>
            building.Value.X == x && building.Value.Y == y && building.Value.Kind != BuildingKind.TownCenter);
        var tile = Engine.Tiles[y * Engine.Width + x];
        tile.Replace(tile.Value with
        {
            Terrain = TerrainType.Grass,
            NaturalWaterYield = naturalWater,
            DroughtTicks = 0,
            NationId = Town.Value.NationId,
            ClaimedSettlementId = Town.Value.Id,
        });
        return Engine.GrantFacility(Town.Value.Id, BuildingKind.Well, x, y);
    }
}
