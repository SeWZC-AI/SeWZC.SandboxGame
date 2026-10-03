namespace SeWZC.WorldBox.Core;

public sealed partial class WorldEngine
{
    public static bool IsWaterTerrain(TerrainType terrain) => terrain is TerrainType.Water or TerrainType.DeepWater or TerrainType.River or TerrainType.Lake;
    public static bool IsFreshWater(Tile tile) => tile.Terrain is TerrainType.River or TerrainType.Lake;
    public static bool IsWaterSource(Tile tile) => IsFreshWater(tile) || tile.Terrain == TerrainType.Wetland;

    private void GenerateLakesAndWater()
    {
        // Small inland basins. All choices use the saved world's seeded noise;
        // no renderer, wall clock or independent random source affects creation.
        for (var y = 6; y < State.Height - 6; y += 12)
        for (var x = 6; x < State.Width - 6; x += 12)
        {
            if (Noise(x * .7, y * .7, 977) < .79) continue;
            if (Circle(x, y, 4).Any(i => IsWaterTerrain(State.Tiles[i].Terrain) || !State.Tiles[i].IsWalkable)) continue;
            var radius = Noise(x, y, 983) > .7 ? 2 : 1;
            foreach (var index in Circle(x, y, radius))
            {
                var tile = State.Tiles[index]; tile.Terrain = TerrainType.Lake;
                tile.Elevation = 75; tile.Fertility = 80; tile.ResourceAmount = 0;
                tile.Deposit = null; tile.DepositAmount = 0; tile.DepositDiscovered = false;
            }
        }
        // One creation-time multi-source BFS, O(map area), never repeated by Step.
        var distances = new int[State.Tiles.Length]; Array.Fill(distances, int.MaxValue);
        var queue = new Queue<int>();
        for (var i = 0; i < State.Tiles.Length; i++)
            if (IsFreshWater(State.Tiles[i])) { distances[i] = 0; queue.Enqueue(i); }
        while (queue.TryDequeue(out var current))
        {
            var x = current % State.Width; var y = current / State.Width;
            foreach (var (dx, dy) in Directions)
            {
                if (!InBounds(x + dx, y + dy)) continue;
                var next = Index(x + dx, y + dy);
                if (distances[next] != int.MaxValue) continue;
                distances[next] = distances[current] + 1; queue.Enqueue(next);
            }
        }
        for (var i = 0; i < State.Tiles.Length; i++)
        {
            var tile = State.Tiles[i]; var x = i % State.Width; var y = i / State.Width;
            if (IsWaterTerrain(tile.Terrain)) { tile.NaturalWaterYield = 0; continue; }
            // Soil and water use independent salts and scales. A Gaussian shore
            // contribution saturates nearby and vanishes far away, rather than
            // imposing a linear or inverse-distance relationship.
            var soil = Noise(x / 9d, y / 9d, 1201);
            tile.Fertility = (byte)Math.Clamp(25 + soil * 75, 0, 100);
            var water = .0005 + Math.Pow(Noise(x / 11d, y / 11d, 1601), 3) * .018;
            var shore = distances[i] == int.MaxValue ? 0 : .012 * Math.Exp(-Math.Pow(distances[i] / 4d, 2));
            tile.NaturalWaterYield = Math.Round(water + shore, 6);
            if (tile.Terrain is TerrainType.Grass or TerrainType.Desert or TerrainType.Wetland or TerrainType.Forest)
                tile.Terrain = tile.Fertility >= 65 && tile.NaturalWaterYield < .004 ? TerrainType.DryFertile
                    : tile.NaturalWaterYield >= .012 ? TerrainType.Wetland
                    : tile.NaturalWaterYield >= .007 ? TerrainType.Forest : TerrainType.Grass;
        }
    }

    private void SeedPlants(Tile tile)
    {
        tile.Plants = tile.Terrain switch
        {
            TerrainType.Forest => new() { Trees = .7, Shrubs = .3 },
            TerrainType.Grass or TerrainType.DryFertile or TerrainType.Hills or TerrainType.Tundra => new() { Grass = .6, Shrubs = .15 },
            TerrainType.Wetland => new() { Reeds = .6, Grass = .3 },
            _ => new()
        };
    }

    private void TickPlants()
    {
        if (!State.Rules.ResourceRegeneration) return;
        // Each row is updated once per 120-day year, with no yearly full-map spike.
        var band = (int)((State.Tick - 1) % 120);
        var firstRow = band * State.Height / 120; var lastRow = (band + 1) * State.Height / 120;
        for (var y = firstRow; y < lastRow; y++)
        for (var x = 0; x < State.Width; x++)
        {
            var tile = State.Tiles[Index(x, y)];
            if (!tile.IsWalkable || tile.Improvement != LandImprovement.None) continue;
            if (tile.FireTicks > 0) { tile.Plants = new(); continue; }
            var plants = tile.Plants;
            for (var species = 0; species < 4; species++)
            {
                var kind = (PlantKind)species; var nearby = 0d;
                foreach (var (dx, dy) in Directions)
                    if (InBounds(x + dx, y + dy)) nearby = Math.Max(nearby, State.Tiles[Index(x + dx, y + dy)].Plants.Get(kind));
                var suitable = kind == PlantKind.Reeds ? tile.NaturalWaterYield >= .01
                    : kind == PlantKind.Trees ? tile.NaturalWaterYield >= .004 && tile.Fertility >= 40
                    : tile.Fertility >= 15;
                var value = plants.Get(kind);
                var capacity = suitable ? tile.Fertility / 100d * (tile.DroughtTicks > 0 ? .3 : 1) : 0;
                value = suitable ? value + .06 * value * (1 - value / Math.Max(.01, capacity)) + nearby * .012 : value * .8;
                plants.Set(kind, Math.Clamp(value, 0, 1));
            }
            var total = plants.Total;
            if (total > 1)
                for (var species = 0; species < 4; species++) plants.Set((PlantKind)species, plants.Get((PlantKind)species) / total);
            tile.Plants = plants;
            if (tile.Terrain is TerrainType.Grass or TerrainType.DryFertile && plants.Trees >= .5 && tile.ClaimedSettlementId == 0)
                tile.Terrain = TerrainType.Forest;
        }
    }
}
