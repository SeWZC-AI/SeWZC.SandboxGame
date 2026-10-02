using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using SeWZC.WorldBox.Core;

// Explicit opt-in measurement; run the executable functional suite separately first.
internal static class SimulationPerformance
{
    public static int Run(string[] args, Func<int, bool, WorldEngine> createWorld)
    {
        int Option(string name, int fallback, int minimum, int maximum)
        {
            var index = Array.IndexOf(args, name);
            if (index < 0) return fallback;
            if (index + 1 >= args.Length || !int.TryParse(args[index + 1], out var value)
                || value < minimum || value > maximum)
                throw new ArgumentException($"{name} requires an integer in [{minimum}, {maximum}].");
            return value;
        }

        var population = Option("--population", 2000, 16, WorldEngine.MaxPopulation);
        var warmup = Option("--warmup", 60, 0, 10_000);
        var ticks = Option("--ticks", 120, 1, 10_000);
        var repetitions = Option("--repetitions", 5, 1, 100);
        var wars = !args.Contains("--peace");
        var outputIndex = Array.IndexOf(args, "--output");
        if (outputIndex >= 0 && outputIndex + 1 >= args.Length)
            throw new ArgumentException("--output requires a JSON file path.");

        // Prime JIT paths before starting the independently recreated measurement worlds.
        var priming = createWorld(population, wars);
        priming.Step(warmup);
        priming.Step(ticks);
        priming = null;

        var measurements = new List<object>();
        string? expectedDigest = null;
        for (var repetition = 0; repetition < repetitions; repetition++)
        {
            var engine = createWorld(population, wars);
            engine.Step(warmup);
            var durations = new double[ticks];
            var allocations = new long[ticks];
            var gcBefore = Enumerable.Range(0, 3).Select(GC.CollectionCount).ToArray();
            var total = Stopwatch.GetTimestamp();
            for (var tick = 0; tick < ticks; tick++)
            {
                var allocated = GC.GetAllocatedBytesForCurrentThread();
                var started = Stopwatch.GetTimestamp();
                engine.Step();
                durations[tick] = Stopwatch.GetElapsedTime(started).TotalMilliseconds;
                allocations[tick] = GC.GetAllocatedBytesForCurrentThread() - allocated;
            }
            var elapsed = Stopwatch.GetElapsedTime(total).TotalMilliseconds;
            var collections = Enumerable.Range(0, 3).Select(i => GC.CollectionCount(i) - gcBefore[i]).ToArray();
            var ordered = durations.Order().ToArray();
            double Percentile(double p) => ordered[Math.Clamp((int)Math.Ceiling(p * ticks) - 1, 0, ticks - 1)];

            // Serialization and validation are outside the timed simulation region.
            var save = engine.ExportJson();
            var bytes = Encoding.UTF8.GetBytes(save);
            var digest = Convert.ToHexString(SHA256.HashData(bytes));
            if (expectedDigest is not null && digest != expectedDigest)
                throw new InvalidOperationException("Repeated worlds diverged.");
            expectedDigest = digest;
            if (bytes.Length <= WorldEngine.MaxSaveBytes) _ = WorldEngine.ImportJson(save);
            measurements.Add(new
            {
                repetition, elapsedMs = elapsed, meanMs = durations.Average(),
                medianMs = Percentile(.5), p95Ms = Percentile(.95), maxMs = ordered[^1],
                allocatedBytesPerTick = allocations.Average(), gcCollections = collections,
                finalTick = engine.State.Tick, finalPopulation = engine.State.Population,
                nations = engine.State.Nations.Count, armies = engine.State.Armies.Count,
                rememberedFacts = engine.State.Residents.Sum(r => r.Agent.Memory.Count),
                saveBytes = bytes.Length, saveWithinLimit = bytes.Length <= WorldEngine.MaxSaveBytes,
                saveSha256 = digest, tickMs = durations, tickAllocatedBytes = allocations
            });
            Console.WriteLine($"RUN {repetition + 1}/{repetitions}: {durations.Average():F3} ms/tick, " +
                $"p95 {Percentile(.95):F3} ms, max {ordered[^1]:F3} ms; " +
                $"{allocations.Average() / 1024:F1} KiB/tick; final population {engine.State.Population}.");
        }

        var report = JsonSerializer.Serialize(new
        {
            timestampUtc = DateTimeOffset.UtcNow, framework = RuntimeInformation.FrameworkDescription,
            os = RuntimeInformation.OSDescription, architecture = RuntimeInformation.ProcessArchitecture.ToString(),
            processorCount = Environment.ProcessorCount, serverGc = System.Runtime.GCSettings.IsServerGC,
            seed = 451, width = 256, height = 256, initialPopulation = population, initialNations = 16,
            wars = wars ? 8 : 0, terrain = "flat grass", naturalDisasters = false,
            warmupTicks = warmup, measuredTicks = ticks, repetitions,
            limitation = "Native simulation only. Timings exclude generation, warmup, serialization, UI and browser execution. Sampling adds overhead; use a separate untraced run for timing.",
            measurements
        }, new JsonSerializerOptions { WriteIndented = true });
        if (outputIndex >= 0)
        {
            var output = Path.GetFullPath(args[outputIndex + 1]);
            Directory.CreateDirectory(Path.GetDirectoryName(output)!);
            File.WriteAllText(output, report, new UTF8Encoding(false));
            Console.WriteLine($"REPORT {output}");
        }
        else Console.WriteLine(report);
        return 0;
    }
}
