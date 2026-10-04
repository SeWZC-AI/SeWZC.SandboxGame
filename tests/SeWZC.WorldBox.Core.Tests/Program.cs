using System.Diagnostics;
using System.Text;
using System.Text.Json.Nodes;
using SeWZC.WorldBox.Core;

var evolutionOption = Array.IndexOf(args, "--evolution");
if (evolutionOption >= 0)
{
    EvolutionProbe.Run(evolutionOption + 1 < args.Length ? args[evolutionOption + 1] : "artifacts/evolution-probe.json");
    return 0;
}

// A dependency-free executable suite, runnable with dotnet run --project tests/SeWZC.WorldBox.Core.Tests.
if (args.Contains("--profile-simulation"))
    return SimulationPerformance.Run(args, CreateBenchmarkWorld);
var developmentOption = Array.IndexOf(args, "--simulate-development");
if (developmentOption >= 0) return DevelopmentDiagnostics.Run(args[(developmentOption + 1)..]);

var visualOption = Array.IndexOf(args, "--export-visual-fixture");
if (visualOption >= 0)
{
    if (visualOption + 1 >= args.Length) return 2;
    VisualFixture.Export(args[visualOption + 1]); return 0;
}

var fixtureOption = Array.IndexOf(args, "--export-browser-fixture");
if (fixtureOption >= 0)
{
    if (fixtureOption + 1 >= args.Length)
    {
        Console.Error.WriteLine("Usage: --export-browser-fixture <path>");
        return 2;
    }
    var fixture = CreateBenchmarkWorld();
    var json = fixture.ExportJson();
    var bytes = Encoding.UTF8.GetByteCount(json);
    Check(bytes <= WorldEngine.MaxSaveBytes, "Browser fixture exceeds the save size limit.");
    _ = WorldEngine.ImportJson(json);
    var path = Path.GetFullPath(args[fixtureOption + 1]);
    Directory.CreateDirectory(Path.GetDirectoryName(path)!);
    File.WriteAllText(path, json, new UTF8Encoding(false));
    Console.WriteLine($"EXPORTED {path}: {bytes} bytes; tick {fixture.State.Tick}, 256×256 terrain, " +
        $"{fixture.State.Population} residents, {fixture.State.Nations.Count} nations, " +
        $"{fixture.State.Diplomacies.Count(d => d.Status == DiplomaticStatus.War)} wars.");
    return 0;
}

var tests = new (string Name, Action Run)[]
{
    ("same seed reproduces terrain and starting world", Generation),
    ("simulation advances residents and world time", Simulation),
    ("save and resume preserves subsequent simulation", SaveResume),
    ("invalid save files are rejected before use", InvalidSaves),
    ("terrain edits keep residents on accessible land", TerrainEditing),
    ("mixed edits preserve valid deterministic save states", MixedEditSaveRoundTrips),
    ("water barrier blocks armies and occupation", ImpassableBarrier),
    ("disasters change world state and affect residents", Disasters),
    ("nation editing updates authoritative resources", NationEditing),
    ("territory transfer and nation splitting preserve ownership", NationTerritoryEditing),
    ("spawning on owned land joins its existing nation", SpawnOnOwnedLand),
    ("food availability changes population survival", FoodAvailability),
    ("war leads to casualties or territorial capture", War)
}.Concat(AgentBehaviorTests.Cases).Concat(EditorAndMigrationTests.Cases).Concat(SocietyBehaviorTests.Cases()).Concat(EvolutionTests.Cases).Concat(WorkQueryTests.Cases)
    .Concat(PersistenceRegressionTests.Cases).Concat(SocietyRegressionTests.Cases).Concat(AgentRegressionTests.Cases)
    .Concat(DiplomacyKnowledgeTests.Cases).Concat(StoryTests.Cases).Concat(PresentationWorldTests.Cases)
    .Concat(TradeRegressionTests.Cases).Concat(AdvancementTests.Cases).Concat(ResearchTreeTests.Cases).Concat(ResearchGameplayTests.Cases).Concat(LandTransportTests.Cases).Concat(EcologyAndConflictTests.Cases).Concat(DevelopmentPlanningTests.Cases).Concat(PerformanceBehaviorTests.Cases).Concat(ProvisioningAndClaimsTests.Cases).Concat(SurvivalAndDisasterTests.Cases).Concat(TownInfrastructureTests.Cases).Concat(GeographyEcologyTests.Cases).Concat(TownActivityTests.Cases).Concat(SimulationOptimizationTests.Cases).ToArray();
