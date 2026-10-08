using SeWZC.WorldBox.Core.Runtime;

namespace SeWZC.WorldBox.Core;

public sealed partial class WorldEngine
{
    private int FindSafeVisibleSite(ResidentCursor person, SettlementCursor home, AgentFact? danger)
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
            if (!Walkable(x, y, person.Race) || Current.Tiles[Index(x, y)].FireTicks > 0)
                continue;
            var dangerDistance = Distance(x, y, danger?.X ?? person.X, danger?.Y ?? person.Y);
            var homeDistance = Distance(x, y, home.X, home.Y);
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
                   || (FindBuilding(person.Agent.Goal.TargetEntityId) is { } clinic && IsBuildingOperational(clinic)));
    }

    private void AddRecoveryChoice(ResidentCursor person, SettlementCursor home, List<GoalChoice> choices)
    {
        if (person.SicknessTicks == 0 && person.Health >= 40
                                      && !(person.Agent.Goal.Kind == AgentGoalKind.Rest && person.Health < 70))
            return;
        IReadOnlyList<BuildingCursor>? buildings = _localWorkQueriesActive
            ? _localWorkBuildings.GetValueOrDefault(home.Id)
            : Current.Society.Buildings;
        BuildingCursor? selected = null;
        var bestDistance = int.MaxValue;
        var reachable = 0;
        if (buildings is not null)
        {
            foreach (var building in buildings)
            {
                if (building.SettlementId != home.Id || building.Kind is not (BuildingKind.Infirmary
                                                         or BuildingKind.Hospital)
                                                     || !IsBuildingOperational(building))
                    continue;
                var distance = Distance(person.X, person.Y, building.X, building.Y);
                if (distance > 6 || distance >= bestDistance
                                 || !VisibleSiteReachable(person, Index(building.X, building.Y), ref reachable))
                    continue;
                selected = building;
                bestDistance = distance;
            }
        }

        choices.Add(new GoalChoice(AgentGoalKind.Rest, selected?.X ?? home.X, selected?.Y ?? home.Y,
            105 + (100 - person.Health) * .4,
            selected is null
                ? "患病或伤势尚未恢复，回家休养，暂缓普通劳动"
                : "患病或伤势尚未恢复，前往眼前可达的医疗设施休养并等待现场治疗",
            SettlementId: home.Id, EntityId: selected?.Id ?? 0));
    }
}
