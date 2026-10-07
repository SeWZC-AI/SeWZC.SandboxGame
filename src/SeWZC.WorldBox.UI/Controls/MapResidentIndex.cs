using SeWZC.WorldBox.Core;

namespace SeWZC.WorldBox.UI.Controls;

/// <summary>从居民快照派生的空间索引，覆盖移动区段经过的分区。</summary>
internal sealed class MapResidentIndex
{
    private const int CellTiles = 16;
    private readonly Dictionary<(int X, int Y), List<Resident>> _cells = [];
    private readonly HashSet<int> _seen = [];
    private ImmutableVector<Resident>? _residents;

    public void Query(ImmutableVector<Resident> residents, int left, int top, int right, int bottom,
        List<Resident> result)
    {
        if (!ReferenceEquals(residents, _residents))
        {
            foreach (var cell in _cells.Values)
                cell.Clear();
            foreach (var resident in residents)
            {
                for (var y = Math.Min(resident.Y, resident.FromY) / CellTiles;
                     y <= Math.Max(resident.Y, resident.FromY) / CellTiles; y++)
                    for (var x = Math.Min(resident.X, resident.FromX) / CellTiles;
                         x <= Math.Max(resident.X, resident.FromX) / CellTiles; x++)
                    {
                        if (!_cells.TryGetValue((x, y), out var cell))
                            _cells[(x, y)] = cell = [];
                        cell.Add(resident);
                    }
            }
            _residents = residents;
        }

        result.Clear();
        _seen.Clear();
        for (var y = top / CellTiles; y <= bottom / CellTiles; y++)
            for (var x = left / CellTiles; x <= right / CellTiles; x++)
                if (_cells.TryGetValue((x, y), out var cell))
                    foreach (var resident in cell)
                        if (Math.Min(resident.X, resident.FromX) <= right &&
                            Math.Max(resident.X, resident.FromX) >= left &&
                            Math.Min(resident.Y, resident.FromY) <= bottom &&
                            Math.Max(resident.Y, resident.FromY) >= top && _seen.Add(resident.Id))
                            result.Add(resident);
    }

    public void Clear()
    {
        _cells.Clear();
        _seen.Clear();
        _residents = null;
    }
}
