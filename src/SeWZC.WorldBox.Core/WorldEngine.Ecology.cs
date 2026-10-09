using System.Numerics;
using SeWZC.WorldBox.Core.Runtime;

namespace SeWZC.WorldBox.Core;

public sealed partial class WorldEngine
{
    // 每 tick 限制生态复评地格数，避免大地图出现整图更新峰值；完整周期随地图规模增长。
    private const int WildlifeTilesPerTick = 128;
    private double[]? _wildlifeBiomass;
    private double[]? _wildlifeCapacities;
    private double[]? _wildlifeChanges;
    private WildlifeHabitat[]? _wildlifeHabitats;
    private byte[]? _wildlifeHerbivoreKinds;
    private int[]? _wildlifeIncoming;
    private int[]? _wildlifeMasks;
    private double[]? _wildlifePopulations;
    private double[]? _wildlifePredatorLimits;
    private double[]? _wildlifePressure;
    private byte[]? _wildlifePreyCompetitors;
    private double[]? _wildlifeReplacement;
    private double[]? _wildlifeSharedBiomass;

    /// <summary>受每 tick 地格预算限制，完成一轮全部动物复评所需的模拟步数。</summary>
    public int WildlifeCycleTicks => Math.Max(6, (Current.Tiles.Count + WildlifeTilesPerTick - 1) / WildlifeTilesPerTick);

    private static int NextWildlife(ref int mask)
    {
        var kind = BitOperations.TrailingZeroCount((uint)mask);
        mask &= mask - 1;
        return kind;
    }

    /// <summary>返回动物物种的中文名称。</summary>
    /// <param name="kind">动物物种。</param>
    public static string WildlifeName(WildlifeKind kind)
    {
        return AnimalRules.For(kind).Name;
    }

    /// <summary>选择指定体型与食性分组中超过显示阈值的最大种群；没有候选时返回 <c>None</c>。</summary>
    /// <param name="tile">待查看当前种群的地格。</param>
    /// <param name="group">按 <c>size * 2 + diet</c> 编码的体型与食性分组。</param>
    public static WildlifeKind VisibleWildlife(Tile tile, int group)
    {
        var selected = WildlifeKind.None;
        var largest = .02;
        var mask = tile.WildlifeMask;
        while (mask != 0)
        {
            var kind = (WildlifeKind)NextWildlife(ref mask);
            if ((int)AnimalRules.For(kind).Size * 2 + (int)AnimalRules.For(kind).Diet == group
                && tile.AnimalPopulation(kind) > largest)
            {
                selected = kind;
                largest = tile.AnimalPopulation(kind);
            }
        }

        return selected;
    }

    /// <summary>一次选择六个体型与食性分组的代表物种，保持逐组查询的阈值和同量次序。</summary>
    /// <param name="tile">待查看当前种群的地格。</param>
    /// <param name="groups">至少六项的输出缓冲，按 <c>size * 2 + diet</c> 编码保存代表物种。</param>
    public static void FillVisibleWildlife(Tile tile, Span<WildlifeKind> groups)
    {
        if (groups.Length < 6)
            throw new ArgumentException("显示缓冲必须覆盖六个分组。", nameof(groups));
        groups.Clear();
        Span<double> largest = stackalloc double[6];
        largest.Fill(.02);
        var mask = tile.WildlifeMask;
        while (mask != 0)
        {
            var kind = (WildlifeKind)NextWildlife(ref mask);
            var animal = AnimalRules.For(kind);
            var group = (int)animal.Size * 2 + (int)animal.Diet;
            var population = tile.AnimalPopulation(kind);
            if (population <= largest[group])
                continue;
            groups[group] = kind;
            largest[group] = population;
        }
    }

    /// <summary>计算地格当前容量；食肉动物还受可分配的猎物生物量限制。</summary>
    /// <param name="tile">要评估种群容量的地格。</param>
    /// <param name="kind">动物物种。</param>
    public static double WildlifeCapacity(Tile tile, WildlifeKind kind)
    {
        var capacity = AnimalRules.EnvironmentalCapacity(tile, kind);
        if (capacity == 0 || kind == WildlifeKind.None || AnimalRules.For(kind).Diet != AnimalDiet.Carnivore)
            return capacity;
        var prey = 0d;
        foreach (var species in AnimalRules.PreyFor(kind))
            prey += tile.AnimalPopulation(species) * AnimalRules.For(species).BodyMass /
                    AnimalRules.PredatorCompetitors(tile, species);
        return Math.Min(capacity, prey * .12 / AnimalRules.For(kind).BodyMass);
    }