var filterOption = Array.IndexOf(args, "--filter");
var suiteOption = Array.IndexOf(args, "--suite");
var suite = suiteOption < 0 ? "unit" : args.ElementAtOrDefault(suiteOption + 1);
if (suite is not ("unit" or "integration" or "long" or "all") ||
    (filterOption >= 0 && (filterOption + 1 >= args.Length || args[filterOption + 1].StartsWith("--"))))
{
    Console.Error.WriteLine("Usage: [--suite unit|integration|long|all] [--filter <name>] [--list]");
    return 2;
}
tests = tests.Where(t => suite == "all" || suite == Scope(t.Run)).ToArray();
if (filterOption >= 0)
    tests = tests.Where(t => t.Name.Contains(args[filterOption + 1], StringComparison.OrdinalIgnoreCase)).ToArray();
if (tests.Length == 0)
{
    Console.Error.WriteLine("No tests matched the requested suite/filter.");
    return 2;
}
if (args.Contains("--list"))
{
    foreach (var test in tests) Console.WriteLine($"{Scope(test.Run)}: {test.Name}");
    return 0;
}
var failures = 0;
var totalTime = Stopwatch.StartNew();
foreach (var (name, run) in tests)
{
    var elapsed = Stopwatch.StartNew();
    try
    {
        run();
        Console.WriteLine($"PASS {name} ({elapsed.ElapsedMilliseconds} ms)");
    }
    catch (Exception ex)
    {
        failures++;
        Console.Error.WriteLine($"FAIL {name}: {ex.GetType().Name}: {ex.Message}");
        Console.Error.WriteLine(ex.StackTrace);
    }
}
Console.WriteLine($"{tests.Length - failures}/{tests.Length} {suite} checks passed in {totalTime.Elapsed.TotalSeconds:F2} s");
if (failures == 0 && args.Contains("--benchmark")) Benchmark();
return failures == 0 ? 0 : 1;

static string Scope(Action test) => test.Method.IsDefined(typeof(LongRunningTestAttribute), false) ? "long"
    : test.Method.IsDefined(typeof(UnitTestAttribute), false) ? "unit" : "integration";

static void Generation()
{
    var a = WorldEngine.Create(42, 64, 64);
    var b = WorldEngine.Create(42, 64, 64);
    var c = WorldEngine.Create(43, 64, 64);
    Check(a.ExportJson() == b.ExportJson(), "Identical seeds produced different worlds.");
    Check(a.State.Tiles.Length == 64 * 64, "Terrain does not match dimensions.");
    Check(a.State.Tiles.Any(t => t.IsWalkable) && a.State.Tiles.Any(t => !t.IsWalkable),
        "Generated world requires both land and geographic obstacles.");
    Check(!a.State.Tiles.Select(t => t.Terrain).SequenceEqual(c.State.Tiles.Select(t => t.Terrain)),
        "Different seeds must change the generated terrain.");
    Check(a.State.Population > 0 && a.State.Nations.Count > 0, "Demo world starts empty.");
    CheckResidents(a.State);
}

static void Simulation()
{
    var engine = WorldEngine.Create(17, 64, 64);
    engine.State.NaturalDisasters = false;
    var ages = engine.State.Residents.ToDictionary(r => r.Id, r => r.Age);
    engine.Step(48);
    Check(engine.State.Tick == 48, "Requested steps did not advance world time.");
    Check(engine.State.Residents.Any(r => ages.TryGetValue(r.Id, out var age) && r.Age > age),
        "Residents do not age as simulation advances.");
    Check(engine.State.Settlements.All(s => s.Resources.Food >= 0), "Food stocks became negative.");
    CheckResidents(engine.State);
}

static void SaveResume()
{
    var uninterrupted = WorldEngine.Create(9876, 64, 64);
    uninterrupted.Step(37);
    AssertResume(uninterrupted, 91);

    var conflict = WarWorld();
    var nations = conflict.State.Nations.ToArray();
    conflict.SetDiplomacy(nations[0].Id, nations[1].Id, DiplomaticStatus.War);
    conflict.TriggerDisaster(15, 32, DisasterKind.Drought, 8);
    conflict.Step(50);
    Check(conflict.State.Armies.Any(a => a.MoveStartedTick > 0 && (a.FromX != a.X || a.FromY != a.Y)),
        "Save scenario requires a moving army.");
    for (var y = 29; y <= 35; y++)
    for (var x = 32; x <= 38; x++)
        conflict.State.Tiles[y * conflict.State.Width + x].Terrain = TerrainType.Forest;
    conflict.TriggerDisaster(35, 32, DisasterKind.Fire, 3);
    AssertResume(conflict, 91);

    var (trading, trader, _, _) = AgentBehaviorTests.TradeWorld();
    trading.Step(20);
    Check(trader.Agent.Goal.Kind == AgentGoalKind.Trade && trader.Inventory.Food > 0,
        "Trade scenario requires a physical carrier still delivering food.");
    AssertResume(trading, 100);
}

