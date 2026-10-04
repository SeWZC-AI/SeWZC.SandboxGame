using System.Text.Json.Nodes;
using SeWZC.WorldBox.Core;

internal static class ProvisioningAndClaimsTests
{
    public static IEnumerable<(string Name, Action Run)> Cases =>
    [
        ("claims require actual arrival and three days while population only raises the ceiling", Claims),
        ("an idle builder autonomously claims visible land after occupied production slots", AutonomousClaim),
        ("river and lake water is unlimited but requires local collection and transport", Water),
        ("hunting and fishing consume actual populations and cannot harvest remotely", Harvest),
        ("bridges enforce both endpoints' axes and natural shore limits through upgrades", Bridges),
        ("paid upgrades require actual labor and survive mid-project saves", Upgrades),
        ("autonomous towns upgrade used facilities after meeting development needs", AutonomousUpgrade),
        ("fertility and nonlinear water generation are independent and remain fixed after creation", Terrain),
        ("animal migration enters crowded habitat while plant migration runs once per year", Migration),
        ("effects and task inspection are read-only and corrupt new state is rejected", Inspection),
        ("carried supplies sustain a long journey and survival switches stop consumption", Journey)
    ];

    private static void Check(bool value, string message) { if (!value) throw new Exception(message); }
    private static WorldEngine Flat(int population = 3)
    {
        var e = WorldEngine.Create(42, 32, 32, false);
        foreach (var t in e.State.Tiles)
        {
            t.Terrain = TerrainType.Grass; t.Fertility = 80; t.ResourceAmount = 100;
            t.Wildlife = WildlifeKind.None; t.WildlifePopulation = 0; t.OtherWildlife = default;
            t.NaturalWaterYield = .001; t.Plants = new() { Grass = .6 };
        }
        e.ConfigureWorld(new WorldRules { Aging = false, Births = false, Hunger = false, Thirst = false,
            Disease = false, Construction = false, Research = false, Expansion = false, Migration = false,
            Trade = false, Wars = false, Peace = false, Alliances = false, Secession = false,
            ResourceRegeneration = false }, false, false);
        e.State.Tick = 1;
        e.SpawnResidents(14, 16, RaceKind.Human, population);
        foreach (var r in e.State.Residents) Hold(e, r, AgentGoalKind.Rest, r.X, r.Y);
        return e;
    }
    private static void Hold(WorldEngine e, Resident r, AgentGoalKind kind, int x, int y, int entity = 0)
    {
        r.X = r.FromX = x; r.Y = r.FromY = y; r.Age = 25;
        r.MoveStartedTick = Math.Max(0, e.State.Tick - 1); r.MoveDurationTicks = 1;
        r.Agent.Goal = new() { Kind = kind, TargetX = x, TargetY = y, TargetEntityId = entity,
            TargetSettlementId = r.SettlementId, StartedTick = e.State.Tick, ReviewTick = e.State.Tick + 1000, PlayerDirected = true };
    }

    private static void Claims()
    {
        var e = Flat(60); var town = e.State.Settlements.Single();
        e.State.Rules.Expansion = true;
        var initial = e.State.Tiles.Count(t => t.NationId == town.NationId);
        var radius = town.MaxClaimRadius; e.Step(30);
        Check(town.MaxClaimRadius > radius && e.State.Tiles.Count(t => t.NationId == town.NationId) == initial,
            "Population wrote ownership without a resident's occupation.");
        var r = e.State.Residents[0]; var tile = e.State.Tiles[17 * 32 + 14];
        Check(tile.NationId == 0, "Claim fixture is already owned.");
        Hold(e, r, AgentGoalKind.ClaimLand, 14, 17); r.Agent.Goal.TargetX = 15;
        Check(!e.TryClaimLand(r) && tile.NationId == 0, "A remote claim succeeded.");
        Hold(e, r, AgentGoalKind.ClaimLand, 14, 17);
        e.Step(2); Check(tile.NationId == 0, "Claim did not wait for the resident's stay.");
        e.Step(); Check(tile.NationId == town.NationId && tile.ClaimedSettlementId == town.Id, "Physical claim failed.");
        var resumed = WorldEngine.ImportJson(e.ExportJson()); e.Step(6); resumed.Step(6);
        Check(e.ExportJson() == resumed.ExportJson(), "Claims diverged after resume.");
    }

