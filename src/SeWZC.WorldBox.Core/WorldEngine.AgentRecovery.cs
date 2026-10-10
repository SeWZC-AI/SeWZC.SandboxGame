using SeWZC.WorldBox.Core.Runtime;

namespace SeWZC.WorldBox.Core;

public sealed partial class WorldEngine
{
    private int FindSafeVisibleSite(StateReference<Resident> person, StateReference<Settlement> home, AgentFact? danger)
    {
        var reachable = 0;
        var best = -1;
        var bestDangerDistance = -1;
        var bestHomeDistance = int.MaxValue;
        foreach (var offset in VisibleResourceOffsets)
        {
            if (offset.Distance > 5)
                break;
            var x = person.Value.X + offset.X;
            var y = person.Value.Y + offset.Y;
            if (!Walkable(x, y, person.Value.Race) || Tiles[Index(x, y)].Value.FireTicks > 0)
                continue;
            var dangerDistance = Distance(x, y, danger?.X ?? person.Value.X, danger?.Y ?? person.Value.Y);
            var homeDistance = Distance(x, y, home.Value.X, home.Value.Y);
            if ((dangerDistance > bestDangerDistance ||
                 (dangerDistance == bestDangerDistance && homeDistance < bestHomeDistance))
                && VisibleSiteReachable(person, Index(x, y), ref reachable))
            {
                best = Index(x, y);
                bestDangerDistance = dangerDistance;
                bestHomeDistance = homeDistance;
            }
        }

        return best;
    }

    private bool RecoveryGoalContinues(StateReference<Resident> person)
    {
        return (person.Value.Agent.Fatigue > 8 || person.Value.Agent.Sleep < ResidentNeedsRules.FullEfficiencyThreshold * ResidentNeedsRules.MaximumPercent
                || person.Value.SicknessTicks > 0 || person.Value.Health < 70)
               && (person.Value.Agent.Goal.TargetEntityId == 0
                   || (FindBuilding(person.Value.Agent.Goal.TargetEntityId) is { } clinic
                       && (clinic.Value.Kind == BuildingKind.Housing ? ResidentHome(person.Value) == clinic : IsBuildingOperational(clinic.Value))));
    }

    private void AddRecoveryChoice(StateReference<Resident> person, StateReference<Settlement> home, List<GoalChoice> choices)
    {
        if (person.Value.SicknessTicks == 0 && person.Value.Health >= 40
                                      && !(person.Value.Agent.Goal.Kind == AgentGoalKind.Rest && person.Value.Health < 70))
            return;
        IReadOnlyList<StateReference<Building>>? buildings = _localWorkQueriesActive
            ? _localWorkBuildings.GetValueOrDefault(home.Value.Id)
            : Buildings;
        StateReference<Building>? selected = null;
        var bestDistance = int.MaxValue;
        var reachable = 0;
        if (buildings is not null)
        {
            foreach (var building in buildings)
            {
                if (building.Value.SettlementId != home.Value.Id || building.Value.Kind is not (BuildingKind.Infirmary
                                                         or BuildingKind.Hospital)
                                                     || !IsBuildingOperational(building.Value))
                    continue;
                var distance = Distance(person.Value.X, person.Value.Y, building.Value.X, building.Value.Y);
                if (distance > 6 || distance >= bestDistance
                                 || !VisibleSiteReachable(person, Index(building.Value.X, building.Value.Y), ref reachable))
                    continue;
                selected = building;
                bestDistance = distance;
            }
        }

        var resting = RestDestination(person.Value);
        choices.Add(new GoalChoice(AgentGoalKind.Rest, selected?.Value.X ?? resting.X, selected?.Value.Y ?? resting.Y,
            105 + (100 - person.Value.Health) * .4,
            selected is null
                ? "患病或伤势尚未恢复，回家休养，暂缓普通劳动"
                : "患病或伤势尚未恢复，前往眼前可达的医疗设施休养并等待现场治疗",
            SettlementId: home.Value.Id, EntityId: selected?.Value.Id ?? person.Value.HomeBuildingId));
    }
}
