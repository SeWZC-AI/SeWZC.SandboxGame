namespace SeWZC.WorldBox.Core;

public sealed partial class WorldEngine
{
    public static bool IsWaterfrontBuilding(BuildingKind kind) => kind is BuildingKind.Dock or BuildingKind.Shipyard;
    private static bool IsMaterialFacility(BuildingKind kind) => kind is BuildingKind.Workshop or BuildingKind.LumberCamp or BuildingKind.Quarry;

    public double BuildingSiteScore(int settlementId, BuildingKind kind, int x, int y)
    {
        if (!_settlements.TryGetValue(settlementId, out var town) || !InBounds(x, y)) return double.NegativeInfinity;
        var tile = State.Tiles[Index(x, y)];
        var score = -Distance(x, y, town.X, town.Y) * .25 + (tile.RoadLevel > 0 ? 1 : 0);
        if (kind is BuildingKind.Farm or BuildingKind.AutomatedFarm or BuildingKind.RunicGarden)
            score += tile.Fertility / 10d - (tile.DroughtTicks > 0 ? 8 : 0);
        if (IsMaterialFacility(kind))
            foreach (var i in Circle(x, y, 1))
            {
                if (i == Index(x, y)) continue;
                var source = State.Tiles[i]; var yields = TerrainRules.For(source.Terrain);
                var yield = kind == BuildingKind.LumberCamp ? yields.WoodYield : kind == BuildingKind.Quarry ? yields.StoneYield + yields.OreYield : Math.Max(yields.WoodYield, yields.StoneYield + yields.OreYield);
                score += Math.Min(1, source.ResourceAmount / 25) * yield * 12;
            }
        if (kind == BuildingKind.Well) score += Math.Min(1, DailyWaterYield(tile)) * 15;
        if (kind is BuildingKind.SignalTower or BuildingKind.Watchtower) score += tile.Terrain == TerrainType.Hills ? 5 : tile.Elevation / 100d;
        if (kind == BuildingKind.ArcaneSanctum) score += TerrainRules.For(tile.Terrain).ManaRate * 4;
        foreach (var building in State.Society.Buildings)
        {
            var distance = Distance(x, y, building.X, building.Y);
            if (distance <= 2) score -= distance == 1 ? 4 : 1;
            if (kind is BuildingKind.Market or BuildingKind.Granary or BuildingKind.Infirmary && distance <= 4) score += 1.5;
        }
        return score;
    }

    private int BestBuildingSite(Settlement town, BuildingKind kind, bool founding = false)
    {
        var radius = founding ? 6 : Math.Max(8, town.MaxClaimRadius);
        return Circle(town.X, town.Y, radius)
            .Where(i => FacilityPlacementError(town.Id, kind, i % State.Width, i / State.Width, true) is null
                && (founding || _citizens[town.Id].Any(p => p.Health > 0 && Distance(p.X, p.Y, i % State.Width, i / State.Width) <= 6)))
            .OrderByDescending(i => BuildingSiteScore(town.Id, kind, i % State.Width, i / State.Width)).ThenBy(i => i).FirstOrDefault(-1);
    }

    private bool PassiveFacility(Building building) => building.Kind is BuildingKind.Housing or BuildingKind.Granary or BuildingKind.Watchtower;

    public int GetHousingCapacity(int settlementId)
    {
        var town = RequireTown(settlementId);
        return Math.Min(20_000, town.Housing + State.Society.Buildings
            .Where(b => b.SettlementId == settlementId && b.Kind == BuildingKind.Housing && IsFacilityOperating(b)).Sum(b => b.Level * 20));
    }

    private double GranaryRestBonus(int settlementId)
    {
        IEnumerable<Building>? buildings = _localWorkQueriesActive ? _localWorkBuildings.GetValueOrDefault(settlementId) : State.Society.Buildings;
        var bonus = 1d;
        if (buildings is null) return bonus;
        foreach (var building in buildings)
            if (building.SettlementId == settlementId && building.Kind == BuildingKind.Granary && IsFacilityOperating(building))
                bonus = Math.Max(bonus, 1 + .1 * building.Level);
        return bonus;
    }

    public double BoatTravelMultiplier(int x, int y, int nationId)
    {
        var bonus = 1d;
        foreach (var dock in State.Society.Buildings)
            if (dock.Kind == BuildingKind.Dock && IsFacilityOperating(dock) && RequireTown(dock.SettlementId).NationId == nationId
                && Distance(x, y, dock.X, dock.Y) <= 3) bonus = Math.Max(bonus, 1 + .15 * dock.Level);
        return bonus;
    }
}
