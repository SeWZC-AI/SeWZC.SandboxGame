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
        ("archived histories remain editable after army and terrain changes", ArchivedReferences),
        ("directional diplomacy and remembered order identifiers preserve nonzero and zero values", DirectionalStateRoundTrip),
        ("directional diplomacy and order snapshots reject invalid or missing fields", InvalidDirectionalState),
        ("format four rejects legacy format and simulation versions", RejectLegacyVersions)
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
        engine.ConfigureWorld(new WorldRules { Births = false, Hunger = false, Thirst = false, Migration = false, Expansion = false, Secession = false }, false, false);
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

    private static void DirectionalStateRoundTrip()
    {
        var engine = CreateMilitaryWorld();
        var relation = engine.State.Diplomacies.Single();
        Require(relation.FirstOpinion == -80 && relation.SecondOpinion == -80 && relation.Opinion == -80,
            "An explicit diplomatic command did not initialize both directional opinions.");
        Require(engine.State.Armies.All(a => a.LastOrderFactId > 0 && a.LastOrderFactId < engine.State.NextId),
            "Normal army recruitment did not preserve the order's fact identifier.");
        foreach (var (first, second, aggregate) in new[] { (-40, 71, 16), (-71, 40, -16), (100, -100, 0) })
        {
            relation.FirstOpinion = first; relation.SecondOpinion = second; relation.Opinion = aggregate;
            var restored = AssertRoundTrip(engine);
            var restoredRelation = restored.State.Diplomacies.Single();
            Require(restoredRelation.FirstOpinion == first && restoredRelation.SecondOpinion == second && restoredRelation.Opinion == aggregate,
                "Saving flattened asymmetric opinions or changed a rounded aggregate.");
            Require(restored.State.Armies.Select(a => a.LastOrderFactId).SequenceEqual(engine.State.Armies.Select(a => a.LastOrderFactId)),
                "Saving changed the army's remembered order identity.");
        }
        // Memory has bounded retention; an army must retain its ordering marker after the fact expires.
        var rememberedId = engine.State.Armies[0].LastOrderFactId;
        foreach (var resident in engine.State.Residents.Concat(engine.State.ArchivedResidents))
        {
            resident.Agent.Memory.RemoveAll(f => f.Id == rememberedId);
            resident.Agent.CarriedMessages.RemoveAll(f => f.Id == rememberedId);
        }
        foreach (var town in engine.State.Settlements) town.PublicKnowledge.RemoveAll(f => f.Id == rememberedId);
        foreach (var message in engine.State.PendingMessages) message.Facts.RemoveAll(f => f.Id == rememberedId);
        Require(AssertRoundTrip(engine).State.Armies[0].LastOrderFactId == rememberedId,
            "An expired fact invalidated the army's persistent order marker.");

        relation.FirstOpinion = 0; relation.SecondOpinion = 0; relation.Opinion = 0;
        foreach (var army in engine.State.Armies) army.LastOrderFactId = 0;
        var saved = JsonNode.Parse(engine.ExportJson())!;
        Require(saved["FormatVersion"]!.GetValue<int>() == 11 && saved["SimulationVersion"]!.GetValue<int>() == 11,
            "New worlds did not explicitly save both current version fields.");
        Require(saved["Diplomacies"]![0]!["FirstOpinion"]?.GetValue<int>() == 0
            && saved["Diplomacies"]![0]!["SecondOpinion"]?.GetValue<int>() == 0
            && saved["Armies"]!.AsArray().All(a => a!["LastOrderFactId"]?.GetValue<int>() == 0),
            "A required field with a legal zero value was omitted from the save.");
        var zeroRestored = AssertRoundTrip(engine);
        Require(zeroRestored.State.Diplomacies.Single().FirstOpinion == 0 && zeroRestored.State.Diplomacies.Single().SecondOpinion == 0
            && zeroRestored.State.Armies.All(a => a.LastOrderFactId == 0), "Legal zero values did not survive loading.");
    }

    private static void InvalidDirectionalState()
    {
        var engine = CreateMilitaryWorld();
        foreach (var property in new[] { "FirstOpinion", "SecondOpinion", "Opinion" })
        foreach (var value in new[] { -101, 101 })
            RejectInvalidSave(engine, json => json["Diplomacies"]![0]![property] = value,
                $"Out-of-range {property}={value} was accepted.");
        RejectInvalidSave(engine, json => json["Diplomacies"]![0]!["Opinion"] = -79,
            "An aggregate opinion inconsistent with its directions was accepted.");
        foreach (var (first, second) in new[] { (100, -99), (-100, 99) })
            RejectInvalidSave(engine, json =>
            {
                json["Diplomacies"]![0]!["FirstOpinion"] = first;
                json["Diplomacies"]![0]!["SecondOpinion"] = second;
                json["Diplomacies"]![0]!["Opinion"] = 0;
            }, "A half-point aggregate used a rounding rule inconsistent with simulation.");
        foreach (var value in new[] { -1, engine.State.NextId, int.MaxValue })
            RejectInvalidSave(engine, json => json["Armies"]![0]!["LastOrderFactId"] = value,
                $"Invalid remembered order identifier {value} was accepted.");
        foreach (var property in new[] { "FirstOpinion", "SecondOpinion" })
            RejectInvalidSave(engine, json => json["Diplomacies"]![0]!.AsObject().Remove(property),
                $"Missing required directional field {property} silently defaulted.");
        RejectInvalidSave(engine, json => json["Armies"]![0]!.AsObject().Remove("LastOrderFactId"),
            "A missing required order identifier silently defaulted to its zero sentinel.");
        foreach (var property in new[] { "FormatVersion", "SimulationVersion" })
            RejectInvalidSave(engine, json => json.AsObject().Remove(property),
                $"Missing required version field {property} silently adopted the current version.");
    }

    private static void RejectLegacyVersions()
    {
        var engine = CreateMilitaryWorld();
        foreach (var (format, simulation) in new[] { (3, 5), (5, 3), (3, 3), (4, 5), (5, 4), (4, 4), (5, 6), (6, 5), (5, 5), (6, 6), (6, 7), (7, 6), (7, 7), (8, 7), (7, 8), (9, 9), (10, 9), (9, 10) })
            RejectInvalidSave(engine, json =>
            {
                json["FormatVersion"] = format;
                json["SimulationVersion"] = simulation;
            }, $"Legacy format/simulation versions {format}/{simulation} were accepted.");
    }

    private static WorldEngine CreateMilitaryWorld()
    {
        var engine = CreateWorld(14);
        engine.SpawnResidents(44, 32, RaceKind.Elf, 14);
        engine.ConfigureWorld(new WorldRules
        {
            Births = false, Aging = false, Hunger = false, Thirst = false, Disease = false, Construction = false, Research = false,
            Expansion = false, Trade = false, Wars = false, Alliances = false, Peace = false, Migration = false, Secession = false
        }, false, false);
        foreach (var resident in engine.State.Residents)
        {
            var home = engine.State.Settlements.First(t => t.Id == resident.SettlementId);
            resident.X = resident.FromX = home.X; resident.Y = resident.FromY = home.Y;
            resident.Age = 24; resident.Health = 100; resident.MagicTraining = 0;
            resident.Agent.Goal = new AgentGoal { Kind = AgentGoalKind.Rest, TargetX = home.X, TargetY = home.Y,
                ReviewTick = 1000, PlayerDirected = true };
        }
        var nations = engine.State.Nations.ToArray();
        engine.SetDiplomacy(nations[0].Id, nations[1].Id, DiplomaticStatus.War);
        engine.Step(30);
        Require(engine.State.Armies.Count == 2, "The persistence fixture did not recruit both armies through normal commands.");
        return engine;
    }

    private static void RejectInvalidSave(WorldEngine engine, Action<JsonNode> change, string reason)
    {
        var before = engine.ExportJson();
        var candidate = JsonNode.Parse(before)!;
        change(candidate);
        var rejected = false;
        try { WorldEngine.ImportJson(candidate.ToJsonString()); }
        catch (ArgumentException) { rejected = true; }
        Require(rejected, reason);
        Require(engine.ExportJson() == before, "Rejected import modified the existing world.");
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
