using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using SeWZC.WorldBox.Core;

internal static class PerformanceBehaviorTests
{
    public static IEnumerable<(string Name, Action Run)> Cases =>
    [
        ("ecology updates daily bands and resumes from every phase", DailyEcology),
        ("ecology reads changed food capacity before the next band", CapacityEdits),
        ("large-map ecology uses sparse cycles and resumes across band boundaries", SparseEcology),
        ("territory totals track edits and rebuild after loading", TerritoryEdits),
        ("buffered saves preserve complete JSON and cancel before returning a partial capture", BufferedSave),
        ("compact saves preserve all species exact values and explicit nonzero defaults", CompactSave),
        ("save text chunks preserve UTF-8 split across writes and chunk boundaries", Utf8Chunks),
        ("ecological value equality covers every saved field and preserves default omission", EcologicalValueEquality),
        ("edible animals reflect edits ecology and cold save restoration", EdibleAnimals),
        ("batch wildlife capacities match individual queries through habitat and population edits",
            BatchWildlifeCapacities),
    ];

    private static WorldEngine Flat()
    {
        var engine = WorldEngine.Create(73921, 32, 32, false);
        engine.State.NaturalDisasters = false;
        foreach (var tile in engine.State.Tiles)
        {
            tile.Terrain = TerrainType.Grass;
            tile.Fertility = 100;
            tile.ResourceAmount = 100;
            tile.NaturalWaterYield = .02;
            tile.Wildlife = WildlifeKind.Rabbit;
            tile.WildlifePopulation = 1;
            tile.OtherWildlife = default;
        }

        return engine;
    }

    private static void Check(bool valid, string message)
    {
        if (!valid) throw new InvalidOperationException(message);
    }

    [UnitTest]
    private static void BatchWildlifeCapacities()
    {
        Span<double> capacities = stackalloc double[AnimalRules.SpeciesCount];
        Span<WildlifeKind> groups = stackalloc WildlifeKind[6];
        foreach (var terrain in Enum.GetValues<TerrainType>())
        {
            var tile = new Tile
            {
                Terrain = terrain,
                Fertility = 100,
                ResourceAmount = 100,
                NaturalWaterYield = .02,
                Plants = new PlantCoverage { Grass = .3, Trees = .4, Shrubs = .2, Reeds = .1 },
                Wildlife = WildlifeKind.Rabbit,
                WildlifePopulation = .5,
                OtherWildlife = new WildlifePopulations
                {
                    Deer = .3,
                    Fish = .2,
                    Bison = .1,
                    SeaCow = .2,
                    Wolf = .04,
                    Fox = .020001,
                    Bear = .05,
                    SnowLeopard = .04,
                },
            };
            for (var edit = 0; edit < 8; edit++)
            {
                switch (edit)
                {
                    case 1: tile.OtherWildlife = new WildlifePopulations { Fish = .04, Hippo = 2 }; break;
                    case 2:
                        tile.WildlifePopulation = 0;
                        tile.NaturalWaterYield = .001;
                        break;
                    case 3: tile.Fertility = 20; break;
                    case 4:
                        tile.Improvement = LandImprovement.Farmland;
                        tile.SettlementId = 1;
                        break;
                    case 5: tile.ResourceAmount = 0; break;
                    case 6: tile.DroughtTicks = 1; break;
                    case 7: tile.FireTicks = 1; break;
                }

                WorldEngine.FillWildlifeCapacities(tile, capacities);
                WorldEngine.FillVisibleWildlife(tile, groups);
                for (var group = 0; group < 6; group++)
                    Check(groups[group] == WorldEngine.VisibleWildlife(tile, group), "Batch visible species differ");
                Check(capacities[0] == 0, "None has an ecological capacity");
                foreach (var kind in AnimalRules.Species)
                    Check(capacities[(int)kind] == WorldEngine.WildlifeCapacity(tile, kind),
                        $"Batch capacity differs for {terrain}, {kind}, edit {edit}");
            }
        }

        var engine = Flat();
        var before = engine.ExportJson();
        WorldEngine.FillWildlifeCapacities(engine.State.Tiles[0], capacities);
        Check(before == engine.ExportJson(), "Capacity observation changed the saved world");
    }

