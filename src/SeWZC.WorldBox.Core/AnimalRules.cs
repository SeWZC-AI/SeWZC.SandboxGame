namespace SeWZC.WorldBox.Core;

[Flags]
public enum AnimalHabitat { None = 0, Green = 1, Forest = 2, High = 4, Cold = 8, Dry = 16, Wet = 32, Fresh = 64, Marine = 128 }
public readonly record struct AnimalDefinition(string Name, AnimalSize Size, AnimalDiet Diet, AnimalHabitat Habitats, byte MinimumFertility, double MinimumWater)
{
    public double BodyMass => Size == AnimalSize.Small ? 1 : Size == AnimalSize.Medium ? 2 : 4;
    public bool Aquatic => (Habitats & (AnimalHabitat.Fresh | AnimalHabitat.Marine)) != 0;
}
public static class AnimalRules
{
    public const int SpeciesCount = 32;
    public static readonly WildlifeKind[] Species = Enum.GetValues<WildlifeKind>().Where(k => k != WildlifeKind.None).ToArray();
    private const AnimalHabitat Green = AnimalHabitat.Green, Forest = AnimalHabitat.Forest, High = AnimalHabitat.High, Cold = AnimalHabitat.Cold,
        Dry = AnimalHabitat.Dry, Wet = AnimalHabitat.Wet, Fresh = AnimalHabitat.Fresh, Marine = AnimalHabitat.Marine;
    private static readonly AnimalDefinition[] Definitions = [new("无", AnimalSize.Small, AnimalDiet.Herbivore, AnimalHabitat.None, 0, 0),
        new("野兔", AnimalSize.Small, AnimalDiet.Herbivore, Green | High | Cold, 15, 0.001),
        new("鹿", AnimalSize.Medium, AnimalDiet.Herbivore, Green, 25, 0.003),
        new("野猪", AnimalSize.Medium, AnimalDiet.Herbivore, Forest | Wet, 25, 0.004),
        new("山羊", AnimalSize.Medium, AnimalDiet.Herbivore, High | Cold, 15, 0.001),
        new("狼", AnimalSize.Medium, AnimalDiet.Carnivore, Green | High | Cold | Wet, 15, 0.001),
        new("水鸟", AnimalSize.Small, AnimalDiet.Herbivore, Wet | Fresh, 15, 0),
        new("植食小鱼", AnimalSize.Small, AnimalDiet.Herbivore, Fresh | Marine, 10, 0),
        new("狐狸", AnimalSize.Small, AnimalDiet.Carnivore, Green | High | Cold, 15, 0.001),
        new("棕熊", AnimalSize.Large, AnimalDiet.Carnivore, Forest | High | Wet, 35, 0.007),
        new("野牛", AnimalSize.Large, AnimalDiet.Herbivore, Green, 40, 0.007),
        new("牦牛", AnimalSize.Large, AnimalDiet.Herbivore, High | Cold, 30, 0.004),
        new("跳鼠", AnimalSize.Small, AnimalDiet.Herbivore, Dry, 10, 0.0005),
        new("羚羊", AnimalSize.Medium, AnimalDiet.Herbivore, Dry | Green, 20, 0.002),
        new("野骆驼", AnimalSize.Large, AnimalDiet.Herbivore, Dry, 20, 0.003),
        new("耳廓狐", AnimalSize.Small, AnimalDiet.Carnivore, Dry, 10, 0.0005),
        new("胡狼", AnimalSize.Medium, AnimalDiet.Carnivore, Dry | Green, 20, 0.002),
        new("狮子", AnimalSize.Large, AnimalDiet.Carnivore, Dry | Green, 30, 0.004),
        new("水豚", AnimalSize.Medium, AnimalDiet.Herbivore, Wet, 25, 0.006),
        new("河马", AnimalSize.Large, AnimalDiet.Herbivore, Wet, 40, 0.012),
        new("水獭", AnimalSize.Small, AnimalDiet.Carnivore, Wet | Fresh, 15, 0),
        new("鳄鱼", AnimalSize.Large, AnimalDiet.Carnivore, Wet | Fresh, 30, 0),
        new("草鱼", AnimalSize.Medium, AnimalDiet.Herbivore, Fresh, 25, 0),
        new("海牛", AnimalSize.Large, AnimalDiet.Herbivore, Fresh, 40, 0),
        new("掠食小鱼", AnimalSize.Small, AnimalDiet.Carnivore, Fresh | Marine, 10, 0),
        new("鲈鱼", AnimalSize.Medium, AnimalDiet.Carnivore, Fresh | Marine, 20, 0),
        new("鲨鱼", AnimalSize.Large, AnimalDiet.Carnivore, Marine, 30, 0),
        new("海龟", AnimalSize.Medium, AnimalDiet.Herbivore, Marine, 25, 0),
        new("大海牛", AnimalSize.Large, AnimalDiet.Herbivore, Marine, 40, 0),
        new("麝牛", AnimalSize.Large, AnimalDiet.Herbivore, Cold, 25, 0.003),
        new("北极熊", AnimalSize.Large, AnimalDiet.Carnivore, Cold, 25, 0.003),
        new("雪豹", AnimalSize.Medium, AnimalDiet.Carnivore, Cold | High, 15, 0.001),
    ];
    private static readonly WildlifeKind[][] Prey = Enumerable.Range(0, SpeciesCount)
        .Select(i => Species.Where(p => CanPreyOn((WildlifeKind)i, p)).ToArray()).ToArray();
    internal static ReadOnlySpan<WildlifeKind> PreyFor(WildlifeKind predator) => Prey[(int)predator];
    public static AnimalDefinition For(WildlifeKind kind) => Definitions[(int)kind];
    public static bool CanPreyOn(WildlifeKind predator, WildlifeKind prey) => predator != WildlifeKind.None && prey != WildlifeKind.None
        && For(predator).Diet == AnimalDiet.Carnivore && For(prey).Diet == AnimalDiet.Herbivore
        && Math.Abs((int)For(predator).Size - (int)For(prey).Size) <= 1;
    public static AnimalHabitat Habitat(TerrainType terrain) => terrain switch
    {
        TerrainType.Forest or TerrainType.Woodland or TerrainType.Rainforest => Green | Forest,
        TerrainType.Grass or TerrainType.Meadow => Green,
        TerrainType.Floodplain => Green | Wet,
        TerrainType.Wetland => Wet,
        TerrainType.Hills or TerrainType.Mountain or TerrainType.AlpineMeadow => High,
        TerrainType.Snow or TerrainType.Tundra => Cold,
        TerrainType.Sand or TerrainType.Desert or TerrainType.Savanna or TerrainType.Scrub or TerrainType.DryFertile => Dry,
        TerrainType.Stream or TerrainType.River or TerrainType.LargeRiver or TerrainType.Lake => Fresh,
        TerrainType.Water or TerrainType.DeepWater => Marine,
        _ => AnimalHabitat.None
    };
    public static double EnvironmentalCapacity(Tile tile, WildlifeKind kind)
    {
        if (kind == WildlifeKind.None || tile.FireTicks > 0) return 0;
        var animal = For(kind);
        if ((animal.Habitats & Habitat(tile.Terrain)) == 0) return 0;
        var water = WorldEngine.IsWaterTerrain(tile.Terrain) ? 1 : tile.NaturalWaterYield;
        if (tile.Fertility < animal.MinimumFertility || water < animal.MinimumWater) return 0;
        var food = .2 + tile.Fertility / 125d;
        if (!WorldEngine.IsWaterTerrain(tile.Terrain)) food *= Math.Clamp(tile.ResourceAmount / 100, 0, 1) * Math.Clamp(water / Math.Max(.004, animal.MinimumWater * 2), .15, 1);
        var capacity = animal.Size == AnimalSize.Small ? 12 : animal.Size == AnimalSize.Medium ? 6 : 2.5;
        return capacity * food * (tile.Improvement == LandImprovement.Farmland ? .35 : 1)
            * (tile.SettlementId != 0 ? .1 : 1) * (tile.DroughtTicks > 0 ? .25 : 1);
    }
}
