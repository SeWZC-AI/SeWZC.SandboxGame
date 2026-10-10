using System.Collections;

namespace SeWZC.WorldBox.Core.Runtime;

/// <summary>不可变实体集合的定位索引；索引引用不进入世界状态或存档。</summary>
internal sealed class EntityStore<T> : IReadOnlyList<StateReference<T>>, StateReference<T>.ICollectionOwner
    where T : class
{
    private readonly List<StateReference<T>> _items = [];
    private readonly Func<T, T, bool>? _groupChanged;
    private bool _membershipChanged;
    private T[] _membershipValues = [];
    private ImmutableVector<T> _snapshot;
    private int _updateDepth;
    private ImmutableVector<T>.Builder? _updates;

    /// <summary>从不可变实体集合建立稳定定位索引。</summary>
    /// <param name="snapshot">初始实体集合。</param>
    /// <param name="groupChanged">判定替换是否影响分组；不维护分组时为空。</param>
    public EntityStore(ImmutableVector<T> snapshot, Func<T, T, bool>? groupChanged = null)
    {
        _snapshot = snapshot;
        _groupChanged = groupChanged;
        foreach (var value in snapshot)
        {
            var reference = new StateReference<T>(value);
            reference.Collection = this;
            reference.Position = _items.Count;
            _items.Add(reference);
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

    public StateReference<T> this[int index]
    {
        get => _items[index];
        set
        {
            _items[index].Collection = null;
            _items[index] = value;
            value.Collection = this;
            value.Position = index;
            MembershipRevision++;
            if (_updateDepth > 0)
                MarkMembershipChanged();
            else
                _snapshot = CaptureSnapshot().SetItem(index, value.Value);
        }
    }

    IEnumerator<StateReference<T>> IEnumerable<StateReference<T>>.GetEnumerator()
    {
        return _items.GetEnumerator();
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }

    public List<StateReference<T>>.Enumerator GetEnumerator()
    {
        return _items.GetEnumerator();
    }

    void StateReference<T>.ICollectionOwner.Replace(StateReference<T> reference, T value)
    {
        if (_groupChanged?.Invoke(reference.Value, value) == true)
        {
            GroupRevision++;
        }

        Changed?.Invoke(reference.Position, reference.Value, value);
        reference.Synchronize(value);
        if (_membershipChanged)
            return;
        if (_updateDepth > 0)
            (_updates ??= new ImmutableVector<T>.Builder(_snapshot)).SetItem(reference.Position, value);
        else
            _snapshot = _snapshot.SetItem(reference.Position, value);
    }

    internal UpdateScope BeginUpdates()
    {
        _updateDepth++;
        return new UpdateScope(this);
    }

    internal void FlushUpdates()
    {
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
            _snapshot = rebuilt;
            return;
        }

        if (_updates is null)
            return;
        var snapshot = _updates.Freeze();
        _updates = null;
        _snapshot = snapshot;
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
            var reference = _items[index++];
            if (!ReferenceEquals(reference.Value, value))
            {
                if (_groupChanged?.Invoke(reference.Value, value) == true)
                    GroupRevision++;
                Changed?.Invoke(reference.Position, reference.Value, value);
                reference.Synchronize(value);
            }
        }

        _snapshot = next;
    }

    public void Add(StateReference<T> reference)
    {
        reference.Collection = this;
        reference.Position = Count;
        _items.Add(reference);
        MembershipRevision++;
        if (_updateDepth > 0)
            MarkMembershipChanged();
        else
            _snapshot = CaptureSnapshot().Add(reference.Value);
    }

    public bool Remove(StateReference<T> reference)
    {
        var index = IndexOf(reference);
        if (index < 0)
            return false;
        RemoveAt(index);
        return true;
    }

    public void RemoveAt(int index)
    {
        // 无批次时先冻结已有替换，再移动索引。
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
            _snapshot = CaptureSnapshot().RemoveAt(index);
    }

    private void MarkMembershipChanged()
    {
        // 成员顺序立即生效，原树保持独立；冻结边界按最终顺序一次重建，合并日内增删。
        _membershipChanged = true;
        _updates = null;
    }

    public int RemoveAll(Predicate<StateReference<T>> predicate)
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
            _snapshot = CaptureSnapshot().Clear();
    }

    public int IndexOf(StateReference<T> reference)
    {
        return ReferenceEquals(reference.Collection, this) ? reference.Position : -1;
    }

    public int FindIndex(Predicate<StateReference<T>> predicate)
    {
        return _items.FindIndex(predicate);
    }

    internal readonly struct UpdateScope(EntityStore<T> owner) : IDisposable
    {
        public void Dispose()
        {
            if (--owner._updateDepth == 0)
                owner.FlushUpdates();
        }
    }
}