    [UnitTest]
    private static void EdibleAnimals()
    {
        var query = typeof(WorldEngine).GetMethod("EdibleAnimal", BindingFlags.NonPublic | BindingFlags.Static)!
            .CreateDelegate<Func<Tile, bool, WildlifeKind>>();
        var tile = new Tile
        {
            Terrain = TerrainType.Grass,
            Wildlife = WildlifeKind.Deer,
            WildlifePopulation = .5,
            OtherWildlife = new WildlifePopulations { Rabbit = .49, Boar = 1, Fish = 2 },
        };

        void Expect(WildlifeKind expected, bool water = false)
        {
            Check(query(tile, water) == expected, "Stale edible animal after changing populations or terrain");
        }

        Expect(WildlifeKind.Boar);
        Expect(WildlifeKind.Boar);
        tile.OtherWildlife = new WildlifePopulations { Rabbit = 3, Boar = 1, Fish = 2 };
        Expect(WildlifeKind.Rabbit);
        tile.Wildlife = WildlifeKind.Rabbit;
        Expect(WildlifeKind.Boar);
        tile.WildlifePopulation = 3;
        Expect(WildlifeKind.Rabbit);
        tile.Terrain = TerrainType.Lake;
        Expect(WildlifeKind.None);
        Expect(WildlifeKind.Fish, true);
        tile.OtherWildlife = new WildlifePopulations { Fish = .049, SeaCow = .05, Shark = 3 };
        Expect(WildlifeKind.SeaCow, true);
        tile.OtherWildlife = default;
        Expect(WildlifeKind.None, true);
        tile.Terrain = TerrainType.Grass;
        Expect(WildlifeKind.Rabbit);
        Expect(WildlifeKind.None, true);
        tile.WildlifePopulation = 0;
        Expect(WildlifeKind.None);

        var engine = Flat();

        WildlifeKind Reference(Tile current, bool water)
        {
            if (water != WorldEngine.IsWaterTerrain(current.Terrain)) return WildlifeKind.None;
            return AnimalRules.Species.Where(kind => AnimalRules.For(kind).Diet == AnimalDiet.Herbivore
                                                     && (!water || AnimalRules.For(kind).Aquatic) &&
                                                     current.AnimalPopulation(kind) >= .05)
                .OrderByDescending(kind => current.AnimalPopulation(kind) * AnimalRules.For(kind).BodyMass)
                .FirstOrDefault();
        }

        foreach (var kind in AnimalRules.Species)
        {
            tile = engine.State.Tiles[(int)kind];
            tile.Wildlife = kind;
            tile.WildlifePopulation = .5;
            var others = new WildlifePopulations { Fish = .5, SeaCow = 1, SnowLeopard = 2 };
            others.Set(kind, 0);
            tile.OtherWildlife = others;
            foreach (var terrain in new[] { TerrainType.Grass, TerrainType.Lake })
            {
                tile.Terrain = terrain;
                foreach (var water in new[] { false, true })
                    Check(query(tile, water) == Reference(tile, water), "Species order or diet changed");
            }
        }

        var beforeQueries = engine.ExportJson();

        void Verify(WorldEngine current)
        {
            foreach (var t in current.State.Tiles)
            foreach (var water in new[] { false, true })
                Check(query(t, water) == Reference(t, water), "Ecology left a stale edible result");
        }

        Verify(engine);
        Check(engine.ExportJson() == beforeQueries, "Animal queries changed saved state");
        var resumed = WorldEngine.ImportJson(beforeQueries);
        Verify(resumed);
        for (var day = 0; day < 12; day++)
        {
            engine.Step();
            resumed.Step();
            Verify(engine);
        }

        Check(engine.ExportJson() == resumed.ExportJson(), "Warm and restored animal caches diverged");
    }

