using SeWZC.WorldBox.Core.Runtime;

namespace SeWZC.WorldBox.Core;

public sealed partial class WorldEngine
{
    /// <summary>将世界推进一个模拟 tick。</summary>
    public void Tick()
    {
        Step();
    }

    /// <summary>将世界推进指定数量的模拟 tick。</summary>
    /// <param name="steps">推进的 tick 数，范围为 0 至 10,000；为零时不改变世界。</param>
    public void Step(int steps = 1)
    {
        if (steps is < 0 or > 10_000)
            throw new ArgumentOutOfRangeException(nameof(steps));
        for (var step = 0; step < steps; step++)
        {
            using var scalarUpdates = Current.BeginScalarUpdates();
            using var tileUpdates = Current.Tiles.BeginUpdates();
            using var residentUpdates = Current.Residents.BeginUpdates();
            using var settlementUpdates = Current.Settlements.BeginUpdates();
            using var buildingUpdates = Current.Buildings.BeginUpdates();
            using var nationUpdates = Current.Nations.BeginUpdates();
            using var armyUpdates = Current.Armies.BeginUpdates();
            Current.Tick++;
            Reindex();
            BeginKnowledgeQueries();
            try
            {
                UpdateDisasters();
                TickWildlife();
                TickPlants();
                UpdateAgentNeedsAndActions();
                UpdateLocalCommunication();
                TickSociety();
                var peopleRevision = Current.Residents.MembershipRevision;
                var townsRevision = Current.Settlements.MembershipRevision;
                var nationsRevision = Current.Nations.MembershipRevision;
                var buildingsAfterSociety = Current.Buildings.Snapshot;
                TickDiplomacy();
                TickLocalConflicts();
                TickMigrationAndSecession();
                Reindex();
                if (Current.Tick % (SimulationTime.TicksPerYear / 10) == 0)
                    GrowSettlements();
                if (Current.Tick % SimulationTime.TicksPerMonth == 0)
                    RefreshTerritoryClaims();
                UpdateArmies();
                ArchiveDeadResidents();
                Reindex();
                foreach (var settlement in Current.Settlements.Where(s => _citizens[s.Value.Id].Count == 0).ToArray())
                    RemoveSettlement(settlement, "居民离散，聚落成为遗址");
                RemoveEmptyNations();
                if (Current.Residents.MembershipRevision != peopleRevision
                    || Current.Settlements.MembershipRevision != townsRevision
                    || Current.Nations.MembershipRevision != nationsRevision
                    || !ReferenceEquals(Current.Buildings.Snapshot, buildingsAfterSociety))
                    ReconcileSocietyTopology();
                RefreshTotals();
                ObserveProjects();
            }
            finally
            {
                EndKnowledgeQueries();
            }
        }
    }

