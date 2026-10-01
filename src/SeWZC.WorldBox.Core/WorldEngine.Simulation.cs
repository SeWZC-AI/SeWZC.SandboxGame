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
            UpdateEconomy();
            UpdateResidents();
            Reindex();
            if (State.Tick % 12 == 0) GrowSettlements();
            if (State.Tick % 30 == 0) UpdateStrategy();
            UpdateTrade();
            UpdateArmies();
            State.Residents.RemoveAll(r => r.Health <= 0);
            Reindex();
            foreach (var settlement in State.Settlements.Where(s => _citizens[s.Id].Count == 0).ToArray()) RemoveSettlement(settlement, "居民离散，聚落成为遗址");
            RemoveEmptyNations();
            RefreshTotals();
        }
    }

    private void UpdateEconomy()
    {
        foreach (var settlement in State.Settlements)
        {
            var people = _citizens[settlement.Id];
            var environment = Circle(settlement.X, settlement.Y, 4).Select(i => State.Tiles[i]).ToArray();
            var fertility = environment.Average(t => t.IsWalkable ? t.Fertility / 100d * (t.DroughtTicks > 0 ? 0.15 : 1) * (t.FireTicks > 0 ? 0 : 1) : 0);
            var forest = environment.Count(t => t.Terrain == TerrainType.Forest && t.FireTicks == 0);
            var minerals = environment.Count(t => t.Terrain is TerrainType.Mountain or TerrainType.Snow);
            var tech = _nations.GetValueOrDefault(settlement.NationId)?.Technology ?? 1;
            foreach (var person in people)
            {
                if (person.ArmyId != 0 || person.Age < 14) continue;
                var productivity = (person.SicknessTicks > 0 ? 0.4 : 1) * (person.Hunger > 40 ? 0.55 : 1) * (1 + 0.08 * (tech - 1));
                if (person.Profession == Profession.Farmer) settlement.Resources.Food += 0.22 * fertility * productivity;
                if (person.Profession == Profession.Lumberjack) settlement.Resources.Wood += (forest > 0 ? 0.045 : 0.008) * productivity * (person.Race == RaceKind.Elf ? 1.25 : 1);
                if (person.Profession == Profession.Miner)
                {
                    settlement.Resources.Stone += 0.025 * productivity * (person.Race == RaceKind.Dwarf ? 1.4 : 1);
                    settlement.Resources.Ore += (minerals > 0 ? 0.025 : 0.003) * productivity * (person.Race == RaceKind.Dwarf ? 1.4 : 1);
                }
            }
            // Small-scale gathering keeps founding villages viable; barren or burnt land cannot supply it.
            settlement.Resources.Food += people.Count * 0.018 * fertility;
            var requirement = people.Sum(p => p.Age < 14 ? 0.025 : p.Race == RaceKind.Orc ? 0.064 : 0.05);
            var ratio = requirement > 0 ? Math.Min(1, settlement.Resources.Food / requirement) : 1;
            settlement.Resources.Food = Math.Max(0, settlement.Resources.Food - requirement);
            foreach (var person in people) person.Hunger = Math.Clamp(person.Hunger + (ratio < 0.95 ? (1 - ratio) * 2 : -3), 0, 100);
            if (State.Tiles[Index(settlement.X, settlement.Y)].FireTicks > 0)
            {
                settlement.Resources.Food *= 0.96; settlement.Resources.Wood *= 0.96;
                if (settlement.Housing > 10) settlement.Housing--;
            }
            CapResources(settlement.Resources);
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
            person.Age += 1d / 120;
            if (person.Profession == Profession.Child && person.Age >= 14) person.Profession = AssignProfession();
            var maxAge = person.Race switch { RaceKind.Elf => 180, RaceKind.Dwarf => 120, RaceKind.Orc => 70, _ => 90 };
            if (person.Age > maxAge) person.Health -= 0.5;
            if (person.Hunger > 60) person.Health -= 0.55;
            else if (person.SicknessTicks == 0 && person.Age <= maxAge) person.Health = Math.Min(100, person.Health + 0.15);
            var tile = State.Tiles[Index(person.X, person.Y)];
            if (tile.FireTicks > 0) person.Health -= 4;
            if (person.SicknessTicks > 0) { person.SicknessTicks--; person.Health -= 0.5; }
            else if (infected.Contains(Index(person.X, person.Y)) && RandomInt(100) < 3) person.SicknessTicks = 45;
            person.Activity = person.SicknessTicks > 0 ? ResidentActivity.Sick : person.Hunger > 40 ? ResidentActivity.Hungry : person.ArmyId != 0 ? ResidentActivity.Marching : ResidentActivity.Working;
            if (person.Health <= 0) { deaths++; continue; }
            if (person.ArmyId != 0 || (State.Tick + person.Id) % 3 != 0) continue;
            if (!_settlements.TryGetValue(person.SettlementId, out var settlement)) continue;
            var direction = RandomInt(4);
            for (var attempt = 0; attempt < 4; attempt++)
            {
                var (dx, dy) = Directions[(direction + attempt) % 4];
                var xx = person.X + dx; var yy = person.Y + dy;
                if (!Walkable(xx, yy)) continue;
                var distance = Distance(xx, yy, settlement.X, settlement.Y);
                if (distance > 7 && distance >= Distance(person.X, person.Y, settlement.X, settlement.Y)) continue;
                var next = State.Tiles[Index(xx, yy)];
                if (next.FireTicks > 0 || next.NationId != 0 && next.NationId != person.NationId && GetDiplomacy(person.NationId, next.NationId) == DiplomaticStatus.War) continue;
                person.X = xx; person.Y = yy; break;
            }
        }
        State.Residents.RemoveAll(r => r.Health <= 0);
        if (deaths > 0 && (deaths >= 3 || State.Tick % 12 == 0)) AddEvent(WorldEventKind.Death, $"{deaths} 位居民因饥饿、灾害、疾病或衰老逝去。");
    }

    private void GrowSettlements()
    {
        foreach (var town in State.Settlements.ToArray())
        {
            var citizens = _citizens[town.Id];
            if (citizens.Count > town.Housing * 0.75 && town.Resources.Wood >= 25 && town.Resources.Stone >= 8)
            {
                town.Resources.Wood -= 25; town.Resources.Stone -= 8; town.Housing += 20;
                town.Level = Math.Min(5, 1 + town.Housing / 70);
            }
            var adults = citizens.Where(p => p.Age >= 18 && p.Age < (p.Race == RaceKind.Elf ? 100 : 55) && p.Hunger < 30 && p.SicknessTicks == 0).ToArray();
            if (adults.Length >= 6 && citizens.Count < town.Housing && town.Resources.Food > citizens.Count * 0.8 && State.Residents.Count < MaxPopulation)
            {
                var births = Math.Min(Math.Max(1, adults.Length / 28), Math.Min(town.Housing - citizens.Count, MaxPopulation - State.Residents.Count));
                for (var b = 0; b < births; b++)
                {
                    var child = NewResident(town, adults[RandomInt(adults.Length)].Race, 0);
                    State.Residents.Add(child); citizens.Add(child); town.Resources.Food = Math.Max(0, town.Resources.Food - 0.6);
                }
                if (State.Tick % 120 == 0) AddEvent(WorldEventKind.Growth, $"{town.Name}迎来新生儿，人口增至{citizens.Count}。", town.X, town.Y);
            }
            if (State.Tick % 120 == 0 && citizens.Count >= 90 && town.Resources.Food >= 120 && town.Resources.Wood >= 40 && State.Settlements.Count < 256)
                ExpandSettlement(town, citizens);
        }
    }

    private void ExpandSettlement(Settlement origin, List<Resident> citizens)
    {
        for (var attempt = 0; attempt < 10; attempt++)
        {
            var x = origin.X + RandomInt(39) - 19; var y = origin.Y + RandomInt(39) - 19;
            if (!Walkable(x, y) || Distance(x, y, origin.X, origin.Y) < 14 || State.Tiles[Index(x, y)].NationId != 0 || State.Settlements.Any(s => Distance(x, y, s.X, s.Y) < 13)) continue;
            if (FindPath(origin.X, origin.Y, x, y) is null) continue;
            var town = new Settlement { Id = NewId(), Name = PlaceNames[RandomInt(PlaceNames.Length)] + "镇", X = x, Y = y, NationId = origin.NationId, Resources = new ResourceStock { Food = 80, Wood = 20, Stone = 5 } };
            origin.Resources.Food -= 80; origin.Resources.Wood -= 20; origin.Resources.Stone = Math.Max(0, origin.Resources.Stone - 5);
            State.Settlements.Add(town); _settlements[town.Id] = town; _citizens[town.Id] = [];
            foreach (var migrant in citizens.Where(p => p.ArmyId == 0).Take(24).ToArray())
            {
                migrant.SettlementId = town.Id; migrant.X = x; migrant.Y = y;
                citizens.Remove(migrant); _citizens[town.Id].Add(migrant);
            }
            State.Tiles[Index(x, y)].SettlementId = town.Id; ClaimTerritory(town, 6);
            AddEvent(WorldEventKind.Growth, $"{_nations[origin.NationId].Name}因人口增长建立了{town.Name}。", x, y);
            return;
        }
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
        if (State.NaturalDisasters && State.Tick % 600 == 0 && State.Residents.Count > 0)
        {
            var person = State.Residents[RandomInt(State.Residents.Count)];
            TriggerDisaster(person.X, person.Y, (DisasterKind)RandomInt(3), 5);
        }
    }

    private void UpdateStrategy()
    {
        foreach (var nation in State.Nations)
        {
            var towns = State.Settlements.Where(s => s.NationId == nation.Id).ToArray();
            var people = towns.Sum(s => _citizens[s.Id].Count);
            var food = towns.Sum(s => s.Resources.Food);
            nation.Decision = food < people ? "粮食不足：优先生产与寻求贸易" : people > towns.Sum(s => s.Housing) * 0.8 ? "人口增长：修建住房，寻找新聚落" : "储备充足：积累资源，发展聚落";
            foreach (var town in towns) ClaimTerritory(town, Math.Min(17, 6 + _citizens[town.Id].Count / 15));
            if (towns.Length > 0 && nation.Technology < 5 && towns[0].Resources.Ore > nation.Technology * 30 && towns[0].Resources.Wood > 80)
            {
                towns[0].Resources.Ore -= nation.Technology * 30; towns[0].Resources.Wood -= 40; nation.Technology++;
                AddEvent(WorldEventKind.Growth, $"{nation.Name}改进了工具，技术提升至 {nation.Technology} 级。", towns[0].X, towns[0].Y);
            }
            if (State.Tick % 120 != 0 || people < 24 || food >= people * 0.7 || State.Diplomacies.Any(r => r.Status == DiplomaticStatus.War && (r.FirstNationId == nation.Id || r.SecondNationId == nation.Id))) continue;
            var origin = towns.FirstOrDefault(); if (origin is null) continue;
            var target = State.Settlements.Where(s => s.NationId != nation.Id && GetDiplomacy(nation.Id, s.NationId) == DiplomaticStatus.Neutral && Distance(origin.X, origin.Y, s.X, s.Y) <= 60).OrderBy(s => Distance(origin.X, origin.Y, s.X, s.Y)).FirstOrDefault();
            if (target is not null && FindPath(origin.X, origin.Y, target.X, target.Y) is not null && RandomInt(100) < 30)
            {
                SetDiplomacy(nation.Id, target.NationId, DiplomaticStatus.War);
                nation.Decision = "粮食危机：征兵争夺邻国土地";
            }
        }
        if (State.Tick % 60 == 0) PlanTrade();
    }

    private void PlanTrade()
    {
        foreach (var source in State.Settlements)
        {
            if (source.Resources.Food < 40 || State.TradeRoutes.Any(r => r.FromSettlementId == source.Id)) continue;
            var target = State.Settlements.Where(s => s.Id != source.Id && GetDiplomacy(source.NationId, s.NationId) != DiplomaticStatus.War && s.Resources.Food < source.Resources.Food * 0.6 && Distance(source.X, source.Y, s.X, s.Y) < 100).OrderBy(s => Distance(source.X, source.Y, s.X, s.Y)).FirstOrDefault();
            if (target is null) continue;
            var path = FindPath(source.X, source.Y, target.X, target.Y); if (path is null) continue;
            var cargo = Math.Min(30, (source.Resources.Food - target.Resources.Food) * 0.15);
            source.Resources.Food -= cargo;
            State.TradeRoutes.Add(new TradeRoute { FromSettlementId = source.Id, ToSettlementId = target.Id, FoodCargo = cargo, TravelTicks = Math.Max(4, path.Count * 2), RemainingTicks = Math.Max(4, path.Count * 2) });
        }
    }

    private void UpdateTrade()
    {
        foreach (var route in State.TradeRoutes.ToArray())
        {
            if (!_settlements.TryGetValue(route.FromSettlementId, out var from) || !_settlements.TryGetValue(route.ToSettlementId, out var to)) { State.TradeRoutes.Remove(route); continue; }
            if (GetDiplomacy(from.NationId, to.NationId) == DiplomaticStatus.War) { from.Resources.Food += route.FoodCargo; State.TradeRoutes.Remove(route); continue; }
            route.RemainingTicks--;
            if (route.RemainingTicks > 0) continue;
            if (FindPath(from.X, from.Y, to.X, to.Y) is null) from.Resources.Food += route.FoodCargo;
            else
            {
                to.Resources.Food += route.FoodCargo;
                var payment = Math.Min(to.Resources.Wood, route.FoodCargo * 0.4); to.Resources.Wood -= payment; from.Resources.Wood += payment;
                if (from.NationId != to.NationId)
                {
                    var relation = Relation(from.NationId, to.NationId); relation.Opinion = Math.Min(100, relation.Opinion + 5);
                    if (relation.Opinion >= 60 && relation.Status == DiplomaticStatus.Neutral) SetDiplomacy(from.NationId, to.NationId, DiplomaticStatus.Allied);
                }
                AddEvent(WorldEventKind.Trade, $"{from.Name}向{to.Name}运送了 {route.FoodCargo:0} 份粮食，换取木材。", to.X, to.Y);
            }
            State.TradeRoutes.Remove(route);
        }
    }
}
