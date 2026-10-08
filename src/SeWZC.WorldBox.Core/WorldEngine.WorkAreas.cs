using SeWZC.WorldBox.Core.Runtime;
namespace SeWZC.WorldBox.Core;

public sealed partial class WorldEngine
{
    /// <summary>已在家园附近确认的自然劳动地块。</summary>
    /// <param name="Index">地格索引。</param>
    /// <param name="Profession">可在此从事的专业劳动。</param>
    private readonly record struct NaturalWorkPlot(int Index, Profession Profession);

    private static bool NaturalWorkPlotAvailable(TileCursor tile, Profession profession)
    {
        if (!tile.IsWalkable || tile.FireTicks > 0) return false;
        return profession switch
        {
            Profession.Farmer => tile.PlantHarvestEfficiency(false) >= .25 && .7 * tile.PlantSiteYield(false) >= .04,
            Profession.Lumberjack => tile.PlantHarvestEfficiency(true) >= .25 && tile.PlantSiteYield(true) > 0,
            Profession.Miner => tile.ResourceAmount > 0
                && TerrainRules.For(tile.Terrain).StoneYield + TerrainRules.For(tile.Terrain).OreYield >= .5,
            _ => false,
        };
    }

    private void AssignNaturalWorkAreas(SettlementCursor town, List<ResidentCursor> adults, List<NaturalWorkPlot> plots)
    {
        var occupied = new Dictionary<NaturalWorkPlot, int>();
        foreach (var person in adults)
            if (person.Agent.WorkAreaIndex >= 0)
            {
                var plot = new NaturalWorkPlot(person.Agent.WorkAreaIndex, person.Profession);
                occupied[plot] = occupied.GetValueOrDefault(plot) + 1;
            }
        foreach (var person in adults)
        {
            if (person.Health < 60 || person.SicknessTicks > 0 || person.Agent.Goal.PlayerDirected
                || person.Agent.DestinationSettlementId != 0 || Distance(person.X, person.Y, town.X, town.Y) > 3
                || person.TravelMode != TravelMode.Foot || Current.Tick - person.MoveStartedTick < person.MoveDurationTicks)
                continue;
            var previous = person.Agent.WorkAreaIndex;
            var previousPlot = new NaturalWorkPlot(previous, person.Profession);
            if (person.Agent.WorkplaceId == 0 && plots.Contains(previousPlot)
                && occupied.GetValueOrDefault(previousPlot) == 1) continue;
            if (previous >= 0) occupied[previousPlot]--;
            var selected = -1;
            var bestDistance = int.MaxValue;
            if (person.Agent.WorkplaceId == 0)
                foreach (var plot in plots)
                {
                    if (plot.Profession != person.Profession || occupied.GetValueOrDefault(plot) > 0
                        || !RaceTerrainRules.CanWalk(Current.Tiles[plot.Index], person.Race)) continue;
                    var distance = Distance(person.X, person.Y, plot.Index % Current.Width, plot.Index / Current.Width);
                    if (distance < bestDistance || distance == bestDistance && plot.Index < selected)
                    { selected = plot.Index; bestDistance = distance; }
                }
            if (selected >= 0) occupied[new(selected, person.Profession)] = 1;
            person.Agent.WorkAreaIndex = selected;
        }
    }
}