    private void UpdateResidents()
    {
        var rules = Current.Rules;
        var tick = Current.Tick;
        var tiles = Current.Tiles;
        // 先保存现有病例的位置，避免新感染在同一天沿居民遍历顺序连锁传播。
        var infected = new HashSet<int>(Current.Residents.Where(r => r.SicknessTicks > 0).Select(r => Index(r.X, r.Y)));
        BeginDailyDrinking();
        // 库存只在本阶段按居民顺序记账，阶段结束显式提交聚落。
        var warehouses = new ResourceStock[Current.Settlements.Count];
        foreach (var town in Current.Settlements)
            warehouses[town.Position] = town.Value.Resources;
        try
        {
            var count = Current.Residents.Count;
            if (count < 4096 || Environment.ProcessorCount <= 1 || OperatingSystem.IsBrowser())
            {
                foreach (var cursor in Current.Residents)
                    cursor.ApplyDay(Prepare(cursor).Advance(rules, tick));
            }
            else
            {
                if (_dailyResidentInputs.Length < count)
                {
                    _dailyResidentInputs = new DailyResidentInput[count];
                }

                for (var index = 0; index < count; index++)
                    _dailyResidentInputs[index] = Prepare(Current.Residents[index]);
                // 整批输入已固定；纯身体转换直接按顺序提交，不必暂存另一份输出数组。
                for (var index = 0; index < count; index++)
                    Current.Residents[index].ApplyDay(_dailyResidentInputs[index].Advance(rules, tick));
                Array.Clear(_dailyResidentInputs, 0, count);
            }
        }
        finally
        {
            EndDailyDrinking();
            foreach (var town in Current.Settlements)
                town.Replace(town.Value.WithResources(warehouses[town.Position]));
        }

        ArchiveDeadResidents();

        DailyResidentInput Prepare(ResidentCursor cursor)
        {
            InitializeAgent(cursor);
            // 返仓只转换库存、认知及出行方式；将前两项直接交给纯结算，避免先冻结一份中间居民。
            var person = cursor.Value;
            var inventory = cursor.Inventory;
            if (cursor.Health > 0 && cursor.ArmyId == 0
                                  && _settlements.TryGetValue(cursor.SettlementId, out var home)
                                  && Distance(cursor.X, cursor.Y, home.Value.X, home.Value.Y) <= 1
                                  && Walkable(cursor.X, cursor.Y, cursor.Race)
                                  && tick - cursor.MoveStartedTick >= cursor.MoveDurationTicks
                                  && !(cursor.TravelMode == TravelMode.Boat &&
                                       cursor.Agent.Goal.Kind == AgentGoalKind.Fish))
            {
                var warehouse = UnloadAtHome(cursor, home.Value.Id, warehouses[home.Position]);
                var supplied = ProvisionAtHome(cursor, home.Value, warehouse);
                inventory = supplied.Inventory;
                warehouses[home.Position] = supplied.Warehouse;
            }

            var age = rules.Aging ? Math.Min(1000, person.Age + 1d / SimulationTime.TicksPerYear) : person.Age;
            var profession = person.Profession == Profession.Child && age >= 14
                ? AssignProfession()
                : person.Profession;
            var infectionDuration = 0;
            if (person.SicknessTicks == 0 && rules.Disease && person.DiseaseImmuneUntilTick <= tick
                && infected.Count > 0 && (tick + person.Id) % 6 == 0)
            {
                var exposed = infected.Contains(Index(person.X, person.Y));
                foreach (var (dx, dy) in Directions)
                    if (InBounds(person.X + dx, person.Y + dy)
                        && infected.Contains(Index(person.X + dx, person.Y + dy)))
                    {
                        exposed = true;
                        break;
                    }

                if (exposed && RandomInt(100) < 6)
                    infectionDuration = 3 * SimulationTime.TicksPerDay + RandomInt(SimulationTime.TicksPerDay + 1);
            }

            var tile = tiles[Index(person.X, person.Y)];
            var manaRecovery = person.Mana >= 100 || rules.MagicRate == 0
                ? 0
                : .025 * rules.MagicRate * TerrainRules.For(tile.Value.Terrain).ManaRate
                  * (.5 + person.MagicTalent / 100)
                  * (HasResearch(person.SettlementId, Advancement.ManaAttunement) ? 1.5 : 1);
            var hasHome = _settlements.ContainsKey(person.SettlementId);
            var waterUse = WaterUse(age, tile.Value) / SimulationTime.TicksPerDay;
            var water = hasHome && person.ArmyId == 0 && person.Health > 0 && rules.Thirst
                        && inventory.Water < waterUse
                ? WithdrawWater(person.X, person.Y, person.MoveStartedTick, person.MoveDurationTicks,
                    cursor.Inventory.Water, Index(person.X, person.Y), waterUse - inventory.Water)
                : 0;
            return new DailyResidentInput
            {
                Person = person,
                Agent = cursor.Agent,
                Inventory = inventory,
                Tile = tile.Value,
                Profession = profession,
                InfectionDuration = infectionDuration,
                ManaRecovery = manaRecovery / SimulationTime.TicksPerDay,
                ConsumeNeeds =
                    person.ArmyId == 0 && hasHome,
                SocialGrowth = hasHome && (tick + person.Id) % SimulationTime.TicksPerDay == 0 ? .07 : 0,
                DeliveredWater = water,
                ArrivedTile =
                    hasHome && person.ArmyId == 0 && person.Health > 0
                    && tick - person.MoveStartedTick == person.MoveDurationTicks
                        ? Index(person.X, person.Y)
                        : -1,
            };
        }
    }

