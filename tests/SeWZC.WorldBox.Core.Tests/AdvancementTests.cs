using System.Text.Json.Nodes;
using SeWZC.WorldBox.Core;

internal static class AdvancementTests
{
    public static IEnumerable<(string Name, Action Run)> Cases =>
    [
        ("advancement routes reach their final research through independent physical production", FullRoutes),
        ("advancement production carries inputs and outputs and resumes in transit", Transport),
        ("advancement factories enforce knowledge labor fuel mana and receiving capacity", ProductionGates),
        ("advancement magic switch blocks new development but keeps existing production", MagicSwitch),
        ("advancement planner starts new research and builds unlocked facilities", Planning),
        ("advancement research travels through actual messages without granting prerequisites", Knowledge),
        ("advancement save validation preserves new stocks and rejects legacy or corrupt state", Persistence),
    ];

    private static (WorldEngine Engine, Settlement Town, Resident Worker) World()
    {
        var engine = WorldEngine.Create(223, 64, 64, false);
        foreach (var tile in engine.State.Tiles)
        {
            tile.Terrain = TerrainType.Grass;
            tile.Fertility = 100;
        }

        engine.SpawnResidents(24, 24, RaceKind.Human, 2);
        engine.ConfigureWorld(new WorldRules
        {
            Births = false, Aging = false, Hunger = false, Thirst = false, Disease = false,
            Construction = false, Research = false, Expansion = false, Trade = false, Wars = false,
            Alliances = false, Peace = false, Migration = false, Secession = false, ResourceRegeneration = false,
        }, false, true);
        var town = engine.State.Settlements[0];
        town.Resources = new ResourceStock
            { Food = 10000, Wood = 10000, Stone = 10000, Ore = 10000, Coal = 10000, Oil = 10000, RareEarth = 10000 };
        foreach (var person in engine.State.Residents)
        {
            person.Age = 25;
            person.Inventory = new ResourceStock { Food = 1.2 };
            person.MagicTalent = 0;
            person.MagicTraining = 0;
            Hold(engine, person, town.X, town.Y);
        }

        var worker = engine.State.Residents.Last();
        worker.Profession = Profession.Builder;
        TestLand.ClaimAllTowns(engine);
        return (engine, town, worker);
    }

    private static void Hold(WorldEngine engine, Resident person, int x, int y)
    {
        person.X = person.FromX = x;
        person.Y = person.FromY = y;
        person.MoveStartedTick = Math.Max(0, engine.State.Tick - 1);
        person.MoveDurationTicks = 1;
        person.Agent.Goal = new AgentGoal
        {
            Kind = AgentGoalKind.Rest, TargetX = x, TargetY = y,
            PlayerDirected = true, StartedTick = engine.State.Tick, ReviewTick = engine.State.Tick + 90000,
        };
    }

    private static Building Facility(WorldEngine engine, Settlement town, BuildingKind kind, bool gift = true)
    {
        var positions = from y in Enumerable.Range(town.Y - 5, 11)
            from x in Enumerable.Range(town.X - 5, 11)
            where engine.FacilityPlacementError(town.Id, kind, x, y, gift) is null
            orderby Math.Abs(x - town.X) + Math.Abs(y - town.Y) descending, y, x
            select (x, y);
        var at = positions.First();
        var id = gift
            ? engine.GrantFacility(town.Id, kind, at.x, at.y)
            : engine.BuildFacility(town.Id, kind, at.x, at.y);
        return engine.State.Society.Buildings.Single(b => b.Id == id);
    }

    private static void Know(WorldEngine engine, Settlement town, ResearchKind kind)
    {
        if (kind == ResearchKind.SignalNetwork) Know(engine, town, ResearchKind.Electrification);
        if (AdvancementRules.For(kind) is { } a)
            foreach (var required in a.Prerequisites)
                Know(engine, town, required);
        engine.GrantReceivedResearch(town.Id, kind);
    }