    [UnitTest]
    private static void EcologicalValueEquality()
    {
        void Fields<T>() where T : struct, IEquatable<T>
        {
            foreach (var property in typeof(T).GetProperties()
                         .Where(p => p.CanWrite && p.PropertyType == typeof(double)))
            {
                object box = default(T);
                property.SetValue(box, .25);
                var value = (T)box;
                Check(!value.Equals(default) && !default(T).Equals(value), "Equality omitted " + property.Name);
                Check(
                    value.Equals((object)value) && value.Equals(value) && value.GetHashCode() == ((T)box).GetHashCode(),
                    "Inconsistent equality for " + property.Name);
                property.SetValue(box, -0d);
                var zero = (T)box;
                Check(zero.Equals(default) && zero.GetHashCode() == default(T).GetHashCode(),
                    "Signed zero changed equality for " + property.Name);
                property.SetValue(box, double.NaN);
                value = (T)box;
                Check(value.Equals((T)box), "NaN changed existing double equality for " + property.Name);
            }
        }

        Fields<WildlifePopulations>();
        Fields<PlantCoverage>();
        var engine = Flat();
        var tile = engine.State.Tiles[0];
        tile.OtherWildlife = default;
        tile.Plants = default;
        var json = engine.ExportJson();
        using (var document = JsonDocument.Parse(json))
        {
            Check(!document.RootElement.GetProperty("Tiles")[0].TryGetProperty("OtherWildlife", out _)
                  && !document.RootElement.GetProperty("Tiles")[0].TryGetProperty("p", out _),
                "Empty ecological values were written");
        }

        tile.OtherWildlife = new WildlifePopulations { SnowLeopard = .25 };
        tile.Plants = new PlantCoverage { Reeds = .25 };
        json = engine.ExportJson();
        var resumed = WorldEngine.ImportJson(json);
        Check(resumed.State.Tiles[0].OtherWildlife.Equals(tile.OtherWildlife) && resumed.State.Tiles[0].Plants
                                                                                  .Equals(tile.Plants)
                                                                              && resumed.ExportJson() == json,
            "Nonempty ecological values changed during saving");
    }

    [UnitTest]
    private static void DailyEcology()
    {
        var engine = Flat();
        foreach (var tile in engine.State.Tiles) tile.WildlifePopulation = .01;
        engine.Step();
        Check(engine.State.Tiles[2 * 32 + 16].WildlifePopulation > .01, "First band did not grow on the first day");
        Check(engine.State.Tiles[28 * 32 + 16].WildlifePopulation == .01, "Distant band grew before its turn");
        for (var phase = 1; phase <= 6; phase++)
        {
            var resumed = WorldEngine.ImportJson(engine.ExportJson());
            engine.Step();
            resumed.Step();
            Check(engine.ExportJson() == resumed.ExportJson(), $"Ecology resume diverged at phase {phase}");
        }

        foreach (var row in new[] { 2, 8, 13, 18, 24, 29 })
            Check(engine.State.Tiles[row * 32 + 16].WildlifePopulation > .01,
                "Some rows skipped their six-day growth cycle");
    }

    [UnitTest]
    private static void CapacityEdits()
    {
        var engine = Flat();
        var tile = engine.State.Tiles[16 * 32 + 16];
        tile.Terrain = TerrainType.Forest;
        tile.Wildlife = WildlifeKind.Deer;
        tile.WildlifePopulation = 2;
        engine.Step(3); // 此行曾作为上一分区的迁移边界读取。
        tile.ResourceAmount = 0;
        engine.Step();
        Check(tile.WildlifePopulation > 0 && tile.WildlifePopulation <= 1.76,
            "Depleted food did not invalidate the cached habitat");
    }