static void AssertResume(WorldEngine uninterrupted, int steps)
{
    var saved = uninterrupted.ExportJson();
    var resumed = WorldEngine.ImportJson(saved);
    var imported = resumed.ExportJson();
    if (saved != imported) throw new InvalidOperationException("A valid save changed during import: " + JsonDifference(JsonNode.Parse(saved), JsonNode.Parse(imported)));
    uninterrupted.Step(steps);
    resumed.Step(steps);
    Check(uninterrupted.ExportJson() == resumed.ExportJson(),
        "Save/resume changed subsequent decisions or random outcomes.");
}

static string? JsonDifference(JsonNode? first, JsonNode? second, string path = "$")
{
    if (JsonNode.DeepEquals(first, second)) return null;
    if (first is JsonObject a && second is JsonObject b)
    {
        foreach (var item in a)
        {
            var difference = JsonDifference(item.Value, b[item.Key], path + "." + item.Key);
            if (difference is not null) return difference;
        }
    }
    if (first is JsonArray aa && second is JsonArray bb)
    {
        if (aa.Count != bb.Count) return $"{path}: count {aa.Count} -> {bb.Count}";
        for (var i = 0; i < aa.Count; i++)
        {
            var difference = JsonDifference(aa[i], bb[i], $"{path}[{i}]");
            if (difference is not null) return difference;
        }
    }
    return $"{path}: {first?.ToJsonString()} -> {second?.ToJsonString()}";
}

static void InvalidSaves()
{
    var engine = FlatWorld();
    engine.SpawnResidents(20, 20, RaceKind.Human, 12);
    var valid = engine.ExportJson();
    Reject("{unfinished", "malformed JSON");
    Reject("null", "null save");
    Reject("{}", "missing dimensions and terrain");
    Reject("{}" + new string(' ', WorldEngine.MaxSaveBytes), "oversized input");
    Reject(Modify(valid, root => root["FormatVersion"] = 999), "unsupported save version");
    Reject(Modify(valid, root => root["RandomState"] = 0), "zero random state");
    Reject(Modify(valid, root => root["Width"] = 257), "oversized dimension");
    Reject(Modify(valid, root => root["Residents"] = null), "null resident collection");
    Reject(Modify(valid, root => root["Tiles"]![0] = null), "null tile");
    Reject(Modify(valid, root => root["Nations"]![0] = null), "null nation");
    Reject(Modify(valid, root => root["Residents"]![0] = null), "null resident");
    Reject(Modify(valid, root => root["Settlements"]![0]!["Resources"] = null), "null resource stock");
    Reject(Modify(valid, root => root["Settlements"]![0]!["Resources"]!["Food"] = -1), "negative resource stock");
    Reject(Modify(valid, root => root["Tiles"]!.AsArray().RemoveAt(0)), "mismatched terrain count");
    Reject(Modify(valid, root => root["Tiles"]![0]!["Terrain"] = 999), "unknown terrain type");
    Reject(Modify(valid, root => root["Tiles"]![0]!["NationId"] = int.MaxValue), "missing tile nation");
    Reject(Modify(valid, root => root["Residents"]![0]!["Race"] = 999), "unknown resident race");
    Reject(Modify(valid, root => root["Residents"]![0]!["Health"] = 101), "out-of-range health");
    Reject(Modify(valid, root => root["Residents"]![0]!["X"] = -1), "out-of-bounds resident");
    Reject(Modify(valid, root => root["Residents"]![0]!["NationId"] = int.MaxValue), "missing resident nation");
    Reject(Modify(valid, root => root["Residents"]![0]!["SettlementId"] = int.MaxValue), "missing resident settlement");
    Reject(Modify(valid, root => root["Residents"]![0]!["ArmyId"] = int.MaxValue), "missing resident army");
    Reject(Modify(valid, root => root["Nations"]![0]!["CapitalId"] = int.MaxValue), "missing capital");
    var home = engine.State.Settlements.Single();
    Reject(Modify(valid, root => root["Tiles"]![home.Y * engine.State.Width + home.X]!["NationId"] = 0),
        "settlement center owned by a different nation");
    Reject(Modify(valid, root => root["Residents"]![1]!["Id"] = root["Residents"]![0]!["Id"]!.GetValue<int>()),
        "duplicate entity identifier");
    var diplomacy = WarWorld().ExportJson();
    Reject(Modify(diplomacy, root => root["Diplomacies"]![0]!["Status"] = 999), "unknown diplomacy status");
    Reject(Modify(diplomacy, root => root["Diplomacies"]!.AsArray().Add(root["Diplomacies"]![0]!.DeepClone())),
        "duplicate diplomatic relation");
    Check(engine.ExportJson() == valid, "Invalid import modified an existing world.");
}

