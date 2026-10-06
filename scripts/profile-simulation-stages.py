#!/usr/bin/env python3
"""Instrument a disposable source copy; never modify the production engine.

Run the functional suite first. Build/run the printed project with
--profile-simulation --output <report.json> and the usual measurement options.
--ecology-only bypasses residents/war; --dense-ecology seeds a fully populated
rich forest. These options exist only in the disposable probe copy.
"""
import argparse
import re
from pathlib import Path
import shutil

parser = argparse.ArgumentParser(description=__doc__)
parser.add_argument("destination", type=Path, help="new directory for the instrumented source copy")
args = parser.parse_args()
root = Path(__file__).resolve().parents[1]
destination = args.destination.resolve()
if destination.exists():
    parser.error("destination must not exist; choose a new directory")
if destination.is_relative_to(root / "src") or destination.is_relative_to(root / "tests"):
    parser.error("destination must be outside source and test directories")

driver_source = (root / "tests/SeWZC.WorldBox.Core.Tests/SimulationPerformance.cs").read_text()
for marker in (
    "            var total = Stopwatch.GetTimestamp();",
    "                engine.Step();",
    "            var elapsed = Stopwatch.GetElapsedTime(total).TotalMilliseconds;",
    "                repetition, elapsedMs = elapsed,",
):
    if driver_source.count(marker) != 1:
        raise SystemExit("Measurement driver changed; review probes before running.")

simulation = (root / "src/SeWZC.WorldBox.Core/WorldEngine.Simulation.cs").read_text()
stages = [
    ("Reindex", "Reindex();"),
    ("UpdateDisasters", "UpdateDisasters();"),
    ("TickWildlife", "TickWildlife();"),
    ("TickPlants", "TickPlants();"),
    ("UpdateResidents", "UpdateResidents();"),
    ("UpdateAgentNeedsAndActions", "UpdateAgentNeedsAndActions();"),
    ("UpdateLocalCommunication", "UpdateLocalCommunication();"),
    ("TickSociety", "TickSociety();"),
    ("TickDiplomacy", "TickDiplomacy();"),
    ("TickLocalConflicts", "TickLocalConflicts();"),
    ("TickMigrationAndSecession", "TickMigrationAndSecession();"),
    ("GrowSettlements", "if (State.Tick % 12 == 0) GrowSettlements();"),
    ("RefreshTerritoryClaims", "if (State.Tick % 30 == 0) RefreshTerritoryClaims();"),
    ("UpdateArmies", "UpdateArmies();"),
    ("ArchiveDeadResidents", "ArchiveDeadResidents();"),
    ("RemoveSettlements", 'foreach (var settlement in State.Settlements.Where(s => _citizens[s.Id].Count == 0).ToArray())\n                    RemoveSettlement(settlement, "居民离散，聚落成为遗址");'),
    ("RemoveEmptyNations", "RemoveEmptyNations();"),
    ("ReconcileSocietyTopology", "ReconcileSocietyTopology();"),
    ("RefreshTotals", "RefreshTotals();"),
    ("ObserveProjects", "ObserveProjects();"),
]
start = simulation.index("    public void Step(")
end = simulation.index("    private static void CapResources", start)
step = simulation[start:end]
for name, statement in stages:
    expected = 3 if name == "Reindex" else 1
    if step.count(statement) != expected:
        raise SystemExit(f"Step changed: expected {expected} occurrences of {name}; review probes before running.")
# Separate local names for repeated calls, aggregate them into one stage.
instrumented = step
for index, (name, statement) in enumerate(stages):
    call = 0
    def insert(match):
        global call
        local = f"probe{index}_{call}"
        call += 1
        indent = match.group(1)
        return (f"{indent}var {local} = SimulationStageProbe.Begin();\n" + match.group(0)
                + f"{indent}SimulationStageProbe.End({index}, {local});\n")
    instrumented = re.sub(r"(?m)^([ \t]*)" + re.escape(statement) + r"\n", insert, instrumented)

shutil.copytree(root / "src/SeWZC.WorldBox.Core", destination / "src/SeWZC.WorldBox.Core",
                ignore=shutil.ignore_patterns("bin", "obj"))
shutil.copytree(root / "tests/SeWZC.WorldBox.Core.Tests", destination / "tests/SeWZC.WorldBox.Core.Tests",
                ignore=shutil.ignore_patterns("bin", "obj"))
for name in ("global.json", "Directory.Build.props"):
    shutil.copy2(root / name, destination / name)
(destination / "src/SeWZC.WorldBox.Core/WorldEngine.Simulation.cs").write_text(
    simulation[:start] + instrumented + simulation[end:])

