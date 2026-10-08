using System.Collections;
using System.Collections.Immutable;

namespace SeWZC.WorldBox.Core.Runtime;

/// <summary>不可变实体集合的定位索引；索引引用不进入世界状态或存档。</summary>
internal sealed class EntityListCursor<T, TCursor> : IReadOnlyList<TCursor> where T : class where TCursor : StateCursor<T>
{
    private readonly List<TCursor> _items = [];
    private readonly Action<ImmutableVector<T>> _publish;
    private readonly Func<T, TCursor> _create;
    private ImmutableVector<T> _snapshot;
    private ImmutableVector<T>.Builder? _updates;
    private int _updateDepth;
    public ImmutableVector<T> Snapshot { get { FlushUpdates(); return _snapshot; } }
    public int Count => _items.Count;
    public int Length => Count;
    internal long MembershipRevision { get; private set; }

    public EntityListCursor(ImmutableVector<T> snapshot, Action<ImmutableVector<T>> publish, Func<T, TCursor> create)
    {
        _snapshot = snapshot;
        _publish = publish;
        _create = create;
        foreach (var value in snapshot)
        {
            var cursor = create(value);
            cursor.Collection = this;
            cursor.Position = _items.Count;
            _items.Add(cursor);
            Bind(cursor);
        }
    }

    private void Bind(TCursor cursor) => cursor.Bind(value =>
    {
        if (!ReferenceEquals(cursor.Collection, this)) return;
        if (_updateDepth > 0)
            (_updates ??= new(_snapshot)).SetItem(cursor.Position, value);
        else
            Commit(_snapshot.SetItem(cursor.Position, value));
    });
    private void Commit(ImmutableVector<T> snapshot) { _snapshot = snapshot; _publish(snapshot); }

    internal UpdateScope BeginUpdates()
    {
        _updateDepth++;
        return new(this);
    }

    internal void FlushUpdates()
    {
        if (_updates is null) return;
        var snapshot = _updates.Freeze();
        _updates = null;
        Commit(snapshot);
    }

    internal readonly struct UpdateScope(EntityListCursor<T, TCursor> owner) : IDisposable
    {
        public void Dispose()
        {
            if (--owner._updateDepth == 0) owner.FlushUpdates();
        }
    }
    internal void Transform(Func<T, T> transform)
    {
        var next = Snapshot.Map(transform);
        if (ReferenceEquals(next, Snapshot)) return;
        var index = 0;
        foreach (var value in next)
        {
            var cursor = _items[index++];
            if (!ReferenceEquals(cursor.Value, value)) cursor.Synchronize(value);
        }
        Commit(next);
    }
    public TCursor this[int index]
    {
        get => _items[index];
        set
        {
            _items[index].Collection = null;
            _items[index] = value;
            value.Collection = this;
            value.Position = index;
            Bind(value);
            MembershipRevision++;
            Commit(Snapshot.SetItem(index, value.Value));
        }
    }
    public void Add(TCursor cursor)
    {
        cursor.Collection = this;
        cursor.Position = Count;
        _items.Add(cursor);
        Bind(cursor);
        MembershipRevision++;
        Commit(Snapshot.Add(cursor.Value));
    }
    public bool Remove(TCursor cursor)
    {
        var index = IndexOf(cursor);
        if (index < 0) return false;
        RemoveAt(index);
        return true;
    }
    public void RemoveAt(int index)
    {
        _items[index].Collection = null;
        _items.RemoveAt(index);
        MembershipRevision++;
        for (var i = index; i < Count; i++) _items[i].Position = i;
        Commit(Snapshot.RemoveAt(index));
    }
    public int RemoveAll(Predicate<TCursor> predicate)
    {
        var count = 0;
        for (var i = Count - 1; i >= 0; i--) if (predicate(_items[i])) { RemoveAt(i); count++; }
        return count;
    }
    public void Clear()
    {
        if (Count > 0) MembershipRevision++;
        foreach (var item in _items) item.Collection = null;
        _items.Clear();
        Commit(Snapshot.Clear());
    }
    public int IndexOf(TCursor cursor) => ReferenceEquals(cursor.Collection, this) ? cursor.Position : -1;
    public int FindIndex(Predicate<TCursor> predicate) => _items.FindIndex(predicate);
    public IEnumerator<TCursor> GetEnumerator() => _items.GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
