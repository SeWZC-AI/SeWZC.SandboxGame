namespace SeWZC.WorldBox.Core;

public enum PlantKind
{
    Trees,
    Shrubs,
    Grass,
    Reeds,
    Crops,
}

/// <summary>从地格共享资源存量推导的只读植物数量、覆盖率和产物名称。</summary>
public static class PlantResources
{
    /// <summary>枚举各类植物的份额或农田作物，跳过正在燃烧或资源耗尽的地格。</summary>
    public static IEnumerable<(PlantKind Kind, double Cover, double Quantity)> At(Tile tile)
    {
        if (tile.FireTicks > 0 || tile.ResourceAmount <= 0) yield break;
        var cover = Math.Clamp(tile.ResourceAmount / 100, 0, 1) * (tile.DroughtTicks > 0 ? .4 : 1);
        if (tile.Improvement == LandImprovement.Farmland)
        {
            yield return (PlantKind.Crops, cover, tile.ResourceAmount);
            yield break;
        }

        for (var species = 0; species < 4; species++)
        {
            var kind = (PlantKind)species;
            var coverage = tile.Plants.Get(kind) * cover;
            if (tile.Plants.Get(kind) > 0) yield return (kind, coverage, tile.ResourceAmount * tile.Plants.Get(kind));
        }
    }

    public static string Name(PlantKind kind)
    {
        return kind switch
        {
            PlantKind.Trees => "乔木", PlantKind.Shrubs => "灌木", PlantKind.Grass => "草本", PlantKind.Reeds => "芦苇",
            _ => "作物",
        };
    }

    public static string ProductName(PlantKind kind)
    {
        return kind switch
        {
            PlantKind.Trees => "木材", PlantKind.Shrubs => "浆果", PlantKind.Grass => "草籽与嫩叶",
            PlantKind.Reeds => "芦苇嫩芽", _ => "谷物",
        };
    }
}
