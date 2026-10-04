#!/usr/bin/env python3
"""Measure default ecological value comparison and empty-world save cost.

Pass a built Core DLL and an output JSON. This is an isolated microbenchmark,
not a simulation CPU share. Run old and new binaries sequentially.
"""
import argparse
import os
from pathlib import Path
import subprocess
import tempfile
from xml.sax.saxutils import escape

parser = argparse.ArgumentParser(description=__doc__)
parser.add_argument("core_dll", type=Path)
parser.add_argument("output", type=Path)
args = parser.parse_args()
dotnet = os.environ.get("WORLDBOX_DOTNET", "dotnet")
core = args.core_dll.resolve(strict=True)
output = args.output.resolve()
output.parent.mkdir(parents=True, exist_ok=True)
with tempfile.TemporaryDirectory(prefix="worldbox-value-profile-") as folder:
    root = Path(folder)
    (root / "Probe.csproj").write_text(f'''<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup><OutputType>Exe</OutputType><TargetFramework>net10.0</TargetFramework><ImplicitUsings>enable</ImplicitUsings></PropertyGroup>
  <ItemGroup><Reference Include="SeWZC.WorldBox.Core"><HintPath>{escape(str(core))}</HintPath></Reference></ItemGroup>
</Project>''')
    (root / "Program.cs").write_text(r'''
using System.Diagnostics;
using System.Text.Json;
using System.Security.Cryptography;
using System.Text;
using SeWZC.WorldBox.Core;
const int comparisons = 100_000;
object Compare<T>() where T : struct
{
    var values = new T[2]; var comparer = EqualityComparer<T>.Default;
    for (var i = 0; i < 1000; i++) _ = comparer.Equals(values[i & 1], default);
    var runs = new List<object>();
    for (var run = 0; run < 3; run++)
    {
        var allocated = GC.GetAllocatedBytesForCurrentThread(); var started = Stopwatch.GetTimestamp(); var equal = 0;
        for (var i = 0; i < comparisons; i++) if (comparer.Equals(values[i & 1], default)) equal++;
        var elapsed = Stopwatch.GetElapsedTime(started).TotalMilliseconds;
        var bytes = GC.GetAllocatedBytesForCurrentThread() - allocated;
        if (equal != comparisons) throw new Exception("Default comparison changed");
        runs.Add(new { elapsedMs = elapsed, nsPerComparison = elapsed * 1_000_000 / comparisons, allocatedBytesPerComparison = (double)bytes / comparisons });
    }
    return new { type = typeof(T).Name, comparisons, runs };
}
var wildlife = Compare<WildlifePopulations>(); var plants = Compare<PlantCoverage>();
var engine = WorldEngine.Create(451, 32, 32, false);
foreach (var tile in engine.State.Tiles) { tile.Wildlife = WildlifeKind.None; tile.WildlifePopulation = 0; tile.OtherWildlife = default; tile.Plants = default; }
for (var i = 0; i < 3; i++) _ = engine.ExportJson();
var saves = new List<object>();
for (var run = 0; run < 3; run++)
{
    var allocated = GC.GetAllocatedBytesForCurrentThread(); var started = Stopwatch.GetTimestamp(); string save = "";
    for (var i = 0; i < 20; i++) save = engine.ExportJson();
    var elapsed = Stopwatch.GetElapsedTime(started).TotalMilliseconds; var bytes = GC.GetAllocatedBytesForCurrentThread() - allocated;
    saves.Add(new { meanMs = elapsed / 20, allocatedBytesPerSave = bytes / 20, saveBytes = Encoding.UTF8.GetByteCount(save), saveSha256 = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(save))) });
}
Console.WriteLine(JsonSerializer.Serialize(new { framework = System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription,
    assemblyModuleId = typeof(WorldEngine).Module.ModuleVersionId, wildlife, plants, saves,
    limitation = "Isolated default-value and 32x32 empty-world save measurements; not full game CPU percentages." }, new JsonSerializerOptions { WriteIndented = true }));
''')
    subprocess.run([dotnet, "build", str(root / "Probe.csproj"), "-c", "Release", "-p:UseSharedCompilation=false", "-nologo", "-v:q"], check=True)
    with output.open("w") as stream:
        subprocess.run([dotnet, str(root / "bin/Release/net10.0/Probe.dll")], stdout=stream, check=True)
print(output)
