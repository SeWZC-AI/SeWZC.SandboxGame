using System.Collections;
using System.Runtime.CompilerServices;

namespace SeWZC.WorldBox.Core;

/// <summary>按索引保存不可变对象的持久化序列；更新只复制通往目标项的数组路径。</summary>
/// <typeparam name="T">不可变对象类型。</typeparam>
[CollectionBuilder(typeof(ImmutableVectorBuilder), nameof(ImmutableVectorBuilder.Create))]
public sealed class ImmutableVector<T> : IReadOnlyList<T> where T : class
{
    private const int Bits = 3;
    private const int Width = 1 << Bits;
    private const int Mask = Width - 1;
    private readonly object?[] _root;
    private readonly int _shift;

    /// <summary>集合中的对象数量。</summary>
    public int Count { get; }

    /// <summary>建立空序列。</summary>
    public ImmutableVector() : this([], 0, 0) { }

    private ImmutableVector(object?[] root, int shift, int count)
    {
        _root = root;
        _shift = shift;
        Count = count;
    }

    /// <summary>读取指定索引的对象。</summary>
    /// <param name="index">从零开始的索引。</param>
    public T this[int index]
    {
        get
        {
            if ((uint)index >= (uint)Count) throw new ArgumentOutOfRangeException(nameof(index));
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
        return new(Set(_root, _shift, index, value), _shift, Count);
    }

    private static object?[] Set(object?[]? previous, int shift, int index, T value)
    {
        var next = previous is null || previous.Length == 0 ? new object?[Width] : previous.AsSpan().ToArray();
        var slot = (index >> shift) & Mask;
        next[slot] = shift == 0 ? value : Set((object?[]?)next[slot], shift - Bits, index, value);
        return next;
    }

    /// <summary>返回在末尾添加对象后的序列。</summary>
    /// <param name="value">要添加的不可变对象。</param>
    public ImmutableVector<T> Add(T value)
    {
        var root = _root;
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
            for (var slot = 0; slot < length; slot++) yield return (T)leaf[slot]!;
        }
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
