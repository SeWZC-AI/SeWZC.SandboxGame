#!/usr/bin/env python3
"""Instrument a disposable source copy; never modify the production engine.

Run the functional suite first. Build/run the printed project with
--profile-simulation --output <report.json> and the usual measurement options.
"""
import argparse
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
    ("UpdateResidents", "UpdateResidents();"),
    ("UpdateAgentNeedsAndActions", "UpdateAgentNeedsAndActions();"),
    ("UpdateLocalCommunication", "UpdateLocalCommunication();"),
    ("TickSociety", "TickSociety();"),
    ("TickDiplomacy", "TickDiplomacy();"),
    ("TickMigrationAndSecession", "TickMigrationAndSecession();"),
    ("GrowSettlements", "if (State.Tick % 12 == 0) GrowSettlements();"),
    ("RefreshTerritoryClaims", "if (State.Tick % 30 == 0) RefreshTerritoryClaims();"),
    ("UpdateArmies", "UpdateArmies();"),
    ("ArchiveDeadResidents", "ArchiveDeadResidents();"),
    ("RemoveSettlements", 'foreach (var settlement in State.Settlements.Where(s => _citizens[s.Id].Count == 0).ToArray()) RemoveSettlement(settlement, "居民离散，聚落成为遗址");'),
    ("RemoveEmptyNations", "RemoveEmptyNations();"),
    ("ReconcileSocietyTopology", "ReconcileSocietyTopology();"),
    ("RefreshTotals", "RefreshTotals();"),
]
start = simulation.index("    public void Step(")
end = simulation.index("    private static void CapResources", start)
step = simulation[start:end]
for name, statement in stages:
    line = "            " + statement + "\n"
    expected = 3 if name == "Reindex" else 1
    if step.count(line) != expected:
        raise SystemExit(f"Step changed: expected {expected} occurrences of {name}; review probes before running.")
# Separate local names for repeated calls, aggregate them into one stage.
counts = {}
instrumented = []
for line in step.splitlines(keepends=True):
    match = next(((i, name) for i, (name, statement) in enumerate(stages)
                  if line == "            " + statement + "\n"), None)
    if match is None:
        instrumented.append(line)
        continue
    index, name = match
    call = counts.get(name, 0)
    counts[name] = call + 1
    local = f"probe{index}_{call}"
    instrumented.extend([f"            var {local} = SimulationStageProbe.Begin();\n", line,
                         f"            SimulationStageProbe.End({index}, {local});\n"])

shutil.copytree(root / "src/SeWZC.WorldBox.Core", destination / "src/SeWZC.WorldBox.Core",
                ignore=shutil.ignore_patterns("bin", "obj"))
shutil.copytree(root / "tests/SeWZC.WorldBox.Core.Tests", destination / "tests/SeWZC.WorldBox.Core.Tests",
                ignore=shutil.ignore_patterns("bin", "obj"))
for name in ("global.json", "Directory.Build.props"):
    shutil.copy2(root / name, destination / name)
(destination / "src/SeWZC.WorldBox.Core/WorldEngine.Simulation.cs").write_text(
    simulation[:start] + "".join(instrumented) + simulation[end:])

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
    public static (long Time, long Bytes) Begin() => Enabled
        ? (Stopwatch.GetTimestamp(), GC.GetAllocatedBytesForCurrentThread()) : default;
    public static void End(int stage, (long Time, long Bytes) start)
    {
        if (!Enabled) return;
        Duration[stage] += Stopwatch.GetTimestamp() - start.Time;
        Allocated[stage] += GC.GetAllocatedBytesForCurrentThread() - start.Bytes;
        Calls[stage]++;
    }
    public static void Reset()
    {
        Array.Clear(Duration); Array.Clear(Allocated); Array.Clear(Calls);
    }
    public static object[] Snapshot() => Names.Select((name, i) => (object)new
    {
        name, elapsedMs = Duration[i] * 1000d / Stopwatch.Frequency,
        allocatedBytes = Allocated[i], calls = Calls[i]
    }).ToArray();
}
""".replace("NAMES", names)
(destination / "src/SeWZC.WorldBox.Core/SimulationStageProbe.cs").write_text(probe_source)
driver = destination / "tests/SeWZC.WorldBox.Core.Tests/SimulationPerformance.cs"
source = driver.read_text()
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
driver.write_text(source)
print(destination / "tests/SeWZC.WorldBox.Core.Tests")
