using SeWZC.WorldBox.Core.Runtime;

namespace SeWZC.WorldBox.Core;

public sealed partial class WorldEngine
{
    private bool NaturalWorkPlotAvailable(int index, Profession profession, StateReference<Settlement> town)
    {
        var tile = Tiles[index];
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
        foreach (var nearby in Circle(index % Width, index / Width, 1))
        {
            var tile = Tiles[nearby];
            if (Distance(nearby % Width, nearby / Width, town.Value.X, town.Value.Y) > 6
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

    private void AssignNaturalWorkAreas(StateReference<Settlement> town, List<StateReference<Resident>> adults, List<NaturalWorkPlot> plots)
    {
        var occupied = new Dictionary<NaturalWorkPlot, int>();
        foreach (var person in adults)
            if (person.Value.Agent.WorkAreaIndex >= 0)
            {
                var plot = new NaturalWorkPlot(person.Value.Agent.WorkAreaIndex, person.Value.Profession);
                occupied[plot] = occupied.GetValueOrDefault(plot) + 1;
            }

        foreach (var person in adults)
        {
            if (person.Value.Health < 60 || person.Value.SicknessTicks > 0 || person.Value.Agent.Goal.PlayerDirected
                || person.Value.Agent.DestinationSettlementId != 0 || Distance(person.Value.X, person.Value.Y, town.Value.X, town.Value.Y) > 3
                || person.Value.TravelMode != TravelMode.Foot ||
                SimulationTick - person.Value.MoveStartedTick < person.Value.MoveDurationTicks)
                continue;
            var previous = person.Value.Agent.WorkAreaIndex;
            var previousPlot = new NaturalWorkPlot(previous, person.Value.Profession);
            if (person.Value.Agent.WorkplaceId == 0 && plots.Contains(previousPlot)
                                              && occupied.GetValueOrDefault(previousPlot) == 1
                                              && RaceTerrainRules.CanWalk(Tiles[previous].Value, person.Value.Race))
                continue;
            if (previous >= 0)
                occupied[previousPlot]--;
            var selected = -1;
            var bestDistance = int.MaxValue;
            if (person.Value.Agent.WorkplaceId == 0)
            {
                foreach (var plot in plots)
                {
                    if (plot.Profession != person.Value.Profession || occupied.GetValueOrDefault(plot) > 0
                                                             || !RaceTerrainRules.CanWalk(Tiles[plot.Index].Value,
                                                                 person.Value.Race))
                        continue;
                    var distance = Distance(person.Value.X, person.Value.Y, plot.Index % Width, plot.Index / Width);
                    if (distance < bestDistance || (distance == bestDistance && plot.Index < selected))
                    {
                        selected = plot.Index;
                        bestDistance = distance;
                    }
                }
            }

            if (selected >= 0)
                occupied[new NaturalWorkPlot(selected, person.Value.Profession)] = 1;
            if (person.Value.Agent.WorkAreaIndex != selected)
                person.Replace(person.Value.WithAgent(person.Value.Agent with { WorkAreaIndex = selected }));
        }
    }

    /// <summary>已在家园附近确认的自然劳动地块。</summary>
    /// <param name="Index">地格索引。</param>
    /// <param name="Profession">可在此从事的专业劳动。</param>
    private readonly record struct NaturalWorkPlot(int Index, Profession Profession);
}
