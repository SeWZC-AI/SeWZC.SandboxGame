using System.Text.Json.Nodes;
using SeWZC.WorldBox.Core;

internal static class DevelopmentPlanningTests
{
    public static IEnumerable<(string Name, Action Run)> Cases =>
    [
        ("development directions preserve choice and reject invalid saved values", DirectionPersistence),
        ("natural magic autonomously trains practitioners without mandatory crystal devices", NaturalMagic),
        ("technology civilizations complete real industrial production within a century", TechnologyCentury),
    ];

    [UnitTest]
    private static void DirectionPersistence()
    {
        var engine = WorldEngine.Create(42, 32, 32, false);
        foreach (var tile in engine.State.Tiles) tile.Terrain = TerrainType.Grass;
        TestLand.ClearWildlife(engine);
        engine.SpawnResidents(8, 16, RaceKind.Human, 1);
        engine.SpawnResidents(24, 16, RaceKind.Orc, 1);
        var nation = engine.State.Nations[0];
        engine.SetDevelopmentFocus(nation.Id, DevelopmentFocus.Technology);
        var saved = engine.ExportJson();
        var resumed = WorldEngine.ImportJson(saved);
        Require(resumed.State.Nations[0].DevelopmentFocus == DevelopmentFocus.Technology,
            "Direction was lost on resume");
        var bad = JsonNode.Parse(saved)!;
        bad["Nations"]![0]!["DevelopmentFocus"] = 999;
        try
        {
            WorldEngine.ImportJson(bad.ToJsonString());
            throw new Exception("Invalid direction accepted");
        }
        catch (ArgumentException)
        {
        }

        // Older current-format worlds deterministically use culture, without random initialization.
        var prior = JsonNode.Parse(saved)!;
        foreach (var n in prior["Nations"]!.AsArray()) n!.AsObject().Remove("DevelopmentFocus");
        Require(
            WorldEngine.ImportJson(prior.ToJsonString()).State.Nations
                .All(n => n.DevelopmentFocus == DevelopmentFocus.Automatic), "Safe automatic default missing");
    }

    private static void NaturalMagic()
    {
        var engine = WorldEngine.Create(42, 64, 64);
        foreach (var nation in engine.State.Nations)
            engine.SetDevelopmentFocus(nation.Id, DevelopmentFocus.MagicPractice);
        engine.ConfigureWorld(engine.State.Rules with { Wars = false, Secession = false }, false, true);
        engine.Step(3600);
        Require(engine.State.Society.Research.Any(r => r.Completed.Contains(Advancement.ArcaneArts)),
            "Natural magic did not learn its foundation");
        Require(engine.State.Society.Buildings.Any(b => b.Kind == BuildingKind.ArcaneSanctum && b.IsCompleted),
            "Practitioners have no finished training place");
        Require(engine.State.Residents.Any(r => r.MagicTraining > 8), "No actual training took place");
        Require(!engine.State.Society.Buildings.Any(b => ProductionRules.For(b.Kind)?.Research.Magic == true),
            "Natural magic unnecessarily required crystal industry");
    }

    [LongRunningTest]
    private static void TechnologyCentury()
    {
        foreach (var seed in new[] { 73921, 42 })
        {
            var engine = WorldEngine.Create(seed);
            foreach (var nation in engine.State.Nations)
                engine.SetDevelopmentFocus(nation.Id, DevelopmentFocus.Technology);
            long firstIndustry = -1;
            for (var i = 0; i < 100; i++)
            {
                engine.Step(120);
                if (firstIndustry < 0 &&
                    engine.State.Society.Buildings.Any(b => b.Kind == BuildingKind.Foundry && b.ProductionBatches > 0))
                    firstIndustry = engine.State.Tick;
            }

            Require(firstIndustry is > 0 and <= 12000,
                $"Seed {seed} made no actual industrial progress within a century");
            Require(
                !engine.State.Society.Buildings.Any(b =>
                    b.Kind == BuildingKind.ArcaneSanctum || ProductionRules.For(b.Kind)?.Research.Magic == true),
                "Technology route built unnecessary magical devices");
            var resumed = WorldEngine.ImportJson(engine.ExportJson());
            engine.Step(12);
            resumed.Step(12);
            Require(engine.ExportJson() == resumed.ExportJson(),
                "Century world failed deterministic save continuation");
            Console.WriteLine($"seed={seed} actual industrial production by year {firstIndustry / 120d:0.0}");
        }
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
    }
}
