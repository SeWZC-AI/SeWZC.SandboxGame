namespace SeWZC.WorldBox.Core;

public sealed partial class WorldEngine
{
    public static double NaturalResourceCapacity(Tile tile) => IsWaterTerrain(tile.Terrain) ? 0 : 50 + tile.Fertility;

    private static double WildlifeHarvestEfficiency(Tile tile, WildlifeKind kind)
    {
        if (kind == WildlifeKind.None) return 0;
        var capacity = AnimalRules.EnvironmentalCapacity(tile, kind);
        var density = Math.Min(1, tile.AnimalPopulation(kind) / Math.Max(.05, capacity));
        return density * density;
    }

    private static double WildlifeHarvestAmount(Tile tile, WildlifeKind kind, double effort)
        => Math.Min(tile.AnimalPopulation(kind) * .1, Math.Max(0, effort) * WildlifeHarvestEfficiency(tile, kind));

    private static double PlantStock(Tile tile, bool wood = false)
        => tile.ResourceAmount * (tile.Improvement == LandImprovement.Farmland && !wood ? 1
            : wood ? tile.Plants.Trees : tile.Plants.Shrubs + tile.Plants.Grass + tile.Plants.Reeds);

    private static double NaturalPlantHarvestEfficiency(Tile tile, bool wood = false)
    {
        if (tile.Improvement == LandImprovement.Farmland && !wood) return 1;
        var density = Math.Clamp(PlantStock(tile, wood) / 20, 0, 1);
        return density * density;
    }

    private static double HarvestPlants(Tile tile, double desired, bool wood = false)
    {
        var stock = PlantStock(tile, wood);
        var amount = Math.Min(stock * (tile.Improvement == LandImprovement.Farmland && !wood ? 1 : .1), Math.Max(0, desired));
        if (amount <= 0) return 0;
        var before = tile.ResourceAmount; tile.ResourceAmount -= amount;
        if (tile.Improvement != LandImprovement.Farmland)
        {
            var plants = tile.Plants;
            for (var species = 0; species < 4; species++)
            {
                var kind = (PlantKind)species; var quantity = plants.Get(kind) * before;
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
