using System.Text.Json;
using SeWZC.WorldBox.Core;

internal static class EcologyDiagnostics
{
    public static int Run(string[] args)
    {
        var output = args.ElementAtOrDefault(0) ?? "artifacts/ecology.json";
        var days = int.Parse(args.ElementAtOrDefault(1) ?? "6000");
        var runs = new List<object>();
        foreach (var seed in new[] { 42, 73921 })
        foreach (var size in new[] { 128, 256 })
        {
            var e = WorldEngine.Create(seed, size, size, false);
            e.ConfigureWorld(new WorldRules { ResourceRegeneration = false }, false, true);
            var samples = new List<object>();
            void Sample() => samples.Add(new { e.State.Tick, Species = AnimalRules.Species.Select(k => new
            {
                Kind = k.ToString(), Name = WorldEngine.WildlifeName(k),
                Population = e.State.Tiles.Sum(t => t.AnimalPopulation(k)),
                VisibleTiles = e.State.Tiles.Count(t => t.AnimalPopulation(k) >= .25)
            }).ToArray() });
            Sample();
            foreach (var tick in new[] { 120, 1200, days }.Distinct().Order().Where(t => t <= days))
            { e.Step(tick - (int)e.State.Tick); Sample(); }
            runs.Add(new { seed, size, samples });
            Console.WriteLine($"Ecology seed {seed}, {size}×{size}, day {days}");
        }
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(output))!);
        File.WriteAllText(output, JsonSerializer.Serialize(runs, new JsonSerializerOptions { WriteIndented = true }));
        return 0;
    }
}
