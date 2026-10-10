using SeWZC.WorldBox.Core.Runtime;

namespace SeWZC.WorldBox.Core;

public sealed partial class WorldEngine
{
    private void AssignResidentHomes()
    {
        var homes = Buildings.Where(reference => reference.Value.Kind == BuildingKind.Housing
                                                 && IsFacilityOperating(reference.Value)
                                                 && Tiles[Index(reference.Value.X, reference.Value.Y)].Value.FireTicks == 0)
            .ToDictionary(reference => reference.Value.Id);
        var occupied = new Dictionary<int, int>();
        foreach (var reference in Residents)
        {
            var person = reference.Value;
            if (person.Health <= 0)
                continue;
            if (homes.TryGetValue(person.HomeBuildingId, out var home)
                && home.Value.SettlementId == person.SettlementId
                && ResidentCanReachHome(reference, home.Value)
                && occupied.GetValueOrDefault(home.Value.Id) < home.Value.Level * HousingCapacityPerLevel)
            {
                occupied[home.Value.Id] = occupied.GetValueOrDefault(home.Value.Id) + 1;
                if (person.IsInsideHome && (person.X != home.Value.X || person.Y != home.Value.Y))
                    reference.Replace(person with { IsInsideHome = false, BedRestAfterRescue = false });
                continue;
            }
            if (person.HomeBuildingId != 0 || person.IsInsideHome)
                reference.Replace(person with
                {
                    HomeBuildingId = 0, IsInsideHome = false, BedRestAfterRescue = false,
                    Agent = person.Agent with { DailyPlan = null },
                });
        }

        foreach (var reference in Residents)
        {
            var person = reference.Value;
            if (person.Health <= 0 || person.HomeBuildingId != 0)
                continue;
            StateReference<Building>? selected = null;
            var distance = int.MaxValue;
            foreach (var home in homes.Values)
            {
                if (home.Value.SettlementId != person.SettlementId
                    || occupied.GetValueOrDefault(home.Value.Id) >= home.Value.Level * HousingCapacityPerLevel
                    || !ResidentCanReachHome(reference, home.Value))
                    continue;
                var candidateDistance = Distance(person.X, person.Y, home.Value.X, home.Value.Y);
                if (candidateDistance >= distance)
                    continue;
                selected = home;
                distance = candidateDistance;
            }
            if (selected is null)
                continue;
            occupied[selected.Value.Id] = occupied.GetValueOrDefault(selected.Value.Id) + 1;
            var assigned = person with { HomeBuildingId = selected.Value.Id, Agent = person.Agent with { DailyPlan = null } };
            if (!person.Agent.Initialized && person.Age < ResidentNeedsRules.MinimumOutdoorAge)
                assigned = assigned.WithPosition(selected.Value.X, selected.Value.Y) with { IsInsideHome = true };
            reference.Replace(assigned);
        }
    }

    private bool ResidentCanReachHome(StateReference<Resident> reference, Building home)
    {
        var person = reference.Value;
        if (!Walkable(home.X, home.Y, person.Race))
            return false;
        var traveler = person.CarriedByResidentId == 0 ? reference : ResidentCarrier(person) ?? reference;
        // 只核实眼前通路；远处住宅接近后再检查，沿用居民的局部导航。
        if (Distance(traveler.Value.X, traveler.Value.Y, home.X, home.Y) > 6)
            return true;
        var search = 0;
        return VisibleSiteReachable(traveler, Index(home.X, home.Y), ref search);
    }

    private StateReference<Building>? ResidentHome(Resident person)
    {
        var home = FindBuilding(person.HomeBuildingId);
        return home is not null && home.Value.Kind == BuildingKind.Housing
                               && home.Value.SettlementId == person.SettlementId
                               && IsFacilityOperating(home.Value)
                               && Tiles[Index(home.Value.X, home.Value.Y)].Value.FireTicks == 0 ? home : null;
    }

    /// <summary>返回居民已分配且仍可居住的住宅。</summary>
    /// <param name="residentId">居民编号。</param>
    public Building? GetResidentHome(int residentId) => GetResident(residentId) is { } person ? ResidentHome(person)?.Value : null;

    /// <summary>计算住宅中已分配的存活居民人数。</summary>
    /// <param name="buildingId">住宅编号。</param>
    public int GetHousingOccupancy(int buildingId) => Residents.Count(reference =>
        reference.Value.Health > 0 && reference.Value.HomeBuildingId == buildingId);

    private bool EnterResidentHome(StateReference<Resident> person)
    {
        if (ResidentHome(person.Value) is not { } home || person.Value.X != home.Value.X || person.Value.Y != home.Value.Y
            || !ResidentHasArrived(person.Value, SimulationTick))
            return false;
        if (!person.Value.IsInsideHome)
            person.Replace(person.Value with { IsInsideHome = true, MovementCredit = 0 });
        return true;
    }

    private (int X, int Y) RestDestination(Resident person)
    {
        var home = ResidentHome(person);
        return home is null ? (person.X, person.Y) : (home.Value.X, home.Value.Y);
    }

    private static bool ResidentHasArrived(Resident person, long tick) =>
        (person.MovementRoute.IsEmpty && person.FromX == person.X && person.FromY == person.Y)
        || person.MoveStartedTick + person.MoveDurationTicks <= tick;
}
