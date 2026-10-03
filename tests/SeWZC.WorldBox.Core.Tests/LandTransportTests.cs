using System.Text.Json.Nodes;
using SeWZC.WorldBox.Core;

internal static class LandTransportTests
{
    public static IEnumerable<(string Name, Action Run)> Cases =>
    [
        ("land automatic crossings use visible obstacles and route previews stay read only", AutomaticCrossing),
        ("transport docks manufacture boats only from carried materials", BoatProduction),
        ("land distant workers provision a complete gathering trip", DistantGathering),
        ("land research keeps radio modern without a circular power prerequisite", ResearchStages),
        ("land movement distinguishes walking boats aircraft and improved mountains", Traversal),
        ("land farms are built on site and record physical harvest and drought", Farming),
        ("land bridges and mountain passes require labor and resume during construction", Construction),
        ("land deposits remain unknown until technology and local prospecting", Deposits),
        ("land generated mountain components remain small across seeds", Mountains),
        ("transport aircraft crosses mountains with cargo and returns the borrowed vehicle", () => Journey(true)),
        ("transport boats cross rivers with cargo and return to their warehouse", () => Journey(false)),
        ("land and transport corrupt save state is rejected", Persistence)
    ];

    private static (WorldEngine Engine, Settlement Town, Resident Worker) World()
    {
        var engine = WorldEngine.Create(714, 32, 32, false);
        foreach (var tile in engine.State.Tiles)
        { tile.Terrain = TerrainType.Grass; tile.Fertility = 100; tile.Deposit = null; tile.DepositAmount = 0; }
        engine.SpawnResidents(8, 16, RaceKind.Human, 3);
        engine.ConfigureWorld(new WorldRules { Births = false, Aging = false, Hunger = false, Thirst = false, Disease = false,
            Construction = false, Research = false, Expansion = false, Trade = false, Wars = false,
            Alliances = false, Peace = false, Migration = false, Secession = false, ResourceRegeneration = false }, false, false);
        var town = engine.State.Settlements[0];
        town.Resources = new() { Food = 1000, Wood = 1000, Stone = 1000, Ore = 1000, Alloy = 100, EnergyCells = 100, Coal = 100, Oil = 100 };
        foreach (var person in engine.State.Residents)
        { person.MagicTalent = 0; person.Age = 25; person.Inventory.Food = 1.2; Hold(engine, person, town.X, town.Y); }
        var worker = engine.State.Residents.Last(); worker.Profession = Profession.Builder;
        return (engine, town, worker);
    }

    private static void Hold(WorldEngine engine, Resident person, int x, int y)
    {
        person.X = person.FromX = x; person.Y = person.FromY = y;
        person.MoveStartedTick = Math.Max(0, engine.State.Tick - 1); person.MoveDurationTicks = 1;
        person.Agent.Goal = new() { Kind = AgentGoalKind.Rest, TargetX = x, TargetY = y, StartedTick = engine.State.Tick,
            ReviewTick = engine.State.Tick + 1000, PlayerDirected = true };
    }

    private static void DistantGathering()
    {
        var (engine, town, worker) = World();
        engine.State.Society.Buildings.Clear();
        foreach (var tile in engine.State.Tiles) { tile.Terrain = TerrainType.Wetland; tile.ResourceAmount = 0; }
        var forest = engine.State.Tiles[town.Y * 32 + town.X + 6];
        forest.Terrain = TerrainType.Forest; forest.ResourceAmount = 100;
        worker.Profession = Profession.Lumberjack; worker.Inventory.Food = 0; town.Resources.Wood = 0;
        worker.Agent.Goal = new() { Kind = AgentGoalKind.Idle }; worker.Agent.NextThinkTick = engine.State.Tick;
        engine.ConfigureWorld(engine.State.Rules with { Hunger = true }, false, false);
        engine.Step(150);
        Check(town.Resources.Wood >= 3 && forest.Harvested > 0,
            "Workers exhausted their travelling ration before gathering and returning physical wood.");
    }

    [UnitTest]
    private static void AutomaticCrossing()
    {
        var (engine, town, worker) = World();
        engine.GrantReceivedResearch(town.Id, ResearchKind.Logistics);
        engine.ConfigureWorld(engine.State.Rules with { Construction = true }, false, false);
        var tile = engine.State.Tiles[16 * 32 + 15]; tile.Terrain = TerrainType.River;
        Hold(engine, worker, 14, 16); worker.Agent.Goal.Kind = AgentGoalKind.Flee; worker.Agent.Goal.TargetX = 18;
        var saved = engine.ExportJson(); engine.PreviewResidentRoute(worker.Id);
        Check(saved == engine.ExportJson(), "Read-only navigation preview started construction.");
        engine.Step();
        Check(engine.State.Society.Buildings.Any(b => b.Kind == BuildingKind.Bridge && b.X == 15 && b.Y == 16),
            "A qualified traveller failed to plan a visible crossing.");
    }

