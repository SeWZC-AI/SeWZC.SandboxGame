using SeWZC.WorldBox.Core.Runtime;
namespace SeWZC.WorldBox.Core;

public sealed partial class WorldEngine
{
    /// <summary>将世界推进一个模拟日。</summary>
    public void Tick()
    {
        Step();
    }

    /// <summary>将世界推进指定数量的模拟日。</summary>
    /// <param name="steps">推进的日数，范围为 0 至 10,000；为零时不改变世界。</param>
    public void Step(int steps = 1)
    {
        if (steps is < 0 or > 10_000)
            throw new ArgumentOutOfRangeException(nameof(steps));
        for (var step = 0; step < steps; step++)
        {
            using var tileUpdates = Current.Tiles.BeginUpdates();
            using var residentUpdates = Current.Residents.BeginUpdates();
            using var settlementUpdates = Current.Settlements.BeginUpdates();
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
                var buildingsAfterSociety = Current.Society.Buildings.Snapshot;
                TickDiplomacy();
                TickLocalConflicts();
                TickMigrationAndSecession();
                Reindex();
                if (Current.Tick % 12 == 0)
                    GrowSettlements();
                if (Current.Tick % 30 == 0)
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
                    || !ReferenceEquals(Current.Society.Buildings.Snapshot, buildingsAfterSociety))
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
        // 先保存现有病例的位置，避免新感染在同一天沿居民遍历顺序连锁传播。
        var infected = new HashSet<int>(Current.Residents.Where(r => r.SicknessTicks > 0).Select(r => Index(r.X, r.Y)));
        Current.Residents.Transform(person =>
        {
            var age = Current.Rules.Aging ? Math.Min(1000, person.Age + 1d / 120) : person.Age;
            var profession = person.Profession == Profession.Child && age >= 14 ? AssignProfession() : person.Profession;
            var infectionDuration = 0;
            if (person.SicknessTicks == 0 && Current.Rules.Disease && person.DiseaseImmuneUntilTick <= Current.Tick
                && infected.Count > 0 && (Current.Tick + person.Id) % 6 == 0)
            {
                var exposed = infected.Contains(Index(person.X, person.Y));
                foreach (var (dx, dy) in Directions)
                    if (InBounds(person.X + dx, person.Y + dy)
                        && infected.Contains(Index(person.X + dx, person.Y + dy)))
                    {
                        exposed = true;
                        break;
                    }
                if (exposed && RandomInt(100) < 6) infectionDuration = 72 + RandomInt(25);
            }
            var tile = Current.Tiles[Index(person.X, person.Y)];
            var manaRecovery = .025 * Current.Rules.MagicRate * TerrainRules.For(tile.Terrain).ManaRate
                * (.5 + person.MagicTalent / 100)
                * (HasResearch(person.SettlementId, Advancement.ManaAttunement) ? 1.5 : 1);
            var hasHome = _settlements.ContainsKey(person.SettlementId);
            var waterUse = WaterUse(person);
            var water = hasHome && person.ArmyId == 0 && person.Health > 0 && Current.Rules.Thirst
                && person.Inventory.Water < waterUse
                ? WithdrawWater(person, Index(person.X, person.Y), waterUse - person.Inventory.Water) : 0;
            return person.AdvanceDay(Current.Rules, tile, Current.Tick, profession, infectionDuration, manaRecovery,
                person.ArmyId == 0 && hasHome, hasHome && (Current.Tick + person.Id) % 4 == 0 ? .28 : 0, water,
                hasHome && person.ArmyId == 0 && person.Health > 0
                    && Current.Tick - person.MoveStartedTick == person.MoveDurationTicks ? Index(person.X, person.Y) : -1);
        });

        ArchiveDeadResidents();
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
                                         && GetHousingCapacity(town.Id) < 60 + Current.Society.Buildings.Where(b =>
                                                 b.SettlementId == town.Id && IsFacilityOperating(b))
                                             .Sum(b => b.Kind == BuildingKind.Farm
                                                 ? 30 * b.Efficiency
                                                 : b.Kind is BuildingKind.AutomatedFarm or BuildingKind.RunicGarden
                                                     ? 120 * b.Efficiency
                                                     : 0)
                                         && citizens.Count > GetHousingCapacity(town.Id) * 0.75
                                         && !Current.Society.Buildings.Any(b =>
                                             b.SettlementId == town.Id && (!b.IsCompleted || b.IsUpgrading))
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
                    p.Age >= 18 && p.Age < (p.Race == RaceKind.Elf ? 100 : 55) && p.Hunger < 30 && p.SicknessTicks == 0)
                .ToArray();
            if (Current.Rules.Births && adults.Length >= 6 && citizens.Count < GetHousingCapacity(town.Id) &&
                town.Resources.Food > citizens.Count * 0.8 && Current.Residents.Count < MaxPopulation)
            {
                var births = Math.Min(Math.Max(1, adults.Length / 28),
                    Math.Min(GetHousingCapacity(town.Id) - citizens.Count, MaxPopulation - Current.Residents.Count));
                for (var b = 0; b < births; b++)
                {
                    var child = NewResident(town, adults[RandomInt(adults.Length)].Race, 0);
                    Current.Residents.Add(child);
                    citizens.Add(child);
                    town.Resources = town.Resources with { Food = Math.Max(0, town.Resources.Food - 0.6) };
                }

                if (Current.Tick % 120 == 0)
                    AddEvent(WorldEventKind.Growth, $"{town.Name}迎来新生儿，人口增至{citizens.Count}。", town.X, town.Y);
            }

