using System.Text.Json.Nodes;
using SeWZC.WorldBox.Core;

internal static class ResearchTreeTests
{
    public static IEnumerable<(string Name, Action Run)> Cases =>
    [
        ("research graph covers every project without cycles or cross-route dependencies", Graph),
        ("research saves IDs and restores shared nodes while rejecting invalid projects", Persistence),
        ("real research changes physical production and survives deterministic saving", Production),
        ("production preserves the next research budget instead of consuming its ore", ResearchBudget),
    ];

    [UnitTest]
    private static void Graph()
    {
        Require(Advancement.All.Select(r => r.Id).Distinct().Count() == Advancement.All.Count
                && Advancement.All.All(r => ReferenceEquals(Advancement.Find(r.Id), r)),
            "Research catalog is incomplete or duplicated");
        var seen = new HashSet<Advancement>();
        foreach (var r in ResearchRules.All)
        {
            Require(r.Prerequisites.All(seen.Contains),
                "Research graph is cyclic or not topologically ordered: " + r);
            Require(r.Work > 0 && !string.IsNullOrWhiteSpace(r.Effect), "Research has no work or actual effect");
            seen.Add(r);
        }

        foreach (var magic in new[] { false, true })
        {
            var route = ResearchRules.Route(magic);
            Require(!route.Any(k => k.Name.Contains("帝国")),
                "Civilization outcome is still a research project");
            Require(route.All(k => k.Prerequisites.All(route.Contains)),
                "Route depends on the other route");
        }
    }

    [UnitTest]
    private static void Persistence()
    {
        var (engine, town, _, _) = World();
        var research = engine.State.Society.Research.Single(r => r.SettlementId == town.Id);
        research.ActiveProject = Advancement.Electrification;
        research.Progress = 5;
        research.RequiredProgress = Advancement.Electrification.Work;
        var save = engine.ExportJson();
        var restored = WorldEngine.ImportJson(save);
        var loaded = restored.State.Society.Research.Single(r => r.SettlementId == town.Id);
        Require(ReferenceEquals(loaded.ActiveProject, Advancement.Electrification)
                && loaded.Completed.All(r => ReferenceEquals(r, Advancement.Find(r.Id))),
            "Loading research created disconnected nodes");
        Require(loaded.ActiveProject!.Prerequisites.Single() == Advancement.Industry
                && restored.HasResearch(town.Id, loaded.ActiveProject!.Prerequisites.Single()),
            "Loaded knowledge no longer satisfies a shared prerequisite");
        var savedProject = JsonNode.Parse(save)!["Society"]!["Research"]![0]!;
        Require(savedProject["ActiveProject"]!.GetValue<int>() == Advancement.Electrification.Id
                && savedProject["Completed"]![0]!.GetValue<int>() == Advancement.Agriculture.Id,
            "Research metadata was expanded into the save");
        Require(restored.ExportJson() == save, "Research changed its numeric save representation");

        foreach (var invalid in new[] { "-1", "2147483647", "1.5", "\"Agriculture\"", "{}", "null" })
        foreach (var active in new[] { false, true })
        {
            if (active && invalid == "null")
                continue;
            var json = JsonNode.Parse(save)!;
            var savedResearch = json["Society"]!["Research"]![0]!;
            if (active)
                savedResearch["ActiveProject"] = JsonNode.Parse(invalid);
            else
                savedResearch["Completed"]![0] = JsonNode.Parse(invalid);
            try
            {
                WorldEngine.ImportJson(json.ToJsonString());
                throw new Exception("Invalid research accepted: " + invalid);
            }
            catch (ArgumentException)
            {
            }
        }
    }

