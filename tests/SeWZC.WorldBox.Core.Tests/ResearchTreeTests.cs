using System.Text.Json.Nodes;
using SeWZC.WorldBox.Core;

internal static class ResearchTreeTests
{
    public static IEnumerable<(string Name, Action Run)> Cases =>
    [
        ("research graph covers every project without cycles or cross-route dependencies", Graph),
        ("real research changes physical production and survives deterministic saving", Production),
        ("production preserves the next research budget instead of consuming its ore", ResearchBudget),
    ];

    [UnitTest]
    private static void Graph()
    {
        Require(ResearchRules.All.Select(r => r.Kind).Order().SequenceEqual(Enum.GetValues<ResearchKind>().Order()),
            "Research catalog is incomplete or duplicated");
        var seen = new HashSet<ResearchKind>();
        foreach (var r in ResearchRules.All)
        {
            Require(r.Prerequisites.All(seen.Contains),
                "Research graph is cyclic or not topologically ordered: " + r.Kind);
            Require(r.Work > 0 && !string.IsNullOrWhiteSpace(r.Effect), "Research has no work or actual effect");
            seen.Add(r.Kind);
        }

        foreach (var magic in new[] { false, true })
        {
            var route = ResearchRules.Route(magic);
            Require(!route.Any(k => ResearchRules.For(k).Name.Contains("帝国")),
                "Civilization outcome is still a research project");
            Require(route.All(k => ResearchRules.For(k).Prerequisites.All(route.Contains)),
                "Route depends on the other route");
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
        foreach (var b in engine.State.Society.Buildings) b.ConstructionProgress = b.ConstructionRequired;
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
        foreach (var k in new[] { ResearchKind.Agriculture, ResearchKind.Logistics, ResearchKind.Industry })
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
        engine.GrantReceivedResearch(town.Id, ResearchKind.EfficientSmelting);
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
                     ResearchKind.Irrigation, ResearchKind.Forestry, ResearchKind.Medicine,
                     ResearchKind.ScientificMethod, ResearchKind.EfficientSmelting,
                 }) engine.GrantReceivedResearch(town.Id, k);
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
        engine.StartResearch(town.Id, ResearchKind.Electrification);
        Require(town.Resources.Ore == 0, "Reserved ore did not reach its actual research project");
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
    }
}
