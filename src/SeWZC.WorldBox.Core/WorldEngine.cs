namespace SeWZC.WorldBox.Core;

/// <summary>不依赖界面和平台的单线程世界引擎，负责生成、模拟、编辑与保存续演。</summary>
public sealed partial class WorldEngine
{
    /// <summary>世界存活居民数量的上限。</summary>
    public const int MaxPopulation = 10_000;
    private static readonly (int X, int Y)[] Directions = [(1, 0), (0, 1), (-1, 0), (0, -1)];

    private static readonly uint[] NationColors =
        [0xFFE7AD62, 0xFF63CCA7, 0xFF8C9DEB, 0xFFE27A7C, 0xFFDFC16E, 0xFFB593DB, 0xFF74BBDC, 0xFFD294C8];

    private static readonly string[] RaceNames = ["人类", "精灵", "矮人", "兽人"];
    private readonly Dictionary<int, Queue<int>> _armyPaths = [];
    private readonly Dictionary<int, int> _armyTargets = [];
    private readonly HashSet<int> _burningTiles = [];
    private readonly Dictionary<int, List<Resident>> _citizens = [];
    private readonly HashSet<int> _dryTiles = [];
    private readonly Dictionary<int, Nation> _nations = [];
    private readonly Dictionary<int, Settlement> _settlements = [];
    private readonly TerritoryCounts _territoryCounts = new();

    private bool _creatingDemo;

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

    /// <summary>引擎持有的当前可变世界状态，不是独立快照。</summary>
    public WorldState State { get; }

    /// <summary>根据种子生成世界，可选择为每个种族建立开局聚落。</summary>
    /// <param name="seed">地形生成及模拟随机序列的种子。</param>
    /// <param name="width">地图宽度，以地格为单位，范围为 32 至 256。</param>
    /// <param name="height">地图高度，以地格为单位，范围为 32 至 256。</param>
    /// <param name="demo">是否在生成的地图中建立四个开局聚落。</param>
    public static WorldEngine Create(int seed = 42, int width = 256, int height = 256, bool demo = true)
    {
        if (width is < 32 or > 256 || height is < 32 or > 256)
            throw new ArgumentOutOfRangeException(nameof(width), "地图宽高必须在 32 到 256 之间。");
        var state = new WorldState
        {
            Seed = seed, Width = width, Height = height, RandomState = (uint)seed ^ 0xA341316Cu,
            Tiles = new Tile[width * height],
        };
        if (state.RandomState == 0) state.RandomState = 1;
        var engine = new WorldEngine(state);
        engine.GenerateTerrain();
        engine.GenerateLakesAndWater();
        var demoSites = demo ? engine.PrepareDemoSites() : [];
        foreach (var tile in state.Tiles) engine.SeedPlants(tile);
        engine.SeedWildlife();
        if (demo)
        {
            engine._creatingDemo = true;
            for (var race = 0; race < 4; race++)
            {
                var location = demoSites[race];
                engine.SpawnResidents(location % width, location / width, (RaceKind)race, 36);
                var town = state.Settlements.Single(t => t.X == location % width && t.Y == location / width);
                foreach (var i in engine.Circle(town.X, town.Y, 3))
                {
                    state.Tiles[i].NationId = town.NationId;
                    state.Tiles[i].ClaimedSettlementId = town.Id;
                }
            }

            engine._creatingDemo = false;
            engine.AddEvent(WorldEventKind.Founding, "四个种族抵达这片大陆。河流、粮食与山脉将塑造他们的命运。");
        }

        engine.InitializeSociety();
        foreach (var resident in state.Residents) engine.InitializeAgent(resident);
        return engine;
    }

    private void Reindex()
    {
        _settlements.Clear();
        _nations.Clear();
        foreach (var group in _citizens.Values) group.Clear();
        foreach (var settlement in State.Settlements)
        {
            _settlements[settlement.Id] = settlement;
            if (!_citizens.ContainsKey(settlement.Id)) _citizens[settlement.Id] = [];
        }

        if (_citizens.Count != _settlements.Count)
            foreach (var id in _citizens.Keys.Where(id => !_settlements.ContainsKey(id)).ToArray())
                _citizens.Remove(id);
        foreach (var nation in State.Nations) _nations[nation.Id] = nation;
        foreach (var person in State.Residents)
            if (_citizens.TryGetValue(person.SettlementId, out var list))
                list.Add(person);
    }

    private uint RandomUInt()
    {
        // 每次取值都更新世界的随机状态，才能在存档载入后继续同一序列。
        var value = State.RandomState;
        value ^= value << 13;
        value ^= value >> 17;
        value ^= value << 5;
        State.RandomState = value;
        return value;
    }

    private int RandomInt(int maximum)
    {
        return (int)(RandomUInt() % (uint)maximum);
    }

    private double RandomDouble()
    {
        return RandomUInt() / 4294967296d;
    }

    private bool InBounds(int x, int y)
    {
        return x >= 0 && y >= 0 && x < State.Width && y < State.Height;
    }

    private int Index(int x, int y)
    {
        return y * State.Width + x;
    }

    private bool Walkable(int x, int y, RaceKind race = RaceKind.Human)
    {
        return InBounds(x, y) && RaceTerrainRules.CanWalk(State.Tiles[Index(x, y)], race);
    }

    private static int Distance(int ax, int ay, int bx, int by)
    {
        return Math.Abs(ax - bx) + Math.Abs(ay - by);
    }

    private int NewId()
    {
        return State.NextId++;
    }

