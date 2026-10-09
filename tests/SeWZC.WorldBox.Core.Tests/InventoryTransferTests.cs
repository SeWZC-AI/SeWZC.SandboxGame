namespace SeWZC.WorldBox.Core.Tests;

/// <summary>返仓卸货与补给的纯状态转换检查。</summary>
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

    /// <summary>库存紧张时先保障当日需求，不让首位居民领走其他居民的公共口粮。</summary>
    [Fact]
    public void Provisioning_preserves_shared_daily_supplies()
    {
        var inventory = new ResourceStock();
        var warehouse = new ResourceStock { Food = .12, Water = .06 };
        var first = InventoryTransfer.Provision(inventory, warehouse, 2, Profession.Laborer,
            true, true, 4, 4, .04, .025);
        var second = InventoryTransfer.Provision(inventory, first.Warehouse, 2, Profession.Laborer,
            true, true, 4, 4, .04, .025);

        Assert.Equal(.04, first.Inventory.Food, 10);
        Assert.Equal(.025, first.Inventory.Water, 10);
        Assert.Equal(.04, second.Inventory.Food, 10);
        Assert.Equal(.025, second.Inventory.Water, 10);
        Assert.Equal(warehouse.Food, first.Inventory.Food + second.Inventory.Food + second.Warehouse.Food, 10);
        Assert.Equal(warehouse.Water, first.Inventory.Water + second.Inventory.Water + second.Warehouse.Water, 10);
        Assert.Equal(0, inventory.Food);
        Assert.Equal(.12, warehouse.Food);
    }

    /// <summary>关闭饥渴时只领取相应职业用品，原库存保持独立。</summary>
    [Theory]
    [InlineData(Profession.Engineer, ResourceKind.Tools, .5)]
    [InlineData(Profession.Physician, ResourceKind.Medicine, 2)]
    [InlineData(Profession.Ranger, ResourceKind.Ammunition, 8)]
    public void Provisioning_disables_needs_but_retains_job_supplies(Profession profession, ResourceKind kind, double target)
    {
        var inventory = new ResourceStock { Food = .1, Water = .2 };
        var warehouse = new ResourceStock { Food = 10, Water = 20 }.WithAmount(kind, target * 2);
        var result = InventoryTransfer.Provision(inventory, warehouse, 1, profession,
            false, false, 4, 4, .04, .025);

        Assert.Equal(inventory.Food, result.Inventory.Food);
        Assert.Equal(inventory.Water, result.Inventory.Water);
        Assert.Equal(target, result.Inventory.Get(kind));
        Assert.Equal(target, result.Warehouse.Get(kind));
        Assert.Equal(target * 2, warehouse.Get(kind));
        Assert.Equal(0, inventory.Get(kind));
    }
}