    private static (WorldEngine Engine, Settlement Town, Resident Worker, Building Foundry) World()
    {
        var engine = WorldEngine.Create(42, 32, 32, false);
        foreach (var t in engine.State.Tiles)
        {
            t.Terrain = TerrainType.Grass;
            t.Fertility = 100;
        }

        engine.SpawnResidents(16, 16, RaceKind.Human, 4);
        var town = engine.State.Settlements.Single();
        TestLand.ClaimAllTowns(engine);
        foreach (var b in engine.State.Society.Buildings)
            b.ConstructionProgress = b.ConstructionRequired;
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
            Trade = false,
            Wars = false,
            Alliances = false,
            Migration = false,
            Secession = false,
        }, false, true);
        town.Resources = new ResourceStock
        {
            Food = 100,
            Wood = 100,
            Stone = 100,
            Ore = 100,
            Alloy = 100,
            Coal = 100,
        };
        foreach (var k in new[] { Advancement.Agriculture, Advancement.Logistics, Advancement.Industry })
            engine.GrantReceivedResearch(town.Id, k);
        var id = engine.GrantFacility(town.Id, BuildingKind.Foundry, 17, 16);
        var foundry = engine.State.Society.Buildings.Single(b => b.Id == id);
        var worker = engine.State.Residents.First();
        worker.Age = 25;
        worker.Profession = Profession.Builder;
        worker.X = worker.FromX = foundry.X;
        worker.Y = worker.FromY = foundry.Y;
        worker.Agent.Goal = new AgentGoal
        {
            Kind = AgentGoalKind.Work, TargetEntityId = id, TargetX = foundry.X, TargetY = foundry.Y,
        };
        return (engine, town, worker, foundry);
    }

    [UnitTest]
    private static void Production()
    {
        var (engine, town, worker, foundry) = World();
        engine.GrantReceivedResearch(town.Id, Advancement.EfficientSmelting);
        worker.Inventory = new ResourceStock { Coal = 1, Ore = 2 };
        Require(engine.TryWorkAtBuilding(worker), "Qualified worker did not produce");
        Require(worker.Inventory.Coal == 0 && worker.Inventory.Ore == 0 && worker.Inventory.Alloy == 1.25,
            "Smelting research changed physical inputs or failed to improve yield");
        Require(foundry.ProductionBatches == 1 && town.Resources.Alloy == 100,
            "Production bypassed personal transport");
        worker.Agent.MaterialPriority = ResourceKind.Ore;
        var save = engine.ExportJson();
        var resumed = WorldEngine.ImportJson(save);
        Require(resumed.State.Residents.First().Agent.MaterialPriority == ResourceKind.Ore, "Mining priority was lost");
        var bad = JsonNode.Parse(save)!;
        bad["Residents"]![0]!["Agent"]!["MaterialPriority"] = (int)ResourceKind.Aircraft;
        try
        {
            WorldEngine.ImportJson(bad.ToJsonString());
            throw new Exception("Invalid mining priority accepted");
        }
        catch (ArgumentException)
        {
        }

        engine.Step(12);
        resumed.Step(12);
        Require(engine.ExportJson() == resumed.ExportJson(), "Research failed deterministic continuation");
    }

    [UnitTest]
    private static void ResearchBudget()
    {
        var (engine, town, worker, foundry) = World();
        engine.GrantFacility(town.Id, BuildingKind.Academy, 16, 17);
        foreach (var k in new[]
                 {
                     Advancement.Irrigation, Advancement.Forestry, Advancement.Medicine,
                     Advancement.ScientificMethod, Advancement.EfficientSmelting,
                 })
            engine.GrantReceivedResearch(town.Id, k);
        engine.SetDevelopmentFocus(town.NationId, DevelopmentFocus.Technology);
        engine.State.Rules.Research = true;
        town.Resources.Alloy = 20;
        town.Resources.Ore = 15;
        worker.Inventory = new ResourceStock();
        var hasWork = engine.TryGetLocalWorkTarget(worker, out var x, out var y);
        Require(!hasWork || x != foundry.X || y != foundry.Y,
            "Foundry ignored reserved ore: " + engine.GetProductionStatus(foundry.Id));
        Require(town.Resources.Ore == 15 && foundry.ProductionBatches == 0,
            "Checking a reserved budget changed resources");
        engine.StartResearch(town.Id, Advancement.Electrification);
        Require(town.Resources.Ore == 0, "Reserved ore did not reach its actual research project");
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new Exception(message);
    }
}