    [UnitTest]
    private static void BoatProduction()
    {
        var (engine, town, worker) = World(); engine.GrantReceivedResearch(town.Id, ResearchKind.Logistics);
        Check(engine.FacilityPlacementError(town.Id, BuildingKind.Dock, 12, 16) is not null, "Inland dock was accepted.");
        engine.State.Tiles[16 * 32 + 13].Terrain = TerrainType.Water;
        var id = engine.GrantFacility(town.Id, BuildingKind.Dock, 12, 16);
        Hold(engine, worker, 12, 16); worker.Agent.Goal.TargetEntityId = id;
        Check(!engine.TryWorkAtBuilding(worker), "Dock remotely consumed warehouse wood.");
        worker.Inventory.Wood = 4;
        Check(engine.TryWorkAtBuilding(worker) && worker.Inventory.Boats == 1 && worker.Inventory.Wood == 0
            && town.Resources.Boats == 0, "Boat was not manufactured and carried from the dock.");
    }

    [UnitTest]
    private static void ResearchStages()
    {
        var (engine, town, _) = World();
        engine.GrantReceivedResearch(town.Id, ResearchKind.Logistics);
        Check(engine.ResearchPrerequisiteError(town.Id, ResearchKind.SignalNetwork)?.Contains("电气化") == true, "Ancient transport unlocked radio.");
        engine.GrantReceivedResearch(town.Id, ResearchKind.SignalNetwork);
        Check(engine.FacilityPlacementError(town.Id, BuildingKind.SignalTower, 11, 16, true) is not null, "Gift bypassed the radio era.");
        engine.GrantReceivedResearch(town.Id, ResearchKind.Industry);
        Check(engine.ResearchPrerequisiteError(town.Id, ResearchKind.Electrification) is null, "Power still depends on radio.");
        engine.GrantReceivedResearch(town.Id, ResearchKind.Electrification);
        Check(engine.FacilityPlacementError(town.Id, BuildingKind.SignalTower, 11, 16) is null, "Modern radio remains blocked.");
        Check(engine.ResearchPrerequisiteError(town.Id, ResearchKind.Aviation) is null, "Aviation has unreachable prerequisites.");
    }

    [UnitTest]
    private static void Traversal()
    {
        var (engine, _, _) = World();
        var mountain = engine.State.Tiles[16 * 32 + 16]; mountain.Terrain = TerrainType.Mountain;
        Check(!WorldEngine.CanTraverse(mountain, TravelMode.Foot) && !WorldEngine.CanTraverse(mountain, TravelMode.Boat)
            && WorldEngine.CanTraverse(mountain, TravelMode.Aircraft), "Terrain ignored a traveller's capabilities.");
        mountain.Improvement = LandImprovement.MountainPass; mountain.RoadLevel = 3;
        Check(mountain.IsWalkable && engine.GetTerrainMoveCost(16, 16) > engine.GetTerrainMoveCost(15, 16) * 2,
            "Mountain improvement must open a slow route even with a high road level.");
        var river = new Tile { Terrain = TerrainType.River };
        Check(!river.IsWalkable && WorldEngine.CanTraverse(river, TravelMode.Boat), "Boats cannot traverse rivers.");
        river.Improvement = LandImprovement.Bridge; Check(river.IsWalkable, "Completed bridge did not open land travel.");
    }

