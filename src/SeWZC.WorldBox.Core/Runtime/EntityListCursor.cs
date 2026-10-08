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
    private bool _membershipChanged;
    private T[] _membershipValues = [];
    public ImmutableVector<T> Snapshot { get { FlushUpdates(); return _snapshot; } }
    public int Count => _items.Count;
    public int Length => Count;
    internal long MembershipRevision { get; private set; }
    internal long GroupRevision { get; private set; }

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

    private void Bind(TCursor cursor)
    {
        var settlement = typeof(T) == typeof(Resident) ? ((Resident)(object)cursor.Value).SettlementId : 0;
        cursor.Bind(value =>
        {
            if (!ReferenceEquals(cursor.Collection, this)) return;
            if (typeof(T) == typeof(Resident))
            {
                var next = ((Resident)(object)value).SettlementId;
                if (next != settlement) { GroupRevision++; settlement = next; }
            }
            if (_membershipChanged) return;
            if (_updateDepth > 0)
                (_updates ??= new(_snapshot)).SetItem(cursor.Position, value);
            else
                Commit(_snapshot.SetItem(cursor.Position, value));
        });
    }
    private void Commit(ImmutableVector<T> snapshot) { _snapshot = snapshot; _publish(snapshot); }

    internal UpdateScope BeginUpdates()
    {
        _updateDepth++;
        return new(this);
    }

    internal void FlushUpdates()
    {
        // 冻结前提交的实体也进入当前构建器，不能被较早的批次结果覆盖。
        _updateDepth++;
        try
        {
            if (typeof(TCursor) == typeof(ResidentCursor))
                foreach (var person in _items) person.FlushPending();
            if (typeof(TCursor) == typeof(SettlementCursor))
                foreach (var town in _items) ((SettlementCursor)(object)town).FlushResources();
        }
        finally { _updateDepth--; }
        if (_membershipChanged)
        {
            if (_membershipValues.Length < Count) _membershipValues = new T[Count];
            for (var index = 0; index < Count; index++) _membershipValues[index] = _items[index].Value;
            var rebuilt = ImmutableVector<T>.Create(_membershipValues.AsSpan(0, Count));
            Array.Clear(_membershipValues, 0, Count);
            _membershipChanged = false;
            _updates = null;
            Commit(rebuilt);
            return;
        }
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
            if (!ReferenceEquals(cursor.Value, value))
            {
                if (typeof(T) == typeof(Resident)
                    && ((Resident)(object)cursor.Value).SettlementId != ((Resident)(object)value).SettlementId)
                    GroupRevision++;
                cursor.Synchronize(value);
            }
        }
        Commit(next);
    }

    public TCursor this[int index]
    {
        get => _items[index];
        set
        {
            value.FlushPending();
            _items[index].Collection = null;
            _items[index] = value;
            value.Collection = this;
            value.Position = index;
            Bind(value);
            MembershipRevision++;
            if (_updateDepth > 0) MarkMembershipChanged();
            else Commit(Snapshot.SetItem(index, value.Value));
        }
    }
    public void Add(TCursor cursor)
    {
        cursor.FlushPending();
        cursor.Collection = this;
        cursor.Position = Count;
        _items.Add(cursor);
        Bind(cursor);
        MembershipRevision++;
        if (_updateDepth > 0) MarkMembershipChanged();
        else Commit(Snapshot.Add(cursor.Value));
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
        // 无日内批次时，先在旧位置提交其他实体的草稿，再移动索引。
        if (_updateDepth == 0) FlushUpdates();
        _items[index].Collection = null;
        _items.RemoveAt(index);
        MembershipRevision++;
        for (var i = index; i < Count; i++) _items[i].Position = i;
        if (_updateDepth > 0) MarkMembershipChanged();
        else Commit(Snapshot.RemoveAt(index));
    }

    private void MarkMembershipChanged()
    {
        // 成员顺序立即生效，原树保持独立；冻结边界按最终顺序一次重建，避免死亡提前冻结全体身体。
        _membershipChanged = true;
        _updates = null;
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
        if (_updateDepth > 0) MarkMembershipChanged();
        else Commit(Snapshot.Clear());
    }
    public int IndexOf(TCursor cursor) => ReferenceEquals(cursor.Collection, this) ? cursor.Position : -1;
    public int FindIndex(Predicate<TCursor> predicate) => _items.FindIndex(predicate);
    public IEnumerator<TCursor> GetEnumerator() => _items.GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
