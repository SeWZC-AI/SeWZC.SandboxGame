using System.Collections;
using System.Runtime.CompilerServices;

namespace SeWZC.WorldBox.Core;

/// <summary>按索引保存不可变对象的持久化序列；连续替换同一项时共享树并保留单项差异。</summary>
/// <typeparam name="T">不可变对象类型。</typeparam>
[CollectionBuilder(typeof(ImmutableVectorBuilder), nameof(ImmutableVectorBuilder.Create))]
public sealed class ImmutableVector<T> : IReadOnlyList<T> where T : class
{
    private const int Bits = 3;
    private const int Width = 1 << Bits;
    private const int Mask = Width - 1;
    private readonly object?[] _root;
    private readonly int _shift;
    private readonly int _changedIndex;
    private readonly T? _changedValue;

    /// <summary>集合中的对象数量。</summary>
    public int Count { get; }

    /// <summary>建立空序列。</summary>
    public ImmutableVector() : this(new object?[Width], 0, 0) { }

    /// <summary>共享已建立的不可变树，并保存当前版本的单项差异。</summary>
    /// <param name="root">不再写入的树节点数组。</param>
    /// <param name="shift">根节点所对应的索引位移。</param>
    /// <param name="count">序列中的对象数量。</param>
    /// <param name="changedIndex">差异索引，无差异时为负一。</param>
    /// <param name="changedValue">该版本在差异索引处的对象。</param>
    private ImmutableVector(object?[] root, int shift, int count, int changedIndex = -1, T? changedValue = null)
    {
        _root = root;
        _shift = shift;
        Count = count;
        _changedIndex = changedIndex;
        _changedValue = changedValue;
    }

    /// <summary>读取指定索引的对象。</summary>
    /// <param name="index">从零开始的索引。</param>
    public T this[int index]
    {
        get
        {
            if ((uint)index >= (uint)Count) throw new ArgumentOutOfRangeException(nameof(index));
            if (index == _changedIndex) return _changedValue!;
            var node = _root;
            for (var shift = _shift; shift > 0; shift -= Bits)
                node = (object?[])node[(index >> shift) & Mask]!;
            return (T)node[index & Mask]!;
        }
    }

    /// <summary>复制输入序列，建立不共享可变输入数组的序列。</summary>
    /// <param name="items">不可变对象组成的输入序列。</param>
    public static ImmutableVector<T> CreateRange(IEnumerable<T> items)
    {
        if (items is ImmutableVector<T> vector) return vector;
        return Create(items.ToArray());
    }

    internal static ImmutableVector<T> Create(ReadOnlySpan<T> items)
    {
        if (items.IsEmpty) return new();
        var shift = 0;
        while ((1L << (shift + Bits)) < items.Length) shift += Bits;
        return new(Build(items, shift), shift, items.Length);
    }

    private static object?[] Build(ReadOnlySpan<T> items, int shift)
    {
        var node = new object?[Width];
        if (shift == 0)
        {
            for (var i = 0; i < items.Length; i++) node[i] = items[i];
        }
        else
        {
            var length = 1 << shift;
            for (var i = 0; i * length < items.Length; i++)
                node[i] = Build(items.Slice(i * length, Math.Min(length, items.Length - i * length)), shift - Bits);
        }
        return node;
    }

    /// <summary>返回替换指定对象后的序列，保留旧序列。</summary>
    /// <param name="index">对象索引。</param>
    /// <param name="value">替换后的不可变对象。</param>
    public ImmutableVector<T> SetItem(int index, T value)
    {
        if ((uint)index >= (uint)Count) throw new ArgumentOutOfRangeException(nameof(index));
        if (ReferenceEquals(this[index], value)) return this;
        // 差异属于当前不可变版本，既不串成查找链，也不修改任何旧节点。
        var root = index == _changedIndex ? _root : MergeChange();
        return new(root, _shift, Count, index, value);
    }

    private object?[] MergeChange() => _changedIndex < 0 ? _root : Set(_root, _shift, _changedIndex, _changedValue!);

    private static object?[] Set(object?[]? previous, int shift, int index, T value)
    {
        var node = previous is null ? new object?[Width] : previous.AsSpan().ToArray();
        var slot = (index >> shift) & Mask;
        if (shift == 0) node[slot] = value;
        else node[slot] = Set((object?[]?)node[slot], shift - Bits, index, value);
        return node;
    }

    /// <summary>返回在末尾添加对象后的序列。</summary>
    /// <param name="value">要添加的不可变对象。</param>
    public ImmutableVector<T> Add(T value)
    {
        var root = MergeChange();
        var shift = _shift;
        if (Count == (1L << (shift + Bits)))
        {
            var grown = new object?[Width];
            grown[0] = root;
            root = grown;
            shift += Bits;
        }
        return new(Set(root, shift, Count, value), shift, Count + 1);
    }

    /// <summary>返回删除指定索引后、维持其他对象原有顺序的序列。</summary>
    /// <param name="index">要删除的对象索引。</param>
    public ImmutableVector<T> RemoveAt(int index)
    {
        if ((uint)index >= (uint)Count) throw new ArgumentOutOfRangeException(nameof(index));
        var items = new T[Count - 1];
        for (var i = 0; i < items.Length; i++) items[i] = this[i < index ? i : i + 1];
        return Create(items);
    }

    /// <summary>返回空序列。</summary>
    public ImmutableVector<T> Clear() => Count == 0 ? this : new();

    /// <inheritdoc />
    public IEnumerator<T> GetEnumerator()
    {
        for (var index = 0; index < Count; index += Width)
        {
            var leaf = _root;
            for (var shift = _shift; shift > 0; shift -= Bits)
                leaf = (object?[])leaf[(index >> shift) & Mask]!;
            var length = Math.Min(Width, Count - index);
            for (var slot = 0; slot < length; slot++)
                yield return index + slot == _changedIndex ? _changedValue! : (T)leaf[slot]!;
        }
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