    private static void AutonomousClaim()
    {
        var e = Flat(6); var town = e.State.Settlements.Single();
        var farm = e.State.Society.Buildings.First(b => b.Kind == BuildingKind.Farm);
        foreach (var building in e.State.Society.Buildings.Where(b => b.Kind == BuildingKind.Workshop)) building.Enabled = false;
        for (var i = 0; i < farm.WorkSlots; i++)
        {
            var farmer = e.State.Residents[i]; farmer.Profession = Profession.Farmer;
            Hold(e, farmer, AgentGoalKind.Work, farm.X, farm.Y, farm.Id);
        }
        var builder = e.State.Residents[farm.WorkSlots]; builder.Profession = Profession.Builder;
        builder.Inventory.Food = 2; builder.Agent.Fatigue = builder.Agent.SocialNeed = 0;
        builder.Agent.Goal = new() { Kind = AgentGoalKind.Idle }; builder.Agent.NextThinkTick = e.State.Tick;
        var original = e.State.Tiles.Count(t => t.ClaimedSettlementId == town.Id);
        e.State.Rules.Expansion = true; e.Step(24);
        Check(e.State.Tiles.Count(t => t.ClaimedSettlementId == town.Id) > original
            && builder.Agent.Decisions.Any(d => d.Goal == AgentGoalKind.ClaimLand),
            "An idle builder never autonomously selected and completed a physical claim.");
    }

    private static void Water()
    {
        foreach (var terrain in new[] { TerrainType.River, TerrainType.Lake })
        {
            var e = Flat(6); var source = 16 * 32 + 17;
            var water = e.State.Tiles[source]; water.Terrain = terrain; water.NaturalWaterYield = 0; water.Plants = default;
            // Current-format saves may still contain the former four-unit quota.
            water.WaterDrawTick = e.State.Tick; water.WaterDrawn = terrain == TerrainType.River ? 4 : 0; water.DroughtTicks = 12;
            foreach (var r in e.State.Residents) Hold(e, r, AgentGoalKind.Rest, 16, 16);
            e = WorldEngine.ImportJson(e.ExportJson());
            var home = e.State.Settlements.Single();
            foreach (var r in e.State.Residents)
            {
                Hold(e, r, AgentGoalKind.FetchWater, 16, 16, source + 1);
                Check(e.TryFetchWater(r) && r.Inventory.Water == 1, "A nearby carrier could not collect a finite load.");
            }
            Check(e.State.Residents.Sum(r => r.Inventory.Water) == 6 && double.IsPositiveInfinity(e.AvailableWater(17, 16)),
                "The shared daily quota or drought still depleted fresh water.");
            var carrier = e.State.Residents[0]; var collected = carrier.Inventory.Water;
            Hold(e, carrier, AgentGoalKind.FetchWater, 15, 16, source + 1);
            Check(!e.TryFetchWater(carrier) && carrier.Inventory.Water == collected, "Water was collected away from the bank.");
            Hold(e, carrier, AgentGoalKind.FetchWater, 16, 16, source + 1); carrier.MoveStartedTick = e.State.Tick;
            Check(!e.TryFetchWater(carrier) && carrier.Inventory.Water == collected, "Water was collected before arrival.");
            foreach (var r in e.State.Residents) Hold(e, r, AgentGoalKind.Rest, r.X, r.Y);
            carrier.Inventory.Water = 5;
            var before = home.Resources.Water; Hold(e, carrier, AgentGoalKind.ReturnHome, home.X, home.Y);
            e.Step(); Check(home.Resources.Water > before && carrier.Inventory.Water > 0, "Water was not physically returned with a travel reserve.");
            Check(double.IsPositiveInfinity(e.AvailableWater(17, 16)), "Fresh water became finite on the next day.");
            var trace = e.AvailableWater(2, 2);
            var resumed = WorldEngine.ImportJson(e.ExportJson()); e.Step(120); resumed.Step(120);
            Check(e.ExportJson() == resumed.ExportJson(), "Unlimited water failed current-format saving and deterministic resume.");
            Check(e.AvailableWater(2, 2) == trace, "Unused trace water accumulated.");
        }
    }

    [UnitTest]
    private static void Harvest()
    {
        var e = Flat(1); var r = e.State.Residents.Single(); var t = e.State.Tiles[16 * 32 + 20];
        t.Wildlife = WildlifeKind.Deer; t.WildlifePopulation = 1;
        Hold(e, r, AgentGoalKind.Hunt, 19, 16, 16 * 32 + 20 + 1);
        Check(!e.TryHarvestWildlife(r) && t.WildlifePopulation == 1, "Remote hunting succeeded.");
        Hold(e, r, AgentGoalKind.Hunt, 20, 16, 16 * 32 + 20 + 1);
        Check(e.TryHarvestWildlife(r) && Math.Abs(r.Inventory.Food - (1 - t.WildlifePopulation) * 2) < 1e-9, "Hunting did not conserve yield.");
        t.Terrain = TerrainType.Lake; t.Wildlife = WildlifeKind.Fish; t.WildlifePopulation = 1;
        Hold(e, r, AgentGoalKind.Fish, 19, 16, 16 * 32 + 20 + 1);
        var food = r.Inventory.Food;
        Check(e.TryHarvestWildlife(r) && Math.Abs(r.Inventory.Food - food - (1 - t.WildlifePopulation)) < 1e-9, "Fishing did not reduce fish.");
    }

