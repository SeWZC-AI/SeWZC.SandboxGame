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
if (args.Length < 1 || options.Any(option => option is not ("--verify" or "--default-rules" or "--large" or "--steady" or "--save-final")
        && !option.StartsWith("--seed=", StringComparison.Ordinal) && !option.StartsWith("--start-day=", StringComparison.Ordinal)
        && !option.StartsWith("--years=", StringComparison.Ordinal) && !option.StartsWith("--repetitions=", StringComparison.Ordinal)
        && !option.StartsWith("--population-floor=", StringComparison.Ordinal))
    || options.Contains("--verify") && options.Count != 1)
    throw new ArgumentException("请指定结果 JSON 路径；--default-rules 使用默认规则，--large 只测 256² / 4096 人，--steady 测量后续 64 日；--seed=整数 指定种子，--start-day=整数 指定计时前推进日数；--years=整数 测量长期演化，--repetitions=整数 指定轮数，--population-floor=整数 在计时外补充居民维持人口负载；--save-final 在计时外保存终态，--verify 独立验证续演。");
var benchmarkSeed = ReadIntegerOption(options, "--seed=", 42);
var years = ReadIntegerOption(options, "--years=", 0);
var repetitions = ReadIntegerOption(options, "--repetitions=", years > 0 ? 3 : 7);
var populationFloor = ReadIntegerOption(options, "--population-floor=", 0);
if (years is < 0 or > 500 || repetitions is < 1 or > 9 || populationFloor is < 0 or > WorldEngine.MaxPopulation)
    throw new ArgumentOutOfRangeException(nameof(options));
if (populationFloor > 0 && (!options.Contains("--large") || !options.Contains("--default-rules") || years == 0))
    throw new ArgumentException("维持人口负载须搭配 --large --default-rules --years=整数。");
