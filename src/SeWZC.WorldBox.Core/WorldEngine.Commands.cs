using SeWZC.WorldBox.Core.Runtime;

namespace SeWZC.WorldBox.Core;

public sealed partial class WorldEngine
{
    /// <summary>替换圆形笔刷范围内的地形，重置当地资源和地块改良，并处理位置失效的实体。</summary>
    /// <param name="x">横向地格坐标。</param>
    /// <param name="y">纵向地格坐标。</param>
    /// <param name="terrain">地形类别。</param>
    /// <param name="radius">笔刷作用半径，以地格为单位。</param>
    public void PaintTerrain(int x, int y, TerrainType terrain, int radius = 2)
    {
        if (!Enum.IsDefined(terrain))
            throw new ArgumentOutOfRangeException(nameof(terrain));
        if (!InBounds(x, y))
            return;
        radius = Math.Clamp(radius, 0, 32);
        foreach (var index in Circle(x, y, radius))
        {
            var tile = Tiles[index];
            tile.Replace(tile.Value.WithTerrain(terrain));
            tile.Replace(tile.Value with
            {
                Improvement = LandImprovement.None,
                RoadLevel = 0,
                BridgeLevel = 0,
                LastHarvestTick = 0,
                Harvested = 0,
            });
            tile.Replace(tile.Value.WithGeneratedDeposit(Seed, index % Width, index / Width));
            tile.Replace(tile.Value.WithFertility(TerrainRules.Fertility(terrain)));
            tile.Replace(tile.Value.WithNaturalWaterYield(terrain == TerrainType.Wetland ? 1 :
                terrain == TerrainType.DryFertile ? .001 :
                IsWaterTerrain(terrain) ? 0 : .004));
            tile.Replace(tile.Value with
            {
                Rainfall = tile.Value.NaturalWaterYield,
                RiverWidth = terrain == TerrainType.Stream ? (byte)1 :
                terrain == TerrainType.River ? (byte)2 :
                terrain == TerrainType.LargeRiver ? (byte)4 : (byte)0,
            });
            tile.Replace(tile.Value.WithSeededPlants());
            tile.Replace(tile.Value.WithResourceAmount(NaturalResourceCapacity(tile.Value)));
            tile.Replace(tile.Value.WithElevation(terrain switch
            {
                TerrainType.DeepWater => 10,
                TerrainType.Water => 50,
                TerrainType.Sand => 75,
                TerrainType.Mountain => 210,
                TerrainType.Snow => 240,
                _ => 110,
            }));
            if (!tile.Value.IsWalkable || IsWaterTerrain(tile.Value.Terrain))
            {
                tile.Replace(tile.Value with { NationId = 0, ClaimedSettlementId = 0, FireTicks = 0, RoadLevel = 0 });
                _burningTiles.Remove(index);
            }
        }

        RelocateInvalidEntities();
        Reindex();
        InitializeSociety();
        RefreshTotals();
    }

