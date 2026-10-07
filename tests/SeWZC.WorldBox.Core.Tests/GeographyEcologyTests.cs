using System.Text.Json.Nodes;
using SeWZC.WorldBox.Core;

internal static class GeographyEcologyTests
{
    public static IEnumerable<(string Name, Action Run)> Cases =>
    [
        ("all generated river scales drain to the sea and mountains remain bounded", Hydrology),
        ("four default tribes have six habitable tiles of radius and own three", StartingHabitats),
        ("streams slow walkers and dwarves can cross natural mountains", Traversal),
        ("altitude changes do not change simulation decisions or terrain costs", Altitude),
        ("animal sizes constrain prey and habitat productivity constrains large animals", AnimalTraits),
        ("finite rainfall sustains drinking without trapping autonomous construction", RainwaterWork),
        ("predators consume eligible herbivores and decline without prey", Predation),
        ("mixed settlements unlock racial buildings and require matching onsite workers", RacialFacilities),
        ("dwarven forge consumes carried material and preserves transported output", Forge),
        ("rainfall river widths and new species survive deterministic save resume", Persistence),
    ];

    private static void Check(bool condition, string message)
    {
        if (!condition)
            throw new Exception(message);
    }

    private static IEnumerable<int> Area(WorldState s, int x, int y, int radius)
    {
        for (var yy = Math.Max(0, y - radius); yy <= Math.Min(s.Height - 1, y + radius); yy++)
        for (var xx = Math.Max(0, x - radius); xx <= Math.Min(s.Width - 1, x + radius); xx++)
            if ((xx - x) * (xx - x) + (yy - y) * (yy - y) <= radius * radius)
                yield return yy * s.Width + xx;
    }

    private static void Hydrology()
    {
        foreach (var (seed, size) in new[] { (42, 64), (73, 128), (451, 256), (-9, 128) })
        {
            var e = WorldEngine.Create(seed, size, size, false);
            var s = e.State;
            var reached = new bool[s.Tiles.Length];
            var queue = new Queue<int>();
            for (var i = 0; i < s.Tiles.Length; i++)
                if (s.Tiles[i].Terrain is TerrainType.Water or TerrainType.DeepWater)
                {
                    reached[i] = true;
                    queue.Enqueue(i);
                }

            while (queue.TryDequeue(out var i))
                foreach (var (dx, dy) in new[] { (1, 0), (-1, 0), (0, 1), (0, -1) })
                {
                    var x = i % size + dx;
                    var y = i / size + dy;
                    if (x < 0 || y < 0 || x >= size || y >= size)
                        continue;
                    var n = y * size + x;
                    if (!reached[n] && WorldEngine.IsWaterTerrain(s.Tiles[n].Terrain))
                    {
                        reached[n] = true;
                        queue.Enqueue(n);
                    }
                }

            Check(s.Tiles.Any(t => t.Terrain == TerrainType.Stream), $"No headwater stream: seed {seed}");
            for (var i = 0; i < s.Tiles.Length; i++)
                if (s.Tiles[i].RiverWidth > 0)
                    Check(reached[i], $"River disconnected from sea: seed {seed}, tile {i}");
            if (size == 256)
            {
                Check(s.Tiles.Any(t => t.RiverWidth is 2 or 3) && s.Tiles.Any(t => t.RiverWidth is 4 or 5),
                    "Missing intermediate or broad rivers");
            }

            Array.Clear(reached);
            for (var i = 0; i < s.Tiles.Length; i++)
            {
                if (reached[i] || s.Tiles[i].Terrain != TerrainType.Mountain)
                    continue;
                var count = 0;
                queue.Enqueue(i);
                reached[i] = true;
                while (queue.TryDequeue(out var current))
                {
                    count++;
                    foreach (var n in Area(s, current % size, current / size, 1))
                        if (!reached[n] && s.Tiles[n].Terrain == TerrainType.Mountain)
                        {
                            reached[n] = true;
                            queue.Enqueue(n);
                        }
                }

                Check(count <= 32, "Oversized connected mountain range");
            }

            Check(s.Tiles.Any(t => t.Rainfall > .015) && s.Tiles.Any(t => t.Fertility > 65 && t.Rainfall < .004),
                "Rain and soil fields are coupled");
        }
    }

