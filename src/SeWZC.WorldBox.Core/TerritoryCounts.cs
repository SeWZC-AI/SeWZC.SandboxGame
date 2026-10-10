using SeWZC.WorldBox.Core.Runtime;

namespace SeWZC.WorldBox.Core;

/// <summary>随地格归属变化更新的国家领土计数缓存。</summary>
internal sealed class TerritoryCounts
{
    private readonly Dictionary<int, int> _counts = [];
    private readonly HashSet<int> _owned = [];
    private long _membershipRevision = -1;
    private EntityStore<Tile>? _tiles;
    private long _traversalFloor;
    private long[] _traversalRegions = [];
    private int _traversalWidth, _regionColumns;
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
        if (_traversalWidth == width)
            return;
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
    public void Bind(EntityStore<Tile> tiles)
    {
        if (ReferenceEquals(_tiles, tiles) && _membershipRevision == tiles.MembershipRevision)
            return;
        _tiles = tiles;
        _membershipRevision = tiles.MembershipRevision;
        _counts.Clear();
        _owned.Clear();
        Revision++;
        InvalidateTraversal();
        _wildlifeFloor = ++_wildlifeRevision;
        _traversalWidth = 0;
        for (var index = 0; index < tiles.Count; index++)
        {
            var tile = tiles[index];
            if (tile.Value.NationId != 0)
                _counts[tile.Value.NationId] = _counts.GetValueOrDefault(tile.Value.NationId) + 1;
            if (tile.Value.NationId != 0 || tile.Value.ClaimedSettlementId != 0)
                _owned.Add(index);
        }
    }

    internal void OnTileChanged(int index, Tile before, Tile after, in TileChange change)
    {
        if (change.Ownership)
        {
            if (after.NationId != 0 || after.ClaimedSettlementId != 0)
                _owned.Add(index);
            else
                _owned.Remove(index);
        }

        if (change.Nation)
            Change(before.NationId, after.NationId);
        if (change.Claims)
            InvalidateClaims();
        if (change.Traversal)
        {
            if (_traversalWidth == 0)
                InvalidateTraversal();
            else
            {
                _traversalRegions[index / _traversalWidth / 16 * _regionColumns + index % _traversalWidth / 16] =
                    ++TraversalRevision;
            }
        }

        if (change.Population || change.Habitat)
        {
            var revision = ++_wildlifeRevision;
            if (_traversalWidth == 0)
                _wildlifeFloor = revision;
            else
            {
                _wildlifeRegions[index / _traversalWidth / 16 * _regionColumns + index % _traversalWidth / 16] =
                    revision;
            }
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
