using SeWZC.WorldBox.Core.Runtime;

namespace SeWZC.WorldBox.Core;

public sealed partial class WorldEngine
{
    // 恢复按十六日累计，各行错峰；固定更新格数会令大地图上的同一片土地恢复得更慢。
    private void RegenerateNaturalResources()
    {
        if (!Current.Rules.ResourceRegeneration || Current.Tick == 0)
            return;
        const int interval = 16;
        var band = (int)((Current.Tick - 1) % interval);
        var first = band * Current.Height / interval * Current.Width;
        var last = (band + 1) * Current.Height / interval * Current.Width;
        for (var index = first; index < last; index++)
        {
            var tile = Current.Tiles[index];
            var before = tile.Value;
            if (!before.IsWalkable || before.FireTicks > 0)
                continue;
            var capacity = NaturalResourceCapacity(before);
            if (before.ResourceAmount >= capacity)
                continue;
            ref readonly var yields = ref TerrainRules.For(before.Terrain);
            var renewal = (yields.FoodYield + yields.WoodYield) * (before.DroughtTicks > 0 ? .2 : 1);
            tile.ResourceAmount = Math.Min(capacity, before.ResourceAmount + renewal * 2 * interval);
        }
    }

    /// <summary>按陆地肥力计算自然资源恢复上限，水域地格返回零。</summary>
    /// <param name="tile">要评估资源容量的地格。</param>
    public static double NaturalResourceCapacity(Tile tile)
    {
        return IsWaterTerrain(tile.Terrain) ? 0 : 50 + tile.Fertility;
    }

    private static double WildlifeHarvestEfficiency(TileCursor tile, WildlifeKind kind)
    {
        // 稀少的动物更难找到，降低采集效率能促使居民在种群耗尽前转向其他来源。
        return tile.HarvestEfficiency(kind);
    }

    private static double WildlifeHarvestAmount(TileCursor tile, WildlifeKind kind, double effort)
    {
        return Math.Min(tile.AnimalPopulation(kind) * .1, Math.Max(0, effort) * WildlifeHarvestEfficiency(tile, kind));
    }

    private static double PlantStock(TileCursor tile, bool wood = false)
    {
        return tile.ResourceAmount * (tile.Improvement == LandImprovement.Farmland && !wood ? 1
            : wood ? tile.Plants.Trees : tile.Plants.Shrubs + tile.Plants.Grass + tile.Plants.Reeds);
    }

    private static double NaturalPlantHarvestEfficiency(TileCursor tile, bool wood = false)
    {
        return tile.PlantHarvestEfficiency(wood);
    }

    /// <summary>扣除可采集的植物生物量，保留未采集的物种，并返回实际采集量。</summary>
    /// <param name="tile">采集植物的地格。</param>
    /// <param name="desired">希望采集的资源数量，实际量受可持续存量限制。</param>
    /// <param name="wood">是否采集木材；关闭时采集食物。</param>
    private static double HarvestPlants(TileCursor tile, double desired, bool wood = false)
    {
        var stock = PlantStock(tile, wood);
        var amount = Math.Min(stock * (tile.Improvement == LandImprovement.Farmland && !wood ? 1 : .1),
            Math.Max(0, desired));
        if (amount <= 0)
            return 0;
        var before = tile.ResourceAmount;
        var remaining = before - amount;
        var plants = tile.Plants;
        if (tile.Improvement != LandImprovement.Farmland)
        {
            // 覆盖比例以共享资源存量为基数；仅减少总存量会连带减少未采集物种。
            for (var species = 0; species < 4; species++)
            {
                var kind = (PlantKind)species;
                var quantity = plants.Get(kind) * before;
                if (wood == (kind == PlantKind.Trees))
                    quantity -= amount * quantity / stock;
                plants = plants.WithCoverage(kind, remaining > 0 ? Math.Max(0, quantity) / remaining : 0);
            }
        }

        tile.Replace(tile.Value with { ResourceAmount = remaining, Plants = plants });

        return amount;
    }

    private static bool WildlifeSiteProductive(TileCursor tile, bool aquatic)
    {
        var kind = EdibleAnimal(tile, aquatic);
        return kind != WildlifeKind.None && WildlifeHarvestEfficiency(tile, kind) >= .25;
    }

    private bool WildlifeGoalProductive(ResidentCursor person)
    {
        var source = person.Agent.Goal.TargetEntityId - 1;
        return source >= 0 && source < Current.Tiles.Count
                           && WildlifeSiteProductive(Current.Tiles[source],
                               person.Agent.Goal.Kind == AgentGoalKind.Fish);
    }
}
