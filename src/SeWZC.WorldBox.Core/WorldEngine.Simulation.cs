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
                foreach (var settlement in Current.Settlements.Where(s => _citizens[s.Id].Count == 0).ToArray())
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
        foreach (var town in Current.Settlements)
            town.BeginResourceUpdates();
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
                    _dailyResidentOutputs = new Resident.DailyState[count];
                }

                for (var index = 0; index < count; index++)
                    _dailyResidentInputs[index] = Prepare(Current.Residents[index]);
                // 保留整批输入的同时刻语义；短小身体转换顺序计算，避免每 tick 调度工作线程。
                for (var index = 0; index < count; index++)
                    _dailyResidentOutputs[index] = _dailyResidentInputs[index].Advance(rules, tick);
                foreach (var cursor in Current.Residents)
                    cursor.ApplyDay(_dailyResidentOutputs[cursor.Position]);
                Array.Clear(_dailyResidentInputs, 0, count);
                Array.Clear(_dailyResidentOutputs, 0, count);
            }
        }
        finally
        {
            EndDailyDrinking();
            foreach (var town in Current.Settlements)
                town.EndResourceUpdates();
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
                                  && Distance(cursor.X, cursor.Y, home.X, home.Y) <= 1
                                  && Walkable(cursor.X, cursor.Y, cursor.Race)
                                  && tick - cursor.MoveStartedTick >= cursor.MoveDurationTicks
                                  && !(cursor.TravelMode == TravelMode.Boat &&
                                       cursor.Agent.Goal.Kind == AgentGoalKind.Fish))
            {
                TransferPersonalProduction(cursor, home);
                inventory = ProvisionAtHome(cursor, home);
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
                : .025 * rules.MagicRate * TerrainRules.For(tile.Terrain).ManaRate
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
        foreach (var town in Current.Settlements.ToArray())
        {
            var citizens = _citizens[town.Id];
            // 住宅与发展项目共享材料预算，须为下一项本地建设或研究留出实际交付的材料。
            var developmentReserve = LocalDevelopmentReserve(town);
            if (Current.Rules.Construction && Current.Rules.Expansion && SettlementExpansionError(town.Id) is null
                && ResourceStock.Kinds.All(k =>
                    town.Resources.Get(k) >= SettlementExpansionCost(town.Tier).Get(k) + developmentReserve.Get(k)))
                ExpandTown(town.Id);
            if (Current.Rules.Construction && town.Resources.Food >= citizens.Count * 2 + developmentReserve.Food
                                           && GetHousingCapacity(town.Id) < 60 + Current.Buildings.Where(b =>
                                                   b.Value.SettlementId == town.Id && IsFacilityOperating(b.Value))
                                               .Sum(b => b.Value.Kind == BuildingKind.Farm
                                                   ? 30 * b.Value.Efficiency
                                                   : b.Value.Kind is BuildingKind.AutomatedFarm or BuildingKind.RunicGarden
                                                       ? 120 * b.Value.Efficiency
                                                       : 0)
                                           && citizens.Count > GetHousingCapacity(town.Id) * 0.75
                                           && !Current.Buildings.Any(b =>
                                               b.Value.SettlementId == town.Id && (!b.Value.IsCompleted || b.Value.IsUpgrading))
                                           && town.Resources.Wood >= 25 + developmentReserve.Wood &&
                                           town.Resources.Stone >= 8 + developmentReserve.Stone)
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
                    && Distance(p.X, p.Y, town.X, town.Y) <= 1 && Walkable(p.X, p.Y, p.Race)
                    && Current.Tick - p.MoveStartedTick >= p.MoveDurationTicks)
                .ToArray();
            // 已返家的家庭可用随身口粮抚育下一代；不要求所有食物先积存在公共仓库，也不读取远处背包。
            var familyFood = town.Resources.Food + adults.Sum(p => Math.Max(0, p.Inventory.Food - FoodUse(p) * 2));
            var dailyFood = citizens.Sum(p => FoodUse(p));
            if (Current.Rules.Births && adults.Length >= 6 &&
                familyFood > dailyFood * 4 + .6 && Current.Residents.Count < MaxPopulation)
            {
                var births = Math.Max(1, adults.Length / 28);
                var housing = GetHousingCapacity(town.Id);
                // 拥挤降低出生率，不能完全禁生而让整代成年人同时老去，连住房建成后也无人延续。
                if (citizens.Count >= housing)
                    births = Math.Max(1, births * housing / Math.Max(1, citizens.Count) / 4);
                else
                    births = Math.Min(births, housing - citizens.Count);
                births = Math.Min(births, MaxPopulation - Current.Residents.Count);
                for (var b = 0; b < births; b++)
                {
                    var remaining = .6;
                    var stored = Math.Min(town.Resources.Food, remaining);
                    town.Resources = town.Resources with { Food = town.Resources.Food - stored };
                    remaining -= stored;
                    foreach (var parent in adults)
                    {
                        if (remaining <= .000001)
                            break;
                        var supplied = Math.Min(remaining, Math.Max(0, parent.Inventory.Food - FoodUse(parent) * 2));
                        parent.Inventory = parent.Inventory with { Food = parent.Inventory.Food - supplied };
                        remaining -= supplied;
                    }

                    var child = NewResident(town, adults[RandomInt(adults.Length)].Race, 0);
                    child.Inventory = new ResourceStock { Food = .6 - remaining };
                    Current.Residents.Add(child);
                    citizens.Add(child);
                }

                if (Current.Tick % SimulationTime.TicksPerYear == 0)
                    AddEvent(WorldEventKind.Growth, $"{town.Name}迎来新生儿，人口增至{citizens.Count}。", town.X, town.Y);
            }

            if (Current.Rules.Expansion && Current.Tick % SimulationTime.TicksPerYear == 0 && citizens.Count >= 80 &&
                !town.IsExpanding
                && ResourceStock.Kinds.All(k =>
                    town.Resources.Get(k) >= VillageFoundingCost.Get(k) + developmentReserve.Get(k)) &&
                Current.Settlements.Count < 256)
                ExpandSettlement(town, citizens);
        }
    }

    private void ExpandSettlement(SettlementCursor origin, List<ResidentCursor> citizens)
    {
        var pioneers = citizens.Where(p => p.ArmyId == 0 && p.Age >= 16 && p.Health >= 60
                                           && p.Agent.DestinationSettlementId == 0 &&
                                           Distance(p.X, p.Y, origin.X, origin.Y) <= 3)
            .OrderByDescending(p => p.Agent.Personality.Ambition).ThenBy(p => p.Id).Take(12).ToArray();
        if (pioneers.Length < 6 || MissingResources(origin.Resources, VillageFoundingCost) is not null)
            return;
        // 建村地点须在出发前报告给原聚落，避免迁徙队伍使用未送达的信息。
        var location = origin.PublicKnowledge.Where(f => f.Kind == AgentFactKind.FoundingSite &&
                                                         f.LearnedTick < Current.Tick
                                                         && Current.Tick - f.ObservedTick <=
                                                         5 * SimulationTime.TicksPerYear &&
                                                         f.Confidence >= .5 &&
                                                         InBounds(f.X, f.Y))
            .Select(f => Index(f.X, f.Y)).Where(i => !IsWaterTerrain(Current.Tiles[i].Terrain)
                                                     && pioneers.All(p =>
                                                         RaceTerrainRules.CanWalk(Current.Tiles[i].Value, p.Race)) &&
                                                     Current.Tiles[i].FireTicks == 0
                                                     && !Current.Buildings.Any(b =>
                                                         b.Value.X == i % Current.Width && b.Value.Y == i / Current.Width)
                                                     && Current.Tiles[i].Fertility >= 25 &&
                                                     (Current.Tiles[i].NationId == 0 ||
                                                      Current.Tiles[i].NationId == origin.NationId)
                                                     && Current.Tiles[i].ClaimedSettlementId == 0
                                                     && FoundingSiteSuitable(i,
                                                         pioneers.Select(p => p.Race).Distinct().ToArray())
                                                     && Distance(i % Current.Width, i / Current.Width, origin.X,
                                                         origin.Y) >= MinimumSettlementDistance
                                                     && Current.Settlements.All(t =>
                                                         Distance(t.X, t.Y, i % Current.Width, i / Current.Width) >=
                                                         MinimumSettlementDistance))
            .OrderByDescending(i => Current.Tiles[i].Fertility).ThenBy(i => i).FirstOrDefault(-1);
        if (location < 0)
            return;
        var x = location % Current.Width;
        var y = location / Current.Width;
        var town = new SettlementCursor(new Settlement
        {
            Id = NewId(),
            Name = NewPlaceName("村"),
            X = x,
            Y = y,
            NationId = origin.NationId,
            CultureId = origin.CultureId,
            FoundationPending = true,
            Resources = new ResourceStock(),
        });
        origin.Resources = Spend(origin.Resources, VillageFoundingCost);
        Current.Settlements.Add(town);
        _settlements[town.Id] = town;
        _citizens[town.Id] = [];
        foreach (var pioneer in pioneers)
        {
            foreach (var resource in ResourceStock.Kinds)
                pioneer.Inventory = pioneer.Inventory.WithAmount(resource,
                    pioneer.Inventory.Get(resource) + VillageFoundingCost.Get(resource) / pioneers.Length);
            pioneer.SettlementId = town.Id;
            var address = new AgentFact
            {
                Id = NewId(),
                Kind = AgentFactKind.SettlementLocation,
                SubjectId = town.Id,
                X = x,
                Y = y,
                Value = town.NationId,
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
                TargetSettlementId = town.Id,
                StartedTick = Current.Tick,
                Reason =
                    $"原聚落人口 {citizens.Count}，为拓荒扩展家园；已收到建村勘察报告，选址 {x}, {y} 肥力 {Current.Tiles[location].Fertility}/100，周围有可登记陆地，背负粮木石步行建立新家园",
                PlayerDirected = true,
                ReviewTick = Current.Tick + 150,
            });
            citizens.Remove(pioneer);
            _citizens[town.Id].Add(pioneer);
        }

        Current.Tiles[location].SettlementId = town.Id;
        ClaimTerritory(town, 4);
        InitializeSociety();
        AddEvent(WorldEventKind.Growth,
            $"{_nations[origin.NationId].Value.Name}派出 {pioneers.Length} 名成年人，携物资前往{town.Name}；仓库等待实物抵达。", x, y);
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
            if (tile.FireTicks <= 0)
            {
                _burningTiles.Remove(index);
                continue;
            }

            tile.FireTicks--;
            if (tile.FireTicks == 0)
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
            if (Current.Tiles[neighborIndex].FireTicks == 0 && RandomInt(1000) < GetTileFlammability(x, y) * 120)
                Ignite(neighborIndex);
        }

        foreach (var index in _dryTiles.ToArray())
            if (--Current.Tiles[index].DroughtTicks <= 0)
            {
                Current.Tiles[index].DroughtTicks = 0;
                _dryTiles.Remove(index);
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
            town.MaxClaimRadius = Math.Max(town.MaxClaimRadius,
                Math.Min(17, 6 + (_citizens.GetValueOrDefault(town.Id)?.Count ?? 0) / 15));
    }
}
