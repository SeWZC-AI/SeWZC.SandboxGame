using System.Text.Json.Nodes;
using SeWZC.WorldBox.Core;

internal static class StoryTests
{
    public static IEnumerable<(string Name, Action Run)> Cases =>
    [
        ("campaign reports travel home before institutions react and resume deterministically", ReportsTravel),
        ("five controlled limited wars reach recovery without repeated conquest over 6000 ticks", LongCampaigns),
        ("old orders cannot remobilize returned armies and recovery blocks new attacks", Recovery),
        ("new military orders cannot cancel a retreat caused by actual supply loss", SupplyRetreat),
        ("limited occupation does not silently retarget a changed destination", ChangedTarget),
        ("defensive orders protect home instead of invading the declarer's capital", Defense),
        ("project estimates use sustained observed work and stop after inactivity or rate changes", Estimates),
        ("construction and research connect actual participants to their completed projects", ProjectStories),
        ("event grouping preserves originals, object boundaries and major turning points", Groups),
        ("accumulated unrest can split a town after its triggering report expires", ExpiredHardship),
        ("story metadata rejects invalid saves while historical references may outlive records", Validation),
    ];

    private static void Check(bool valid, string reason)
    {
        if (!valid)
            throw new Exception(reason);
    }

    private static WorldEngine Flat(bool twoNations = true, int seed = 42)
    {
        var engine = WorldEngine.Create(seed, 64, 64, false);
        foreach (var tile in engine.State.Tiles)
        {
            tile.Terrain = TerrainType.Grass;
            tile.Fertility = 85;
        }

        engine.SpawnResidents(12, 24, RaceKind.Human, 24);
        if (twoNations)
            engine.SpawnResidents(42, 24, RaceKind.Elf, 24);
        engine.ConfigureWorld(new WorldRules
        {
            Births = false,
            Aging = false,
            Hunger = false,
            Thirst = false,
            Disease = false,
            Construction = false,
            Research = false,
            Expansion = false,
            Wars = false,
            Migration = false,
            Secession = false,
        }, false, false);
        foreach (var person in engine.State.Residents)
        {
            person.Age = 24;
            person.Inventory.Food = 0;
            var town = engine.State.Settlements.First(t => t.Id == person.SettlementId);
            person.X = person.FromX = town.X;
            person.Y = person.FromY = town.Y;
            person.Agent.Goal = new AgentGoal
            {
                Kind = AgentGoalKind.Rest,
                TargetX = town.X,
                TargetY = town.Y,
                PlayerDirected = true,
                ReviewTick = 3000,
            };
        }

        foreach (var town in engine.State.Settlements)
            town.Resources = new ResourceStock { Food = 10000, Wood = 1000, Stone = 1000, Ore = 1000 };
        return engine;
    }

    private static Army Muster(WorldEngine engine)
    {
        engine.SetDiplomacy(engine.State.Nations[0].Id, engine.State.Nations[1].Id, DiplomaticStatus.War);
        engine.Step(30);
        return engine.State.Armies.First(a => a.NationId == engine.State.Nations[0].Id);
    }

    private static void ReportsTravel()
    {
        var engine = Flat();
        var army = Muster(engine);
        var nation = engine.State.Nations[0];
        var soldiers = engine.State.Residents.Where(r => r.ArmyId == army.Id).ToArray();
        army.X = army.FromX = 27;
        army.Y = army.FromY = 24;
        army.Supplies = 0;
        army.Gathering = false;
        foreach (var soldier in soldiers)
        {
            soldier.X = soldier.FromX = 27;
            soldier.Y = soldier.FromY = 24;
            soldier.Hunger = 50;
            soldier.Inventory.Food = 0;
        }

        engine.State.Rules.Hunger = true;
        engine.Step();
        Check(army.Retreating && army.Outcome == WarOutcome.SupplyShortage,
            "Army did not retreat from actual hunger and empty supplies");
        Check(nation.Military.ReportedOutcome == WarOutcome.None, "Front-line result teleported to institution");
        Check(soldiers.Any(r => r.Agent.Memory.Any(f => f.Kind == AgentFactKind.WarReport)),
            "Witnesses received no report to carry");
        var resumed = WorldEngine.ImportJson(engine.ExportJson());
        engine.Step(100);
        resumed.Step(100);
        Check(engine.ExportJson() == resumed.ExportJson(), "In-transit military report changed save continuation");
        Check(nation.Military.ReportedOutcome == WarOutcome.SupplyShortage,
            "Report never arrived with returning soldiers");
        Check(nation.Military.LastReportReceivedTick > nation.Military.LastReportObservedTick,
            "Report lost its travel time");
        Check(nation.Military.RecoveryUntilTick > nation.Military.LastReportReceivedTick,
            "Delivered report did not start recovery");
        Check(engine.GetDiplomacy(nation.Id, engine.State.Nations[1].Id) == DiplomaticStatus.Neutral,
            "Delivered retreat report did not initiate ceasefire");
    }

