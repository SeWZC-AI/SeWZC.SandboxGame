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
            using var tileUpdates = Tiles.BeginUpdates();
            using var residentUpdates = Residents.BeginUpdates();
            using var settlementUpdates = Settlements.BeginUpdates();
            using var buildingUpdates = Buildings.BeginUpdates();
            using var nationUpdates = Nations.BeginUpdates();
            using var armyUpdates = Armies.BeginUpdates();
            SimulationTick++;
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
                var peopleRevision = Residents.MembershipRevision;
                var townsRevision = Settlements.MembershipRevision;
                var nationsRevision = Nations.MembershipRevision;
                var buildingsAfterSociety = Buildings.CaptureSnapshot();
                TickDiplomacy();
                TickLocalConflicts();
                TickMigrationAndSecession();
                Reindex();
                if (SimulationTick % (SimulationTime.TicksPerYear / 10) == 0)
                    GrowSettlements();
                if (SimulationTick % SimulationTime.TicksPerMonth == 0)
                    RefreshTerritoryClaims();
                UpdateArmies();
                AssignResidentHomes();
                UpdateResidentNeeds();
                SynchronizeResidentRescues();
                ArchiveDeadResidents();
                Reindex();
                foreach (var settlement in Settlements.Where(s => _citizens[s.Value.Id].Count == 0).ToArray())
                    RemoveSettlement(settlement, "居民离散，聚落成为遗址");
                RemoveEmptyNations();
                if (Residents.MembershipRevision != peopleRevision
                    || Settlements.MembershipRevision != townsRevision
                    || Nations.MembershipRevision != nationsRevision
                    || !ReferenceEquals(Buildings.CaptureSnapshot(), buildingsAfterSociety))
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
        ShareHouseholdSupplies();
        var rules = Rules;
        var tick = SimulationTick;
        var tiles = Tiles;
        // 先保存现有病例的位置，避免新感染在同一天沿居民遍历顺序连锁传播。
        var infected = new HashSet<int>();
        foreach (var reference in Residents)
        {
            var person = reference.Value;
            if (person.SicknessTicks > 0)
                infected.Add(Index(person.X, person.Y));
        }
        BeginDailyDrinking();
        // 库存只在本阶段按居民顺序记账，阶段结束显式提交聚落。
        var warehouses = new ResourceStock[Settlements.Count];
        foreach (var town in Settlements)
            warehouses[town.Position] = town.Value.Resources;
        try
        {
            // 传播位置已固定，公共库存及取水仍按居民顺序记账；个人转换不读取其他居民。
            foreach (var reference in Residents)
                reference.Replace(Advance(reference));
        }
        finally
        {
            EndDailyDrinking();
            foreach (var town in Settlements)
                town.Replace(town.Value.WithResources(warehouses[town.Position]));
        }

        ArchiveDeadResidents();

        Resident Advance(StateReference<Resident> reference)
        {
            InitializeAgent(reference);
            // 返仓结果直接交给纯结算，身体、补给与出行方式共同生成一份居民。
            var person = reference.Value;
            var inventory = person.Inventory;
            var carriedWater = inventory.Water;
            var agent = person.Agent;
            var travelMode = person.TravelMode;
            if (person.Health > 0 && person.ArmyId == 0
                                  && _settlements.TryGetValue(person.SettlementId, out var home)
                                  && Distance(person.X, person.Y, home.Value.X, home.Value.Y) <= 1
                                  && Walkable(person.X, person.Y, person.Race)
                                  && tick - person.MoveStartedTick >= person.MoveDurationTicks
                                  && !(person.TravelMode == TravelMode.Boat &&
                                       person.Agent.Goal.Kind == AgentGoalKind.Fish))
            {
                var unloaded = UnloadAtHome(person, home.Value.Id, warehouses[home.Position]);
                var supplied = ProvisionAtHome(person, unloaded.Inventory, home.Value, unloaded.Warehouse);
                inventory = supplied.Inventory;
                carriedWater = unloaded.Inventory.Water;
                agent = unloaded.Agent;
                travelMode = unloaded.TravelMode;
                warehouses[home.Position] = supplied.Warehouse;
            }

            var age = rules.Aging ? Math.Min(1000, person.Age + 1d / SimulationTime.TicksPerYear) : person.Age;
            var profession = person.Profession == Profession.Child && age >= ResidentNeedsRules.MinimumWorkAge
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
                    carriedWater, Index(person.X, person.Y), waterUse - inventory.Water)
                : 0;
            var consumeNeeds = person.ArmyId == 0 && hasHome;
            var socialGrowth = hasHome && (tick + person.Id) % SimulationTime.TicksPerDay == 0 ? .07 : 0;
            var arrivedTile = hasHome && person.ArmyId == 0 && person.Health > 0
                              && tick - person.MoveStartedTick == person.MoveDurationTicks
                ? Index(person.X, person.Y)
                : -1;
            return person.CalculateDay(rules, tile.Value, tick, profession, infectionDuration,
                manaRecovery / SimulationTime.TicksPerDay, consumeNeeds, socialGrowth, water, arrivedTile,
                inventory, agent, 1d / SimulationTime.TicksPerDay).Apply(person, travelMode);
        }
    }

    private void GrowSettlements()
    {
        HashSet<string>? usedNames = null;
        foreach (var town in Settlements.ToArray())
        {
            var citizens = _citizens[town.Value.Id];
            // 住宅与发展项目共享材料预算，须为下一项本地建设或研究留出实际交付的材料。
            var developmentReserve = LocalDevelopmentReserve(town);
            if (Rules.Construction && Rules.Expansion && SettlementExpansionError(town.Value.Id) is null
                && ResourceStock.Kinds.All(k =>
                    town.Value.Resources.Get(k) >= SettlementExpansionCost(town.Value.Tier).Get(k) + developmentReserve.Get(k)))
                ExpandTown(town.Value.Id);
            if (Rules.Construction && town.Value.Resources.Food >= citizens.Count * 2 + developmentReserve.Food
                                           && GetHousingCapacity(town.Value.Id) < 60 + Buildings.Where(b =>
                                                   b.Value.SettlementId == town.Value.Id && IsFacilityOperating(b.Value))
                                               .Sum(b => b.Value.Kind == BuildingKind.Farm
                                                   ? 30 * b.Value.Efficiency
                                                   : b.Value.Kind is BuildingKind.AutomatedFarm or BuildingKind.RunicGarden
                                                       ? 120 * b.Value.Efficiency
                                                       : 0)
                                           && citizens.Count > GetHousingCapacity(town.Value.Id) * 0.75
                                           && !Buildings.Any(b =>
                                               b.Value.SettlementId == town.Value.Id && (!b.Value.IsCompleted || b.Value.IsUpgrading))
                                           && town.Value.Resources.Wood >= 25 + developmentReserve.Wood &&
                                           town.Value.Resources.Stone >= 8 + developmentReserve.Stone)
            {
                var site = BestBuildingSite(town, BuildingKind.Housing);
                if (site >= 0)
                {
                    BuildPlannedFacility(town, BuildingKind.Housing, site % Width, site / Width,
                        BuildingPurpose(town, BuildingKind.Housing));
                }
            }

            var adults = citizens.Where(p =>
                    p.Value.Age >= 18 && p.Value.Age < (p.Value.Race == RaceKind.Elf ? 100 : 55) && p.Value.Health >= 60
                    && p.Value.Hunger < 30 && (!Rules.Thirst || p.Value.Thirst < 30) && p.Value.SicknessTicks == 0
                    && p.Value.ArmyId == 0 && p.Value.Agent.DestinationSettlementId == 0
                    && (p.Value.IsInsideHome || Distance(p.Value.X, p.Value.Y, town.Value.X, town.Value.Y) <= 1)
                    && Walkable(p.Value.X, p.Value.Y, p.Value.Race)
                    && SimulationTick - p.Value.MoveStartedTick >= p.Value.MoveDurationTicks)
                .ToArray();
            // 已返家的家庭可用随身口粮抚育下一代；不要求所有食物先积存在公共仓库，也不读取远处背包。
            var familyFood = town.Value.Resources.Food + adults.Sum(p => Math.Max(0, p.Value.Inventory.Food - FoodUse(p) * 2));
            var dailyFood = citizens.Sum(p => FoodUse(p));
            if (Rules.Births && adults.Length >= 6 &&
                familyFood > dailyFood * 4 + .6 && Residents.Count < MaxPopulation)
            {
                var births = Math.Max(1, adults.Length / 28);
                var housing = GetHousingCapacity(town.Value.Id);
                // 拥挤降低出生率，不能完全禁生而让整代成年人同时老去，连住房建成后也无人延续。
                if (citizens.Count >= housing)
                    births = Math.Max(1, births * housing / Math.Max(1, citizens.Count) / 4);
                else
                    births = Math.Min(births, housing - citizens.Count);
                births = Math.Min(births, MaxPopulation - Residents.Count);
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
                        var supplied = Math.Min(remaining, Math.Max(0, parent.Value.Inventory.Food - FoodUse(parent) * 2));
                        parent.Replace(parent.Value.WithInventory(parent.Value.Inventory with { Food = parent.Value.Inventory.Food - supplied }));
                        remaining -= supplied;
                    }

                    var child = NewResident(town, adults[RandomInt(adults.Length)].Value.Race, 0,
                        usedNames ??= CollectResidentNames());
                    child.Replace(child.Value.WithInventory(new ResourceStock { Food = .6 - remaining }));
                    Residents.Add(child);
                    usedNames.Add(child.Value.Name);
                    citizens.Add(child);
                }

                if (SimulationTick % SimulationTime.TicksPerYear == 0)
                    AddEvent(WorldEventKind.Growth, $"{town.Value.Name}迎来新生儿，人口增至{citizens.Count}。", town.Value.X, town.Value.Y);
            }

            if (Rules.Expansion && SimulationTick % SimulationTime.TicksPerYear == 0 && citizens.Count >= 80 &&
                !town.Value.IsExpanding
                && ResourceStock.Kinds.All(k =>
                    town.Value.Resources.Get(k) >= VillageFoundingCost.Get(k) + developmentReserve.Get(k)) &&
                Settlements.Count < 256)
                ExpandSettlement(town, citizens);
        }
    }

    private void ExpandSettlement(StateReference<Settlement> origin, List<StateReference<Resident>> citizens)
    {
        var pioneers = citizens.Where(p => p.Value.ArmyId == 0 && p.Value.Age >= 16 && p.Value.Health >= 60
                                           && p.Value.Agent.DestinationSettlementId == 0 &&
                                           Distance(p.Value.X, p.Value.Y, origin.Value.X, origin.Value.Y) <= 3)
            .OrderByDescending(p => p.Value.Agent.Personality.Ambition).ThenBy(p => p.Value.Id).Take(12).ToArray();
        if (pioneers.Length < 6 || MissingResources(origin.Value.Resources, VillageFoundingCost) is not null)
            return;
        // 建村地点须在出发前报告给原聚落，避免迁徙队伍使用未送达的信息。
        var location = origin.Value.PublicKnowledge.Where(f => f.Kind == AgentFactKind.FoundingSite &&
                                                         f.LearnedTick < SimulationTick
                                                         && SimulationTick - f.ObservedTick <=
                                                         5 * SimulationTime.TicksPerYear &&
                                                         f.Confidence >= .5 &&
                                                         InBounds(f.X, f.Y))
            .Select(f => Index(f.X, f.Y)).Where(i => !IsWaterTerrain(Tiles[i].Value.Terrain)
                                                     && pioneers.All(p =>
                                                         RaceTerrainRules.CanWalk(Tiles[i].Value, p.Value.Race)) &&
                                                     Tiles[i].Value.FireTicks == 0
                                                     && !Buildings.Any(b =>
                                                         b.Value.X == i % Width && b.Value.Y == i / Width)
                                                     && Tiles[i].Value.Fertility >= 25 &&
                                                     (Tiles[i].Value.NationId == 0 ||
                                                      Tiles[i].Value.NationId == origin.Value.NationId)
                                                     && Tiles[i].Value.ClaimedSettlementId == 0
                                                     && FoundingSiteSuitable(i,
                                                         pioneers.Select(p => p.Value.Race).Distinct().ToArray())
                                                     && Distance(i % Width, i / Width, origin.Value.X,
                                                         origin.Value.Y) >= MinimumSettlementDistance
                                                     && Settlements.All(t =>
                                                         Distance(t.Value.X, t.Value.Y, i % Width, i / Width) >=
                                                         MinimumSettlementDistance))
            .OrderByDescending(i => Tiles[i].Value.Fertility).ThenBy(i => i).FirstOrDefault(-1);
        if (location < 0)
            return;
        var x = location % Width;
        var y = location / Width;
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
        Settlements.Add(town);
        _settlements[town.Value.Id] = town;
        _citizens[town.Value.Id] = [];
        foreach (var pioneer in pioneers)
        {
            foreach (var resource in ResourceStock.Kinds)
                pioneer.Replace(pioneer.Value.WithInventory(pioneer.Value.Inventory.WithAmount(resource,
                    pioneer.Value.Inventory.Get(resource) + VillageFoundingCost.Get(resource) / pioneers.Length)));
            pioneer.Replace(pioneer.Value with { SettlementId = town.Value.Id });
            var address = new AgentFact
            {
                Id = NewId(),
                Kind = AgentFactKind.SettlementLocation,
                SubjectId = town.Value.Id,
                X = x,
                Y = y,
                Value = town.Value.NationId,
                ObservedTick = SimulationTick,
                LearnedTick = SimulationTick,
                OriginResidentId = pioneer.Value.Id,
                OriginProfession = pioneer.Value.Profession,
                SourceResidentId = pioneer.Value.Id,
                Text = "拓荒队商定的新家园，物资必须亲自带到",
            };
            RememberAgentFact(pioneer, address);
            pioneer.Replace(pioneer.Value.WithAgent(pioneer.Value.Agent.WithGoal(new AgentGoal
            {
                Kind = AgentGoalKind.ReturnHome,
                TargetX = x,
                TargetY = y,
                TargetSettlementId = town.Value.Id,
                StartedTick = SimulationTick,
                Reason =
                    $"原聚落人口 {citizens.Count}，为拓荒扩展家园；已收到建村勘察报告，选址 {x}, {y} 肥力 {Tiles[location].Value.Fertility}/100，周围有可登记陆地，背负粮木石步行建立新家园",
                PlayerDirected = true,
                ReviewTick = SimulationTick + 150,
            })));
            citizens.Remove(pioneer);
            _citizens[town.Value.Id].Add(pioneer);
        }

        Tiles[location].Replace(Tiles[location].Value.WithSettlementId(town.Value.Id));
        ClaimTerritory(town, 4);
        InitializeSociety();
        AddEvent(WorldEventKind.Growth,
            $"{_nations[origin.Value.NationId].Value.Name}派出 {pioneers.Length} 名成年人，携物资前往{town.Value.Name}；仓库等待实物抵达。", x, y);
    }

    private bool FoundingSiteSuitable(int index, ReadOnlySpan<RaceKind> races)
    {
        var x = index % Width;
        var y = index / Width;
        // 建村地点须能连通取得最小占地范围，避免定居在四周无法利用的单个肥沃地格。
        var available = 0;
        for (var nearbyY = Math.Max(0, y - 3); nearbyY <= Math.Min(Height - 1, y + 3); nearbyY++)
        for (var nearbyX = Math.Max(0, x - 3); nearbyX <= Math.Min(Width - 1, x + 3); nearbyX++)
        {
            if ((nearbyX - x) * (nearbyX - x) + (nearbyY - y) * (nearbyY - y) > 9)
                continue;
            var tile = Tiles[Index(nearbyX, nearbyY)].Value;
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
            var tile = Tiles[index];
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

            if (!Rules.FireSpread || SimulationTick % 8 != 0)
                continue;
            var (dx, dy) = Directions[RandomInt(4)];
            var x = index % Width + dx;
            var y = index / Width + dy;
            if (!InBounds(x, y))
                continue;
            var neighborIndex = Index(x, y);
            if (Tiles[neighborIndex].Value.FireTicks == 0 && RandomInt(1000) < GetTileFlammability(x, y) * 120)
                Ignite(neighborIndex);
        }

        foreach (var index in _dryTiles.ToArray())
        {
            var tile = Tiles[index];
            tile.Replace(tile.Value.WithDroughtTicks(tile.Value.DroughtTicks - 1));
            if (tile.Value.DroughtTicks <= 0)
            {
                tile.Replace(tile.Value.WithDroughtTicks(0));
                _dryTiles.Remove(index);
            }
        }

        if (NaturalDisasters && Rules.DisasterFrequency > 0 &&
            SimulationTick % (10 * SimulationTime.TicksPerYear / Rules.DisasterFrequency) == 0 &&
            Residents.Count > 0)
        {
            var person = Residents[RandomInt(Residents.Count)];
            TriggerDisaster(person.Value.X, person.Value.Y, (DisasterKind)RandomInt(Rules.Disease ? 3 : 2),
                2 + Rules.DisasterStrength * 2);
        }
    }

    private void RefreshTerritoryClaims()
    {
        foreach (var town in Settlements)
            town.Replace(town.Value with { MaxClaimRadius = Math.Max(town.Value.MaxClaimRadius,
                Math.Min(17, 6 + (_citizens.GetValueOrDefault(town.Value.Id)?.Count ?? 0) / 15)) });
    }
}
