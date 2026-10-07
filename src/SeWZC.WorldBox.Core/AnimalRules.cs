using System.Numerics;

namespace SeWZC.WorldBox.Core;

/// <summary>动物物种目录及生态规则。</summary>
public static class AnimalRules
{
    /// <summary>包含 <c>None</c> 在内的物种编号数量，用于按编号索引数组。</summary>
    public const int SpeciesCount = 32;

    private const AnimalHabitat Green = AnimalHabitat.Green,
        Forest = AnimalHabitat.Forest,
        High = AnimalHabitat.High,
        Cold = AnimalHabitat.Cold,
        Dry = AnimalHabitat.Dry,
        Wet = AnimalHabitat.Wet,
        Fresh = AnimalHabitat.Fresh,
        Marine = AnimalHabitat.Marine;

    /// <summary>除 <c>None</c> 外的全部动物物种。</summary>
    public static readonly WildlifeKind[] Species =
        Enum.GetValues<WildlifeKind>().Where(k => k != WildlifeKind.None).ToArray();

    private static readonly AnimalDefinition[] Definitions =
    [
        new("无", AnimalSize.Small, AnimalDiet.Herbivore, AnimalHabitat.None, 0, 0),
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

    private static readonly WildlifeKind[] EdibleLandAnimals = Species
        .Where(kind => For(kind).Diet == AnimalDiet.Herbivore).ToArray();

    private static readonly WildlifeKind[] EdibleWaterAnimals = EdibleLandAnimals
        .Where(kind => For(kind).Aquatic).ToArray();

    private static readonly WildlifeKind[][] Prey = Enumerable.Range(0, SpeciesCount)
        .Select(i => Species.Where(p => CanPreyOn((WildlifeKind)i, p)).ToArray()).ToArray();

    private static readonly WildlifeKind[][] Predators = Enumerable.Range(0, SpeciesCount)
        .Select(i => Species.Where(p => CanPreyOn(p, (WildlifeKind)i)).ToArray()).ToArray();

    internal static readonly int HerbivoreMask = Species.Where(k => For(k).Diet == AnimalDiet.Herbivore)
        .Aggregate(0, (mask, k) => mask | (1 << (int)k));

    private static readonly int[] HabitatMasks = Enum.GetValues<TerrainType>()
        .Select(t => Species.Where(k => (For(k).Habitats & Habitat(t)) != 0)
            .Aggregate(0, (mask, k) => mask | (1 << (int)k))).ToArray();

    private static readonly int[] PredatorMasks = Predators
        .Select(kinds => kinds.Aggregate(0, (mask, k) => mask | (1 << (int)k))).ToArray();

    private static readonly double[] BodyMasses = Definitions.Select(a => a.BodyMass).ToArray();

    internal static ReadOnlySpan<WildlifeKind> PreyFor(AnimalSize size)
    {
        return PreyFor(size == AnimalSize.Small ? WildlifeKind.Fox :
            size == AnimalSize.Medium ? WildlifeKind.Wolf : WildlifeKind.Bear);
    }

    /// <summary>同体型捕食者共享相同猎物清单，按原物种顺序汇总快照生物量。</summary>
    internal static void FillPreyBiomass(ReadOnlySpan<double> populations, ReadOnlySpan<byte> competitors,
        Span<double> biomass, Span<double> sharedBiomass)
    {
        for (var size = 0; size < 3; size++)
        {
            var mass = 0d;
            var share = 0d;
            foreach (var prey in PreyFor((AnimalSize)size))
            {
                var population = populations[(int)prey];
                if (population == 0) continue;
                var amount = population * BodyMasses[(int)prey];
                mass += amount;
                share += amount / competitors[(int)prey];
            }

            biomass[size] = mass;
            sharedBiomass[size] = share;
        }
    }

    internal static ReadOnlySpan<WildlifeKind> EdibleAnimals(bool aquatic)
    {
        return aquatic ? EdibleWaterAnimals : EdibleLandAnimals;
    }

    internal static ReadOnlySpan<WildlifeKind> PreyFor(WildlifeKind predator)
    {
        return Prey[(int)predator];
    }

    /// <summary>按物种编号查询动物定义。</summary>
    /// <param name="kind">动物物种。</param>
    public static AnimalDefinition For(WildlifeKind kind)
    {
        return Definitions[(int)kind];
    }

    /// <summary>判断食肉物种与食草猎物是否满足相邻体型等级的捕食关系。</summary>
    /// <param name="predator">待判断的食肉物种。</param>
    /// <param name="prey">待判断的猎物物种。</param>
    public static bool CanPreyOn(WildlifeKind predator, WildlifeKind prey)
    {
        return predator != WildlifeKind.None && prey != WildlifeKind.None
                                             && For(predator).Diet == AnimalDiet.Carnivore &&
                                             For(prey).Diet == AnimalDiet.Herbivore
                                             && Math.Abs((int)For(predator).Size - (int)For(prey).Size) <= 1;
    }

    /// <summary>返回地形对应的可组合栖息地类别。</summary>
    /// <param name="terrain">地形类别。</param>
    public static AnimalHabitat Habitat(TerrainType terrain)
    {
        return terrain switch
        {
            TerrainType.Forest or TerrainType.Woodland or TerrainType.Rainforest => Green | Forest,
            TerrainType.Grass or TerrainType.Meadow => Green,
            TerrainType.Floodplain => Green | Wet,
            TerrainType.Wetland => Wet,
            TerrainType.Hills or TerrainType.Mountain or TerrainType.AlpineMeadow => High,
            TerrainType.Snow or TerrainType.Tundra => Cold,
            TerrainType.Sand or TerrainType.Desert or TerrainType.Savanna or TerrainType.Scrub
                or TerrainType.DryFertile => Dry,
            TerrainType.Stream or TerrainType.River or TerrainType.LargeRiver or TerrainType.Lake => Fresh,
            TerrainType.Water or TerrainType.DeepWater => Marine,
            _ => AnimalHabitat.None,
        };
    }

    // 捕食者共享同一种猎物的生物量预算，避免各自重复占用整个种群。
    /// <summary>计算此地可分配同一种猎物的捕食者种类数，至少返回 1。</summary>
    /// <param name="tile">共享猎物的捕食者所在的地格。</param>
    /// <param name="prey">待判断的猎物物种。</param>
    public static int PredatorCompetitors(Tile tile, WildlifeKind prey)
    {
        var count = 0;
        foreach (var predator in Species)
            if (CanPreyOn(predator, prey) && EnvironmentalCapacity(tile, predator) > 0)
                count++;
        return Math.Max(1, count);
    }

    internal static int FillCapacities(Tile tile, Span<double> capacities, Span<byte> competitors)
    {
        capacities.Clear();
        competitors.Fill(1);
        if (tile.FireTicks > 0) return 0;
        var eligible = 0;
        var habitat = Habitat(tile.Terrain);
        var aquatic = WorldEngine.IsWaterTerrain(tile.Terrain);
        var water = aquatic ? 1 : tile.NaturalWaterYield;
        var vegetation = aquatic
            ? 1
            : .2 + .8 * Math.Clamp(
                tile.Plants.Grass + tile.Plants.Shrubs * .8 + tile.Plants.Trees * .55 + tile.Plants.Reeds * .8, 0, 1);
        var food = (.2 + tile.Fertility / 125d) * vegetation;
        var resources = Math.Clamp(tile.ResourceAmount / 100, 0, 1);
        var candidates = HabitatMasks[(int)tile.Terrain];
        while (candidates != 0)
        {
            var kind = (WildlifeKind)BitOperations.TrailingZeroCount((uint)candidates);
            candidates &= candidates - 1;
            var raw = RawCapacity(tile, kind, habitat, aquatic, water, food, resources);
            capacities[(int)kind] = raw;
            if (raw > 0) eligible |= 1 << (int)kind;
        }

        var herbs = Math.Max(1, BitOperations.PopCount((uint)(eligible & HerbivoreMask)));
        var herbivores = HerbivoreMask;
        var predators = eligible & ~HerbivoreMask;
        while (herbivores != 0)
        {
            var species = BitOperations.TrailingZeroCount((uint)herbivores);
            herbivores &= herbivores - 1;
            capacities[species] /= herbs;
            competitors[species] = (byte)Math.Max(1,
                BitOperations.PopCount((uint)(predators & PredatorMasks[species])));
        }

        return eligible;
    }

    /// <summary>计算地形、供水与植被允许的物种容量，食草动物共享植物预算；不含实际猎物限制。</summary>
    /// <param name="tile">要评估环境容量的地格。</param>
    /// <param name="kind">动物物种。</param>
    public static double EnvironmentalCapacity(Tile tile, WildlifeKind kind)
    {
        var raw = RawCapacity(tile, kind);
        if (raw == 0 || For(kind).Diet == AnimalDiet.Carnivore) return raw;
        var competitors = 0;
        foreach (var candidate in Species)
            if (For(candidate).Diet == AnimalDiet.Herbivore && RawCapacity(tile, candidate) > 0)
                competitors++;
        return raw / Math.Max(1, competitors);
    }

    private static double RawCapacity(Tile tile, WildlifeKind kind)
    {
        if (kind == WildlifeKind.None || tile.FireTicks > 0) return 0;
        var animal = For(kind);
        var habitat = Habitat(tile.Terrain);
        if ((animal.Habitats & habitat) == 0) return 0;
        var aquatic = WorldEngine.IsWaterTerrain(tile.Terrain);
        var water = aquatic ? 1 : tile.NaturalWaterYield;
        if (tile.Fertility < animal.MinimumFertility || water < animal.MinimumWater) return 0;
        var vegetation = aquatic
            ? 1
            : .2 + .8 * Math.Clamp(
                tile.Plants.Grass + tile.Plants.Shrubs * .8 + tile.Plants.Trees * .55 + tile.Plants.Reeds * .8, 0, 1);
        var food = (.2 + tile.Fertility / 125d) * vegetation;
        return RawCapacity(tile, kind, habitat, aquatic, water, food, Math.Clamp(tile.ResourceAmount / 100, 0, 1));
    }

    private static double RawCapacity(Tile tile, WildlifeKind kind, AnimalHabitat habitat, bool aquatic, double water,
        double food, double resources)
    {
        if (kind == WildlifeKind.None || tile.FireTicks > 0) return 0;
        var animal = For(kind);
        if ((animal.Habitats & habitat) == 0 || tile.Fertility < animal.MinimumFertility ||
            water < animal.MinimumWater) return 0;
        if (!aquatic) food *= resources * Math.Clamp(water / Math.Max(.004, animal.MinimumWater * 2), .15, 1);
        var capacity = animal.Size == AnimalSize.Small ? 4.2 : animal.Size == AnimalSize.Medium ? 2.1 : .875;
        return capacity * food * (tile.Improvement == LandImprovement.Farmland ? .35 : 1)
               * (tile.SettlementId != 0 ? .1 : 1) * (tile.DroughtTicks > 0 ? .25 : 1);
    }
}