    /// <summary>在附近可通行的陆地投放居民，加入现有聚落或按需建立新聚落。</summary>
    /// <param name="x">横向地格坐标。</param>
    /// <param name="y">纵向地格坐标。</param>
    /// <param name="race">要投放的居民种族。</param>
    /// <param name="count">请求投放的居民数量，仍受世界人口上限限制。</param>
    public void SpawnResidents(int x, int y, RaceKind race, int count = 12)
    {
        if (!Enum.IsDefined(race))
            throw new ArgumentOutOfRangeException(nameof(race));
        if (!InBounds(x, y))
            return;
        var index = FindWalkable(x, y, 8, race);
        if (index < 0 || Residents.Count >= MaxPopulation)
            return;
        x = index % Width;
        y = index / Width;
        count = Math.Clamp(count, 1, Math.Min(200, MaxPopulation - Residents.Count));
        var owner = Tiles[index].Value.NationId;
        var settlement = Settlements
            .Where(s => owner > 0 ? s.Value.NationId == owner : Distance(s.Value.X, s.Value.Y, x, y) < MinimumSettlementDistance)
            .OrderBy(s => Distance(s.Value.X, s.Value.Y, x, y)).FirstOrDefault();
        if (settlement is null)
        {
            if (Nations.Count >= 64 || Settlements.Count >= 256)
                return;
            var nation = new StateReference<Nation>(new Nation
            {
                Id = NewId(),
                FoundingRace = race,
                Name = NewPlaceName("王国"),
                ColorArgb = NationColors[Nations.Count % NationColors.Length],
            });
            settlement = new StateReference<Settlement>(new Settlement
            {
                Id = NewId(),
                Name = NewPlaceName("村"),
                X = x,
                Y = y,
                NationId = nation.Value.Id,
                Resources = new ResourceStock
                {
                    Food = count * 8,
                    Water = count * 5,
                    Wood = 80,
                    Stone = 45,
                    Ore = 12,
                },
            });
            nation.Replace(nation.Value with { CapitalId = settlement.Value.Id });
            foreach (var other in Nations)
            {
                var opinion = RandomInt(41) - 10;
                Diplomacies = Diplomacies.Add(new DiplomaticRelation
                {
                    FirstNationId = other.Value.Id,
                    SecondNationId = nation.Value.Id,
                    Opinion = opinion,
                    FirstOpinion = opinion,
                    SecondOpinion = opinion,
                });
            }

            Nations.Add(nation);
            Settlements.Add(settlement);
            _nations[nation.Value.Id] = nation;
            _settlements[settlement.Value.Id] = settlement;
            _citizens[settlement.Value.Id] = [];
            Tiles[Index(x, y)].Replace(Tiles[Index(x, y)].Value.WithSettlementId(settlement.Value.Id));
            ClaimTerritory(settlement, 6);
            AddEvent(WorldEventKind.Founding, $"{RaceNames[(int)race]}在{settlement.Value.Name}定居，建立了{nation.Value.Name}。", x, y);
        }

        using var residentUpdates = Residents.BeginUpdates();
        using var settlementUpdates = Settlements.BeginUpdates();
        // 开局居民必须与营地位于同一连通陆岸，避免隔河出生后无法返乡。
        var spawnSites = new List<int> { index };
        var spawnSeen = new HashSet<int> { index };
        for (var site = 0; site < spawnSites.Count; site++)
            foreach (var (dx, dy) in Directions)
            {
                var xx = spawnSites[site] % Width + dx;
                var yy = spawnSites[site] / Width + dy;
                if (!InBounds(xx, yy) || Distance(x, y, xx, yy) > 3
                                      || !CanTraverseStep(spawnSites[site] % Width,
                                          spawnSites[site] / Width, xx, yy, TravelMode.Foot, race))
                    continue;
                var next = Index(xx, yy);
                if (spawnSeen.Add(next))
                    spawnSites.Add(next);
            }

        var usedNames = CollectResidentNames();
        for (var i = 0; i < count; i++)
        {
            var person = NewResident(settlement, race, 16 + RandomInt(28), usedNames);
            var position = spawnSites[RandomInt(spawnSites.Count)];
            person.Replace(person.Value.WithPosition(position % Width, position / Width));
            Residents.Add(person);
            usedNames.Add(person.Value.Name);
            _citizens[settlement.Value.Id].Add(person);
            // 开局口粮统一分配，避免职业和居民处理顺序造成不公平的库存差异。
            var food = Rules.Hunger ? Math.Min(settlement.Value.Resources.Food, 1) : 0;
            var water = Rules.Thirst ? Math.Min(settlement.Value.Resources.Water, .75) : 0;
            settlement.Replace(settlement.Value.WithResources(settlement.Value.Resources with { Food = settlement.Value.Resources.Food - food }));
            person.Replace(person.Value.WithInventory(person.Value.Inventory with { Food = person.Value.Inventory.Food + food }));
            settlement.Replace(settlement.Value.WithResources(settlement.Value.Resources with { Water = settlement.Value.Resources.Water - water }));
            person.Replace(person.Value.WithInventory(person.Value.Inventory with { Water = person.Value.Inventory.Water + water }));
        }

        InitializeSociety();
        foreach (var person in _citizens[settlement.Value.Id])
            InitializeAgent(person);
        RefreshTotals();
    }

