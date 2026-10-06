#!/usr/bin/env python3
"""Create a disposable browser source copy with bounded timing probes.

Production sources and simulation rules remain unchanged. Publish the printed
browser project, then use tests/browser/five-speed-profile.cjs against that site.
"""
import argparse
import shutil
from pathlib import Path

parser = argparse.ArgumentParser(description=__doc__)
parser.add_argument('destination', type=Path)
args = parser.parse_args()
root = Path(__file__).resolve().parents[1]
destination = args.destination.resolve()
if destination.exists():
    parser.error('destination must not exist')
if destination.is_relative_to(root / 'src') or destination.is_relative_to(root / 'tests'):
    parser.error('destination must be outside production source and tests')
destination.mkdir(parents=True)
for name in ('global.json', 'Directory.Build.props'):
    shutil.copy2(root / name, destination / name)
for name in ('SeWZC.WorldBox.Core', 'SeWZC.WorldBox.UI', 'SeWZC.WorldBox.Browser'):
    shutil.copytree(root / 'src' / name, destination / 'src' / name,
                    ignore=shutil.ignore_patterns('bin', 'obj'))

names = []
def replace_once(text, marker, replacement, label):
    if text.count(marker) != 1:
        raise SystemExit(f'Source changed: {label}')
    return text.replace(marker, replacement)

def scope(name, tick):
    if name not in names:
        names.append(name)
    return f'SeWZC.WorldBox.Core.BrowserStageProbe.Measure({names.index(name)}, {tick})'

def method(file, signature, name, tick):
    path = destination / file
    text = path.read_text()
    marker = signature + '\n    {\n'
    if text.count(marker) != 1:
        raise SystemExit(f'Method changed: {file}: {signature}')
    path.write_text(text.replace(marker, marker + f'        using var browserProbe = {scope(name, tick)};\n'))

core = 'src/SeWZC.WorldBox.Core/'
ui = 'src/SeWZC.WorldBox.UI/'
method(core + 'WorldEngine.Simulation.cs', '    public void Step(int steps = 1)', 'Engine.Step', 'State.Tick')
path = destination / core / 'WorldEngine.Simulation.cs'
text = path.read_text()
start, end = text.index('    public void Step('), text.index('    private static void CapResources')
step = text[start:end]
stages = [
    ('Reindex', 'Reindex();'), ('Disasters', 'UpdateDisasters();'),
    ('Wildlife', 'TickWildlife();'), ('Residents', 'UpdateResidents();'),
    ('Actions', 'UpdateAgentNeedsAndActions();'), ('Communication', 'UpdateLocalCommunication();'),
    ('Society', 'TickSociety();'), ('Diplomacy', 'TickDiplomacy();'),
    ('Conflicts', 'TickLocalConflicts();'), ('Migration', 'TickMigrationAndSecession();'),
    ('Growth', 'if (State.Tick % 12 == 0) GrowSettlements();'),
    ('Territory', 'if (State.Tick % 30 == 0) RefreshTerritoryClaims();'),
    ('Armies', 'UpdateArmies();'), ('Archive', 'ArchiveDeadResidents();'),
    ('RemoveSettlements', 'foreach (var settlement in State.Settlements.Where(s => _citizens[s.Id].Count == 0).ToArray())\n                    RemoveSettlement(settlement, "居民离散，聚落成为遗址");'),
    ('RemoveNations', 'RemoveEmptyNations();'), ('Topology', 'ReconcileSocietyTopology();'),
    ('Totals', 'RefreshTotals();'), ('Projects', 'ObserveProjects();')
]
for name, statement in stages:
    marker = '            ' + statement + '\n'
    expected = 3 if name == 'Reindex' else 1
    if step.count(marker) != expected:
        raise SystemExit(f'Simulation changed: {name}')
    step = step.replace(marker, f'            using ({scope("Core." + name, "State.Tick")}) {{ {statement} }}\n')
path.write_text(text[:start] + step + text[end:])

method(ui + 'MainView.cs', '    private void OnTick(object? sender, EventArgs e)', 'UI.OnTick', '_engine.State.Tick')
method(ui + 'MainView.cs', '    private void RefreshUi(bool force = false)', 'UI.RefreshUi', '_engine.State.Tick')
path = destination / ui / 'MainView.cs'
text = path.read_text()
if '            await App.Storage.SaveAsync(_engine.ExportJson());' in text:
    text = replace_once(text, '            await App.Storage.SaveAsync(_engine.ExportJson());',
        f'            string probeJson;\n            using ({scope("Save.Serialize", "_engine.State.Tick")}) probeJson = _engine.ExportJson();\n'
        f'            using ({scope("Save.Storage", "_engine.State.Tick")}) await App.Storage.SaveAsync(probeJson);', 'SaveAsync')
