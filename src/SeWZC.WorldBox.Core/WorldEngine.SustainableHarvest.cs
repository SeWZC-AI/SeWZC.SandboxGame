namespace SeWZC.WorldBox.Core;

public sealed partial class WorldEngine
{
    /// <summary>按陆地肥力计算自然资源恢复上限，水域地格返回零。</summary>
    public static double NaturalResourceCapacity(Tile tile)
    {
        return IsWaterTerrain(tile.Terrain) ? 0 : 50 + tile.Fertility;
    }

    private static double WildlifeHarvestEfficiency(Tile tile, WildlifeKind kind)
    {
        if (kind == WildlifeKind.None) return 0;
        var capacity = AnimalRules.EnvironmentalCapacity(tile, kind);
        var density = Math.Min(1, tile.AnimalPopulation(kind) / Math.Max(.05, capacity));
        // 稀少的动物更难找到，降低采集效率能促使居民在种群耗尽前转向其他来源。
        return density * density;
    }

    private static double WildlifeHarvestAmount(Tile tile, WildlifeKind kind, double effort)
    {
        return Math.Min(tile.AnimalPopulation(kind) * .1, Math.Max(0, effort) * WildlifeHarvestEfficiency(tile, kind));
    }

    private static double PlantStock(Tile tile, bool wood = false)
    {
        return tile.ResourceAmount * (tile.Improvement == LandImprovement.Farmland && !wood ? 1
            : wood ? tile.Plants.Trees : tile.Plants.Shrubs + tile.Plants.Grass + tile.Plants.Reeds);
    }

    private static double NaturalPlantHarvestEfficiency(Tile tile, bool wood = false)
    {
        if (tile.Improvement == LandImprovement.Farmland && !wood) return 1;
        var density = Math.Clamp(PlantStock(tile, wood) / 20, 0, 1);
        return density * density;
    }

    /// <summary>扣除可采集的植物生物量，保留未采集的物种，并返回实际采集量。</summary>
    private static double HarvestPlants(Tile tile, double desired, bool wood = false)
    {
        var stock = PlantStock(tile, wood);
        var amount = Math.Min(stock * (tile.Improvement == LandImprovement.Farmland && !wood ? 1 : .1),
            Math.Max(0, desired));
        if (amount <= 0) return 0;
        var before = tile.ResourceAmount;
        tile.ResourceAmount -= amount;
        if (tile.Improvement != LandImprovement.Farmland)
        {
            // 覆盖比例以共享资源存量为基数；仅减少总存量会连带减少未采集物种。
            var plants = tile.Plants;
            for (var species = 0; species < 4; species++)
            {
                var kind = (PlantKind)species;
                var quantity = plants.Get(kind) * before;
                if (wood == (kind == PlantKind.Trees)) quantity -= amount * quantity / stock;
                plants.Set(kind, Math.Max(0, quantity) / tile.ResourceAmount);
            }

            tile.Plants = plants;
        }

        return amount;
    }

    private static bool WildlifeSiteProductive(Tile tile, bool aquatic)
    {
        var kind = EdibleAnimal(tile, aquatic);
        return kind != WildlifeKind.None && WildlifeHarvestEfficiency(tile, kind) >= .25;
    }

    private bool WildlifeGoalProductive(Resident person)
    {
        var source = person.Agent.Goal.TargetEntityId - 1;
        return source >= 0 && source < State.Tiles.Length
                           && WildlifeSiteProductive(State.Tiles[source], person.Agent.Goal.Kind == AgentGoalKind.Fish);
    }
}
