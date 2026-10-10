using SeWZC.WorldBox.Core.Runtime;

namespace SeWZC.WorldBox.Core;

public sealed partial class WorldEngine
{
    // 恢复按十六日累计，各行错峰；固定更新格数会令大地图上的同一片土地恢复得更慢。
    private void RegenerateNaturalResources()
    {
        if (!Rules.ResourceRegeneration || SimulationTick == 0)
            return;
        const int interval = 16;
        var band = (int)((SimulationTick - 1) % interval);
        var first = band * Height / interval * Width;
        var last = (band + 1) * Height / interval * Width;
        for (var index = first; index < last; index++)
        {
            var tile = Tiles[index];
            var before = tile.Value;
            if (!before.IsWalkable || before.FireTicks > 0)
                continue;
            var capacity = NaturalResourceCapacity(before);
            if (before.ResourceAmount >= capacity)
                continue;
            ref readonly var yields = ref TerrainRules.For(before.Terrain);
            var renewal = (yields.FoodYield + yields.WoodYield) * (before.DroughtTicks > 0 ? .2 : 1);
            tile.Replace(tile.Value.WithResourceAmount(Math.Min(capacity,
                before.ResourceAmount + renewal * 2 * interval / SimulationTime.TicksPerDay)));
        }
    }

    /// <summary>按陆地肥力计算自然资源恢复上限，水域地格返回零。</summary>
    /// <param name="tile">要评估资源容量的地格。</param>
    public static double NaturalResourceCapacity(Tile tile)
    {
        return IsWaterTerrain(tile.Terrain) ? 0 : 50 + tile.Fertility;
    }

    private double WildlifeHarvestEfficiency(StateReference<Tile> tile, WildlifeKind kind)
    {
        // 稀少的动物更难找到，降低采集效率能促使居民在种群耗尽前转向其他来源。
        return HarvestEfficiency(tile, kind);
    }

    private double WildlifeHarvestAmount(StateReference<Tile> tile, WildlifeKind kind, double effort)
    {
        return Math.Min(tile.Value.AnimalPopulation(kind) * .1, Math.Max(0, effort) * WildlifeHarvestEfficiency(tile, kind));
    }

    private double NaturalPlantHarvestEfficiency(StateReference<Tile> tile, bool wood = false)
    {
        return PlantHarvestEfficiency(tile, wood);
    }

    /// <summary>扣除可采集的植物生物量，保留未采集的物种，并返回实际采集量。</summary>
    /// <param name="tile">采集植物的地格。</param>
    /// <param name="desired">希望采集的资源数量，实际量受可持续存量限制。</param>
    /// <param name="wood">是否采集木材；关闭时采集食物。</param>
    private static double HarvestPlants(StateReference<Tile> tile, double desired, bool wood = false)
    {
        var harvest = tile.Value.HarvestPlants(desired, wood);
        tile.Replace(harvest.Tile);
        return harvest.Amount;
    }

    private bool WildlifeSiteProductive(StateReference<Tile> tile, bool aquatic)
    {
        var kind = EdibleAnimal(tile, aquatic);
        return kind != WildlifeKind.None && WildlifeHarvestEfficiency(tile, kind) >= .25;
    }

    private bool WildlifeGoalProductive(StateReference<Resident> person)
    {
        var source = person.Value.Agent.Goal.TargetEntityId - 1;
        return source >= 0 && source < Tiles.Count
                           && WildlifeSiteProductive(Tiles[source],
                               person.Value.Agent.Goal.Kind == AgentGoalKind.Fish);
    }
}
