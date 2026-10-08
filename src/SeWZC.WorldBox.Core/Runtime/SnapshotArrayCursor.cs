using System.Collections;
using System.Collections.Immutable;

namespace SeWZC.WorldBox.Core.Runtime;

/// <summary>短不可变数组的定位入口；每次操作立即提交新的数组。</summary>
internal sealed class SnapshotArrayCursor<T>(ImmutableArray<T> snapshot, Action<ImmutableArray<T>> publish)
    : IList<T>, IReadOnlyList<T>
{
    public SnapshotArrayCursor() : this([], _ => { }) { }
    public ImmutableArray<T> Snapshot { get; private set; } = snapshot;
    public int Count => Snapshot.Length;
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

    IEnumerator<T> IEnumerable<T>.GetEnumerator()
    {
        return ((IEnumerable<T>)Snapshot).GetEnumerator();
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return ((IEnumerable<T>)Snapshot).GetEnumerator();
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
        var changed = !next.Equals(Snapshot);
        if (changed)
            Commit(next);
        return changed;
    }

    public void RemoveAt(int index)
    {
        Commit(Snapshot.RemoveAt(index));
    }

    private void Commit(ImmutableArray<T> value)
    {
        Snapshot = value;
        publish(value);
    }

    public void AddRange(IEnumerable<T> items)
    {
        Commit(Snapshot.AddRange(items));
    }

    public ImmutableArray<T>.Enumerator GetEnumerator()
    {
        return Snapshot.GetEnumerator();
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

    public static implicit operator SnapshotArrayCursor<T>(List<T> items)
    {
        return new SnapshotArrayCursor<T>(items.ToImmutableArray(), _ => { });
    }

    public static implicit operator SnapshotArrayCursor<T>(ImmutableArray<T> items)
    {
        return new SnapshotArrayCursor<T>(items, _ => { });
    }

    public static implicit operator ImmutableArray<T>(SnapshotArrayCursor<T> cursor)
    {
        return cursor.Snapshot;
    }
}
