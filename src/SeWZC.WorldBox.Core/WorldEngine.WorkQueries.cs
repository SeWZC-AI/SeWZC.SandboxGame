namespace SeWZC.WorldBox.Core;

public sealed partial class WorldEngine
{
    // Scratch groups are rebuilt after resident deaths and cleared after the agent phase.
    // Outside that phase public work commands read the authoritative collections directly.
    private readonly Dictionary<int, List<Building>> _localWorkBuildings = [];
    private readonly Dictionary<int, List<Resident>> _localWorkResidents = [];
    private readonly List<List<Building>> _localWorkBuildingBuffers = [];
    private readonly List<List<Resident>> _localWorkResidentBuffers = [];
    private bool _localWorkQueriesActive;
    private readonly Dictionary<int, SettlementResearch> _localResearch = [];

    private static List<T> LocalWorkGroup<T>(Dictionary<int, List<T>> groups, List<List<T>> buffers, int settlementId)
    {
        if (groups.TryGetValue(settlementId, out var group)) return group;
        var index = groups.Count;
        if (index == buffers.Count) buffers.Add([]);
        group = buffers[index];
        groups.Add(settlementId, group);
        return group;
    }

    private void BeginLocalWorkQueries()
    {
        foreach (var research in State.Society.Research) _localResearch[research.SettlementId] = research;
        foreach (var building in State.Society.Buildings)
            LocalWorkGroup(_localWorkBuildings, _localWorkBuildingBuffers, building.SettlementId).Add(building);
        foreach (var resident in State.Residents)
            LocalWorkGroup(_localWorkResidents, _localWorkResidentBuffers, resident.SettlementId).Add(resident);
        _localWorkQueriesActive = true;
    }

    private void EndLocalWorkQueries()
    {
        _localWorkQueriesActive = false; _localResearch.Clear();
        foreach (var group in _localWorkBuildings.Values) group.Clear();
        foreach (var group in _localWorkResidents.Values) group.Clear();
        _localWorkBuildings.Clear();
        _localWorkResidents.Clear();
    }

    private void UpdateLocalWorkMembership(Resident resident, int previousSettlementId)
    {
        if (!_localWorkQueriesActive) return;
        if (_localWorkResidents.TryGetValue(previousSettlementId, out var previous)) previous.Remove(resident);
        LocalWorkGroup(_localWorkResidents, _localWorkResidentBuffers, resident.SettlementId).Add(resident);
    }

    private List<Resident>? ResidentsForLocalWork(int settlementId) => _localWorkQueriesActive
        ? _localWorkResidents.GetValueOrDefault(settlementId) : State.Residents;

    private Building? FindLocalWorkBuilding(Resident resident, int range, bool preferNearest, bool followTarget = false)
    {
        var buildings = _localWorkQueriesActive
            ? _localWorkBuildings.GetValueOrDefault(resident.SettlementId) : State.Society.Buildings;
        if (buildings is null) return null;
        Building? selected = null;
        var bestPriority = 0;
        var bestDistance = 0;
        foreach (var building in buildings)
        {
            if (followTarget && resident.Agent.Goal.TargetEntityId != 0 && resident.Agent.Goal.TargetEntityId != building.Id) continue;
            if (building.SettlementId != resident.SettlementId || building.Health <= 0) continue;
            var distance = Distance(resident.X, resident.Y, building.X, building.Y);
            var workRange = range > 1 && building.Kind is BuildingKind.Bridge or BuildingKind.MountainPass ? 24 : range;
            if (distance > workRange || !BuildingHasWork(building, resident)) continue;
            var priority = WorkPriority(building, resident);
            if (selected is not null && !(priority < bestPriority || priority == bestPriority
                && (preferNearest && distance < bestDistance
                    || (!preferNearest || distance == bestDistance) && building.Id < selected.Id))) continue;
            selected = building;
            bestPriority = priority;
            bestDistance = distance;
        }
        return selected;
    }

    private Resident? FindLocalWorkPatient(Building building, bool firstOnly = false)
    {
        var residents = ResidentsForLocalWork(building.SettlementId);
        if (residents is null) return null;
        Resident? selected = null;
        foreach (var patient in residents)
        {
            if (patient.SettlementId != building.SettlementId || Distance(patient.X, patient.Y, building.X, building.Y) > 3
                || !(patient.Health < 99 || patient.SicknessTicks > 0)) continue;
            if (firstOnly) return patient;
            var comparison = selected is null ? -1 : patient.Health.CompareTo(selected.Health);
            if (comparison < 0 || comparison == 0 && patient.Id < selected!.Id) selected = patient;
        }
        return selected;
    }

    private bool HasTwoLocalWorkers(int settlementId, Profession profession)
    {
        var residents = ResidentsForLocalWork(settlementId);
        if (residents is null) return false;
        var count = 0;
        foreach (var resident in residents)
            if (resident.SettlementId == settlementId && resident.Profession == profession && ++count == 2) return true;
        return false;
    }
}
