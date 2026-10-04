namespace SeWZC.WorldBox.Core;

// Runtime tile indexes only. Ownership and traversal changes notify separate
// revisions; loading or replacing the grid rebuilds the ownership index.
internal sealed class TerritoryCounts
{
    private readonly Dictionary<int, int> _counts = [];
    private Tile[]? _tiles;
    public long Revision { get; private set; }
    public long TraversalRevision { get; private set; }
    public void InvalidateClaims() => Revision++;
    public void InvalidateTraversal() => TraversalRevision++;

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
