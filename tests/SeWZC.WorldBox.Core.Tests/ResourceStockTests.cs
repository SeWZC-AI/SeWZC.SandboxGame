using System.Text.Json;
using SeWZC.WorldBox.Core;

namespace SeWZC.WorldBox.Core.Tests;

/// <summary>可变库存的读取、修改和副本隔离检查。</summary>
public sealed class ResourceStockTests
{
    /// <summary>每种资源使用不同金额，检测字段映射错误。</summary>
    public static TheoryData<ResourceKind, double> Resources => new()
    {
        { ResourceKind.Food, 1 }, { ResourceKind.Wood, 2 }, { ResourceKind.Stone, 3 },
        { ResourceKind.Ore, 4 }, { ResourceKind.Alloy, 5 }, { ResourceKind.EnergyCells, 6 },
        { ResourceKind.Crystals, 7 }, { ResourceKind.Coal, 8 }, { ResourceKind.Oil, 9 },
        { ResourceKind.RareEarth, 10 }, { ResourceKind.Boats, 11 }, { ResourceKind.Aircraft, 12 },
        { ResourceKind.Water, 13 }, { ResourceKind.Tools, 14 }, { ResourceKind.Medicine, 15 },
        { ResourceKind.Ammunition, 16 },
    };

    internal static ResourceStock Stock() => new()
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

    /// <summary>读取指定资源对应的字段。</summary>
    [Theory]
    [MemberData(nameof(Resources))]
    public void Get_returns_the_selected_resource(ResourceKind kind, double expected)
    {
        Assert.Equal(expected, Stock().Get(kind));
    }

    /// <summary>替换资源不会修改其他库存。</summary>
    [Theory]
    [MemberData(nameof(Resources))]
    public void WithAmount_changes_only_the_selected_resource(ResourceKind kind, double original)
    {
        var stock = Stock();

        stock = stock.WithAmount(kind, original + .25);

        Assert.Equal(original + .25, stock.Get(kind));
        foreach (var other in ResourceStock.Kinds.Where(value => value != kind))
            Assert.Equal(Stock().Get(other), stock.Get(other));
    }

    /// <summary>新库存独立于旧值，更新不能修改先前的库存。</summary>
    [Theory]
    [MemberData(nameof(Resources))]
    public void Replacing_resources_preserves_the_original_stock(ResourceKind kind, double expected)
    {
        var stock = Stock();
        var copy = stock;
        Assert.Equal(expected, copy.Get(kind));

        copy = copy.WithAmount(kind, 0);

        Assert.Equal(expected, stock.Get(kind));
    }

    /// <summary>未知资源读取拒绝而不返回其他字段。</summary>
    [Theory]
    [InlineData(-1)]
    [InlineData(int.MaxValue)]
    public void Get_rejects_unknown_resources(int kind)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => Stock().Get((ResourceKind)kind));
    }

    /// <summary>未知资源写入不改变库存。</summary>
    [Fact]
    public void WithAmount_rejects_unknown_resources_without_changing_stock()
    {
        var stock = Stock();
        var before = JsonSerializer.Serialize(stock);

        Assert.Throws<ArgumentOutOfRangeException>(() => stock.WithAmount((ResourceKind)(-1), 9));

        Assert.Equal(before, JsonSerializer.Serialize(stock));
    }

    /// <summary>省略的资源字段恢复为零。</summary>
    [Fact]
    public void Missing_saved_resources_default_to_zero()
    {
        var stock = JsonSerializer.Deserialize<ResourceStock>("{\"Food\":2.5}")!;

        Assert.Equal(2.5, stock.Food);
        Assert.All(ResourceStock.Kinds.Where(kind => kind != ResourceKind.Food),
            kind => Assert.Equal(0, stock.Get(kind)));
    }

    /// <summary>空库存不写入冗余零字段。</summary>
    [Fact]
    public void Empty_stock_serializes_as_an_empty_object()
    {
        Assert.Equal("{}", JsonSerializer.Serialize(new ResourceStock()));
    }

    /// <summary>同时缩放各类资源，原库存保留所有原值。</summary>
    [Theory]
    [MemberData(nameof(Resources))]
    public void Scale_transforms_every_resource_without_changing_original(ResourceKind kind, double expected)
    {
        var original = Stock();

        var changed = original.Scale(.5);

        Assert.Equal(expected * .5, changed.Get(kind));
        Assert.Equal(expected, original.Get(kind));
    }
}