    /// <summary>一次取得全部物种的当前容量，供近景显示复用；不改变世界或消耗随机数。</summary>
    /// <param name="tile">待查询当前容量的地格。</param>
    /// <param name="capacities">覆盖全部物种的输出缓冲，以物种编号作为索引。</param>
    public static void FillWildlifeCapacities(Tile tile, Span<double> capacities)
    {
        if (capacities.Length < AnimalRules.SpeciesCount)
            throw new ArgumentException("容量缓冲必须覆盖全部物种。", nameof(capacities));
        Span<byte> competitors = stackalloc byte[AnimalRules.SpeciesCount];
        var mask = AnimalRules.FillCapacities(tile, capacities, competitors) & ~AnimalRules.HerbivoreMask;
        if (mask == 0)
            return;
        Span<double> populations = stackalloc double[AnimalRules.SpeciesCount];
        tile.CopyAnimalPopulations(populations);
        Span<double> biomass = stackalloc double[3];
        Span<double> sharedBiomass = stackalloc double[3];
        AnimalRules.FillPreyBiomass(populations, competitors, biomass, sharedBiomass);
        while (mask != 0)
        {
            var species = NextWildlife(ref mask);
            var animal = AnimalRules.For((WildlifeKind)species);
            capacities[species] =
                Math.Min(capacities[species], sharedBiomass[(int)animal.Size] * .12 / animal.BodyMass);
        }
    }

    private void SeedWildlife()
    {
        Span<double> capacities = stackalloc double[AnimalRules.SpeciesCount];
        Span<double> populations = stackalloc double[AnimalRules.SpeciesCount];
        Span<byte> competitors = stackalloc byte[AnimalRules.SpeciesCount];
        for (var i = 0; i < Current.Tiles.Count; i++)
        {
            var tile = Current.Tiles[i];
            populations.Clear();
            var primary = WildlifeKind.None;
            AnimalRules.FillCapacities(tile, capacities, competitors);
            for (var diet = 0; diet < 2; diet++)
                foreach (var kind in AnimalRules.Species)
                {
                    if ((int)AnimalRules.For(kind).Diet != diet)
                        continue;
                    var hash = unchecked((uint)i * 2654435761u + (uint)Current.Seed * 31 + (uint)kind * 2246822519u);
                    var capacity = capacities[(int)kind];
                    if (diet == 1 && capacity > 0)
                    {
                        var biomass = 0d;
                        foreach (var prey in AnimalRules.PreyFor(kind))
                            biomass += populations[(int)prey] * AnimalRules.For(prey).BodyMass /
                                       competitors[(int)prey];
                        capacity = Math.Min(capacity, biomass * .12 / AnimalRules.For(kind).BodyMass);
                    }

                    if (capacity > 0)
                    {
                        if (primary == WildlifeKind.None) primary = kind;
                        populations[(int)kind] = capacity * (.15 + hash % 30 / 100d);
                    }
                }
            // 捕食仍读取本格按原顺序生成的猎物数量，全部物种完成后只提交一次地格。
            tile.Replace(tile.Value.WithAnimalPopulations(primary, populations));
        }
    }

