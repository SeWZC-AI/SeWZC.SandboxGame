using System.Diagnostics;
using System.Runtime;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text.Json;
using SeWZC.WorldBox.Core;

const int daysPerYear = SimulationTime.DaysPerYear;
const int bytesPerMegabyte = 1_000_000;
(string Name, int Size, int Population, int Days)[] cases =
[
    ("64x64-144", 64, 144, 36),
    ("128x128-1024", 128, 1024, 24),
    ("256x256-4096", 256, 4096, 12),
];
var options = args.Skip(1).ToHashSet(StringComparer.Ordinal);
if (args.Length < 1 || options.Any(option =>
                        option is not ("--audit" or "--verify" or "--initialization" or "--default-rules" or "--large"
                            or "--steady" or "--save-final")
                        && !option.StartsWith("--seed=", StringComparison.Ordinal) &&
                        !option.StartsWith("--start-day=", StringComparison.Ordinal)
                        && !option.StartsWith("--years=", StringComparison.Ordinal) &&
                        !option.StartsWith("--repetitions=", StringComparison.Ordinal)
                        && !option.StartsWith("--size=", StringComparison.Ordinal) &&
                        !option.StartsWith("--population=", StringComparison.Ordinal)
                        && !option.StartsWith("--population-floor=", StringComparison.Ordinal))
                    || (options.Contains("--verify") && options.Any(option => option != "--verify"
                                                                              && !option.StartsWith("--size=",
                                                                                  StringComparison.Ordinal) &&
                                                                              !option.StartsWith("--population=",
                                                                                  StringComparison.Ordinal)
                                                                              && !option.StartsWith("--seed=",
                                                                                  StringComparison.Ordinal)))
                    || (options.Contains("--audit") && options.Any(option => option != "--audit"
                                                                             && !option.StartsWith("--seed=",
                                                                                 StringComparison.Ordinal) &&
                                                                             !option.StartsWith("--years=",
                                                                                 StringComparison.Ordinal)
                                                                             && !option.StartsWith("--size=",
                                                                                 StringComparison.Ordinal) &&
                                                                             !option.StartsWith("--population=",
                                                                                 StringComparison.Ordinal)))
                    || (options.Contains("--initialization") && options.Any(option =>
                        option is not ("--initialization" or "--large")
                        && !option.StartsWith("--seed=", StringComparison.Ordinal) &&
                        !option.StartsWith("--repetitions=", StringComparison.Ordinal))))
    throw new ArgumentException(
        "请指定结果 JSON 路径；--audit 检查指定年数（默认 100 年），可搭配 --seed、--years 及自定义场景；--initialization 测量创建地图并补足人口，--default-rules 使用默认规则，--large 只测 256² / 4096 人，--size=整数 --population=整数 选择自定义场景，--steady 测量后续 64 日；--seed=整数 指定种子，--start-day=整数 指定计时前推进日数；--years=整数 测量长期演化，--repetitions=整数 指定轮数，--population-floor=整数 在计时外补充居民维持人口负载；--save-final 在计时外保存终态，--verify 独立验证续演，可搭配自定义场景和种子。");
var benchmarkSeed = ReadIntegerOption(options, "--seed=", options.Contains("--audit") ? 73921 : 42);
var years = ReadIntegerOption(options, "--years=", 0);
var repetitions = ReadIntegerOption(options, "--repetitions=", years > 0 ? 3 : 7);
var populationFloor = ReadIntegerOption(options, "--population-floor=", 0);
var customSize = ReadIntegerOption(options, "--size=", 0);
var customPopulation = ReadIntegerOption(options, "--population=", 0);
if (customSize != 0 || customPopulation != 0)
{
    if (customSize is < 32 or > 256 || customPopulation is < 144 or > WorldEngine.MaxPopulation
                                    || options.Contains("--large") || options.Contains("--initialization"))
        throw new ArgumentException(
            "自定义场景须同时指定 --size=32..256 和 --population=144..10000，不能搭配 --large 或 --initialization。");
    cases = [($"{customSize}x{customSize}-{customPopulation}", customSize, customPopulation, 24)];
}

if (years is < 0 or > 500 || repetitions is < 1 or > 9 || populationFloor is < 0 or > WorldEngine.MaxPopulation)
    throw new ArgumentOutOfRangeException(nameof(options));
if (populationFloor > 0 && (!options.Contains("--large") || !options.Contains("--default-rules") || years == 0))
    throw new ArgumentException("维持人口负载须搭配 --large --default-rules --years=整数。");
