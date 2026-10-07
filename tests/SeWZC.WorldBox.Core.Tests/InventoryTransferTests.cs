using SeWZC.WorldBox.Core;

namespace SeWZC.WorldBox.Core.Tests;

/// <summary>返仓卸货的纯状态转换检查。</summary>
public sealed class InventoryTransferTests
{
    /// <summary>卸货保留粮水和职业用品，各类资源总量保持一致且不修改旧库存。</summary>
    [Theory]
    [InlineData(Profession.Engineer, .5, 0, 0)]
    [InlineData(Profession.Builder, 0, 0, 0)]
    [InlineData(Profession.Physician, 0, 2, 0)]
    [InlineData(Profession.Ranger, 0, 0, 8)]
    public void Unloading_preserves_reserves_and_conserves_each_resource(
        Profession profession, double tools, double medicine, double ammunition)
    {
        var inventory = ResourceStockTests.Stock();
        var warehouse = new ResourceStock { Food = 10, Wood = 20 };

        var result = InventoryTransfer.Unload(inventory, warehouse, 1, .5, profession);

        Assert.Equal(1, result.Inventory.Water);
        Assert.Equal(.5, result.Inventory.Food);
        Assert.Equal(tools, result.Inventory.Tools, 10);
        Assert.Equal(medicine, result.Inventory.Medicine);
        Assert.Equal(ammunition, result.Inventory.Ammunition);
        Assert.Equal(0, result.Inventory.Wood);
        foreach (var kind in ResourceStock.Kinds)
            Assert.Equal(inventory.Get(kind) + warehouse.Get(kind),
                result.Inventory.Get(kind) + result.Warehouse.Get(kind), 10);
        Assert.Equal(ResourceStockTests.Stock(), inventory);
        Assert.Equal(10, warehouse.Food);
    }

    /// <summary>粮水低于保留量时不能从仓库额外领取或产生负卸货。</summary>
    [Fact]
    public void Unloading_does_not_replenish_insufficient_personal_reserves()
    {
        var inventory = new ResourceStock { Food = .1, Water = .2 };
        var warehouse = new ResourceStock { Food = 10, Water = 20 };

        var result = InventoryTransfer.Unload(inventory, warehouse, 1, 1, Profession.Child);

        Assert.Equal(inventory, result.Inventory);
        Assert.Equal(warehouse, result.Warehouse);
    }
}