    /// <summary>基于同一份种群快照，处理当日地格分区的繁殖、捕食和迁移。</summary>
    private void TickWildlife()
    {
        var tiles = Current.Tiles;
        var bufferTiles = WildlifeTilesPerTick + Current.Width * 2;
        _wildlifeChanges ??= new double[bufferTiles * AnimalRules.SpeciesCount];
        _wildlifePopulations ??= new double[bufferTiles * AnimalRules.SpeciesCount];
        _wildlifePressure ??= new double[bufferTiles];
        _wildlifeIncoming ??= new int[bufferTiles];
        _wildlifeMasks ??= new int[bufferTiles];
        _wildlifeHabitats ??= new WildlifeHabitat[bufferTiles];
        _wildlifeCapacities ??= new double[bufferTiles * AnimalRules.SpeciesCount];
        _wildlifePreyCompetitors ??= new byte[bufferTiles * AnimalRules.SpeciesCount];
        _wildlifeReplacement ??= new double[bufferTiles * AnimalRules.SpeciesCount];
        _wildlifePredatorLimits ??= new double[bufferTiles * AnimalRules.SpeciesCount];
        _wildlifeHerbivoreKinds ??= new byte[bufferTiles];
        _wildlifeBiomass ??= new double[bufferTiles * 3];
        _wildlifeSharedBiomass ??= new double[bufferTiles * 3];
        var cycle = WildlifeCycleTicks;
        // 从存档中的模拟时间推导分区，载入后即可接续复评顺序，无需另存游标。
        var phase = (int)((Current.Tick - 1) % cycle);
        var first = phase * tiles.Length / cycle;
        var last = (phase + 1) * tiles.Length / cycle;
        var snapshotFirst = Math.Max(0, first - Current.Width);
        var snapshotLast = Math.Min(tiles.Length, last + Current.Width);
        var snapshotCount = snapshotLast - snapshotFirst;
        Array.Clear(_wildlifeChanges, 0, snapshotCount * AnimalRules.SpeciesCount);
        Array.Clear(_wildlifeIncoming, 0, snapshotCount);
        // 按复评间隔折算生态速率，并限制单次损失。
        var elapsed = cycle / (SimulationTime.TicksPerYear / 20d);
        var growthRate = Math.Min(.25, .018 * elapsed);
        var deathRate = Math.Min(.65, 1 - Math.Pow(.88, elapsed));
        // 长复评周期下繁殖量有上限，捕食也须使用同一有效间隔，避免捕食增长超过可恢复供给。
        var predationRate = .035 * (growthRate / .018);
        var migrationRate = Math.Min(.12, .02 * elapsed);
        Span<double> capacityBiomass = stackalloc double[3];
        Span<double> capacitySharedBiomass = stackalloc double[3];
        // 仅保存当前分区及四邻边界的快照，使本日增长、消费和迁移使用同一初始种群。
        for (var i = snapshotFirst; i < snapshotLast; i++)
        {
            var tile = tiles[i];
            var local = i - snapshotFirst;
            var mask = tile.WildlifeMask;
            var pressure = 0d;
            var offset = local * AnimalRules.SpeciesCount;
            // 按地格索引复用边界地格的环境容量缓存。
            var habitatSlot = i % bufferTiles;
            var capacityOffset = habitatSlot * AnimalRules.SpeciesCount;
            tile.CopyAnimalPopulations(_wildlifePopulations.AsSpan(offset, AnimalRules.SpeciesCount));
            _wildlifeMasks[local] = mask;
            // 食物供给饱和后，更多资源不应使环境容量缓存反复失效。
            var habitat = new WildlifeHabitat(tile.Terrain, Math.Clamp(tile.ResourceAmount / 100, 0, 1), tile.Fertility,
                tile.Improvement,
                tile.SettlementId != 0, tile.DroughtTicks > 0, tile.FireTicks > 0, tile.NaturalWaterYield, tile.Plants,
                true);
            if (_wildlifeHabitats[habitatSlot] != habitat)
            {
                _wildlifeHabitats[habitatSlot] = habitat;
                Array.Clear(_wildlifeReplacement, capacityOffset, AnimalRules.SpeciesCount);
                Array.Clear(_wildlifePredatorLimits, capacityOffset, AnimalRules.SpeciesCount);
                var eligible = AnimalRules.FillCapacities(tile,
                    _wildlifeCapacities.AsSpan(capacityOffset, AnimalRules.SpeciesCount),
                    _wildlifePreyCompetitors.AsSpan(capacityOffset, AnimalRules.SpeciesCount));
                _wildlifeHerbivoreKinds[habitatSlot] =
                    (byte)BitOperations.PopCount((uint)(eligible & AnimalRules.HerbivoreMask));
                AnimalRules.FillPreyBiomass(_wildlifeCapacities.AsSpan(capacityOffset, AnimalRules.SpeciesCount),
                    _wildlifePreyCompetitors.AsSpan(capacityOffset, AnimalRules.SpeciesCount),
                    capacityBiomass, capacitySharedBiomass);
                // 食草动物在容量处的恢复量须覆盖同一食物网推导的可持续捕食，避免稳定环境仍持续衰减。
                var predators = eligible & ~AnimalRules.HerbivoreMask;
                while (predators != 0)
                {
                    var predator = (WildlifeKind)NextWildlife(ref predators);
                    var definition = AnimalRules.For(predator);
                    var biomass = capacityBiomass[(int)definition.Size];
                    var share = capacitySharedBiomass[(int)definition.Size];

                    if (biomass == 0)
                        continue;
                    var capacity = Math.Min(_wildlifeCapacities[capacityOffset + (int)predator],
                        share * .12 / definition.BodyMass);
                    var rate = capacity * definition.BodyMass * predationRate / biomass;
                    _wildlifeReplacement[capacityOffset + (int)predator] = rate;
                    _wildlifePredatorLimits[capacityOffset + (int)predator] = capacity;
                }
            }

            // 所有源格及邻格使用同一快照；同体型捕食者复用猎物汇总，迁移时不再重扫。
            var biomassSnapshot = _wildlifeBiomass.AsSpan(local * 3, 3);
            var sharedBiomassSnapshot = _wildlifeSharedBiomass.AsSpan(local * 3, 3);
            if ((mask & AnimalRules.HerbivoreMask) == 0)
            {
                biomassSnapshot.Clear();
                sharedBiomassSnapshot.Clear();
            }
            else
            {
                AnimalRules.FillPreyBiomass(_wildlifePopulations.AsSpan(offset, AnimalRules.SpeciesCount),
                    _wildlifePreyCompetitors.AsSpan(capacityOffset, AnimalRules.SpeciesCount),
                    biomassSnapshot, sharedBiomassSnapshot);
            }

            var herbivores = mask & AnimalRules.HerbivoreMask;
            while (herbivores != 0)
            {
                var species = NextWildlife(ref herbivores);
                var capacity = _wildlifeCapacities[capacityOffset + species];
                if (capacity > 0)
                    pressure += _wildlifePopulations[offset + species] / capacity;
            }

            _wildlifePressure[local] = pressure / Math.Max(1, (int)_wildlifeHerbivoreKinds[habitatSlot]);
        }

        Span<int> neighbours = stackalloc int[4];
        Span<double> preyLosses = stackalloc double[AnimalRules.SpeciesCount];
        Span<double> preyRenewal = stackalloc double[AnimalRules.SpeciesCount];
        Span<double> predatorCapacities = stackalloc double[AnimalRules.SpeciesCount];
        Span<double> predatorSurvivors = stackalloc double[AnimalRules.SpeciesCount];
        for (var i = first; i < last; i++)
        {
            var x = i % Current.Width;
            var y = i / Current.Width;
            var local = i - snapshotFirst;
            var offset = local * AnimalRules.SpeciesCount;
            var mask = _wildlifeMasks[local];
            var capacityOffset = i % bufferTiles * AnimalRules.SpeciesCount;
            if (mask == 0)
                continue;
            preyLosses.Clear();
            preyRenewal.Clear();
            var count = 0;
            if (x + 1 < Current.Width)
                neighbours[count++] = i + 1;
            if (y + 1 < Current.Height)
                neighbours[count++] = i + Current.Width;
            if (x > 0)
                neighbours[count++] = i - 1;
            if (y > 0)
                neighbours[count++] = i - Current.Width;
            var predators = mask & ~AnimalRules.HerbivoreMask;
            while (predators != 0)
            {
                var species = NextWildlife(ref predators);
                var kind = (WildlifeKind)species;
                var animal = AnimalRules.For(kind);
                var biomass = _wildlifeBiomass[local * 3 + (int)animal.Size];
                var sharedBiomass = _wildlifeSharedBiomass[local * 3 + (int)animal.Size];

                predatorCapacities[species] = Math.Min(_wildlifeCapacities[capacityOffset + species],
                    sharedBiomass * .12 / animal.BodyMass);
                var population = _wildlifePopulations[offset + species];
                var capacity = predatorCapacities[species];
                var normalGrowth = capacity > 0
                    ? Math.Max(-population * deathRate, growthRate * population * (1 - population / capacity))
                    : -population * deathRate;
                var demand = population * animal.BodyMass * predationRate;
                // 先结算捕食者饥饿死亡，再由存活个体捕食；极端过密时允许猎物耗尽，不人为设置猎物下限。
                var fed = demand > 0 ? Math.Min(1, sharedBiomass / demand) : 1;
                predatorSurvivors[species] =
                    Math.Max(0, population + normalGrowth) * (fed < 1 ? Math.Pow(fed, .75) : 1);
                if (biomass <= 0)
                    continue;
                var consumption = predatorSurvivors[species] * animal.BodyMass * predationRate / biomass;
                var limit = _wildlifePredatorLimits[capacityOffset + species];
                var renewalRate = limit > 0
                    ? _wildlifeReplacement[capacityOffset + species] * Math.Min(1, predatorSurvivors[species] / limit)
                    : 0;
                foreach (var prey in AnimalRules.PreyFor(kind))
                {
                    preyLosses[(int)prey] += _wildlifePopulations[offset + (int)prey] * consumption;
                    preyRenewal[(int)prey] += _wildlifeCapacities[capacityOffset + (int)prey] * renewalRate;
                }
            }

            while (mask != 0)
            {
                var species = NextWildlife(ref mask);
                var kind = (WildlifeKind)species;
                var animal = AnimalRules.For(kind);
                var population = _wildlifePopulations[offset + species];
                var capacity = animal.Diet == AnimalDiet.Carnivore
                    ? predatorCapacities[species]
                    : _wildlifeCapacities[capacityOffset + species];
                var available = 0d;
                if (animal.Diet == AnimalDiet.Herbivore)
                {
                    var density = capacity > 0 ? population / capacity : 0;
                    var growth = capacity > 0
                        ? Math.Max(-population * deathRate, growthRate * population
                                                                       * (1 - density - .35 * Math.Max(0,
                                                                           _wildlifePressure[local] - 1)))
                        : -population * deathRate;
                    growth += preyRenewal[species] * Math.Min(1, density);
                    var loss = Math.Min(Math.Max(0, population + growth), preyLosses[species]);
                    available = Math.Max(0, population + growth - loss);
                }
                else
                    available = predatorSurvivors[species];

                _wildlifeChanges[offset + species] += available - population;
                // 迁移只能使用捕食后的存活量，避免负库存被截为零后凭空增加动物。
                for (var n = 0; n < count; n++)
                {
                    var next = neighbours[n];
                    var targetLocal = next - snapshotFirst;
                    var targetOffset = targetLocal * AnimalRules.SpeciesCount + species;
                    var targetCapacityOffset = next % bufferTiles * AnimalRules.SpeciesCount;
                    var creek = tiles[next].Terrain == TerrainType.Stream && !animal.Aquatic;
                    if (_wildlifeCapacities[targetCapacityOffset + species] <= 0 && !creek)
                        continue;
                    var targetCapacity = _wildlifeCapacities[targetCapacityOffset + species];
                    if (animal.Diet == AnimalDiet.Carnivore && targetCapacity > 0)
                    {
                        var preyMass = _wildlifeSharedBiomass[targetLocal * 3 + (int)animal.Size];
                        targetCapacity = Math.Min(targetCapacity, preyMass * .12 / animal.BodyMass);
                    }

                    if (targetCapacity <= 0 && !creek)
                        continue;
                    var preference =
                        Math.Clamp(
                            (capacity > 0 ? available / capacity : 2) - (targetCapacity > 0
                                ? _wildlifePopulations[targetOffset] / targetCapacity
                                : 0), 0, 1);
                    var amount = available * migrationRate / count * preference
                                                                   * (creek ||
                                                                      (tiles[i].Terrain == TerrainType.Stream &&
                                                                       !animal.Aquatic)
                                                                       ? .35
                                                                       : 1);
                    _wildlifeChanges[offset + species] -= amount;
                    _wildlifeChanges[targetOffset] += amount;
                    _wildlifeIncoming[targetLocal] |= 1 << species;
                }
            }
        }

        for (var i = snapshotFirst; i < snapshotLast; i++)
        {
            var local = i - snapshotFirst;
            if ((i < first || i >= last) && _wildlifeIncoming[local] == 0)
                continue;
            var tile = tiles[i];
            var mask = _wildlifeMasks[local] | _wildlifeIncoming[local];
            var populations = _wildlifePopulations.AsSpan(local * AnimalRules.SpeciesCount, AnimalRules.SpeciesCount);
            while (mask != 0)
            {
                var species = NextWildlife(ref mask);
                var kind = (WildlifeKind)species;
                var population =
                    Math.Clamp(
                        _wildlifePopulations[local * AnimalRules.SpeciesCount + species] +
                        _wildlifeChanges[local * AnimalRules.SpeciesCount + species], 0, 1000);
                population = population < .000001 ? 0 : population;
                populations[species] = population;
            }

            var primary = tile.Wildlife;
            if (populations[(int)primary] == 0)
            {
                primary = WildlifeKind.None;
                for (var species = 1; species < AnimalRules.SpeciesCount; species++)
                    if (populations[species] > 0)
                    {
                        primary = (WildlifeKind)species;
                        break;
                    }
            }

            tile.Replace(tile.Value.WithAnimalPopulations(primary, populations));
        }
    }

