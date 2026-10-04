namespace SeWZC.WorldBox.Core;

public sealed partial class WorldEngine
{
    public void Tick() => Step();

    public void Step(int steps = 1)
    {
        if (steps is < 0 or > 10_000) throw new ArgumentOutOfRangeException(nameof(steps));
        for (var step = 0; step < steps; step++)
        {
            State.Tick++;
            Reindex();
            UpdateDisasters();
            TickWildlife();
            TickPlants();
            UpdateResidents();
            UpdateAgentNeedsAndActions();
            UpdateLocalCommunication();
            TickSociety();
            TickDiplomacy();
            TickLocalConflicts();
            TickMigrationAndSecession();
            Reindex();
            if (State.Tick % 12 == 0) GrowSettlements();
            if (State.Tick % 30 == 0) RefreshTerritoryClaims();
            UpdateArmies();
            ArchiveDeadResidents();
            Reindex();
            foreach (var settlement in State.Settlements.Where(s => _citizens[s.Id].Count == 0).ToArray()) RemoveSettlement(settlement, "居民离散，聚落成为遗址");
            RemoveEmptyNations();
            ReconcileSocietyTopology();
            RefreshTotals();
            ObserveProjects();
        }
    }

    private static void CapResources(ResourceStock stock)
    {
        stock.Food = Math.Clamp(stock.Food, 0, 1_000_000); stock.Wood = Math.Clamp(stock.Wood, 0, 1_000_000);
        stock.Stone = Math.Clamp(stock.Stone, 0, 1_000_000); stock.Ore = Math.Clamp(stock.Ore, 0, 1_000_000);
        stock.Alloy = Math.Clamp(stock.Alloy, 0, 1_000_000); stock.EnergyCells = Math.Clamp(stock.EnergyCells, 0, 1_000_000);
        stock.Crystals = Math.Clamp(stock.Crystals, 0, 1_000_000);
        foreach (var kind in MineralAndVehicleResources) stock.Set(kind, Math.Clamp(stock.Get(kind), 0, 1_000_000));
    }

    private void UpdateResidents()
    {
        var infected = new HashSet<int>(State.Residents.Where(r => r.SicknessTicks > 0).Select(r => Index(r.X, r.Y)));
        foreach (var person in State.Residents)
        {
            if (State.Rules.Aging) person.Age = Math.Min(1000, person.Age + 1d / 120);
            if (person.Profession == Profession.Child && person.Age >= 14) person.Profession = AssignProfession();
            var maxAge = Lifespan(person.Race);
            if (State.Rules.Aging && person.Age > maxAge) DamageResident(person, .5, DeathCause.OldAge);
            if ((!State.Rules.Hunger || person.Hunger <= 80) && person.Health > 0 && person.SicknessTicks == 0 && person.Age <= maxAge && (!State.Rules.Thirst || person.Thirst <= 95)) person.Health = Math.Min(100, person.Health + 0.15);
            var tile = State.Tiles[Index(person.X, person.Y)];
            if (tile.FireTicks > 0) DamageResident(person, 4, DeathCause.Fire);
            if (person.SicknessTicks > 0)
            {
                person.SicknessTicks--; if (State.Rules.Disease) DamageResident(person, .2, DeathCause.Disease);
                if (person.SicknessTicks == 0) person.DiseaseImmuneUntilTick = State.Tick + 180;
            }
            else if (State.Rules.Disease && person.DiseaseImmuneUntilTick <= State.Tick && (State.Tick + person.Id) % 6 == 0
                && (infected.Contains(Index(person.X, person.Y)) || Directions.Any(d => InBounds(person.X + d.X, person.Y + d.Y)
                    && infected.Contains(Index(person.X + d.X, person.Y + d.Y)))) && RandomInt(100) < 6)
                person.SicknessTicks = 72 + RandomInt(25);
            if (person.Health <= 0) continue;
            if (person.SicknessTicks > 0) person.Activity = ResidentActivity.Sick;
        }
        ArchiveDeadResidents();
    }

