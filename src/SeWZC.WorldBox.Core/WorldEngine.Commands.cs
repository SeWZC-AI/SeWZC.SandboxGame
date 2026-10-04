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
            tile.Improvement = LandImprovement.None; tile.RoadLevel = 0; tile.BridgeLevel = 0;
            tile.LastHarvestTick = 0; tile.Harvested = 0; SeedDeposit(tile, index % State.Width, index / State.Width);
            tile.Fertility = TerrainRules.Fertility(terrain);
            tile.NaturalWaterYield = terrain == TerrainType.Wetland ? 1 : terrain == TerrainType.DryFertile ? .001 : IsWaterTerrain(terrain) ? 0 : .004;
            tile.Rainfall = tile.NaturalWaterYield;
            tile.RiverWidth = terrain == TerrainType.Stream ? (byte)1 : terrain == TerrainType.River ? (byte)2 : terrain == TerrainType.LargeRiver ? (byte)4 : (byte)0;
            SeedPlants(tile);
            tile.ResourceAmount = 100;
            tile.Elevation = (byte)(terrain switch { TerrainType.DeepWater => 10, TerrainType.Water => 50, TerrainType.Sand => 75, TerrainType.Mountain => 210, TerrainType.Snow => 240, _ => 110 });
            if (!tile.IsWalkable || IsWaterTerrain(tile.Terrain)) { tile.NationId = 0; tile.ClaimedSettlementId = 0; tile.FireTicks = 0; tile.RoadLevel = 0; _burningTiles.Remove(index); }
        }
        _armyPaths.Clear();
        RelocateInvalidEntities();
        Reindex(); InitializeSociety(); RefreshTotals();
    }

    public void SpawnResidents(int x, int y, RaceKind race, int count = 12)
    {
        if (!Enum.IsDefined(race)) throw new ArgumentOutOfRangeException(nameof(race));
        if (!InBounds(x, y)) return;
        var index = FindWalkable(x, y, 8, race);
        if (index < 0 || State.Residents.Count >= MaxPopulation) return;
        x = index % State.Width; y = index / State.Width;
        count = Math.Clamp(count, 1, Math.Min(200, MaxPopulation - State.Residents.Count));
        var owner = State.Tiles[index].NationId;
        var settlement = State.Settlements.Where(s => owner > 0 ? s.NationId == owner : Distance(s.X, s.Y, x, y) < MinimumSettlementDistance).OrderBy(s => Distance(s.X, s.Y, x, y)).FirstOrDefault();
        if (settlement is null)
        {
            if (State.Nations.Count >= 64 || State.Settlements.Count >= 256) return;
            var nation = new Nation { Id = NewId(), FoundingRace = race, Name = NewPlaceName("王国"), ColorArgb = NationColors[State.Nations.Count % NationColors.Length] };
            settlement = new Settlement { Id = NewId(), Name = NewPlaceName("村"), X = x, Y = y, NationId = nation.Id, Resources = new ResourceStock { Food = count * 8, Water = count * 5, Wood = 80, Stone = 45, Ore = 12 } };
            nation.CapitalId = settlement.Id;
            foreach (var other in State.Nations)
            {
                var opinion = RandomInt(41) - 10;
                State.Diplomacies.Add(new DiplomaticRelation { FirstNationId = other.Id, SecondNationId = nation.Id,
                    Opinion = opinion, FirstOpinion = opinion, SecondOpinion = opinion });
            }
            State.Nations.Add(nation); State.Settlements.Add(settlement);
            _nations[nation.Id] = nation; _settlements[settlement.Id] = settlement; _citizens[settlement.Id] = [];
            State.Tiles[Index(x, y)].SettlementId = settlement.Id;
            ClaimTerritory(settlement, 6);
            AddEvent(WorldEventKind.Founding, $"{RaceNames[(int)race]}在{settlement.Name}定居，建立了{nation.Name}。", x, y);
        }
        // Founding families start on the same connected shore as their camp.
        var spawnSites = new List<int> { index }; var spawnSeen = new HashSet<int> { index };
        for (var site = 0; site < spawnSites.Count; site++)
            foreach (var (dx, dy) in Directions)
            {
                var xx = spawnSites[site] % State.Width + dx; var yy = spawnSites[site] / State.Width + dy;
                if (!InBounds(xx, yy) || Distance(x, y, xx, yy) > 3
                    || !CanTraverseStep(spawnSites[site] % State.Width, spawnSites[site] / State.Width, xx, yy, TravelMode.Foot, race)) continue;
                var next = Index(xx, yy); if (spawnSeen.Add(next)) spawnSites.Add(next);
            }
        for (var i = 0; i < count; i++)
        {
            var person = NewResident(settlement, race, 16 + RandomInt(28));
            var position = spawnSites[RandomInt(spawnSites.Count)];
            person.X = person.FromX = position % State.Width; person.Y = person.FromY = position / State.Width;
            State.Residents.Add(person); _citizens[settlement.Id].Add(person);
            // Equal starting rations, independent of profession and list order.
            var food = State.Rules.Hunger ? Math.Min(settlement.Resources.Food, 1) : 0;
            var water = State.Rules.Thirst ? Math.Min(settlement.Resources.Water, .75) : 0;
            settlement.Resources.Food -= food; person.Inventory.Food += food;
            settlement.Resources.Water -= water; person.Inventory.Water += water;
        }
        InitializeSociety();
        foreach (var person in _citizens[settlement.Id]) InitializeAgent(person);
        RefreshTotals();
    }

    private Resident NewResident(Settlement settlement, RaceKind race, double age)
    {
        var id = NewId();
        return new Resident { Id = id, Name = NewResidentName(id, race), Race = race, X = settlement.X, Y = settlement.Y, FromX = settlement.X, FromY = settlement.Y, Age = age, CultureId = settlement.CultureId, NationId = settlement.NationId, SettlementId = settlement.Id, Profession = age < 14 ? Profession.Child : AssignProfession(), MagicTalent = (race == RaceKind.Elf ? 45 : race == RaceKind.Dwarf ? 23 : race == RaceKind.Orc ? 28 : 32) + (unchecked((uint)id * 2654435761u ^ (uint)State.Seed) % 36), Trait = new[] { "勤劳", "勇敢", "好奇", "坚韧", "温和" }[RandomInt(5)] };
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
            if (kind == DisasterKind.Drought && tile.IsWalkable) { tile.DroughtTicks = 150; _dryTiles.Add(index); }
        }
        if (kind == DisasterKind.Fire)
        {
            var started = 0; var seeds = Math.Clamp(1 + radius / 10, 1, 3);
            foreach (var index in Circle(x, y, radius).OrderBy(i => Distance(x, y, i % State.Width, i / State.Width)).ThenBy(i => i))
                if (Ignite(index) && ++started >= seeds) break;
            if (started == 0) return;
        }
        if (kind == DisasterKind.Meteor)
        {
            foreach (var index in Circle(x, y, radius))
            {
                var tile = State.Tiles[index];
                if (!tile.IsWalkable) continue;
                tile.Terrain = TerrainType.Sand; tile.RiverWidth = 0; tile.Improvement = LandImprovement.None; tile.Fertility = 5; tile.ResourceAmount = 0; tile.RoadLevel = 0; tile.BridgeLevel = 0; tile.Plants = default;
                tile.FireTicks = 12; _burningTiles.Add(index);
            }
            foreach (var resident in State.Residents.Where(r => Distance(r.X, r.Y, x, y) <= radius))
                DamageResident(resident, 65, DeathCause.Meteor);
            foreach (var building in State.Society.Buildings.Where(b => Distance(b.X, b.Y, x, y) <= radius))
                building.Health = Math.Max(0, building.Health - 80);
        }
        if (kind is DisasterKind.Drought or DisasterKind.Meteor)
            EmitVisual(kind == DisasterKind.Drought ? WorldVisualKind.Drought : WorldVisualKind.Meteor, x, y, radius);
        if (kind == DisasterKind.Plague)
            foreach (var resident in State.Residents.Where(r => r.Health > 0 && r.SicknessTicks == 0
                && r.DiseaseImmuneUntilTick <= State.Tick && Distance(r.X, r.Y, x, y) <= radius)
                .OrderBy(r => Distance(r.X, r.Y, x, y)).ThenBy(r => r.Id).Take(Math.Clamp(1 + radius / 10, 1, 3)))
            {
                resident.SicknessTicks = 72 + RandomInt(25);
                EmitVisual(WorldVisualKind.Plague, resident.X, resident.Y, 1);
            }
        var label = kind == DisasterKind.Fire ? "局部起火，火势将按地形和建筑可燃性逐步蔓延" : kind == DisasterKind.Drought ? "旱灾来临，农田减产，粮食储备将经受考验" : kind == DisasterKind.Plague ? "出现少量疫病病例，后续传播取决于居民实际接触" : "陨石撞击大地，摧毁植被与道路，重创居民和建筑";
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

    public void SetNationResources(int nationId, double? food = null, double? wood = null, double? stone = null, double? ore = null, double? alloy = null, double? energyCells = null, double? crystals = null, double? coal = null, double? oil = null, double? rareEarth = null, double? boats = null, double? aircraft = null, double? water = null)
    {
        if (!_nations.ContainsKey(nationId)) throw new ArgumentException("国家不存在。", nameof(nationId));
        var amounts = new[] { food, wood, stone, ore, alloy, energyCells, crystals, coal, oil, rareEarth, boats, aircraft, water };
        if (amounts.Any(v => v.HasValue && (!double.IsFinite(v.Value) || v.Value < 0 || v.Value > 1_000_000))) throw new ArgumentOutOfRangeException(nameof(food), "资源须在 0 到 1,000,000 之间。");
        if (amounts.All(v => !v.HasValue)) return;
        var towns = State.Settlements.Where(s => s.NationId == nationId).ToArray();
        if (towns.Length == 0) return;
        foreach (var town in towns)
        {
            if (food is { } f) town.Resources.Food = f / towns.Length;
            if (wood is { } w) town.Resources.Wood = w / towns.Length;
            if (stone is { } s) town.Resources.Stone = s / towns.Length;
            if (ore is { } o) town.Resources.Ore = o / towns.Length;
            if (alloy is { } a) town.Resources.Alloy = a / towns.Length;
            if (energyCells is { } e) town.Resources.EnergyCells = e / towns.Length;
            if (crystals is { } c) town.Resources.Crystals = c / towns.Length;
            if (coal is { } co) town.Resources.Coal = co / towns.Length;
            if (oil is { } oi) town.Resources.Oil = oi / towns.Length;
            if (rareEarth is { } re) town.Resources.RareEarth = re / towns.Length;
            if (boats is { } bo) town.Resources.Boats = bo / towns.Length;
            if (aircraft is { } ai) town.Resources.Aircraft = ai / towns.Length;
            if (water is { } wa) town.Resources.Water = wa / towns.Length;
        }
        RefreshTotals();
        AddEvent(WorldEventKind.Editor, $"{_nations[nationId].Name}的资源储备已调整。");
    }

    public void SetDiplomacy(int first, int second, DiplomaticStatus status)
    {
        if (first == second || !_nations.ContainsKey(first) || !_nations.ContainsKey(second)) throw new ArgumentException("请选择两个不同且存在的国家。");
        if (!Enum.IsDefined(status)) throw new ArgumentOutOfRangeException(nameof(status));
        var relation = Relation(first, second);
        relation.Status = status;
        relation.FirstOpinion = relation.SecondOpinion = relation.Opinion = status == DiplomaticStatus.War ? -80 : status == DiplomaticStatus.Allied ? 80 : 0;
        relation.LastChangedTick = State.Tick; relation.Reason = "玩家直接调整外交关系"; relation.AllianceOfferNationId = 0;
        var diplomaticEvent = AddEvent(status == DiplomaticStatus.War ? WorldEventKind.War : WorldEventKind.Diplomacy, $"{_nations[first].Name}与{_nations[second].Name}{(status == DiplomaticStatus.War ? "开战" : status == DiplomaticStatus.Allied ? "结盟" : "恢复中立关系")}。");
        diplomaticEvent.NationId = first; diplomaticEvent.SecondNationId = second; diplomaticEvent.Action = EventAction.Declaration;
        diplomaticEvent.CauseEventId = relation.LastEventId;
        PublishDiplomaticOrder(first, second, status, eventId: diplomaticEvent.Id);
        PublishDiplomaticOrder(second, first, status, eventId: diplomaticEvent.Id, objective: WarObjective.DefendHomeland);
        relation.LastEventId = diplomaticEvent.Id;
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
            var center = State.Tiles[Index(settlement.X, settlement.Y)];
            if (center.IsWalkable && !IsWaterTerrain(center.Terrain) || center.Terrain == TerrainType.Mountain
                && _citizens.GetValueOrDefault(settlement.Id)?.Any(p => p.Race == RaceKind.Dwarf && p.Health > 0 && p.Age >= 14) == true)
            {
                ClaimTerritory(settlement, 6);
                continue;
            }
            State.Tiles[Index(settlement.X, settlement.Y)].SettlementId = 0;
            var position = Circle(settlement.X, settlement.Y, 12)
                .Where(i => State.Tiles[i].IsWalkable && !IsWaterTerrain(State.Tiles[i].Terrain) && State.Tiles[i].SettlementId == 0 && !State.Society.Buildings.Any(b => b.X == i % State.Width && b.Y == i / State.Width) && (State.Tiles[i].NationId == 0 || State.Tiles[i].NationId == settlement.NationId))
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
            if (CanTraverse(State.Tiles[Index(resident.X, resident.Y)], resident.TravelMode, resident.Race)) continue;
            var position = FindWalkable(resident.X, resident.Y, 10, resident.Race);
            if (position >= 0) { resident.X = position % State.Width; resident.Y = position / State.Width; DamageResident(resident, 15, DeathCause.TerrainChange); }
            else DamageResident(resident, resident.Health, IsWaterTerrain(State.Tiles[Index(resident.X, resident.Y)].Terrain) ? DeathCause.Drowning : DeathCause.TerrainChange);
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

}