    private static void StartingHabitats()
    {
        foreach (var (seed, size) in new[] { (42, 32), (73, 64), (-9, 128), (451, 256) })
        {
            var e = WorldEngine.Create(seed, size, size);
            var s = e.State;
            Check(s.Nations.Count == 4 && s.Settlements.Count == 4 && s.Population == 144, "Missing starting tribes");
            foreach (var town in s.Settlements)
            {
                var race = s.Nations.Single(n => n.Id == town.NationId).FoundingRace;
                Check(Area(s, town.X, town.Y, 6).All(i => RaceTerrainRules.For(race, s.Tiles[i].Terrain).Habitable),
                    "Starting neighborhood contains unsuitable land");
                Check(
                    Area(s, town.X, town.Y, 3).All(i =>
                        s.Tiles[i].NationId == town.NationId && s.Tiles[i].ClaimedSettlementId == town.Id),
                    "Starting three-cell territory is missing");
            }

            _ = WorldEngine.ImportJson(e.ExportJson());
        }
    }

    private static WorldEngine Flat()
    {
        var e = WorldEngine.Create(42, 32, 32, false);
        foreach (var t in e.State.Tiles)
        {
            t.Terrain = TerrainType.Grass;
            t.Fertility = 100;
            t.ResourceAmount = 100;
            t.Rainfall = t.NaturalWaterYield = .03;
            t.Wildlife = WildlifeKind.None;
            t.WildlifePopulation = 0;
            t.OtherWildlife = default;
        }

        e.ConfigureWorld(new WorldRules
        {
            Aging = false,
            Births = false,
            Hunger = false,
            Thirst = false,
            Disease = false,
            Construction = false,
            Research = false,
            Expansion = false,
            Migration = false,
            Trade = false,
            Wars = false,
            Peace = false,
            Alliances = false,
            Secession = false,
            ResourceRegeneration = false,
        }, false, true);
        return e;
    }

    [UnitTest]
    private static void Traversal()
    {
        var e = Flat();
        var tile = e.State.Tiles[16 * 32 + 17];
        tile.Terrain = TerrainType.Stream;
        Check(
            e.CanTraverseStep(16, 16, 17, 16, TravelMode.Foot) &&
            e.GetTerrainMoveCost(17, 16) > e.GetTerrainMoveCost(16, 16), "Creek does not allow slow foot passage");
        tile.Terrain = TerrainType.LargeRiver;
        Check(!e.CanTraverseStep(16, 16, 17, 16, TravelMode.Foot), "Wide river allows unassisted foot crossing");
        tile.Terrain = TerrainType.Mountain;
        Check(
            e.CanTraverseStep(16, 16, 17, 16, TravelMode.Foot, RaceKind.Dwarf) &&
            !e.CanTraverseStep(16, 16, 17, 16, TravelMode.Foot), "Dwarf mountain adaptation is absent");
        e.SpawnResidents(17, 16, RaceKind.Dwarf, 1);
        TestLand.ClaimAllTowns(e);
        Check(e.State.Residents.Single().SettlementId != 0, "Dwarf mountain founding failed");
        e.PaintTerrain(17, 16, TerrainType.Mountain, 0);
        var town = e.State.Settlements.Single();
        Check(tile.NationId == town.NationId && tile.ClaimedSettlementId == town.Id,
            "Painting a dwarf mountain home clears its ownership");
        _ = WorldEngine.ImportJson(e.ExportJson());
    }

    private static void Altitude()
    {
        var a = Flat();
        a.SpawnResidents(16, 16, RaceKind.Human, 4);
        var b = WorldEngine.ImportJson(a.ExportJson());
        foreach (var tile in b.State.Tiles)
            tile.Elevation = (byte)(255 - tile.Elevation);
        Check(a.GetTerrainMoveCost(16, 16) == b.GetTerrainMoveCost(16, 16), "Altitude influences movement");
        var town = a.State.Settlements.Single();
        Check(
            a.BuildingSiteScore(town.Id, BuildingKind.Watchtower, 18, 16) ==
            b.BuildingSiteScore(town.Id, BuildingKind.Watchtower, 18, 16),
            "Altitude influences runtime building siting");
        a.Step(12);
        b.Step(12);

        string Normalize(WorldEngine e)
        {
            var node = JsonNode.Parse(e.ExportJson())!;
            foreach (var t in node["Tiles"]!.AsArray())
                t!["Elevation"] = 0;
            return node.ToJsonString();
        }

        Check(Normalize(a) == Normalize(b), "Altitude changed runtime simulation");
    }

