using SeWZC.WorldBox.Core.Runtime;

namespace SeWZC.WorldBox.Core;

public sealed partial class WorldEngine
{
    private int FindSafeVisibleSite(ResidentCursor person, StateReference<Settlement> home, AgentFact? danger)
    {
        var reachable = 0;
        var best = -1;
        var bestDangerDistance = -1;
        var bestHomeDistance = int.MaxValue;
        foreach (var offset in VisibleResourceOffsets)
        {
            if (offset.Distance > 5)
                break;
            var x = person.X + offset.X;
            var y = person.Y + offset.Y;
            if (!Walkable(x, y, person.Race) || Tiles[Index(x, y)].Value.FireTicks > 0)
                continue;
            var dangerDistance = Distance(x, y, danger?.X ?? person.X, danger?.Y ?? person.Y);
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

    private bool RecoveryGoalContinues(ResidentCursor person)
    {
        return (person.Agent.Fatigue > 8 || person.SicknessTicks > 0 || person.Health < 70)
               && (person.Agent.Goal.TargetEntityId == 0
                   || (FindBuilding(person.Agent.Goal.TargetEntityId) is { } clinic && IsBuildingOperational(clinic.Value)));
    }

    private void AddRecoveryChoice(ResidentCursor person, StateReference<Settlement> home, List<GoalChoice> choices)
    {
        if (person.SicknessTicks == 0 && person.Health >= 40
                                      && !(person.Agent.Goal.Kind == AgentGoalKind.Rest && person.Health < 70))
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
                var distance = Distance(person.X, person.Y, building.Value.X, building.Value.Y);
                if (distance > 6 || distance >= bestDistance
                                 || !VisibleSiteReachable(person, Index(building.Value.X, building.Value.Y), ref reachable))
                    continue;
                selected = building;
                bestDistance = distance;
            }
        }

        choices.Add(new GoalChoice(AgentGoalKind.Rest, selected?.Value.X ?? home.Value.X, selected?.Value.Y ?? home.Value.Y,
            105 + (100 - person.Health) * .4,
            selected is null
                ? "患病或伤势尚未恢复，回家休养，暂缓普通劳动"
                : "患病或伤势尚未恢复，前往眼前可达的医疗设施休养并等待现场治疗",
            SettlementId: home.Value.Id, EntityId: selected?.Value.Id ?? 0));
    }
}