    private void GrowSettlements()
    {
        HashSet<string>? usedNames = null;
        foreach (var town in Current.Settlements.ToArray())
        {
            var citizens = _citizens[town.Value.Id];
            // 住宅与发展项目共享材料预算，须为下一项本地建设或研究留出实际交付的材料。
            var developmentReserve = LocalDevelopmentReserve(town);
            if (Current.Rules.Construction && Current.Rules.Expansion && SettlementExpansionError(town.Value.Id) is null
                && ResourceStock.Kinds.All(k =>
                    town.Value.Resources.Get(k) >= SettlementExpansionCost(town.Value.Tier).Get(k) + developmentReserve.Get(k)))
                ExpandTown(town.Value.Id);
            if (Current.Rules.Construction && town.Value.Resources.Food >= citizens.Count * 2 + developmentReserve.Food
                                           && GetHousingCapacity(town.Value.Id) < 60 + Current.Buildings.Where(b =>
                                                   b.Value.SettlementId == town.Value.Id && IsFacilityOperating(b.Value))
                                               .Sum(b => b.Value.Kind == BuildingKind.Farm
                                                   ? 30 * b.Value.Efficiency
                                                   : b.Value.Kind is BuildingKind.AutomatedFarm or BuildingKind.RunicGarden
                                                       ? 120 * b.Value.Efficiency
                                                       : 0)
                                           && citizens.Count > GetHousingCapacity(town.Value.Id) * 0.75
                                           && !Current.Buildings.Any(b =>
                                               b.Value.SettlementId == town.Value.Id && (!b.Value.IsCompleted || b.Value.IsUpgrading))
                                           && town.Value.Resources.Wood >= 25 + developmentReserve.Wood &&
                                           town.Value.Resources.Stone >= 8 + developmentReserve.Stone)
            {
                var site = BestBuildingSite(town, BuildingKind.Housing);
                if (site >= 0)
                {
                    BuildPlannedFacility(town, BuildingKind.Housing, site % Current.Width, site / Current.Width,
                        BuildingPurpose(town, BuildingKind.Housing));
                }
            }

            var adults = citizens.Where(p =>
                    p.Age >= 18 && p.Age < (p.Race == RaceKind.Elf ? 100 : 55) && p.Health >= 60
                    && p.Hunger < 30 && (!Current.Rules.Thirst || p.Thirst < 30) && p.SicknessTicks == 0
                    && p.ArmyId == 0 && p.Agent.DestinationSettlementId == 0
                    && Distance(p.X, p.Y, town.Value.X, town.Value.Y) <= 1 && Walkable(p.X, p.Y, p.Race)
                    && Current.Tick - p.MoveStartedTick >= p.MoveDurationTicks)
                .ToArray();
            // 已返家的家庭可用随身口粮抚育下一代；不要求所有食物先积存在公共仓库，也不读取远处背包。
            var familyFood = town.Value.Resources.Food + adults.Sum(p => Math.Max(0, p.Inventory.Food - FoodUse(p) * 2));
            var dailyFood = citizens.Sum(p => FoodUse(p));
            if (Current.Rules.Births && adults.Length >= 6 &&
                familyFood > dailyFood * 4 + .6 && Current.Residents.Count < MaxPopulation)
            {
                var births = Math.Max(1, adults.Length / 28);
                var housing = GetHousingCapacity(town.Value.Id);
                // 拥挤降低出生率，不能完全禁生而让整代成年人同时老去，连住房建成后也无人延续。
                if (citizens.Count >= housing)
                    births = Math.Max(1, births * housing / Math.Max(1, citizens.Count) / 4);
                else
                    births = Math.Min(births, housing - citizens.Count);
                births = Math.Min(births, MaxPopulation - Current.Residents.Count);
                for (var b = 0; b < births; b++)
                {
                    var remaining = .6;
                    var stored = Math.Min(town.Value.Resources.Food, remaining);
                    town.Replace(town.Value.WithResources(town.Value.Resources with { Food = town.Value.Resources.Food - stored }));
                    remaining -= stored;
                    foreach (var parent in adults)
                    {
                        if (remaining <= .000001)
                            break;
                        var supplied = Math.Min(remaining, Math.Max(0, parent.Inventory.Food - FoodUse(parent) * 2));
                        parent.Inventory = parent.Inventory with { Food = parent.Inventory.Food - supplied };
                        remaining -= supplied;
                    }

                    var child = NewResident(town, adults[RandomInt(adults.Length)].Race, 0,
                        usedNames ??= CollectResidentNames());
                    child.Inventory = new ResourceStock { Food = .6 - remaining };
                    Current.Residents.Add(child);
                    usedNames.Add(child.Name);
                    citizens.Add(child);
                }

                if (Current.Tick % SimulationTime.TicksPerYear == 0)
                    AddEvent(WorldEventKind.Growth, $"{town.Value.Name}迎来新生儿，人口增至{citizens.Count}。", town.Value.X, town.Value.Y);
            }

            if (Current.Rules.Expansion && Current.Tick % SimulationTime.TicksPerYear == 0 && citizens.Count >= 80 &&
                !town.Value.IsExpanding
                && ResourceStock.Kinds.All(k =>
                    town.Value.Resources.Get(k) >= VillageFoundingCost.Get(k) + developmentReserve.Get(k)) &&
                Current.Settlements.Count < 256)
                ExpandSettlement(town, citizens);
        }
    }