    private static void SendToWork(WorldEngine engine, Resident person, Building building)
    {
        person.Agent.Goal = new AgentGoal
        {
            Kind = AgentGoalKind.Work, TargetEntityId = building.Id,
            TargetX = building.X, TargetY = building.Y, StartedTick = engine.State.Tick,
            PlayerDirected = true, ReviewTick = engine.State.Tick + 500,
        };
        person.Agent.NextThinkTick = engine.State.Tick + 500;
    }

    private static void FullRoutes()
    {
        foreach (var magic in new[] { false, true })
        {
            var (engine, town, worker) = World();
            if (magic)
            {
                worker.MagicTalent = 100;
                worker.MagicTraining = 20;
                worker.Mana = 100;
            }

            foreach (var kind in magic
                         ? new[] { ResearchKind.ArcaneArts, ResearchKind.Logistics }
                         : new[] { ResearchKind.Agriculture, ResearchKind.Logistics }) Know(engine, town, kind);
            var academy = Facility(engine, town, BuildingKind.Academy);

            void Supply(ResourceStock cost)
            {
                if (Math.Abs(worker.X - town.X) + Math.Abs(worker.Y - town.Y) > 1)
                {
                    worker.Agent.Goal = new AgentGoal
                    {
                        Kind = AgentGoalKind.ReturnHome, TargetX = town.X, TargetY = town.Y,
                        StartedTick = engine.State.Tick, PlayerDirected = true, ReviewTick = engine.State.Tick + 1000,
                    };
                    for (var tick = 0;
                         tick < 100 && Math.Abs(worker.X - town.X) + Math.Abs(worker.Y - town.Y) > 1;
                         tick++) engine.Step();
                }

                foreach (var resource in AdvancementRules.Resources.Where(r =>
                             town.Resources.Get(r) + .000001 < cost.Get(r)))
                {
                    var producer = engine.State.Society.Buildings.First(b =>
                        b.SettlementId == town.Id && AdvancementRules.For(b.Kind)?.Output == resource);
                    var deadline = engine.State.Tick + 15000;
                    while (town.Resources.Get(resource) + .000001 < cost.Get(resource) && engine.State.Tick < deadline)
                    {
                        if (Math.Abs(worker.X - town.X) + Math.Abs(worker.Y - town.Y) <= 1 &&
                            engine.State.Tick - worker.MoveStartedTick >= worker.MoveDurationTicks &&
                            worker.Agent.Goal.Kind != AgentGoalKind.Work)
                            SendToWork(engine, worker, producer);
                        engine.Step();
                    }

                    Check(town.Resources.Get(resource) + .000001 >= cost.Get(resource),
                        "Physical production never supplied " + resource +
                        $" tick {engine.State.Tick}, goal {worker.Agent.Goal.Kind}, target {worker.Agent.Goal.TargetEntityId}, at {worker.X},{worker.Y}, stock {town.Resources.Get(resource)}, inv {worker.Inventory.Get(resource)}, inputs ore {worker.Inventory.Ore}, coal {worker.Inventory.Coal}, reason {worker.Agent.Goal.Reason}, home {town.X},{town.Y}, goalXY {worker.Agent.Goal.TargetX},{worker.Agent.Goal.TargetY}, fatigue {worker.Agent.Fatigue}, nav retry {worker.Agent.Goal.NavigationRetryTick}, moves {worker.MoveStartedTick}/{worker.MoveDurationTicks}");
                }
            }

            foreach (var definition in ResearchRules.All.Where(r => ResearchRules.Route(magic).Contains(r.Kind)))
            {
                if (engine.HasResearch(town.Id, definition.Kind)) continue;
                if (AdvancementRules.For(definition.Kind) is not { } a)
                {
                    Supply(definition.Cost);
                    Hold(engine, worker, academy.X, academy.Y);
                    worker.Agent.Goal.TargetEntityId = academy.Id;
                    engine.StartResearch(town.Id, definition.Kind);
                    for (var tick = 0; tick < 1000 && !engine.HasResearch(town.Id, definition.Kind); tick++)
                    {
                        engine.Step();
                        engine.TryWorkAtBuilding(worker);
                    }

                    Check(engine.HasResearch(town.Id, definition.Kind), "Research never completed: " + definition.Kind);
                    continue;
                }

                Supply(a.ResearchCost);
                Hold(engine, worker, academy.X, academy.Y);
                worker.Agent.Goal.TargetEntityId = academy.Id;
                engine.StartResearch(town.Id, a.Research);
                for (var tick = 0; tick < 1000 && !engine.HasResearch(town.Id, a.Research); tick++)
                {
                    engine.Step();
                    engine.TryWorkAtBuilding(worker);
                }

                Check(engine.HasResearch(town.Id, a.Research), "Actual research did not unlock " + a.Research);
                // Walk home before picking up materials for the next facility.
                worker.Agent.Goal = new AgentGoal
                {
                    Kind = AgentGoalKind.ReturnHome, TargetX = town.X, TargetY = town.Y,
                    StartedTick = engine.State.Tick, PlayerDirected = true, ReviewTick = engine.State.Tick + 1000,
                };
                for (var tick = 0;
                     tick < 100 && Math.Abs(worker.X - town.X) + Math.Abs(worker.Y - town.Y) > 1;
                     tick++) engine.Step();
                Supply(a.BuildingCost);
                var building = Facility(engine, town, a.Facility, false);
                Hold(engine, worker, building.X, building.Y);
                worker.Agent.Goal.TargetEntityId = building.Id;
                for (var tick = 0; tick < 100 && !building.IsCompleted; tick++)
                {
                    engine.Step();
                    engine.TryWorkAtBuilding(worker);
                }

                Check(building.IsCompleted, "Advanced facility did not complete through real labor.");
                Hold(engine, worker, town.X, town.Y);
            }

            Check(ResearchRules.Route(magic).All(k => engine.HasResearch(town.Id, k)),
                "The full empire research route was not completed.");
            Check(
                AdvancementRules.All.Where(a => a.Magic != magic && !ResearchRules.For(a.Research).Shared)
                    .All(a => !engine.HasResearch(town.Id, a.Research)), "One route silently granted the other route.");
            var saved = WorldEngine.ImportJson(engine.ExportJson());
            engine.Step(20);
            saved.Step(20);
            Check(engine.ExportJson() == saved.ExportJson(), "Final-era state did not resume deterministically.");
        }
    }

