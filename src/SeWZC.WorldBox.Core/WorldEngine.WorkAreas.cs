using SeWZC.WorldBox.Core.Runtime;

namespace SeWZC.WorldBox.Core;

public sealed partial class WorldEngine
{
    private bool NaturalWorkPlotAvailable(int index, Profession profession, StateReference<Settlement> town)
    {
        var tile = Current.Tiles[index];
        if (tile.Value.FireTicks > 0)
            return false;
        // 矮人可进入天然山地，其他种族也可站在邻格采矿；岗位须与实际开采规则一致。
        if (profession == Profession.Miner)
        {
            return (tile.Value.IsWalkable || tile.Value.Terrain == TerrainType.Mountain)
                   && (ResourceSiteYield(index, profession) > 0 || KnownDepositWorkAvailable(index, town));
        }

        if (!tile.Value.IsWalkable)
            return false;
        return profession switch
        {
            Profession.Farmer => PlantHarvestEfficiency(tile, false) >= .25 && .7 * PlantSiteYield(tile, false) >= .04,
            Profession.Lumberjack => PlantHarvestEfficiency(tile, true) >= .25 && PlantSiteYield(tile, true) > 0,
            _ => false,
        };
    }

    private bool KnownDepositWorkAvailable(int index, StateReference<Settlement> town)
    {
        foreach (var nearby in Circle(index % Current.Width, index / Current.Width, 1))
        {
            var tile = Current.Tiles[nearby];
            if (Distance(nearby % Current.Width, nearby / Current.Width, town.Value.X, town.Value.Y) > 6
                || !tile.Value.DepositDiscovered || tile.Value.DepositAmount <= 0 || tile.Value.FireTicks > 0
                || tile.Value.Deposit is not { } kind || town.Value.Resources.Get(kind) >= 16
                || DepositResearch(kind) is not { } research || !HasResearch(town.Value.Id, research))
                continue;
            return true;
        }

        return false;
    }

    private double LocalMineralDeficit(StateReference<Settlement> town)
    {
        var reserve = LocalDevelopmentReserve(town);
        return Math.Max(0, Math.Max(80, reserve.Stone) - town.Value.Resources.Stone)
               + Math.Max(0, Math.Max(80, reserve.Ore) - town.Value.Resources.Ore)
               + (HasResearch(town.Value.Id, Advancement.Industry) ? Math.Max(0, 16 - town.Value.Resources.Coal) : 0)
               + (HasResearch(town.Value.Id, Advancement.Electrification) ? Math.Max(0, 16 - town.Value.Resources.Oil) : 0)
               + (HasResearch(town.Value.Id, Advancement.AdvancedComputing) ? Math.Max(0, 16 - town.Value.Resources.RareEarth) : 0);
    }

    private void AssignNaturalWorkAreas(StateReference<Settlement> town, List<ResidentCursor> adults, List<NaturalWorkPlot> plots)
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
                || person.Agent.DestinationSettlementId != 0 || Distance(person.X, person.Y, town.Value.X, town.Value.Y) > 3
                || person.TravelMode != TravelMode.Foot ||
                Current.Tick - person.MoveStartedTick < person.MoveDurationTicks)
                continue;
            var previous = person.Agent.WorkAreaIndex;
            var previousPlot = new NaturalWorkPlot(previous, person.Profession);
            if (person.Agent.WorkplaceId == 0 && plots.Contains(previousPlot)
                                              && occupied.GetValueOrDefault(previousPlot) == 1
                                              && RaceTerrainRules.CanWalk(Current.Tiles[previous].Value, person.Race))
                continue;
            if (previous >= 0)
                occupied[previousPlot]--;
            var selected = -1;
            var bestDistance = int.MaxValue;
            if (person.Agent.WorkplaceId == 0)
            {
                foreach (var plot in plots)
                {
                    if (plot.Profession != person.Profession || occupied.GetValueOrDefault(plot) > 0
                                                             || !RaceTerrainRules.CanWalk(Current.Tiles[plot.Index].Value,
                                                                 person.Race))
                        continue;
                    var distance = Distance(person.X, person.Y, plot.Index % Current.Width, plot.Index / Current.Width);
                    if (distance < bestDistance || (distance == bestDistance && plot.Index < selected))
                    {
                        selected = plot.Index;
                        bestDistance = distance;
                    }
                }
            }

            if (selected >= 0)
                occupied[new NaturalWorkPlot(selected, person.Profession)] = 1;
            if (person.Agent.WorkAreaIndex != selected)
                person.Agent = person.Agent with { WorkAreaIndex = selected };
        }
    }

    /// <summary>已在家园附近确认的自然劳动地块。</summary>
    /// <param name="Index">地格索引。</param>
    /// <param name="Profession">可在此从事的专业劳动。</param>
    private readonly record struct NaturalWorkPlot(int Index, Profession Profession);
}
