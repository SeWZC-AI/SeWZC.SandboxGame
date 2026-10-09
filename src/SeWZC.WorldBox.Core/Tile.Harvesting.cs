namespace SeWZC.WorldBox.Core;

public sealed partial record Tile
{
    internal double PlantStock(bool wood)
    {
        return ResourceAmount * (Improvement == LandImprovement.Farmland && !wood ? 1
            : wood ? Plants.Trees : Plants.Shrubs + Plants.Grass + Plants.Reeds);
    }

    internal double PlantHarvestEfficiency(bool wood)
    {
        if (Improvement == LandImprovement.Farmland && !wood)
            return 1;
        var density = Math.Clamp(PlantStock(wood) / 20, 0, 1);
        return density * density;
    }

    internal double PlantSiteYield(bool wood, double efficiency)
    {
        return wood
            ? ResourceAmount > 0 && WorldEngine.IsForestTerrain(Terrain)
                ? TerrainRules.For(Terrain).WoodYield * efficiency
                : 0
            : ResourceAmount > 0 && IsWalkable
                ? Math.Min(1, TerrainRules.For(Terrain).FoodYield / .7) * Fertility / 100d
                  * (DroughtTicks > 0 ? .15 : 1) * efficiency
                : 0;
    }

    /// <summary>计算植物采集后的地格与实际数量，保留未采集物种的生物量。</summary>
    /// <param name="desired">希望采集的数量，实际量受可持续存量限制。</param>
    /// <param name="wood">是否采集木材；关闭时采集食物。</param>
    internal PlantHarvest HarvestPlants(double desired, bool wood)
    {
        var stock = PlantStock(wood);
        var amount = Math.Min(stock * (Improvement == LandImprovement.Farmland && !wood ? 1 : .1),
            Math.Max(0, desired));
        if (amount <= 0)
            return new PlantHarvest(this, 0);
        var remaining = ResourceAmount - amount;
        var plants = Plants;
        if (Improvement != LandImprovement.Farmland)
        {
            // 覆盖比例以共享资源存量为基数；仅减少总存量会连带减少未采集物种。
            for (var species = 0; species < 4; species++)
            {
                var kind = (PlantKind)species;
                var quantity = plants.Get(kind) * ResourceAmount;
                if (wood == (kind == PlantKind.Trees))
                    quantity -= amount * quantity / stock;
                plants = plants.WithCoverage(kind, remaining > 0 ? Math.Max(0, quantity) / remaining : 0);
            }
        }

        return new PlantHarvest(this with { ResourceAmount = remaining, Plants = plants }, amount);
    }

    /// <summary>一次植物采集产生的新地格和实际收获。</summary>
    /// <param name="Tile">采集后的地格。</param>
    /// <param name="Amount">实际收获数量。</param>
    internal readonly record struct PlantHarvest(Tile Tile, double Amount);
}
