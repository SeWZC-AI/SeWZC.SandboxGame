namespace SeWZC.WorldBox.Core;

/// <summary>Deterministic, single-threaded world simulation with no UI or platform dependencies.</summary>
public sealed partial class WorldEngine
{
    public const int MaxPopulation = 10_000;
    public WorldState State { get; }
    private readonly Dictionary<int, Settlement> _settlements = [];
    private readonly Dictionary<int, Nation> _nations = [];
    private readonly Dictionary<int, List<Resident>> _citizens = [];
    private readonly Dictionary<int, Queue<int>> _armyPaths = [];
    private readonly Dictionary<int, int> _armyTargets = [];
    private readonly HashSet<int> _burningTiles = [];
    private readonly HashSet<int> _dryTiles = [];
    private static readonly (int X, int Y)[] Directions = [(1, 0), (0, 1), (-1, 0), (0, -1)];
    private static readonly uint[] NationColors = [0xFFE7AD62, 0xFF63CCA7, 0xFF8C9DEB, 0xFFE27A7C, 0xFFDFC16E, 0xFFB593DB, 0xFF74BBDC, 0xFFD294C8];
    private static readonly string[] RaceNames = ["人类", "精灵", "矮人", "兽人"];
    private static readonly string[] PlaceNames = ["晨曦", "银叶", "铁峰", "赤牙", "河湾", "星湖", "霜原", "长风", "白桦", "暮光", "青岚", "金穗"];

    private WorldEngine(WorldState state)
    {
        State = state;
        Reindex();
        for (var i = 0; i < State.Tiles.Length; i++)
        {
            if (State.Tiles[i]?.FireTicks > 0) _burningTiles.Add(i);
            if (State.Tiles[i]?.DroughtTicks > 0) _dryTiles.Add(i);
        }
    }

    public static WorldEngine Create(int seed = 42, int width = 256, int height = 256, bool demo = true)
    {
        if (width is < 32 or > 256 || height is < 32 or > 256)
            throw new ArgumentOutOfRangeException(nameof(width), "地图宽高必须在 32 到 256 之间。");
        var state = new WorldState { Seed = seed, Width = width, Height = height, RandomState = (uint)seed ^ 0xA341316Cu, Tiles = new Tile[width * height] };
        if (state.RandomState == 0) state.RandomState = 1;
        var engine = new WorldEngine(state);
        engine.GenerateTerrain();
        if (demo)
        {
            for (var race = 0; race < 4; race++)
            {
                var x = (int)(width * (race % 2 == 0 ? 0.34 : 0.66));
                var y = (int)(height * (race < 2 ? 0.34 : 0.66));
                var location = engine.FindWalkable(x, y, Math.Max(width, height));
                if (location >= 0) engine.SpawnResidents(location % width, location / width, (RaceKind)race, 36);
            }
            engine.AddEvent(WorldEventKind.Founding, "四个种族抵达这片大陆。河流、粮食与山脉将塑造他们的命运。");
        }
        engine.InitializeSociety();
        foreach (var resident in state.Residents) engine.InitializeAgent(resident);
        return engine;
    }

    private void Reindex()
    {
        _settlements.Clear(); _nations.Clear(); _citizens.Clear();
        foreach (var settlement in State.Settlements) { _settlements[settlement.Id] = settlement; _citizens[settlement.Id] = []; }
        foreach (var nation in State.Nations) _nations[nation.Id] = nation;
        foreach (var person in State.Residents)
            if (_citizens.TryGetValue(person.SettlementId, out var list)) list.Add(person);
    }

    private uint RandomUInt()
    {
        var value = State.RandomState;
        value ^= value << 13; value ^= value >> 17; value ^= value << 5;
        State.RandomState = value;
        return value;
    }
    private int RandomInt(int maximum) => (int)(RandomUInt() % (uint)maximum);
    private double RandomDouble() => RandomUInt() / 4294967296d;
    private bool InBounds(int x, int y) => x >= 0 && y >= 0 && x < State.Width && y < State.Height;
    private int Index(int x, int y) => y * State.Width + x;
    private bool Walkable(int x, int y) => InBounds(x, y) && State.Tiles[Index(x, y)].IsWalkable;
    private static int Distance(int ax, int ay, int bx, int by) => Math.Abs(ax - bx) + Math.Abs(ay - by);
    private int NewId() => State.NextId++;
    private WorldEvent AddEvent(WorldEventKind kind, string message, int x = -1, int y = -1)
    {
        var importance = kind switch
        {
            WorldEventKind.Founding => EventImportance.Historic,
            WorldEventKind.War or WorldEventKind.Disaster => EventImportance.Major,
            WorldEventKind.Trade or WorldEventKind.Personal or WorldEventKind.Communication => EventImportance.Routine,
            _ => EventImportance.Notable
        };
        var entry = new WorldEvent { Id = NewId(), Tick = State.Tick, Kind = kind, Message = message, X = x, Y = y, Importance = importance };
        if (InBounds(x, y)) entry.NationId = State.Tiles[Index(x, y)]?.NationId ?? 0;
        State.Events.Add(entry);
        while (State.Events.Count > 400)
        {
            var expendable = State.Events.FindIndex(e => e.Importance == EventImportance.Routine);
            if (expendable < 0) expendable = State.Events.FindIndex(e => e.Importance == EventImportance.Notable);
            State.Events.RemoveAt(Math.Max(0, expendable));
        }
        return entry;
    }