else:
    # This inclusive elapsed scope contains cooperative waits, not just CPU work.
    first = text.index('    private async Task SaveAsync(bool manual)')
    last = text.index('    private async ValueTask YieldDuringSave(', first)
    save = replace_once(text[first:last], '            var chunks = await source.ExportJsonChunksAsync(YieldDuringSave, capture.Token);',
        f'            string[] chunks;\n            using ({scope("Save.Serialize", "source.State.Tick")}) chunks = await source.ExportJsonChunksAsync(YieldDuringSave, capture.Token);', 'async save capture')
    save = replace_once(save, '            await storage.SaveChunksAsync(chunks);',
        f'            using ({scope("Save.Storage", "source.State.Tick")}) await storage.SaveChunksAsync(chunks);', 'save storage')
    text = text[:first] + save + text[last:]
path.write_text(text)
for file, signature, name in [
    ('WorldMapControl.cs', '    public void RefreshWorld(bool resetCamera = false, bool deferAnimation = false)', 'Map.RefreshWorld'),
    ('WorldMapControl.cs', '    public override void Render(DrawingContext context)', 'Map.Render'),
    ('WorldMapControl.cs', '    private void RebuildChangedChunks()', 'Map.Terrain'),
    ('WorldMapControl.cs', '    private void RebuildResidents()', 'Map.ResidentGeometry'),
    ('WorldMapControl.cs', '    private void DrawLabels(DrawingContext context, WorldState state)', 'Map.Labels'),
    ('WorldMapControl.Ecology.cs', '    private void DrawEcology(DrawingContext context, WorldState state)', 'Map.Ecology'),
    ('WorldMapControl.Sprites.cs', '    private void DrawNearScene(DrawingContext context, WorldState state)', 'Map.NearScene'),
    ('WorldMapControl.Motion.cs', '    private void CaptureMotionSnapshots()', 'Map.MotionSnapshots'),
]:
    method(ui + 'Controls/' + file, signature, name, 'Engine?.State.Tick ?? -1')

# Separate hash scans from actual tile-image reconstruction. The optional
# counterfactual only narrows presentation invalidation to DrawTerrainTile inputs.
path = destination / ui / 'Controls/WorldMapControl.cs'
text = path.read_text()
text = replace_once(text, '            var terrainHash = 2166136261;',
    f'            var hashProbe = {scope("Map.HashScan", "state.Tick")};\n            var terrainHash = 2166136261;', 'chunk hash start')
text = replace_once(text, '            var key = (cx, cy);', '            hashProbe.Dispose();\n            var key = (cx, cy);', 'chunk hash end')
text = replace_once(text, '            if (chunk.Terrain is null || chunk.TerrainHash != terrainHash)\n            {',
    '            if (chunk.Terrain is null || chunk.TerrainHash != terrainHash)\n            {\n' + f'                using var terrainProbe = {scope("Map.TerrainBuild", "state.Tick")};', 'terrain build')
text = replace_once(text, '            if (!chunk.TerritoryCached || chunk.TerritoryHash != territoryHash)\n            {',
    '            if (!chunk.TerritoryCached || chunk.TerritoryHash != territoryHash)\n            {\n' + f'                using var territoryProbe = {scope("Map.TerritoryBuild", "state.Tick")};', 'territory build')
terrain_inputs = destination / ui / 'Controls/WorldMapControl.Terrain.cs'
if terrain_inputs.exists():
    path.write_text(text)
    path = terrain_inputs
    text = path.read_text()
    text = replace_once(text, '    private static uint TerrainImageInput(Tile tile)\n    {',
        '    private static uint TerrainImageInput(Tile tile)\n    {\n        var exactResources = SeWZC.WorldBox.Core.BrowserStageProbe.ExactResourceHash;', 'resource experiment flag')
else:
    text = replace_once(text, '        var colors = new Dictionary<int, uint>();',
        '        var exactResources = SeWZC.WorldBox.Core.BrowserStageProbe.ExactResourceHash;\n        var colors = new Dictionary<int, uint>();', 'resource experiment flag')
