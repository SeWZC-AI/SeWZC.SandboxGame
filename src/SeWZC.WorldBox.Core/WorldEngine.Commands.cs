namespace SeWZC.WorldBox.Core;

public sealed partial class WorldEngine
{
    public void PaintTerrain(int x, int y, TerrainType terrain, int radius = 2)
    {
        if (!Enum.IsDefined(terrain)) throw new ArgumentOutOfRangeException(nameof(terrain));
        if (!InBounds(x, y)) return;
        radius = Math.Clamp(radius, 0, 32);
        foreach (var index in Circle(x, y, radius))
        {
            var tile = State.Tiles[index];
            tile.Terrain = terrain;
            tile.Fertility = TerrainRules.Fertility(terrain);
            tile.ResourceAmount = 100;
            tile.Elevation = (byte)(terrain switch { TerrainType.DeepWater => 10, TerrainType.Water => 50, TerrainType.Sand => 75, TerrainType.Mountain => 210, TerrainType.Snow => 240, _ => 110 });
            if (!tile.IsWalkable) { tile.NationId = 0; tile.FireTicks = 0; tile.RoadLevel = 0; _burningTiles.Remove(index); }
        }
        _armyPaths.Clear();
        RelocateInvalidEntities();
        Reindex(); InitializeSociety(); RefreshTotals();
    }

    public void SpawnResidents(int x, int y, RaceKind race, int count = 12)
    {
        if (!Enum.IsDefined(race)) throw new ArgumentOutOfRangeException(nameof(race));
        if (!InBounds(x, y)) return;
        var index = FindWalkable(x, y, 8);
        if (index < 0 || State.Residents.Count >= MaxPopulation) return;
        x = index % State.Width; y = index / State.Width;
        count = Math.Clamp(count, 1, Math.Min(200, MaxPopulation - State.Residents.Count));
        var owner = State.Tiles[index].NationId;
        var settlement = State.Settlements.Where(s => owner > 0 ? s.NationId == owner : Distance(s.X, s.Y, x, y) <= 8).OrderBy(s => Distance(s.X, s.Y, x, y)).FirstOrDefault();
        if (settlement is null)
        {
            if (State.Nations.Count >= 64 || State.Settlements.Count >= 256) return;
            var nation = new Nation { Id = NewId(), FoundingRace = race, Name = PlaceNames[State.Nations.Count % PlaceNames.Length] + "王国", ColorArgb = NationColors[State.Nations.Count % NationColors.Length] };
            settlement = new Settlement { Id = NewId(), Name = PlaceNames[State.Nations.Count % PlaceNames.Length] + "村", X = x, Y = y, NationId = nation.Id, Resources = new ResourceStock { Food = count * 4, Wood = 25, Stone = 12 } };
            nation.CapitalId = settlement.Id;
            foreach (var other in State.Nations)
                State.Diplomacies.Add(new DiplomaticRelation { FirstNationId = other.Id, SecondNationId = nation.Id, Opinion = RandomInt(41) - 10 });
            State.Nations.Add(nation); State.Settlements.Add(settlement);
            _nations[nation.Id] = nation; _settlements[settlement.Id] = settlement; _citizens[settlement.Id] = [];
            State.Tiles[Index(x, y)].SettlementId = settlement.Id;
            ClaimTerritory(settlement, 6);
            AddEvent(WorldEventKind.Founding, $"{RaceNames[(int)race]}在{settlement.Name}定居，建立了{nation.Name}。", x, y);
        }
        for (var i = 0; i < count; i++)
        {
            var person = NewResident(settlement, race, 16 + RandomInt(28));
            var position = FindWalkable(x + RandomInt(7) - 3, y + RandomInt(7) - 3, 4);
            if (position >= 0) { person.X = position % State.Width; person.Y = position / State.Width; }
            State.Residents.Add(person); _citizens[settlement.Id].Add(person);
        }
        InitializeSociety();
        foreach (var person in _citizens[settlement.Id]) InitializeAgent(person);
        RefreshTotals();
    }