    [UnitTest]
    private static void Bridges()
    {
        var e = Flat(); var home = e.State.Settlements.Single();
        for (var x = 17; x <= 24; x++) e.PaintTerrain(x, 16, TerrainType.River, 0);
        e.GrantFacility(home.Id, BuildingKind.Bridge, 17, 16, BridgeDirection.Horizontal);
        e.GrantFacility(home.Id, BuildingKind.Bridge, 18, 16, BridgeDirection.Horizontal);
        Check(e.BridgePlacementError(19, 16, BridgeDirection.Horizontal) is not null, "A bridge reset the natural shore distance.");
        e.GrantFacility(home.Id, BuildingKind.Bridge, 19, 16, BridgeDirection.Horizontal, 2);
        var b = e.State.Society.Buildings.Single(b => b.X == 17 && b.Y == 16);
        Check(e.CanTraverseStep(16, 16, 17, 16, TravelMode.Foot) && !e.CanTraverseStep(17, 15, 17, 16, TravelMode.Foot), "Bridge allowed a forbidden axis.");
        e.UpgradeBuilding(b.Id, true); e.UpgradeBuilding(b.Id, true, BridgeDirection.Vertical);
        Check(b.Level == 2 && e.State.Tiles[16 * 32 + 17].BridgeLevel == 2
            && !e.CanTraverseStep(17, 16, 18, 16, TravelMode.Foot)
            && e.CanTraverseStep(17, 15, 17, 16, TravelMode.Foot), "Reorientation did not change both-endpoint path constraints.");
        _ = WorldEngine.ImportJson(e.ExportJson());
    }

    private static void Upgrades()
    {
        var e = Flat(1); var town = e.State.Settlements.Single(); var b = e.State.Society.Buildings.First(b => b.Kind == BuildingKind.Farm);
        town.Resources.Wood = town.Resources.Stone = 500; var cost = WorldEngine.GetUpgradeCost(b); var wood = town.Resources.Wood;
        e.UpgradeBuilding(b.Id);
        Check(town.Resources.Wood == wood - cost.Wood && b.Level == 1 && b.IsUpgrading, "Upgrade bypassed payment or labor.");
        var r = e.State.Residents.Single(); r.Profession = Profession.Builder;
        Hold(e, r, AgentGoalKind.Work, b.X, b.Y, b.Id); e.Step(8);
        Check(b.UpgradeProgress > 0 && b.Level == 1, "No physical upgrade work occurred.");
        var resumed = WorldEngine.ImportJson(e.ExportJson()); e.Step(80); resumed.Step(80);
        Check(e.ExportJson() == resumed.ExportJson() && b.Level == 2 && !b.IsUpgrading && b.WorkSlots == 6, "Upgrade did not complete identically after resume.");
    }

    private static void Terrain()
    {
        var e = WorldEngine.Create(42, 128, 128, false);
        Check(e.State.Tiles.Any(t => t.Terrain == TerrainType.Lake) && e.State.Tiles.Any(t => t.Terrain == TerrainType.DryFertile), "Missing lakes or dry fertile terrain.");
        Check(e.State.Tiles.Any(t => t.Fertility >= 70 && t.NaturalWaterYield < .004)
            && e.State.Tiles.Any(t => t.Fertility < 55 && t.NaturalWaterYield > .012), "Water and soil are coupled.");
        var rates = e.State.Tiles.Select(t => t.NaturalWaterYield).ToArray();
        e.PaintTerrain(20, 20, TerrainType.Lake, 0); e.Step(120);
        Check(e.State.Tiles.Where((t, i) => i != 20 * 128 + 20).Select(t => t.NaturalWaterYield)
            .SequenceEqual(rates.Where((_, i) => i != 20 * 128 + 20)), "The creation-time water-distance field was recomputed.");
    }

