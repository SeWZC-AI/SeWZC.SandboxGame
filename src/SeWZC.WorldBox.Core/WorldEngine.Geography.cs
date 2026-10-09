using SeWZC.WorldBox.Core.Runtime;

namespace SeWZC.WorldBox.Core;

public sealed partial class WorldEngine
{
    private int[]? _demoHabitat;

    private void GenerateHydrology()
    {
        var tiles = Tiles;
        var parents = new int[tiles.Length];
        Array.Fill(parents, -1);
        var visited = new bool[tiles.Length];
        var order = new List<int>(tiles.Length);
        var flow = new double[tiles.Length];
        var frontier = new PriorityQueue<int, (int Height, int Index)>();
        for (var i = 0; i < tiles.Length; i++)
        {
            var tile = tiles[i];
            var before = tile.Value;
            var x = i % Width;
            var y = i / Width;
            tile.Replace(before with
            {
                RiverWidth = 0,
                Terrain = before.Elevation < 51 ? TerrainType.DeepWater : before.Elevation < 69 ? TerrainType.Water : TerrainType.Grass,
                Fertility = (byte)Math.Clamp(15 + Noise(x / 23d, y / 23d, 1201) * 85, 0, 100),
                Rainfall = Math.Round(.002 + Math.Pow(Noise(x / 27d, y / 27d, 1601), 3) * .24, 6),
            });
            flow[i] = .2 + tile.Value.Rainfall * 15;
            if (!IsWaterTerrain(tile.Value.Terrain))
                continue;
            visited[i] = true;
            frontier.Enqueue(i, (tile.Value.Elevation, i));
        }

        // 优先洪泛为平坦盆地也建立通向海洋的无环出口，出队顺序可直接用于汇流计算。
        while (frontier.TryDequeue(out var current, out var priority))
        {
            order.Add(current);
            foreach (var (dx, dy) in Directions)
            {
                var x = current % Width + dx;
                var y = current / Width + dy;
                if (!InBounds(x, y))
                    continue;
                var next = Index(x, y);
                if (visited[next])
                    continue;
                visited[next] = true;
                parents[next] = current;
                frontier.Enqueue(next, (Math.Max(priority.Height, tiles[next].Value.Elevation), next));
            }
        }

        for (var p = order.Count - 1; p >= 0; p--)
            if (parents[order[p]] >= 0)
                flow[parents[order[p]]] += flow[order[p]];

        for (var y = 7; y < Height - 7; y += 12)
        for (var x = 7; x < Width - 7; x += 12)
        {
            if (Noise(x, y, 977) < .64)
                continue;
            var area = Circle(x, y, 3).ToArray();
            if (area.Any(i =>
                    IsWaterTerrain(tiles[i].Value.Terrain) || tiles[i].Value.Elevation > 178 || _demoHabitat?[i] >= 0))
                continue;
            var radius = Noise(x, y, 983) > .7 ? 2 : 1;
            foreach (var i in Circle(x, y, radius))
                tiles[i].Replace(tiles[i].Value.WithTerrain(TerrainType.Lake));
        }

        var sources = Enumerable.Range(0, tiles.Length)
            .Where(i => parents[i] >= 0 && !IsWaterTerrain(tiles[i].Value.Terrain) && tiles[i].Value.Elevation >= 120 &&
                        flow[i] < 12)
            .OrderByDescending(i =>
                tiles[i].Value.Elevation + Noise(i % Width / 7d, i / Width / 7d, 1879) * 65)
            .ThenBy(i => i);
        var selected = new List<int>();
        var desired = Math.Clamp(tiles.Length / 4096 + 2, 2, 12);
        var centers = new byte[tiles.Length];
        foreach (var source in sources)
        {
            if (selected.Any(i =>
                    Distance(i % Width, i / Width, source % Width, source / Width) <
                    Math.Max(8, Width / 10)))
                continue;
            var path = new List<int>();
            var current = source;
            var reserved = false;
            while (current >= 0 && tiles[current].Value.Terrain is not (TerrainType.DeepWater or TerrainType.Water))
            {
                if (_demoHabitat?[current] >= 0)
                {
                    reserved = true;
                    break;
                }

                path.Add(current);
                current = parents[current];
            }

            if (reserved || current < 0 || path.Count < 8)
                continue;
            selected.Add(source);
            foreach (var i in path)
                centers[i] = (byte)Math.Max(centers[i],
                    flow[i] < 30 ? 1 : flow[i] < 85 ? 2 : flow[i] < 190 ? 3 : flow[i] < 420 ? 4 : 5);
            if (selected.Count >= desired)
                break;
        }

        // 河道横截面按一至五格精确生成，避免圆形笔刷把河流涂得过宽。
        for (var i = 0; i < centers.Length; i++)
        {
            if (centers[i] == 0)
                continue;
            var width = centers[i];
            var parent = parents[i];
            var vertical = parent >= 0 && parent % Width == i % Width;
            for (var offset = -(width - 1) / 2; offset <= width / 2; offset++)
            {
                var x = i % Width + (vertical ? offset : 0);
                var y = i / Width + (vertical ? 0 : offset);
                if (!InBounds(x, y))
                    continue;
                var next = Index(x, y);
                var tile = tiles[next];
                if (tile.Value.Terrain is TerrainType.DeepWater or TerrainType.Water or TerrainType.Lake ||
                    _demoHabitat?[next] >= 0)
                    continue;
                var riverWidth = Math.Max(tile.Value.RiverWidth, width);
                var terrain = riverWidth == 1 ? TerrainType.Stream :
                    riverWidth <= 3 ? TerrainType.River : TerrainType.LargeRiver;
                if (tile.Value.Terrain != terrain || tile.Value.RiverWidth != riverWidth)
                    tile.Replace(tile.Value with { Terrain = terrain, RiverWidth = riverWidth });
            }
        }

        var distances = new int[tiles.Length];
        Array.Fill(distances, int.MaxValue);
        var queue = new Queue<int>();
        for (var i = 0; i < tiles.Length; i++)
            if (IsFreshWater(tiles[i].Value))
            {
                distances[i] = 0;
                queue.Enqueue(i);
            }

        while (queue.TryDequeue(out var current))
            foreach (var (dx, dy) in Directions)
            {
                var x = current % Width + dx;
                var y = current / Width + dy;
                if (!InBounds(x, y))
                    continue;
                var next = Index(x, y);
                if (distances[next] != int.MaxValue)
                    continue;
                distances[next] = distances[current] + 1;
                queue.Enqueue(next);
            }

        for (var i = 0; i < tiles.Length; i++)
        {
            var tile = tiles[i];
            var shore = distances[i] == int.MaxValue ? 0 : .1 * Math.Exp(-Math.Pow(distances[i] / 4d, 2));
            var generated = tile.Value.WithNaturalWaterYield(IsWaterTerrain(tile.Value.Terrain)
                ? 0 : Math.Round(tile.Value.Rainfall + shore, 6));
            if (!IsWaterTerrain(generated.Terrain))
            {
                RaceKind? demoRace = _demoHabitat is not null && _demoHabitat[i] >= 0 ? (RaceKind)_demoHabitat[i] : null;
                var variation = demoRace is null ? 0 : Noise(i % Width / 2d, i / Width / 2d, 1901);
                generated = generated.WithGeneratedBiome((i / Width + .5) / Height * 2 - 1,
                    distances[i], demoRace, variation);
            }

            tile.Replace(generated.WithResourceAmount(IsWaterTerrain(generated.Terrain) ? 0 : 50 + generated.Fertility));
            tile.Replace(tile.Value.WithGeneratedDeposit(Seed, i % Width, i / Width));
        }

        LimitMountainRanges();
    }


