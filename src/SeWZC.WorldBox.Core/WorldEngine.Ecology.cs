using System.Numerics;

namespace SeWZC.WorldBox.Core;

public sealed partial class WorldEngine
{
    // Fixed daily work also bounds large-map cost. Small maps retain a six-day
    // cycle; 128 and 256 maps re-evaluate every 64 and 256 simulated days.
    private const int WildlifeTilesPerDay = 256;
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

    /// <summary>受每日地格预算限制，完成一轮全部动物复评所需的模拟日数。</summary>
    public int WildlifeCycleDays => Math.Max(6, (State.Tiles.Length + WildlifeTilesPerDay - 1) / WildlifeTilesPerDay);

    private static int NextWildlife(ref int mask)
    {
        var kind = BitOperations.TrailingZeroCount((uint)mask);
        mask &= mask - 1;
        return kind;
    }

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

    /// <summary>计算地格当前容量；食肉动物还受可分配的猎物生物量限制。</summary>
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

    private void SeedWildlife()
    {
        foreach (var tile in State.Tiles)
        {
            tile.Wildlife = WildlifeKind.None;
            tile.WildlifePopulation = 0;
            tile.OtherWildlife = new WildlifePopulations();
        }

        Span<double> capacities = stackalloc double[AnimalRules.SpeciesCount];
        Span<byte> competitors = stackalloc byte[AnimalRules.SpeciesCount];
        for (var i = 0; i < State.Tiles.Length; i++)
        {
            var tile = State.Tiles[i];
            AnimalRules.FillCapacities(tile, capacities, competitors);
            for (var diet = 0; diet < 2; diet++)
                foreach (var kind in AnimalRules.Species)
                {
                    if ((int)AnimalRules.For(kind).Diet != diet) continue;
                    var hash = unchecked((uint)i * 2654435761u + (uint)State.Seed * 31 + (uint)kind * 2246822519u);
                    var capacity = capacities[(int)kind];
                    if (diet == 1 && capacity > 0)
                    {
                        var biomass = 0d;
                        foreach (var prey in AnimalRules.PreyFor(kind))
                            biomass += tile.AnimalPopulation(prey) * AnimalRules.For(prey).BodyMass /
                                       competitors[(int)prey];
                        capacity = Math.Min(capacity, biomass * .12 / AnimalRules.For(kind).BodyMass);
                    }

                    if (capacity > 0) tile.SetAnimalPopulation(kind, capacity * (.15 + hash % 30 / 100d));
                }
        }
    }

