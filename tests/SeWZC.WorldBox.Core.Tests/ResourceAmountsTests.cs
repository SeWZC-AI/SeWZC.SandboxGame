namespace SeWZC.WorldBox.Core.Tests;

/// <summary>不可变费用与库存值之间的转换检查。</summary>
public sealed class ResourceAmountsTests
{
    private static ResourceAmounts Amounts()
    {
        return new ResourceAmounts
        {
            Food = 1,
            Wood = 2,
            Stone = 3,
            Ore = 4,
            Alloy = 5,
            EnergyCells = 6,
            Crystals = 7,
            Coal = 8,
            Oil = 9,
            RareEarth = 10,
            Boats = 11,
            Aircraft = 12,
            Water = 13,
            Tools = 14,
            Medicine = 15,
            Ammunition = 16,
        };
    }

    /// <summary>费用读取对应的资源字段。</summary>
    [Theory]
    [MemberData(nameof(ResourceStockTests.Resources), MemberType = typeof(ResourceStockTests))]
    public void Get_returns_the_selected_amount(ResourceKind kind, double expected)
    {
        Assert.Equal(expected, Amounts().Get(kind));
    }

    /// <summary>库存转换保留费用，替换库存不会修改目录中的金额。</summary>
    [Theory]
    [MemberData(nameof(ResourceStockTests.Resources), MemberType = typeof(ResourceStockTests))]
    public void ToStock_preserves_the_immutable_recipe(ResourceKind kind, double expected)
    {
        var amounts = Amounts();
        var copy = amounts.ToStock();
        Assert.Equal(expected, copy.Get(kind));

        copy = copy.WithAmount(kind, 0);

        Assert.Equal(expected, amounts.Get(kind));
    }

    /// <summary>未知资源不能被当作合法费用读取。</summary>
    [Fact]
    public void Get_rejects_an_unknown_resource()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => Amounts().Get((ResourceKind)(-1)));
    }
}