    [UnitTest]
    private static void AnimalTraits()
    {
        foreach (var terrain in Enum.GetValues<TerrainType>())
        {
            var habitat = new Tile
            {
                Terrain = terrain,
                Fertility = 100,
                ResourceAmount = 100,
                Rainfall = .2,
                NaturalWaterYield = .2,
            };
            foreach (var size in Enum.GetValues<AnimalSize>())
            foreach (var diet in Enum.GetValues<AnimalDiet>())
                Check(AnimalRules.Species.Any(s => AnimalRules.For(s).Size == size && AnimalRules.For(s).Diet == diet
                        && AnimalRules.EnvironmentalCapacity(habitat, s) > 0), $"Missing {size}/{diet} in {terrain}");
        }

        Check(AnimalRules.CanPreyOn(WildlifeKind.Wolf, WildlifeKind.Bison),
            "Medium predator cannot hunt large herbivore");
        Check(
            !AnimalRules.CanPreyOn(WildlifeKind.Fox, WildlifeKind.Bison) &&
            !AnimalRules.CanPreyOn(WildlifeKind.Bear, WildlifeKind.Rabbit),
            "Predator hunts prey two size classes away");
        Check(!AnimalRules.CanPreyOn(WildlifeKind.Wolf, WildlifeKind.Fox), "Carnivores hunt other carnivores");
        var t = new Tile
        {
            Terrain = TerrainType.Grass, Fertility = 100, NaturalWaterYield = .03, ResourceAmount = 100,
        };
        Check(WorldEngine.WildlifeCapacity(t, WildlifeKind.Bison) > 0, "Productive habitat excludes large herbivores");
        t.NaturalWaterYield = .001;
        Check(
            WorldEngine.WildlifeCapacity(t, WildlifeKind.Bison) == 0 &&
            WorldEngine.WildlifeCapacity(t, WildlifeKind.Rabbit) > 0, "Water fails to constrain animal size");
        t.Fertility = 20;
        t.NaturalWaterYield = .03;
        Check(WorldEngine.WildlifeCapacity(t, WildlifeKind.Bison) == 0, "Poor soil supports large herbivores");
    }

    private static void RainwaterWork()
    {
        var e = Flat();
        e.SpawnResidents(16, 16, RaceKind.Human, 6);
        TestLand.ClaimAllTowns(e);
        var town = e.State.Settlements.Single();
        town.Resources.Water = 0;
        town.Resources.Food = 300;
        foreach (var person in e.State.Residents)
        {
            person.Inventory.Water = 0;
            person.Profession = Profession.Builder;
        }

        e.State.Rules.Hunger = e.State.Rules.Thirst = e.State.Rules.Construction = true;
        var site = Area(e.State, town.X, town.Y, 3).First(i =>
            e.FacilityPlacementError(town.Id, BuildingKind.Academy, i % 32, i / 32) is null);
        var id = e.BuildFacility(town.Id, BuildingKind.Academy, site % 32, site / 32);
        var building = e.State.Society.Buildings.Single(b => b.Id == id);
        e.Step(120);
        Check(building.IsCompleted,
            $"Residents remained collecting daily drizzle instead of constructing: {building.ConstructionProgress:0.0}; "
            + string.Join(", ", e.State.Residents.Select(p => $"{p.Agent.Goal.Kind}/{p.Agent.Goal.Reason}")));
        Check(e.State.Residents.All(p => p.Thirst < 30 && p.Health == 100),
            "Available rainfall did not sustain actual drinking");
        Check(e.State.Tiles.All(t => t.WaterDrawTick != e.State.Tick || t.WaterDrawn <= t.NaturalWaterYield + .000001),
            "Rainwater exceeded its shared daily allowance");
    }

    private static void Predation()
    {
        var a = Flat();
        foreach (var t in a.State.Tiles)
        {
            t.Wildlife = WildlifeKind.Rabbit;
            t.WildlifePopulation = 4;
            t.OtherWildlife = new WildlifePopulations { Deer = 3, Bison = 2, Wolf = 1, Fox = 1 };
        }

        var b = WorldEngine.ImportJson(a.ExportJson());
        foreach (var t in b.State.Tiles)
        {
            var p = t.OtherWildlife;
            p.Wolf = p.Fox = 0;
            t.OtherWildlife = p;
        }

        a.Step(6);
        b.Step(6);
        Check(
            a.State.Tiles.Sum(t => t.AnimalPopulation(WildlifeKind.Rabbit)) <
            b.State.Tiles.Sum(t => t.AnimalPopulation(WildlifeKind.Rabbit)), "Predation does not remove small prey");
        Check(
            a.State.Tiles.Sum(t => t.AnimalPopulation(WildlifeKind.Bison)) <
            b.State.Tiles.Sum(t => t.AnimalPopulation(WildlifeKind.Bison)),
            "Medium predators do not consume larger prey");
        var starving = Flat();
        foreach (var t in starving.State.Tiles)
        {
            t.Wildlife = WildlifeKind.Wolf;
            t.WildlifePopulation = 1;
        }

        starving.Step(12);
        Check(starving.State.Tiles.Sum(t => t.AnimalPopulation(WildlifeKind.Wolf)) < 1024,
            "Carnivores grow without herbivores");
        Check(starving.State.Tiles.All(t => t.WildlifePopulation >= 0), "Negative animal count");
    }