    private static void Transport()
    {
        var (engine, town, worker) = World();
        Know(engine, town, ResearchKind.Industry);
        var building = Facility(engine, town, BuildingKind.Foundry);
        var coal = town.Resources.Coal;
        var ore = town.Resources.Ore;
        SendToWork(engine, worker, building);
        engine.Step();
        Check(town.Resources.Coal == coal - 4 && town.Resources.Ore == ore - 8 && worker.Inventory.Ore == 8,
            "The worker did not physically load its bounded four-batch cargo at home.");
        Check(town.Resources.Alloy == 0 && building.ProductionBatches == 0,
            "Remote factory produced before its carrier arrived.");
        var saved = WorldEngine.ImportJson(engine.ExportJson());
        for (var i = 0; i < 100 && building.ProductionBatches == 0; i++)
        {
            engine.Step();
            saved.Step();
        }

        Check(engine.ExportJson() == saved.ExportJson(), "Carried inputs changed across save/load.");
        Check(building.ProductionBatches == 1 && worker.Inventory.Alloy == 1 && town.Resources.Alloy == 0,
            "Output was not carried by the actual worker.");
        var returnSave = WorldEngine.ImportJson(engine.ExportJson());
        for (var i = 0; i < 100 && town.Resources.Alloy == 0; i++)
        {
            engine.Step();
            returnSave.Step();
        }

        Check(engine.ExportJson() == returnSave.ExportJson() && town.Resources.Alloy == 4,
            "Output failed physical return or deterministic continuation.");
        Check(engine.State.Events.Any(e => e.ResidentId == worker.Id && e.Action == EventAction.Delivery),
            "First production has no observable event.");
        Hold(engine, worker, town.X, town.Y);
        town.Resources.Coal = 1 - .0000001;
        town.Resources.Ore = 2 - .0000001;
        SendToWork(engine, worker, building);
        engine.Step();
        Check(town.Resources.Coal == 0 && town.Resources.Ore == 0,
            "Material precision tolerance created negative warehouse stock.");
        WorldEngine.ImportJson(engine.ExportJson());
    }