            if (Current.Rules.Expansion && Current.Tick % 120 == 0 && citizens.Count >= 80 && !town.IsExpanding
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
                                                         && Current.Tick - f.ObservedTick <= 600 && f.Confidence >= .5 &&
                                                         InBounds(f.X, f.Y))
            .Select(f => Index(f.X, f.Y)).Where(i => !IsWaterTerrain(Current.Tiles[i].Terrain)
                                                     && pioneers.All(p =>
                                                         RaceTerrainRules.CanWalk(Current.Tiles[i], p.Race)) &&
                                                     Current.Tiles[i].FireTicks == 0
                                                     && !Current.Society.Buildings.Any(b =>
                                                         b.X == i % Current.Width && b.Y == i / Current.Width)
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
        var town = new SettlementCursor
        {
            Id = NewId(),
            Name = NewPlaceName("村"),
            X = x,
            Y = y,
            NationId = origin.NationId,
            CultureId = origin.CultureId,
            FoundationPending = true,
            Resources = new ResourceStock(),
        };
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
            pioneer.Agent.Goal = new AgentGoal
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
            };
            citizens.Remove(pioneer);
            _citizens[town.Id].Add(pioneer);
        }

        Current.Tiles[location].SettlementId = town.Id;
        ClaimTerritory(town, 4);
        InitializeSociety();
        AddEvent(WorldEventKind.Growth,
            $"{_nations[origin.NationId].Name}派出 {pioneers.Length} 名成年人，携物资前往{town.Name}；仓库等待实物抵达。", x, y);
    }

    private bool FoundingSiteSuitable(int index, RaceKind[] races)
    {
        var x = index % Current.Width;
        var y = index / Current.Width;
        // 建村地点须能连通取得最小占地范围，避免定居在四周无法利用的单个肥沃地格。
        return Circle(x, y, 3).Count(i => !IsWaterTerrain(Current.Tiles[i].Terrain) && Current.Tiles[i].FireTicks == 0
            && Current.Tiles[i].ClaimedSettlementId == 0 &&
            races.All(race => RaceTerrainRules.CanWalk(Current.Tiles[i], race))) >= SettlementActivationArea;
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
            Current.Tick % (1200 / Current.Rules.DisasterFrequency) == 0 && Current.Residents.Count > 0)
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