names = ", ".join('"' + name + '"' for name, _ in stages)
probe_source = """using System.Diagnostics;
namespace SeWZC.WorldBox.Core;
public static class SimulationStageProbe
{
    public static bool Enabled;
    private static readonly string[] Names = [NAMES];
    private static readonly long[] Duration = new long[Names.Length];
    private static readonly long[] Allocated = new long[Names.Length];
    private static readonly long[] Calls = new long[Names.Length];
    private static readonly long[] Maximum = new long[Names.Length];
    private static readonly List<long>[] Samples = Names.Select(_ => new List<long>(30_000)).ToArray();
    public static (long Time, long Bytes) Begin() => Enabled
        ? (Stopwatch.GetTimestamp(), GC.GetAllocatedBytesForCurrentThread()) : default;
    public static void End(int stage, (long Time, long Bytes) start)
    {
        if (!Enabled) return;
        var duration = Stopwatch.GetTimestamp() - start.Time;
        Duration[stage] += duration;
        Maximum[stage] = Math.Max(Maximum[stage], duration);
        Samples[stage].Add(duration);
        Allocated[stage] += GC.GetAllocatedBytesForCurrentThread() - start.Bytes;
        Calls[stage]++;
    }
    public static void Reset()
    {
        Array.Clear(Duration); Array.Clear(Allocated); Array.Clear(Calls); Array.Clear(Maximum);
        foreach (var samples in Samples) samples.Clear();
    }
    public static object[] Snapshot() => Names.Select((name, i) => (object)new
    {
        name, elapsedMs = Duration[i] * 1000d / Stopwatch.Frequency,
        maxMs = Maximum[i] * 1000d / Stopwatch.Frequency,
        p95Ms = Samples[i].Count == 0 ? 0 : Samples[i].Order().ElementAt((int)Math.Ceiling(Samples[i].Count * .95) - 1) * 1000d / Stopwatch.Frequency,
        callMs = Samples[i].Select(t => t * 1000d / Stopwatch.Frequency).ToArray(),
        allocatedBytes = Allocated[i], calls = Calls[i]
    }).ToArray();
}
""".replace("NAMES", names)
(destination / "src/SeWZC.WorldBox.Core/SimulationStageProbe.cs").write_text(probe_source)
(destination / "src/SeWZC.WorldBox.Core/EcologyOnlyProbe.cs").write_text("""namespace SeWZC.WorldBox.Core;
public sealed partial class WorldEngine
{
    public void StepEcologyForProbe(int days = 1)
    {
        for (var day = 0; day < days; day++)
        {
            State.Tick++;
            var wildlife = SimulationStageProbe.Begin();
            TickWildlife();
            SimulationStageProbe.End(2, wildlife);
            var plants = SimulationStageProbe.Begin();
            TickPlants();
            SimulationStageProbe.End(3, plants);
        }
    }
}
""")
driver = destination / "tests/SeWZC.WorldBox.Core.Tests/SimulationPerformance.cs"
source = driver.read_text()
source = source.replace("        var wars = !args.Contains(\"--peace\");",
                        "        var wars = !args.Contains(\"--peace\");\n"
                        "        void Advance(WorldEngine engine, int days = 1)\n"
                        "        { if (args.Contains(\"--ecology-only\")) engine.StepEcologyForProbe(days); else engine.Step(days); }\n"
                        "        WorldEngine Create()\n"
                        "        {\n"
                        "            var engine = createWorld(population, wars);\n"
                        "            if (args.Contains(\"--dense-ecology\"))\n"
                        "                foreach (var tile in engine.State.Tiles)\n"
                        "                {\n"
                        "                    tile.Terrain = TerrainType.Forest; tile.Fertility = 100; tile.ResourceAmount = 100;\n"
                        "                    tile.Rainfall = tile.NaturalWaterYield = .2;\n"
                        "                    tile.Plants = new() { Trees = .7, Shrubs = .3 };\n"
                        "                    tile.Wildlife = WildlifeKind.Rabbit; tile.WildlifePopulation = 0; tile.OtherWildlife = default;\n"
                        "                    var others = tile.OtherWildlife;\n"
                        "                    foreach (var species in AnimalRules.Species)\n"
                        "                    {\n"
                        "                        var population = AnimalRules.EnvironmentalCapacity(tile, species) * .8;\n"
                        "                        if (species == WildlifeKind.Rabbit) tile.WildlifePopulation = population;\n"
                        "                        else others.Set(species, population);\n"
                        "                    }\n"
                        "                    tile.OtherWildlife = others;\n"
                        "                }\n"
                        "            return engine;\n"
                        "        }")
source = source.replace("createWorld(population, wars);", "Create();")
# Keep the factory's production-world call rather than recursing into itself.
source = source.replace("            var engine = Create();\n            if", "            var engine = createWorld(population, wars);\n            if")
source = source.replace("priming.Step(warmup);", "Advance(priming, warmup);")
source = source.replace("priming.Step(ticks);", "Advance(priming, ticks);")
source = source.replace("engine.Step(warmup);", "Advance(engine, warmup);")
source = source.replace("                engine.Step();", "                Advance(engine);")
source = source.replace("            var total = Stopwatch.GetTimestamp();",
                        "            SimulationStageProbe.Reset();\n"
                        "            SimulationStageProbe.Enabled = true;\n"
                        "            var total = Stopwatch.GetTimestamp();")
source = source.replace("            var elapsed = Stopwatch.GetElapsedTime(total).TotalMilliseconds;",
                        "            var elapsed = Stopwatch.GetElapsedTime(total).TotalMilliseconds;\n"
                        "            SimulationStageProbe.Enabled = false;")
source = source.replace("                repetition, elapsedMs = elapsed,",
                        "                stages = SimulationStageProbe.Snapshot(),\n"
                        "                repetition, elapsedMs = elapsed,")
source = source.replace("            warmupTicks = warmup, measuredTicks = ticks, repetitions,",
                        "            ecologyOnly = args.Contains(\"--ecology-only\"), denseEcology = args.Contains(\"--dense-ecology\"),\n"
                        "            warmupTicks = warmup, measuredTicks = ticks, repetitions,")
driver.write_text(source)
print(destination / "tests/SeWZC.WorldBox.Core.Tests")