    private void ExpandSettlement(StateReference<Settlement> origin, List<ResidentCursor> citizens)
    {
        var pioneers = citizens.Where(p => p.ArmyId == 0 && p.Age >= 16 && p.Health >= 60
                                           && p.Agent.DestinationSettlementId == 0 &&
                                           Distance(p.X, p.Y, origin.Value.X, origin.Value.Y) <= 3)
            .OrderByDescending(p => p.Agent.Personality.Ambition).ThenBy(p => p.Id).Take(12).ToArray();
        if (pioneers.Length < 6 || MissingResources(origin.Value.Resources, VillageFoundingCost) is not null)
            return;
        // 建村地点须在出发前报告给原聚落，避免迁徙队伍使用未送达的信息。
        var location = origin.Value.PublicKnowledge.Where(f => f.Kind == AgentFactKind.FoundingSite &&
                                                         f.LearnedTick < Current.Tick
                                                         && Current.Tick - f.ObservedTick <=
                                                         5 * SimulationTime.TicksPerYear &&
                                                         f.Confidence >= .5 &&
                                                         InBounds(f.X, f.Y))
            .Select(f => Index(f.X, f.Y)).Where(i => !IsWaterTerrain(Current.Tiles[i].Value.Terrain)
                                                     && pioneers.All(p =>
                                                         RaceTerrainRules.CanWalk(Current.Tiles[i].Value, p.Race)) &&
                                                     Current.Tiles[i].Value.FireTicks == 0
                                                     && !Current.Buildings.Any(b =>
                                                         b.Value.X == i % Current.Width && b.Value.Y == i / Current.Width)
                                                     && Current.Tiles[i].Value.Fertility >= 25 &&
                                                     (Current.Tiles[i].Value.NationId == 0 ||
                                                      Current.Tiles[i].Value.NationId == origin.Value.NationId)
                                                     && Current.Tiles[i].Value.ClaimedSettlementId == 0
                                                     && FoundingSiteSuitable(i,
                                                         pioneers.Select(p => p.Race).Distinct().ToArray())
                                                     && Distance(i % Current.Width, i / Current.Width, origin.Value.X,
                                                         origin.Value.Y) >= MinimumSettlementDistance
                                                     && Current.Settlements.All(t =>
                                                         Distance(t.Value.X, t.Value.Y, i % Current.Width, i / Current.Width) >=
                                                         MinimumSettlementDistance))
            .OrderByDescending(i => Current.Tiles[i].Value.Fertility).ThenBy(i => i).FirstOrDefault(-1);
        if (location < 0)
            return;
        var x = location % Current.Width;
        var y = location / Current.Width;
        var town = new StateReference<Settlement>(new Settlement
        {
            Id = NewId(),
            Name = NewPlaceName("村"),
            X = x,
            Y = y,
            NationId = origin.Value.NationId,
            CultureId = origin.Value.CultureId,
            FoundationPending = true,
            Resources = new ResourceStock(),
        });
        origin.Replace(origin.Value.WithResources(Spend(origin.Value.Resources, VillageFoundingCost)));
        Current.Settlements.Add(town);
        _settlements[town.Value.Id] = town;
        _citizens[town.Value.Id] = [];
        foreach (var pioneer in pioneers)
        {
            foreach (var resource in ResourceStock.Kinds)
                pioneer.Inventory = pioneer.Inventory.WithAmount(resource,
                    pioneer.Inventory.Get(resource) + VillageFoundingCost.Get(resource) / pioneers.Length);
            pioneer.Replace(pioneer.Value with { SettlementId = town.Value.Id });
            var address = new AgentFact
            {
                Id = NewId(),
                Kind = AgentFactKind.SettlementLocation,
                SubjectId = town.Value.Id,
                X = x,
                Y = y,
                Value = town.Value.NationId,
                ObservedTick = Current.Tick,
                LearnedTick = Current.Tick,
                OriginResidentId = pioneer.Id,
                OriginProfession = pioneer.Profession,
                SourceResidentId = pioneer.Id,
                Text = "拓荒队商定的新家园，物资必须亲自带到",
            };
            RememberAgentFact(pioneer, address);
            pioneer.Agent = pioneer.Agent.WithGoal(new AgentGoal
            {
                Kind = AgentGoalKind.ReturnHome,
                TargetX = x,
                TargetY = y,
                TargetSettlementId = town.Value.Id,
                StartedTick = Current.Tick,
                Reason =
                    $"原聚落人口 {citizens.Count}，为拓荒扩展家园；已收到建村勘察报告，选址 {x}, {y} 肥力 {Current.Tiles[location].Value.Fertility}/100，周围有可登记陆地，背负粮木石步行建立新家园",
                PlayerDirected = true,
                ReviewTick = Current.Tick + 150,
            });
            citizens.Remove(pioneer);
            _citizens[town.Value.Id].Add(pioneer);
        }

        Current.Tiles[location].Replace(Current.Tiles[location].Value.WithSettlementId(town.Value.Id));
        ClaimTerritory(town, 4);
        InitializeSociety();
        AddEvent(WorldEventKind.Growth,
            $"{_nations[origin.Value.NationId].Value.Name}派出 {pioneers.Length} 名成年人，携物资前往{town.Value.Name}；仓库等待实物抵达。", x, y);
    }

