using System.Collections;
using System.Collections.Immutable;

namespace SeWZC.WorldBox.Core.Runtime;

/// <summary>不可变实体集合的定位索引；索引引用不进入世界状态或存档。</summary>
internal sealed class EntityListCursor<T, TCursor> : IReadOnlyList<TCursor> where T : class where TCursor : StateCursor<T>
{
    private readonly List<TCursor> _items = [];
    private readonly Dictionary<TCursor, int> _positions = [];
    private readonly Action<ImmutableVector<T>> _publish;
    private readonly Func<T, TCursor> _create;
    public ImmutableVector<T> Snapshot { get; private set; }
    public int Count => _items.Count;
    public int Length => Count;

    public EntityListCursor(ImmutableVector<T> snapshot, Action<ImmutableVector<T>> publish, Func<T, TCursor> create)
    {
        Snapshot = snapshot;
        _publish = publish;
        _create = create;
        foreach (var value in snapshot)
        {
            var cursor = create(value);
            _positions[cursor] = _items.Count;
            _items.Add(cursor);
            Bind(cursor);
        }
    }

    private void Bind(TCursor cursor) => cursor.Bind(value =>
    {
        if (_positions.TryGetValue(cursor, out var index))
            Commit(Snapshot.SetItem(index, value));
    });
    private void Commit(ImmutableVector<T> snapshot) { Snapshot = snapshot; _publish(snapshot); }
    public TCursor this[int index]
    {
        get => _items[index];
        set
        {
            _positions.Remove(_items[index]);
            _items[index] = value;
            _positions[value] = index;
            Bind(value);
            Commit(Snapshot.SetItem(index, value.Value));
        }
    }
    public void Add(TCursor cursor)
    {
        _positions[cursor] = Count;
        _items.Add(cursor);
        Bind(cursor);
        Commit(Snapshot.Add(cursor.Value));
    }
    public bool Remove(TCursor cursor)
    {
        if (!_positions.TryGetValue(cursor, out var index)) return false;
        RemoveAt(index);
        return true;
    }
    public void RemoveAt(int index)
    {
        _positions.Remove(_items[index]);
        _items.RemoveAt(index);
        for (var i = index; i < Count; i++) _positions[_items[i]] = i;
        Commit(Snapshot.RemoveAt(index));
    }
    public int RemoveAll(Predicate<TCursor> predicate)
    {
        var count = 0;
        for (var i = Count - 1; i >= 0; i--) if (predicate(_items[i])) { RemoveAt(i); count++; }
        return count;
    }
    public void Clear() { _items.Clear(); _positions.Clear(); Commit(Snapshot.Clear()); }
    public int IndexOf(TCursor cursor) => _positions.GetValueOrDefault(cursor, -1);
    public int FindIndex(Predicate<TCursor> predicate) => _items.FindIndex(predicate);
    public IEnumerator<TCursor> GetEnumerator() => _items.GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
