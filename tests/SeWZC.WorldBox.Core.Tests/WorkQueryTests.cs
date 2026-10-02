using SeWZC.WorldBox.Core;

internal static class WorkQueryTests
{
    public static IEnumerable<(string Name, Action Run)> Cases =>
    [
        ("work queries preserve priority ties and read edits after a simulation phase", WorkSelection),
        ("local medical work sees same-phase migration and excludes archived residents", MedicalMembership)
    ];

    private static WorldEngine Flat()
    {
        var engine = WorldEngine.Create(3107, 64, 64, false);
        foreach (var tile in engine.State.Tiles) { tile.Terrain = TerrainType.Grass; tile.Fertility = 80; }
        engine.ConfigureWorld(new WorldRules
        {
            Births = false, Aging = false, Hunger = false, Disease = false, Construction = false,
            Research = false, Expansion = false, Trade = false, Wars = false, Alliances = false,
            Peace = false, Migration = false, Secession = false
        }, false, false);
        engine.SpawnResidents(16, 32, RaceKind.Human, 6);
        return engine;
    }

    private static void Check(bool valid, string message)
    {
        if (!valid) throw new InvalidOperationException(message);
    }

    private static void Hold(Resident resident, int x, int y, AgentGoalKind goal = AgentGoalKind.Rest)
    {
        resident.X = resident.FromX = x; resident.Y = resident.FromY = y;
        resident.Age = 25; resident.Health = 100; resident.SicknessTicks = 0;
        resident.MagicTraining = 0; resident.Inventory.Food = 2;
        resident.Agent.Goal = new AgentGoal { Kind = goal, TargetX = x, TargetY = y,
            PlayerDirected = true, ReviewTick = 1_000, Reason = "A held work-query fixture" };
    }

    private static Building Facility(WorldEngine engine, Settlement town, BuildingKind kind, int x, int y) => new()
    {
        Id = engine.State.NextId++, SettlementId = town.Id, Kind = kind, X = x, Y = y,
        ConstructionRequired = 30, ConstructionProgress = 30, WorkSlots = 3
    };

    private static void WorkSelection()
    {
        var engine = Flat(); var town = engine.State.Settlements.Single();
        foreach (var resident in engine.State.Residents) Hold(resident, town.X, town.Y);
        var worker = engine.State.Residents[0]; worker.Profession = Profession.Farmer;
        engine.State.Society.Buildings.Clear();
        var first = Facility(engine, town, BuildingKind.Farm, 17, 32);
        var second = Facility(engine, town, BuildingKind.Farm, 16, 33);
        engine.State.Society.Buildings.AddRange([second, first]);
        Check(engine.TryGetLocalWorkTarget(worker, out var x, out var y) && x == first.X && y == first.Y,
            "Equal priority and distance no longer chose the lowest building ID.");
        first.X = 16; first.Y = 32; second.X = 17; second.Y = 32;
        Hold(worker, 17, 32, AgentGoalKind.Work);
        Check(engine.TryGetLocalWorkTarget(worker, out x, out y) && x == second.X && y == second.Y,
            "Target selection did not prefer the closer building before comparing IDs.");
        Check(engine.TryWorkAtBuilding(worker) && first.Workers.Contains(worker.Id) && second.Workers.Count == 0,
            "Immediate work incorrectly used distance ahead of building ID.");
        first.Workers.Clear(); first.Workers.AddRange(engine.State.Residents.Skip(1).Take(3).Select(r => r.Id));
        first.LastWorkedTick = engine.State.Tick;
        Check(engine.TryWorkAtBuilding(worker) && second.Workers.Contains(worker.Id), "A full facility did not reject an additional worker.");
        first.Workers.Clear(); second.Workers.Clear();
        engine.Step();
        var construction = Facility(engine, town, BuildingKind.Workshop, 18, 32);
        construction.ConstructionProgress = 0;
        engine.State.Society.Buildings.Add(construction);
        Check(engine.TryGetLocalWorkTarget(worker, out x, out y) && x == construction.X && y == construction.Y,
            "A work command reused a stale index after a new building was added.");
        engine.State.Society.Buildings.Remove(construction);
        first.Health = 0;
        Check(engine.TryGetLocalWorkTarget(worker, out x, out y) && x == second.X && y == second.Y,
            "A work command did not observe removal and health edits after the phase ended.");
    }

    private static void MedicalMembership()
    {
        var engine = Flat(); engine.SpawnResidents(48, 32, RaceKind.Elf, 6);
        var home = engine.State.Settlements[0]; var destination = engine.State.Settlements[1];
        foreach (var town in engine.State.Settlements) { town.Resources.Food = 500; town.Housing = 100; }
        foreach (var resident in engine.State.Residents)
        {
            var town = engine.State.Settlements.Single(t => t.Id == resident.SettlementId);
            Hold(resident, town.X, town.Y);
        }
        engine.State.Society.Buildings.Clear();
        var originClinic = Facility(engine, home, BuildingKind.Infirmary, home.X, home.Y);
        var destinationClinic = Facility(engine, destination, BuildingKind.Infirmary, destination.X, destination.Y);
        engine.State.Society.Buildings.AddRange([originClinic, destinationClinic]);
        var migrant = engine.State.Residents[0];
        Hold(migrant, destination.X, destination.Y, AgentGoalKind.Migrate);
        migrant.Agent.Goal.TargetSettlementId = destination.Id;
        migrant.Health = 40; migrant.SicknessTicks = 6;
        var dead = engine.State.Residents[1]; dead.Health = 0; dead.SicknessTicks = 2;
        var originDoctor = engine.State.Residents[2];
        Hold(originDoctor, home.X, home.Y, AgentGoalKind.Work);
        var destinationDoctor = engine.State.Residents.First(r => r.SettlementId == destination.Id);
        Hold(destinationDoctor, destination.X, destination.Y, AgentGoalKind.Work);
        engine.Step();
        Check(migrant.SettlementId == destination.Id && migrant.Health > 40 && migrant.SicknessTicks == 4,
            "A destination doctor could not treat a resident who arrived earlier in the same agent phase.");
        Check(destinationClinic.Workers.Contains(destinationDoctor.Id), "The actual treating doctor was not recorded.");
        Check(!engine.State.Residents.Contains(dead), "The medical fixture did not archive the deceased resident before agent actions.");
        Check(originClinic.Workers.Count == 0, "An archived or migrated resident remained a patient at their previous settlement.");
        var resumed = WorldEngine.ImportJson(engine.ExportJson());
        engine.Step(20); resumed.Step(20);
        Check(engine.ExportJson() == resumed.ExportJson(), "Work-query scratch groups changed save/resume behavior.");
    }
}
