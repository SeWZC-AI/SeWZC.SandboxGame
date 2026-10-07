namespace SeWZC.WorldBox.Core;

/// <summary>随地格归属变化更新的国家领土计数缓存。</summary>
internal sealed class TerritoryCounts
{
    private readonly Dictionary<int, int> _counts = [];
    private readonly HashSet<int> _owned = [];
    private Runtime.EntityListCursor<Tile, Runtime.TileCursor>? _tiles;
    private long _membershipRevision = -1;
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
        TraversalRevision++;
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
        if (before.Terrain != after.Terrain || before.Improvement != after.Improvement || before.BridgeDirection != after.BridgeDirection || (before.FireTicks > 0) != (after.FireTicks > 0)) InvalidateTraversal();
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