    private static void ProductionGates()
    {
        foreach (var a in AdvancementRules.All)
        {
            var (engine, town, worker) = World();
            var building = Facility(engine, town, a.Facility);
            Hold(engine, worker, building.X, building.Y);
            worker.Agent.Goal.TargetEntityId = building.Id;
            worker.Inventory = a.Input.Copy();
            worker.MagicTalent = 100;
            worker.MagicTraining = 20;
            worker.Mana = 100;
            Check(!engine.TryWorkAtBuilding(worker), "Gifted advanced facility bypassed local knowledge.");
            Know(engine, town, a.Research);
            worker.Inventory = new ResourceStock();
            Check(!engine.TryWorkAtBuilding(worker), "Factory consumed warehouse materials at a distance.");
            worker.Inventory = a.Input.Copy();
            worker.Inventory.Set(a.Output, 1_000_000);
            Check(!engine.TryWorkAtBuilding(worker), "Full inventory still accepted production.");
            worker.Inventory = a.Input.Copy();
            if (a.Magic)
            {
                worker.Mana = 0;
                Check(!engine.TryWorkAtBuilding(worker), "Magic production ignored personal mana.");
                worker.Mana = 100;
            }

            var before = worker.Inventory.Copy();
            var mana = worker.Mana;
            Check(engine.TryWorkAtBuilding(worker), "Qualified worker could not process " + a.Facility);
            foreach (var resource in AdvancementRules.Resources)
                Check(
                    Math.Abs(worker.Inventory.Get(resource) - (before.Get(resource) - a.Input.Get(resource) +
                                                               (resource == a.Output ? a.Yield : 0))) < 1e-8,
                    "Recipe did not conserve its specified physical inputs/outputs.");
            Check(worker.Mana == mana - a.Mana && !engine.TryWorkAtBuilding(worker),
                "Work repeated in the same tick or spent incorrect mana.");
        }
    }

    private static void MagicSwitch()
    {
        var (engine, town, worker) = World();
        Know(engine, town, ResearchKind.Crystalcraft);
        var building = Facility(engine, town, BuildingKind.Crystallizer);
        Facility(engine, town, BuildingKind.Academy);
        engine.ConfigureWorld(engine.State.Rules, false, false);
        var before = engine.ExportJson();
        try
        {
            engine.StartResearch(town.Id, ResearchKind.RunicEngineering);
            throw new Exception("New magic research started.");
        }
        catch (InvalidOperationException)
        {
        }

        Check(engine.ExportJson() == before, "Rejected research spent resources or changed state.");
        Check(engine.ResearchPrerequisiteError(town.Id, ResearchKind.Industry) != "世界规则已关闭新的魔法发展",
            "Magic switch blocked the technology route.");
        worker.MagicTalent = 100;
        worker.MagicTraining = 20;
        worker.Mana = 100;
        Hold(engine, worker, building.X, building.Y);
        worker.Agent.Goal.TargetEntityId = building.Id;
        worker.Inventory.Ore = 1;
        Check(engine.TryWorkAtBuilding(worker) && worker.Inventory.Crystals == 1,
            "Turning off new magic erased existing production capability.");
    }

