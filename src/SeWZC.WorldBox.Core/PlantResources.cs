namespace SeWZC.WorldBox.Core;

public enum PlantKind { Trees, Shrubs, Grass, Reeds, Crops }

public static class PlantResources
{
    // These describe the existing shared resource stock, not additional harvestable inventory.
    public static IEnumerable<(PlantKind Kind, double Cover, double Quantity)> At(Tile tile)
    {
        if (tile.FireTicks > 0 || tile.ResourceAmount <= 0) yield break;
        var cover = Math.Clamp(tile.ResourceAmount / 100, 0, 1) * (tile.DroughtTicks > 0 ? .4 : 1);
        if (tile.Improvement == LandImprovement.Farmland) { yield return (PlantKind.Crops, cover, tile.ResourceAmount); yield break; }
        for (var species = 0; species < 4; species++)
        {
            var kind = (PlantKind)species; var coverage = tile.Plants.Get(kind) * cover;
            if (tile.Plants.Get(kind) > 0) yield return (kind, coverage, tile.ResourceAmount * tile.Plants.Get(kind));
        }
    }

    public static string Name(PlantKind kind) => kind switch
    { PlantKind.Trees => "乔木", PlantKind.Shrubs => "灌木", PlantKind.Grass => "草本", PlantKind.Reeds => "芦苇", _ => "作物" };

    public static string ProductName(PlantKind kind) => kind switch
    { PlantKind.Trees => "木材", PlantKind.Shrubs => "浆果", PlantKind.Grass => "草籽与嫩叶", PlantKind.Reeds => "芦苇嫩芽", _ => "谷物" };
}