static void TerrainEditing()
{
    var engine = FlatWorld();
    engine.SpawnResidents(20, 20, RaceKind.Elf, 16);
    var resident = engine.State.Residents[0];
    var x = resident.X;
    var y = resident.Y;
    engine.PaintTerrain(x, y, TerrainType.Water, 3);
    Check(engine.State.Tiles[y * 64 + x].Terrain == TerrainType.Water, "Terrain brush did not paint its center.");
    engine.Step(5);
    CheckResidents(engine.State);
    Check(engine.State.Residents.Count > 0, "A small terrain edit destroyed the entire settlement.");
    engine.PaintTerrain(x, y, TerrainType.Grass, 1);
    Check(engine.State.Tiles[y * 64 + x].IsWalkable, "Restored land is not traversable.");

    // An intact resident on the opposite shore must follow their displaced home.
    // Northern land belongs to a competitor, so it is unavailable for relocation.
    var relocation = FlatWorld();
    relocation.SpawnResidents(24, 24, RaceKind.Human, 16);
    relocation.SpawnResidents(48, 48, RaceKind.Orc, 12);
    var town = relocation.State.Settlements[0];
    var foreignNation = relocation.State.Nations[1].Id;
    var foreignLand = new HashSet<int>();
    for (var yy = 12; yy < 24; yy++)
    for (var xx = 12; xx <= 36; xx++)
    {
        var index = yy * relocation.State.Width + xx;
        relocation.State.Tiles[index].NationId = foreignNation;
        foreignLand.Add(index);
    }
    var displaced = relocation.State.Residents.First(r => r.SettlementId == town.Id);
    displaced.X = 30;
    displaced.Y = 24;
    relocation.PaintTerrain(24, 24, TerrainType.Water, 5);
    var townIndex = town.Y * relocation.State.Width + town.X;
    Check(town.X != 24 || town.Y != 24, "A flooded settlement did not relocate.");
    Check(!foreignLand.Contains(townIndex), "A displaced settlement moved onto another nation's territory.");
    Check(relocation.State.Tiles[townIndex].NationId == town.NationId,
        "A relocated settlement does not own its new center tile.");
    Check(displaced.X == 30 && displaced.Y == 24,
        "Relocating a flooded town teleported an unaffected resident from the opposite shore.");
    var previous = (displaced.X, displaced.Y);
    var moved = false;
    for (var step = 0; step < 12; step++)
    {
        relocation.Tick();
        moved |= previous != (displaced.X, displaced.Y);
    }
    Check(moved, "A displaced resident could not resume moving after settlement relocation.");
    CheckResidents(relocation.State);
    AssertResume(relocation, 12);
}

[LongRunningTest]
static void MixedEditSaveRoundTrips()
{
    foreach (var seed in new[] { 2101, 9917 })
    {
        var engine = WorldEngine.Create(seed, 64, 64);
        engine.State.NaturalDisasters = false;
        foreach (var nation in engine.State.Nations)
            engine.SetNationResources(nation.Id, 5000, 500, 500, 500);
        if (engine.State.Nations.Count > 1)
            engine.SetDiplomacy(engine.State.Nations[0].Id, engine.State.Nations[1].Id, DiplomaticStatus.War);
        engine.Step(50);
        var random = new Random(seed);
        for (var operation = 0; operation < 20; operation++)
        {
            var x = random.Next(engine.State.Width);
            var y = random.Next(engine.State.Height);
            if (operation % 2 == 0 && engine.State.Settlements.Count > 0)
            {
                var town = engine.State.Settlements[random.Next(engine.State.Settlements.Count)];
                x = Math.Clamp(town.X + random.Next(-5, 6), 0, engine.State.Width - 1);
                y = Math.Clamp(town.Y + random.Next(-5, 6), 0, engine.State.Height - 1);
            }
            engine.PaintTerrain(x, y, (TerrainType)random.Next(7), random.Next(1, 7));
            if (operation % 3 == 0)
                engine.TriggerDisaster(x, y, (DisasterKind)random.Next(3), random.Next(2, 7));
            if (operation % 5 == 0)
                engine.SpawnResidents(random.Next(64), random.Next(64), (RaceKind)random.Next(4), 8);
            try
            {
                CheckResidents(engine.State);
                AssertResume(engine, random.Next(3, 16));
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Mixed-edit seed {seed}, operation {operation}: {ex.Message}", ex);
            }
        }
    }
}