var startDay = ReadIntegerOption(options, "--start-day=", options.Contains("--steady") ? 12 : 0);
if (startDay is < 0 or > 10_000)
    throw new ArgumentOutOfRangeException(nameof(startDay));
if (options.Contains("--large"))
    cases = cases.Where(scenario => scenario.Size == 256).ToArray();
if (options.Contains("--steady"))
    cases = cases.Select(scenario => (scenario.Name, scenario.Size, scenario.Population, 64)).ToArray();
if (years > 0)
    cases = cases.Select(scenario => (scenario.Name, scenario.Size, scenario.Population, years * daysPerYear))
        .ToArray();
Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(args[0]))!);
var core = typeof(WorldEngine).Assembly;
var coreSha256 = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(core.Location)));
var benchmarkSha256 =
    Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(typeof(BenchmarkStatistics).Assembly.Location)));
var results = new List<object>();
if (options.Contains("--audit"))
{
    SimulationAudit.Run(args[0], customSize > 0
        ? CreateScenario(customSize, customPopulation, benchmarkSeed)
        : WorldEngine.Create(benchmarkSeed), years == 0 ? 100 : years);
    return;
}

if (options.Contains("--verify"))
{
    // 完整规则的长程行为检查独立运行，不计入性能样本或单元测试。
    foreach (var seed in options.Any(option => option.StartsWith("--seed=", StringComparison.Ordinal))
                 ? new[] { benchmarkSeed }
                 : new[] { 42, 731 })
    foreach (var size in customSize > 0 ? new[] { customSize } : new[] { 64, 128 })
    {
        var population = customPopulation > 0 ? customPopulation : 144;
        var engine = CreateScenario(size, population, seed);
        var rules = engine.State.Rules;
        engine.Step(120 * SimulationTime.TicksPerDay);
        var restored = WorldEngine.ImportJson(engine.ExportJson());
        engine.Step(120 * SimulationTime.TicksPerDay);
        restored.Step(120 * SimulationTime.TicksPerDay);
        var saved = engine.ExportJson();
        var state = CanonicalState(saved);
        if (!state.AsSpan().SequenceEqual(CanonicalState(restored.ExportJson())))
            throw new InvalidOperationException("默认规则下中途保存恢复后的续演结果不同。");
        results.Add(new
        {
            Seed = seed,
            Size = size,
            Days = 240,
            InitialPopulation = population,
            FinalPopulation = engine.State.Population,
            Rules = rules,
            NaturalDisasters = true,
            CoreAssemblySha256 = coreSha256,
            StateSha256 = Convert.ToHexString(SHA256.HashData(state)),
            SaveContinuationVerified = true,
            engine.State.FormatVersion,
            engine.State.SimulationVersion,
        });
        Console.WriteLine($"Verified seed {seed}, {size}x{size}, 240 days, population {engine.State.Population}");
    }

    File.WriteAllText(args[0], JsonSerializer.Serialize(results, new JsonSerializerOptions { WriteIndented = true }));
    return;
}

