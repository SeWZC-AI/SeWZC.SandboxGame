using System.Numerics;

namespace SeWZC.WorldBox.Core;

public sealed partial class WorldEngine
{
    private double[]? _wildlifeChanges;
    private double[]? _wildlifePopulations;
    private double[]? _wildlifePressure;
    private int[]? _wildlifeIncoming;
    private int[]? _wildlifeMasks;
    private WildlifeHabitat[]? _wildlifeHabitats;
    private double[]? _wildlifeCapacities;
    private readonly record struct WildlifeHabitat(TerrainType Terrain, double Resources, byte Fertility,
        LandImprovement Improvement, bool Settled, bool Drought, bool Fire, bool Initialized);
    private static int NextWildlife(ref int mask)
    { var kind = BitOperations.TrailingZeroCount((uint)mask); mask &= mask - 1; return kind; }

    public static string WildlifeName(WildlifeKind kind) => kind switch
    {
        WildlifeKind.Rabbit => "野兔", WildlifeKind.Deer => "鹿", WildlifeKind.Boar => "野猪",
        WildlifeKind.Goat => "山羊", WildlifeKind.Wolf => "狼", WildlifeKind.Waterfowl => "水鸟",
        WildlifeKind.Fish => "鱼", _ => "无"
    };

    public static double WildlifeCapacity(Tile tile, WildlifeKind kind)
    {
        var habitat = kind switch
        {
            WildlifeKind.Rabbit => tile.Terrain is TerrainType.Grass or TerrainType.DryFertile or TerrainType.Hills or TerrainType.Tundra ? 12 : 0,
            WildlifeKind.Deer => tile.Terrain is TerrainType.Forest or TerrainType.Grass or TerrainType.DryFertile ? 9 : 0,
            WildlifeKind.Boar => tile.Terrain is TerrainType.Forest or TerrainType.Wetland ? 8 : 0,
            WildlifeKind.Goat => tile.Terrain is TerrainType.Mountain or TerrainType.Hills ? 7 : 0,
            WildlifeKind.Wolf => tile.Terrain is TerrainType.Forest or TerrainType.Snow or TerrainType.Tundra ? 4 : 0,
            WildlifeKind.Waterfowl => tile.Terrain is TerrainType.Wetland or TerrainType.River or TerrainType.Water ? 10 : 0,
            WildlifeKind.Fish => tile.Terrain is TerrainType.Water or TerrainType.DeepWater or TerrainType.River or TerrainType.Lake ? 18 : 0,
            _ => 0
        };
        var food = kind is WildlifeKind.Fish or WildlifeKind.Waterfowl or WildlifeKind.Goat
            ? .6 + tile.Fertility / 250d : Math.Clamp(tile.ResourceAmount / 100, 0, 1) * (.25 + tile.Fertility / 133d);
        return habitat * food * (tile.Improvement == LandImprovement.Farmland ? .35 : 1)
            * (tile.SettlementId != 0 ? .1 : 1) * (tile.DroughtTicks > 0 ? .25 : 1) * (tile.FireTicks > 0 ? 0 : 1);
    }

    private void SeedWildlife()
    {
        for (var i = 0; i < State.Tiles.Length; i++)
        {
            var tile = State.Tiles[i];
            var hash = unchecked((uint)i * 2654435761u + (uint)State.Seed);
            tile.Wildlife = tile.Terrain switch
            {
                TerrainType.Forest => hash % 3 == 0 ? WildlifeKind.Boar : WildlifeKind.Deer,
                TerrainType.Grass or TerrainType.DryFertile => WildlifeKind.Rabbit,
                TerrainType.Hills or TerrainType.Mountain => WildlifeKind.Goat,
                TerrainType.Snow or TerrainType.Tundra => WildlifeKind.Wolf,
                TerrainType.Wetland => WildlifeKind.Waterfowl,
                TerrainType.River or TerrainType.Water or TerrainType.DeepWater or TerrainType.Lake => WildlifeKind.Fish,
                _ => WildlifeKind.None
            };
            tile.WildlifePopulation = WildlifeCapacity(tile, tile.Wildlife) * (.15 + hash % 50 / 100d);
            var companion = tile.Terrain switch
            {
                TerrainType.Forest => tile.Wildlife == WildlifeKind.Deer ? WildlifeKind.Boar : WildlifeKind.Deer,
                TerrainType.Grass or TerrainType.DryFertile => WildlifeKind.Deer,
                TerrainType.Hills or TerrainType.Tundra => WildlifeKind.Rabbit,
                TerrainType.Wetland => WildlifeKind.Boar,
                TerrainType.River or TerrainType.Water or TerrainType.Lake => WildlifeKind.Waterfowl,
                _ => WildlifeKind.None
            };
            if (companion != WildlifeKind.None)
                tile.SetAnimalPopulation(companion, WildlifeCapacity(tile, companion) * (.1 + hash % 20 / 100d));
            if (tile.Terrain == TerrainType.Forest && hash % 7 == 0)
                tile.SetAnimalPopulation(WildlifeKind.Wolf, WildlifeCapacity(tile, WildlifeKind.Wolf) * .15);
        }
    }