    private Resident NewResident(Settlement settlement, RaceKind race, double age)
    {
        var id = NewId();
        return new Resident { Id = id, Name = $"{RaceNames[(int)race]}·{id}", Race = race, X = settlement.X, Y = settlement.Y, FromX = settlement.X, FromY = settlement.Y, Age = age, CultureId = settlement.CultureId, NationId = settlement.NationId, SettlementId = settlement.Id, Profession = age < 14 ? Profession.Child : AssignProfession(), MagicTalent = (race == RaceKind.Elf ? 45 : race == RaceKind.Dwarf ? 23 : race == RaceKind.Orc ? 28 : 32) + (unchecked((uint)id * 2654435761u ^ (uint)State.Seed) % 36), Trait = new[] { "勤劳", "勇敢", "好奇", "坚韧", "温和" }[RandomInt(5)] };
    }

    private Profession AssignProfession()
    {
        var roll = RandomInt(100);
        return roll < 45 ? Profession.Farmer : roll < 63 ? Profession.Lumberjack : roll < 78 ? Profession.Miner : roll < 85 ? Profession.Builder : roll < 90 ? Profession.Trader : roll < 94 ? Profession.Messenger : roll < 97 ? Profession.Scholar : roll < 99 ? Profession.Mage : Profession.Representative;
    }

    public void TriggerDisaster(int x, int y, DisasterKind kind, int radius = 5)
    {
        if (!Enum.IsDefined(kind)) throw new ArgumentOutOfRangeException(nameof(kind));
        if (!InBounds(x, y)) return;
        radius = Math.Clamp(radius, 1, 32);
        foreach (var index in Circle(x, y, radius))
        {
            var tile = State.Tiles[index];
            if (kind == DisasterKind.Fire && tile.IsWalkable) { tile.FireTicks = 16 + RandomInt(16); _burningTiles.Add(index); }
            if (kind == DisasterKind.Drought && tile.IsWalkable) { tile.DroughtTicks = 150; _dryTiles.Add(index); }
        }
        if (kind == DisasterKind.Plague)
            foreach (var resident in State.Residents)
                if (Distance(resident.X, resident.Y, x, y) <= radius * 1.4) resident.SicknessTicks = 45 + RandomInt(40);
        var label = kind == DisasterKind.Fire ? "火灾吞噬草木，威胁附近居民" : kind == DisasterKind.Drought ? "旱灾来临，农田减产，粮食储备将经受考验" : "疫病扩散，患病居民的健康与生产力下降";
        AddEvent(WorldEventKind.Disaster, label + "。", x, y);
    }

    public void RenameNation(int nationId, string name)
    {
        if (!_nations.TryGetValue(nationId, out var nation)) throw new ArgumentException("国家不存在。", nameof(nationId));
        name = (name ?? "").Trim();
        if (name.Length is < 1 or > 40 || name.Any(char.IsControl)) throw new ArgumentException("国名须为 1–40 个可见字符。", nameof(name));
        var previous = nation.Name; nation.Name = name;
        AddEvent(WorldEventKind.Editor, $"{previous}更名为{name}。");
    }

    public void SetNationResources(int nationId, double food, double wood, double stone, double ore)
    {
        if (!_nations.ContainsKey(nationId)) throw new ArgumentException("国家不存在。", nameof(nationId));
        if (new[] { food, wood, stone, ore }.Any(v => !double.IsFinite(v) || v < 0 || v > 1_000_000)) throw new ArgumentOutOfRangeException(nameof(food), "资源须在 0 到 1,000,000 之间。");
        var towns = State.Settlements.Where(s => s.NationId == nationId).ToArray();
        if (towns.Length == 0) return;
        foreach (var town in towns) town.Resources = new ResourceStock { Food = food / towns.Length, Wood = wood / towns.Length, Stone = stone / towns.Length, Ore = ore / towns.Length };
        RefreshTotals();
        AddEvent(WorldEventKind.Editor, $"{_nations[nationId].Name}的资源储备已调整。");
    }

    public void SetDiplomacy(int first, int second, DiplomaticStatus status)
    {
        if (first == second || !_nations.ContainsKey(first) || !_nations.ContainsKey(second)) throw new ArgumentException("请选择两个不同且存在的国家。");
        if (!Enum.IsDefined(status)) throw new ArgumentOutOfRangeException(nameof(status));
        var relation = Relation(first, second);
        relation.Status = status; relation.Opinion = status == DiplomaticStatus.War ? -80 : status == DiplomaticStatus.Allied ? 80 : 0;
        PublishDiplomaticOrder(first, second, status);
        PublishDiplomaticOrder(second, first, status);
        var diplomaticEvent = AddEvent(status == DiplomaticStatus.War ? WorldEventKind.War : WorldEventKind.Diplomacy, $"{_nations[first].Name}与{_nations[second].Name}{(status == DiplomaticStatus.War ? "开战" : status == DiplomaticStatus.Allied ? "结盟" : "恢复中立关系")}。");
        diplomaticEvent.NationId = first; diplomaticEvent.SecondNationId = second;
    }

