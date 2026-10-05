using System.Diagnostics;
using System.Text;
using System.Text.Json;
using SeWZC.WorldBox.Core;

internal static class DevelopmentDiagnostics
{
    // Runs the actual core without UI timing or edits. Snapshots deliberately copy mutable values.
    public static int Run(string[] args)
    {
        var positional = args.Where(a =>
            a is not ("--complete-agenda" or "--no-disasters" or "--technology" or "--magic-practice"
                or "--arcane-industry" or "--peaceful" or "--require-empire" or "--until-empire")).ToArray();
        if (positional.Length is < 1 or > 4)
        {
            Console.Error.WriteLine(
                "Usage: --simulate-development <output-directory> [seed=73921] [size=256] [ticks=3600] [--no-disasters] [--peaceful] [--technology|--magic-practice|--arcane-industry] [--require-empire] [--until-empire] [--complete-agenda]");
            return 2;
        }

        var seed = positional.Length > 1 ? int.Parse(positional[1]) : 73921;
        var size = positional.Length > 2 ? int.Parse(positional[2]) : 256;
        var ticks = positional.Length > 3 ? int.Parse(positional[3]) : 3600;
        if (ticks is < 1 or > 100_000) throw new ArgumentOutOfRangeException(nameof(ticks));
        var output = Path.GetFullPath(positional[0]);
        Directory.CreateDirectory(output);
        var engine = WorldEngine.Create(seed, size, size);
        engine.State.NaturalDisasters = !args.Contains("--no-disasters");
        if (args.Contains("--peaceful")) engine.ConfigureWorld(WorldRules.For(WorldPreset.Flourishing), false, true);
        if (args.Contains("--technology") || args.Contains("--magic-practice") || args.Contains("--arcane-industry"))
            foreach (var nation in engine.State.Nations)
                engine.SetDevelopmentFocus(nation.Id,
                    args.Contains("--technology") ? DevelopmentFocus.Technology :
                    args.Contains("--arcane-industry") ? DevelopmentFocus.ArcaneIndustry :
                    DevelopmentFocus.MagicPractice);
        var samples = new List<object>();
        var milestones = new Dictionary<int, long>();
        var observedEvents = new Dictionary<int, WorldEvent>();
        var observedDeaths = new Dictionary<int, Resident>();
        var targetRoute = ResearchRules.Route(args.Contains("--arcane-industry"));
        long completionTick = -1;

        int[] CompleteTowns(WorldEngine current)
        {
            return current.State.Settlements
                .Where(t => current.GetCivilizationProgress(t.Id, args.Contains("--arcane-industry")).Achieved)
                .Select(t => t.Id).ToArray();
        }

        var elapsed = Stopwatch.StartNew();
        Sample();
        for (var completed = 0; completed < ticks; completed += 120)
        {
            // Explicit all-research experiment supplies a paid agenda, never free knowledge/resources.
            if (args.Contains("--complete-agenda"))
            {
                foreach (var town in engine.State.Settlements)
                {
                    var project = engine.State.Society.Research.First(r => r.SettlementId == town.Id);
                    if (project.ActiveProject.HasValue || !engine.State.Society.Buildings.Any(b =>
                            b.SettlementId == town.Id && b.Kind == BuildingKind.Academy && b.IsCompleted)) continue;
                    var candidate = targetRoute.Where(k => !engine.HasResearch(town.Id, k) &&
                                                           engine.ResearchPrerequisiteError(town.Id, k) is null
                                                           && WorldEngine.MissingResources(town.Resources,
                                                               WorldEngine.GetResearchCost(k)) is null)
                        .Select(k => (ResearchKind?)k).FirstOrDefault();
                    if (candidate.HasValue) engine.StartResearch(town.Id, candidate.Value);
                }
            }

            engine.Step(Math.Min(120, ticks - completed));
            Sample();
            if (completionTick < 0 && CompleteTowns(engine).Length > 0) completionTick = engine.State.Tick;
            if (args.Contains("--until-empire") && completionTick >= 0 &&
                engine.State.Tick >= completionTick + 1200) break;
        }

        var save = engine.ExportJson();
        try
        {
            _ = WorldEngine.ImportJson(save);
        }
        catch
        {
            File.WriteAllText(Path.Combine(output, "invalid.worldbox.json"), save, new UTF8Encoding(false));
            throw;
        }

        var resumed = WorldEngine.ImportJson(save);
        engine.Step(24);
        resumed.Step(24);
        if (engine.ExportJson() != resumed.ExportJson())
            throw new Exception("Final save failed deterministic continuation.");
        File.WriteAllText(Path.Combine(output, "final.worldbox.json"), save, new UTF8Encoding(false));
        var completeTowns = CompleteTowns(resumed);
        File.WriteAllText(Path.Combine(output, "report.json"), JsonSerializer.Serialize(new
        {
            seed, size, ticks, completeResearchAgenda = args.Contains("--complete-agenda"),
            simulatedTicks = engine.State.Tick - 24, completionTick, milestones, completeTowns,
            deterministicContinuationTicks = 24, requiredResearch = targetRoute.Select(k => k.ToString()).ToArray(),
            disasters = engine.State.NaturalDisasters, elapsedSeconds = elapsed.Elapsed.TotalSeconds,
            saveBytes = Encoding.UTF8.GetByteCount(save), samples, observedEvents = observedEvents.Values,
            deaths = observedDeaths.Values.Select(r => new
                { r.Id, cause = r.DeathCause.ToString(), r.DeathTick, r.SettlementId, r.X, r.Y }),
        }, new JsonSerializerOptions { WriteIndented = true }), new UTF8Encoding(false));
        Console.WriteLine($"Report and validated final save: {output}");
        return args.Contains("--require-empire") && completeTowns.Length == 0 ? 1 : 0;

        void Sample()
        {
            var state = engine.State;
            foreach (var entry in state.Events) observedEvents.TryAdd(entry.Id, entry);
            foreach (var resident in state.ArchivedResidents) observedDeaths.TryAdd(resident.Id, resident);
            var towns = state.Settlements.Select(town =>
            {
                var people = state.Residents.Where(p => p.SettlementId == town.Id).ToArray();
                var research = state.Society.Research.First(r => r.SettlementId == town.Id);
                if (engine.GetCivilizationProgress(town.Id, args.Contains("--arcane-industry")).Achieved)
                    milestones.TryAdd(town.Id, state.Tick);
                return new
                {
                    town.Id, town.Name, town.NationId, focus = engine.GetDevelopmentFocus(town.Id).ToString(), town.X,
                    town.Y, town.Population,
                    housing = engine.GetHousingCapacity(town.Id), tier = WorldEngine.SettlementTierName(town.Tier),
                    exclusiveLand = engine.GetSettlementArea(town.Id),
                    stock = town.Resources.Copy(), town.DevelopmentGoal, town.DevelopmentBlocker,
                    civilization = engine.GetCivilizationProgress(town.Id, args.Contains("--arcane-industry")),
                    policy = engine.GetLocalPolicy(town.Id).ToString(),
                    hunger = people.Select(p => p.Hunger).DefaultIfEmpty().Average(),
                    thirst = people.Select(p => p.Thirst).DefaultIfEmpty().Average(),
                    health = people.Select(p => p.Health).DefaultIfEmpty().Average(),
                    fatigue = people.Select(p => p.Agent.Fatigue).DefaultIfEmpty().Average(),
                    children = people.Count(p => p.Age < 14),
                    foodCarried = people.Sum(p => p.Inventory.Food),
                    goals = people.GroupBy(p => p.Agent.Goal.Kind).ToDictionary(g => g.Key.ToString(), g => g.Count()),
                    professions = people.Where(p => p.Age >= 14).GroupBy(p => p.Profession)
                        .ToDictionary(g => g.Key.ToString(), g => g.Count()),
                    research = new
                    {
                        active = research.ActiveProject?.ToString(), research.Progress,
                        completed = research.Completed.Select(k => k.ToString()).ToArray(),
                    },
                    buildings = state.Society.Buildings.Where(b => b.SettlementId == town.Id).Select(b => new
                    {
                        kind = b.Kind.ToString(), b.X, b.Y, b.ConstructionProgress, b.ConstructionRequired,
                        b.LastWorkedTick, b.ProductionBatches, b.ServiceActions,
                    }).ToArray(),
                };
            }).ToArray();
            samples.Add(new
            {
                state.Tick, state.Population, nations = state.Nations.Count,
                settlements = state.Settlements.Count, territory = state.Nations.Sum(n => n.Territory), towns,
            });
            Console.WriteLine($"tick={state.Tick} population={state.Population} settlements={towns.Length} " +
                              $"buildings={state.Society.Buildings.Count} research={state.Society.Research.Sum(r => r.Completed.Count)} " +
                              $"food={state.Settlements.Sum(t => t.Resources.Food):F1} wood={state.Settlements.Sum(t => t.Resources.Wood):F1} " +
                              $"stone={state.Settlements.Sum(t => t.Resources.Stone):F1} hungry={state.Residents.Count(p => p.Hunger > 60)} " +
                              $"thirsty={state.Residents.Count(p => p.Thirst > 80)} deaths={observedDeaths.Count}");
        }
    }
}
