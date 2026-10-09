using SeWZC.WorldBox.Core.Runtime;

namespace SeWZC.WorldBox.Core;

public sealed partial class WorldEngine
{
    private TileQueryCache[] _tileQueries = [];
    private long _tileQueryMembershipRevision = -1;

    private void OnTileChanged(int index, Tile before, Tile after)
    {
        var change = TileChange.Between(before, after);
        if (_tileQueryMembershipRevision == Current.Tiles.MembershipRevision)
            _tileQueries[index].Invalidate(change);
        _territoryCounts.OnTileChanged(index, before, after, change);
    }

    private ref TileQueryCache QueryCache(StateReference<Tile> tile)
    {
        var tiles = Current.Tiles;
        if (_tileQueryMembershipRevision != tiles.MembershipRevision)
        {
            if (_tileQueries.Length != tiles.Count)
                _tileQueries = new TileQueryCache[tiles.Count];
            else
                Array.Clear(_tileQueries);
            _tileQueryMembershipRevision = tiles.MembershipRevision;
        }

        return ref _tileQueries[tile.Position];
    }

    private double PlantHarvestEfficiency(StateReference<Tile> tile, bool wood)
    {
        return ReferenceEquals(tile.Collection, Current.Tiles)
            ? QueryCache(tile).PlantHarvestEfficiency(tile.Value, wood)
            : tile.Value.PlantHarvestEfficiency(wood);
    }

    private double PlantSiteYield(StateReference<Tile> tile, bool wood)
    {
        return ReferenceEquals(tile.Collection, Current.Tiles)
            ? QueryCache(tile).PlantSiteYield(tile.Value, wood)
            : tile.Value.PlantSiteYield(wood, tile.Value.PlantHarvestEfficiency(wood));
    }

    private WildlifeKind EdibleAnimal(StateReference<Tile> tile, bool aquatic = false)
    {
        if (aquatic != IsWaterTerrain(tile.Value.Terrain))
            return WildlifeKind.None;
        return ReferenceEquals(tile.Collection, Current.Tiles)
            ? QueryCache(tile).EdibleAnimal(tile.Value, aquatic)
            : tile.Value.EdibleAnimal(aquatic);
    }

    private double HarvestEfficiency(StateReference<Tile> tile, WildlifeKind kind)
    {
        if (ReferenceEquals(tile.Collection, Current.Tiles))
            return QueryCache(tile).HarvestEfficiency(tile.Value, kind);
        if (kind == WildlifeKind.None)
            return 0;
        var density = Math.Min(1, tile.Value.AnimalPopulation(kind)
                                 / Math.Max(.05, AnimalRules.EnvironmentalCapacity(tile.Value, kind)));
        return density * density;
    }
}
