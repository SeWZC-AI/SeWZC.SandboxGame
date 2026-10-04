#!/usr/bin/env python3
"""Create a disposable probe with method self times and narrow communication regions.

Run functional tests first. Build the printed project and run --profile-simulation.
No production timers are inserted. Nested methods report both inclusive and self
time, so overlapping inclusive percentages must never be added together.
"""
import argparse
from pathlib import Path
import re
import subprocess
import sys

parser = argparse.ArgumentParser(description=__doc__)
parser.add_argument("destination", type=Path)
parser.add_argument("--deep", action="store_true", help="also time very frequent leaf queries; use for counts, not untraced wall-time shares")
args = parser.parse_args()
root = Path(__file__).resolve().parents[1]
destination = args.destination.resolve()
subprocess.run([sys.executable, str(root / "scripts/profile-simulation-stages.py"), str(destination)], check=True)
core = destination / "src/SeWZC.WorldBox.Core"
methods = [
    "ChooseAgentGoal", "ProductiveGoalContinues", "ActOnAgentGoal", "MoveAgentTowards", "SelectAgentStep",
    "MarkVisibleReachable", "FindVisibleResourceSite", "ResourceSiteYield", "FindWaterSite",
    "AddProvisionChoices", "AddBoatFishingChoice", "FindHarvestableWildlife", "VisibleClaimSite",
    "GatherActualResources", "ObserveAgentEnvironment", "RememberAgentFact", "LatestAgentFact",
    "SelectMessageFacts", "RelayKnownAgentMessages", "ReceiveSocietyReport", "ExchangeCulture",
    "FindLocalWorkBuilding", "BuildingHasWork", "FindWorkshopResource", "TryWorkAtBuilding",
    "ActOnProduction", "CanProduce", "ActOnRacialWork", "PlanLocalDevelopment", "ReconcileConnectedClaims",
    "BalanceLocalWorkforce", "RefreshLocalRepresentatives", "ReconcileSocietyTopology", "TickWildlife",
    "WildlifeCapacity", "TickPlants", "AgentFoodPolicyMultiplier", "ProvisionAtHome", "TransferPersonalProduction",
    "CanTraverseStep", "PrepareJourneyTransport", "AddAgentMissionChoices", "AgentFactReliability",
    "GrowSettlements", "ObserveProjects", "BeginLocalWorkQueries", "EndLocalWorkQueries",
]
if not args.deep:
    methods = [method for method in methods if method not in {"ResourceSiteYield", "CanTraverseStep", "AgentFactReliability"}]
names = []
for method in methods:
    matches = []
    pattern = re.compile(r"    (?:public|private|internal) (?:static )?[^;{}=]*?\b" + method + r"\([^;{}]*?\)\s*\n    \{")
    for path in core.glob("*.cs"):
        source = path.read_text()
        matches.extend((path, match.end()) for match in pattern.finditer(source))
    if len(matches) != 1:
        print(f"SKIP {method}: {len(matches)} block-bodied definitions", file=sys.stderr)
        continue
    index = len(names); names.append(method)
    path, position = matches[0]; source = path.read_text()
    path.write_text(source[:position] + f"\n        using var detailScope = SimulationDetailProbe.Enter({index});" + source[position:])

for filename, method in ((("WorldEngine.Society.cs", "HasResearch"),
                         ("WorldEngine.Society.cs", "IsFacilityOperating"),
                         ("WorldEngine.WorkQueries.cs", "FindBuilding")) if args.deep else ()):
    path = core / filename; source = path.read_text()
    pattern = re.compile(r"(    (?:public|private) [^\n]+?\b" + method + r"\([^\n]*?\)) => ([^;]+);")
    match = pattern.search(source)
    if match is None:
        raise SystemExit(f"Expression method changed: review {method}")
    index = len(names); names.append(method)
    replacement = f"{match.group(1)}\n    {{\n        using var detailScope = SimulationDetailProbe.Enter({index});\n        return {match.group(2)};\n    }}"
    path.write_text(source[:match.start()] + replacement + source[match.end():])

# Dense enum recipe lookup is measured separately from recipient-ID lookup.
recipes = core / "AdvancementRules.cs"
source = recipes.read_text()
for kind in (("ResearchKind", "BuildingKind") if args.deep else ()):
    pattern = re.compile(r"    public static Advancement\? For\(" + kind + r" kind\) => ([^\n]+);")
    match = pattern.search(source)
    if match is None:
        raise SystemExit(f"Recipe lookup changed: review {kind}")
    index = len(names); names.append(f"AdvancementRules.For.{kind}")
    replacement = f"    public static Advancement? For({kind} kind)\n    {{\n        using var detailScope = SimulationDetailProbe.Enter({index});\n        return {match.group(1)};\n    }}"
    source = source[:match.start()] + replacement + source[match.end():]
recipes.write_text(source)

simulation = core / "WorldEngine.Simulation.cs"
source = simulation.read_text()
source = re.sub(r"(            var probe(\d+)_\d+ = SimulationStageProbe.Begin\(\);)",
                lambda m: f"            SimulationDetailProbe.Stage = {m.group(2)};\n" + m.group(1), source)
simulation.write_text(source)

# Separate cache lookup/marking from the actual six-step traversal. Calls to
# BFS are misses; lookup calls minus BFS calls are shared-cache hits.
agents = core / "WorldEngine.Agents.cs"
source = agents.read_text()
lookup = "        if (cache.TryMark(slot, key, State.Tick, revision, _localMoveVisited, search)) return search;"
if source.count(lookup) != 1:
    raise SystemExit("Reachability changed: review shared-cache lookup")
