using SeWZC.WorldBox.Core.Runtime;

namespace SeWZC.WorldBox.Core;

public sealed partial class WorldEngine
{
    private readonly Dictionary<int, int> _localResearch = [];
    private readonly Dictionary<int, StateReference<Building>> _localWaterWells = [];

    private readonly List<List<StateReference<Building>>> _localWorkBuildingBuffers = [];

    // 劳动索引在补给前建立，死亡、迁居及目标变化同步维护，行动阶段后清除；阶段外命令读取权威集合。
    private readonly Dictionary<int, List<StateReference<Building>>> _localWorkBuildings = [];
    private readonly List<List<StateReference<Resident>>> _localWorkResidentBuffers = [];
    private readonly Dictionary<int, List<StateReference<Resident>>> _localWorkResidents = [];
    private readonly Dictionary<int, ResourceStock> _productionReserves = [];
    private readonly Dictionary<int, StateReference<Building>> _workBuildingsById = [];
    private readonly Dictionary<int, int> _workReservations = [];
    private bool _localWorkQueriesActive;

    // 自主常规采集及常规设施劳动每四 tick 错峰；扑火、消防站现场维修、驻留、需求及交通仍逐 tick 处理。
    private int WorkInterval(StateReference<Resident> person)
    {
        return _localWorkQueriesActive && !person.Value.Agent.Goal.PlayerDirected ? 4 : 1;
    }

    private bool IsWorkDay(StateReference<Resident> person)
    {
        return (SimulationTick + person.Value.Id) % WorkInterval(person) == 0;
    }

    // 自主劳动的日产量按白天班次折算，避免加入夜间睡眠后把原有日供给再减半。
    private double WorkDays(StateReference<Resident> person, bool mental = false)
    {
        return WorkInterval(person) / (double)(
            person.Value.Agent.Goal.PlayerDirected
                ? SimulationTime.TicksPerDay
                : SimulationTime.ReturnHomeTick - SimulationTime.WakeTick) * ResidentNeedsRules.WorkEfficiency(person.Value, mental);
    }