    private void TickWildlife()
    {
        var tiles = State.Tiles;
        _wildlifeChanges ??= new double[tiles.Length * 8];
        _wildlifePopulations ??= new double[tiles.Length * 8];
        _wildlifePressure ??= new double[tiles.Length];
        _wildlifeIncoming ??= new int[tiles.Length];
        _wildlifeMasks ??= new int[tiles.Length];
        _wildlifeHabitats ??= new WildlifeHabitat[tiles.Length];
        _wildlifeCapacities ??= new double[tiles.Length * 8];
        // Each row grows once per six days. Complete a band and its migration
        // atomically, so saves never need to preserve a half-finished ecology pass.
        var band = (int)((State.Tick - 1) % 6);
        var firstRow = band * State.Height / 6;
        var lastRow = (band + 1) * State.Height / 6;
        var first = firstRow * State.Width;
        var last = lastRow * State.Width;
        var snapshotFirst = Math.Max(0, firstRow - 1) * State.Width;
        var snapshotLast = Math.Min(State.Height, lastRow + 1) * State.Width;
        Array.Clear(_wildlifeChanges, snapshotFirst * 8, (snapshotLast - snapshotFirst) * 8);
        Array.Clear(_wildlifeIncoming, snapshotFirst, snapshotLast - snapshotFirst);
        // Snapshot only the active band and its one-row migration apron. Flat
        // arrays avoid repeatedly copying population structs and switching species
        // for every neighbour. All migrations in this band see its starting state.
        for (var i = snapshotFirst; i < snapshotLast; i++)
        {
            var tile = tiles[i]; var mask = tile.WildlifeMask; var pressure = 0d; var offset = i * 8;
            var others = tile.OtherWildlife;
            _wildlifePopulations[offset + 1] = others.Rabbit;
            _wildlifePopulations[offset + 2] = others.Deer;
            _wildlifePopulations[offset + 3] = others.Boar;
            _wildlifePopulations[offset + 4] = others.Goat;
            _wildlifePopulations[offset + 5] = others.Wolf;
            _wildlifePopulations[offset + 6] = others.Waterfowl;
            _wildlifePopulations[offset + 7] = others.Fish;
            if (tile.Wildlife != WildlifeKind.None)
                _wildlifePopulations[offset + (int)tile.Wildlife] = tile.WildlifePopulation;
            _wildlifeMasks[i] = mask;
            // Above 100 resources, food availability is already saturated.
            var habitat = new WildlifeHabitat(tile.Terrain, Math.Clamp(tile.ResourceAmount / 100, 0, 1), tile.Fertility, tile.Improvement,
                tile.SettlementId != 0, tile.DroughtTicks > 0, tile.FireTicks > 0, true);
            if (_wildlifeHabitats[i] != habitat)
            {
                _wildlifeHabitats[i] = habitat;
                for (var species = 1; species <= (int)WildlifeKind.Fish; species++)
                    _wildlifeCapacities[offset + species] = WildlifeCapacity(tile, (WildlifeKind)species);
            }
            while (mask != 0)
            {
                var species = NextWildlife(ref mask); var capacity = _wildlifeCapacities[offset + species];
                if (capacity > 0) pressure += _wildlifePopulations[offset + species] / capacity;
            }
            _wildlifePressure[i] = pressure;
        }
        Span<int> neighbours = stackalloc int[4];
        for (var y = firstRow; y < lastRow; y++)
        for (var x = 0; x < State.Width; x++)
        {
            var i = y * State.Width + x; var offset = i * 8; var mask = _wildlifeMasks[i];
            if (mask == 0) continue;
            var count = 0;
            if (x + 1 < State.Width) neighbours[count++] = i + 1;
            if (y + 1 < State.Height) neighbours[count++] = i + State.Width;
            if (x > 0) neighbours[count++] = i - 1;
            if (y > 0) neighbours[count++] = i - State.Width;
            while (mask != 0)
            {
                var species = NextWildlife(ref mask);
                var population = _wildlifePopulations[offset + species]; var capacity = _wildlifeCapacities[offset + species];
                var density = capacity > 0 ? population / capacity : 0;
                _wildlifeChanges[offset + species] += capacity > 0
                    ? Math.Max(-population * .12, .018 * population * (1 - density - .35 * (_wildlifePressure[i] - density))) : -population * .12;
                for (var n = 0; n < count; n++)
                {
                    var next = neighbours[n]; var targetOffset = next * 8 + species; var targetCapacity = _wildlifeCapacities[targetOffset];
                    var targetPopulation = _wildlifePopulations[targetOffset];
                    if (targetCapacity <= 0) continue;
                    // Capacity influences preference and future survival, not entry.
                    // A crowded but suitable neighbour can still receive migrants.
                    var preference = .5 + .5 / (1 + _wildlifePressure[next]);
                    var amount = population * .20 / count * preference;
                    _wildlifeChanges[offset + species] -= amount; _wildlifeChanges[targetOffset] += amount;
                    _wildlifeIncoming[next] |= 1 << species;
                }
            }
        }
        for (var i = snapshotFirst; i < snapshotLast; i++)
        {
            if ((i < first || i >= last) && _wildlifeIncoming[i] == 0) continue;
            var tile = tiles[i]; var mask = _wildlifeMasks[i] | _wildlifeIncoming[i];
            while (mask != 0)
            {
                var species = NextWildlife(ref mask); var kind = (WildlifeKind)species;
                var population = Math.Clamp(_wildlifePopulations[i * 8 + species] + _wildlifeChanges[i * 8 + species], 0, 1000);
                tile.SetAnimalPopulation(kind, population < .001 ? 0 : population);
            }
            if (tile.WildlifePopulation == 0)
            {
                tile.Wildlife = WildlifeKind.None;
                var remaining = tile.OtherWildlife.ActiveMask;
                if (remaining != 0)
                {
                    var kind = (WildlifeKind)NextWildlife(ref remaining); var population = tile.OtherWildlife.Get(kind);
                    var others = tile.OtherWildlife; others.Set(kind, 0); tile.OtherWildlife = others;
                    tile.Wildlife = kind; tile.WildlifePopulation = population;
                }
            }
        }
    }