    [UnitTest]
    private static void RacialFacilities()
    {
        var e = Flat();
        e.SpawnResidents(16, 16, RaceKind.Human, 1);
        var town = e.State.Settlements.Single();
        TestLand.ClaimAllTowns(e);
        Check(e.FacilityPlacementError(town.Id, BuildingKind.WarDrum, 20, 16, true) is not null,
            "Absent race unlocks building");
        e.SpawnResidents(16, 16, RaceKind.Orc, 1);
        var orc = e.State.Residents.Single(p => p.Race == RaceKind.Orc);
        orc.Age = 25;
        TestLand.ClaimAllTowns(e);
        var id = e.GrantFacility(town.Id, BuildingKind.WarDrum, 20, 16);
        var building = e.State.Society.Buildings.Single(b => b.Id == id);
        var human = e.State.Residents.Single(p => p.Race == RaceKind.Human);
        human.X = 20;
        human.Y = 16;
        human.Age = 25;
        human.Agent.Goal = new AgentGoal { Kind = AgentGoalKind.Work, TargetEntityId = id };
        human.Inventory.Food = human.Inventory.Water = 1;
        Check(!e.TryWorkAtBuilding(human), "Wrong race operates special building");
        orc.X = 20;
        orc.Y = 16;
        orc.Agent.Goal = new AgentGoal { Kind = AgentGoalKind.Work, TargetEntityId = id };
        orc.Inventory.Food = orc.Inventory.Water = 1;
        human.Agent.Fatigue = 40;
        Check(e.TryWorkAtBuilding(orc) && human.Agent.Fatigue < 40 && orc.Inventory.Food < 1,
            "Onsite racial service lacks physical inputs or effects");
        e.SpawnResidents(16, 16, RaceKind.Elf, 1);
        TestLand.ClaimAllTowns(e);
        e.State.Tiles[18 * 32 + 18].Terrain = TerrainType.Forest;
        e.GrantFacility(town.Id, BuildingKind.SacredGrove, 18, 18);
        Check(e.State.Tiles[18 * 32 + 18].Terrain == TerrainType.Forest,
            "Sacred grove destroys its own forest habitat");
        var elf = e.State.Residents.Single(p => p.Race == RaceKind.Elf);
        elf.Age = 25;
        elf.MagicTalent = 100;
        var grove = e.State.Society.Buildings.Single(b => b.Kind == BuildingKind.SacredGrove);

        void Arrive(Resident worker, Building facility)
        {
            e.State.Tick++;
            worker.X = worker.FromX = facility.X;
            worker.Y = worker.FromY = facility.Y;
            worker.MoveStartedTick = e.State.Tick - 1;
            worker.MoveDurationTicks = 1;
            worker.Inventory.Food = worker.Inventory.Water = 1;
            worker.Agent.Goal = new AgentGoal
            {
                Kind = AgentGoalKind.Work, TargetEntityId = facility.Id, TargetX = facility.X, TargetY = facility.Y,
            };
        }

        Building Add(BuildingKind kind)
        {
            var site = Area(e.State, 16, 16, 6)
                .First(i => e.FacilityPlacementError(town.Id, kind, i % 32, i / 32, true) is null);
            var added = e.GrantFacility(town.Id, kind, site % 32, site / 32);
            return e.State.Society.Buildings.Single(b => b.Id == added);
        }

        Arrive(elf, grove);
        var training = elf.MagicTraining;
        Check(e.TryWorkAtBuilding(elf) && elf.MagicTraining > training,
            "Sacred grove never trains its actual elf worker");
        var hall = Add(BuildingKind.AssemblyHall);
        Arrive(human, hall);
        orc.X = hall.X;
        orc.Y = hall.Y;
        orc.Agent.SocialNeed = 40;
        Check(e.TryWorkAtBuilding(human) && orc.Agent.SocialNeed < 40, "Assembly hall has no local social effect");
        var guild = Add(BuildingKind.TradeGuild);
        Arrive(human, guild);
        human.Profession = Profession.Trader;
        Check(e.TryWorkAtBuilding(human) && e.IsBuildingOperational(guild), "Guild never becomes staffed");
        human.Agent.Goal = new AgentGoal
        {
            Kind = AgentGoalKind.Explore,
            TargetX = guild.X + 4,
            TargetY = guild.Y,
            PlayerDirected = true,
            StartedTick = e.State.Tick,
            ReviewTick = e.State.Tick + 100,
        };
        e.Step();
        Check(human.MoveDurationTicks < (int)Math.Round(2 * e.GetTerrainMoveCost(human.X, human.Y, human.Race)),
            "Staffed guild does not help its actual moving trader");
        var garden = Add(BuildingKind.HerbGarden);
        Arrive(elf, garden);
        human.X = garden.X;
        human.Y = garden.Y;
        human.Health = 60;
        human.SicknessTicks = 10;
        Check(e.TryWorkAtBuilding(elf) && human.Health > 60 && human.SicknessTicks == 9,
            "Herb garden does not treat a nearby real patient");
        e.SpawnResidents(16, 16, RaceKind.Dwarf, 1);
        var dwarf = e.State.Residents.Single(p => p.Race == RaceKind.Dwarf);
        dwarf.Age = 25;
        TestLand.ClaimAllTowns(e);
        var mine = Add(BuildingKind.MiningHall);
        var rock = e.State.Tiles[mine.Y * 32 + mine.X + 1];
        rock.Terrain = TerrainType.Hills;
        rock.ResourceAmount = 100;
        Arrive(dwarf, mine);
        var stone = dwarf.Inventory.Stone;
        var ore = dwarf.Inventory.Ore;
        Check(
            e.TryWorkAtBuilding(dwarf) && rock.ResourceAmount < 100 && dwarf.Inventory.Stone > stone &&
            dwarf.Inventory.Ore > ore,
            "Mining hall bypasses the finite resource or personal cargo");
        var hunting = Add(BuildingKind.HuntingCamp);
        Arrive(orc, hunting);
        var preyTile = e.State.Tiles[hunting.Y * 32 + hunting.X];
        preyTile.Wildlife = WildlifeKind.Rabbit;
        preyTile.WildlifePopulation = 2;
        Check(e.TryWorkAtBuilding(orc) && preyTile.WildlifePopulation < 2 && orc.Inventory.Food > 1,
            "Hunting camp creates food without catching actual prey");
        _ = WorldEngine.ImportJson(e.ExportJson());
    }