    [UnitTest]
    private static void Farming()
    {
        var (engine, town, worker) = World();
        var tile = engine.State.Tiles[16 * 32 + 11]; tile.Terrain = TerrainType.Forest;
        var id = engine.BuildFacility(town.Id, BuildingKind.Farm, 11, 16);
        var farm = engine.State.Society.Buildings.Single(b => b.Id == id);
        Check(tile.Improvement == LandImprovement.None, "Farmland appeared before labor.");
        Hold(engine, worker, 11, 16); worker.Agent.Goal.TargetEntityId = id;
        for (var i = 0; i < 80 && !farm.IsCompleted; i++) { engine.State.Tick++; engine.TryWorkAtBuilding(worker); }
        Check(farm.IsCompleted && tile.Improvement == LandImprovement.Farmland && tile.Terrain == TerrainType.Grass,
            "Completed farming work did not transform the land.");
        var stored = town.Resources.Food; var carried = worker.Inventory.Food;
        engine.State.Tick++; engine.TryWorkAtBuilding(worker); var first = worker.Inventory.Food - carried;
        Check(first > 0 && town.Resources.Food == stored && tile.Harvested > 0, "Farm output was not physically carried and recorded.");
        tile.DroughtTicks = 20; carried = worker.Inventory.Food;
        engine.State.Tick++; engine.TryWorkAtBuilding(worker);
        Check(worker.Inventory.Food - carried < first / 2, "Drought failed to reduce real farm production.");
    }

    private static void Construction()
    {
        foreach (var kind in new[] { BuildingKind.Bridge, BuildingKind.MountainPass })
        {
            var (engine, town, worker) = World();
            engine.GrantReceivedResearch(town.Id, ResearchKind.Logistics);
            for (var y = 0; y < 32; y++) engine.State.Tiles[y * 32 + 15].Terrain = kind == BuildingKind.Bridge ? TerrainType.River : TerrainType.Mountain;
            var tile = engine.State.Tiles[16 * 32 + 15]; var wood = town.Resources.Wood;
            var id = engine.BuildFacility(town.Id, kind, 15, 16); var b = engine.State.Society.Buildings.Single(b => b.Id == id);
            Check(!tile.IsWalkable && town.Resources.Wood < wood, "Construction bypassed cost or opened before completion.");
            Hold(engine, worker, 13, 16);
            worker.Agent.Goal.Kind = AgentGoalKind.Work; worker.Agent.Goal.TargetX = 15; worker.Agent.Goal.TargetEntityId = id;
            engine.Step(10); Check(b.ConstructionProgress > 0 && !b.IsCompleted, "Worker never reached the bank/mountain foot.");
            var resumed = WorldEngine.ImportJson(engine.ExportJson());
            engine.Step(55); resumed.Step(55);
            Check(engine.ExportJson() == resumed.ExportJson() && b.IsCompleted && tile.IsWalkable, "Construction failed to resume deterministically.");
            Hold(engine, worker, 14, 16); worker.Agent.Goal.Kind = AgentGoalKind.Flee; worker.Agent.Goal.TargetX = 18;
            engine.Step(35); Check(worker.X >= 16, "Completed crossing is still blocked by the navigator.");
            if (kind == BuildingKind.Bridge)
            {
                Hold(engine, worker, 15, 16); b.Health = 0; engine.Step();
                Check(!tile.IsWalkable && tile.RoadLevel == 0 && engine.State.Tiles[worker.Y * 32 + worker.X].IsWalkable,
                    "Destroyed bridge retained passage or stranded an invalid resident.");
                WorldEngine.ImportJson(engine.ExportJson());
            }
        }
    }

    private static void Deposits()
    {
        var (engine, town, worker) = World();
        town.Resources.Coal = 0; var tile = engine.State.Tiles[16 * 32 + 14];
        tile.Deposit = ResourceKind.Coal; tile.DepositAmount = 6;
        worker.Profession = Profession.Miner; Hold(engine, worker, 14, 16); worker.Agent.Goal.Kind = AgentGoalKind.Work;
        engine.Step(4); Check(!tile.DepositDiscovered && worker.Inventory.Coal == 0, "Ancient worker could see or extract coal.");
        engine.GrantReceivedResearch(town.Id, ResearchKind.Industry); engine.Step(4);
        Check(tile.DepositDiscovered && worker.Inventory.Coal > 0 && town.Resources.Coal == 0
            && Math.Abs(6 - tile.DepositAmount - worker.Inventory.Coal) < 1e-7, "Discovery/extraction did not conserve the actual deposit.");
        Check(engine.GetTileProductionSummary(14, 16).Contains("煤矿藏"), "Discovered resource has no inspection status.");
        var resumed = WorldEngine.ImportJson(engine.ExportJson()); engine.Step(5); resumed.Step(5);
        Check(engine.ExportJson() == resumed.ExportJson(), "A mined deposit failed deterministic restore.");
    }

