using System.Text.Json.Nodes;
using SeWZC.WorldBox.Core;

internal static class PersistenceRegressionTests
{
    public static IEnumerable<(string Name, Action Run)> Cases =>
    [
        ("maximum edited age remains valid while aging and after death", MaximumAge),
        ("identity text rejects tabs atomically while personal prose preserves them", IdentityText),
        ("conflicting memory revisions are rejected before allocating identifiers", MemoryRevisions),
        ("archived goals never start missions or consume living supplies", ArchivedMissions),
        ("archived histories remain editable after army and terrain changes", ArchivedReferences)
    ];

    private static void MaximumAge()
    {
        var engine = CreateWorld();
        var id = engine.State.Residents[0].Id;
        engine.EditResident(id, new ResidentEdit { Age = 1000 });
        engine.Tick();
        Require(engine.GetResident(id)!.Age == 1000, "Aging exceeded the maximum accepted resident age.");
        AssertRoundTrip(engine);
        engine.EditResident(id, new ResidentEdit { Health = 0 });
        engine.Tick();
        Require(engine.State.ArchivedResidents.Any(r => r.Id == id && r.Age == 1000),
            "The maximum-age resident did not retain a valid age in their archive.");
        AssertRoundTrip(engine);
    }

    private static void IdentityText()
    {
        var engine = CreateWorld();
        var id = engine.State.Residents[0].Id;
        RejectUnchanged(engine, () => engine.EditResident(id, new ResidentEdit { Name = "A\tB" }));
        RejectUnchanged(engine, () => engine.EditResident(id, new ResidentEdit { Trait = "A\tB" }));
        engine.EditResident(id, new ResidentEdit
        {
            Name = "New name",
            History = [new ResidentHistoryEntry { Text = "Personal\tprose\nremains readable" }]
        });
        var restored = AssertRoundTrip(engine).GetResident(id)!;
        Require(restored.History.Single().Text == "Personal\tprose\nremains readable",
            "Identity validation also rejected or rewrote valid personal prose.");
    }

    private static void MemoryRevisions()
    {
        var engine = CreateWorld();
        var id = engine.State.Residents[0].Id;
        var mind = JsonNode.Parse(engine.ExportResidentMind(id))!;
        var first = mind["Memory"]![0]!.DeepClone();
        var second = first.DeepClone();
        var newFact = first.DeepClone();
        newFact["Id"] = 0;
        first["Text"] = "First revision";
        second["Text"] = "Conflicting revision";
        // A preceding new fact must not consume an ID before the later conflict is detected.
        mind["Memory"] = new JsonArray(newFact, first, second);
        RejectUnchanged(engine, () => engine.EditResidentMindJson(id, mind.ToJsonString()));

        engine.SpawnResidents(44, 32, RaceKind.Elf, 3);
        var target = engine.State.Settlements.Last();
        mind = JsonNode.Parse(engine.ExportResidentMind(id))!;
        mind["Goal"] = MissionGoal(engine, target, AgentGoalKind.DeliverMessage);
        engine.EditResidentMindJson(id, mind.ToJsonString());
        var resident = engine.GetResident(id)!;
        var oldFact = resident.Agent.Memory.First(f => resident.Agent.CarriedMessages.Any(c => c.Id == f.Id));
        var oldId = oldFact.Id;
        var oldText = oldFact.Text;
        mind = JsonNode.Parse(engine.ExportResidentMind(id))!;
        mind["Memory"]!.AsArray().First(f => f!["Id"]!.GetValue<int>() == oldId)!["Text"] = "Edited memory only";
        engine.EditResidentMindJson(id, mind.ToJsonString());
        resident = engine.GetResident(id)!;
        Require(resident.Agent.Memory.Any(f => f.Text == "Edited memory only" && f.Id != oldId)
            && resident.Agent.CarriedMessages.Any(f => f.Id == oldId && f.Text == oldText),
            "Editing one memory also changed the previously prepared message snapshot.");
        AssertRoundTrip(engine);
    }

