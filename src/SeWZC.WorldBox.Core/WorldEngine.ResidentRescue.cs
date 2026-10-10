using SeWZC.WorldBox.Core.Runtime;
using System.Diagnostics.CodeAnalysis;

namespace SeWZC.WorldBox.Core;

public sealed partial class WorldEngine
{
    private readonly List<StateReference<Resident>> _rescueCandidates = [];
    private readonly Dictionary<int, StateReference<Resident>> _carriedResidentsByCarrier = [];
    private readonly HashSet<int> _rescueReservations = [];
    private bool _rescueQueriesActive;

    private static bool NeedsResidentRescue(Resident person) => person.Health > 0
        && (ResidentNeedsRules.IsUnconscious(person) && !person.BedRestAfterRescue
            || person.Age < ResidentNeedsRules.MinimumOutdoorAge && !person.IsInsideHome);

    private StateReference<Resident>? ResidentCarrier(Resident person) => FindLiveResident(person.CarriedByResidentId)
        ?? ArchivedResidents.FirstOrDefault(reference => reference.Value.Id == person.CarriedByResidentId);

    private void PrepareResidentRescues()
    {
        SynchronizeResidentRescues();
        _rescueCandidates.Clear();
        _carriedResidentsByCarrier.Clear();
        _rescueReservations.Clear();
        foreach (var reference in Residents)
        {
            var person = reference.Value;
            if (person.CarriedByResidentId != 0)
                _carriedResidentsByCarrier[person.CarriedByResidentId] = reference;
            else if (NeedsResidentRescue(person) && ResidentHome(person) is not null)
                _rescueCandidates.Add(reference);
            if (person.Health > 0 && person.Agent.Goal.Kind == AgentGoalKind.Rescue)
                _rescueReservations.Add(person.Agent.Goal.TargetEntityId);
        }
        _rescueQueriesActive = true;
    }

    private void EndResidentRescueQueries()
    {
        _rescueQueriesActive = false;
        _rescueCandidates.Clear();
        _carriedResidentsByCarrier.Clear();
        _rescueReservations.Clear();
    }

    private bool IsCarryingResident(int id) => _rescueQueriesActive ? _carriedResidentsByCarrier.ContainsKey(id)
        : Residents.Any(reference => reference.Value.CarriedByResidentId == id);

    private bool TryStartResidentRescue(StateReference<Resident> reference)
    {
        var person = reference.Value;
        if (person.Agent.Goal.Kind == AgentGoalKind.Rescue || _rescueCandidates.Count == 0
            || IsCarryingResident(person.Id)
            || person.Agent.DestinationSettlementId != 0
            || person.Agent.Goal.Kind is AgentGoalKind.DeliverMessage or AgentGoalKind.Trade or AgentGoalKind.Petition or AgentGoalKind.Migrate
            || !ResidentNeedsRules.CanWork(person) || person.ArmyId != 0
            || person.Agent.Fatigue >= ResidentNeedsRules.FullEfficiencyThreshold * ResidentNeedsRules.MaximumPercent
            || person.Agent.Sleep <= ResidentNeedsRules.FullEfficiencyThreshold * ResidentNeedsRules.MaximumPercent
            || person.FrozenUntilTick > SimulationTick || !IsWorkDay(reference))
            return false;
        StateReference<Resident>? patient = null;
        var distance = 7;
        var reachable = 0;
        foreach (var candidate in _rescueCandidates)
        {
            var value = candidate.Value;
            if (value.Id == person.Id || value.SettlementId != person.SettlementId || value.CarriedByResidentId != 0
                || !NeedsResidentRescue(value) || IsCarryingResident(value.Id) || _rescueReservations.Contains(value.Id))
                continue;
            var candidateDistance = Distance(person.X, person.Y, value.X, value.Y);
            if (candidateDistance >= distance || !VisibleSiteReachable(reference, Index(value.X, value.Y), ref reachable))
                continue;
            patient = candidate;
            distance = candidateDistance;
        }
        if (patient is null)
            return false;
        var goal = new AgentGoal
        {
            Kind = AgentGoalKind.Rescue, TargetEntityId = patient.Value.Id,
            TargetX = patient.Value.X, TargetY = patient.Value.Y, TargetSettlementId = patient.Value.SettlementId,
            StartedTick = SimulationTick, ReviewTick = SimulationTick + SimulationTime.TicksPerDay,
            Reason = "发现附近需要帮助的居民，前往并背负送回已分配的住宅",
        };
        ChangeWorkReservation(person.Agent.Goal, goal);
        reference.Replace(person.WithAgent(person.Agent.WithGoal(goal) with { NextThinkTick = goal.ReviewTick }));
        _rescueReservations.Add(patient.Value.Id);
        return true;
    }