    private StateReference<Resident> NewResident(StateReference<Settlement> settlement, RaceKind race, double age,
        IReadOnlySet<string> usedNames)
    {
        var id = NewId();
        return new StateReference<Resident>(new Resident
        {
            Id = id,
            Name = NewResidentName(id, race, Seed, usedNames),
            Race = race,
            X = settlement.Value.X,
            Y = settlement.Value.Y,
            FromX = settlement.Value.X,
            FromY = settlement.Value.Y,
            Age = age,
            CultureId = settlement.Value.CultureId,
            NationId = settlement.Value.NationId,
            SettlementId = settlement.Value.Id,
            Profession = age < 14 ? Profession.Child : AssignProfession(),
            MagicTalent = (race == RaceKind.Elf ? 45 : race == RaceKind.Dwarf ? 23 : race == RaceKind.Orc ? 28 : 32) +
                          unchecked(((uint)id * 2654435761u) ^ (uint)Seed) % 36,
            Trait = new[] { "勤劳", "勇敢", "好奇", "坚韧", "温和" }[RandomInt(5)],
        });
    }

    private Profession AssignProfession()
    {
        var roll = RandomInt(100);
        return roll < 45 ? Profession.Farmer :
            roll < 63 ? Profession.Lumberjack :
            roll < 78 ? Profession.Miner :
            roll < 85 ? Profession.Builder :
            roll < 90 ? Profession.Trader :
            roll < 91 ? Profession.Messenger :
            roll < 97 ? Profession.Scholar :
            roll < 99 ? Profession.Mage : Profession.Representative;
    }

    /// <summary>施加局部灾害；火灾和疫病先产生少量源头，后续传播由模拟规则决定。</summary>
    /// <param name="x">横向地格坐标。</param>
    /// <param name="y">纵向地格坐标。</param>
    /// <param name="kind">要施加的灾害类别。</param>
    /// <param name="radius">灾害作用半径，以地格为单位。</param>
    public void TriggerDisaster(int x, int y, DisasterKind kind, int radius = 5)
    {
        if (!Enum.IsDefined(kind))
            throw new ArgumentOutOfRangeException(nameof(kind));
        if (!InBounds(x, y))
            return;
        radius = Math.Clamp(radius, 1, 32);
        foreach (var index in Circle(x, y, radius))
        {
            var tile = Tiles[index];
            if (kind == DisasterKind.Drought && tile.Value.IsWalkable)
            {
                tile.Replace(tile.Value.WithDroughtTicks(5 * SimulationTime.TicksPerMonth));
                _dryTiles.Add(index);
            }
        }

        if (kind == DisasterKind.Fire)
        {
            var started = 0;
            var seeds = Math.Clamp(1 + radius / 10, 1, 3);
            foreach (var index in Circle(x, y, radius)
                         .OrderBy(i => Distance(x, y, i % Width, i / Width))
                         .ThenBy(i => i))
                if (Ignite(index) && ++started >= seeds)
                    break;
            if (started == 0)
                return;
        }

        if (kind == DisasterKind.Meteor)
        {
            foreach (var index in Circle(x, y, radius))
            {
                var tile = Tiles[index];
                if (!tile.Value.IsWalkable)
                    continue;
                tile.Replace(tile.Value.WithTerrain(TerrainType.Sand));
                tile.Replace(tile.Value with
                {
                    RiverWidth = 0,
                    Improvement = LandImprovement.None,
                    Fertility = 5,
                    ResourceAmount = 0,
                    RoadLevel = 0,
                    BridgeLevel = 0,
                    Plants = default,
                    FireTicks = 12,
                });
                _burningTiles.Add(index);
            }

            foreach (var resident in Residents.Where(r => Distance(r.Value.X, r.Value.Y, x, y) <= radius))
                DamageResident(resident, 65, DeathCause.Meteor);
            foreach (var building in Buildings.Where(b => Distance(b.Value.X, b.Value.Y, x, y) <= radius))
                building.Replace(building.Value with { Health = Math.Max(0, building.Value.Health - 80) });
        }

        if (kind is DisasterKind.Drought or DisasterKind.Meteor)
            EmitVisual(kind == DisasterKind.Drought ? WorldVisualKind.Drought : WorldVisualKind.Meteor, x, y, radius);
        if (kind == DisasterKind.Plague)
        {
            foreach (var resident in Residents.Where(r => r.Value.Health > 0 && r.Value.SicknessTicks == 0
                                                                               && r.Value.DiseaseImmuneUntilTick <=
                                                                               SimulationTick &&
                                                                               Distance(r.Value.X, r.Value.Y, x, y) <= radius)
                         .OrderBy(r => Distance(r.Value.X, r.Value.Y, x, y)).ThenBy(r => r.Value.Id)
                         .Take(Math.Clamp(1 + radius / 10, 1, 3)))
            {
                resident.Replace(resident.Value.WithSicknessTicks(3 * SimulationTime.TicksPerDay + RandomInt(SimulationTime.TicksPerDay + 1)));
                EmitVisual(WorldVisualKind.Plague, resident.Value.X, resident.Value.Y);
            }
        }

        var label = kind == DisasterKind.Fire ? "局部起火，火势将按地形和建筑可燃性逐步蔓延" :
            kind == DisasterKind.Drought ? "旱灾来临，农田减产，粮食储备将经受考验" :
            kind == DisasterKind.Plague ? "出现少量疫病病例，后续传播取决于居民实际接触" : "陨石撞击大地，摧毁植被与道路，重创居民和建筑";
        AddEvent(WorldEventKind.Disaster, label + "。", x, y);
    }