    private bool FoundingSiteSuitable(int index, ReadOnlySpan<RaceKind> races)
    {
        var x = index % Current.Width;
        var y = index / Current.Width;
        // 建村地点须能连通取得最小占地范围，避免定居在四周无法利用的单个肥沃地格。
        var available = 0;
        for (var nearbyY = Math.Max(0, y - 3); nearbyY <= Math.Min(Current.Height - 1, y + 3); nearbyY++)
        for (var nearbyX = Math.Max(0, x - 3); nearbyX <= Math.Min(Current.Width - 1, x + 3); nearbyX++)
        {
            if ((nearbyX - x) * (nearbyX - x) + (nearbyY - y) * (nearbyY - y) > 9)
                continue;
            var tile = Current.Tiles[Index(nearbyX, nearbyY)].Value;
            if (IsWaterTerrain(tile.Terrain) || tile.FireTicks > 0 || tile.ClaimedSettlementId != 0)
                continue;
            var usable = true;
            foreach (var race in races)
                if (!RaceTerrainRules.CanWalk(tile, race))
                {
                    usable = false;
                    break;
                }

            if (usable && ++available >= SettlementActivationArea)
                return true;
        }

        return false;
    }

    private void UpdateDisasters()
    {
        foreach (var index in _burningTiles.Order().ToArray())
        {
            var tile = Current.Tiles[index];
            if (tile.Value.FireTicks <= 0)
            {
                _burningTiles.Remove(index);
                continue;
            }

            tile.Replace(tile.Value.WithFireTicks(tile.Value.FireTicks - 1));
            if (tile.Value.FireTicks == 0)
            {
                EndFire(index, true);
                continue;
            }

            if (!Current.Rules.FireSpread || Current.Tick % 8 != 0)
                continue;
            var (dx, dy) = Directions[RandomInt(4)];
            var x = index % Current.Width + dx;
            var y = index / Current.Width + dy;
            if (!InBounds(x, y))
                continue;
            var neighborIndex = Index(x, y);
            if (Current.Tiles[neighborIndex].Value.FireTicks == 0 && RandomInt(1000) < GetTileFlammability(x, y) * 120)
                Ignite(neighborIndex);
        }

        foreach (var index in _dryTiles.ToArray())
        {
            var tile = Current.Tiles[index];
            tile.Replace(tile.Value.WithDroughtTicks(tile.Value.DroughtTicks - 1));
            if (tile.Value.DroughtTicks <= 0)
            {
                tile.Replace(tile.Value.WithDroughtTicks(0));
                _dryTiles.Remove(index);
            }
        }

        if (Current.NaturalDisasters && Current.Rules.DisasterFrequency > 0 &&
            Current.Tick % (10 * SimulationTime.TicksPerYear / Current.Rules.DisasterFrequency) == 0 &&
            Current.Residents.Count > 0)
        {
            var person = Current.Residents[RandomInt(Current.Residents.Count)];
            TriggerDisaster(person.X, person.Y, (DisasterKind)RandomInt(Current.Rules.Disease ? 3 : 2),
                2 + Current.Rules.DisasterStrength * 2);
        }
    }

    private void RefreshTerritoryClaims()
    {
        foreach (var town in Current.Settlements)
            town.Replace(town.Value with { MaxClaimRadius = Math.Max(town.Value.MaxClaimRadius,
                Math.Min(17, 6 + (_citizens.GetValueOrDefault(town.Value.Id)?.Count ?? 0) / 15)) });
    }
}
