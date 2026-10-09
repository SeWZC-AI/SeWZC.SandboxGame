using System.Collections;

namespace SeWZC.WorldBox.Core.Runtime;

/// <summary>不可变实体集合的定位索引；索引引用不进入世界状态或存档。</summary>
internal sealed class EntityListCursor<T, TCursor> : IReadOnlyList<TCursor>, StateReference<T>.ICollectionOwner
    where T : class where TCursor : StateReference<T>
{
    private readonly List<TCursor> _items = [];
    private readonly List<StateReference<T>> _pending = [];
    private readonly Action<ImmutableVector<T>> _publish;
    private readonly Func<T, T, bool>? _groupChanged;
    private bool _membershipChanged;
    private T[] _membershipValues = [];
    private ImmutableVector<T> _snapshot;
    private int _updateDepth;
    private ImmutableVector<T>.Builder? _updates;

    /// <summary>从不可变集合建立定位索引，提交时维护调用方指定的分组修订。</summary>
    /// <param name="snapshot">初始实体集合。</param>
    /// <param name="publish">发布冻结后的集合。</param>
    /// <param name="create">为初始实体建立定位引用。</param>
    /// <param name="groupChanged">判断一次替换是否改变分组；不需要分组时为空。</param>
    public EntityListCursor(ImmutableVector<T> snapshot, Action<ImmutableVector<T>> publish, Func<T, TCursor> create,
        Func<T, T, bool>? groupChanged = null)
    {
        _snapshot = snapshot;
        _publish = publish;
        _groupChanged = groupChanged;
        foreach (var value in snapshot)
        {
            var cursor = create(value);
            cursor.Collection = this;
            cursor.Position = _items.Count;
            _items.Add(cursor);
        }
    }

    internal ImmutableVector<T> CaptureSnapshot()
    {
        FlushUpdates();
        return _snapshot;
    }

    internal Action<int, T, T>? Changed { get; set; }

    public int Length => Count;
    internal long MembershipRevision { get; private set; }
    internal long GroupRevision { get; private set; }
    public int Count => _items.Count;

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
            MembershipRevision++;
            if (_updateDepth > 0)
                MarkMembershipChanged();
            else
                Commit(CaptureSnapshot().SetItem(index, value.Value));
        }
    }

    IEnumerator<TCursor> IEnumerable<TCursor>.GetEnumerator()
    {
        return _items.GetEnumerator();
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }

    public List<TCursor>.Enumerator GetEnumerator()
    {
        return _items.GetEnumerator();
    }

    void StateReference<T>.ICollectionOwner.Replace(StateReference<T> cursor, in T value)
    {
        if (_groupChanged?.Invoke(cursor.Value, value) == true)
        {
            GroupRevision++;
        }

        Changed?.Invoke(cursor.Position, cursor.Value, value);
        cursor.Synchronize(value);
        if (_membershipChanged)
            return;
        if (_updateDepth > 0)
            (_updates ??= new ImmutableVector<T>.Builder(_snapshot)).SetItem(cursor.Position, value);
        else
            Commit(_snapshot.SetItem(cursor.Position, value));
    }

    void StateReference<T>.ICollectionOwner.RegisterPending(StateReference<T> cursor)
    {
        if (cursor.PendingCommit)
            return;
        cursor.PendingCommit = true;
        _pending.Add(cursor);
    }

    private void Commit(ImmutableVector<T> snapshot)
    {
        _snapshot = snapshot;
        _publish(snapshot);
    }

    internal UpdateScope BeginUpdates()
    {
        _updateDepth++;
        return new UpdateScope(this);
    }

    internal void FlushUpdates()
    {
        // 冻结前提交的实体也进入当前构建器，不能被较早的批次结果覆盖。
        _updateDepth++;
        try
        {
            for (var index = 0; index < _pending.Count; index++)
            {
                var cursor = _pending[index];
                if (!ReferenceEquals(cursor.Collection, this))
                    continue;
                cursor.PendingCommit = false;
                cursor.FlushPending();
            }

            _pending.Clear();
        }
        finally
        {
            _updateDepth--;
        }

        if (_membershipChanged)
        {
            if (_membershipValues.Length < Count)
                _membershipValues = new T[Count];
            for (var index = 0; index < Count; index++)
                _membershipValues[index] = _items[index].Value;
            var rebuilt = ImmutableVector<T>.Create(_membershipValues.AsSpan(0, Count));
            Array.Clear(_membershipValues, 0, Count);
            _membershipChanged = false;
            _updates = null;
            Commit(rebuilt);
            return;
        }

        if (_updates is null)
            return;
        var snapshot = _updates.Freeze();
        _updates = null;
        Commit(snapshot);
    }

    internal void Transform(Func<T, T> transform)
    {
        var before = CaptureSnapshot();
        var next = before.Map(transform);
        if (ReferenceEquals(next, before))
            return;
        var index = 0;
        foreach (var value in next)
        {
            var cursor = _items[index++];
            if (!ReferenceEquals(cursor.Value, value))
            {
                if (_groupChanged?.Invoke(cursor.Value, value) == true)
                    GroupRevision++;
                Changed?.Invoke(cursor.Position, cursor.Value, value);
                cursor.Synchronize(value);
            }
        }

        Commit(next);
    }

    public void Add(TCursor cursor)
    {
        cursor.FlushPending();
        cursor.Collection = this;
        cursor.Position = Count;
        _items.Add(cursor);
        MembershipRevision++;
        if (_updateDepth > 0)
            MarkMembershipChanged();
        else
            Commit(CaptureSnapshot().Add(cursor.Value));
    }

    public bool Remove(TCursor cursor)
    {
        var index = IndexOf(cursor);
        if (index < 0)
            return false;
        RemoveAt(index);
        return true;
    }

    public void RemoveAt(int index)
    {
        // 无日内批次时，先在旧位置提交其他实体的草稿，再移动索引。
        if (_updateDepth == 0)
            FlushUpdates();
        _items[index].Collection = null;
        _items.RemoveAt(index);
        MembershipRevision++;
        for (var i = index; i < Count; i++)
            _items[i].Position = i;
        if (_updateDepth > 0)
            MarkMembershipChanged();
        else
            Commit(CaptureSnapshot().RemoveAt(index));
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
        for (var i = Count - 1; i >= 0; i--)
            if (predicate(_items[i]))
            {
                RemoveAt(i);
                count++;
            }

        return count;
    }

    public void Clear()
    {
        if (Count > 0)
            MembershipRevision++;
        foreach (var item in _items)
            item.Collection = null;
        _items.Clear();
        if (_updateDepth > 0)
            MarkMembershipChanged();
        else
            Commit(CaptureSnapshot().Clear());
    }

    public int IndexOf(TCursor cursor)
    {
        return ReferenceEquals(cursor.Collection, this) ? cursor.Position : -1;
    }

    public int FindIndex(Predicate<TCursor> predicate)
    {
        return _items.FindIndex(predicate);
    }

    internal readonly struct UpdateScope(EntityListCursor<T, TCursor> owner) : IDisposable
    {
        public void Dispose()
        {
            if (--owner._updateDepth == 0)
                owner.FlushUpdates();
        }
    }
}