    /// <summary>校验并修改国家名称，同时记录玩家编辑事件。</summary>
    /// <param name="nationId">国家 ID。</param>
    /// <param name="name">新的名称。</param>
    public void RenameNation(int nationId, string name)
    {
        if (!_nations.TryGetValue(nationId, out var nation))
            throw new ArgumentException("国家不存在。", nameof(nationId));
        name = (name ?? "").Trim();
        if (name.Length is < 1 or > 40 || name.Any(char.IsControl))
            throw new ArgumentException("国名须为 1–40 个可见字符。", nameof(name));
        var previous = nation.Value.Name;
        nation.Replace(nation.Value with { Name = name });
        AddEvent(WorldEventKind.Editor, $"{previous}更名为{name}。");
    }

    /// <summary>将指定的国家资源总量均分到各聚落仓库；未指定的资源保留原库存。</summary>
    /// <param name="nationId">国家 ID。</param>
    /// <param name="food">要设置的国家粮食总量，空值表示保留现有库存。</param>
    /// <param name="wood">要设置的国家木材总量，空值表示保留现有库存。</param>
    /// <param name="stone">要设置的国家石材总量，空值表示保留现有库存。</param>
    /// <param name="ore">要设置的国家矿石总量，空值表示保留现有库存。</param>
    /// <param name="alloy">要设置的国家合金总量，空值表示保留现有库存。</param>
    /// <param name="energyCells">要设置的国家动力单元总量，空值表示保留现有库存。</param>
    /// <param name="crystals">要设置的国家魔晶总量，空值表示保留现有库存。</param>
    /// <param name="coal">要设置的国家煤总量，空值表示保留现有库存。</param>
    /// <param name="oil">要设置的国家石油总量，空值表示保留现有库存。</param>
    /// <param name="rareEarth">要设置的国家稀土总量，空值表示保留现有库存。</param>
    /// <param name="boats">要设置的国家舟船总量，空值表示保留现有库存。</param>
    /// <param name="aircraft">要设置的国家运输机总量，空值表示保留现有库存。</param>
    /// <param name="water">要设置的国家饮水总量，空值表示保留现有库存。</param>
    public void SetNationResources(int nationId, double? food = null, double? wood = null, double? stone = null,
        double? ore = null, double? alloy = null, double? energyCells = null, double? crystals = null,
        double? coal = null, double? oil = null, double? rareEarth = null, double? boats = null,
        double? aircraft = null, double? water = null)
    {
        if (!_nations.ContainsKey(nationId))
            throw new ArgumentException("国家不存在。", nameof(nationId));
        var amounts = new[]
        {
            food, wood, stone, ore, alloy, energyCells, crystals, coal, oil, rareEarth, boats, aircraft, water,
        };
        if (amounts.Any(v => v.HasValue && (!double.IsFinite(v.Value) || v.Value < 0 || v.Value > 1_000_000)))
            throw new ArgumentOutOfRangeException(nameof(food), "资源须在 0 到 1,000,000 之间。");
        if (amounts.All(v => !v.HasValue))
            return;
        var towns = Settlements.Where(s => s.Value.NationId == nationId).ToArray();
        if (towns.Length == 0)
            return;
        foreach (var town in towns)
        {
            var stock = town.Value.Resources;
            town.Replace(town.Value.WithResources(stock with
            {
                Food = food is { } f ? f / towns.Length : stock.Food,
                Wood = wood is { } w ? w / towns.Length : stock.Wood,
                Stone = stone is { } s ? s / towns.Length : stock.Stone,
                Ore = ore is { } o ? o / towns.Length : stock.Ore,
                Alloy = alloy is { } a ? a / towns.Length : stock.Alloy,
                EnergyCells = energyCells is { } e ? e / towns.Length : stock.EnergyCells,
                Crystals = crystals is { } c ? c / towns.Length : stock.Crystals,
                Coal = coal is { } co ? co / towns.Length : stock.Coal,
                Oil = oil is { } oi ? oi / towns.Length : stock.Oil,
                RareEarth = rareEarth is { } re ? re / towns.Length : stock.RareEarth,
                Boats = boats is { } bo ? bo / towns.Length : stock.Boats,
                Aircraft = aircraft is { } ai ? ai / towns.Length : stock.Aircraft,
                Water = water is { } wa ? wa / towns.Length : stock.Water,
            }));
        }

        RefreshTotals();
        AddEvent(WorldEventKind.Editor, $"{_nations[nationId].Value.Name}的资源储备已调整。");
    }

