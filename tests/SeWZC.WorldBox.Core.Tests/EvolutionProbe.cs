using System.Text.Json;
using SeWZC.WorldBox.Core;

internal static class EvolutionProbe
{
    public static void Run(string outputPath)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(outputPath))!);
var all = new List<object>();
foreach (var seed in new[] {73921,42,223,17,9876}) {
 var e=WorldEngine.Create(seed,256,256,true); var seen=new HashSet<int>(); var points=new List<object>(); var kinds=new Dictionary<string,int>(); var actions=new Dictionary<string,int>(); var observedDeaths=new HashSet<int>(); long last=0,gap=0; int total=0;
 for(var tick=0;tick<6000;tick+=60){
  e.Step(60);
  foreach(var dead in e.State.ArchivedResidents) observedDeaths.Add(dead.Id);
  foreach(var ev in e.State.Events.Where(x=>seen.Add(x.Id)).OrderBy(x=>x.Tick)){
   if(ev.Kind is not (WorldEventKind.Founding or WorldEventKind.Growth or WorldEventKind.Construction or WorldEventKind.Research or WorldEventKind.Diplomacy or WorldEventKind.War or WorldEventKind.Disaster)) continue;
   gap=Math.Max(gap,ev.Tick-last);last=ev.Tick;total++;var k=ev.Kind.ToString();kinds[k]=kinds.GetValueOrDefault(k)+1; var action=ev.Action.ToString(); actions[action]=actions.GetValueOrDefault(action)+1;
  }
  if(e.State.Tick%600==0) points.Add(new {e.State.Tick,e.State.Population,Nations=e.State.Nations.Count,Towns=e.State.Settlements.Count,Research=e.State.Society.Research.Sum(r=>r.Completed.Count),Buildings=e.State.Society.Buildings.Count,Wars=e.State.Diplomacies.Count(r=>r.Status==DiplomaticStatus.War), Recovering=e.State.Nations.Count(n=>n.Military.RecoveryUntilTick>e.State.Tick), Armies=e.State.Armies.Count, ReportedCampaigns=e.State.Nations.Count(n=>n.Military.ReportedOutcome!=WarOutcome.None), ObservedDeaths=observedDeaths.Count});
 }
 gap=Math.Max(gap,e.State.Tick-last);
 var resume=WorldEngine.ImportJson(e.ExportJson()); e.Step(60);resume.Step(60); if(e.ExportJson()!=resume.ExportJson()) throw new Exception("Continuation mismatch");
 var result=new {seed,simulatedSecondsAt1x=1200,events=total,longestGapSecondsAt1x=gap*.2,kinds,actions,observedDeaths=observedDeaths.Count,points};all.Add(result); Console.WriteLine(JsonSerializer.Serialize(result));
}
File.WriteAllText(outputPath,JsonSerializer.Serialize(all,new JsonSerializerOptions{WriteIndented=true}));

    }
}