    /// <summary>创建并返回编年史记录；即使该记录被容量淘汰规则立即移除，也返回该对象。</summary>
    /// <param name="kind">编年史事件类别，用于选择默认重要程度。</param>
    /// <param name="message">事件的描述文字。</param>
    /// <param name="x">事件地点的横向地格坐标，-1 表示无具体地点。</param>
    /// <param name="y">事件地点的纵向地格坐标，-1 表示无具体地点。</param>
    /// <param name="action">事件记录的具体行动。</param>
    /// <param name="settlementId">归属或待查询聚落的稳定 ID。</param>
    /// <param name="residentId">待操作居民的稳定 ID。</param>
    /// <param name="causeEventId">关联的前因事件 ID，0 表示未指定前因。</param>
    /// <param name="evidenceFactId">关联的信息依据 ID，0 表示未指定依据。</param>
    private WorldEvent AddEvent(WorldEventKind kind, string message, int x = -1, int y = -1,
        EventAction action = EventAction.General, int settlementId = 0, int residentId = 0, int causeEventId = 0,
        int evidenceFactId = 0)
    {
        var importance = kind switch
        {
            WorldEventKind.Founding => EventImportance.Historic,
            WorldEventKind.War or WorldEventKind.Disaster or WorldEventKind.Research or WorldEventKind.Diplomacy =>
                EventImportance.Major,
            WorldEventKind.Trade or WorldEventKind.Personal or WorldEventKind.Communication => EventImportance.Routine,
            _ => EventImportance.Notable,
        };
        var entry = new WorldEvent
        {
            Id = NewId(), Tick = State.Tick, Kind = kind, Message = message, X = x, Y = y, Importance = importance,
            Action = action, SettlementId = settlementId, ResidentId = residentId, CauseEventId = causeEventId,
            EvidenceFactId = evidenceFactId,
        };
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
            if (resident.DeathCause == DeathCause.None)
            {
                resident.DeathCause = DeathCause.PlayerIntervention;
                resident.DeathTick = State.Tick;
            }

            var death = AddEvent(WorldEventKind.Death,
                $"{resident.Name}逝世：{DeathCauseName(resident.DeathCause)}，终年 {resident.Age:0.0} 岁。", resident.X,
                resident.Y, residentId: resident.Id);
            death.NationId = resident.NationId;
            death.SettlementId = resident.SettlementId;
            RecordLife(resident, $"逝世原因：{DeathCauseName(resident.DeathCause)}，终年 {resident.Age:0.0} 岁。", death,
                importance: EventImportance.Major);
            if (resident.History.Count > 24) resident.History.RemoveAt(0);
            State.ArchivedResidents.Add(resident);
            State.Residents.Remove(resident);
            if (_citizens.TryGetValue(resident.SettlementId, out var citizens)) citizens.Remove(resident);
        }

        while (State.ArchivedResidents.Count > 256) State.ArchivedResidents.RemoveAt(0);
    }

    private int FindWalkable(int x, int y, int radius, RaceKind race = RaceKind.Human)
    {
        if (Walkable(x, y, race)) return Index(x, y);
        for (var r = 1; r <= radius; r++)
        {
            for (var dx = -r; dx <= r; dx++)
            {
                if (Walkable(x + dx, y - r, race)) return Index(x + dx, y - r);
                if (Walkable(x + dx, y + r, race)) return Index(x + dx, y + r);
            }

            for (var dy = -r + 1; dy < r; dy++)
            {
                if (Walkable(x - r, y + dy, race)) return Index(x - r, y + dy);
                if (Walkable(x + r, y + dy, race)) return Index(x + r, y + dy);
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
            State.Tiles[Index(x, y)] = new Tile
            {
                Terrain = elevation < .20 ? TerrainType.DeepWater :
                    elevation < .27 ? TerrainType.Water : TerrainType.Grass,
                Elevation = (byte)Math.Clamp(elevation * 255, 0, 255),
            };
        }
    }

    private double Noise(double x, double y, int salt)
    {
        var ix = (int)Math.Floor(x);
        var iy = (int)Math.Floor(y);
        var fx = x - ix;
        var fy = y - iy;
        fx = fx * fx * (3 - 2 * fx);
        fy = fy * fy * (3 - 2 * fy);

        double Hash(int a, int b)
        {
            var n = unchecked((uint)(a * 374761393 + b * 668265263 + State.Seed * 31 + salt));
            n = (n ^ (n >> 13)) * 1274126177;
            n ^= n >> 16;
            return n / 4294967295d;
        }

        return (Hash(ix, iy) * (1 - fx) + Hash(ix + 1, iy) * fx) * (1 - fy) +
               (Hash(ix, iy + 1) * (1 - fx) + Hash(ix + 1, iy + 1) * fx) * fy;
    }

    private void RefreshTotals()
    {
        ReconcileConnectedClaims();
        _territoryCounts.Bind(State.Tiles);
        foreach (var nation in State.Nations)
        {
            nation.Population = 0;
            nation.Territory = _territoryCounts.Get(nation.Id);
            nation.Resources = new ResourceStock();
        }

        foreach (var settlement in State.Settlements)
        {
            settlement.Population = _citizens.GetValueOrDefault(settlement.Id)?.Count ?? 0;
            RefreshSettlementName(settlement);
            if (!_nations.TryGetValue(settlement.NationId, out var nation)) continue;
            nation.Population += settlement.Population;
            nation.Resources.Food += settlement.Resources.Food;
            nation.Resources.Wood += settlement.Resources.Wood;
            nation.Resources.Stone += settlement.Resources.Stone;
            nation.Resources.Ore += settlement.Resources.Ore;
            nation.Resources.Alloy += settlement.Resources.Alloy;
            nation.Resources.EnergyCells += settlement.Resources.EnergyCells;
            nation.Resources.Crystals += settlement.Resources.Crystals;
            foreach (var kind in MineralAndVehicleResources)
                nation.Resources.Set(kind, nation.Resources.Get(kind) + settlement.Resources.Get(kind));
        }
    }
}