    private (StateReference<Resident> Patient, StateReference<Building> Home)? ResidentRescueDestination(StateReference<Resident> carrier)
    {
        if (_carriedResidentsByCarrier.TryGetValue(carrier.Value.Id, out var aboard)
            && aboard.Value.Id != carrier.Value.Agent.Goal.TargetEntityId)
            return null;
        var patient = FindLiveResident(carrier.Value.Agent.Goal.TargetEntityId);
        if (patient is null || patient.Value.Health <= 0 || IsCarryingResident(patient.Value.Id)
            || patient.Value.SettlementId != carrier.Value.SettlementId
            || ResidentHome(patient.Value) is not { } home
            || patient.Value.CarriedByResidentId != 0 && patient.Value.CarriedByResidentId != carrier.Value.Id
            || patient.Value.CarriedByResidentId == 0 && !NeedsResidentRescue(patient.Value))
            return null;
        return (patient, home);
    }

    private void ActOnResidentRescue(StateReference<Resident> carrier)
    {
        if (ResidentRescueDestination(carrier) is not { } destination)
        {
            FinishResidentRescue(carrier, FindLiveResident(carrier.Value.Agent.Goal.TargetEntityId));
            return;
        }
        var (patient, home) = destination;
        if (patient.Value.CarriedByResidentId == 0)
        {
            if (!ResidentHasArrived(patient.Value, SimulationTick))
            {
                carrier.Replace(carrier.Value.WithActivity(ResidentActivity.Resting));
                return;
            }
            if (Distance(carrier.Value.X, carrier.Value.Y, patient.Value.X, patient.Value.Y) > 1)
            {
                var approaching = carrier.Value.Agent.Goal with { TargetX = patient.Value.X, TargetY = patient.Value.Y };
                carrier.Replace(carrier.Value.WithAgent(carrier.Value.Agent.WithGoal(approaching)));
                MoveAgentTowards(carrier, patient.Value.X, patient.Value.Y);
                return;
            }
            patient.Replace(patient.Value.WithPosition(carrier.Value.X, carrier.Value.Y) with
            {
                CarriedByResidentId = carrier.Value.Id, IsInsideHome = false,
            });
            _carriedResidentsByCarrier[carrier.Value.Id] = patient;
        }
        var carrying = (carrier.Value.Agent.Goal with { TargetX = home.Value.X, TargetY = home.Value.Y }).ResetNavigation();
        if (carrier.Value.Agent.Goal.TargetX != home.Value.X || carrier.Value.Agent.Goal.TargetY != home.Value.Y)
            carrier.Replace(carrier.Value.WithAgent(carrier.Value.Agent.WithGoal(carrying)));
        if (carrier.Value.X != home.Value.X || carrier.Value.Y != home.Value.Y)
        {
            if (carrier.Value.Agent.Sleep < 40 || carrier.Value.Agent.Fatigue > DailyRoutineRules.RestFatigueThreshold
                || carrier.Value.Activity == ResidentActivity.Sleeping && carrier.Value.Agent.Sleep < ResidentNeedsRules.MaximumPercent)
            {
                carrier.Replace(carrier.Value.WithActivity(ResidentActivity.Sleeping));
                return;
            }
            MoveAgentTowards(carrier, home.Value.X, home.Value.Y);
            return;
        }
        patient.Replace(patient.Value.WithPosition(home.Value.X, home.Value.Y) with
        {
            CarriedByResidentId = 0, IsInsideHome = true,
            BedRestAfterRescue = ResidentNeedsRules.IsUnconscious(patient.Value),
            Activity = ResidentNeedsRules.IsUnconscious(patient.Value) ? ResidentActivity.Unconscious : ResidentActivity.Sleeping,
        });
        FinishResidentRescue(carrier, null);
    }