    private void GrowSettlements()
    {
        foreach (var town in State.Settlements.ToArray())
        {
            var citizens = _citizens[town.Id];
            // Housing and the local planner share a material budget; growth must leave the next
            // school, research or facility able to start when its physical deliveries arrive.
            var developmentReserve = LocalDevelopmentReserve(town);
            if (State.Rules.Construction && State.Rules.Expansion && SettlementExpansionError(town.Id) is null
                && AdvancementRules.Resources.All(k => town.Resources.Get(k) >= SettlementExpansionCost(town.Tier).Get(k) + developmentReserve.Get(k)))
                ExpandTown(town.Id);
            if (State.Rules.Construction && citizens.Count > GetHousingCapacity(town.Id) * 0.75
                && !State.Society.Buildings.Any(b => b.SettlementId == town.Id && (!b.IsCompleted || b.IsUpgrading))
                && town.Resources.Wood >= 25 + developmentReserve.Wood && town.Resources.Stone >= 8 + developmentReserve.Stone)
            {
                var site = BestBuildingSite(town, BuildingKind.Housing);
                if (site >= 0) BuildPlannedFacility(town, BuildingKind.Housing, site % State.Width, site / State.Width, BuildingPurpose(town, BuildingKind.Housing));
            }
            var adults = citizens.Where(p => p.Age >= 18 && p.Age < (p.Race == RaceKind.Elf ? 100 : 55) && p.Hunger < 30 && p.SicknessTicks == 0).ToArray();
            if (State.Rules.Births && adults.Length >= 6 && citizens.Count < GetHousingCapacity(town.Id) && town.Resources.Food > citizens.Count * 0.8 && State.Residents.Count < MaxPopulation)
            {
                var births = Math.Min(Math.Max(1, adults.Length / 28), Math.Min(GetHousingCapacity(town.Id) - citizens.Count, MaxPopulation - State.Residents.Count));
                for (var b = 0; b < births; b++)
                {
                    var child = NewResident(town, adults[RandomInt(adults.Length)].Race, 0);
                    State.Residents.Add(child); citizens.Add(child); town.Resources.Food = Math.Max(0, town.Resources.Food - 0.6);
                }
                if (State.Tick % 120 == 0) AddEvent(WorldEventKind.Growth, $"{town.Name}迎来新生儿，人口增至{citizens.Count}。", town.X, town.Y);
            }
            if (State.Rules.Expansion && State.Tick % 120 == 0 && citizens.Count >= 80 && !town.IsExpanding
                && AdvancementRules.Resources.All(k => town.Resources.Get(k) >= VillageFoundingCost.Get(k) + developmentReserve.Get(k)) && State.Settlements.Count < 256)
                ExpandSettlement(town, citizens);
        }
    }

    private void ExpandSettlement(Settlement origin, List<Resident> citizens)
    {
        var pioneers = citizens.Where(p => p.ArmyId == 0 && p.Age >= 16 && p.Health >= 60
            && p.Agent.DestinationSettlementId == 0 && Distance(p.X, p.Y, origin.X, origin.Y) <= 3)
            .OrderByDescending(p => p.Agent.Personality.Ambition).ThenBy(p => p.Id).Take(12).ToArray();
        if (pioneers.Length < 6 || MissingResources(origin.Resources, VillageFoundingCost) is not null) return;
        // A previously observed site must have been reported to the origin before departure.
        var location = origin.PublicKnowledge.Where(f => f.Kind == AgentFactKind.FoundingSite && f.LearnedTick < State.Tick
            && State.Tick - f.ObservedTick <= 600 && f.Confidence >= .5 && InBounds(f.X, f.Y))
            .Select(f => Index(f.X, f.Y)).Where(i => !IsWaterTerrain(State.Tiles[i].Terrain)
            && pioneers.All(p => RaceTerrainRules.CanWalk(State.Tiles[i], p.Race)) && State.Tiles[i].FireTicks == 0
            && !State.Society.Buildings.Any(b => b.X == i % State.Width && b.Y == i / State.Width)
            && State.Tiles[i].Fertility >= 25 && (State.Tiles[i].NationId == 0 || State.Tiles[i].NationId == origin.NationId)
            && State.Tiles[i].ClaimedSettlementId == 0
            && FoundingSiteSuitable(i, pioneers.Select(p => p.Race).Distinct().ToArray())
            && Distance(i % State.Width, i / State.Width, origin.X, origin.Y) >= MinimumSettlementDistance
            && State.Settlements.All(t => Distance(t.X, t.Y, i % State.Width, i / State.Width) >= MinimumSettlementDistance))
            .OrderByDescending(i => State.Tiles[i].Fertility).ThenBy(i => i).FirstOrDefault(-1);
        if (location < 0) return;
        var x = location % State.Width; var y = location / State.Width;
        var town = new Settlement { Id = NewId(), Name = NewPlaceName("村"), X = x, Y = y,
            NationId = origin.NationId, CultureId = origin.CultureId, FoundationPending = true, Resources = new ResourceStock() };
        Spend(origin.Resources, VillageFoundingCost);
        State.Settlements.Add(town); _settlements[town.Id] = town; _citizens[town.Id] = [];
        foreach (var pioneer in pioneers)
        {
            foreach (var resource in AdvancementRules.Resources)
                pioneer.Inventory.Set(resource, pioneer.Inventory.Get(resource) + VillageFoundingCost.Get(resource) / pioneers.Length);
            pioneer.SettlementId = town.Id;
            var address = new AgentFact { Id = NewId(), Kind = AgentFactKind.SettlementLocation, SubjectId = town.Id, X = x, Y = y,
                Value = town.NationId, ObservedTick = State.Tick, LearnedTick = State.Tick, OriginResidentId = pioneer.Id,
                OriginProfession = pioneer.Profession, SourceResidentId = pioneer.Id, Text = "拓荒队商定的新家园，物资必须亲自带到" };
            RememberAgentFact(pioneer, address);
            pioneer.Agent.Goal = new AgentGoal { Kind = AgentGoalKind.ReturnHome, TargetX = x, TargetY = y, TargetSettlementId = town.Id,
                StartedTick = State.Tick, Reason = $"原聚落人口 {citizens.Count}，为拓荒扩展家园；已收到建村勘察报告，选址 {x}, {y} 肥力 {State.Tiles[location].Fertility}/100，周围有可登记陆地，背负粮木石步行建立新家园", PlayerDirected = true, ReviewTick = State.Tick + 150 };
            citizens.Remove(pioneer); _citizens[town.Id].Add(pioneer);
        }
        State.Tiles[location].SettlementId = town.Id; ClaimTerritory(town, 4);
        InitializeSociety();
        AddEvent(WorldEventKind.Growth, $"{_nations[origin.NationId].Name}派出 {pioneers.Length} 名成年人，携物资前往{town.Name}；仓库等待实物抵达。", x, y);
    }