index = len(names); names.append("Reachability.SharedCacheLookupAndMark")
source = source.replace(lookup, f"""        bool detailCacheHit;
        using (SimulationDetailProbe.Enter({index}))
            detailCacheHit = cache.TryMark(slot, key, State.Tick, revision, _localMoveVisited, search);
        if (detailCacheHit) return search;""")
first = source.index("        _localMoveVisited[start] = search; var head = 0; var tail = 1;", source.index("    private int MarkVisibleReachable("))
last = source.index("        cache.Store(slot, key, State.Tick, revision, _localMoveQueue, tail);", first)
index = len(names); names.append("Reachability.BreadthFirstSearch")
source = source[:first] + f"        var detailBfs = SimulationDetailProbe.Enter({index});\n" + source[first:last] + "        detailBfs.Dispose();\n" + source[last:]
agents.write_text(source)

communication = core / "WorldEngine.Communication.cs"
source = communication.read_text()
regions = [
    ("Communication.GatherNearbyResidents", "            foreach (var tile in Circle(sender.X, sender.Y, conversationRadius))", "            if (_conversationNeighbors.Count == 0) continue;"),
    ("Communication.SelectNearbyResidentByIdRank", "            var recipient = SelectConversationRecipient(", "            var facts = SelectMessageFacts(sender, relay: false);"),
]
for name, start, end in regions:
    if source.count(start) != 1 or source.count(end) != 1:
        raise SystemExit(f"Communication changed: review region {name}")
    index = len(names); names.append(name)
    first = source.index(start); last = source.index(end, first)
    source = source[:first] + f"        var detailRegion{index} = SimulationDetailProbe.Enter({index});\n" + source[first:last] + f"        detailRegion{index}.Dispose();\n" + source[last:]

communication.write_text(source)

# Count cache reuse without adding a timer to each very frequent tile lookup.
ecology = core / "WorldState.Ecology.cs"
source = ecology.read_text()
hit = "        if (cached != byte.MaxValue) return (WildlifeKind)cached;"
if source.count(hit) != 1:
    raise SystemExit("Edible animal cache changed: review scan region")
source = source.replace(hit, "        if (SimulationDetailProbe.Enabled) SimulationDetailProbe.EdibleAnimalQueries++;\n" + hit
    + "\n        if (SimulationDetailProbe.Enabled) SimulationDetailProbe.EdiblePopulationScans++;")
ecology.write_text(source)
quoted_names = ", ".join('"' + name + '"' for name in names)
(core / "SimulationDetailProbe.cs").write_text("""using System.Diagnostics;
namespace SeWZC.WorldBox.Core;
public static class SimulationDetailProbe
{
    public static bool Enabled;
    public static int Stage;
    public static long EdibleAnimalQueries, EdiblePopulationScans;
    private static readonly string[] Names = [NAMES];
    private static readonly long[] Inclusive = new long[Names.Length], Self = new long[Names.Length], Calls = new long[Names.Length];
    private static readonly long[] StageSelf = new long[20 * Names.Length], StageCalls = new long[20 * Names.Length];
    private static readonly long[] Children = new long[128];
    private static int Depth;
    public static Scope Enter(int index)
    {
        if (!Enabled) return default;
        Children[Depth] = 0;
        return new(index, ++Depth, Stopwatch.GetTimestamp(), Stage);
    }
    public readonly struct Scope(int index, int depth, long started, int stage) : IDisposable
    {
        public void Dispose()
        {
            if (depth == 0) return;
            var elapsed = Stopwatch.GetTimestamp() - started;
            var self = elapsed - Children[depth - 1];
            Inclusive[index] += elapsed; Self[index] += self; Calls[index]++;
            StageSelf[stage * Names.Length + index] += self; StageCalls[stage * Names.Length + index]++;
            Depth--; if (Depth > 0) Children[Depth - 1] += elapsed;
        }
    }
    public static void Reset() { Array.Clear(Inclusive); Array.Clear(Self); Array.Clear(Calls); Array.Clear(StageSelf); Array.Clear(StageCalls); Depth = 0; EdibleAnimalQueries = EdiblePopulationScans = 0; }
    public static object[] Snapshot() => Names.Select((name, i) => (object)new
    {
        name, inclusiveMs = Inclusive[i] * 1000d / Stopwatch.Frequency,
        selfMs = Self[i] * 1000d / Stopwatch.Frequency, calls = Calls[i],
        byStage = Enumerable.Range(0, 20).Where(stage => StageCalls[stage * Names.Length + i] > 0).Select(stage => new
        { stage, selfMs = StageSelf[stage * Names.Length + i] * 1000d / Stopwatch.Frequency, calls = StageCalls[stage * Names.Length + i] }).ToArray()
    }).ToArray();
}
""".replace("NAMES", quoted_names))
driver = destination / "tests/SeWZC.WorldBox.Core.Tests/SimulationPerformance.cs"
source = driver.read_text().replace("            SimulationStageProbe.Reset();", "            SimulationStageProbe.Reset();\n            SimulationDetailProbe.Reset();\n            SimulationDetailProbe.Enabled = true;")
source = source.replace("            SimulationStageProbe.Enabled = false;", "            SimulationStageProbe.Enabled = false;\n            SimulationDetailProbe.Enabled = false;")
source = source.replace("                stages = SimulationStageProbe.Snapshot(),", "                stages = SimulationStageProbe.Snapshot(),\n                details = SimulationDetailProbe.Snapshot(),\n                edibleAnimalQueries = SimulationDetailProbe.EdibleAnimalQueries,\n                ediblePopulationScans = SimulationDetailProbe.EdiblePopulationScans,")
driver.write_text(source)
print(driver.parent)
