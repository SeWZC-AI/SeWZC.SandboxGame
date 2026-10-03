using SeWZC.WorldBox.Core;

internal static class PerformanceBehaviorTests
{
    public static IEnumerable<(string Name, Action Run)> Cases =>
    [
        ("ecology updates daily bands and resumes from every phase", DailyEcology),
        ("ecology reads changed food capacity before the next band", CapacityEdits),
        ("territory totals track edits and rebuild after loading", TerritoryEdits),
        ("buffered saves preserve complete JSON and cancel before returning a partial capture", BufferedSave)
    ];

    private static WorldEngine Flat()
    {
        var engine = WorldEngine.Create(73921, 32, 32, false);
        engine.State.NaturalDisasters = false;
        foreach (var tile in engine.State.Tiles)
        {
            tile.Terrain = TerrainType.Grass; tile.Fertility = 100; tile.ResourceAmount = 100;
            tile.Wildlife = WildlifeKind.Rabbit; tile.WildlifePopulation = 1; tile.OtherWildlife = default;
        }
        return engine;
    }

    private static void Check(bool valid, string message)
    { if (!valid) throw new InvalidOperationException(message); }

    [UnitTest]
    private static void DailyEcology()
    {
        var engine = Flat(); engine.Step();
        Check(engine.State.Tiles[2 * 32 + 16].WildlifePopulation > 1, "First band did not grow on the first day");
        Check(engine.State.Tiles[28 * 32 + 16].WildlifePopulation == 1, "Distant band grew before its turn");
        for (var phase = 1; phase <= 6; phase++)
        {
            var resumed = WorldEngine.ImportJson(engine.ExportJson());
            engine.Step(); resumed.Step();
            Check(engine.ExportJson() == resumed.ExportJson(), $"Ecology resume diverged at phase {phase}");
        }
        foreach (var row in new[] { 2, 8, 13, 18, 24, 29 })
            Check(engine.State.Tiles[row * 32 + 16].WildlifePopulation > 1, "Some rows skipped their six-day growth cycle");
    }

    [UnitTest]
    private static void CapacityEdits()
    {
        var engine = Flat(); var tile = engine.State.Tiles[16 * 32 + 16];
        tile.Terrain = TerrainType.Forest; tile.Wildlife = WildlifeKind.Deer; tile.WildlifePopulation = 2;
        engine.Step(3); // This row was read as the preceding band's migration apron.
        tile.ResourceAmount = 0;
        engine.Step();
        Check(tile.WildlifePopulation > 0 && tile.WildlifePopulation <= 1.76, "Depleted food did not invalidate the cached habitat");
    }

    [UnitTest]
    private static void TerritoryEdits()
    {
        var engine = Flat(); engine.SpawnResidents(16, 16, RaceKind.Human, 6); engine.Step();
        var nation = engine.State.Nations.Single(); var tile = engine.State.Tiles[0];
        tile.NationId = nation.Id; engine.Step();
        Check(nation.Territory == engine.State.Tiles.Count(t => t.NationId == nation.Id), "Direct ownership edit left a stale territory count");
        tile.NationId = 0; engine.TransferTerritory(3, 3, nation.Id, 1); engine.Step();
        Check(nation.Territory == engine.State.Tiles.Count(t => t.NationId == nation.Id), "Territory brush left a stale territory count");
        var resumed = WorldEngine.ImportJson(engine.ExportJson()); resumed.Step(); engine.Step();
        Check(resumed.ExportJson() == engine.ExportJson(), "Ownership index did not rebuild from saved land");
    }

    [UnitTest]
    private static void BufferedSave()
    {
        var engine = Flat(); var before = engine.ExportJson(); var writes = 0;
        var json = engine.ExportJsonAsync(_ => { writes++; return ValueTask.CompletedTask; }).GetAwaiter().GetResult();
        Check(writes > 1 && json == before, "Buffered save changed the complete JSON or never offered a yield");
        using var cancellation = new CancellationTokenSource();
        try
        {
            engine.ExportJsonAsync(_ => { cancellation.Cancel(); return ValueTask.CompletedTask; }, cancellation.Token).GetAwaiter().GetResult();
            throw new Exception("Canceled capture returned a partial save");
        }
        catch (OperationCanceledException) { }
        Check(engine.ExportJson() == before && WorldEngine.ImportJson(json).ExportJson() == before, "Save capture modified the world or produced invalid JSON");
    }
}