    /// <summary>直接改变两国外交关系，并发布需要居民和机构接收的命令。</summary>
    /// <param name="first">关系中第一国的 ID。</param>
    /// <param name="second">关系中第二国的 ID。</param>
    /// <param name="status">两国新的共同外交状态。</param>
    public void SetDiplomacy(int first, int second, DiplomaticStatus status)
    {
        if (first == second || !_nations.ContainsKey(first) || !_nations.ContainsKey(second))
            throw new ArgumentException("请选择两个不同且存在的国家。");
        if (!Enum.IsDefined(status))
            throw new ArgumentOutOfRangeException(nameof(status));
        var relation = Relation(first, second);
        var opinion = status == DiplomaticStatus.War ? -80 : status == DiplomaticStatus.Allied ? 80 : 0;
        relation = PublishRelation(relation with
        {
            Status = status,
            FirstOpinion = opinion,
            SecondOpinion = opinion,
            Opinion = opinion,
            LastChangedTick = SimulationTick,
            Reason = "玩家直接调整外交关系",
            AllianceOfferNationId = 0,
        });
        var diplomaticEvent = AddEvent(status == DiplomaticStatus.War ? WorldEventKind.War : WorldEventKind.Diplomacy,
            $"{_nations[first].Value.Name}与{_nations[second].Value.Name}{(status == DiplomaticStatus.War ? "开战" : status == DiplomaticStatus.Allied ? "结盟" : "恢复中立关系")}。");
        diplomaticEvent = PublishEvent(diplomaticEvent with
        {
            NationId = first,
            SecondNationId = second,
            Action = EventAction.Declaration,
            CauseEventId = relation.LastEventId,
        });
        PublishDiplomaticOrder(first, second, status, eventId: diplomaticEvent.Id);
        PublishDiplomaticOrder(second, first, status, eventId: diplomaticEvent.Id,
            objective: WarObjective.DefendHomeland);
        relation = PublishRelation(relation with { LastEventId = diplomaticEvent.Id });
    }

    /// <summary>查询两国共同的外交状态；同国视为结盟，无关系记录时为中立。</summary>
    /// <param name="first">关系中第一国的 ID。</param>
    /// <param name="second">关系中第二国的 ID。</param>
    public DiplomaticStatus GetDiplomacy(int first, int second)
    {
        return first == second
            ? DiplomaticStatus.Allied
            : Diplomacies.FirstOrDefault(r =>
                (r.FirstNationId == first && r.SecondNationId == second) ||
                (r.FirstNationId == second && r.SecondNationId == first))?.Status ?? DiplomaticStatus.Neutral;
    }

