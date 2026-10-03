namespace SeWZC.WorldBox.Core;

public enum PlantKind { Trees, Shrubs, Grass, Reeds, Crops }

public static class PlantResources
{
    // These describe the existing shared resource stock, not additional harvestable inventory.
    public static IEnumerable<(PlantKind Kind, double Cover)> At(Tile tile)
    {
        if (tile.FireTicks > 0 || tile.ResourceAmount < 1) yield break;
        var cover = Math.Clamp(tile.ResourceAmount / 100, 0, 1) * (tile.DroughtTicks > 0 ? .4 : 1);
        if (tile.Improvement == LandImprovement.Farmland) { yield return (PlantKind.Crops, cover); yield break; }
        for (var species = 0; species < 4; species++)
        {
            var kind = (PlantKind)species; var coverage = tile.Plants.Get(kind) * cover;
            if (coverage > .001) yield return (kind, coverage);
        }
    }

    public static string Name(PlantKind kind) => kind switch
    { PlantKind.Trees => "乔木", PlantKind.Shrubs => "灌木", PlantKind.Grass => "草本", PlantKind.Reeds => "芦苇", _ => "作物" };
}