static void ImpassableBarrier()
{
    var engine = WarWorld();
    for (var y = 0; y < engine.State.Height; y++)
        engine.PaintTerrain(32, y, TerrainType.DeepWater, 0);
    var capitals = engine.State.Settlements.ToDictionary(s => s.Id, s => s.NationId);
    var nations = engine.State.Nations.OrderBy(n => n.Id).ToArray();
    engine.SetDiplomacy(nations[0].Id, nations[1].Id, DiplomaticStatus.War);
    for (var step = 0; step < 160; step++)
    {
        engine.Tick();
        Check(engine.State.Settlements.All(s => !capitals.TryGetValue(s.Id, out var owner) || owner == s.NationId),
            "An army occupied a settlement across an impassable water barrier.");
        foreach (var army in engine.State.Armies)
            Check(army.NationId == nations[0].Id ? army.X < 32 : army.X > 32,
                "An army crossed an impassable water barrier.");
    }
    CheckResidents(engine.State);
}

static void Disasters()
{
    var engine = FlatWorld();
    engine.SpawnResidents(24, 24, RaceKind.Human, 16);
    foreach (var tile in engine.State.Tiles) tile.Terrain = TerrainType.Forest;
    var target = engine.State.Residents[0];
    engine.TriggerDisaster(target.X, target.Y, DisasterKind.Fire, 8);
    Check(engine.State.Tiles.Any(t => t.FireTicks > 0), "Fire leaves no burning terrain.");
    engine.Step(3);
    Check(engine.State.Residents.Any(r => r.Health < 100) || engine.State.Residents.Count < 16,
        "Fire has no effect on residents in its area.");

    var drought = FlatWorld();
    drought.SpawnResidents(24, 24, RaceKind.Dwarf, 12);
    var mildWeather = WorldEngine.ImportJson(drought.ExportJson());
    drought.TriggerDisaster(24, 24, DisasterKind.Drought, 16);
    Check(drought.State.Tiles.Any(t => t.DroughtTicks > 0), "Drought has no lasting local state.");
    drought.Step(24);
    mildWeather.Step(24);
    Check(drought.State.Settlements.Sum(s => s.Resources.Food) < mildWeather.State.Settlements.Sum(s => s.Resources.Food),
        "Drought did not lower food production compared with mild weather.");

    var plague = FlatWorld();
    plague.SpawnResidents(24, 24, RaceKind.Orc, 16);
    plague.TriggerDisaster(24, 24, DisasterKind.Plague, 8);
    Check(plague.State.Residents.Any(r => r.SicknessTicks > 0), "Plague infects no residents in its area.");
    plague.Step(5);
    Check(plague.State.Residents.Any(r => r.Health < 100) || plague.State.Population < 16,
        "Plague does not affect infected residents' health.");
}

