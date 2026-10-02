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
            UpdateResidents();
            UpdateAgentNeedsAndActions();
            UpdateLocalCommunication();
            TickSociety();
            TickDiplomacy();
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
    }

    private void UpdateResidents()
    {
        var infected = new HashSet<int>(State.Residents.Where(r => r.SicknessTicks > 0).Select(r => Index(r.X, r.Y)));
        var deaths = 0;
        foreach (var person in State.Residents)
        {
            if (State.Rules.Aging) person.Age += 1d / 120;
            if (person.Profession == Profession.Child && person.Age >= 14) person.Profession = AssignProfession();
            var maxAge = person.Race switch { RaceKind.Elf => 180, RaceKind.Dwarf => 120, RaceKind.Orc => 70, _ => 90 };
            if (State.Rules.Aging && person.Age > maxAge) person.Health -= 0.5;
            if (State.Rules.Hunger && person.Hunger > 60) person.Health -= 0.55;
            else if (person.SicknessTicks == 0 && person.Age <= maxAge) person.Health = Math.Min(100, person.Health + 0.15);
            var tile = State.Tiles[Index(person.X, person.Y)];
            if (tile.FireTicks > 0) person.Health -= 4;
            if (person.SicknessTicks > 0) { person.SicknessTicks--; if (State.Rules.Disease) person.Health -= 0.5; }
            else if (State.Rules.Disease && infected.Contains(Index(person.X, person.Y)) && RandomInt(100) < 3) person.SicknessTicks = 45;
            if (person.Health <= 0) { deaths++; continue; }
            if (person.SicknessTicks > 0) person.Activity = ResidentActivity.Sick;
        }
        ArchiveDeadResidents();
        if (deaths > 0 && (deaths >= 3 || State.Tick % 12 == 0)) AddEvent(WorldEventKind.Death, $"{deaths} 位居民因饥饿、灾害、疾病或衰老逝去。");
    }

    private void GrowSettlements()
    {
        foreach (var town in State.Settlements.ToArray())
        {
            var citizens = _citizens[town.Id];
            // Housing and the local planner share a material budget; growth must leave the next
            // school, research or facility able to start when its physical deliveries arrive.
            var developmentReserve = LocalDevelopmentReserve(town);
            if (citizens.Count > town.Housing * 0.75 && town.Resources.Wood >= 25 + developmentReserve.Wood && town.Resources.Stone >= 8 + developmentReserve.Stone)
            {
                town.Resources.Wood -= 25; town.Resources.Stone -= 8; town.Housing += 20;
                town.Level = Math.Min(5, 1 + town.Housing / 70);
            }
            var adults = citizens.Where(p => p.Age >= 18 && p.Age < (p.Race == RaceKind.Elf ? 100 : 55) && p.Hunger < 30 && p.SicknessTicks == 0).ToArray();
            if (State.Rules.Births && adults.Length >= 6 && citizens.Count < town.Housing && town.Resources.Food > citizens.Count * 0.8 && State.Residents.Count < MaxPopulation)
            {
                var births = Math.Min(Math.Max(1, adults.Length / 28), Math.Min(town.Housing - citizens.Count, MaxPopulation - State.Residents.Count));
                for (var b = 0; b < births; b++)
                {
                    var child = NewResident(town, adults[RandomInt(adults.Length)].Race, 0);
                    State.Residents.Add(child); citizens.Add(child); town.Resources.Food = Math.Max(0, town.Resources.Food - 0.6);
                }
                if (State.Tick % 120 == 0) AddEvent(WorldEventKind.Growth, $"{town.Name}迎来新生儿，人口增至{citizens.Count}。", town.X, town.Y);
            }
            if (State.Rules.Expansion && State.Tick % 120 == 0 && citizens.Count >= 60 && town.Resources.Food >= 120 && town.Resources.Wood >= 40 && State.Settlements.Count < 256)
                ExpandSettlement(town, citizens);
        }
    }

    private void ExpandSettlement(Settlement origin, List<Resident> citizens)
    {
        var pioneers = citizens.Where(p => p.ArmyId == 0 && p.Age >= 16 && p.Health >= 60
            && p.Agent.DestinationSettlementId == 0 && Distance(p.X, p.Y, origin.X, origin.Y) <= 3)
            .OrderByDescending(p => p.Agent.Personality.Ambition).ThenBy(p => p.Id).Take(12).ToArray();
        if (pioneers.Length < 6 || origin.Resources.Food < 80 || origin.Resources.Wood < 20 || origin.Resources.Stone < 5) return;
        // Founders select a site that somebody in the present party can actually see.
        var location = Circle(origin.X, origin.Y, 9).Where(i => State.Tiles[i].IsWalkable && State.Tiles[i].FireTicks == 0
            && State.Tiles[i].Fertility >= 25 && (State.Tiles[i].NationId == 0 || State.Tiles[i].NationId == origin.NationId)
            && Distance(i % State.Width, i / State.Width, origin.X, origin.Y) >= 8
            && pioneers.Any(p => Distance(p.X, p.Y, i % State.Width, i / State.Width) <= 6)
            && State.Settlements.All(t => Distance(t.X, t.Y, i % State.Width, i / State.Width) >= 8))
            .OrderByDescending(i => State.Tiles[i].Fertility).ThenBy(i => i).FirstOrDefault(-1);
        if (location < 0) return;
        var x = location % State.Width; var y = location / State.Width;
        var town = new Settlement { Id = NewId(), Name = PlaceNames[State.Settlements.Count % PlaceNames.Length] + "镇", X = x, Y = y,
            NationId = origin.NationId, CultureId = origin.CultureId, Resources = new ResourceStock() };
        origin.Resources.Food -= 80; origin.Resources.Wood -= 20; origin.Resources.Stone -= 5;
        State.Settlements.Add(town); _settlements[town.Id] = town; _citizens[town.Id] = [];
        foreach (var pioneer in pioneers)
        {
            pioneer.Inventory.Food += 80d / pioneers.Length;
            pioneer.Inventory.Wood += 20d / pioneers.Length;
            pioneer.Inventory.Stone += 5d / pioneers.Length;
            pioneer.SettlementId = town.Id;
            var address = new AgentFact { Id = NewId(), Kind = AgentFactKind.SettlementLocation, SubjectId = town.Id, X = x, Y = y,
                Value = town.NationId, ObservedTick = State.Tick, LearnedTick = State.Tick, OriginResidentId = pioneer.Id,
                OriginProfession = pioneer.Profession, SourceResidentId = pioneer.Id, Text = "拓荒队商定的新家园，物资必须亲自带到" };
            RememberAgentFact(pioneer, address);
            pioneer.Agent.Goal = new AgentGoal { Kind = AgentGoalKind.ReturnHome, TargetX = x, TargetY = y, TargetSettlementId = town.Id,
                StartedTick = State.Tick, Reason = "背负原聚落提供的粮木石，步行建立新家园", PlayerDirected = true, ReviewTick = State.Tick + 150 };
            citizens.Remove(pioneer); _citizens[town.Id].Add(pioneer);
        }
        State.Tiles[location].SettlementId = town.Id; ClaimTerritory(town, 4);
        InitializeSociety();
        AddEvent(WorldEventKind.Growth, $"{_nations[origin.NationId].Name}派出 {pioneers.Length} 名成年人，携物资前往{town.Name}；仓库等待实物抵达。", x, y);
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
                if (tile.Terrain == TerrainType.Forest) tile.Terrain = TerrainType.Grass;
                tile.Fertility = (byte)Math.Max(5, tile.Fertility - 25); _burningTiles.Remove(index);
                continue;
            }
            if (State.Tick % 3 != 0 || RandomInt(100) >= 22) continue;
            var (dx, dy) = Directions[RandomInt(4)]; var x = index % State.Width + dx; var y = index / State.Width + dy;
            if (!InBounds(x, y)) continue;
            var neighborIndex = Index(x, y); var neighbor = State.Tiles[neighborIndex];
            if (neighbor.FireTicks == 0 && neighbor.Terrain == TerrainType.Forest) { neighbor.FireTicks = 18; _burningTiles.Add(neighborIndex); }
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
            ClaimTerritory(town, Math.Min(17, 6 + (_citizens.GetValueOrDefault(town.Id)?.Count ?? 0) / 15));
    }
}