    [UnitTest]
    private static void Mountains()
    {
        foreach (var seed in new[] { 42, 451, 73921 })
        {
            var engine = WorldEngine.Create(seed, 128, 128, false); var state = engine.State;
            var remaining = Enumerable.Range(0, state.Tiles.Length).Where(i => state.Tiles[i].Terrain == TerrainType.Mountain).ToHashSet();
            Check(remaining.Count < state.Tiles.Length * .07, "Mountains still cover too much of the map.");
            while (remaining.Count > 0)
            {
                var queue = new Queue<int>(); queue.Enqueue(remaining.First()); remaining.Remove(queue.Peek()); var count = 0;
                while (queue.TryDequeue(out var at))
                {
                    count++;
                    foreach (var next in new[] { at % 128 > 0 ? at - 1 : -1, at % 128 < 127 ? at + 1 : -1, at - 128, at + 128 })
                        if (remaining.Remove(next)) queue.Enqueue(next);
                }
                Check(count <= 32, "A generated impassable mountain component exceeds the cap.");
            }
        }
    }

    private static void Journey(bool flying)
    {
        var (engine, home, worker) = World();
        engine.SpawnResidents(25, 16, RaceKind.Elf, 1); var destination = engine.State.Settlements.Last();
        Hold(engine, engine.State.Residents.Last(), 25, 16);
        for (var y = 0; y < 32; y++) engine.State.Tiles[y * 32 + 16].Terrain = flying ? TerrainType.Mountain : TerrainType.River;
        engine.GrantReceivedResearch(home.Id, ResearchKind.Logistics);
        if (flying) { engine.GrantReceivedResearch(home.Id, ResearchKind.Electrification); engine.GrantReceivedResearch(home.Id, ResearchKind.Aviation); home.Resources.Aircraft = 1; }
        else home.Resources.Boats = 1;
        worker.Profession = Profession.Messenger; worker.Inventory.Food = 20;
        var fact = new AgentFact { Id = engine.State.NextId++, Kind = AgentFactKind.SettlementLocation, SubjectId = destination.Id,
            X = destination.X, Y = destination.Y, Value = destination.NationId, OriginResidentId = worker.Id, SourceResidentId = worker.Id };
        worker.Agent.Memory.Add(fact); worker.Agent.CarriedMessages.Add(fact);
        worker.Agent.DestinationSettlementId = destination.Id; worker.Agent.MissionOriginSettlementId = home.Id;
        worker.Agent.Goal = new() { Kind = AgentGoalKind.DeliverMessage, TargetX = destination.X, TargetY = destination.Y,
            TargetSettlementId = destination.Id, ReviewTick = 1000, PlayerDirected = true };
        var fuel = home.Resources.Oil; var crossed = false; WorldEngine? restored = null;
        for (var i = 0; i < 150; i++)
        {
            engine.Step(); restored?.Step();
            if (worker.X == 16 && !crossed)
            {
                crossed = true; Check(worker.TravelMode == (flying ? TravelMode.Aircraft : TravelMode.Boat), "Crossed without an appropriate vehicle.");
                restored = WorldEngine.ImportJson(engine.ExportJson());
            }
            if (crossed && worker.TravelMode == TravelMode.Foot && worker.Agent.DestinationSettlementId == 0 && worker.X < 16) break;
        }
        Check(crossed && worker.Agent.DestinationSettlementId == 0 && worker.X < 16, "Traveller failed delivery and physical return.");
        Check(home.Resources.Get(flying ? ResourceKind.Aircraft : ResourceKind.Boats) == 1 && worker.TravelMode == TravelMode.Foot,
            "Borrowed vehicle was not physically returned.");
        Check(!flying || home.Resources.Oil < fuel, "Flight did not consume real fuel.");
        Check(restored is not null && restored.ExportJson() == engine.ExportJson(), "Mid-journey restore diverged.");
    }

    [UnitTest]
    private static void Persistence()
    {
        var (engine, _, worker) = World(); var json = engine.ExportJson();
        foreach (var mutate in new Action<JsonNode>[] {
            r => r["Tiles"]![0]!["DepositAmount"] = -1,
            r => r["Tiles"]![0]!["Improvement"] = 3,
            r => r["Residents"]![0]!["TravelMode"] = 2,
            r => r["Residents"]![0]!["Inventory"]!["Coal"] = -1,
            r => r["FormatVersion"] = 6 })
        {
            var data = JsonNode.Parse(json)!; mutate(data);
            try { WorldEngine.ImportJson(data.ToJsonString()); } catch (ArgumentException) { continue; }
            throw new Exception("Corrupt land/transport data was accepted.");
        }
        Check(engine.GetResident(worker.Id) is not null && engine.ExportJson() == json, "Rejected data changed the active world.");
    }

    private static void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
}