    private bool FoundingSiteSuitable(int index, RaceKind[] races)
    {
        var x = index % State.Width; var y = index / State.Width;
        // The crew must be able to establish the minimum footprint, rather
        // than settling on a fertile single tile surrounded by unusable land.
        return Circle(x, y, 3).Count(i => !IsWaterTerrain(State.Tiles[i].Terrain) && State.Tiles[i].FireTicks == 0
            && State.Tiles[i].ClaimedSettlementId == 0 && races.All(race => RaceTerrainRules.CanWalk(State.Tiles[i], race))) >= SettlementActivationArea;
    }

    private void UpdateDisasters()
    {
        foreach (var index in _burningTiles.Order().ToArray())
        {
            var tile = State.Tiles[index];
            if (tile.FireTicks <= 0) { _burningTiles.Remove(index); continue; }
            tile.FireTicks--;
            if (tile.FireTicks == 0)
            {
                EndFire(index, true);
                continue;
            }
            if (!State.Rules.FireSpread || State.Tick % 8 != 0) continue;
            var (dx, dy) = Directions[RandomInt(4)]; var x = index % State.Width + dx; var y = index / State.Width + dy;
            if (!InBounds(x, y)) continue;
            var neighborIndex = Index(x, y);
            if (State.Tiles[neighborIndex].FireTicks == 0 && RandomInt(1000) < GetTileFlammability(x, y) * 120) Ignite(neighborIndex);
        }
        foreach (var index in _dryTiles.ToArray())
            if (--State.Tiles[index].DroughtTicks <= 0) { State.Tiles[index].DroughtTicks = 0; _dryTiles.Remove(index); }
        if (State.NaturalDisasters && State.Rules.DisasterFrequency > 0 && State.Tick % (1200 / State.Rules.DisasterFrequency) == 0 && State.Residents.Count > 0)
        {
            var person = State.Residents[RandomInt(State.Residents.Count)];
            TriggerDisaster(person.X, person.Y, (DisasterKind)RandomInt(State.Rules.Disease ? 3 : 2), 2 + State.Rules.DisasterStrength * 2);
        }
    }

    private void RefreshTerritoryClaims()
    {
        foreach (var town in State.Settlements)
            town.MaxClaimRadius = Math.Max(town.MaxClaimRadius, Math.Min(17, 6 + (_citizens.GetValueOrDefault(town.Id)?.Count ?? 0) / 15));
    }
}
