using System.Collections;
using System.Runtime.CompilerServices;

namespace SeWZC.WorldBox.Core;

/// <summary>按索引保存不可变对象的持久化序列；相邻项更新共享上层树，保留当前叶分支的差异。</summary>
/// <typeparam name="T">不可变对象类型。</typeparam>
[CollectionBuilder(typeof(ImmutableVectorBuilder), nameof(ImmutableVectorBuilder.Create))]
public sealed partial class ImmutableVector<T> : IReadOnlyList<T> where T : class
{
    private const int Bits = 5;
    private const int Width = 1 << Bits;
    private const int Mask = Width - 1;
    private readonly int _changedIndex;
    private readonly object?[]? _changedLeaf;
    private readonly T? _changedValue;
    private readonly int _leafStart;
    private readonly object?[] _root;
    private readonly int _shift;

    /// <summary>建立空序列。</summary>
    public ImmutableVector() : this(new object?[Width], 0, 0) { }

    /// <summary>共享已建立的不可变树，并保存当前版本的叶分支及单项差异。</summary>
    /// <param name="root">不再写入的树节点数组。</param>
    /// <param name="shift">根节点所对应的索引位移。</param>
    /// <param name="count">序列中的对象数量。</param>
    /// <param name="leafStart">差异叶分支的首项索引，无差异时为负一。</param>
    /// <param name="changedLeaf">不再写入的差异叶分支数组。</param>
    /// <param name="changedIndex">差异索引，无差异时为负一。</param>
    /// <param name="changedValue">该版本在差异索引处的对象。</param>
    private ImmutableVector(object?[] root, int shift, int count, int leafStart = -1,
        object?[]? changedLeaf = null, int changedIndex = -1, T? changedValue = null)
    {
        _root = root;
        _shift = shift;
        Count = count;
        _leafStart = leafStart;
        _changedLeaf = changedLeaf;
        _changedIndex = changedIndex;
        _changedValue = changedValue;
    }

    /// <summary>集合中的对象数量。</summary>
    public int Count { get; }

    /// <summary>读取指定索引的对象。</summary>
    /// <param name="index">从零开始的索引。</param>
    public T this[int index]
    {
        get
        {
            if ((uint)index >= (uint)Count)
                throw new ArgumentOutOfRangeException(nameof(index));
            if (index == _changedIndex)
                return _changedValue!;
            var leaf = (index & ~Mask) == _leafStart ? _changedLeaf! : FindLeaf(index);
            return (T)leaf[index & Mask]!;
        }
    }

    /// <inheritdoc />
    public IEnumerator<T> GetEnumerator()
    {
        for (var index = 0; index < Count; index += Width)
        {
            var leaf = index == _leafStart ? _changedLeaf! : FindLeaf(index);
            var length = Math.Min(Width, Count - index);
            for (var slot = 0; slot < length; slot++)
                yield return index + slot == _changedIndex ? _changedValue! : (T)leaf[slot]!;
        }
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }

    private object?[] FindLeaf(int index)
    {
        var node = _root;
        for (var shift = _shift; shift > 0; shift -= Bits)
            node = (object?[])node[(index >> shift) & Mask]!;
        return node;
    }

    /// <summary>复制输入序列，建立不共享可变输入数组的序列。</summary>
    /// <param name="items">不可变对象组成的输入序列。</param>
    public static ImmutableVector<T> CreateRange(IEnumerable<T> items)
    {
        if (items is ImmutableVector<T> vector)
            return vector;
        return Create(items.ToArray());
    }

    internal static ImmutableVector<T> Create(ReadOnlySpan<T> items)
    {
        if (items.IsEmpty)
            return new ImmutableVector<T>();
        var shift = 0;
        while (1L << (shift + Bits) < items.Length)
            shift += Bits;
        return new ImmutableVector<T>(Build(items, shift), shift, items.Length);
    }

    private static object?[] Build(ReadOnlySpan<T> items, int shift)
    {
        var node = new object?[Width];
        if (shift == 0)
        {
            for (var i = 0; i < items.Length; i++)
                node[i] = items[i];
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
        if ((uint)index >= (uint)Count)
            throw new ArgumentOutOfRangeException(nameof(index));
        if (index == _changedIndex)
        {
            return ReferenceEquals(_changedValue, value)
                ? this
                : new ImmutableVector<T>(_root, _shift, Count, _leafStart, _changedLeaf, index, value);
        }

        var start = index & ~Mask;
        var sameLeaf = start == _leafStart;
        var leaf = sameLeaf ? _changedLeaf! : FindLeaf(index);
        if (ReferenceEquals(leaf[index & Mask], value))
            return this;
        // 相邻项只合并叶分支差异，切换叶分支时才复制上层路径；不修改旧数组或串接历史版本。
        var root = sameLeaf ? _root : MergeChange();
        if (sameLeaf)
            leaf = MergeLeafChange();
        return new ImmutableVector<T>(root, _shift, Count, start, leaf, index, value);
    }