    [LongRunningTest]
    private static void LongCampaigns()
    {
        foreach (var seed in new[] { 73921, 42, 223, 17, 9876 })
        {
            var engine = Flat(seed: seed);
            _ = Muster(engine);
            var retired = false;
            var reported = false;
            var recovered = false;
            for (var elapsed = 30; elapsed < 6000; elapsed += 30)
            {
                engine.Step(30);
                retired |= engine.State.Events.Any(e => e.Action == EventAction.Retreat);
                reported |= engine.State.Events.Any(e => e.Action == EventAction.Report);
                recovered |= engine.State.Nations.Any(n => n.Military.RecoveryUntilTick > engine.State.Tick);
            }

            Check(retired && reported && recovered,
                $"Seed {seed} did not complete physical retreat, report and recovery");
            Check(engine.State.Armies.Count == 0 && engine.State.Population > 0,
                $"Seed {seed} kept fighting or emptied the world");
            Check(engine.State.Diplomacies.All(d => d.Status != DiplomaticStatus.War),
                $"Seed {seed} retained war after reports");
            var resumed = WorldEngine.ImportJson(engine.ExportJson());
            engine.Step(60);
            resumed.Step(60);
            Check(engine.ExportJson() == resumed.ExportJson(), $"Seed {seed} did not resume after its campaign");
        }
    }

    private static void Recovery()
    {
        var engine = Flat();
        var army = Muster(engine);
        var nation = engine.State.Nations[0];
        engine.State.Rules.Peace = false;
        army.Gathering = false;
        army.Morale = 0;
        engine.Step(2);
        Check(!engine.State.Armies.Any(a => a.NationId == nation.Id), "Returned army was not disbanded");
        var mobilized = nation.Military.LastMobilizedOrderId;
        engine.Step(60);
        Check(
            nation.Military.LastMobilizedOrderId == mobilized && !engine.State.Armies.Any(a => a.NationId == nation.Id),
            "An old war order recruited a fresh army");
        engine.SetDiplomacy(nation.Id, engine.State.Nations[1].Id, DiplomaticStatus.War);
        engine.Step(30);
        Check(nation.Military.LastMobilizedOrderId == mobilized, "Recovery allowed a new offensive muster");
    }

    private static void SupplyRetreat()
    {
        var engine = Flat();
        var army = Muster(engine);
        army.X = army.FromX = 27;
        army.Y = army.FromY = 24;
        army.Supplies = 0;
        army.Gathering = false;
        foreach (var soldier in engine.State.Residents.Where(r => r.ArmyId == army.Id))
        {
            soldier.X = soldier.FromX = 27;
            soldier.Y = soldier.FromY = 24;
            soldier.Hunger = 50;
            soldier.Inventory.Food = 0;
        }

        engine.State.Rules.Hunger = true;
        engine.Step();
        Check(army.Outcome == WarOutcome.SupplyShortage, "Fixture did not produce a real supply retreat");
        var campaign = army.CampaignEventId;
        var commander = engine.State.Residents.Single(r => r.Id == army.CommanderId);
        var order = new AgentFact
        {
            Id = engine.State.NextId++,
            Kind = AgentFactKind.WarOrder,
            SubjectId = army.TargetNationId,
            TargetNationId = army.NationId,
            X = army.TargetX,
            Y = army.TargetY,
            ObservedTick = engine.State.Tick,
            LearnedTick = engine.State.Tick,
            OriginResidentId = commander.Id,
            SourceResidentId = commander.Id,
        };
        commander.Agent.Memory.Add(order);
        engine.Step();
        Check(army.LastOrderFactId == order.Id && army.KnownDiplomacy == DiplomaticStatus.War,
            "The new order was not remembered in the command cursor");
        Check(army.Retreating && army.Outcome == WarOutcome.SupplyShortage && army.CampaignEventId == campaign,
            "A newer war order erased the physical reason for retreat");
        var resumed = WorldEngine.ImportJson(engine.ExportJson());
        engine.Step(10);
        resumed.Step(10);
        Check(engine.ExportJson() == resumed.ExportJson(),
            "Retreat with a superseding order did not resume deterministically");
    }