foreach (var scenario in cases)
{
    if (options.Contains("--initialization"))
    {
        for (var warmup = 0; warmup < 4; warmup++)
            CreateScenario(scenario.Size, scenario.Population, benchmarkSeed);
        var samples = new List<object>();
        string? creationChecksum = null;
        for (var repetition = 0; repetition < repetitions; repetition++)
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
            // 创建地图和生成居民同步执行，不将后台编译线程的分配混入初始化样本。
            var allocatedBefore = GC.GetAllocatedBytesForCurrentThread();
            var started = Stopwatch.GetTimestamp();
            var created = CreateScenario(scenario.Size, scenario.Population, benchmarkSeed);
            var elapsed = Stopwatch.GetElapsedTime(started).TotalMilliseconds;
            var allocated = GC.GetAllocatedBytesForCurrentThread() - allocatedBefore;
            var saved = CanonicalState(created.ExportJson());
            var observed = Convert.ToHexString(SHA256.HashData(saved));
            if (creationChecksum is not null && creationChecksum != observed)
                throw new InvalidOperationException("相同种子的重复初始化产生了不同结果。");
            creationChecksum = observed;
            if (!saved.AsSpan()
                    .SequenceEqual(CanonicalState(WorldEngine.ImportJson(created.ExportJson()).ExportJson())))
                throw new InvalidOperationException("初始化状态保存恢复后的结果不同。");
            samples.Add(new { ElapsedMs = elapsed, AllocatedBytes = allocated });
            Console.WriteLine($"初始化 {scenario.Name} 第 {repetition + 1} 轮：{elapsed:F3} 毫秒，{allocated} 字节");
        }

        results.Add(new
        {
            scenario.Name,
            scenario.Size,
            scenario.Population,
            Seed = benchmarkSeed,
            WarmupCreations = 4,
            Repetitions = repetitions,
            Samples = samples,
            CoreAssemblySha256 = coreSha256,
            BenchmarkAssemblySha256 = benchmarkSha256,
            StateSha256 = creationChecksum,
            SaveRoundtripVerified = true,
            AllocationScope = "Current managed thread",
            Runtime = RuntimeInformation.FrameworkDescription,
            OperatingSystem = RuntimeInformation.OSDescription,
            Architecture = RuntimeInformation.ProcessArchitecture.ToString(),
            ServerGarbageCollection = GCSettings.IsServerGC,
            GcLatencyMode = GCSettings.LatencyMode.ToString(),
            GcConfiguration = GC.GetConfigurationVariables(),
            TieredCompilation = Environment.GetEnvironmentVariable("DOTNET_TieredCompilation"),
            TieredPGO = Environment.GetEnvironmentVariable("DOTNET_TieredPGO"),
            LogicalProcessors = Environment.ProcessorCount,
            CpuQuota = File.Exists("/sys/fs/cgroup/cpu.max") ? File.ReadAllText("/sys/fs/cgroup/cpu.max").Trim() : null,
            MemoryLimit = File.Exists("/sys/fs/cgroup/memory.max")
                ? File.ReadAllText("/sys/fs/cgroup/memory.max").Trim()
                : null,
        });
        continue;
    }

    var engine = CreateScenario(scenario.Size, scenario.Population, benchmarkSeed);
    if (!options.Contains("--default-rules"))
    {
        engine.ConfigureWorld(engine.State.Rules with
        {
            Births = false,
            Aging = false,
            Hunger = false,
            Thirst = false,
            Disease = false,
            Wars = false,
            Secession = false,
        }, false, true);
    }

    var preAdvanceReplenished = 0;
    for (var day = 0; day < startDay; day++)
    {
        preAdvanceReplenished += MaintainPopulation(engine, populationFloor);
        engine.Step(SimulationTime.TicksPerDay);
    }

    var measureStartTick = engine.State.Tick;
    var measuredInitialPopulation = engine.State.Population;
    var initialCivilization = CaptureCivilization(engine.State);
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
            engine.Step(SimulationTime.TicksPerDay);
        }
    }

    Console.WriteLine($"Warmup {scenario.Name}: {warmupDays} days, initial population {scenario.Population}");
    var times = new List<double>();
    var dailyTimes = new List<double[]>();
    var dailySamples = new List<DailyMeasurement[]>();
    var annualResults = new List<object>();
    var repetitionResults = new List<object>();
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
        for (var day = 0; day < scenario.Days; day++)
        {
            replenished += MaintainPopulation(engine, populationFloor);
            var population = engine.State.Population;
            var dayGen0 = GC.CollectionCount(0);
            var dayGen1 = GC.CollectionCount(1);
            var dayGen2 = GC.CollectionCount(2);
            var dayAllocated = GC.GetTotalAllocatedBytes(true);
            var cpuBefore = BenchmarkClock.ReadProcessCpu();
            var started = Stopwatch.GetTimestamp();
            engine.Step(SimulationTime.TicksPerDay);
            days[day] = Stopwatch.GetElapsedTime(started).TotalMilliseconds;
            var cpuAfter = BenchmarkClock.ReadProcessCpu();
            var allocated = GC.GetTotalAllocatedBytes(true) - dayAllocated;
            if (allocated < 0)
                throw new InvalidOperationException(
                    $"GC allocation counter decreased at tick {engine.State.Tick}. Disable DATAS with DOTNET_GCDynamicAdaptationMode=0 on runtimes affected by dotnet/runtime#131069.");
            var g0 = GC.CollectionCount(0) - dayGen0;
            var g1 = GC.CollectionCount(1) - dayGen1;
            var g2 = GC.CollectionCount(2) - dayGen2;
            var pause = g0 + g1 + g2 > 0
                ? GC.GetGCMemoryInfo().PauseDurations.ToArray().Sum(duration => duration.TotalMilliseconds)
                : 0;
            samples[day] = new DailyMeasurement(engine.State.Tick, population, engine.State.Population, days[day],
                (cpuAfter - cpuBefore) / 1_000_000d, allocated, g0, g1, g2, pause);
            if (years > 0 && (day + 1) % daysPerYear == 0)
            {
                var yearSamples = samples.AsSpan(day + 1 - daysPerYear, daysPerYear).ToArray();
                var yearTiming = BenchmarkStatistics.From(yearSamples.Select(sample => sample.WallMs));
                var yearAllocation =
                    BenchmarkStatistics.From(yearSamples.Select(sample =>
                        sample.AllocatedBytes / (double)bytesPerMegabyte));
                annualResults.Add(new
                {
                    Repetition = repetition,
                    Year = (day + 1) / daysPerYear,
                    StartTick = yearSamples[0].Tick - SimulationTime.TicksPerDay,
                    EndTick = engine.State.Tick,
                    SampleCount = yearSamples.Length,
                    MeanMsPerDay = yearTiming.Mean,
                    P95MsPerDay = yearTiming.P95,
                    MaxMsPerDay = yearTiming.Max,
                    MeanAllocatedMBPerDay = yearAllocation.Mean,
                    P95AllocatedMBPerDay = yearAllocation.P95,
                    MaxAllocatedMBPerDay = yearAllocation.Max,
                    GcCollections =
                        new[]
                        {
                            yearSamples.Sum(sample => sample.Gen0), yearSamples.Sum(sample => sample.Gen1),
                            yearSamples.Sum(sample => sample.Gen2),
                        },
                    MinPopulation = yearSamples.Min(sample => Math.Min(sample.StartPopulation, sample.EndPopulation)),
                    MaxPopulation = yearSamples.Max(sample => Math.Max(sample.StartPopulation, sample.EndPopulation)),
                    EndPopulation = engine.State.Population,
                    EndSettlements = engine.State.Settlements.Count,
                    EndBuildings = engine.State.Society.Buildings.Count,
                    EndArmies = engine.State.Armies.Count,
                    Civilization = CaptureCivilization(engine.State),
                });
                if ((day + 1) % (10 * daysPerYear) == 0)
                {
                    Console.WriteLine(
                        $"{scenario.Name} {repetition}: year {(day + 1) / daysPerYear}, time ms/day mean/P95/max {yearTiming.Mean:F3}/{yearTiming.P95:F3}/{yearTiming.Max:F3}, allocation MB/day mean/P95/max {yearAllocation.Mean:F3}/{yearAllocation.P95:F3}/{yearAllocation.Max:F3}, population {engine.State.Population}, replenished {replenished}");
                    File.WriteAllText(args[0] + ".progress.json", JsonSerializer.Serialize(annualResults));
                }
            }
        }

        var timing = BenchmarkStatistics.From(days);
        var allocation =
            BenchmarkStatistics.From(samples.Select(sample => sample.AllocatedBytes / (double)bytesPerMegabyte));
        times.Add(timing.Mean);
        dailyTimes.Add(days);
        dailySamples.Add(samples);
        replenishedResidents.Add(replenished);
        allocations.Add(samples.Sum(sample => sample.AllocatedBytes) / scenario.Days);
        collections.Add([
            samples.Sum(sample => sample.Gen0), samples.Sum(sample => sample.Gen1), samples.Sum(sample => sample.Gen2),
        ]);
        repetitionResults.Add(new
        {
            Repetition = repetition,
            StartTick = measureStartTick,
            EndTick = engine.State.Tick,
            SampleCount = samples.Length,
            MeanMsPerDay = timing.Mean,
            P95MsPerDay = timing.P95,
            MaxMsPerDay = timing.Max,
            MeanAllocatedMBPerDay = allocation.Mean,
            P95AllocatedMBPerDay = allocation.P95,
            MaxAllocatedMBPerDay = allocation.Max,
            GcCollections = collections[^1],
            ReplenishedResidents = replenished,
            MinPopulation = samples.Min(sample => Math.Min(sample.StartPopulation, sample.EndPopulation)),
            MaxPopulation = samples.Max(sample => Math.Max(sample.StartPopulation, sample.EndPopulation)),
            EndPopulation = engine.State.Population,
            EndSettlements = engine.State.Settlements.Count,
            EndBuildings = engine.State.Society.Buildings.Count,
            EndArmies = engine.State.Armies.Count,
        });
        var saved = engine.ExportJson();
        var observed = Convert.ToHexString(SHA256.HashData(CanonicalState(saved)));
        if (options.Contains("--save-final"))
            File.WriteAllText(args[0] + "." + scenario.Name + "." + repetition + ".final.json", saved);
        if (checksum is not null && checksum != observed)
            throw new InvalidOperationException("相同初态的重复模拟产生了不同结果。");
        checksum = observed;
        finalPopulation = engine.State.Population;
        Console.WriteLine(
            $"{scenario.Name} {repetition}: time ms/day mean/P95/max {timing.Mean:F3}/{timing.P95:F3}/{timing.Max:F3}, allocation MB/day mean/P95/max {allocation.Mean:F3}/{allocation.P95:F3}/{allocation.Max:F3}");
    }

    var allSamples = dailySamples.SelectMany(samples => samples).ToArray();
    var totalTiming = BenchmarkStatistics.From(allSamples.Select(sample => sample.WallMs));
    var totalAllocation =
        BenchmarkStatistics.From(allSamples.Select(sample => sample.AllocatedBytes / (double)bytesPerMegabyte));
    // 各次终态已逐一比较；同一终态的保存续演只需在计时外验证一次。
    var finalCivilization = CaptureCivilization(engine.State);
    var restored = WorldEngine.ImportJson(engine.ExportJson());
    engine.Step(SimulationTime.TicksPerDay);
    restored.Step(SimulationTime.TicksPerDay);
    if (!CanonicalState(engine.ExportJson()).AsSpan().SequenceEqual(CanonicalState(restored.ExportJson())))
        throw new InvalidOperationException("中途保存恢复后的续演结果不同。");
    results.Add(new
    {
        scenario.Name,
        scenario.Size,
        InitialPopulation = measuredInitialPopulation,
        RequestedPopulation = scenario.Population,
        Seed = benchmarkSeed,
        scenario.Days,
        MeasureStartTick = measureStartTick,
        MeasureEndTick = measureStartTick + scenario.Days * SimulationTime.TicksPerDay,
        SimulatedYears = scenario.Days / (double)daysPerYear,
        DaysPerYear = daysPerYear,
        SampleCount = allSamples.Length,
        WarmupDays = warmupDays,
        Repetitions = repetitions,
        PopulationFloor = populationFloor,
        PreAdvanceReplenished = preAdvanceReplenished,
        ReplenishmentOutsideStepTiming = true,
        ReplenishedResidents = replenishedResidents,
        engine.State.Rules,
        engine.State.NaturalDisasters,
        engine.State.Society.MagicEnabled,
        CoreAssemblySha256 = coreSha256,
        CoreModuleVersionId = core.ManifestModule.ModuleVersionId,
        BenchmarkAssemblySha256 = benchmarkSha256,
        TieredCompilation = Environment.GetEnvironmentVariable("DOTNET_TieredCompilation"),
        TieredPGO = Environment.GetEnvironmentVariable("DOTNET_TieredPGO"),
        ServerGarbageCollection = GCSettings.IsServerGC,
        GcLatencyMode = GCSettings.LatencyMode.ToString(),
        GcConfiguration = GC.GetConfigurationVariables(),
        CpuClock = OperatingSystem.IsLinux() ? "Linux CLOCK_PROCESS_CPUTIME_ID" : null,
        AllocationScope = "All managed threads",
        AllocationBytesPerMB = bytesPerMegabyte,
        PercentileMethod = "Nearest rank: ceil(sample count * 0.95)",
        MeanMsPerDay = totalTiming.Mean,
        P95MsPerDay = totalTiming.P95,
        MaxMsPerDay = totalTiming.Max,
        MeanAllocatedMBPerDay = totalAllocation.Mean,
        P95AllocatedMBPerDay = totalAllocation.P95,
        MaxAllocatedMBPerDay = totalAllocation.Max,
        TotalStepMs = allSamples.Sum(sample => sample.WallMs),
        TotalAllocatedMB = allSamples.Sum(sample => sample.AllocatedBytes) / (double)bytesPerMegabyte,
        MinPopulation = allSamples.Min(sample => Math.Min(sample.StartPopulation, sample.EndPopulation)),
        MaxPopulation = allSamples.Max(sample => Math.Max(sample.StartPopulation, sample.EndPopulation)),
        TimesMsPerDay = times,
        DailyTimesMs = dailyTimes,
        DailyMeasurements = dailySamples,
        AnnualResults = annualResults,
        RepetitionResults = repetitionResults,
        BytesPerDay = allocations,
        GcCollections = collections,
        FinalPopulation = finalPopulation,
        StateSha256 = checksum,
        InitialCivilization = initialCivilization,
        FinalCivilization = finalCivilization,
        RepeatedSimulationVerified = repetitions > 1,
        SaveContinuationVerified = true,
        engine.State.FormatVersion,
        engine.State.SimulationVersion,
        Runtime = RuntimeInformation.FrameworkDescription,
        Architecture = RuntimeInformation.ProcessArchitecture.ToString(),
        OperatingSystem = RuntimeInformation.OSDescription,
        LogicalProcessors = Environment.ProcessorCount,
        CpuQuota = File.Exists("/sys/fs/cgroup/cpu.max") ? File.ReadAllText("/sys/fs/cgroup/cpu.max").Trim() : null,
        MemoryLimit = File.Exists("/sys/fs/cgroup/memory.max")
            ? File.ReadAllText("/sys/fs/cgroup/memory.max").Trim()
            : null,
    });
    Console.WriteLine(
        $"Summary {scenario.Name}, ticks {measureStartTick}..{measureStartTick + scenario.Days * SimulationTime.TicksPerDay}, {allSamples.Length} samples: time ms/day mean/P95/max {totalTiming.Mean:F3}/{totalTiming.P95:F3}/{totalTiming.Max:F3}, allocation MB/day mean/P95/max {totalAllocation.Mean:F3}/{totalAllocation.P95:F3}/{totalAllocation.Max:F3}");
}