[UnitTest]
static void NationEditing()
{
    var engine = FlatWorld(32);
    TestLand.ClearWildlife(engine);
    engine.SpawnResidents(20, 20, RaceKind.Human, 12);
    var nation = engine.State.Nations.Single();
    engine.RenameNation(nation.Id, "海岚共同体");
    engine.SetNationResources(nation.Id, 800, 450, 320, 175);
    engine.SetNationColor(nation.Id, 0x00123456);
    engine.SetNationTechnology(nation.Id, 4);
    RejectAction(() => engine.SetNationTechnology(nation.Id, 0), "technology below the supported range");
    RejectAction(() => engine.SetNationTechnology(nation.Id, 6), "technology above the supported range");
    var settlements = engine.State.Settlements.Where(s => s.NationId == nation.Id).ToArray();
    Check(nation.Name == "海岚共同体", "Nation name editor did not apply.");
    Check(nation.ColorArgb == 0xFF123456, "Nation color editor did not preserve RGB with opaque alpha.");
    Check(nation.Technology == 4, "Technology editing or rejected input changed the selected level.");
    Check(Math.Abs(settlements.Sum(s => s.Resources.Food) - 800) < 0.001, "Food editor did not update settlement stocks.");
    Check(Math.Abs(settlements.Sum(s => s.Resources.Wood) - 450) < 0.001, "Wood editor did not update settlement stocks.");
    Check(Math.Abs(settlements.Sum(s => s.Resources.Stone) - 320) < 0.001, "Stone editor did not update settlement stocks.");
    Check(Math.Abs(settlements.Sum(s => s.Resources.Ore) - 175) < 0.001, "Ore editor did not update settlement stocks.");
    Check(Math.Abs(nation.Resources.Food - 800) < 0.001, "Nation's displayed total differs from authoritative stock.");
    var resumed = WorldEngine.ImportJson(engine.ExportJson());
    Check(resumed.State.Nations.Single().Name == "海岚共同体", "Edited name did not survive saving.");
    Check(resumed.State.Nations.Single().ColorArgb == 0xFF123456 && resumed.State.Nations.Single().Technology == 4,
        "Edited color or technology did not survive saving.");
    Check(Math.Abs(resumed.State.Nations.Single().Resources.Food - 800) < 0.001, "Edited resources did not survive saving.");
}

static void NationTerritoryEditing()
{
    var engine = FlatWorld();
    engine.SpawnResidents(12, 16, RaceKind.Human, 24);
    engine.SpawnResidents(40, 16, RaceKind.Elf, 18);
    engine.SpawnResidents(24, 48, RaceKind.Dwarf, 24);
    var originalNations = engine.State.Nations.ToArray();
    var firstTown = engine.State.Settlements[0];
    var secondTown = engine.State.Settlements[1];
    var thirdTown = engine.State.Settlements[2];
    RejectAction(() => engine.SplitSettlement(firstTown.Id, "过早独立"), "splitting a country's only settlement");
    foreach (var nation in originalNations) engine.SetNationResources(nation.Id, 10000, 500, 500, 500);
    var residentIds = engine.State.Residents.Select(r => r.Id).ToHashSet();
    engine.TransferTerritory(secondTown.X, secondTown.Y, originalNations[0].Id, 3);
    Check(engine.State.Nations.All(n => n.Id != originalNations[1].Id), "A nation with no remaining town survived the transfer.");
    Check(secondTown.NationId == originalNations[0].Id, "The transferred settlement did not join its target nation.");
    Check(residentIds.SetEquals(engine.State.Residents.Select(r => r.Id)), "Transferring a town deleted or created residents.");
    CheckResidents(engine.State);
    _ = WorldEngine.ImportJson(engine.ExportJson());

    engine.SetDiplomacy(originalNations[0].Id, originalNations[2].Id, DiplomaticStatus.War);
    engine.Step(35);
    Check(engine.State.Armies.Any(a => a.NationId == originalNations[0].Id), "Transfer scenario requires soldiers with an active home reference.");
    residentIds = engine.State.Residents.Select(r => r.Id).ToHashSet();
    engine.TransferTerritory(firstTown.X, firstTown.Y, originalNations[2].Id, 3);
    Check(firstTown.NationId == originalNations[2].Id, "Capital territory did not transfer.");
    Check(engine.State.Nations.Single(n => n.Id == originalNations[0].Id).CapitalId == secondTown.Id,
        "Losing a capital did not designate the remaining settlement as capital.");
    Check(residentIds.SetEquals(engine.State.Residents.Select(r => r.Id)), "Capital transfer lost existing civilians or soldiers.");
    CheckResidents(engine.State);
    _ = WorldEngine.ImportJson(engine.ExportJson());

    var newNationId = engine.SplitSettlement(thirdTown.Id, "苍湾联邦");
    var independent = engine.State.Nations.Single(n => n.Id == newNationId);
    Check(independent.Name == "苍湾联邦" && independent.CapitalId == thirdTown.Id,
        "Split did not create the named country with its selected capital.");
    Check(thirdTown.NationId == newNationId, "The independent settlement still belongs to its parent.");
    Check(engine.State.Nations.Single(n => n.Id == originalNations[2].Id).CapitalId == firstTown.Id,
        "Parent country did not replace its seceding capital.");
    Check(residentIds.SetEquals(engine.State.Residents.Select(r => r.Id)), "Splitting a settlement changed the resident roster.");
    CheckResidents(engine.State);
    _ = WorldEngine.ImportJson(engine.ExportJson());

    Check(engine.State.Armies.Any(a => a.NationId == originalNations[0].Id), "Final transfer must exercise an existing army.");
    engine.TransferTerritory(secondTown.X, secondTown.Y, newNationId, 3);
    Check(engine.State.Nations.All(n => n.Id != originalNations[0].Id), "The absorbed country remains after losing its final capital.");
    Check(engine.State.Diplomacies.All(d => d.FirstNationId != originalNations[0].Id && d.SecondNationId != originalNations[0].Id),
        "Diplomacy still references the absorbed country.");
    Check(engine.State.Armies.All(a => a.NationId != originalNations[0].Id),
        "An army still belongs to the absorbed country.");
    Check(residentIds.SetEquals(engine.State.Residents.Select(r => r.Id)), "Final capital transfer discarded mobilized residents.");
    CheckResidents(engine.State);
    AssertResume(engine, 37);
}