original = '(uint)Math.Clamp((int)(tile.ResourceAmount / 25), 0, 4) * 256'
exact = next((marker for marker in (
    '(WorldEngine.IsForestTerrain(tile.Terrain) && tile.ResourceAmount < 25 ? 256u : 0u)',
    '(tile.Terrain == TerrainType.Forest && tile.ResourceAmount < 25 ? 256u : 0u)',
) if marker in text), None)
default_exact = original not in text
if default_exact:
    if exact is None:
        raise SystemExit('Source changed: fixed terrain resource hash')
    text = replace_once(text, exact, '(exactResources ? ' + exact + ' : ' + original + ')', 'fixed terrain resource hash')
else:
    exact = '(WorldEngine.IsForestTerrain(tile.Terrain) && tile.ResourceAmount < 25 ? 256u : 0u)'
    text = replace_once(text, original, '(exactResources ? ' + exact + ' : ' + original + ')', 'terrain resource hash')
path.write_text(text)

probe = '''using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;
namespace SeWZC.WorldBox.Core;
public static class BrowserStageProbe
{
    public static bool ExactResourceHash = DEFAULT_EXACT;
    private static readonly Sample[] Buffer = new Sample[100000];
    private static int Count, Overflow;
    private static long Epoch = Stopwatch.GetTimestamp();
    private static readonly string[] Names = [NAMES];
    public static Scope Measure(int stage, long tick) => new(stage, tick, Stopwatch.GetTimestamp());
    public static void Reset() { Count = Overflow = 0; Epoch = Stopwatch.GetTimestamp(); }
    public readonly struct Scope(int stage, long tick, long start) : IDisposable
    {
        public void Dispose()
        {
            var end = Stopwatch.GetTimestamp();
            if (Count >= Buffer.Length) { Overflow++; return; }
            Buffer[Count++] = new Sample { Stage = stage, Tick = tick,
                StartMs = (start - Epoch) * 1000d / Stopwatch.Frequency,
                Ms = (end - start) * 1000d / Stopwatch.Frequency };
        }
    }
    public struct Sample { public int Stage { get; set; } public long Tick { get; set; }
        public double StartMs { get; set; } public double Ms { get; set; } }
    public sealed class Report { public string[] Names { get; set; } = [];
        public Sample[] Samples { get; set; } = []; public int Overflow { get; set; }
        public double ElapsedMs { get; set; } }
    public static string Snapshot() => JsonSerializer.Serialize(new Report {
        Names = Names, Samples = Buffer.AsSpan(0, Count).ToArray(), Overflow = Overflow,
        ElapsedMs = (Stopwatch.GetTimestamp() - Epoch) * 1000d / Stopwatch.Frequency }, ProbeJsonContext.Default.Report);
}
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(BrowserStageProbe.Report))]
internal partial class ProbeJsonContext : JsonSerializerContext { }
'''.replace('NAMES', ', '.join('"' + name + '"' for name in names)).replace('DEFAULT_EXACT', str(default_exact).lower())
(destination / core / 'BrowserStageProbe.cs').write_text(probe)
path = destination / 'src/SeWZC.WorldBox.Browser/BrowserTestBridge.cs'
text = replace_once(path.read_text(), '    [JSExport]\n    public static string ReadSnapshot()',
'''    [JSExport]
    public static string ReadPerformance() => SeWZC.WorldBox.Core.BrowserStageProbe.Snapshot();
    [JSExport]
    public static void ResetPerformance() => SeWZC.WorldBox.Core.BrowserStageProbe.Reset();
    [JSExport]
    public static void SetExactResourceHash(bool enabled) => SeWZC.WorldBox.Core.BrowserStageProbe.ExactResourceHash = enabled;

    [JSExport]
    public static string ReadSnapshot()''', 'browser probe exports')
path.write_text(text)
path = destination / 'src/SeWZC.WorldBox.Browser/wwwroot/main.js'
text = replace_once(path.read_text(), 'snapshot: () => JSON.parse(readSnapshot())',
    'snapshot: () => JSON.parse(readSnapshot()), performance: () => JSON.parse(exports.SeWZC.WorldBox.Browser.BrowserTestBridge.ReadPerformance()), resetPerformance: () => exports.SeWZC.WorldBox.Browser.BrowserTestBridge.ResetPerformance(), setExactResourceHash: value => exports.SeWZC.WorldBox.Browser.BrowserTestBridge.SetExactResourceHash(value)', 'browser probe bridge')
path.write_text(text)
print(destination / 'src/SeWZC.WorldBox.Browser/SeWZC.WorldBox.Browser.csproj')
