namespace SeWZC.WorldBox.Core;

/// <summary>派生的国家地格计数，以及分别跟踪占地和通行变化的修订号。</summary>
internal sealed class TerritoryCounts
{
    private readonly Dictionary<int, int> _counts = [];
    private Tile[]? _tiles;
    public long Revision { get; private set; }
    public long TraversalRevision { get; private set; }
    public void InvalidateClaims() => Revision++;
    public void InvalidateTraversal() => TraversalRevision++;

    /// <summary>将归属变化通知绑定到地格数组；更换数组时重建计数。</summary>
    /// <remarks>已绑定的地格实例须保留；直接替换数组元素会绕过该地格的通知。</remarks>
    public void Bind(Tile[] tiles)
    {
        if (ReferenceEquals(_tiles, tiles)) return;
        if (_tiles is not null)
            foreach (var tile in _tiles)
                if (ReferenceEquals(tile.TerritoryCounts, this)) tile.TerritoryCounts = null;
        _tiles = tiles; _counts.Clear(); Revision++; TraversalRevision++;
        foreach (var tile in tiles)
        {
            tile.TerritoryCounts = this;
            if (tile.NationId != 0) _counts[tile.NationId] = _counts.GetValueOrDefault(tile.NationId) + 1;
        }
    }

    public int Get(int nationId) => _counts.GetValueOrDefault(nationId);

    public void Change(int previous, int next)
    {
        Revision++;
        if (previous != 0) _counts[previous] = _counts.GetValueOrDefault(previous) - 1;
        if (next != 0) _counts[next] = _counts.GetValueOrDefault(next) + 1;
    }
}
