using System.Collections;
using System.Collections.Immutable;

namespace SeWZC.WorldBox.Core.Runtime;

/// <summary>不可变值集合的引擎内更新入口；每次操作立即提交新的持久化集合。</summary>
internal sealed class SnapshotListCursor<T>(ImmutableList<T> snapshot, Action<ImmutableList<T>> publish)
    : IList<T>, IReadOnlyList<T>
{
    public SnapshotListCursor() : this([], _ => { }) { }
    public ImmutableList<T> Snapshot { get; private set; } = snapshot;
    public int Count => Snapshot.Count;
    public bool IsReadOnly => false;

    public T this[int index]
    {
        get => Snapshot[index];
        set => Commit(Snapshot.SetItem(index, value));
    }

    public void Add(T item)
    {
        Commit(Snapshot.Add(item));
    }

    public void Clear()
    {
        Commit(Snapshot.Clear());
    }

    public bool Contains(T item)
    {
        return Snapshot.Contains(item);
    }

    public void CopyTo(T[] array, int arrayIndex)
    {
        Snapshot.CopyTo(array, arrayIndex);
    }

    public IEnumerator<T> GetEnumerator()
    {
        return ((IEnumerable<T>)Snapshot).GetEnumerator();
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }

    public int IndexOf(T item)
    {
        return Snapshot.IndexOf(item);
    }

    public void Insert(int index, T item)
    {
        Commit(Snapshot.Insert(index, item));
    }

    public bool Remove(T item)
    {
        var next = Snapshot.Remove(item);
        var changed = !ReferenceEquals(next, Snapshot);
        if (changed)
            Commit(next);
        return changed;
    }

    public void RemoveAt(int index)
    {
        Commit(Snapshot.RemoveAt(index));
    }

    private void Commit(ImmutableList<T> value)
    {
        Snapshot = value;
        publish(value);
    }

    public void AddRange(IEnumerable<T> items)
    {
        Commit(Snapshot.AddRange(items));
    }

    public void RemoveRange(int index, int count)
    {
        Commit(Snapshot.RemoveRange(index, count));
    }

    public int RemoveAll(Predicate<T> predicate)
    {
        var before = Count;
        Commit(Snapshot.RemoveAll(predicate));
        return before - Count;
    }

    public void Sort(Comparison<T> comparison)
    {
        Commit(Snapshot.Sort(comparison));
    }

    public static implicit operator SnapshotListCursor<T>(List<T> items)
    {
        return new SnapshotListCursor<T>(items.ToImmutableList(), _ => { });
    }

    public static implicit operator SnapshotListCursor<T>(ImmutableList<T> items)
    {
        return new SnapshotListCursor<T>(items, _ => { });
    }

    public static implicit operator ImmutableList<T>(SnapshotListCursor<T> cursor)
    {
        return cursor.Snapshot;
    }
}