File.WriteAllText(args[0], JsonSerializer.Serialize(results, new JsonSerializerOptions { WriteIndented = true }));

// 年度诊断在 Step 计时外采样，保留各聚落差异，不用单个总分掩盖饥荒或停滞。
static object CaptureCivilization(WorldState state)
{
    return new
    {
        state.Population,
        Hungry = state.Residents.Count(person => person.Hunger > 60),
        Thirsty = state.Residents.Count(person => person.Thirst > 80),
        Sick = state.Residents.Count(person => person.SicknessTicks > 0),
        Children = state.Residents.Count(person => person.Age < 18),
        MeanHealth = state.Residents.Count == 0 ? 0 : state.Residents.Average(person => person.Health),
        CompletedBuildings = state.Society.Buildings.Count(building => building.IsCompleted),
        ProductionBatches = state.Society.Buildings.Sum(building => (long)building.ProductionBatches),
        Settlements = state.Settlements.Select(town => new
        {
            town.Id,
            town.Name,
            town.Population,
            town.Tier,
            town.Resources,
            Research = state.Society.Research.FirstOrDefault(research => research.SettlementId == town.Id)
                ?.Completed.Select(research => research.Id).ToArray(),
            Buildings = state.Society.Buildings.Where(building => building.SettlementId == town.Id)
                .Select(building =>
                    new { building.Kind, building.IsCompleted, building.Health, building.ProductionBatches }).ToArray(),
        }).ToArray(),
    };
}

