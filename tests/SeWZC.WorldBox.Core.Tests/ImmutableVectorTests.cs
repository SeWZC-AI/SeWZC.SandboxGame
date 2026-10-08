using SeWZC.WorldBox.Core;

namespace SeWZC.WorldBox.Core.Tests;

/// <summary>持久化序列的分支边界、路径更新和输入隔离。</summary>
public sealed class ImmutableVectorTests
{
    private sealed record Item(int Id);

    /// <summary>局部可变更新读取原序列的差异，跨分支冻结后不能改写任何已返回的版本。</summary>
    [Theory]
    [InlineData(9)]
    [InlineData(33)]
    [InlineData(65)]
    [InlineData(513)]
    [InlineData(1025)]
    public void Builder_freezes_independent_versions_across_branch_boundaries(int count)
    {
        var before = ImmutableVector<Item>.CreateRange(Enumerable.Range(0, count).Select(id => new Item(id)))
            .SetItem(0, new Item(-1));
        var builder = new ImmutableVector<Item>.Builder(before);
        Assert.Same(before, builder.Freeze());

        builder.SetItem(1, new Item(-2));
        builder.SetItem(count - 1, new Item(-3));
        var first = builder.Freeze();
        builder.SetItem(0, new Item(-4));
        builder.SetItem(count - 1, new Item(-5));
        var second = builder.Freeze();

        Assert.Equal(-1, before[0].Id);
        Assert.Equal(1, before[1].Id);
        Assert.Equal(count - 1, before[count - 1].Id);
        Assert.Equal(-1, first[0].Id);
        Assert.Equal(-2, first[1].Id);
        Assert.Equal(-3, first[count - 1].Id);
        Assert.Equal(-4, second[0].Id);
        Assert.Equal(-5, second[count - 1].Id);
        Assert.Same(before[2], second[2]);
        Assert.Same(second, builder.Freeze());
    }

    /// <summary>无效的批量写入不污染已保留的有效更新。</summary>
    [Fact]
    public void Builder_rejects_invalid_indices_without_losing_valid_updates()
    {
        ImmutableVector<Item> before = [new(1)];
        var builder = new ImmutableVector<Item>.Builder(before);
        builder.SetItem(0, new Item(2));

        Assert.Throws<ArgumentOutOfRangeException>(() => builder.SetItem(-1, new Item(3)));
        Assert.Throws<ArgumentOutOfRangeException>(() => builder.SetItem(1, new Item(3)));

        Assert.Equal(1, before[0].Id);
        Assert.Equal(2, builder.Freeze()[0].Id);
    }

    /// <summary>整批转换读取尚未合并的差异，并保留输入和未改变的对象。</summary>
    [Theory]
    [InlineData(9)]
    [InlineData(65)]
    [InlineData(513)]
    public void Map_preserves_pending_updates_and_unchanged_values(int count)
    {
        var original = ImmutableVector<Item>.CreateRange(Enumerable.Range(0, count).Select(id => new Item(id)));
        var before = original.SetItem(count - 1, new Item(-1));

        var after = before.Map(item => item.Id < 0 ? new Item(-2) : item);

        Assert.Equal(count - 1, original[count - 1].Id);
        Assert.Equal(-1, before[count - 1].Id);
        Assert.Equal(-2, after[count - 1].Id);
        Assert.Same(before[0], after[0]);
        Assert.Same(before, before.Map(item => item));
    }

    /// <summary>转换中途失败不会修改输入分支或已保留的差异。</summary>
    [Fact]
    public void Failing_map_preserves_the_input()
    {
        var before = ImmutableVector<Item>.CreateRange(Enumerable.Range(0, 9).Select(id => new Item(id)))
            .SetItem(0, new Item(-1));

        Assert.Throws<InvalidOperationException>(() => before.Map(item =>
            item.Id == 8 ? throw new InvalidOperationException() : new Item(100)));

        Assert.Equal<int>([-1, .. Enumerable.Range(1, 8)], before.Select(item => item.Id));
        Assert.Empty(new ImmutableVector<Item>().Map(item => item));
    }

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

    /// <summary>连续替换及切换目标索引不会改变任何中间版本，追加也保留最后的替换。</summary>
    [Theory]
    [InlineData(8)]
    [InlineData(64)]
    [InlineData(512)]
    public void Repeated_updates_preserve_each_intermediate_version(int count)
    {
        var before = ImmutableVector<Item>.CreateRange(Enumerable.Range(0, count).Select(id => new Item(id)));
        var first = before.SetItem(0, new Item(-1));
        var second = first.SetItem(1, new Item(-2));
        var third = second.SetItem(0, new Item(-3));
        var fourth = third.SetItem(count - 1, new Item(-4));
        var appended = fourth.Add(new Item(count));

        Assert.Equal(Enumerable.Range(0, count), before.Select(item => item.Id));
        Assert.Equal(-1, first[0].Id);
        Assert.Equal(count - 1, first[count - 1].Id);
        Assert.Equal(1, first[1].Id);
        Assert.Equal(-1, second[0].Id);
        Assert.Equal(-2, second[1].Id);
        Assert.Equal(count - 1, second[count - 1].Id);
        Assert.Equal(-3, third[0].Id);
        Assert.Equal(-2, third[1].Id);
        Assert.Equal(count - 1, third[count - 1].Id);
        Assert.Equal(-4, fourth[count - 1].Id);
        Assert.Equal(fourth.Select(item => item.Id).Append(count), appended.Select(item => item.Id));
        Assert.Same(fourth, fourth.SetItem(count - 1, fourth[count - 1]));
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
