using System.Text.Json.Nodes;
using SeWZC.WorldBox.Core;

internal static class EditorAndMigrationTests
{
    public static IEnumerable<(string Name, Action Run)> Cases =>
    [
        ("resident edits are atomic, preserve zero values and create actual missions", ResidentEditing),
        ("settlers carry supplies instead of teleporting people or warehouse stock", SettlerSupplies)
    ];

    private static void ResidentEditing()
    {
        var (engine, actor, _, destination) = AgentBehaviorTests.TradeWorld();
        var actorId = actor.Id;
        var before = engine.ExportJson();
        var invalid = JsonNode.Parse(engine.ExportResidentMind(actorId))!;
        invalid["Goal"]!["TargetX"] = -1;
        var rejected = false;
        try { engine.EditResidentMindJson(actorId, invalid.ToJsonString()); }
        catch (ArgumentException) { rejected = true; }
        Require(rejected && engine.ExportJson() == before, "An invalid goal was partially applied to the world.");

        var pastEvents = engine.State.Events.Select(e => (e.Id, e.Tick, e.Kind, e.Message)).ToArray();
        var foodBefore = engine.State.Settlements.Sum(s => s.Resources.Food);
        var courageBefore = actor.Agent.Personality.Courage;
        engine.EditResident(actorId, new ResidentEdit { History = [new ResidentHistoryEntry
        {
            Tick = engine.State.Tick, Text = "Recorded hardship", Experience = PersonalExperienceKind.Hardship, Impact = 1
        }] });
        actor = engine.GetResident(actorId)!;
        Require(actor.Agent.Personality.Courage < courageBefore, "A meaningful history edit did not change subsequent personality.");
        Require(pastEvents.SequenceEqual(engine.State.Events.Take(pastEvents.Length).Select(e => (e.Id, e.Tick, e.Kind, e.Message)))
            && engine.State.Settlements.Sum(s => s.Resources.Food) == foodBefore,
            "Editing personal history recomputed prior events or past resource outcomes.");

        var mind = JsonNode.Parse(engine.ExportResidentMind(actorId))!;
        mind["Memory"]!.AsArray().Add(new JsonObject
        {
            ["Id"] = 0, ["Kind"] = (int)AgentFactKind.Personal, ["SubjectId"] = actorId,
            ["X"] = actor.X, ["Y"] = actor.Y, ["Value"] = 0, ["ObservedTick"] = engine.State.Tick,
            ["LearnedTick"] = engine.State.Tick, ["Confidence"] = 1, ["Text"] = "An edited personal observation"
        });
        mind["Personality"]!["Courage"] = 0;
        mind["Personality"]!["Diligence"] = 0;
        mind["Personality"]!["Sociability"] = 0;
        mind["Personality"]!["Ambition"] = 0;
        mind["JobChangedTick"] = 0;
        engine.EditResidentMindJson(actorId, mind.ToJsonString());
        actor = engine.GetResident(actorId)!;
        var newFact = actor.Agent.Memory.Single(f => f.Text == "An edited personal observation");
        Require(newFact.Id > 0 && newFact.OriginResidentId == actorId && newFact.SourceResidentId == actorId,
            "A new memory did not receive a persistent identity and provenance.");
        engine.EditResident(actorId, new ResidentEdit { Age = 0, Health = 0, Hunger = 0, Mana = 0,
            MagicTalent = 0, MagicTraining = 0, Inventory = new ResourceStock() });
        var restored = WorldEngine.ImportJson(engine.ExportJson()).GetResident(actorId)!;
        Require(restored.Age == 0 && restored.Health == 0 && restored.Mana == 0 && restored.MagicTalent == 0
            && restored.Inventory.Food == 0 && restored.Agent.JobChangedTick == 0
            && restored.Agent.Personality.Courage == 0 && restored.Agent.Personality.Diligence == 0
            && restored.Agent.Personality.Sociability == 0 && restored.Agent.Personality.Ambition == 0,
            "Zero-valued edits reverted to property initializers on save/load.");

        foreach (var kind in new[] { AgentGoalKind.Trade, AgentGoalKind.DeliverMessage, AgentGoalKind.Petition })
        {
            var (missionWorld, courier, source, target) = AgentBehaviorTests.TradeWorld();
            var editedMind = JsonNode.Parse(missionWorld.ExportResidentMind(courier.Id))!;
            editedMind["Goal"] = new JsonObject
            {
                ["Kind"] = (int)kind, ["TargetX"] = target.X, ["TargetY"] = target.Y,
                ["TargetSettlementId"] = target.Id, ["StartedTick"] = missionWorld.State.Tick,
                ["ReviewTick"] = 300, ["PlayerDirected"] = true, ["Reason"] = "A deliberate player instruction"
            };
            missionWorld.EditResidentMindJson(courier.Id, editedMind.ToJsonString());
            courier = missionWorld.GetResident(courier.Id)!;
            Require(courier.Agent.DestinationSettlementId == target.Id,
                $"Editing a {kind} goal did not initialize the actual mission.");
            missionWorld.Step(12);
            Require(courier.Agent.Goal.Kind == kind && Distance(courier, source) > 1 && Distance(courier, target) > 1,
                $"The edited {kind} goal did not produce physical travel while preserving the player's intention.");
            _ = WorldEngine.ImportJson(missionWorld.ExportJson());
        }
    }