    private static void Planning()
    {
        var (engine, town, worker) = World();
        engine.SpawnResidents(town.X, town.Y, RaceKind.Human, 6);
        foreach (var person in engine.State.Residents)
        {
            person.Age = 25;
            Hold(engine, person, town.X, town.Y);
        }

        foreach (var kind in new[]
                 {
                     ResearchKind.Agriculture, ResearchKind.Logistics, ResearchKind.Irrigation, ResearchKind.Forestry,
                     ResearchKind.Medicine, ResearchKind.ScientificMethod,
                 }) engine.GrantReceivedResearch(town.Id, kind);
        Facility(engine, town, BuildingKind.Academy);
        Facility(engine, town, BuildingKind.Waystation);
        engine.ConfigureWorld(engine.State.Rules with { Research = true, Construction = true }, false, false);
        engine.Step(60);
        Check(
            engine.State.Society.Research.Single(r => r.SettlementId == town.Id).ActiveProject == ResearchKind.Industry,
            "Autonomous development stopped after the four original research entries.");
        engine.GrantReceivedResearch(town.Id, ResearchKind.Industry);
        engine.Step(60);
        Check(engine.State.Society.Buildings.Any(b => b.Kind == BuildingKind.Foundry),
            "The planner did not build its unlocked production facility.");
    }

    private static void Knowledge()
    {
        var (engine, town, worker) = World();
        var fact = new AgentFact
        {
            Id = engine.State.NextId++, Kind = AgentFactKind.Research, SubjectId = town.Id,
            X = town.X, Y = town.Y, Value = (int)ResearchKind.AdvancedComputing, ObservedTick = 0, LearnedTick = 0,
            OriginResidentId = worker.Id, SourceResidentId = worker.Id,
        };
        var recipient = engine.State.Residents.First();
        engine.State.PendingMessages.Add(new PendingMessage
        {
            SenderId = worker.Id, RecipientId = recipient.Id,
            DeliverTick = 3, Facts = [fact],
        });
        engine.Step(2);
        Check(!engine.HasResearch(town.Id, ResearchKind.AdvancedComputing),
            "Remote research arrived before its message.");
        engine.Step();
        Check(engine.HasResearch(town.Id, ResearchKind.AdvancedComputing),
            "New research was truncated to the original four values.");
        Check(!engine.HasResearch(town.Id, ResearchKind.Automation),
            "Receiving advanced knowledge invented its prerequisites.");
        var building = Facility(engine, town, BuildingKind.Fabricator);
        Check(engine.GetProductionStatus(building.Id).Contains("前置"),
            "Received future knowledge bypassed operational prerequisites.");
    }

    private static void Persistence()
    {
        var (engine, town, worker) = World();
        engine.EditResident(worker.Id,
            new ResidentEdit { Inventory = new ResourceStock { Alloy = 1.25, EnergyCells = 2.5, Crystals = 3.75 } });
        var saved = engine.ExportJson();
        var restored = WorldEngine.ImportJson(saved);
        Check(restored.State.Residents.Single(r => r.Id == worker.Id).Inventory.Crystals == 3.75,
            "Editor or save discarded new resources.");

        void Reject(Action<JsonNode> edit)
        {
            var root = JsonNode.Parse(saved)!;
            edit(root);
            try
            {
                WorldEngine.ImportJson(root.ToJsonString());
            }
            catch (ArgumentException)
            {
                return;
            }

            throw new Exception("Corrupt advancement state accepted.");
        }

        Reject(r => r["FormatVersion"] = 5);
        Reject(r => r["Settlements"]![0]!["Resources"]!["Alloy"] = -1);
        Reject(r => r["Residents"]![0]!["Inventory"]!["EnergyCells"] = 1_000_001);
        Reject(r => r["Settlements"]![0]!["Resources"]!["Crystals"] = null);
        Reject(r => r["Society"]!["Buildings"]![0]!["ProductionBatches"] = -1);
        Check(engine.ExportJson() == saved, "Rejected imports changed the current world.");
    }

    private static void Check(bool value, string message)
    {
        if (!value) throw new InvalidOperationException(message);
    }
}