var startDay = ReadIntegerOption(options, "--start-day=", options.Contains("--steady") ? 12 : 0);
if (startDay is < 0 or > 10_000) throw new ArgumentOutOfRangeException(nameof(startDay));
if (options.Contains("--large")) cases = cases.Where(scenario => scenario.Size == 256).ToArray();
if (options.Contains("--steady")) cases = cases.Select(scenario => (scenario.Name, scenario.Size, scenario.Population, 64)).ToArray();
if (years > 0) cases = cases.Select(scenario => (scenario.Name, scenario.Size, scenario.Population, years * 120)).ToArray();
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
    var preAdvanceReplenished = 0;
    for (var day = 0; day < startDay; day++)
    {
        preAdvanceReplenished += MaintainPopulation(engine, populationFloor);
        engine.Step();
    }
    var measureStartTick = engine.State.Tick;
    var measuredInitialPopulation = engine.State.Population;
    var initial = engine.ExportJson();
    // 动态 PGO 在后台优化热点，短窗口也至少预热 128 日，避免首轮混入编译开销。
    var warmupWindow = years > 0 ? 256 : scenario.Days;
    var warmupRepetitions = years > 0 ? 1 : Math.Max(2, (128 + scenario.Days - 1) / scenario.Days);
    var warmupDays = warmupRepetitions * warmupWindow;
    for (var warmup = 0; warmup < warmupRepetitions; warmup++)
    {
        engine = WorldEngine.ImportJson(initial);
        for (var day = 0; day < warmupWindow; day++)
        {
            MaintainPopulation(engine, populationFloor);
            engine.Step();
        }
    }
    Console.WriteLine($"Warmup {scenario.Name}: {warmupDays} days, initial population {scenario.Population}");
    var times = new List<double>();
    var dailyTimes = new List<double[]>();
    var dailySamples = new List<DailyMeasurement[]>();
    var annualResults = new List<object>();
    var replenishedResidents = new List<int>();
    var allocations = new List<long>();
    var collections = new List<int[]>();
    string? checksum = null;
    var finalPopulation = 0;
    for (var repetition = 0; repetition < repetitions; repetition++)
    {
        engine = WorldEngine.ImportJson(initial);
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        var days = new double[scenario.Days];
        var samples = new DailyMeasurement[scenario.Days];
        var replenished = 0;
        var gen0 = GC.CollectionCount(0);
        var gen1 = GC.CollectionCount(1);
        var gen2 = GC.CollectionCount(2);
        for (var day = 0; day < scenario.Days; day++)
        {
            replenished += MaintainPopulation(engine, populationFloor);
            var population = engine.State.Population;
            var dayGen0 = GC.CollectionCount(0);
            var dayGen1 = GC.CollectionCount(1);
            var dayGen2 = GC.CollectionCount(2);
            var cpuBefore = BenchmarkClock.ReadProcessCpu();
            var dayAllocated = GC.GetTotalAllocatedBytes(precise: true);
            var started = Stopwatch.GetTimestamp();
            engine.Step();
            days[day] = Stopwatch.GetElapsedTime(started).TotalMilliseconds;
            var cpuAfter = BenchmarkClock.ReadProcessCpu();
            var allocated = GC.GetTotalAllocatedBytes(precise: true) - dayAllocated;
            var g0 = GC.CollectionCount(0) - dayGen0;
            var g1 = GC.CollectionCount(1) - dayGen1;
            var g2 = GC.CollectionCount(2) - dayGen2;
            var pause = g0 + g1 + g2 > 0 ? GC.GetGCMemoryInfo().PauseDurations.ToArray().Sum(duration => duration.TotalMilliseconds) : 0;
            samples[day] = new(engine.State.Tick, population, engine.State.Population, days[day],
                (cpuAfter - cpuBefore) / 1_000_000d, allocated, g0, g1, g2, pause);
            if (years > 0 && (day + 1) % 120 == 0)
            {
                var yearDays = days.AsSpan(day - 119, 120).ToArray();
                var yearSamples = samples.AsSpan(day - 119, 120).ToArray();
                annualResults.Add(new
                {
                    Repetition = repetition, Year = (day + 1) / 120, EndTick = engine.State.Tick,
                    MeanMsPerDay = yearDays.Average(), P95MsPerDay = Percentile(yearDays, .95), MaxMsPerDay = yearDays.Max(),
                    MinPopulation = yearSamples.Min(sample => sample.StartPopulation),
                    MaxPopulation = yearSamples.Max(sample => sample.StartPopulation), EndPopulation = engine.State.Population,
                });
                if ((day + 1) % 1200 == 0)
                {
                    Console.WriteLine($"{scenario.Name} {repetition}: year {(day + 1) / 120}, P95 {Percentile(yearDays, .95):F3}, population {engine.State.Population}, replenished {replenished}");
                    File.WriteAllText(args[0] + ".progress.json", JsonSerializer.Serialize(annualResults));
                }
            }
        }
        times.Add(days.Average());
        dailyTimes.Add(days);
        dailySamples.Add(samples);
        replenishedResidents.Add(replenished);
        allocations.Add(samples.Sum(sample => sample.AllocatedBytes) / scenario.Days);
        collections.Add([GC.CollectionCount(0) - gen0, GC.CollectionCount(1) - gen1, GC.CollectionCount(2) - gen2]);
        var saved = engine.ExportJson();
        var observed = Convert.ToHexString(SHA256.HashData(CanonicalState(saved)));
        if (options.Contains("--save-final"))
            File.WriteAllText(args[0] + "." + scenario.Name + "." + repetition + ".final.json", saved);
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
        WarmupDays = warmupDays, Repetitions = repetitions, PopulationFloor = populationFloor,
        PreAdvanceReplenished = preAdvanceReplenished,
        ReplenishmentOutsideStepTiming = true, ReplenishedResidents = replenishedResidents,
        Rules = engine.State.Rules, engine.State.NaturalDisasters, engine.State.Society.MagicEnabled,
        CoreAssemblySha256 = coreSha256, CoreModuleVersionId = core.ManifestModule.ModuleVersionId,
        TieredCompilation = Environment.GetEnvironmentVariable("DOTNET_TieredCompilation"),
        TieredPGO = Environment.GetEnvironmentVariable("DOTNET_TieredPGO"),
        ServerGarbageCollection = System.Runtime.GCSettings.IsServerGC,
        GcLatencyMode = System.Runtime.GCSettings.LatencyMode.ToString(),
        CpuClock = OperatingSystem.IsLinux() ? "Linux CLOCK_PROCESS_CPUTIME_ID" : null, AllocationScope = "All managed threads",
        MeanMsPerDay = times.Average(), TimesMsPerDay = times, DailyTimesMs = dailyTimes,
        DailyMeasurements = dailySamples, AnnualResults = annualResults,
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

// 压力场景只通过正常居民生成入口补充成年居民，不修改存活者、库存、需求或世界规则。
static int MaintainPopulation(WorldEngine engine, int floor)
{
    var added = 0;
    while (engine.State.Population < floor)
    {
        var state = engine.State;
        var town = state.Settlements.OrderBy(town => town.Population).FirstOrDefault();
        var x = town?.X ?? -1;
        var y = town?.Y ?? -1;
        var race = town is null ? RaceKind.Human : state.Nations.First(nation => nation.Id == town.NationId).FoundingRace;
        if (town is null)
        {
            var site = Enumerable.Range(0, state.Tiles.Count).First(index => state.Tiles[index].IsWalkable && state.Tiles[index].FireTicks == 0);
            x = site % state.Width;
            y = site / state.Width;
        }
        engine.SpawnResidents(x, y, race, Math.Min(200, floor - state.Population));
        var increase = engine.State.Population - state.Population;
        if (increase <= 0) throw new InvalidOperationException("无法在实际可通行区域补足压力场景的人口。");
        added += increase;
    }
    return added;
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