static void SpawnOnOwnedLand()
{
    var engine = FlatWorld();
    engine.SpawnResidents(24, 24, RaceKind.Human, 60);
    var nationId = engine.State.Nations.Single().Id;
    engine.SetNationResources(nationId, 10000, 500, 500, 500);
    engine.TransferTerritory(24, 24, nationId, 10);
    Check(engine.State.Tiles[24 * engine.State.Width + 33].NationId == nationId,
        "Scenario requires owned territory beyond the village founding radius.");
    var before = engine.State.Population;
    engine.SpawnResidents(33, 24, RaceKind.Elf, 8);
    Check(engine.State.Nations.Count == 1 && engine.State.Settlements.Count == 1,
        "Spawning within owned territory created a phantom country or settlement.");
    Check(engine.State.Population == before + 8 && engine.State.Residents.All(r => r.NationId == nationId),
        "New residents did not join the landowner's existing country.");
    CheckResidents(engine.State);
    AssertResume(engine, 12);
}

static void FoodAvailability()
{
    var poor = FlatWorld();
    foreach (var tile in poor.State.Tiles)
    {
        tile.Terrain = TerrainType.Sand;
        tile.Fertility = 0;
    }
    poor.SpawnResidents(24, 24, RaceKind.Human, 20);
    var nationId = poor.State.Nations.Single().Id;
    poor.SetNationResources(nationId, 0, 100, 100, 100);
    var wealthy = WorldEngine.ImportJson(poor.ExportJson());
    wealthy.SetNationResources(nationId, 20000, 100, 100, 100);
    poor.Step(180);
    wealthy.Step(180);
    Check(wealthy.State.Residents.Sum(r => r.Health) > poor.State.Residents.Sum(r => r.Health),
        "Food shortage did not reduce population or total health compared with plentiful food.");
    Check(wealthy.State.Population > 0, "Well supplied population did not survive.");
}

static void War()
{
    var engine = WarWorld();
    var originalIds = engine.State.Residents.Select(r => r.Id).ToHashSet();
    var owners = engine.State.Settlements.ToDictionary(s => s.Id, s => s.NationId);
    var nations = engine.State.Nations.ToArray();
    engine.SetDiplomacy(nations[0].Id, nations[1].Id, DiplomaticStatus.War);
    var formedArmy = false;
    for (var step = 0; step < 500; step++)
    {
        engine.Tick();
        formedArmy |= engine.State.Armies.Count > 0;
    }
    Check(formedArmy, "Declared war never mobilized an army.");
    var capture = engine.State.Settlements.Any(s => owners.TryGetValue(s.Id, out var owner) && owner != s.NationId);
    var casualties = originalIds.Except(engine.State.Residents.Select(r => r.Id)).Any();
    Check(capture || casualties, "War on connected land produced neither casualties nor capture.");
    Check(engine.State.Events.Any(e => e.Kind == WorldEventKind.War), "War produced no observable history.");
    CheckResidents(engine.State);
}

static WorldEngine FlatWorld(int size = 64)
{
    var engine = WorldEngine.Create(1234, size, size, false);
    engine.State.NaturalDisasters = false;
    foreach (var tile in engine.State.Tiles)
    {
        tile.Terrain = TerrainType.Grass;
        tile.Fertility = 80;
        tile.Elevation = 80;
    }
    return engine;
}