    private static void ArchivedMissions()
    {
        var engine = CreateWorld();
        engine.SpawnResidents(44, 32, RaceKind.Elf, 3);
        var person = engine.State.Residents[0];
        var home = engine.State.Settlements.First(t => t.Id == person.SettlementId);
        var target = engine.State.Settlements.Last();
        engine.EditResident(person.Id, new ResidentEdit
        {
            Age = 999, Health = 0, X = home.X, Y = home.Y, Inventory = new ResourceStock()
        });
        engine.Tick();
        Require(engine.State.ArchivedResidents.Any(r => r.Id == person.Id), "The archive scenario did not produce a dead resident.");
        var food = home.Resources.Food;
        var inventory = engine.GetResident(person.Id)!.Inventory.Food;
        foreach (var kind in new[] { AgentGoalKind.Trade, AgentGoalKind.DeliverMessage, AgentGoalKind.Petition })
        {
            var mind = JsonNode.Parse(engine.ExportResidentMind(person.Id))!;
            mind["Goal"] = MissionGoal(engine, target, kind);
            engine.EditResidentMindJson(person.Id, mind.ToJsonString());
            Require(home.Resources.Food == food && engine.GetResident(person.Id)!.Inventory.Food == inventory,
                "An archived goal edit consumed living food or loaded a deceased courier.");
            Require(engine.State.Residents.All(r => r.Id != person.Id), "An archive edit revived the deceased resident.");
        }
        AssertRoundTrip(engine);
    }

    private static void ArchivedReferences()
    {
        var engine = CreateWorld(14);
        engine.SpawnResidents(44, 32, RaceKind.Elf, 14);
        engine.ConfigureWorld(new WorldRules { Births = false, Hunger = false, Migration = false, Expansion = false, Secession = false }, false, false);
        foreach (var resident in engine.State.Residents)
        {
            var home = engine.State.Settlements.First(t => t.Id == resident.SettlementId);
            resident.X = resident.FromX = home.X;
            resident.Y = resident.FromY = home.Y;
            resident.Agent.Goal = new AgentGoal { Kind = AgentGoalKind.Rest, TargetX = home.X, TargetY = home.Y, ReviewTick = 1000, PlayerDirected = true };
        }
        var nations = engine.State.Nations.ToArray();
        engine.SetDiplomacy(nations[0].Id, nations[1].Id, DiplomaticStatus.War);
        engine.Step(30);
        var army = engine.State.Armies.First(a => a.NationId == nations[0].Id);
        var soldiers = engine.State.Residents.Where(r => r.ArmyId == army.Id).ToArray();
        foreach (var soldier in soldiers) engine.EditResident(soldier.Id, new ResidentEdit { Health = 0, Age = 999 });
        engine.Tick();
        var archived = engine.State.ArchivedResidents.First(r => r.Id == soldiers[0].Id);
        Require(archived.ArmyId == army.Id && engine.State.Armies.All(a => a.Id != army.Id),
            "The archive scenario needs a soldier whose army has disbanded.");
        engine.EditResident(archived.Id, new ResidentEdit
        {
            History = [new ResidentHistoryEntry { Tick = engine.State.Tick, Text = "Remembered after the army disbanded" }]
        });
        engine.PaintTerrain(archived.X, archived.Y, TerrainType.Water, 0);
        Require(!engine.State.Tiles[archived.Y * engine.State.Width + archived.X].IsWalkable,
            "The historic death position did not become impassable.");
        engine.EditResident(archived.Id, new ResidentEdit { Name = "Remembered soldier" });
        archived = engine.GetResident(archived.Id)!;
        Require(archived.Name == "Remembered soldier" && archived.ArmyId == army.Id && archived.History.Count == 1,
            "Editing an archive rewrote historical affiliation or discarded its history.");
        AssertRoundTrip(engine);
    }

    private static WorldEngine CreateWorld(int population = 3)
    {
        var engine = WorldEngine.Create(42, 64, 64, false);
        engine.State.NaturalDisasters = false;
        foreach (var tile in engine.State.Tiles) { tile.Terrain = TerrainType.Grass; tile.Fertility = 80; }
        engine.SpawnResidents(12, 32, RaceKind.Human, population);
        return engine;
    }

    private static JsonObject MissionGoal(WorldEngine engine, Settlement destination, AgentGoalKind kind) => new()
    {
        ["Kind"] = (int)kind, ["TargetX"] = destination.X, ["TargetY"] = destination.Y,
        ["TargetSettlementId"] = destination.Id, ["StartedTick"] = engine.State.Tick,
        ["ReviewTick"] = engine.State.Tick + 24, ["PlayerDirected"] = true, ["Reason"] = "An edited intention"
    };

    private static void RejectUnchanged(WorldEngine engine, Action action)
    {
        var before = engine.ExportJson();
        var rejected = false;
        try { action(); }
        catch (ArgumentException) { rejected = true; }
        Require(rejected && engine.ExportJson() == before, "An invalid resident edit changed world state or consumed identifiers.");
    }

    private static WorldEngine AssertRoundTrip(WorldEngine engine)
    {
        var saved = engine.ExportJson();
        var restored = WorldEngine.ImportJson(saved);
        Require(restored.ExportJson() == saved, "A successful resident edit did not survive an exact save/load roundtrip.");
        return restored;
    }

    private static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
}