    /// <summary>返回种族的基础寿命，以模拟年为单位。</summary>
    /// <param name="race">要查询基础寿命的种族。</param>
    public static int Lifespan(RaceKind race)
    {
        return race switch
        {
            RaceKind.Elf => 180,
            RaceKind.Dwarf => 120,
            RaceKind.Orc => 70,
            _ => 90,
        };
    }

    private void RefreshSettlementName(SettlementCursor town)
    {
        var suffix = SettlementTierName(town.Tier);
        if (town.Name.EndsWith(suffix, StringComparison.Ordinal))
            return;
        var stem = town.Name.Length > 0 && town.Name[^1] is '城' or '镇' or '村' ? town.Name[..^1] : town.Name;
        var name = stem + suffix;
        if (Current.Settlements.Any(other => other.Id != town.Id && other.Name == name))
            name = stem + town.Id + suffix;
        town.Name = name;
    }

    private void EnsureTownCenters()
    {
        foreach (var town in Current.Settlements)
        {
            var center =
                Current.Buildings.FirstOrDefault(b =>
                    b.SettlementId == town.Id && b.Kind == BuildingKind.TownCenter);
            if (center is null)
            {
                center = new BuildingCursor(new Building
                {
                    Id = NewId(),
                    Kind = BuildingKind.TownCenter,
                    SettlementId = town.Id,
                    X = town.X,
                    Y = town.Y,
                    ConstructionProgress = 30,
                    ConstructionRequired = 30,
                    WorkSlots = 3,
                });
                Current.Buildings.Add(center);
            }

            center.Replace(center.Value with { X = town.X, Y = town.Y });
            if (center.Health <= 0 && Current.Tiles[Index(town.X, town.Y)].FireTicks == 0 && Current.Rules.Construction
                && MissingResources(town.Resources, GetBuildingCost(BuildingKind.TownCenter)) is null)
            {
                town.Resources = Spend(town.Resources, GetBuildingCost(BuildingKind.TownCenter));
                center.Replace(center.Value with { Health = 100, ConstructionProgress = 0 });
                center.Workers = center.Workers.Clear();
                center.Replace(center.Value with { LastWorkedTick = -100, Observation = new ProjectObservation() });
                var rebuilding = AddEvent(WorldEventKind.Construction, $"{town.Name}投入材料重建受损的城镇中心。", town.X, town.Y,
                    EventAction.Started, town.Id);
                center.Observation = center.Observation with { StartEventId = rebuilding.Id };
            }
        }
    }

    private readonly record struct WildlifeHabitat(
        TerrainType Terrain,
        double Resources,
        byte Fertility,
        LandImprovement Improvement,
        bool Settled,
        bool Drought,
        bool Fire,
        double Water,
        PlantCoverage Plants,
        bool Initialized);
}