    private static void AutonomousUpgrade()
    {
        var e = Flat(6); var town = e.State.Settlements.Single();
        e.State.Society.MagicEnabled = true;
        var research = e.State.Society.Research.Single(); research.Completed = Enum.GetValues<ResearchKind>().ToList();
        foreach (var kind in AdvancementRules.Resources) town.Resources.Set(kind, 10_000);
        e.PaintTerrain(18, 16, TerrainType.River, 0);
        foreach (var kind in Enum.GetValues<BuildingKind>())
        {
            if (kind is BuildingKind.Bridge or BuildingKind.MountainPass || e.State.Society.Buildings.Any(b => b.Kind == kind)) continue;
            var site = Enumerable.Range(0, e.State.Tiles.Length).First(i => e.FacilityPlacementError(town.Id, kind, i % 32, i / 32, true) is null);
            e.GrantFacility(town.Id, kind, site % 32, site / 32);
        }
        var farm = e.State.Society.Buildings.First(b => b.Kind == BuildingKind.Farm);
        var worker = e.State.Residents[0]; worker.Profession = Profession.Farmer;
        Hold(e, worker, AgentGoalKind.Work, farm.X, farm.Y, farm.Id);
        foreach (var r in e.State.Residents.Skip(1)) Hold(e, r, AgentGoalKind.Rest, town.X, town.Y);
        e.State.Rules.Construction = true;
        e.Step(100);
        Check(farm.Level > 1 || farm.IsUpgrading, "A used, funded facility never received an autonomous upgrade.");
    }

    private static void Migration()
    {
        var e = Flat(1); var origin = e.State.Tiles[16 * 32 + 20]; var target = e.State.Tiles[16 * 32 + 21];
        origin.Wildlife = target.Wildlife = WildlifeKind.Rabbit; origin.WildlifePopulation = 200; target.WildlifePopulation = 20;
        e.Step(6);
        Check(target.WildlifePopulation > 20, "Over-capacity habitat blocked fast migration.");
        e.State.Rules.ResourceRegeneration = true;
        origin.Plants = new() { Shrubs = 1 }; target.Plants = default; e.Step(120);
        Check(target.Plants.Shrubs > 0 && target.Plants.Shrubs < .1, "Plant spread was absent or not slow and yearly.");
    }

    [UnitTest]
    private static void Inspection()
    {
        var e = Flat(1); var r = e.State.Residents.Single(); r.SicknessTicks = 10; r.Thirst = 90; r.Hunger = 85;
        var b = e.State.Society.Buildings.First(b => b.Kind == BuildingKind.Farm);
        var before = e.ExportJson();
        Check(e.GetResidentEffects(r.Id).Any(x => x.Name == "疫病") && e.GetBuildingEffects(b.Id).Count > 0 && e.GetTileEffects(b.X, b.Y).Count > 0, "Missing effect details.");
        Hold(e, r, AgentGoalKind.FetchWater, b.X, b.Y, b.Id); var held = e.ExportJson();
        Check(e.GetResidentTaskIcon(r) == ResidentTaskIcon.Water && e.GetResidentActionSummary(r.Id).Contains("打水"), "A source ID was confused with a facility.");
        Check(e.ExportJson() == held, "Observation changed the world.");
        var bad = JsonNode.Parse(before)!; bad["Residents"]![0]!["Thirst"] = -1;
        try { WorldEngine.ImportJson(bad.ToJsonString()); throw new Exception("Invalid thirst accepted."); } catch (ArgumentException) { }
        bad = JsonNode.Parse(before)!; bad["Society"]!["Buildings"]![0]!["Level"] = 4;
        try { WorldEngine.ImportJson(bad.ToJsonString()); throw new Exception("Invalid upgrade accepted."); } catch (ArgumentException) { }
    }

    private static void Journey()
    {
        var e = Flat(1); var r = e.State.Residents.Single(); var home = e.State.Settlements.Single();
        e.State.Rules.Hunger = e.State.Rules.Thirst = true;
        r.Inventory.Food = 6; r.Inventory.Water = 4;
        Hold(e, r, AgentGoalKind.Explore, 14, 16); r.Agent.Goal.TargetX = 30;
        e.Step(60);
        Check(r.Health == 100 && r.Hunger < 1 && r.Thirst < 1, "A provisioned traveller starved or dehydrated.");
        e.State.Rules.Hunger = e.State.Rules.Thirst = false;
        Hold(e, r, AgentGoalKind.Rest, 30, 16); var food = r.Inventory.Food; var water = r.Inventory.Water;
        e.Step(12); Check(r.Inventory.Food == food && r.Inventory.Water == water, "Disabled survival still consumed carried supplies.");
        Check(home.Resources.Food >= 0 && home.Resources.Water >= 0, "Provisions created a negative warehouse.");
    }
}