    private object?[] MergeLeafChange()
    {
        var leaf = CopyNode(_changedLeaf!);
        leaf[_changedIndex & Mask] = _changedValue;
        return leaf;
    }

    private object?[] MergeChange()
    {
        return _leafStart < 0 ? _root : SetLeaf(_root, _shift, _leafStart, MergeLeafChange());
    }

    private static object?[] SetLeaf(object?[] previous, int shift, int index, object?[] leaf)
    {
        if (shift == 0)
            return leaf;
        var node = CopyNode(previous);
        var slot = (index >> shift) & Mask;
        node[slot] = SetLeaf((object?[])node[slot]!, shift - Bits, index, leaf);
        return node;
    }

    private static object?[] Set(object?[]? previous, int shift, int index, T value)
    {
        var node = previous is null ? new object?[Width] : CopyNode(previous);
        var slot = (index >> shift) & Mask;
        if (shift == 0)
            node[slot] = value;
        else
            node[slot] = Set((object?[]?)node[slot], shift - Bits, index, value);
        return node;
    }

    /// <summary>从当前序列转换整个逻辑阶段，只复制有变化的分支，保留输入快照。</summary>
    /// <param name="transform">仅根据输入值产生新的不可变对象的转换。</param>
    public ImmutableVector<T> Map(Func<T, T> transform)
    {
        var root = MergeChange();
        var mapped = MapNode(root, _shift, Count, transform);
        return ReferenceEquals(root, mapped) ? this : new ImmutableVector<T>(mapped, _shift, Count);
    }

    private static object?[] MapNode(object?[] previous, int shift, int count, Func<T, T> transform)
    {
        object?[]? changed = null;
        var block = 1 << shift;
        for (var slot = 0; slot < Width && slot * block < count; slot++)
        {
            var value = shift == 0
                ? transform((T)previous[slot]!)
                : (object)MapNode((object?[])previous[slot]!, shift - Bits, Math.Min(block, count - slot * block),
                    transform);
            if (ReferenceEquals(previous[slot], value))
                continue;
            changed ??= CopyNode(previous);
            changed[slot] = value;
        }

        return changed ?? previous;
    }

    // 节点容量固定，批量复制引用；调用方在冻结前独占新节点。
    private static object?[] CopyNode(object?[] previous)
    {
        var node = new object?[Width];
        previous.AsSpan().CopyTo(node);
        return node;
    }

    /// <summary>返回在末尾添加对象后的序列。</summary>
    /// <param name="value">要添加的不可变对象。</param>
    public ImmutableVector<T> Add(T value)
    {
        var root = MergeChange();
        var shift = _shift;
        if (Count == 1L << (shift + Bits))
        {
            var grown = new object?[Width];
            grown[0] = root;
            root = grown;
            shift += Bits;
        }

        return new ImmutableVector<T>(Set(root, shift, Count, value), shift, Count + 1);
    }

    /// <summary>返回删除指定索引后、维持其他对象原有顺序的序列。</summary>
    /// <param name="index">要删除的对象索引。</param>
    public ImmutableVector<T> RemoveAt(int index)
    {
        if ((uint)index >= (uint)Count)
            throw new ArgumentOutOfRangeException(nameof(index));
        var items = new T[Count - 1];
        for (var i = 0; i < items.Length; i++)
            items[i] = this[i < index ? i : i + 1];
        return Create(items);
    }

    /// <summary>查找第一个符合条件的对象索引；未找到时返回负一。</summary>
    /// <param name="predicate">对象的筛选条件。</param>
    public int FindIndex(Predicate<T> predicate)
    {
        ArgumentNullException.ThrowIfNull(predicate);
        for (var index = 0; index < Count; index++)
            if (predicate(this[index]))
                return index;
        return -1;
    }

    /// <summary>返回移除所有符合条件对象后的序列，保留输入及剩余对象顺序。</summary>
    /// <param name="predicate">要移除对象的筛选条件。</param>
    public ImmutableVector<T> RemoveAll(Predicate<T> predicate)
    {
        var first = FindIndex(predicate);
        if (first < 0)
            return this;
        var items = new T[Count - 1];
        for (var index = 0; index < first; index++)
            items[index] = this[index];
        var count = first;
        for (var index = first + 1; index < Count; index++)
        {
            var item = this[index];
            if (!predicate(item))
                items[count++] = item;
        }

        return Create(items.AsSpan(0, count));
    }

    /// <summary>返回空序列。</summary>
    public ImmutableVector<T> Clear()
    {
        return Count == 0 ? this : new ImmutableVector<T>();
    }
}