    private static void Forge()
    {
        var e = Flat();
        e.SpawnResidents(16, 16, RaceKind.Dwarf, 1);
        var town = e.State.Settlements.Single();
        TestLand.ClaimAllTowns(e);
        e.State.Society.Research.Single().Completed =
            [Advancement.Agriculture, Advancement.Logistics, Advancement.Industry];
        var id = e.GrantFacility(town.Id, BuildingKind.DwarvenForge, 20, 16);
        var dwarf = e.State.Residents.Single();
        dwarf.Age = 25;
        dwarf.X = 20;
        dwarf.Y = 16;
        dwarf.Inventory.Wood = dwarf.Inventory.Ore = 2;
        dwarf.Agent.Goal = new AgentGoal { Kind = AgentGoalKind.Work, TargetEntityId = id };
        Check(
            e.TryWorkAtBuilding(dwarf) && dwarf.Inventory.Wood == 0 && dwarf.Inventory.Ore == 0 &&
            dwarf.Inventory.Alloy == 1.5 && town.Resources.Alloy == 0,
            "Forge bypasses carried materials or output transport");
        var resumed = WorldEngine.ImportJson(e.ExportJson());
        e.Step(12);
        resumed.Step(12);
        Check(e.ExportJson() == resumed.ExportJson(), "New production diverges after resume");
    }

    private static void Persistence()
    {
        var e = WorldEngine.Create(73, 64, 64);
        e.State.NaturalDisasters = false;
        e.Step(9);
        var restored = WorldEngine.ImportJson(e.ExportJson());
        e.Step(12);
        restored.Step(12);
        Check(e.ExportJson() == restored.ExportJson(), "Expanded ecology or rainfall diverges after resume");
        var invalid = JsonNode.Parse(e.ExportJson())!;
        invalid["Tiles"]![0]!["rain"] = -1;
        try
        {
            WorldEngine.ImportJson(invalid.ToJsonString());
            throw new Exception("Negative rainfall was accepted");
        }
        catch (ArgumentException)
        {
        }

        invalid = JsonNode.Parse(e.ExportJson())!;
        invalid["Tiles"]![0]!["OtherWildlife"] = new JsonArray((int)WildlifeKind.SnowLeopard, -1);
        try
        {
            WorldEngine.ImportJson(invalid.ToJsonString());
            throw new Exception("Corrupt new species was accepted");
        }
        catch (ArgumentException)
        {
        }
    }
}
