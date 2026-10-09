using SeWZC.WorldBox.Core.Runtime;

namespace SeWZC.WorldBox.Core.Tests;

/// <summary>编辑命令所需的最小地图和一名居民，每个用例创建独立实例。</summary>
internal sealed class WorldFixture
{
    internal WorldFixture()
    {
        Engine = WorldEngine.Create(42, 32, 32, false);
        foreach (var tile in Engine.Current.Tiles)
            tile.Terrain = TerrainType.Grass;
        Engine.SpawnResidents(16, 16, RaceKind.Human, 1);
        Town = Engine.Current.Settlements.Single();
        ResidentId = Engine.Current.Residents.Single().Id;
    }

    internal WorldEngine Engine { get; }
    internal SettlementCursor Town { get; }
    internal int ResidentId { get; }
    internal ResidentCursor Resident => Engine.RequireResident(ResidentId)!;

    internal int AddWell(int x, int y, double naturalWater)
    {
        Engine.Current.Buildings.RemoveAll(building =>
            building.Value.X == x && building.Value.Y == y && building.Value.Kind != BuildingKind.TownCenter);
        var tile = Engine.Current.Tiles[y * Engine.Current.Width + x];
        tile.Replace(tile.Value with
        {
            Terrain = TerrainType.Grass,
            NaturalWaterYield = naturalWater,
            DroughtTicks = 0,
            NationId = Town.NationId,
            ClaimedSettlementId = Town.Id,
        });
        return Engine.GrantFacility(Town.Id, BuildingKind.Well, x, y);
    }
}