    private DiplomaticRelation Relation(int first, int second)
    {
        var relation = Diplomacies.FirstOrDefault(r =>
            (r.FirstNationId == first && r.SecondNationId == second) ||
            (r.FirstNationId == second && r.SecondNationId == first));
        if (relation is not null)
            return relation;
        relation = new DiplomaticRelation
        {
            FirstNationId = Math.Min(first, second), SecondNationId = Math.Max(first, second),
        };
        Diplomacies = Diplomacies.Add(relation);
        return relation;
    }

    private IEnumerable<int> Circle(int cx, int cy, int radius)
    {
        for (var y = Math.Max(0, cy - radius); y <= Math.Min(Height - 1, cy + radius); y++)
        for (var x = Math.Max(0, cx - radius); x <= Math.Min(Width - 1, cx + radius); x++)
            if ((x - cx) * (x - cx) + (y - cy) * (y - cy) <= radius * radius)
                yield return Index(x, y);
    }

    private void RelocateInvalidEntities()
    {
        foreach (var settlement in Settlements.ToArray())
        {
            var center = Tiles[Index(settlement.Value.X, settlement.Value.Y)];
            if ((center.Value.IsWalkable && !IsWaterTerrain(center.Value.Terrain)) || (center.Value.Terrain == TerrainType.Mountain
                                                                           && _citizens.GetValueOrDefault(settlement.Value.Id)
                                                                               ?.Any(p => p.Value.Race == RaceKind.Dwarf &&
                                                                                   p.Value.Health > 0 && p.Value.Age >= 14) ==
                                                                           true))
            {
                ClaimTerritory(settlement, 6);
                continue;
            }

            Tiles[Index(settlement.Value.X, settlement.Value.Y)].Replace(Tiles[Index(settlement.Value.X, settlement.Value.Y)].Value.WithSettlementId(0));
            var position = Circle(settlement.Value.X, settlement.Value.Y, 12)
                .Where(i => Tiles[i].Value.IsWalkable && !IsWaterTerrain(Tiles[i].Value.Terrain) &&
                            Tiles[i].Value.SettlementId == 0 &&
                            !Buildings.Any(b => b.Value.X == i % Width && b.Value.Y == i / Width) &&
                            (Tiles[i].Value.NationId == 0 || Tiles[i].Value.NationId == settlement.Value.NationId))
                .OrderBy(i => Distance(i % Width, i / Width, settlement.Value.X, settlement.Value.Y))
                .FirstOrDefault(-1);
            if (position >= 0)
            {
                settlement.Replace(
                    settlement.Value with { X = position % Width, Y = position / Width });
                Tiles[position].Replace(Tiles[position].Value.WithSettlementId(settlement.Value.Id));
                ClaimTerritory(settlement, 6);
                AddEvent(WorldEventKind.Editor, $"地形改变，{settlement.Value.Name}迁往可居住的土地。", settlement.Value.X, settlement.Value.Y);
            }
            else
                RemoveSettlement(settlement, "家园被地形变化摧毁");
        }

        foreach (var resident in Residents)
        {
            if (CanTraverse(Tiles[Index(resident.Value.X, resident.Value.Y)].Value, resident.Value.TravelMode, resident.Value.Race))
                continue;
            var position = FindWalkable(resident.Value.X, resident.Value.Y, 10, resident.Value.Race);
            if (position >= 0)
            {
                resident.Replace(resident.Value with { X = position % Width, Y = position / Width });
                DamageResident(resident, 15, DeathCause.TerrainChange);
            }
            else
            {
                DamageResident(resident, resident.Value.Health,
                    IsWaterTerrain(Tiles[Index(resident.Value.X, resident.Value.Y)].Value.Terrain)
                        ? DeathCause.Drowning
                        : DeathCause.TerrainChange);
            }
        }

        ArchiveDeadResidents();
        foreach (var army in Armies.ToArray())
        {
            if (Walkable(army.Value.X, army.Value.Y))
                continue;
            var position = FindWalkable(army.Value.X, army.Value.Y, 10);
            if (position >= 0)
                army.Replace(army.Value with { X = position % Width, Y = position / Width });
            else
                DisbandArmy(army);
        }

        RemoveEmptyNations();
    }
}
