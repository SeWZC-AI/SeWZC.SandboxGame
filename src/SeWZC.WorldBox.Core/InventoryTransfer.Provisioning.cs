namespace SeWZC.WorldBox.Core;

public readonly partial record struct InventoryTransfer
{
    /// <summary>判断仓库是否有居民按当前规则需要领取的粮水或职业用品。</summary>
    /// <param name="warehouse">领取前的聚落库存。</param>
    /// <param name="profession">居民职业。</param>
    /// <param name="hunger">是否领取口粮。</param>
    /// <param name="thirst">是否领取饮水。</param>
    internal static bool HasSupplies(in ResourceStock warehouse, Profession profession, bool hunger, bool thirst)
    {
        return hunger && warehouse.Food > 0 || thirst && warehouse.Water > 0
               || profession == Profession.Engineer && warehouse.Tools > 0
               || profession == Profession.Physician && warehouse.Medicine > 0
               || profession == Profession.Ranger && warehouse.Ammunition > 0;
    }

    /// <summary>按公共保留量和个人储备目标计算补给，返回个人库存与仓库的新值。</summary>
    /// <param name="inventory">领取前的个人库存。</param>
    /// <param name="warehouse">领取前的聚落库存。</param>
    /// <param name="population">共享公共库存的居民人数。</param>
    /// <param name="profession">领取职业用品的居民职业。</param>
    /// <param name="hunger">是否领取口粮。</param>
    /// <param name="thirst">是否领取饮水。</param>
    /// <param name="foodReserve">个人口粮储备目标。</param>
    /// <param name="waterReserve">个人饮水储备目标。</param>
    /// <param name="foodUse">个人每日粮食需求。</param>
    /// <param name="waterUse">个人每日饮水需求。</param>
    internal static InventoryTransfer Provision(in ResourceStock inventory, in ResourceStock warehouse,
        int population, Profession profession, bool hunger, bool thirst,
        double foodReserve, double waterReserve, double foodUse, double waterUse)
    {
        var available = Math.Max(Math.Min(warehouse.Food, foodUse), warehouse.Food - population * .06);
        var food = hunger ? Math.Min(available, Math.Max(0, foodReserve - inventory.Food)) : 0;
        available = Math.Max(Math.Min(warehouse.Water, waterUse), warehouse.Water - population * .03);
        var water = thirst ? Math.Min(available, Math.Max(0, waterReserve - inventory.Water)) : 0;
        var supplied = new InventoryTransfer(inventory with { Food = inventory.Food + food, Water = inventory.Water + water },
            warehouse with { Food = warehouse.Food - food, Water = warehouse.Water - water });
        return profession switch
        {
            Profession.Engineer => TakeSupply(supplied, ResourceKind.Tools, .5),
            Profession.Physician => TakeSupply(supplied, ResourceKind.Medicine, 2),
            Profession.Ranger => TakeSupply(supplied, ResourceKind.Ammunition, 8),
            _ => supplied,
        };
    }

    private static InventoryTransfer TakeSupply(in InventoryTransfer supplied, ResourceKind kind, double target)
    {
        var take = Math.Min(supplied.Warehouse.Get(kind), Math.Max(0, target - supplied.Inventory.Get(kind)));
        return new InventoryTransfer(supplied.Inventory.WithAmount(kind, supplied.Inventory.Get(kind) + take),
            supplied.Warehouse.WithAmount(kind, supplied.Warehouse.Get(kind) - take));
    }
}