    private static void SparseEcology()
    {
        var engine = WorldEngine.Create(73921, 128, 128, false);
        engine.State.NaturalDisasters = false;
        engine.State.Rules.ResourceRegeneration = false;
        foreach (var tile in engine.State.Tiles)
        {
            tile.Terrain = TerrainType.Tundra;
            tile.Fertility = 100;
            tile.ResourceAmount = 100;
            tile.NaturalWaterYield = .02;
            tile.Wildlife = WildlifeKind.None;
            tile.WildlifePopulation = 0;
            tile.OtherWildlife = default;
        }

        var early = engine.State.Tiles[64];
        var late = engine.State.Tiles[127 * 128 + 64];
        early.Wildlife = late.Wildlife = WildlifeKind.Rabbit;
        early.WildlifePopulation = late.WildlifePopulation = 1;
        late.OtherWildlife = new WildlifePopulations { SnowLeopard = .1 };
        Check(engine.WildlifeCycleDays == 64, "Large-map cycle did not scale with its daily work budget");
        engine.Step();
        Check(early.WildlifePopulation != 1 && late.WildlifePopulation == 1,
            "Distant animals were reevaluated before their scheduled turn");
        engine.Step(61);
        Check(late.WildlifePopulation == 1 && late.OtherWildlife.SnowLeopard == .1,
            "Last band changed before its turn");
        var resumed = WorldEngine.ImportJson(engine.ExportJson());
        engine.Step(3);
        resumed.Step(3);
        Check(engine.ExportJson() == resumed.ExportJson(), "Sparse ecology diverged across its last and first bands");
        Check(late.WildlifePopulation != 1 && late.OtherWildlife.SnowLeopard != .1,
            "Sparse updates missed herbivores or species in mask bit 31");
        Check(engine.State.Tiles.All(t => t.WildlifePopulation >= 0 && t.OtherWildlife.SnowLeopard >= 0),
            "Coarse predation and migration produced negative stock");
    }

    [UnitTest]
    private static void TerritoryEdits()
    {
        var engine = Flat();
        engine.SpawnResidents(16, 16, RaceKind.Human, 6);
        engine.Step();
        var nation = engine.State.Nations.Single();
        var tile = engine.State.Tiles[0];
        tile.NationId = nation.Id;
        engine.Step();
        Check(nation.Territory == engine.State.Tiles.Count(t => t.NationId == nation.Id),
            "Direct ownership edit left a stale territory count");
        tile.NationId = 0;
        engine.TransferTerritory(3, 3, nation.Id, 1);
        engine.Step();
        Check(nation.Territory == engine.State.Tiles.Count(t => t.NationId == nation.Id),
            "Territory brush left a stale territory count");
        var resumed = WorldEngine.ImportJson(engine.ExportJson());
        resumed.Step();
        engine.Step();
        Check(resumed.ExportJson() == engine.ExportJson(), "Ownership index did not rebuild from saved land");
    }

    [UnitTest]
    private static void BufferedSave()
    {
        var engine = Flat();
        var before = engine.ExportJson();
        var writes = 0;
        var json = engine.ExportJsonAsync(_ =>
        {
            writes++;
            return ValueTask.CompletedTask;
        }).GetAwaiter().GetResult();
        Check(writes > 1 && json == before, "Buffered save changed the complete JSON or never offered a yield");
        var chunks = engine.ExportJsonChunksAsync(_ => ValueTask.CompletedTask).GetAwaiter().GetResult();
        Check(chunks.Length > 1 && chunks.All(c => c.Length <= 16 * 1024) && string.Concat(chunks) == before,
            "Chunked capture changed JSON or returned unbounded text chunks");
        using var cancellation = new CancellationTokenSource();
        try
        {
            engine.ExportJsonAsync(_ =>
            {
                cancellation.Cancel();
                return ValueTask.CompletedTask;
            }, cancellation.Token).GetAwaiter().GetResult();
            throw new Exception("Canceled capture returned a partial save");
        }
        catch (OperationCanceledException)
        {
        }

        Check(engine.ExportJson() == before && WorldEngine.ImportJson(json).ExportJson() == before,
            "Save capture modified the world or produced invalid JSON");
    }