static WorldEngine CreateScenario(int size, int population, int seed)
{
    var engine = WorldEngine.Create(seed, size, size);
    var towns = engine.State.Settlements.ToArray();
    var extra = population - engine.State.Population;
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

    if (engine.State.Population != population)
        throw new InvalidOperationException("基准世界未达到要求的人口数量。");
    return engine;
}

static int ReadIntegerOption(HashSet<string> options, string prefix, int fallback)
{
    var values = options.Where(option => option.StartsWith(prefix, StringComparison.Ordinal)).ToArray();
    if (values.Length == 0)
        return fallback;
    if (values.Length != 1 || !int.TryParse(values[0].AsSpan(prefix.Length), out var value))
        throw new ArgumentException($"{prefix} 只能指定一次，且必须为整数。");
    return value;
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
        var race = town is null
            ? RaceKind.Human
            : state.Nations.First(nation => nation.Id == town.NationId).FoundingRace;
        if (town is null)
        {
            var site = Enumerable.Range(0, state.Tiles.Count)
                .First(index => state.Tiles[index].IsWalkable && state.Tiles[index].FireTicks == 0);
            x = site % state.Width;
            y = site / state.Width;
        }

        engine.SpawnResidents(x, y, race, Math.Min(200, floor - state.Population));
        var increase = engine.State.Population - state.Population;
        if (increase <= 0)
            throw new InvalidOperationException("无法在实际可通行区域补足压力场景的人口。");
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
    {
        WriteCanonical(writer, document.RootElement);
    }

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