    private static void ChangedTarget()
    {
        var engine = Flat();
        var army = Muster(engine);
        army.TargetX = 36;
        army.TargetY = 24; // 记忆中的目的地，邻近另一处可见城镇。
        army.X = army.FromX = 36;
        army.Y = army.FromY = 24;
        army.Gathering = false;
        foreach (var person in engine.State.Residents.Where(r => r.ArmyId == army.Id))
        {
            person.X = person.FromX = 36;
            person.Y = person.FromY = 24;
        }

        engine.Step();
        Check(army.Outcome == WarOutcome.TargetChanged && army.Retreating,
            "Army substituted a nearby town for its limited objective");
    }

    private static void Defense()
    {
        var engine = Flat();
        _ = Muster(engine);
        var nation = engine.State.Nations[1];
        var capital = engine.State.Settlements.First(t => t.Id == nation.CapitalId);
        var army = engine.State.Armies.Single(a => a.NationId == nation.Id);
        Check(army.Objective == WarObjective.DefendHomeland && army.TargetSettlementId == capital.Id,
            "Defensive order targeted foreign capital");
        engine.Step(5);
        Check(Math.Abs(army.X - capital.X) + Math.Abs(army.Y - capital.Y) <= 2,
            "Defenders left home without a local threat");
    }

    private static void Estimates()
    {
        var engine = Flat(false);
        var town = engine.State.Settlements[0];
        TestLand.ClaimAllTowns(engine);
        var id = engine.BuildFacility(town.Id, BuildingKind.Farm, town.X + 1, town.Y);
        var building = engine.State.Society.Buildings.Single(b => b.Id == id);
        var worker = engine.State.Residents[0];
        worker.Profession = Profession.Builder;
        worker.Agent.Goal.TargetEntityId = id;
        for (var tick = 0; tick < 16; tick++)
        {
            engine.Step();
            Check(engine.TryWorkAtBuilding(worker), "Worker did not reach construction");
        }

        var before = engine.ExportJson();
        var estimate = engine.GetCompletionEstimate(building.Observation, building.ConstructionProgress,
            building.ConstructionRequired);
        Check(estimate.RemainingTicks > 0, "Stable work had no estimate");
        Check(engine.ExportJson() == before, "Reading the estimate modified the world");
        var restored = WorldEngine.ImportJson(before);
        Check(restored.GetDevelopmentEstimate(town.Id) == engine.GetDevelopmentEstimate(town.Id),
            "Estimate samples were lost during load");
        engine.Step(12);
        Check(
            engine.GetCompletionEstimate(building.Observation, building.ConstructionProgress,
                building.ConstructionRequired).RemainingTicks is null, "Stopped work kept a misleading ETA");
        engine.State.Rules.DevelopmentRate = 2;
        Check(engine.GetDevelopmentEstimate(town.Id).RemainingTicks is null, "Rate change reused an old estimate");
    }

    private static void ProjectStories()
    {
        var engine = Flat(false);
        var town = engine.State.Settlements[0];
        TestLand.ClaimAllTowns(engine);
        var id = engine.BuildFacility(town.Id, BuildingKind.Academy, town.X + 1, town.Y);
        var building = engine.State.Society.Buildings.Single(b => b.Id == id);
        var worker = engine.State.Residents[0];
        worker.Profession = Profession.Builder;
        worker.Agent.Goal.TargetEntityId = id;
        while (!building.IsCompleted)
        {
            engine.Step();
            engine.TryWorkAtBuilding(worker);
        }

        var completed =
            engine.State.Events.Last(e => e.Kind == WorldEventKind.Construction && e.Action == EventAction.Completed);
        Check(completed.CauseEventId == building.Observation.StartEventId, "Completion lost the actual start event");
        Check(worker.History.Any(h => h.EventId == completed.Id), "Actual builder was absent from the story");
        Check(!engine.State.Residents[1].History.Any(h => h.EventId == completed.Id),
            "Uninvolved resident was credited");
        engine.StartResearch(town.Id, Advancement.Agriculture);
        var research = engine.State.Society.Research.Single(r => r.SettlementId == town.Id);
        var start = research.Observation.StartEventId;
        while (research.ActiveProject is not null)
        {
            engine.Step();
            engine.TryWorkAtBuilding(worker);
        }

        Check(engine.State.Events.Single(e => e.Id == research.LastCompletionEventId).CauseEventId == start,
            "Research completion lost its project");
        Check(worker.History.Any(h => h.EventId == research.LastCompletionEventId),
            "Actual research contribution was not recorded");
        _ = WorldEngine.ImportJson(engine.ExportJson());
    }

