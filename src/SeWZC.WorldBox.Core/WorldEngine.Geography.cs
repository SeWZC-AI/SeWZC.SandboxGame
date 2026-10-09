using SeWZC.WorldBox.Core.Runtime;

namespace SeWZC.WorldBox.Core;

public sealed partial class WorldEngine
{
    private int[]? _demoHabitat;

    private void GenerateHydrology()
    {
        var tiles = Current.Tiles;
        var parents = new int[tiles.Length];
        Array.Fill(parents, -1);
        var visited = new bool[tiles.Length];
        var order = new List<int>(tiles.Length);
        var flow = new double[tiles.Length];
        var frontier = new PriorityQueue<int, (int Height, int Index)>();
        for (var i = 0; i < tiles.Length; i++)
        {
            var tile = tiles[i];
            tile.RiverWidth = 0;
            tile.Terrain = tile.Elevation < 51 ? TerrainType.DeepWater :
                tile.Elevation < 69 ? TerrainType.Water : TerrainType.Grass;
            var x = i % Current.Width;
            var y = i / Current.Width;
            tile.Fertility = (byte)Math.Clamp(15 + Noise(x / 23d, y / 23d, 1201) * 85, 0, 100);
            tile.Rainfall = Math.Round(.002 + Math.Pow(Noise(x / 27d, y / 27d, 1601), 3) * .24, 6);
            flow[i] = .2 + tile.Rainfall * 15;
            if (!IsWaterTerrain(tile.Terrain))
                continue;
            visited[i] = true;
            frontier.Enqueue(i, (tile.Elevation, i));
        }

        // 优先洪泛为平坦盆地也建立通向海洋的无环出口，出队顺序可直接用于汇流计算。
        while (frontier.TryDequeue(out var current, out var priority))
        {
            order.Add(current);
            foreach (var (dx, dy) in Directions)
            {
                var x = current % Current.Width + dx;
                var y = current / Current.Width + dy;
                if (!InBounds(x, y))
                    continue;
                var next = Index(x, y);
                if (visited[next])
                    continue;
                visited[next] = true;
                parents[next] = current;
                frontier.Enqueue(next, (Math.Max(priority.Height, tiles[next].Elevation), next));
            }
        }

        for (var p = order.Count - 1; p >= 0; p--)
            if (parents[order[p]] >= 0)
                flow[parents[order[p]]] += flow[order[p]];

        for (var y = 7; y < Current.Height - 7; y += 12)
        for (var x = 7; x < Current.Width - 7; x += 12)
        {
            if (Noise(x, y, 977) < .64)
                continue;
            var area = Circle(x, y, 3).ToArray();
            if (area.Any(i =>
                    IsWaterTerrain(tiles[i].Terrain) || tiles[i].Elevation > 178 || _demoHabitat?[i] >= 0))
                continue;
            var radius = Noise(x, y, 983) > .7 ? 2 : 1;
            foreach (var i in Circle(x, y, radius))
                tiles[i].Terrain = TerrainType.Lake;
        }

        var sources = Enumerable.Range(0, tiles.Length)
            .Where(i => parents[i] >= 0 && !IsWaterTerrain(tiles[i].Terrain) && tiles[i].Elevation >= 120 &&
                        flow[i] < 12)
            .OrderByDescending(i =>
                tiles[i].Elevation + Noise(i % Current.Width / 7d, i / Current.Width / 7d, 1879) * 65)
            .ThenBy(i => i);
        var selected = new List<int>();
        var desired = Math.Clamp(tiles.Length / 4096 + 2, 2, 12);
        var centers = new byte[tiles.Length];
        foreach (var source in sources)
        {
            if (selected.Any(i =>
                    Distance(i % Current.Width, i / Current.Width, source % Current.Width, source / Current.Width) <
                    Math.Max(8, Current.Width / 10)))
                continue;
            var path = new List<int>();
            var current = source;
            var reserved = false;
            while (current >= 0 && tiles[current].Terrain is not (TerrainType.DeepWater or TerrainType.Water))
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
            var vertical = parent >= 0 && parent % Current.Width == i % Current.Width;
            for (var offset = -(width - 1) / 2; offset <= width / 2; offset++)
            {
                var x = i % Current.Width + (vertical ? offset : 0);
                var y = i / Current.Width + (vertical ? 0 : offset);
                if (!InBounds(x, y))
                    continue;
                var next = Index(x, y);
                var tile = tiles[next];
                if (tile.Terrain is TerrainType.DeepWater or TerrainType.Water or TerrainType.Lake ||
                    _demoHabitat?[next] >= 0)
                    continue;
                var riverWidth = Math.Max(tile.RiverWidth, width);
                tile.Terrain = riverWidth == 1 ? TerrainType.Stream :
                    riverWidth <= 3 ? TerrainType.River : TerrainType.LargeRiver;
                tile.RiverWidth = riverWidth;
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
                var x = current % Current.Width + dx;
                var y = current / Current.Width + dy;
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
            tile.NaturalWaterYield = IsWaterTerrain(tile.Terrain) ? 0 : Math.Round(tile.Rainfall + shore, 6);
            if (!IsWaterTerrain(tile.Terrain))
                tile.Terrain = GeneratedBiome(tile, i, distances[i]);
            tile.ResourceAmount = IsWaterTerrain(tile.Terrain) ? 0 : 50 + tile.Fertility;
            SeedDeposit(tile, i % Current.Width, i / Current.Width);
        }

        LimitMountainRanges();
    }

    private TerrainType GeneratedBiome(TileCursor tile, int index, int waterDistance)
    {
        var y = index / Current.Width;
        if (_demoHabitat is not null && index >= 0 && _demoHabitat[index] >= 0)
        {
            var race = (RaceKind)_demoHabitat[index];
            tile.Replace(tile.Value with
            {
                Fertility = race == RaceKind.Dwarf ? (byte)60 : race == RaceKind.Orc ? (byte)55 : (byte)80,
                Rainfall = race == RaceKind.Elf ? .096 :
                race == RaceKind.Orc ? .012 :
                race == RaceKind.Dwarf ? .064 : .036,
            });
            var variation = Noise(index % Current.Width / 2d, index / Current.Width / 2d, 1901);
            if (variation > .58)
            {
                tile.Replace(tile.Value with { Elevation = 120, Rainfall = .064 });
                tile.NaturalWaterYield = tile.Rainfall;
                return TerrainType.Woodland;
            }

            if (variation < .35 && race is RaceKind.Human or RaceKind.Orc)
            {
                tile.Replace(tile.Value with { Elevation = 160, Fertility = 50, Rainfall = .024 });
                tile.NaturalWaterYield = tile.Rainfall;
                return TerrainType.Hills;
            }

            tile.NaturalWaterYield = tile.Rainfall;
            return race switch
            {
                RaceKind.Elf => TerrainType.Forest,
                RaceKind.Dwarf => TerrainType.AlpineMeadow,
                RaceKind.Orc => TerrainType.Savanna,
                _ => TerrainType.Meadow,
            };
        }

        if (tile.Elevation < 79)
            return TerrainType.Sand;
        if (tile.Elevation > 197)
            return TerrainType.Snow;
        if (tile.Elevation > 180)
            return TerrainType.Mountain;
        if (tile.Elevation > 149)
        {
            return tile.Fertility >= 55 && tile.NaturalWaterYield >= .032
                ? TerrainType.AlpineMeadow
                : TerrainType.Hills;
        }

        if (Math.Abs((y + .5) / Current.Height * 2 - 1) > .72)
            return TerrainType.Tundra;
        if (tile.NaturalWaterYield < .016)
        {
            return tile.Fertility >= 65 ? TerrainType.DryFertile :
                tile.Fertility < 30 ? TerrainType.Desert : TerrainType.Savanna;
        }

        if (tile.Fertility < 30)
            return TerrainType.Scrub;
        if (waterDistance <= 3 && tile.Fertility >= 70 && tile.Elevation < 135)
            return TerrainType.Floodplain;
        if (tile.NaturalWaterYield >= .132)
            return tile.Fertility >= 65 ? TerrainType.Rainforest : TerrainType.Wetland;
        if (tile.NaturalWaterYield >= .076)
            return TerrainType.Forest;
        if (tile.NaturalWaterYield >= .044)
            return TerrainType.Woodland;
        return tile.Fertility >= 70 ? TerrainType.Meadow : TerrainType.Grass;
    }

    private int[] PrepareDemoSites()
    {
        var sites = new List<int>();
        for (var race = 0; race < 4; race++)
        {
            var targetX = Current.Width / 2 + (race % 2 == 0 ? -1 : 1) * Math.Clamp(Current.Width / 8, 10, 20);
            var targetY = Current.Height / 2 + (race < 2 ? -1 : 1) * Math.Clamp(Current.Height / 8, 10, 20);
            var best = -1;
            var bestDistance = int.MaxValue;
            for (var y = 6; y < Current.Height - 6; y++)
            for (var x = 6; x < Current.Width - 6; x++)
            {
                var distance = Distance(x, y, targetX, targetY);
                if (distance >= bestDistance || sites.Any(i =>
                        Distance(x, y, i % Current.Width, i / Current.Width) < MinimumSettlementDistance))
                    continue;
                if (Current.Tiles[Index(x, y)].Fertility < 40 || !Circle(x, y, 6).All(i =>
                        RaceTerrainRules.For((RaceKind)race, Current.Tiles[i].Terrain).Habitable))
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
        _demoHabitat = new int[Current.Tiles.Count];
        Array.Fill(_demoHabitat, -1);
        sites.Clear();
        var left = Math.Max(7, Current.Width / 4 - 1);
        var top = Math.Max(7, Current.Height / 4 - 1);
        for (var race = 0; race < 4; race++)
        {
            var x = race % 2 == 0 ? left : Current.Width - left - 1;
            var y = race < 2 ? top : Current.Height - top - 1;
            sites.Add(Index(x, y));
            foreach (var i in Circle(x, y, 6))
            {
                _demoHabitat[i] = race;
                Current.Tiles[i].Elevation = race == (int)RaceKind.Dwarf ? (byte)160 : (byte)120;
            }
        }

        GenerateHydrology();
        _demoHabitat = null;
        return sites.ToArray();
    }
}
