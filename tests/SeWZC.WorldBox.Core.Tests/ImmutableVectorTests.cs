using SeWZC.WorldBox.Core;

namespace SeWZC.WorldBox.Core.Tests;

/// <summary>持久化序列的分支边界、路径更新和输入隔离。</summary>
public sealed class ImmutableVectorTests
{
    private sealed record Item(int Id);

    /// <summary>跨越叶和分支边界的读取、枚举及追加保持输入顺序。</summary>
    [Theory]
    [InlineData(0)]
    [InlineData(8)]
    [InlineData(64)]
    [InlineData(512)]
    [InlineData(4096)]
    public void Append_preserves_order_across_tree_boundaries(int count)
    {
        var input = Enumerable.Range(0, count).Select(id => new Item(id)).ToArray();
        var before = ImmutableVector<Item>.CreateRange(input);

        var after = before.Add(new Item(count));

        Assert.Equal(count, before.Count);
        Assert.Equal(count + 1, after.Count);
        Assert.Equal(Enumerable.Range(0, count + 1), after.Select(item => item.Id));
        for (var index = 0; index < count; index++) Assert.Same(input[index], after[index]);
        Assert.Equal(count, after[count].Id);
    }

    /// <summary>路径更新隔离旧序列，并共享其他不可变对象。</summary>
    [Fact]
    public void SetItem_preserves_the_previous_version()
    {
        var before = ImmutableVector<Item>.CreateRange(Enumerable.Range(0, 65).Select(id => new Item(id)));
        var replacement = new Item(999);

        var after = before.SetItem(64, replacement);

        Assert.Equal(64, before[64].Id);
        Assert.Same(replacement, after[64]);
        Assert.Same(before[0], after[0]);
        Assert.Same(before, before.SetItem(0, before[0]));
    }

    /// <summary>输入数组的后续替换不能改变持久化序列。</summary>
    [Fact]
    public void Construction_copies_the_input_buffer()
    {
        Item[] input = [new(1), new(2)];
        var sequence = ImmutableVector<Item>.CreateRange(input);

        input[0] = new Item(99);

        Assert.Equal(1, sequence[0].Id);
    }

    /// <summary>删除保留其他项的顺序，并保持旧序列完整。</summary>
    [Theory]
    [InlineData(0)]
    [InlineData(8)]
    [InlineData(16)]
    public void Removal_preserves_order_and_the_previous_version(int index)
    {
        var before = ImmutableVector<Item>.CreateRange(Enumerable.Range(0, 17).Select(id => new Item(id)));

        var after = before.RemoveAt(index);

        Assert.Equal(17, before.Count);
        Assert.Equal(Enumerable.Range(0, 17).Where(id => id != index), after.Select(item => item.Id));
    }

    /// <summary>越界更新不能创建无效序列或改变原值。</summary>
    [Theory]
    [InlineData(-1)]
    [InlineData(1)]
    public void Invalid_index_is_rejected(int index)
    {
        ImmutableVector<Item> sequence = [new(1)];

        Assert.Throws<ArgumentOutOfRangeException>(() => sequence[index]);
        Assert.Throws<ArgumentOutOfRangeException>(() => sequence.SetItem(index, new Item(2)));
        Assert.Throws<ArgumentOutOfRangeException>(() => sequence.RemoveAt(index));
        Assert.Equal(1, sequence[0].Id);
    }
}
