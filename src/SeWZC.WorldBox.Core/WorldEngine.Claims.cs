namespace SeWZC.WorldBox.Core;

public sealed partial class WorldEngine
{
    // A founding footprint is a single occupied site. Population only raises the
    // ceiling; it never writes ownership of a surrounding circle.
    private void ClaimTerritory(Settlement town, int radius)
    {
        town.MaxClaimRadius = Math.Max(town.MaxClaimRadius, Math.Clamp(radius, 1, 17));
        if (town.FoundationPending) return;
        var tile = State.Tiles[Index(town.X, town.Y)];
        tile.NationId = town.NationId; tile.ClaimedSettlementId = town.Id;
    }

    private bool CanClaimTile(Settlement town, int index)
    {
        var tile = State.Tiles[index]; var x = index % State.Width; var y = index / State.Width;
        if (!tile.IsWalkable || tile.FireTicks > 0 || tile.ClaimedSettlementId != 0
            || tile.NationId != 0 && tile.NationId != town.NationId
            || Distance(town.X, town.Y, x, y) > town.MaxClaimRadius) return false;
        foreach (var (dx, dy) in Directions)
            if (InBounds(x + dx, y + dy) && State.Tiles[Index(x + dx, y + dy)].ClaimedSettlementId == town.Id
                && CanTraverseStep(x + dx, y + dy, x, y, TravelMode.Foot)) return true;
        return false;
    }

    private int VisibleClaimSite(Resident person, Settlement town)
    {
        if (town.FoundationPending || !State.Rules.Expansion || person.Age < 14 || person.ArmyId != 0
            || person.Profession != Profession.Builder) return -1;
        var active = 0;
        foreach (var resident in _citizens[town.Id])
            if (resident.Id != person.Id && resident.Agent.Goal.Kind == AgentGoalKind.ClaimLand && ++active >= 2) return -1;
        foreach (var offset in VisibleResourceOffsets)
        {
            var x = person.X + offset.X; var y = person.Y + offset.Y;
            if (InBounds(x, y) && CanClaimTile(town, Index(x, y))) return Index(x, y);
        }
        return -1;
    }

    public bool TryClaimLand(Resident person)
    {
        if (!_settlements.TryGetValue(person.SettlementId, out var town) || person.Age < 14 || person.ArmyId != 0
            || person.Health <= 0 || person.Agent.Goal.Kind != AgentGoalKind.ClaimLand
            || State.Tick - person.MoveStartedTick < person.MoveDurationTicks
            || person.X != person.Agent.Goal.TargetX || person.Y != person.Agent.Goal.TargetY) return false;
        var index = Index(person.X, person.Y);
        if (State.Residents.Any(r => r.Id != person.Id && r.Health > 0 && r.SettlementId != town.Id && r.X == person.X && r.Y == person.Y)) return false;
        if (!CanClaimTile(town, index)) { person.Agent.NextThinkTick = State.Tick; return false; }
        if (person.Agent.Goal.WorkTicks < 3) return true;
        var tile = State.Tiles[index]; tile.NationId = town.NationId; tile.ClaimedSettlementId = town.Id;
        person.Agent.NextThinkTick = State.Tick;
        if (State.Tick % 12 == 0)
            AddEvent(WorldEventKind.Growth, $"{person.Name}实地为{town.Name}登记新地盘。", person.X, person.Y,
                EventAction.General, town.Id, person.Id);
        return true;
    }


    private void FinishFoundation(Resident person, Settlement town)
    {
        if (!town.FoundationPending || person.X != town.X || person.Y != town.Y || person.Agent.Goal.WorkTicks < 3) return;
        town.FoundationPending = false; ClaimTerritory(town, 4);
        AddFoundingFacility(town, BuildingKind.Farm); AddFoundingFacility(town, BuildingKind.Workshop);
        AddEvent(WorldEventKind.Founding, $"{person.Name}到场驻留后建立{town.Name}，开始实地登记地盘。", town.X, town.Y, EventAction.Completed, town.Id, person.Id);
    }

    private void RegisterBuildingGround(Building building)
    {
        if (!_settlements.TryGetValue(building.SettlementId, out var town) || town.FoundationPending) return;
        var tile = State.Tiles[Index(building.X, building.Y)];
        if (tile.NationId != 0 && tile.NationId != town.NationId) return;
        tile.NationId = town.NationId;
        if (tile.ClaimedSettlementId == 0) tile.ClaimedSettlementId = town.Id;
    }
}
