namespace SeWZC.WorldBox.Core;

/// <summary>随地格归属变化更新的国家领土计数缓存。</summary>
internal sealed class TerritoryCounts
{
    private readonly Dictionary<int, int> _counts = [];
    private readonly HashSet<int> _owned = [];
    private Runtime.EntityListCursor<Tile, Runtime.TileCursor>? _tiles;
    private long _membershipRevision = -1;
    private long[] _traversalRegions = [];
    private int _traversalWidth, _regionColumns;
    private long _traversalFloor;
    private long[] _wildlifeRegions = [];
    private long _wildlifeRevision, _wildlifeFloor;
    public long Revision { get; private set; }
    public long TraversalRevision { get; private set; }
    internal IEnumerable<int> OwnedTiles => _owned;

    public void InvalidateClaims()
    {
        Revision++;
    }

    public void InvalidateTraversal()
    {
        TraversalRevision++;
        _traversalFloor = TraversalRevision;
    }

    // 六格视野至多跨四个十六格区域；远方火情不使本地可达性失效。
    internal long VisibleTraversalRevision(int x, int y, int width)
    {
        EnsureRegions(width);
        return VisibleRevision(x, y, width, _traversalRegions, _traversalFloor);
    }

    internal long VisibleWildlifeRevision(int x, int y, int width)
    {
        EnsureRegions(width);
        return VisibleRevision(x, y, width, _wildlifeRegions, _wildlifeFloor);
    }

    private void EnsureRegions(int width)
    {
        if (_traversalWidth == width) return;
        _traversalWidth = width;
        _regionColumns = (width + 15) / 16;
        var count = _regionColumns * ((_tiles!.Count / width + 15) / 16);
        _traversalRegions = new long[count];
        _wildlifeRegions = new long[count];
        _traversalFloor = TraversalRevision;
        _wildlifeFloor = _wildlifeRevision;
    }

    private long VisibleRevision(int x, int y, int width, long[] regions, long floor)
    {
        // 鱼源可在六步岸边的相邻一格；七格视野仍至多跨四个区域。
        var left = Math.Max(0, x - 7) / 16;
        var right = Math.Min(width - 1, x + 7) / 16;
        var top = Math.Max(0, y - 7) / 16;
        var bottom = Math.Min(_tiles!.Count / width - 1, y + 7) / 16;
        return Math.Max(floor, Math.Max(
            Math.Max(regions[top * _regionColumns + left], regions[top * _regionColumns + right]),
            Math.Max(regions[bottom * _regionColumns + left], regions[bottom * _regionColumns + right])));
    }

    /// <summary>将归属变化通知绑定到引擎定位索引；更换索引时重建计数。</summary>
    /// <param name="tiles">要绑定归属变更通知的地格索引。</param>
    public void Bind(Runtime.EntityListCursor<Tile, Runtime.TileCursor> tiles)
    {
        if (ReferenceEquals(_tiles, tiles) && _membershipRevision == tiles.MembershipRevision) return;
        if (_tiles is not null)
            foreach (var tile in _tiles) tile.Changed = null;
        _tiles = tiles;
        _membershipRevision = tiles.MembershipRevision;
        _counts.Clear();
        _owned.Clear();
        Revision++;
        InvalidateTraversal();
        _wildlifeFloor = ++_wildlifeRevision;
        _traversalWidth = 0;
        Action<Runtime.TileCursor, Tile, Tile> changed = OnTileChanged;
        for (var index = 0; index < tiles.Count; index++)
        {
            var tile = tiles[index];
            tile.Changed = changed;
            if (tile.NationId != 0) _counts[tile.NationId] = _counts.GetValueOrDefault(tile.NationId) + 1;
            if (tile.NationId != 0 || tile.ClaimedSettlementId != 0) _owned.Add(index);
        }
    }

    private void OnTileChanged(Runtime.TileCursor cursor, Tile before, Tile after)
    {
        if (!ReferenceEquals(cursor.Collection, _tiles)) return;
        var index = cursor.Position;
        if (before.NationId != after.NationId || before.ClaimedSettlementId != after.ClaimedSettlementId)
        {
            if (after.NationId != 0 || after.ClaimedSettlementId != 0) _owned.Add(index);
            else _owned.Remove(index);
        }
        if (before.NationId != after.NationId) Change(before.NationId, after.NationId);
        if (before.ClaimedSettlementId != after.ClaimedSettlementId || WorldEngine.IsWaterTerrain(before.Terrain) != WorldEngine.IsWaterTerrain(after.Terrain)) InvalidateClaims();
        if (before.Terrain != after.Terrain || before.Improvement != after.Improvement || before.BridgeDirection != after.BridgeDirection || (before.FireTicks > 0) != (after.FireTicks > 0))
        {
            if (_traversalWidth == 0) InvalidateTraversal();
            else _traversalRegions[index / _traversalWidth / 16 * _regionColumns + index % _traversalWidth / 16] = ++TraversalRevision;
        }
        if (before.Wildlife != after.Wildlife || before.WildlifePopulation != after.WildlifePopulation
            || !before.SameOtherWildlife(after) || before.Terrain != after.Terrain
            || before.Fertility != after.Fertility || before.Plants != after.Plants
            || before.NaturalWaterYield != after.NaturalWaterYield || before.Improvement != after.Improvement
            || (before.SettlementId != 0) != (after.SettlementId != 0)
            || (before.DroughtTicks > 0) != (after.DroughtTicks > 0) || (before.FireTicks > 0) != (after.FireTicks > 0)
            || Math.Min(100, before.ResourceAmount) != Math.Min(100, after.ResourceAmount))
        {
            var revision = ++_wildlifeRevision;
            if (_traversalWidth == 0) _wildlifeFloor = revision;
            else _wildlifeRegions[index / _traversalWidth / 16 * _regionColumns + index % _traversalWidth / 16] = revision;
        }
    }

    public int Get(int nationId)
    {
        return _counts.GetValueOrDefault(nationId);
    }

    public void Change(int previous, int next)
    {
        Revision++;
        if (previous != 0)
            _counts[previous] = _counts.GetValueOrDefault(previous) - 1;
        if (next != 0)
            _counts[next] = _counts.GetValueOrDefault(next) + 1;
    }
}
