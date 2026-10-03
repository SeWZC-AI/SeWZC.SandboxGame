namespace SeWZC.WorldBox.Core;

public sealed partial class WorldEngine
{
    private double[]? _wildlifeChanges;

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
            WildlifeKind.Rabbit => tile.Terrain is TerrainType.Grass or TerrainType.Hills or TerrainType.Tundra ? 12 : 0,
            WildlifeKind.Deer => tile.Terrain is TerrainType.Forest or TerrainType.Grass ? 9 : 0,
            WildlifeKind.Boar => tile.Terrain is TerrainType.Forest or TerrainType.Wetland ? 8 : 0,
            WildlifeKind.Goat => tile.Terrain is TerrainType.Mountain or TerrainType.Hills ? 7 : 0,
            WildlifeKind.Wolf => tile.Terrain is TerrainType.Forest or TerrainType.Snow or TerrainType.Tundra ? 4 : 0,
            WildlifeKind.Waterfowl => tile.Terrain is TerrainType.Wetland or TerrainType.River or TerrainType.Water ? 10 : 0,
            WildlifeKind.Fish => tile.Terrain is TerrainType.Water or TerrainType.DeepWater or TerrainType.River ? 18 : 0,
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
                TerrainType.Grass => WildlifeKind.Rabbit,
                TerrainType.Hills or TerrainType.Mountain => WildlifeKind.Goat,
                TerrainType.Snow or TerrainType.Tundra => WildlifeKind.Wolf,
                TerrainType.Wetland => WildlifeKind.Waterfowl,
                TerrainType.River or TerrainType.Water or TerrainType.DeepWater => WildlifeKind.Fish,
                _ => WildlifeKind.None
            };
            tile.WildlifePopulation = WildlifeCapacity(tile, tile.Wildlife) * (.15 + hash % 50 / 100d);
        }
    }

    private void TickWildlife()
    {
        if (State.Tick % 6 != 0) return;
        var tiles = State.Tiles;
        _wildlifeChanges ??= new double[tiles.Length];
        Array.Clear(_wildlifeChanges);
        // Two passes: every migration uses the same starting population, independent of tile order.
        for (var i = 0; i < tiles.Length; i++)
        {
            var tile = tiles[i]; var population = tile.WildlifePopulation;
            if (population <= 0 || tile.Wildlife == WildlifeKind.None) continue;
            var capacity = WildlifeCapacity(tile, tile.Wildlife);
            _wildlifeChanges[i] += capacity > 0
                ? Math.Max(-population * .12, .07 * population * (1 - population / capacity)) : -population * .12;
            var moving = population * .025;
            foreach (var (dx, dy) in Directions)
            {
                var x = i % State.Width + dx; var y = i / State.Width + dy;
                if (!InBounds(x, y)) continue;
                var next = Index(x, y); var target = tiles[next];
                if (target.Wildlife != tile.Wildlife && target.Wildlife != WildlifeKind.None) continue;
                var targetCapacity = WildlifeCapacity(target, tile.Wildlife);
                if (targetCapacity <= target.WildlifePopulation) continue;
                var amount = Math.Min(moving / 4, (targetCapacity - target.WildlifePopulation) * .01);
                _wildlifeChanges[i] -= amount; _wildlifeChanges[next] += amount;
                // Empty habitats are assigned in stable direction order, without creating animals.
                if (target.Wildlife == WildlifeKind.None) target.Wildlife = tile.Wildlife;
            }
        }
        for (var i = 0; i < tiles.Length; i++)
        {
            tiles[i].WildlifePopulation = Math.Clamp(tiles[i].WildlifePopulation + _wildlifeChanges[i], 0, 1000);
            if (tiles[i].WildlifePopulation < .001) { tiles[i].WildlifePopulation = 0; tiles[i].Wildlife = WildlifeKind.None; }
        }
    }

    public static int Lifespan(RaceKind race) => race switch
    { RaceKind.Elf => 180, RaceKind.Dwarf => 120, RaceKind.Orc => 70, _ => 90 };

    public static string SettlementSuffix(int population) => population >= 160 ? "城" : population >= 60 ? "镇" : "村";

    private void RefreshSettlementName(Settlement town)
    {
        if (town.Name.EndsWith(SettlementSuffix(town.Population), StringComparison.Ordinal)) return;
        var stem = town.Name.Length > 0 && town.Name[^1] is '城' or '镇' or '村' ? town.Name[..^1] : town.Name;
        var name = stem + SettlementSuffix(town.Population);
        if (State.Settlements.Any(other => other.Id != town.Id && other.Name == name)) name = stem + town.Id + SettlementSuffix(town.Population);
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
