using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using SeWZC.WorldBox.Core;

internal static class SavePerformance
{
    // Opt-in benchmark, separate from functional tests and their timing budget.
    public static int Run(string[] args, Func<WorldEngine> createWorld)
    {
        var engine = createWorld();
        var expected = engine.ExportJson();
        var samples = new List<object>();
        for (var i = -1; i < 3; i++)
        {
            var last = Stopwatch.GetTimestamp();
            var started = last;
            var longest = 0d;
            var yields = 0;
            var allocated = GC.GetTotalAllocatedBytes(true);
            var chunks = engine.ExportJsonChunksAsync(_ =>
            {
                longest = Math.Max(longest, Stopwatch.GetElapsedTime(last).TotalMilliseconds);
                last = Stopwatch.GetTimestamp();
                yields++;
                return ValueTask.CompletedTask;
            }).GetAwaiter().GetResult();
            longest = Math.Max(longest, Stopwatch.GetElapsedTime(last).TotalMilliseconds);
            var elapsed = Stopwatch.GetElapsedTime(started).TotalMilliseconds;
            var allocations = GC.GetTotalAllocatedBytes(true) - allocated;
            if (string.Concat(chunks) != expected)
                throw new Exception("Benchmark save differs from synchronous capture.");
            if (i >= 0)
                samples.Add(new
                    { elapsedMs = elapsed, longestSliceMs = longest, yields, allocatedBytes = allocations });
        }

        var report = JsonSerializer.Serialize(new
        {
            mode = "chunked", runtime = RuntimeInformation.FrameworkDescription, os = RuntimeInformation.OSDescription,
            engine.State.Width, engine.State.Height, engine.State.Population,
            jsonBytes = Encoding.UTF8.GetByteCount(expected), samples,
        }, new JsonSerializerOptions { WriteIndented = true });
        var output = Array.IndexOf(args, "--output");
        if (output >= 0) File.WriteAllText(args[output + 1], report);
        Console.WriteLine(report);
        return 0;
    }
}