    private void ArchiveDeadResidents()
    {
        foreach (var resident in State.Residents.Where(r => r.Health <= 0).ToArray())
        {
            resident.Health = 0;
            resident.History.Add(new ResidentHistoryEntry { Tick = State.Tick, Importance = EventImportance.Major, Text = "生命结束，留下的经历仍保存在人物档案中。" });
            if (resident.History.Count > 24) resident.History.RemoveAt(0);
            State.ArchivedResidents.Add(resident);
            State.Residents.Remove(resident);
        }
        while (State.ArchivedResidents.Count > 256) State.ArchivedResidents.RemoveAt(0);
    }

    private int FindWalkable(int x, int y, int radius)
    {
        if (Walkable(x, y)) return Index(x, y);
        for (var r = 1; r <= radius; r++)
        {
            for (var dx = -r; dx <= r; dx++)
            {
                if (Walkable(x + dx, y - r)) return Index(x + dx, y - r);
                if (Walkable(x + dx, y + r)) return Index(x + dx, y + r);
            }
            for (var dy = -r + 1; dy < r; dy++)
            {
                if (Walkable(x - r, y + dy)) return Index(x - r, y + dy);
                if (Walkable(x + r, y + dy)) return Index(x + r, y + dy);
            }
        }
        return -1;
    }

    private void GenerateTerrain()
    {
        for (var y = 0; y < State.Height; y++)
        for (var x = 0; x < State.Width; x++)
        {
            var nx = (x + 0.5) / State.Width * 2 - 1;
            var ny = (y + 0.5) / State.Height * 2 - 1;
            var radial = Math.Sqrt(nx * nx + ny * ny);
            var broad = Noise(x / (State.Width * 0.16), y / (State.Height * 0.16), 0);
            var fine = Noise(x / 7.0, y / 7.0, 71);
            var elevation = 0.75 - radial * 0.58 + (broad - 0.5) * 0.48 + (fine - 0.5) * 0.10;
            var moisture = Noise(x / 15.0, y / 15.0, 311);
            var terrain = elevation < 0.20 ? TerrainType.DeepWater : elevation < 0.27 ? TerrainType.Water : elevation < 0.31 ? TerrainType.Sand : elevation > 0.76 ? TerrainType.Snow : elevation > 0.66 ? TerrainType.Mountain : elevation > 0.57 ? TerrainType.Hills : Math.Abs(ny) > 0.64 ? TerrainType.Tundra : moisture < 0.29 ? TerrainType.Desert : moisture > 0.72 && elevation < 0.43 ? TerrainType.Wetland : moisture > 0.54 ? TerrainType.Forest : TerrainType.Grass;
            if (elevation is > 0.30 and < 0.61 && Math.Abs(nx - 0.22 * Math.Sin(ny * 7 + State.Seed * 0.003)) < 0.014)
                terrain = TerrainType.River;
            State.Tiles[Index(x, y)] = new Tile { Terrain = terrain, Elevation = (byte)Math.Clamp(elevation * 255, 0, 255), Fertility = TerrainRules.Fertility(terrain), ResourceAmount = terrain is TerrainType.Desert or TerrainType.Sand ? 55 : terrain == TerrainType.Wetland ? 150 : 100 };

        }
    }

    private double Noise(double x, double y, int salt)
    {
        var ix = (int)Math.Floor(x); var iy = (int)Math.Floor(y);
        var fx = x - ix; var fy = y - iy;
        fx = fx * fx * (3 - 2 * fx); fy = fy * fy * (3 - 2 * fy);
        double Hash(int a, int b)
        {
            var n = unchecked((uint)(a * 374761393 + b * 668265263 + State.Seed * 31 + salt));
            n = (n ^ (n >> 13)) * 1274126177; n ^= n >> 16;
            return n / 4294967295d;
        }
        return (Hash(ix, iy) * (1 - fx) + Hash(ix + 1, iy) * fx) * (1 - fy) + (Hash(ix, iy + 1) * (1 - fx) + Hash(ix + 1, iy + 1) * fx) * fy;
    }

    private void RefreshTotals()
    {
        foreach (var nation in State.Nations) { nation.Population = 0; nation.Territory = 0; nation.Resources = new ResourceStock(); }
        foreach (var settlement in State.Settlements)
        {
            settlement.Population = _citizens.GetValueOrDefault(settlement.Id)?.Count ?? 0;
            if (!_nations.TryGetValue(settlement.NationId, out var nation)) continue;
            nation.Population += settlement.Population;
            nation.Resources.Food += settlement.Resources.Food; nation.Resources.Wood += settlement.Resources.Wood;
            nation.Resources.Stone += settlement.Resources.Stone; nation.Resources.Ore += settlement.Resources.Ore;
        }
        foreach (var tile in State.Tiles)
            if (_nations.TryGetValue(tile.NationId, out var nation)) nation.Territory++;
    }
}
