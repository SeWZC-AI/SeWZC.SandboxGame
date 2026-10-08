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
var options = args.Skip(1).ToHashSet(StringComparer.Ordinal);
if (args.Length < 1 || options.Any(option => option is not ("--verify" or "--default-rules" or "--large" or "--steady")
        && !option.StartsWith("--seed=", StringComparison.Ordinal) && !option.StartsWith("--start-day=", StringComparison.Ordinal))
    || options.Contains("--verify") && options.Count != 1)
    throw new ArgumentException("请指定结果 JSON 路径；--default-rules 使用默认规则，--large 只测 256² / 4096 人，--steady 测量后续 64 日；--seed=整数 指定种子，--start-day=整数 指定计时前推进日数；--verify 独立验证续演。原生入口启用分层编译和动态 PGO，可用 DOTNET_TieredCompilation=0 对照。");
var benchmarkSeed = ReadIntegerOption(options, "--seed=", 42);
var startDay = ReadIntegerOption(options, "--start-day=", options.Contains("--steady") ? 12 : 0);
if (startDay is < 0 or > 10_000) throw new ArgumentOutOfRangeException(nameof(startDay));
if (options.Contains("--large")) cases = cases.Where(scenario => scenario.Size == 256).ToArray();
if (options.Contains("--steady")) cases = cases.Select(scenario => (scenario.Name, scenario.Size, scenario.Population, 64)).ToArray();
var core = typeof(WorldEngine).Assembly;
var coreSha256 = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(core.Location)));
var results = new List<object>();
if (options.Contains("--verify"))
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
        var saved = engine.ExportJson();
        var state = CanonicalState(saved);
        if (!state.AsSpan().SequenceEqual(CanonicalState(restored.ExportJson())))
            throw new InvalidOperationException("默认规则下中途保存恢复后的续演结果不同。");
        results.Add(new
        {
            Seed = seed, Size = size, Days = 240, InitialPopulation = 144,
            FinalPopulation = engine.State.Population, Rules = rules, NaturalDisasters = true,
            CoreAssemblySha256 = coreSha256,
            StateSha256 = Convert.ToHexString(SHA256.HashData(state)), SaveContinuationVerified = true,
            engine.State.FormatVersion, engine.State.SimulationVersion,
        });
        Console.WriteLine($"Verified seed {seed}, {size}x{size}, 240 days, population {engine.State.Population}");
    }
    File.WriteAllText(args[0], JsonSerializer.Serialize(results, new JsonSerializerOptions { WriteIndented = true }));
    return;
}
foreach (var scenario in cases)
{
    var engine = WorldEngine.Create(benchmarkSeed, scenario.Size, scenario.Size, true);
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
    if (!options.Contains("--default-rules"))
    {
        engine.ConfigureWorld(engine.State.Rules with
        {
            Births = false, Aging = false, Hunger = false, Thirst = false, Disease = false,
            Wars = false, Secession = false,
        }, false, true);
    }
    engine.Step(startDay);
    var measureStartTick = engine.State.Tick;
    var measuredInitialPopulation = engine.State.Population;
    var initial = engine.ExportJson();
    // 动态 PGO 在后台优化热点，短窗口也至少预热 128 日，避免首轮混入编译开销。
    var warmupRepetitions = Math.Max(2, (128 + scenario.Days - 1) / scenario.Days);
    var warmupDays = warmupRepetitions * scenario.Days;
    for (var warmup = 0; warmup < warmupRepetitions; warmup++)
    {
        engine = WorldEngine.ImportJson(initial);
        engine.Step(scenario.Days);
    }
    Console.WriteLine($"Warmup {scenario.Name}: {warmupDays} days, initial population {scenario.Population}");
    var times = new List<double>();
    var dailyTimes = new List<double[]>();
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
        var days = new double[scenario.Days];
        var before = GC.GetAllocatedBytesForCurrentThread();
        var gen0 = GC.CollectionCount(0);
        var gen1 = GC.CollectionCount(1);
        var gen2 = GC.CollectionCount(2);
        var watch = Stopwatch.StartNew();
        for (var day = 0; day < scenario.Days; day++)
        {
            var started = Stopwatch.GetTimestamp();
            engine.Step();
            days[day] = Stopwatch.GetElapsedTime(started).TotalMilliseconds;
        }
        watch.Stop();
        times.Add(watch.Elapsed.TotalMilliseconds / scenario.Days);
        dailyTimes.Add(days);
        allocations.Add((GC.GetAllocatedBytesForCurrentThread() - before) / scenario.Days);
        collections.Add([GC.CollectionCount(0) - gen0, GC.CollectionCount(1) - gen1, GC.CollectionCount(2) - gen2]);
        var saved = engine.ExportJson();
        var observed = Convert.ToHexString(SHA256.HashData(CanonicalState(saved)));
        if (checksum is not null && checksum != observed)
            throw new InvalidOperationException("相同初态的重复模拟产生了不同结果。");
        checksum = observed;
        finalPopulation = engine.State.Population;
        Console.WriteLine($"{scenario.Name} {repetition}: {times[^1]:F3} ms/day, P95 {Percentile(days, .95):F3}, max {days.Max():F3}, {allocations[^1]} bytes/day");
    }
    // 各次终态已逐一比较；同一终态的保存续演只需在计时外验证一次。
    var restored = WorldEngine.ImportJson(engine.ExportJson());
    engine.Step();
    restored.Step();
    if (!CanonicalState(engine.ExportJson()).AsSpan().SequenceEqual(CanonicalState(restored.ExportJson())))
        throw new InvalidOperationException("中途保存恢复后的续演结果不同。");
    results.Add(new
    {
        scenario.Name, scenario.Size, InitialPopulation = measuredInitialPopulation, RequestedPopulation = scenario.Population, Seed = benchmarkSeed, scenario.Days,
        MeasureStartTick = measureStartTick,
        WarmupDays = warmupDays, Repetitions = 7,
        Rules = engine.State.Rules, engine.State.NaturalDisasters, engine.State.Society.MagicEnabled,
        CoreAssemblySha256 = coreSha256, CoreModuleVersionId = core.ManifestModule.ModuleVersionId,
        TieredCompilation = Environment.GetEnvironmentVariable("DOTNET_TieredCompilation"),
        TieredPGO = Environment.GetEnvironmentVariable("DOTNET_TieredPGO"),
        ServerGarbageCollection = System.Runtime.GCSettings.IsServerGC,
        GcLatencyMode = System.Runtime.GCSettings.LatencyMode.ToString(),
        MeanMsPerDay = times.Average(), TimesMsPerDay = times, DailyTimesMs = dailyTimes,
        P95MsPerDay = Percentile(dailyTimes.SelectMany(days => days), .95),
        MaxMsPerDay = dailyTimes.SelectMany(days => days).Max(),
        BytesPerDay = allocations, GcCollections = collections,
        FinalPopulation = finalPopulation, StateSha256 = checksum, SaveContinuationVerified = true,
        engine.State.FormatVersion, engine.State.SimulationVersion,
        Runtime = System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription,
        Architecture = System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture.ToString(),
        OperatingSystem = System.Runtime.InteropServices.RuntimeInformation.OSDescription,
        LogicalProcessors = Environment.ProcessorCount,
        CpuQuota = File.Exists("/sys/fs/cgroup/cpu.max") ? File.ReadAllText("/sys/fs/cgroup/cpu.max").Trim() : null,
        MemoryLimit = File.Exists("/sys/fs/cgroup/memory.max") ? File.ReadAllText("/sys/fs/cgroup/memory.max").Trim() : null,
    });
}
File.WriteAllText(args[0], JsonSerializer.Serialize(results, new JsonSerializerOptions { WriteIndented = true }));

static int ReadIntegerOption(HashSet<string> options, string prefix, int fallback)
{
    var values = options.Where(option => option.StartsWith(prefix, StringComparison.Ordinal)).ToArray();
    if (values.Length == 0) return fallback;
    if (values.Length != 1 || !int.TryParse(values[0].AsSpan(prefix.Length), out var value))
        throw new ArgumentException($"{prefix} 只能指定一次，且必须为整数。");
    return value;
}

static double Percentile(IEnumerable<double> values, double fraction)
{
    var sorted = values.Order().ToArray();
    return sorted[(int)Math.Ceiling(sorted.Length * fraction) - 1];
}

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
