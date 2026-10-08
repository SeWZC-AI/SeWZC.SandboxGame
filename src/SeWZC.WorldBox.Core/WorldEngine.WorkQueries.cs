using SeWZC.WorldBox.Core.Runtime;
namespace SeWZC.WorldBox.Core;

public sealed partial class WorldEngine
{
    private readonly Dictionary<int, SettlementResearchCursor> _localResearch = [];
    private readonly Dictionary<int, BuildingCursor> _localWaterWells = [];

    private readonly List<List<BuildingCursor>> _localWorkBuildingBuffers = [];

    // 劳动索引在补给前建立，死亡、迁居及目标变化同步维护，行动阶段后清除；阶段外命令读取权威集合。
    private readonly Dictionary<int, List<BuildingCursor>> _localWorkBuildings = [];
    private readonly List<List<ResidentCursor>> _localWorkResidentBuffers = [];
    private readonly Dictionary<int, List<ResidentCursor>> _localWorkResidents = [];
    private readonly Dictionary<int, ResourceStock> _productionReserves = [];
    private readonly Dictionary<int, BuildingCursor> _workBuildingsById = [];
    private readonly Dictionary<int, int> _workReservations = [];
    private bool _localWorkQueriesActive;

    // 自主常规采集及常规设施劳动四日错峰；扑火、消防站现场维修、驻留、日常需求及交通仍逐日处理。
    private int WorkInterval(ResidentCursor person) => _localWorkQueriesActive && !person.Agent.Goal.PlayerDirected ? 4 : 1;
    private bool IsWorkDay(ResidentCursor person) => (Current.Tick + person.Id) % WorkInterval(person) == 0;

    private BuildingCursor? FindBuilding(int id)
    {
        if (id == 0) return null;
        if (_localWorkQueriesActive) return _workBuildingsById.GetValueOrDefault(id);
        foreach (var building in Current.Society.Buildings)
            if (building.Id == id) return building;
        return null;
    }

    private static int ReservedWork(in AgentGoal goal)
    {
        return goal.Kind is AgentGoalKind.Work or AgentGoalKind.Study or AgentGoalKind.TrainMagic
            ? goal.TargetEntityId
            : 0;
    }

    private void ChangeWorkReservation(in AgentGoal previous, in AgentGoal next)
    {
        if (!_localWorkQueriesActive)
            return;
        var oldId = ReservedWork(previous);
        var newId = ReservedWork(next);
        if (oldId == newId)
            return;
        if (oldId != 0)
            _workReservations[oldId] = Math.Max(0, _workReservations.GetValueOrDefault(oldId) - 1);
        if (newId != 0)
            _workReservations[newId] = _workReservations.GetValueOrDefault(newId) + 1;
    }

    private static List<T> LocalWorkGroup<T>(Dictionary<int, List<T>> groups, List<List<T>> buffers, int settlementId)
    {
        if (groups.TryGetValue(settlementId, out var group))
            return group;
        var index = groups.Count;
        if (index == buffers.Count)
            buffers.Add([]);
        group = buffers[index];
        groups.Add(settlementId, group);
        return group;
    }

    private void BeginLocalWorkQueries()
    {
        foreach (var research in Current.Society.Research)
            _localResearch[research.SettlementId] = research;
        foreach (var building in Current.Society.Buildings)
        {
            _workBuildingsById[building.Id] = building;
            if (building.Kind == BuildingKind.Well)
                _localWaterWells[Index(building.X, building.Y)] = building;
            LocalWorkGroup(_localWorkBuildings, _localWorkBuildingBuffers, building.SettlementId).Add(building);
        }

        foreach (var resident in Current.Residents)
        {
            if (resident.Health <= 0)
                continue;
            LocalWorkGroup(_localWorkResidents, _localWorkResidentBuffers, resident.SettlementId).Add(resident);
            var id = ReservedWork(resident.Agent.Goal);
            if (id != 0 && resident.ArmyId == 0)
                _workReservations[id] = _workReservations.GetValueOrDefault(id) + 1;
        }

        _localWorkQueriesActive = true;
        foreach (var town in Current.Settlements)
            _productionReserves[town.Id] = LocalDevelopmentReserve(town);
    }

    private void EndLocalWorkQueries()
    {
        _localWorkQueriesActive = false;
        _localResearch.Clear();
        _workBuildingsById.Clear();
        _localWaterWells.Clear();
        _productionReserves.Clear();
        _workReservations.Clear();
        foreach (var group in _localWorkBuildings.Values)
            group.Clear();
        foreach (var group in _localWorkResidents.Values)
            group.Clear();
        _localWorkBuildings.Clear();
        _localWorkResidents.Clear();
    }

    private void UpdateLocalWorkMembership(ResidentCursor resident, int previousSettlementId)
    {
        if (!_localWorkQueriesActive)
            return;
        if (_localWorkResidents.TryGetValue(previousSettlementId, out var previous))
            previous.Remove(resident);
        LocalWorkGroup(_localWorkResidents, _localWorkResidentBuffers, resident.SettlementId).Add(resident);
    }