    private void FinishResidentRescue(StateReference<Resident> carrier, StateReference<Resident>? patient)
    {
        if (patient?.Value.CarriedByResidentId == carrier.Value.Id)
            ReleaseResidentPassenger(patient, carrier);
        if (patient?.Value.CarriedByResidentId != carrier.Value.Id)
            _carriedResidentsByCarrier.Remove(carrier.Value.Id);
        _rescueReservations.Remove(carrier.Value.Agent.Goal.TargetEntityId);
        var resumed = (carrier.Value.Agent.DailyPlan?.WorkGoal ?? new AgentGoal()).ResetNavigation();
        ChangeWorkReservation(carrier.Value.Agent.Goal, resumed);
        carrier.Replace(carrier.Value.WithAction(carrier.Value.Agent.WithGoal(resumed) with { NextThinkTick = SimulationTick },
            ResidentActivity.Resting));
        EnterResidentHome(carrier);
    }

    private void SynchronizeResidentRescues()
    {
        var archiveNeeded = false;
        foreach (var reference in Residents)
        {
            var person = reference.Value;
            if (person.CarriedByResidentId == 0)
                continue;
            var carrier = ResidentCarrier(person);
            if (person.Health <= 0 || carrier is null || carrier.Value.Health <= 0 || ResidentNeedsRules.IsUnconscious(carrier.Value)
                || carrier.Value.Agent.Goal.Kind != AgentGoalKind.Rescue || carrier.Value.Agent.Goal.TargetEntityId != person.Id
                || carrier.Value.SettlementId != person.SettlementId || ResidentHome(person) is null)
            {
                if (ReleaseResidentPassenger(reference, carrier))
                {
                    _carriedResidentsByCarrier.Remove(person.CarriedByResidentId);
                    archiveNeeded |= person.Health > 0 && reference.Value.Health <= 0 || carrier?.Value.Health <= 0;
                    continue;
                }
            }
            var moving = carrier.Value;
            reference.Replace(person with
            {
                X = moving.X, Y = moving.Y, FromX = moving.FromX, FromY = moving.FromY,
                MoveStartedTick = moving.MoveStartedTick, MoveDurationTicks = moving.MoveDurationTicks,
                MovementRoute = moving.MovementRoute, MovementCredit = 0, IsInsideHome = false,
            });
        }
        if (archiveNeeded)
            ArchiveDeadResidents();
    }

    // 只能在当地落地或邻岸放下乘客；仍在水上时保留同舟关系，不能隔空送回住宅。
    private bool ReleaseResidentPassenger(StateReference<Resident> passenger, [NotNullWhen(false)] StateReference<Resident>? carrier)
    {
        var person = passenger.Value;
        if (person.Health > 0 && carrier is { Value.Health: > 0 } && !ResidentHasArrived(person, SimulationTick))
            return false;
        if (person.Health <= 0 || CanTraverse(Tiles[Index(person.X, person.Y)].Value, person.TravelMode, person.Race))
        {
            passenger.Replace(person with { CarriedByResidentId = 0 });
            return true;
        }
        var shore = Circle(person.X, person.Y, 1).FirstOrDefault(index =>
            CanTraverse(Tiles[index].Value, person.TravelMode, person.Race) && Tiles[index].Value.FireTicks == 0, -1);
        if (shore >= 0)
        {
            passenger.Replace(person.WithPosition(shore % Width, shore / Width) with { CarriedByResidentId = 0 });
            return true;
        }
        if (carrier is { Value.Health: > 0 })
            return false;
        if (carrier is not null)
        {
            var vehicle = carrier.Value.TravelMode == TravelMode.Boat ? ResourceKind.Boats : ResourceKind.Aircraft;
            if (carrier.Value.TravelMode is TravelMode.Boat or TravelMode.Aircraft && carrier.Value.Inventory.Get(vehicle) >= 1)
            {
                var mode = carrier.Value.TravelMode;
                carrier.Replace(carrier.Value.WithInventory(carrier.Value.Inventory.WithAmount(vehicle, carrier.Value.Inventory.Get(vehicle) - 1))
                    .WithTravelMode(TravelMode.Foot));
                passenger.Replace(person with { CarriedByResidentId = 0, TravelMode = mode,
                    Inventory = person.Inventory.WithAmount(vehicle, person.Inventory.Get(vehicle) + 1) });
                return true;
            }
        }
        passenger.Replace(person with { CarriedByResidentId = 0 });
        DamageResident(passenger, person.Health, IsWaterTerrain(Tiles[Index(person.X, person.Y)].Value.Terrain)
            ? DeathCause.Drowning : DeathCause.TerrainChange);
        return true;
    }
}
