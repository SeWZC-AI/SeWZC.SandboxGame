using SeWZC.WorldBox.Core.Runtime;

namespace SeWZC.WorldBox.Core;

public sealed partial class WorldEngine
{
    private VisibleWildlifeCache? _visibleWildlifeCache;

    // 只缓存视野内有可持续种群的地址；个人需求、领地效率和可达性仍在调用时判断。
    private ReadOnlySpan<int> VisibleWildlifeSites(ResidentCursor person)
    {
        _territoryCounts.Bind(Current.Tiles);
        var width = Current.Width;
        var origin = Index(person.X, person.Y);
        var revision = _territoryCounts.VisibleWildlifeRevision(person.X, person.Y, width);
        var cache = _visibleWildlifeCache ??= new VisibleWildlifeCache();
        var slot = cache.Slot(origin, width);
        if (cache.TryGet(slot, origin, revision, out var sites))
            return sites;
        Span<int> candidates = stackalloc int[113];
        var count = 0;
        foreach (var offset in VisibleResourceOffsets)
        {
            if (offset.Distance > 7)
                break;
            var x = person.X + offset.X;
            var y = person.Y + offset.Y;
            if (!InBounds(x, y))
                continue;
            var index = Index(x, y);
            var tile = Current.Tiles[index];
            if (tile.FireTicks > 0)
                continue;
            var aquatic = IsWaterTerrain(tile.Terrain);
            if (!aquatic && offset.Distance > 6)
                continue;
            var kind = EdibleAnimal(tile, aquatic);
            if (kind != WildlifeKind.None && WildlifeHarvestEfficiency(tile, kind) >= .25)
                candidates[count++] = index;
        }

        return cache.Store(slot, origin, revision, candidates[..count]);
    }

    private sealed class VisibleWildlifeCache
    {
        private const int Slots = 2048, Cells = 113;
        private readonly int[] _origins = new int[Slots], _counts = new int[Slots], _sites = new int[Slots * Cells];
        private readonly long[] _revisions = new long[Slots];
        private int _width;

        internal int Slot(int origin, int width)
        {
            if (_width != width)
            {
                Array.Clear(_revisions);
                _width = width;
            }

            return (int)(unchecked((uint)origin * 2654435761u) >> 21);
        }

        internal bool TryGet(int slot, int origin, long revision, out ReadOnlySpan<int> sites)
        {
            sites = _sites.AsSpan(slot * Cells, _counts[slot]);
            return _origins[slot] == origin && _revisions[slot] == revision;
        }

        internal ReadOnlySpan<int> Store(int slot, int origin, long revision, scoped ReadOnlySpan<int> sites)
        {
            _origins[slot] = origin;
            _revisions[slot] = revision;
            _counts[slot] = sites.Length;
            var destination = _sites.AsSpan(slot * Cells, sites.Length);
            sites.CopyTo(destination);
            return destination;
        }
    }
}
