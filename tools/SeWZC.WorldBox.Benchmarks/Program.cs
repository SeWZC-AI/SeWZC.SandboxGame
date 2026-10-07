using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;
using SeWZC.WorldBox.Core;

(string Name, int Size, int Population, int Days)[] cases =
[
    ("64x64-144", 64, 144, 36),
    ("128x128-1024", 128, 1024, 24),
    ("256x256-4096", 256, 4096, 12),
];
if (args.Length is < 1 or > 2 || args.Length == 2 && args[1] is not ("--verify" or "--default-rules"))
    throw new ArgumentException("请指定结果 JSON 路径，可追加 --verify 验证续演或 --default-rules 测量默认规则；使用 DOTNET_TieredCompilation=0。");
var core = typeof(WorldEngine).Assembly;
var coreSha256 = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(core.Location)));
var results = new List<object>();
if (args.Length == 2 && args[1] == "--verify")
{
    // 完整规则的长程行为检查独立运行，不计入性能样本或单元测试。
    foreach (var seed in new[] { 42, 731 })
    foreach (var size in new[] { 64, 128 })
    {
        var engine = WorldEngine.Create(seed, size, size, true);
        var rules = engine.State.Rules;
        engine.Step(120);
        var restored = WorldEngine.ImportJson(engine.ExportJson());
        engine.Step(120);
        restored.Step(120);
        var state = CanonicalState(engine.ExportJson());
        if (!state.AsSpan().SequenceEqual(CanonicalState(restored.ExportJson())))
            throw new InvalidOperationException("默认规则下中途保存恢复后的续演结果不同。");
        results.Add(new
        {
            Seed = seed, Size = size, Days = 240, InitialPopulation = 144,
            FinalPopulation = engine.State.Population, Rules = rules, NaturalDisasters = true,
            CoreAssemblySha256 = coreSha256,
            StateSha256 = Convert.ToHexString(SHA256.HashData(state)), SaveContinuationVerified = true,
        });
        Console.WriteLine($"Verified seed {seed}, {size}x{size}, 240 days, population {engine.State.Population}");
    }
    File.WriteAllText(args[0], JsonSerializer.Serialize(results, new JsonSerializerOptions { WriteIndented = true }));
    return;
}
foreach (var scenario in cases)
{
    var engine = WorldEngine.Create(42, scenario.Size, scenario.Size, true);
    var towns = engine.State.Settlements.ToArray();
    var extra = scenario.Population - engine.State.Population;
    for (var i = 0; i < towns.Length; i++)
    {
        var remaining = extra / towns.Length + (i < extra % towns.Length ? 1 : 0);
        while (remaining > 0)
        {
            var batch = Math.Min(remaining, 200);
            engine.SpawnResidents(towns[i].X, towns[i].Y,
                engine.State.Residents.First(resident => resident.SettlementId == towns[i].Id).Race, batch);
            remaining -= batch;
        }
    }
    if (engine.State.Population != scenario.Population)
        throw new InvalidOperationException("基准世界未达到要求的人口数量。");
    if (args.Length == 1)
    {
        engine.ConfigureWorld(engine.State.Rules with
        {
            Births = false, Aging = false, Hunger = false, Thirst = false, Disease = false,
            Wars = false, Secession = false,
        }, false, true);
    }
    var initial = engine.ExportJson();
    for (var warmup = 0; warmup < 2; warmup++)
    {
        engine = WorldEngine.ImportJson(initial);
        engine.Step(scenario.Days);
    }
    Console.WriteLine($"Warmup {scenario.Name}: {scenario.Days * 2} days, initial population {scenario.Population}");
    var times = new List<double>();
    var allocations = new List<long>();
    var collections = new List<int[]>();
    string? checksum = null;
    var finalPopulation = 0;
    for (var repetition = 0; repetition < 7; repetition++)
    {
        engine = WorldEngine.ImportJson(initial);
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        var before = GC.GetAllocatedBytesForCurrentThread();
        var gen0 = GC.CollectionCount(0);
        var gen1 = GC.CollectionCount(1);
        var gen2 = GC.CollectionCount(2);
        var watch = Stopwatch.StartNew();
        engine.Step(scenario.Days);
        watch.Stop();
        times.Add(watch.Elapsed.TotalMilliseconds / scenario.Days);
        allocations.Add((GC.GetAllocatedBytesForCurrentThread() - before) / scenario.Days);
        collections.Add([GC.CollectionCount(0) - gen0, GC.CollectionCount(1) - gen1, GC.CollectionCount(2) - gen2]);
        var saved = engine.ExportJson();
        var observed = Convert.ToHexString(SHA256.HashData(CanonicalState(saved)));
        if (checksum is not null && checksum != observed)
            throw new InvalidOperationException("相同初态的重复模拟产生了不同结果。");
        checksum = observed;
        finalPopulation = engine.State.Population;
        var restored = WorldEngine.ImportJson(saved);
        engine.Step(1);
        restored.Step(1);
        if (!CanonicalState(engine.ExportJson()).AsSpan().SequenceEqual(CanonicalState(restored.ExportJson())))
            throw new InvalidOperationException("中途保存恢复后的续演结果不同。");
        Console.WriteLine($"{scenario.Name} {repetition}: {times[^1]:F3} ms/day, {allocations[^1]} bytes/day");
    }
    results.Add(new
    {
        scenario.Name, scenario.Size, InitialPopulation = scenario.Population, Seed = 42, scenario.Days,
        WarmupDays = scenario.Days * 2, Repetitions = 7,
        Rules = engine.State.Rules, engine.State.NaturalDisasters, engine.State.Society.MagicEnabled,
        CoreAssemblySha256 = coreSha256, CoreModuleVersionId = core.ManifestModule.ModuleVersionId,
        TieredCompilation = Environment.GetEnvironmentVariable("DOTNET_TieredCompilation"),
        TimesMsPerDay = times, BytesPerDay = allocations, GcCollections = collections,
        FinalPopulation = finalPopulation, StateSha256 = checksum, SaveContinuationVerified = true,
        Runtime = System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription,
        Architecture = System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture.ToString(),
    });
}
File.WriteAllText(args[0], JsonSerializer.Serialize(results, new JsonSerializerOptions { WriteIndented = true }));

// 属性顺序不影响存档语义；数组顺序属于模拟状态，原样保留。
static byte[] CanonicalState(string json)
{
    using var document = JsonDocument.Parse(json);
    using var stream = new MemoryStream();
    using (var writer = new Utf8JsonWriter(stream))
        WriteCanonical(writer, document.RootElement);
    return stream.ToArray();
}

static void WriteCanonical(Utf8JsonWriter writer, JsonElement element)
{
    if (element.ValueKind == JsonValueKind.Object)
    {
        writer.WriteStartObject();
        foreach (var property in element.EnumerateObject().OrderBy(property => property.Name, StringComparer.Ordinal))
        {
            writer.WritePropertyName(property.Name);
            WriteCanonical(writer, property.Value);
        }
        writer.WriteEndObject();
    }
    else if (element.ValueKind == JsonValueKind.Array)
    {
        writer.WriteStartArray();
        foreach (var item in element.EnumerateArray())
            WriteCanonical(writer, item);
        writer.WriteEndArray();
    }
    else
        element.WriteTo(writer);
}