    [UnitTest]
    private static void Groups()
    {
        var entries = Enumerable.Range(1, 4).Select(i => new WorldEvent
        {
            Id = i,
            Tick = i * 10,
            Kind = WorldEventKind.Trade,
            Action = EventAction.Delivery,
            SettlementId = 4,
            ResidentId = 5,
            NationId = 6,
        }).ToList();
        entries[2].SettlementId = 7;
        entries[3].Importance = EventImportance.Major;
        var groups = WorldStories.Group(entries);
        Check(groups.Count == 3 && groups.Sum(g => g.Count) == 4 && groups.Any(g => g.Count == 2),
            "Grouping crossed an object or major-event boundary");
        Check(WorldStories.Involves(entries[0], new ObservedObject(ObservedObjectKind.Resident, 5)),
            "Resident attention missed participation");
        Check(!WorldStories.Involves(entries[0], new ObservedObject(ObservedObjectKind.Settlement, 5)),
            "Attention confused object kinds");
        Check(entries.Select(e => e.Id).SequenceEqual(new[] { 1, 2, 3, 4 }), "Grouping changed original records");
    }

    private static void ExpiredHardship()
    {
        var engine = Flat();
        var town = engine.State.Settlements[1];
        var parent = engine.State.Nations[0].Id;
        engine.TransferTerritory(town.X, town.Y, parent, 0);
        engine.State.Tick = 1199;
        town.Unrest = 100;
        engine.State.Rules.Secession = true;
        engine.State.Society.Reports.Clear();
        engine.Step();
        Check(town.NationId != parent, "Accumulated unrest did not split the town without a retained report");
        var entry = engine.State.Events.Last(e => e.Action == EventAction.Secession);
        Check(entry.EvidenceFactId == 0 && entry.CauseEventId == 0, "Secession invented missing evidence");
        _ = WorldEngine.ImportJson(engine.ExportJson());
    }

    private static void Validation()
    {
        var engine = Flat();
        _ = Muster(engine);
        var json = engine.ExportJson();

        void Reject(Action<JsonNode> mutate)
        {
            var root = JsonNode.Parse(json)!;
            mutate(root);
            try
            {
                WorldEngine.ImportJson(root.ToJsonString());
            }
            catch (ArgumentException)
            {
                return;
            }

            throw new Exception("Invalid story state was accepted");
        }

        Reject(root => root["FormatVersion"] = 3);
        Reject(root => root["Armies"]![0]!["Objective"] = 999);
        Reject(root => root["Events"]![0]!["AdditionalCauseEventIds"] = JsonNode.Parse("[-1]"));
        Reject(root => root["Nations"]![0]!["Military"]!["RecoveryUntilTick"] = -1);
        Reject(root => root["Events"]![0]!["CauseEventId"] = root["Events"]![0]!["Id"]!.GetValue<int>());
        Reject(root => root["Society"]!["Buildings"]![0]!["Observation"]!["Samples"] =
            JsonNode.Parse("[{\"Tick\":99999,\"Progress\":1}]"));
        var beforeEdit = engine.ExportJson();
        try
        {
            engine.EditResident(engine.State.Residents[0].Id,
                new ResidentEdit { History = [new ResidentHistoryEntry { EventId = engine.State.NextId + 1 }] });
            throw new Exception("Invalid history reference was accepted by editor");
        }
        catch (ArgumentException)
        {
            Check(engine.ExportJson() == beforeEdit, "Rejected history edit changed the world");
        }

        engine.State.Events.Clear();
        _ = WorldEngine.ImportJson(engine.ExportJson());
        Check(engine.State.Nations[0].Military.CampaignEventId > 0, "Trimming history erased live campaign provenance");
    }
}