    private void RemoveLocalWorkResident(ResidentCursor resident)
    {
        if (!_localWorkQueriesActive || !_localWorkResidents.TryGetValue(resident.SettlementId, out var group)
                                    || !group.Remove(resident))
            return;
        var workId = ReservedWork(resident.Agent.Goal);
        if (workId != 0 && resident.ArmyId == 0)
            _workReservations[workId] = Math.Max(0, _workReservations.GetValueOrDefault(workId) - 1);
    }

    private IReadOnlyList<ResidentCursor>? ResidentsForLocalWork(int settlementId)
    {
        return _localWorkQueriesActive
            ? _localWorkResidents.GetValueOrDefault(settlementId)
            : Current.Residents;
    }

    private BuildingCursor? FindLocalWorkBuilding(ResidentCursor resident, int range, bool preferNearest, bool followTarget = false)
    {
        IReadOnlyList<BuildingCursor>? buildings = _localWorkQueriesActive
            ? _localWorkBuildings.GetValueOrDefault(resident.SettlementId)
            : Current.Society.Buildings;
        if (buildings is null)
            return null;
        BuildingCursor? selected = null;
        var bestPriority = 0;
        var bestDistance = 0;
        var preferSpecialty = resident.Profession is Profession.Physician or Profession.Firefighter
                                  or Profession.Archivist
                                  or Profession.Surveyor or Profession.Gardener
                              && ExpansionJobHasNearbyWork(resident);
        foreach (var building in buildings)
        {
            if (followTarget && resident.Agent.Goal.TargetEntityId != 0 &&
                resident.Agent.Goal.TargetEntityId != building.Id)
                continue;
            if (building.SettlementId != resident.SettlementId || building.Health <= 0)
                continue;
            if (!followTarget && ReservedWork(resident.Agent.Goal) != building.Id && _localWorkQueriesActive
                && _workReservations.GetValueOrDefault(building.Id) >= building.WorkSlots)
                continue;
            var distance = Distance(resident.X, resident.Y, building.X, building.Y);
            if (range == 1 && IsWaterfrontBuilding(building.Kind) && (distance != 1
                                                                      || !Current.Tiles[Index(resident.X, resident.Y)]
                                                                          .IsWalkable
                                                                      || IsWaterTerrain(Current
                                                                          .Tiles[Index(resident.X, resident.Y)]
                                                                          .Terrain)))
                continue;
            var workRange = range > 1 && building.Kind is BuildingKind.MountainPass or BuildingKind.Bridge ? 24 : range;
            if (distance > workRange || !BuildingHasWork(building, resident))
                continue;
            if (range > 1 && distance <= 6 && !VisibleWorkSiteReachable(resident, building.X, building.Y,
                    !building.IsCompleted || building.IsUpgrading || IsWaterfrontBuilding(building.Kind) ||
                    building.Kind == BuildingKind.TownCenter))
                continue;
            if (range > 1 && resident.Agent.Goal.NavigationTarget == Index(building.X, building.Y) &&
                Current.Tick < resident.Agent.Goal.NavigationRetryTick)
                continue;
            var priority = WorkPriority(building, resident, preferSpecialty);
            if (selected is not null && !(priority < bestPriority || (priority == bestPriority
                                                                      && ((preferNearest && distance < bestDistance)
                                                                          || ((!preferNearest ||
                                                                               distance == bestDistance) &&
                                                                              building.Id < selected.Id)))))
                continue;
            selected = building;
            bestPriority = priority;
            bestDistance = distance;
        }

        return selected;
    }

    private ResidentCursor? FindLocalWorkPatient(BuildingCursor building, bool firstOnly = false)
    {
        var residents = ResidentsForLocalWork(building.SettlementId);
        if (residents is null)
            return null;
        ResidentCursor? selected = null;
        foreach (var patient in residents)
        {
            if (patient.Health <= 0 || patient.SettlementId != building.SettlementId || Distance(patient.X, patient.Y, building.X,
                                                                  building.Y) > 3
                                                              || !(patient.Health < 99 || patient.SicknessTicks > 0))
                continue;
            if (firstOnly)
                return patient;
            var comparison = selected is null ? -1 : patient.Health.CompareTo(selected.Health);
            if (comparison < 0 || (comparison == 0 && patient.Id < selected!.Id))
                selected = patient;
        }

        return selected;
    }

    private bool HasTwoLocalWorkers(int settlementId, Profession profession)
    {
        var residents = ResidentsForLocalWork(settlementId);
        if (residents is null)
            return false;
        var count = 0;
        foreach (var resident in residents)
            if (resident.SettlementId == settlementId && resident.Profession == profession && ++count == 2)
                return true;
        return false;
    }
}