    /// <summary>基于同一份种群快照，处理当日地格分区的繁殖、捕食和迁移。</summary>
    private void TickWildlife()
    {
        var tiles = State.Tiles;
        var bufferTiles = WildlifeTilesPerDay + State.Width * 2;
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
        var cycle = WildlifeCycleDays;
        // 从存档中的模拟时间推导分区，载入后即可接续复评顺序，无需另存游标。
        var phase = (int)((State.Tick - 1) % cycle);
        var first = phase * tiles.Length / cycle;
        var last = (phase + 1) * tiles.Length / cycle;
        var snapshotFirst = Math.Max(0, first - State.Width);
        var snapshotLast = Math.Min(tiles.Length, last + State.Width);
        var snapshotCount = snapshotLast - snapshotFirst;
        Array.Clear(_wildlifeChanges, 0, snapshotCount * AnimalRules.SpeciesCount);
        Array.Clear(_wildlifeIncoming, 0, snapshotCount);
        // Rates account for elapsed simulation time with bounded losses. Nothing
        // is updated by rendering or real-time frames, and no RNG is consumed.
        var elapsed = cycle / 6d;
        var growthRate = Math.Min(.25, .018 * elapsed);
        var deathRate = Math.Min(.65, 1 - Math.Pow(.88, elapsed));
        // Births are bounded for a long revisit interval. Scale predation with
        // that same effective interval rather than letting it outgrow renewal.
        var predationRate = .035 * (growthRate / .018);
        var migrationRate = Math.Min(.12, .02 * elapsed);
        // Snapshot only a compact band and its four-neighbour apron. Each day's
        // growth, consumption and migrations see one initial population state.
        for (var i = snapshotFirst; i < snapshotLast; i++)
        {
            var tile = tiles[i];
            var local = i - snapshotFirst;
            var mask = tile.WildlifeMask;
            var pressure = 0d;
            var offset = local * AnimalRules.SpeciesCount;
            // Keep habitat slots tied to map indices, not the moving band's origin.
            // Adjacent days share their apron and can retain its expensive capacities.
            var habitatSlot = i % bufferTiles;
            var capacityOffset = habitatSlot * AnimalRules.SpeciesCount;
            tile.OtherWildlife.CopyTo(_wildlifePopulations.AsSpan(offset, AnimalRules.SpeciesCount));
            _wildlifePopulations[offset + (int)tile.Wildlife] = tile.WildlifePopulation;
            _wildlifeMasks[local] = mask;
            // Above 100 resources, food availability is already saturated.
            var habitat = new WildlifeHabitat(tile.Terrain, Math.Clamp(tile.ResourceAmount / 100, 0, 1), tile.Fertility,
                tile.Improvement,
                tile.SettlementId != 0, tile.DroughtTicks > 0, tile.FireTicks > 0, tile.NaturalWaterYield, tile.Plants,
                true);
            if (_wildlifeHabitats[habitatSlot] != habitat)
            {
                _wildlifeHabitats[habitatSlot] = habitat;
                _wildlifeHerbivoreKinds[habitatSlot] = 0;
                Array.Clear(_wildlifeReplacement, capacityOffset, AnimalRules.SpeciesCount);
                AnimalRules.FillCapacities(tile, _wildlifeCapacities.AsSpan(capacityOffset, AnimalRules.SpeciesCount),
                    _wildlifePreyCompetitors.AsSpan(capacityOffset, AnimalRules.SpeciesCount));
                for (var species = 1; species < AnimalRules.SpeciesCount; species++)
                    if (_wildlifeCapacities[capacityOffset + species] > 0 &&
                        AnimalRules.For((WildlifeKind)species).Diet == AnimalDiet.Herbivore)
                        _wildlifeHerbivoreKinds[habitatSlot]++;
                // At every carrying capacity, herbivore renewal exactly replaces
                // the sustainable predator consumption computed from the same food web.
                foreach (var predator in AnimalRules.Species)
                {
                    var definition = AnimalRules.For(predator);
                    if (definition.Diet != AnimalDiet.Carnivore ||
                        _wildlifeCapacities[capacityOffset + (int)predator] <= 0) continue;
                    var biomass = 0d;
                    var share = 0d;
                    foreach (var prey in AnimalRules.PreyFor(predator))
                    {
                        var mass = _wildlifeCapacities[capacityOffset + (int)prey] * AnimalRules.For(prey).BodyMass;
                        biomass += mass;
                        share += mass / _wildlifePreyCompetitors[capacityOffset + (int)prey];
                    }

                    if (biomass == 0) continue;
                    var capacity = Math.Min(_wildlifeCapacities[capacityOffset + (int)predator],
                        share * .12 / definition.BodyMass);
                    var rate = capacity * definition.BodyMass * predationRate / biomass;
                    _wildlifeReplacement[capacityOffset + (int)predator] = rate;
                    _wildlifePredatorLimits[capacityOffset + (int)predator] = capacity;
                }
            }

            while (mask != 0)
            {
                var species = NextWildlife(ref mask);
                var capacity = _wildlifeCapacities[capacityOffset + species];
                if (capacity > 0 && AnimalRules.For((WildlifeKind)species).Diet == AnimalDiet.Herbivore)
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
            var x = i % State.Width;
            var y = i / State.Width;
            var local = i - snapshotFirst;
            var offset = local * AnimalRules.SpeciesCount;
            var mask = _wildlifeMasks[local];
            var capacityOffset = i % bufferTiles * AnimalRules.SpeciesCount;
            if (mask == 0) continue;
            preyLosses.Clear();
            preyRenewal.Clear();
            var count = 0;
            if (x + 1 < State.Width) neighbours[count++] = i + 1;
            if (y + 1 < State.Height) neighbours[count++] = i + State.Width;
            if (x > 0) neighbours[count++] = i - 1;
            if (y > 0) neighbours[count++] = i - State.Width;
            var predators = mask;
            while (predators != 0)
            {
                var species = NextWildlife(ref predators);
                var kind = (WildlifeKind)species;
                var animal = AnimalRules.For(kind);
                if (animal.Diet != AnimalDiet.Carnivore) continue;
                var biomass = 0d;
                var sharedBiomass = 0d;
                foreach (var prey in AnimalRules.PreyFor(kind))
                {
                    var mass = _wildlifePopulations[offset + (int)prey] * AnimalRules.For(prey).BodyMass;
                    biomass += mass;
                    sharedBiomass += mass / _wildlifePreyCompetitors[capacityOffset + (int)prey];
                }

                predatorCapacities[species] = Math.Min(_wildlifeCapacities[capacityOffset + species],
                    sharedBiomass * .12 / animal.BodyMass);
                var population = _wildlifePopulations[offset + species];
                var capacity = predatorCapacities[species];
                var normalGrowth = capacity > 0
                    ? Math.Max(-population * deathRate, growthRate * population * (1 - population / capacity))
                    : -population * deathRate;
                var demand = population * animal.BodyMass * predationRate;
                // Food shortage kills predators before feeding. Starving survivors
                // can still consume all prey under extreme overcrowding. No prey floor is imposed.
                var fed = demand > 0 ? Math.Min(1, sharedBiomass / demand) : 1;
                predatorSurvivors[species] = Math.Max(0, population + normalGrowth) * Math.Pow(fed, .75);
                if (biomass <= 0) continue;
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
                var density = capacity > 0 ? population / capacity : 0;
                var growth = capacity > 0
                    ? Math.Max(-population * deathRate, growthRate * population
                                                                   * (1 - density - .35 * Math.Max(0,
                                                                       _wildlifePressure[local] - 1)))
                    : -population * deathRate;
                if (animal.Diet == AnimalDiet.Herbivore) growth += preyRenewal[species] * Math.Min(1, density);
                var loss = Math.Min(Math.Max(0, population + growth), preyLosses[species]);
                var available = animal.Diet == AnimalDiet.Carnivore
                    ? predatorSurvivors[species]
                    : Math.Max(0, population + growth - loss);
                _wildlifeChanges[offset + species] += available - population;
                // Migration spends only survivors, preventing simultaneous predation
                // and migration from creating animals through a clamped negative stock.
                for (var n = 0; n < count; n++)
                {
                    var next = neighbours[n];
                    var targetLocal = next - snapshotFirst;
                    var targetOffset = targetLocal * AnimalRules.SpeciesCount + species;
                    var targetCapacityOffset = next % bufferTiles * AnimalRules.SpeciesCount;
                    var creek = tiles[next].Terrain == TerrainType.Stream && !animal.Aquatic;
                    if (_wildlifeCapacities[targetCapacityOffset + species] <= 0 && !creek) continue;
                    var targetCapacity = _wildlifeCapacities[targetCapacityOffset + species];
                    if (animal.Diet == AnimalDiet.Carnivore && targetCapacity > 0)
                    {
                        var preyMass = 0d;
                        foreach (var prey in AnimalRules.PreyFor(kind))
                            preyMass += _wildlifePopulations[targetLocal * AnimalRules.SpeciesCount + (int)prey] *
                                        AnimalRules.For(prey).BodyMass /
                                        _wildlifePreyCompetitors[targetCapacityOffset + (int)prey];
                        targetCapacity = Math.Min(targetCapacity, preyMass * .12 / animal.BodyMass);
                    }

                    if (targetCapacity <= 0 && !creek) continue;
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
            if ((i < first || i >= last) && _wildlifeIncoming[local] == 0) continue;
            var tile = tiles[i];
            var mask = _wildlifeMasks[local] | _wildlifeIncoming[local];
            var others = tile.OtherWildlife;
            while (mask != 0)
            {
                var species = NextWildlife(ref mask);
                var kind = (WildlifeKind)species;
                var population =
                    Math.Clamp(
                        _wildlifePopulations[local * AnimalRules.SpeciesCount + species] +
                        _wildlifeChanges[local * AnimalRules.SpeciesCount + species], 0, 1000);
                population = population < .000001 ? 0 : population;
                if (kind == tile.Wildlife) tile.WildlifePopulation = population;
                else others.Set(kind, population);
            }

            tile.OtherWildlife = others;
            if (tile.WildlifePopulation == 0)
            {
                tile.Wildlife = WildlifeKind.None;
                var remaining = tile.OtherWildlife.ActiveMask;
                if (remaining != 0)
                {
                    var kind = (WildlifeKind)NextWildlife(ref remaining);
                    var population = tile.OtherWildlife.Get(kind);
                    others.Set(kind, 0);
                    tile.OtherWildlife = others;
                    tile.Wildlife = kind;
                    tile.WildlifePopulation = population;
                }
            }
        }
    }

    public static int Lifespan(RaceKind race)
    {
        return race switch
        {
            RaceKind.Elf => 180, RaceKind.Dwarf => 120, RaceKind.Orc => 70, _ => 90,
        };
    }

    private void RefreshSettlementName(Settlement town)
    {
        var suffix = SettlementTierName(town.Tier);
        if (town.Name.EndsWith(suffix, StringComparison.Ordinal)) return;
        var stem = town.Name.Length > 0 && town.Name[^1] is '城' or '镇' or '村' ? town.Name[..^1] : town.Name;
        var name = stem + suffix;
        if (State.Settlements.Any(other => other.Id != town.Id && other.Name == name)) name = stem + town.Id + suffix;
        town.Name = name;
    }

    private void EnsureTownCenters()
    {
        foreach (var town in State.Settlements)
        {
            var center =
                State.Society.Buildings.FirstOrDefault(b =>
                    b.SettlementId == town.Id && b.Kind == BuildingKind.TownCenter);
            if (center is null)
            {
                center = new Building
                {
                    Id = NewId(), Kind = BuildingKind.TownCenter, SettlementId = town.Id,
                    X = town.X, Y = town.Y, ConstructionProgress = 30, ConstructionRequired = 30, WorkSlots = 3,
                };
                State.Society.Buildings.Add(center);
            }

            center.X = town.X;
            center.Y = town.Y;
            if (center.Health <= 0 && State.Tiles[Index(town.X, town.Y)].FireTicks == 0 && State.Rules.Construction
                && MissingResources(town.Resources, GetBuildingCost(BuildingKind.TownCenter)) is null)
            {
                Spend(town.Resources, GetBuildingCost(BuildingKind.TownCenter));
                center.Health = 100;
                center.ConstructionProgress = 0;
                center.Workers.Clear();
                center.LastWorkedTick = -100;
                center.Observation = new ProjectObservation();
                var rebuilding = AddEvent(WorldEventKind.Construction, $"{town.Name}投入材料重建受损的城镇中心。", town.X, town.Y,
                    EventAction.Started, town.Id);
                center.Observation.StartEventId = rebuilding.Id;
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