    public static int Lifespan(RaceKind race) => race switch
    { RaceKind.Elf => 180, RaceKind.Dwarf => 120, RaceKind.Orc => 70, _ => 90 };

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
            var center = State.Society.Buildings.FirstOrDefault(b => b.SettlementId == town.Id && b.Kind == BuildingKind.TownCenter);
            if (center is null)
            {
                center = new Building { Id = NewId(), Kind = BuildingKind.TownCenter, SettlementId = town.Id,
                    X = town.X, Y = town.Y, ConstructionProgress = 30, ConstructionRequired = 30, WorkSlots = 3 };
                State.Society.Buildings.Add(center);
            }
            center.X = town.X; center.Y = town.Y;
            if (center.Health <= 0 && State.Tiles[Index(town.X, town.Y)].FireTicks == 0 && State.Rules.Construction
                && MissingResources(town.Resources, GetBuildingCost(BuildingKind.TownCenter)) is null)
            {
                Spend(town.Resources, GetBuildingCost(BuildingKind.TownCenter));
                center.Health = 100; center.ConstructionProgress = 0;
                center.Workers.Clear(); center.LastWorkedTick = -100; center.Observation = new();
                var rebuilding = AddEvent(WorldEventKind.Construction, $"{town.Name}投入材料重建受损的城镇中心。", town.X, town.Y, EventAction.Started, town.Id);
                center.Observation.StartEventId = rebuilding.Id;
            }
        }
    }
}