    [UnitTest]
    private static void Utf8Chunks()
    {
        var type = typeof(WorldEngine).GetNestedType("YieldingSaveStream", BindingFlags.NonPublic)!;
        var text = "😊中" + new string('a', 16 * 1024 - 1) + "😊界" + new string('b', 16 * 1024 - 1) + "终";
        var bytes = Encoding.UTF8.GetBytes(text);
        using var stream =
            (Stream)Activator.CreateInstance(type, (Func<CancellationToken, ValueTask>)(_ => ValueTask.CompletedTask))!;
        // 首个字符跨序列化写入边界；后续字符跨单次写入中的内部缓冲边界。
        foreach (var count in new[] { 1, 1, 2, 1, 2 })
        {
            stream.WriteAsync(bytes.AsMemory(0, count)).GetAwaiter().GetResult();
            bytes = bytes[count..];
        }

        stream.WriteAsync(bytes).GetAwaiter().GetResult();
        var chunks = (string[])type.GetMethod("Complete")!.Invoke(stream, null)!;
        Check(string.Concat(chunks) == text && chunks.All(c => c.Length is > 0 and <= 16 * 1024),
            "UTF-8 was corrupted or text chunks exceeded the bound");
    }

    [UnitTest]
    private static void CompactSave()
    {
        var engine = Flat();
        engine.SpawnResidents(16, 16, RaceKind.Human, 1);
        var person = engine.State.Residents[0];
        person.Name = "零值与😊";
        person.Health = person.Mana = person.MagicTalent = 0;
        person.Inventory = new ResourceStock();
        var tile = engine.State.Tiles[0];
        tile.ResourceAmount = 0;
        tile.Wildlife = WildlifeKind.None;
        tile.WildlifePopulation = 0;
        var wildlife = new WildlifePopulations();
        foreach (var kind in AnimalRules.Species)
            wildlife.Set(kind, (int)kind == 1 ? double.Epsilon : (int)kind + .123456789012345);
        tile.OtherWildlife = wildlife;
        var json = engine.ExportJson();
        var root = JsonNode.Parse(json)!;
        Check(root["Residents"]![0]!["Inventory"]!.AsObject().Count == 0,
            "Zero resource amounts still occupy the save");
        foreach (var name in new[] { "NationId", "SettlementId", "FireTicks", "DroughtTicks", "RoadLevel" })
            Check(root["Tiles"]![0]![name] is null, "Zero tile state still occupies the save: " + name);
        Check(root["Tiles"]![0]!["ResourceAmount"]!.GetValue<double>() == 0
              && root["Residents"]![0]!["Health"]!.GetValue<double>() == 0
              && !root["NaturalDisasters"]!.GetValue<bool>(),
            "A zero/false value with a nonzero initializer was omitted");
        var resumed = WorldEngine.ImportJson(json);
        Check(resumed.State.Tiles[0].OtherWildlife.Equals(wildlife) && resumed.ExportJson() == json,
            "Sparse populations lost a species or rounded an exact value");
        foreach (var invalid in new JsonNode[]
                 {
                     new JsonArray(1, 1, 1, 2), new JsonArray(0, 1), new JsonArray(32, 1), new JsonArray(1),
                     new JsonArray(1, -1), new JsonArray(1, 1001), new JsonArray(new JsonObject()),
                     new JsonObject { ["Rabbit"] = 1 },
                 })
        {
            var bad = root.DeepClone();
            bad["Tiles"]![0]!["OtherWildlife"] = invalid;
            try
            {
                WorldEngine.ImportJson(bad.ToJsonString());
                throw new Exception("Invalid sparse population accepted");
            }
            catch (ArgumentException)
            {
            }
        }

        engine.Step(8);
        resumed.Step(8);
        Check(engine.ExportJson() == resumed.ExportJson(), "Compact capture changed subsequent simulation");
    }
}
