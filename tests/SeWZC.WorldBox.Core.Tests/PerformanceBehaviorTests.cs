using SeWZC.WorldBox.Core;

internal static class PerformanceBehaviorTests
{
    public static IEnumerable<(string Name, Action Run)> Cases =>
    [
        ("ecology updates daily bands and resumes from every phase", DailyEcology),
        ("ecology reads changed food capacity before the next band", CapacityEdits),
        ("large-map ecology uses sparse cycles and resumes across band boundaries", SparseEcology),
        ("territory totals track edits and rebuild after loading", TerritoryEdits),
        ("buffered saves preserve complete JSON and cancel before returning a partial capture", BufferedSave)
    ];

    private static WorldEngine Flat()
    {
        var engine = WorldEngine.Create(73921, 32, 32, false);
        engine.State.NaturalDisasters = false;
        foreach (var tile in engine.State.Tiles)
        {
            tile.Terrain = TerrainType.Grass; tile.Fertility = 100; tile.ResourceAmount = 100; tile.NaturalWaterYield = .02;
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

    private static void SparseEcology()
    {
        var engine = WorldEngine.Create(73921, 128, 128, false);
        engine.State.NaturalDisasters = false;
        engine.State.Rules.ResourceRegeneration = false;
        foreach (var tile in engine.State.Tiles)
        {
            tile.Terrain = TerrainType.Tundra; tile.Fertility = 100; tile.ResourceAmount = 100;
            tile.NaturalWaterYield = .02; tile.Wildlife = WildlifeKind.None;
            tile.WildlifePopulation = 0; tile.OtherWildlife = default;
        }
        var early = engine.State.Tiles[64];
        var late = engine.State.Tiles[127 * 128 + 64];
        early.Wildlife = late.Wildlife = WildlifeKind.Rabbit;
        early.WildlifePopulation = late.WildlifePopulation = 1;
        late.OtherWildlife = new() { SnowLeopard = .1 };
        Check(engine.WildlifeCycleDays == 64, "Large-map cycle did not scale with its daily work budget");
        engine.Step();
        Check(early.WildlifePopulation != 1 && late.WildlifePopulation == 1,
            "Distant animals were reevaluated before their scheduled turn");
        engine.Step(61);
        Check(late.WildlifePopulation == 1 && late.OtherWildlife.SnowLeopard == .1,
            "Last band changed before its turn");
        var resumed = WorldEngine.ImportJson(engine.ExportJson());
        engine.Step(3); resumed.Step(3);
        Check(engine.ExportJson() == resumed.ExportJson(), "Sparse ecology diverged across its last and first bands");
        Check(late.WildlifePopulation != 1 && late.OtherWildlife.SnowLeopard != .1,
            "Sparse updates missed herbivores or species in mask bit 31");
        Check(engine.State.Tiles.All(t => t.WildlifePopulation >= 0 && t.OtherWildlife.SnowLeopard >= 0),
            "Coarse predation and migration produced negative stock");
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