static WorldEngine CreateBenchmarkWorld(int population = 2000, bool wars = true)
{
    var engine = WorldEngine.Create(451, 256, 256, false);
    engine.State.NaturalDisasters = false;
    foreach (var tile in engine.State.Tiles)
    {
        tile.Terrain = TerrainType.Grass;
        tile.Fertility = 80;
    }
    for (var row = 0; row < 4; row++)
    for (var column = 0; column < 4; column++)
    {
        var townIndex = row * 4 + column;
        var remaining = population / 16 + (townIndex < population % 16 ? 1 : 0);
        while (remaining > 0)
        {
            var count = Math.Min(200, remaining);
            engine.SpawnResidents(32 + column * 64, 32 + row * 64, (RaceKind)((row + column) % 4), count);
            remaining -= count;
        }
    }
    var nationIds = engine.State.Nations.Select(n => n.Id).ToArray();
    foreach (var id in nationIds) engine.SetNationResources(id, 100000, 10000, 10000, 10000);
    for (var i = 0; wars && i < nationIds.Length; i += 2)
        engine.SetDiplomacy(nationIds[i], nationIds[i + 1], DiplomaticStatus.War);
    return engine;
}

static void Benchmark()
{
    var engine = CreateBenchmarkWorld();
    var initialPopulation = engine.State.Population;
    var initialNations = engine.State.Nations.Count;
    engine.Step(60);
    var timer = Stopwatch.StartNew();
    engine.Step(120);
    timer.Stop();
    Console.WriteLine($"BENCHMARK native .NET: 256×256 terrain, {initialPopulation} starting residents, " +
        $"{initialNations} starting nations, 8 wars; 120 ticks in {timer.Elapsed.TotalMilliseconds:F1} ms " +
        $"({timer.Elapsed.TotalMilliseconds / 120:F2} ms/tick), final population {engine.State.Population}, " +
        $"active armies {engine.State.Armies.Count}. Browser/mobile rendering is not measured.");
    var save = engine.ExportJson();
    var saveBytes = Encoding.UTF8.GetByteCount(save);
    Console.WriteLine($"BENCHMARK populated v2 save: {saveBytes} bytes at tick {engine.State.Tick}, " +
        $"{engine.State.Residents.Sum(r => r.Agent.Memory.Count)} remembered facts.");
    Check(saveBytes <= WorldEngine.MaxSaveBytes, "The populated target-scale world exceeds the save size limit.");
    _ = WorldEngine.ImportJson(save);
}

static WorldEngine WarWorld()
{
    var engine = FlatWorld();
    engine.SpawnResidents(15, 32, RaceKind.Human, 24);
    engine.SpawnResidents(46, 32, RaceKind.Orc, 12);
    Check(engine.State.Nations.Count == 2, "Scenario requires two separate nations.");
    foreach (var nation in engine.State.Nations)
        engine.SetNationResources(nation.Id, 20000, 500, 500, 500);
    return engine;
}

static void CheckResidents(WorldState state)
{
    foreach (var resident in state.Residents)
    {
        Check(resident.X >= 0 && resident.X < state.Width && resident.Y >= 0 && resident.Y < state.Height,
            $"Resident {resident.Id} is outside the map.");
        Check(WorldEngine.CanTraverse(state.Tiles[resident.Y * state.Width + resident.X], resident.TravelMode, resident.Race),
            $"Resident {resident.Id} occupies impassable terrain.");
        Check(state.Nations.Any(n => n.Id == resident.NationId), $"Resident {resident.Id} has no nation.");
        Check(state.Settlements.Any(s => s.Id == resident.SettlementId && s.NationId == resident.NationId),
            $"Resident {resident.Id} has no settlement in their own country.");
        Check(resident.ArmyId == 0 || state.Armies.Any(a => a.Id == resident.ArmyId && a.NationId == resident.NationId),
            $"Resident {resident.Id} has an invalid army reference.");
        Check(double.IsFinite(resident.Health) && double.IsFinite(resident.Hunger), "Resident health is not finite.");
    }
}

static string Modify(string json, Action<JsonNode> change)
{
    var root = JsonNode.Parse(json)!;
    change(root);
    return root.ToJsonString();
}

static void Reject(string json, string reason)
{
    var rejected = false;
    try { _ = WorldEngine.ImportJson(json); }
    catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or System.Text.Json.JsonException or FormatException or System.IO.InvalidDataException)
    {
        rejected = true;
    }
    Check(rejected, $"Importer accepted {reason}.");
}

static void RejectAction(Action action, string reason)
{
    var rejected = false;
    try { action(); }
    catch (Exception ex) when (ex is ArgumentException or InvalidOperationException) { rejected = true; }
    Check(rejected, $"Editor accepted {reason}.");
}

static void Check(bool condition, string message)
{
    if (!condition) throw new InvalidOperationException(message);
}