    private static void SettlerSupplies()
    {
        var engine = WorldEngine.Create(223, 64, 64, false);
        engine.State.NaturalDisasters = false;
        foreach (var tile in engine.State.Tiles) { tile.Terrain = TerrainType.Grass; tile.Fertility = 80; }
        engine.SpawnResidents(24, 24, RaceKind.Human, 100);
        var origin = engine.State.Settlements.Single();
        engine.SetNationResources(origin.NationId, 10000, 1000, 500, 500);
        foreach (var person in engine.State.Residents)
        {
            person.Age = 60; person.X = person.FromX = origin.X + 2; person.Y = person.FromY = origin.Y;
            person.Inventory.Food = 20;
            person.Agent.Goal = new AgentGoal { Kind = AgentGoalKind.Rest, TargetX = origin.X + 2, TargetY = origin.Y,
                ReviewTick = 1000, PlayerDirected = true };
        }
        engine.State.Tick = 1;
        origin.Resources.Water = 200;
        var scout = engine.State.Residents[0];
        origin.PublicKnowledge.Add(new AgentFact { Id = engine.State.NextId++, Kind = AgentFactKind.FoundingSite,
            SubjectId = origin.Id, X = 42, Y = 24, Value = 80, ObservedTick = 0, LearnedTick = 0,
            OriginResidentId = scout.Id, SourceResidentId = scout.Id, OriginProfession = scout.Profession,
            Text = "已实际递送的建村勘察报告" });
        engine.Step(118);
        var positions = engine.State.Residents.ToDictionary(r => r.Id, r => (r.X, r.Y));
        engine.Tick();
        Require(engine.State.Settlements.Count == 2, "The supplied crowded village did not found a new settlement.");
        var town = engine.State.Settlements.Single(s => s.Id != origin.Id);
        var migrants = engine.State.Residents.Where(r => r.SettlementId == town.Id).ToArray();
        Require(migrants.Length > 0 && migrants.All(r => positions[r.Id] == (r.X, r.Y)),
            "Founding a new settlement teleported its residents.");
        Require(town.Resources.Food == 0 && town.Resources.Wood == 0 && town.Resources.Stone == 0
            && migrants.Sum(r => r.Inventory.Wood) > 0,
            "The new village received stock before its settlers physically carried the supplies there.");
        var arrived = false;
        for (var step = 0; step < 120; step++)
        {
            engine.Tick();
            if (town.Resources.Wood <= 0) continue;
            Require(migrants.Any(r => Distance(r, town) <= 1), "Settler supplies arrived before a settler.");
            arrived = true; break;
        }
        Require(arrived, "The settlers did not deliver their carried founding supplies.");
        _ = WorldEngine.ImportJson(engine.ExportJson());
    }

    private static int Distance(Resident resident, Settlement town) => Math.Abs(resident.X - town.X) + Math.Abs(resident.Y - town.Y);
    private static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
}