    public DiplomaticStatus GetDiplomacy(int first, int second) => first == second ? DiplomaticStatus.Allied : State.Diplomacies.FirstOrDefault(r => r.FirstNationId == first && r.SecondNationId == second || r.FirstNationId == second && r.SecondNationId == first)?.Status ?? DiplomaticStatus.Neutral;

    private DiplomaticRelation Relation(int first, int second)
    {
        var relation = State.Diplomacies.FirstOrDefault(r => r.FirstNationId == first && r.SecondNationId == second || r.FirstNationId == second && r.SecondNationId == first);
        if (relation is not null) return relation;
        relation = new DiplomaticRelation { FirstNationId = Math.Min(first, second), SecondNationId = Math.Max(first, second) };
        State.Diplomacies.Add(relation); return relation;
    }

    private IEnumerable<int> Circle(int cx, int cy, int radius)
    {
        for (var y = Math.Max(0, cy - radius); y <= Math.Min(State.Height - 1, cy + radius); y++)
        for (var x = Math.Max(0, cx - radius); x <= Math.Min(State.Width - 1, cx + radius); x++)
            if ((x - cx) * (x - cx) + (y - cy) * (y - cy) <= radius * radius) yield return Index(x, y);
    }

    private void RelocateInvalidEntities()
    {
        foreach (var settlement in State.Settlements.ToArray())
        {
            if (Walkable(settlement.X, settlement.Y)) continue;
            State.Tiles[Index(settlement.X, settlement.Y)].SettlementId = 0;
            var position = Circle(settlement.X, settlement.Y, 12)
                .Where(i => State.Tiles[i].IsWalkable && State.Tiles[i].SettlementId == 0 && (State.Tiles[i].NationId == 0 || State.Tiles[i].NationId == settlement.NationId))
                .OrderBy(i => Distance(i % State.Width, i / State.Width, settlement.X, settlement.Y))
                .FirstOrDefault(-1);
            if (position >= 0)
            {
                settlement.X = position % State.Width; settlement.Y = position / State.Width;
                State.Tiles[position].SettlementId = settlement.Id;
                ClaimTerritory(settlement, 6);
                AddEvent(WorldEventKind.Editor, $"地形改变，{settlement.Name}迁往可居住的土地。", settlement.X, settlement.Y);
            }
            else RemoveSettlement(settlement, "家园被地形变化摧毁");
        }
        foreach (var resident in State.Residents)
        {
            if (Walkable(resident.X, resident.Y)) continue;
            var position = FindWalkable(resident.X, resident.Y, 10);
            if (position >= 0) { resident.X = position % State.Width; resident.Y = position / State.Width; resident.Health -= 15; }
            else resident.Health = 0;
        }
        ArchiveDeadResidents();
        foreach (var army in State.Armies.ToArray())
        {
            if (Walkable(army.X, army.Y)) continue;
            var position = FindWalkable(army.X, army.Y, 10);
            if (position >= 0) { army.X = position % State.Width; army.Y = position / State.Width; }
            else DisbandArmy(army);
        }
        RemoveEmptyNations();
    }

    private void ClaimTerritory(Settlement settlement, int radius)
    {
        var start = Index(settlement.X, settlement.Y);
        var visited = new HashSet<int> { start }; var queue = new Queue<int>(); queue.Enqueue(start);
        while (queue.TryDequeue(out var index))
        {
            var tile = State.Tiles[index];
            if (!tile.IsWalkable || tile.NationId != 0 && tile.NationId != settlement.NationId) continue;
            tile.NationId = settlement.NationId;
            var x = index % State.Width; var y = index / State.Width;
            foreach (var (dx, dy) in Directions)
            {
                var xx = x + dx; var yy = y + dy;
                if (!InBounds(xx, yy) || Distance(xx, yy, settlement.X, settlement.Y) > radius) continue;
                var next = Index(xx, yy); if (visited.Add(next)) queue.Enqueue(next);
            }
        }
    }
}
