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
        switch (tile.Terrain)
        {
            case TerrainType.Forest: yield return (PlantKind.Trees, cover); yield return (PlantKind.Shrubs, cover * .6); break;
            case TerrainType.Grass: case TerrainType.Hills: case TerrainType.Tundra:
                yield return (PlantKind.Grass, cover); yield return (PlantKind.Shrubs, cover * .35); break;
            case TerrainType.Wetland: yield return (PlantKind.Reeds, cover); yield return (PlantKind.Grass, cover * .5); break;
        }
    }

    public static string Name(PlantKind kind) => kind switch
    { PlantKind.Trees => "乔木", PlantKind.Shrubs => "灌木", PlantKind.Grass => "草本", PlantKind.Reeds => "芦苇", _ => "作物" };
}