    private int[] PrepareDemoSites()
    {
        var sites = new List<int>();
        for (var race = 0; race < 4; race++)
        {
            var targetX = Width / 2 + (race % 2 == 0 ? -1 : 1) * Math.Clamp(Width / 8, 10, 20);
            var targetY = Height / 2 + (race < 2 ? -1 : 1) * Math.Clamp(Height / 8, 10, 20);
            var best = -1;
            var bestDistance = int.MaxValue;
            for (var y = 6; y < Height - 6; y++)
            for (var x = 6; x < Width - 6; x++)
            {
                var distance = Distance(x, y, targetX, targetY);
                if (distance >= bestDistance || sites.Any(i =>
                        Distance(x, y, i % Width, i / Width) < MinimumSettlementDistance))
                    continue;
                if (Tiles[Index(x, y)].Value.Fertility < 40 || !Circle(x, y, 6).All(i =>
                        RaceTerrainRules.For((RaceKind)race, Tiles[i].Value.Terrain).Habitable))
                    continue;
                best = Index(x, y);
                bestDistance = distance;
            }

            if (best < 0)
                break;
            sites.Add(best);
        }

        if (sites.Count == 4)
            return sites.ToArray();
        // 使用有界且确定的兜底布局，保证小地图和极端种子仍有四处独立宜居的开局区域。
        _demoHabitat = new int[Tiles.Count];
        Array.Fill(_demoHabitat, -1);
        sites.Clear();
        var left = Math.Max(7, Width / 4 - 1);
        var top = Math.Max(7, Height / 4 - 1);
        for (var race = 0; race < 4; race++)
        {
            var x = race % 2 == 0 ? left : Width - left - 1;
            var y = race < 2 ? top : Height - top - 1;
            sites.Add(Index(x, y));
            foreach (var i in Circle(x, y, 6))
            {
                _demoHabitat[i] = race;
                Tiles[i].Replace(Tiles[i].Value.WithElevation(race == (int)RaceKind.Dwarf ? (byte)160 : (byte)120));
            }
        }

        GenerateHydrology();
        _demoHabitat = null;
        return sites.ToArray();
    }
}
