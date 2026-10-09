namespace SeWZC.WorldBox.Core;

/// <summary>居民返仓卸货后的个人库存与聚落仓库。</summary>
/// <param name="Inventory">卸货后保留在居民身上的资源。</param>
/// <param name="Warehouse">接收卸货后的聚落资源。</param>
public readonly partial record struct InventoryTransfer(ResourceStock Inventory, ResourceStock Warehouse)
{
    /// <summary>计算返仓卸货结果，保留旅程粮水和职业用品，不修改输入库存。</summary>
    /// <param name="inventory">居民卸货前的库存。</param>
    /// <param name="warehouse">接收卸货前的仓库。</param>
    /// <param name="waterReserve">居民需要保留的饮水数量。</param>
    /// <param name="foodReserve">居民需要保留的口粮数量。</param>
    /// <param name="profession">居民职业，决定工具、药品和弹药的保留量。</param>
    public static InventoryTransfer Unload(in ResourceStock inventory, in ResourceStock warehouse,
        double waterReserve, double foodReserve, Profession profession)
    {
        var water = Math.Max(0, inventory.Water - waterReserve);
        var food = Math.Max(0, inventory.Food - foodReserve);
        if (water == 0 && food == 0 && !inventory.HasMaterials)
            return new InventoryTransfer(inventory, warehouse);
        var tools = Math.Max(0, inventory.Tools - (profession == Profession.Engineer ? .5 : 0));
        var medicine = Math.Max(0, inventory.Medicine - (profession == Profession.Physician ? 2 : 0));
        var ammunition = Math.Max(0, inventory.Ammunition - (profession == Profession.Ranger ? 8 : 0));
        return new InventoryTransfer(
            new ResourceStock
            {
                Water = inventory.Water - water,
                Food = inventory.Food - food,
                Tools = inventory.Tools - tools,
                Medicine = inventory.Medicine - medicine,
                Ammunition = inventory.Ammunition - ammunition,
            },
            warehouse with
            {
                Water = warehouse.Water + water,
                Food = warehouse.Food + food,
                Wood = warehouse.Wood + inventory.Wood,
                Stone = warehouse.Stone + inventory.Stone,
                Ore = warehouse.Ore + inventory.Ore,
                Alloy = warehouse.Alloy + inventory.Alloy,
                EnergyCells = warehouse.EnergyCells + inventory.EnergyCells,
                Crystals = warehouse.Crystals + inventory.Crystals,
                Coal = warehouse.Coal + inventory.Coal,
                Oil = warehouse.Oil + inventory.Oil,
                RareEarth = warehouse.RareEarth + inventory.RareEarth,
                Boats = warehouse.Boats + inventory.Boats,
                Aircraft = warehouse.Aircraft + inventory.Aircraft,
                Tools = warehouse.Tools + tools,
                Medicine = warehouse.Medicine + medicine,
                Ammunition = warehouse.Ammunition + ammunition,
            });
    }
}