    private StateReference<Building>? FindBuilding(int id)
    {
        if (id == 0)
            return null;
        if (_localWorkQueriesActive)
            return _workBuildingsById.GetValueOrDefault(id);
        foreach (var building in Buildings)
            if (building.Value.Id == id)
                return building;
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
        for (var index = 0; index < Society.Research.Count; index++)
            _localResearch[Society.Research[index].SettlementId] = index;
        foreach (var building in Buildings)
        {
            _workBuildingsById[building.Value.Id] = building;
            if (building.Value.Kind == BuildingKind.Well)
                _localWaterWells[Index(building.Value.X, building.Value.Y)] = building;
            LocalWorkGroup(_localWorkBuildings, _localWorkBuildingBuffers, building.Value.SettlementId).Add(building);
        }

        foreach (var resident in Residents)
        {
            if (resident.Value.Health <= 0)
                continue;
            LocalWorkGroup(_localWorkResidents, _localWorkResidentBuffers, resident.Value.SettlementId).Add(resident);
            var id = ReservedWork(resident.Value.Agent.Goal);
            if (id != 0 && resident.Value.ArmyId == 0)
                _workReservations[id] = _workReservations.GetValueOrDefault(id) + 1;
        }

        _localWorkQueriesActive = true;
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

    private void UpdateLocalWorkMembership(StateReference<Resident> resident, int previousSettlementId)
    {
        if (resident.Value.Agent.WorkplaceId != 0 || resident.Value.Agent.WorkAreaIndex != -1)
            resident.Replace(resident.Value.WithAgent(resident.Value.Agent with { WorkplaceId = 0, WorkAreaIndex = -1 }));
        if (!_localWorkQueriesActive)
            return;
        if (_localWorkResidents.TryGetValue(previousSettlementId, out var previous))
            previous.Remove(resident);
        LocalWorkGroup(_localWorkResidents, _localWorkResidentBuffers, resident.Value.SettlementId).Add(resident);
    }

    private void RemoveLocalWorkResident(StateReference<Resident> resident)
    {
        if (!_localWorkQueriesActive || !_localWorkResidents.TryGetValue(resident.Value.SettlementId, out var group)
                                     || !group.Remove(resident))
            return;
        var workId = ReservedWork(resident.Value.Agent.Goal);
        if (workId != 0 && resident.Value.ArmyId == 0)
            _workReservations[workId] = Math.Max(0, _workReservations.GetValueOrDefault(workId) - 1);
    }

    private IReadOnlyList<StateReference<Resident>>? ResidentsForLocalWork(int settlementId)
    {
        return _localWorkQueriesActive
            ? _localWorkResidents.GetValueOrDefault(settlementId)
            : Residents;
    }

    private static bool HasActiveResearchProject(ImmutableVector<SettlementResearch> research, int settlementId)
    {
        for (var index = 0; index < research.Count; index++)
            if (research[index].SettlementId == settlementId && research[index].ActiveProject is not null)
                return true;
        return false;
    }

    private StateReference<Building>? FindLocalWorkBuilding(StateReference<Resident> resident, int range, bool preferNearest,
        bool followTarget = false)
    {
        IReadOnlyList<StateReference<Building>>? buildings = _localWorkQueriesActive
            ? _localWorkBuildings.GetValueOrDefault(resident.Value.SettlementId)
            : Buildings;
        if (buildings is null)
            return null;
        StateReference<Building>? selected = null;
        var bestPriority = 0;
        var bestDistance = 0;
        var preferSpecialty = resident.Value.Profession is Profession.Physician or Profession.Firefighter
                                  or Profession.Archivist
                                  or Profession.Surveyor or Profession.Gardener
                              && ExpansionJobHasNearbyWork(resident);
        for (var index = 0; index < buildings.Count; index++)
        {
            var building = buildings[index];
            if (followTarget && resident.Value.Agent.Goal.TargetEntityId != 0 &&
                resident.Value.Agent.Goal.TargetEntityId != building.Value.Id)
                continue;
            if (building.Value.SettlementId != resident.Value.SettlementId || building.Value.Health <= 0)
                continue;
            if (!followTarget && ReservedWork(resident.Value.Agent.Goal) != building.Value.Id && _localWorkQueriesActive
                && _workReservations.GetValueOrDefault(building.Value.Id) >= building.Value.WorkSlots)
                continue;
            var distance = Distance(resident.Value.X, resident.Value.Y, building.Value.X, building.Value.Y);
            if (range == 1 && IsWaterfrontBuilding(building.Value.Kind) && (distance != 1
                                                                      || !Tiles[Index(resident.Value.X, resident.Value.Y)].Value
                                                                          .IsWalkable
                                                                      || IsWaterTerrain(Tiles[Index(resident.Value.X, resident.Value.Y)].Value
                                                                          .Terrain)))
                continue;
            var workRange = range > 1 && building.Value.Kind is BuildingKind.MountainPass or BuildingKind.Bridge ? 24 : range;
            if (distance > workRange || !BuildingHasWork(building.Value, resident))
                continue;
            if (range > 1 && distance <= 6 && !VisibleWorkSiteReachable(resident, building.Value.X, building.Value.Y,
                    !building.Value.IsCompleted || building.Value.IsUpgrading || IsWaterfrontBuilding(building.Value.Kind) ||
                    building.Value.Kind == BuildingKind.TownCenter))
                continue;
            if (range > 1 && resident.Value.Agent.Goal.NavigationTarget == Index(building.Value.X, building.Value.Y) &&
                SimulationTick < resident.Value.Agent.Goal.NavigationRetryTick)
                continue;
            var priority = WorkPriority(building.Value, resident, preferSpecialty) * 2
                           + (resident.Value.Agent.WorkplaceId == building.Value.Id ? 0 : 1);
            if (selected is not null && !(priority < bestPriority || (priority == bestPriority
                                                                      && ((preferNearest && distance < bestDistance)
                                                                          || ((!preferNearest ||
                                                                               distance == bestDistance) &&
                                                                              building.Value.Id < selected.Value.Id)))))
                continue;
            selected = building;
            bestPriority = priority;
            bestDistance = distance;
        }

        return selected;
    }

    private StateReference<Resident>? FindLocalWorkPatient(Building building, bool firstOnly = false)
    {
        var residents = ResidentsForLocalWork(building.SettlementId);
        if (residents is null)
            return null;
        StateReference<Resident>? selected = null;
        for (var index = 0; index < residents.Count; index++)
        {
            var patient = residents[index];
            var health = patient.Value.Health;
            if (health <= 0 || !(health < 99 || patient.Value.SicknessTicks > 0)
                || patient.Value.SettlementId != building.SettlementId
                || Distance(patient.Value.X, patient.Value.Y, building.X, building.Y) > 3)
                continue;
            if (firstOnly)
                return patient;
            var comparison = selected is null ? -1 : health.CompareTo(selected.Value.Health);
            if (comparison < 0 || (comparison == 0 && patient.Value.Id < selected!.Value.Id))
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
        for (var index = 0; index < residents.Count; index++)
        {
            var resident = residents[index];
            if (resident.Value.SettlementId == settlementId && resident.Value.Profession == profession && ++count == 2)
                return true;
        }
        return false;
    }
}
