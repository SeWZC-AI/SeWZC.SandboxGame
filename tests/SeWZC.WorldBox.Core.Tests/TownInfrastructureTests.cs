using System.Text.Json.Nodes;
using SeWZC.WorldBox.Core;

internal static class TownInfrastructureTests
{
    public static IEnumerable<(string Name, Action Run)> Cases =>
    [
        ("town expansion consumes resources and exclusive land without changing the center", Expansion),
        ("town expansion work and navigation resume deterministically", ExpansionResume),
        ("waterfront shipyards and docks have separate physical jobs", Waterfront),
        ("building sites reward actual terrain and cannot use another town's claim", Sites),
        ("specialized facilities use local resources and healthy houses add capacity", Facilities),
        ("migration admission uses the capacity of actual operating houses", HousingAdmission),
        ("blocked directional bridges stop navigation and exploration never builds crossings", BlockedRoute),
        ("automatic bridges require a real destination, visible banks and whole-span materials", BridgePlanning),
        ("new villages respect spacing and arrive with useful initial supplies", StartingSupplies),
        ("twelve pioneers finish a foundation after delivering fractional building materials", FoundationDelivery),
        ("pioneers reject a walkable stream and found on reported dry ground", StreamFoundation),
        ("new town and navigation state rejects corrupt saves", InvalidState),
        ("wells use a thresholded affine yield with shared finite daily collection", WellSupply)
    ];

    private static void Check(bool condition, string message) { if (!condition) throw new Exception(message); }
    private static WorldEngine Flat(int population = 1, bool claimed = true)
    {
        var e = WorldEngine.Create(42, 32, 32, false);
        foreach (var tile in e.State.Tiles)
        {
            tile.Terrain = TerrainType.Grass; tile.Fertility = 70; tile.ResourceAmount = 100;
            tile.NaturalWaterYield = .001; tile.Deposit = null; tile.DepositAmount = 0;
            tile.Wildlife = WildlifeKind.None; tile.WildlifePopulation = 0; tile.OtherWildlife = default;
        }
        e.ConfigureWorld(new WorldRules { Aging = false, Births = false, Hunger = false, Thirst = false, Disease = false,
            Construction = false, Research = false, Expansion = false, Trade = false, Wars = false, Alliances = false,
            Peace = false, Migration = false, Secession = false, Conflict = 0 }, false, false);
        e.SpawnResidents(12, 16, RaceKind.Human, population);
        foreach (var resident in e.State.Residents) Hold(e, resident, AgentGoalKind.Rest, 12, 16);
        if (claimed) TestLand.ClaimAllTowns(e);
        return e;
    }

    private static void Hold(WorldEngine e, Resident resident, AgentGoalKind kind, int x, int y, int buildingId = 0)
    {
        resident.X = resident.FromX = x; resident.Y = resident.FromY = y;
        resident.MoveStartedTick = e.State.Tick; resident.MoveDurationTicks = 1;
        resident.Agent.Goal = new() { Kind = kind, TargetX = x, TargetY = y, TargetEntityId = buildingId,
            StartedTick = e.State.Tick, ReviewTick = e.State.Tick + 1000, PlayerDirected = true };
        resident.Agent.NextThinkTick = e.State.Tick + 1000;
    }

    private static void Claim(WorldEngine e, Settlement town, int area)
    {
        var ordered = Enumerable.Range(0, e.State.Tiles.Length).Where(i => e.State.Tiles[i].IsWalkable)
            .OrderBy(i => Math.Abs(i % 32 - town.X) + Math.Abs(i / 32 - town.Y)).Take(area);
        foreach (var i in ordered) { e.State.Tiles[i].NationId = town.NationId; e.State.Tiles[i].ClaimedSettlementId = town.Id; }
    }

    [UnitTest]
    private static void Expansion()
    {
        var e = Flat(60, false); var town = e.State.Settlements.Single(); var center = e.State.Society.Buildings.Single(b => b.Kind == BuildingKind.TownCenter);
        town.Resources = new() { Food = 1000, Wood = 1000, Stone = 1000, Ore = 100 };
        e.UpgradeBuilding(center.Id, true); e.UpgradeBuilding(center.Id, true);
        Check(town.Tier == SettlementTier.Village && town.Name.EndsWith('村'), "Center or population granted a town tier.");
        Check(e.SettlementExpansionError(town.Id)?.Contains("独占陆地") == true, "Shared national territory bypassed exclusive area.");
        town.MaxClaimRadius = 10;
        var requiredArea = e.GetSettlementExpansionArea(town.Id);
        Claim(e, town, requiredArea);
        var bridgedWater = e.State.Tiles.First(t => t.ClaimedSettlementId == town.Id);
        bridgedWater.Terrain = TerrainType.River; bridgedWater.Improvement = LandImprovement.Bridge;
        Check(e.GetSettlementArea(town.Id) == requiredArea - 1 && e.SettlementExpansionError(town.Id) is not null, "A water bridge counted as exclusive expansion land.");
        bridgedWater.Terrain = TerrainType.Grass; bridgedWater.Improvement = LandImprovement.None;
        var wood = town.Resources.Wood;
        e.ExpandTown(town.Id);
        Check(town.IsExpanding && town.Tier == SettlementTier.Village && town.Resources.Wood == wood - 80, "Expansion skipped payment or actual work.");
        try { e.ExpandTown(town.Id); throw new Exception("Duplicate expansion accepted."); } catch (InvalidOperationException) { }
        var worker = e.State.Residents[0]; worker.Profession = Profession.Builder;
        Hold(e, worker, AgentGoalKind.Work, town.X, town.Y, center.Id);
        e.Step(90);
        Check(town.Tier == SettlementTier.Town && town.Name.EndsWith('镇') && center.Level == 3, "Paid town work changed the center level or never completed.");
        town.Population = 1;
        Check(town.Tier == SettlementTier.Town, "Population drop removed the completed tier.");
        var speed = e.MessageTravelMultiplier(town.X, town.Y, town.NationId);
        Check(speed > 1, "Town transport bonus was absent.");
    }

    private static void ExpansionResume()
    {
        var e = Flat(160); var town = e.State.Settlements.Single(); town.Tier = SettlementTier.Town;
        town.Resources = new() { Food = 1000, Wood = 1000, Stone = 1000, Ore = 1000 }; Claim(e, town, 100);
        e.ExpandTown(town.Id);
        var worker = e.State.Residents[0]; worker.Profession = Profession.Builder;
        var center = e.State.Society.Buildings.Single(b => b.Kind == BuildingKind.TownCenter);
        Hold(e, worker, AgentGoalKind.Work, town.X, town.Y, center.Id); e.Step(12);
        var resumed = WorldEngine.ImportJson(e.ExportJson()); e.Step(180); resumed.Step(180);
        Check(e.ExportJson() == resumed.ExportJson() && town.Tier == SettlementTier.City && center.Level == 1,
            "Mid-project expansion failed deterministic resume or altered the center.");
    }

    [UnitTest]
    private static void Waterfront()
    {
        foreach (var kind in new[] { BuildingKind.Dock, BuildingKind.Shipyard })
        {
            var paid = Flat(); var home = paid.State.Settlements.Single();
            paid.GrantReceivedResearch(home.Id, ResearchKind.Logistics);
            var water = paid.State.Tiles[16 * 32 + 15]; water.Terrain = TerrainType.River; water.NationId = water.ClaimedSettlementId = 0;
            var id = paid.BuildFacility(home.Id, kind, 15, 16);
            var project = paid.State.Society.Buildings.Single(b => b.Id == id);
            var builder = paid.State.Residents.Single(); builder.Age = 25;
            Hold(paid, builder, AgentGoalKind.Work, 14, 16, id);
            Check(project.ConstructionProgress == 0 && water.ClaimedSettlementId == home.Id, "Paid waterfront project was gifted or not registered");
            paid.State.Tick++;
            Check(paid.TryWorkAtBuilding(builder) && project.ConstructionProgress > 0, "Ownership gate prevented paid construction on unclaimed water");
            var restored = WorldEngine.ImportJson(paid.ExportJson()); paid.Step(60); restored.Step(60);
            Check(project.IsCompleted && paid.ExportJson() == restored.ExportJson(), "Paid waterfront construction failed completion or saved continuation");
        }
        var e = Flat(); var town = e.State.Settlements.Single(); e.GrantReceivedResearch(town.Id, ResearchKind.Logistics);
        Check(e.FacilityPlacementError(town.Id, BuildingKind.Shipyard, 15, 16, true) is not null, "Dry shipyard was accepted.");
        e.State.Tiles[16 * 32 + 15].Terrain = TerrainType.River;
        e.State.Tiles[18 * 32 + 15].Terrain = TerrainType.River;
        var shipyardId = e.GrantFacility(town.Id, BuildingKind.Shipyard, 15, 16);
        var dockId = e.GrantFacility(town.Id, BuildingKind.Dock, 15, 18);
        var worker = e.State.Residents.Single(); worker.Profession = Profession.Builder;
        Hold(e, worker, AgentGoalKind.Work, 14, 16, shipyardId); e.State.Tick++;
        Check(!e.TryWorkAtBuilding(worker), "Shipyard used remote warehouse wood.");
        worker.Inventory.Wood = 4;
        Check(e.TryWorkAtBuilding(worker) && worker.Inventory.Boats == 1 && worker.Inventory.Wood == 0, "Shore worker failed physical boat production.");
        Hold(e, worker, AgentGoalKind.Work, 14, 18, dockId); e.State.Tick++;
        worker.Inventory.Wood = 4; var boats = worker.Inventory.Boats;
        Check(e.TryWorkAtBuilding(worker) && worker.Inventory.Wood == 4 && worker.Inventory.Boats == boats
            && e.BoatTravelMultiplier(15, 18, town.NationId) > 1, "Dock manufactured boats or failed transport service.");
        town.Resources.Food = 0; e.State.Tick += 13;
        Check(!e.TryWorkAtBuilding(worker) && e.BoatTravelMultiplier(15, 18, town.NationId) == 1,
            "Failed unfunded dock work refreshed its operating bonus.");
        _ = WorldEngine.ImportJson(e.ExportJson());
    }

    [UnitTest]
    private static void Sites()
    {
        var e = Flat(); var town = e.State.Settlements.Single();
        e.State.Tiles[16 * 32 + 13].Fertility = 10; e.State.Tiles[16 * 32 + 18].Fertility = 100;
        Check(e.BuildingSiteScore(town.Id, BuildingKind.Farm, 18, 16) > e.BuildingSiteScore(town.Id, BuildingKind.Farm, 13, 16), "Farm siting still prizes proximity over fertile ground.");
        e.SpawnResidents(29, 16, RaceKind.Human, 1); var neighbor = e.State.Settlements.Single(t => t.Id != town.Id);
        e.TransferTerritory(neighbor.X, neighbor.Y, town.NationId, 0);
        var plot = e.State.Tiles[16 * 32 + 17]; plot.NationId = town.NationId; plot.ClaimedSettlementId = neighbor.Id;
        Check(e.FacilityPlacementError(town.Id, BuildingKind.Housing, 17, 16, true)?.Contains("独占") == true,
            "Two same-nation towns shared one registered plot.");
    }

    [UnitTest]
    private static void Facilities()
    {
        var e = Flat(); var town = e.State.Settlements.Single(); var worker = e.State.Residents.Single();
        e.State.Tiles[16 * 32 + 17].Terrain = TerrainType.Forest;
        e.State.Tiles[16 * 32 + 17].Plants = new() { Trees = 1 };
        var camp = e.GrantFacility(town.Id, BuildingKind.LumberCamp, 16, 16);
        worker.Profession = Profession.Lumberjack; Hold(e, worker, AgentGoalKind.Work, 16, 16, camp); e.State.Tick++;
        var wood = worker.Inventory.Wood;
        Check(e.TryWorkAtBuilding(worker) && worker.Inventory.Wood > wood, "Forest-edge camp failed actual harvesting.");
        e.State.Tiles[18 * 32 + 16].Terrain = TerrainType.Wetland;
        e.State.Tiles[18 * 32 + 16].NaturalWaterYield = 1;
        var well = e.GrantFacility(town.Id, BuildingKind.Well, 16, 18);
        Hold(e, worker, AgentGoalKind.Work, 16, 18, well); e.State.Tick++;
        var waterBefore = e.AvailableWater(16, 18); var carriedBefore = worker.Inventory.Water;
        Check(e.TryWorkAtBuilding(worker) && Math.Abs(waterBefore - e.AvailableWater(16, 18) - (worker.Inventory.Water - carriedBefore)) < 1e-9, "Well collection did not debit the shared daily supply.");
        var house = e.GrantFacility(town.Id, BuildingKind.Housing, 15, 20);
        Check(e.GetHousingCapacity(town.Id) == town.Housing + 20, "Healthy house provided no capacity.");
        e.SetBuildingEnabled(house, false);
        Check(e.GetHousingCapacity(town.Id) == town.Housing, "Stopped house kept its bonus.");
    }

    [UnitTest]
    private static void WellSupply()
    {
        var e = Flat(8); var town = e.State.Settlements.Single();
        var tile = e.State.Tiles[18 * 32 + 16];
        Check(e.FacilityPlacementError(town.Id, BuildingKind.Well, 16, 18, true)?.Contains("供水量高于 0.02") == true, "A dry site still allowed an ineffective well");
        foreach (var (source, expected) in new[] { (.005, 0d), (.02, 0d), (.03, .3), (.04, .6), (.05, .9), (.1, 2.4) })
        {
            tile.NaturalWaterYield = source;
            Check(Math.Abs(WorldEngine.WellWaterYield(tile) - expected) < 1e-9, "Well ignored its threshold or nonzero intercept.");
        }
        tile.NaturalWaterYield = .04;
        var id = e.GrantFacility(town.Id, BuildingKind.Well, 16, 18);
        Check(Math.Abs(e.GetWaterSupply(16, 18) - .64) < 1e-9, "Working well added no useful local supply.");
        var before = e.ExportJson(); _ = e.GetWaterSupply(16, 18); _ = e.GetBuildingEffects(id);
        Check(e.ExportJson() == before, "Inspecting well supply changed the world.");
        e.SetBuildingEnabled(id, false);
        Check(e.GetWaterSupply(16, 18) == .04, "Stopped well still supplied water.");
        e.SetBuildingEnabled(id, true); tile.DroughtTicks = 2;
        Check(WorldEngine.WellWaterYield(tile) == 0 && Math.Abs(e.GetWaterSupply(16, 18) - .008) < 1e-9, "Drought did not shut down a well below threshold.");
        tile.DroughtTicks = 0; tile.NaturalWaterYield = .2;
        foreach (var person in e.State.Residents) Hold(e, person, AgentGoalKind.FetchWater, 16, 18, 18 * 32 + 16 + 1);
        e.State.Tick++;
        foreach (var person in e.State.Residents) e.TryFetchWater(person);
        Check(e.AvailableWater(16, 18) < 1e-9 && Math.Abs(tile.WaterDrawn - 5.6) < 1e-9, "Repeated well work exceeded or failed to consume its daily limit.");
        var resumed = WorldEngine.ImportJson(e.ExportJson()); e.Step(6); resumed.Step(6);
        Check(e.ExportJson() == resumed.ExportJson(), "Large well draw lost deterministic continuation.");
    }

    private static void BlockedRoute()
    {
        var e = Flat(); var town = e.State.Settlements.Single(); var worker = e.State.Residents.Single();
        e.GrantReceivedResearch(town.Id, ResearchKind.Logistics); e.State.Rules.Construction = true;
        for (var y = 0; y < 32; y++)
        { e.State.Tiles[y * 32 + 16].Terrain = TerrainType.River; e.State.Tiles[y * 32 + 17].Terrain = TerrainType.River; }
        e.State.Tiles[15 * 32 + 16].Terrain = TerrainType.Grass;
        e.GrantFacility(town.Id, BuildingKind.Bridge, 16, 16, BridgeDirection.Vertical);
        Hold(e, worker, AgentGoalKind.Explore, 15, 16); worker.Agent.Goal.TargetX = 20;
        e.Step(220);
        Check(worker.X < 17 && e.State.Society.Buildings.Count(b => b.Kind == BuildingKind.Bridge) == 1,
            "Wrong-axis bridge allowed crossing or exploration created random bridges.");
        Check(worker.Agent.Goal.NavigationWithoutProgress == 64 || worker.Agent.Goal.NavigationRetryTick > 0,
            "An unreachable task never reached a bounded navigation stop.");
        var resumed = WorldEngine.ImportJson(e.ExportJson()); e.Step(30); resumed.Step(30);
        Check(e.ExportJson() == resumed.ExportJson(), "Saved navigation history changed resumed movement.");
    }

    [UnitTest]
    private static void HousingAdmission()
    {
        foreach (var enabled in new[] { true, false })
        {
            var e = Flat(); var migrant = e.State.Residents.Single(); var original = migrant.SettlementId;
            e.SpawnResidents(29, 16, RaceKind.Human, 1); var destination = e.State.Settlements.Single(t => t.Id != original);
            TestLand.ClaimAllTowns(e);
            destination.Housing = 1; destination.Resources.Food = 20;
            var housing = e.GrantFacility(destination.Id, BuildingKind.Housing, 28, 20);
            e.SetBuildingEnabled(housing, enabled);
            migrant.Age = 30; Hold(e, migrant, AgentGoalKind.Migrate, destination.X, destination.Y);
            migrant.Agent.Goal.TargetSettlementId = destination.Id; e.Step(3);
            Check(migrant.SettlementId == (enabled ? destination.Id : original),
                "Arrival ignored real housing capacity or accepted a migrant into disabled housing.");
            _ = WorldEngine.ImportJson(e.ExportJson());
        }
    }

    [UnitTest]
    private static void BridgePlanning()
    {
        foreach (var affordable in new[] { true, false })
        {
            var e = Flat(); var town = e.State.Settlements.Single(); var worker = e.State.Residents.Single();
            e.GrantReceivedResearch(town.Id, ResearchKind.Logistics); e.State.Rules.Construction = true;
            for (var y = 0; y < 32; y++) e.State.Tiles[y * 32 + 13].Terrain = TerrainType.River;
            town.Resources.Wood = town.Resources.Stone = affordable ? 1000 : 0;
            worker.Age = 30; worker.Profession = Profession.Builder; Hold(e, worker, AgentGoalKind.ReturnHome, 14, 16);
            worker.Agent.Goal.TargetX = town.X; worker.Agent.Goal.TargetSettlementId = town.Id;
            e.Step();
            var bridges = e.State.Society.Buildings.Where(b => b.Kind == BuildingKind.Bridge).ToArray();
            if (!affordable) { Check(bridges.Length == 0, "Unaffordable crossing still started a bridge."); continue; }
            Check(bridges.Length == 1 && bridges[0].X == 13 && bridges[0].Y == 16
                && bridges[0].Direction == BridgeDirection.Horizontal, "A known blocked home route failed to plan the connecting axis.");
            var bridge = bridges[0]; Hold(e, worker, AgentGoalKind.Work, 14, 16, bridge.Id);
            Check(bridge.PlanningReason.Contains("两岸") && bridge.SiteReason.Contains("实际任务"), "Bridge omitted its construction purpose and route evidence.");
            e.Step(80);
            Check(bridge.IsCompleted && e.CanTraverseStep(14, 16, 13, 16, TravelMode.Foot)
                && e.CanTraverseStep(13, 16, 12, 16, TravelMode.Foot), "Actual bridge work did not open both banks.");
        }
    }

    [UnitTest]
    private static void StartingSupplies()
    {
        var e = Flat(36); var town = e.State.Settlements.Single();
        Check(town.Resources.Food >= 36 * 7 && town.Resources.Water >= 36 * 4 && town.Resources.Wood >= 80,
            "Initial resources cannot support the first construction and supply cycle.");
        e.SpawnResidents(20, 16, RaceKind.Human, 1);
        Check(e.State.Settlements.Count == 1, "Residents created a village inside minimum spacing.");
        Check(WorldEngine.VillageFoundingCost.Wood >= 80 && WorldEngine.VillageFoundingCost.Stone >= 40, "Autonomous founding costs stayed trivial.");
    }

    [UnitTest]
    private static void FoundationDelivery()
    {
        var e = Flat(12); var town = e.State.Settlements.Single();
        town.FoundationPending = true; town.Resources = new();
        foreach (var person in e.State.Residents)
        {
            person.Inventory.Wood = WorldEngine.VillageFoundingCost.Wood / 12;
            person.Inventory.Stone = WorldEngine.VillageFoundingCost.Stone / 12;
            Hold(e, person, AgentGoalKind.ReturnHome, town.X, town.Y);
        }
        e.Step(8);
        Check(!town.FoundationPending && town.Resources.Wood >= 0 && town.Resources.Stone >= 0,
            "Rounding of physically delivered pioneer shares prevented foundation completion.");
        _ = WorldEngine.ImportJson(e.ExportJson());
    }

    [UnitTest]
    private static void StreamFoundation()
    {
        var e = Flat(80); var home = e.State.Settlements.Single(); e.State.Tick = 119;
        home.Resources = new() { Food = 10000, Water = 1000, Wood = 1000, Stone = 1000, Ore = 100 };
        foreach (var person in e.State.Residents) { person.Age = 30; Hold(e, person, AgentGoalKind.Rest, home.X, home.Y); }
        var creek = e.State.Tiles[16 * 32 + 28]; creek.Terrain = TerrainType.Stream; creek.Fertility = 100;
        foreach (var y in new[] { 16, 15 })
            home.PublicKnowledge.Add(new AgentFact { Id = e.State.NextId++, Kind = AgentFactKind.FoundingSite,
                SubjectId = home.Id, X = 28, Y = y, ObservedTick = 1, LearnedTick = 1,
                OriginResidentId = e.State.Residents[0].Id, SourceResidentId = e.State.Residents[0].Id,
                OriginProfession = e.State.Residents[0].Profession, Confidence = 1, Text = "已带回的建村勘察" });
        e.State.Rules.Expansion = true; e.Step();
        var founded = e.State.Settlements.Single(t => t.Id != home.Id);
        Check(founded.X == 28 && founded.Y == 15 && founded.FoundationPending,
            "Walking through a creek incorrectly permits building a village in its water");
        _ = WorldEngine.ImportJson(e.ExportJson());
    }

    [UnitTest]
    private static void InvalidState()
    {
        var e = Flat();
        foreach (var mutation in new Action<JsonObject>[] {
            json => json["Settlements"]![0]!["Tier"] = 9,
            json => json["Settlements"]![0]!["ExpansionRequired"] = -1,
            json => json["Residents"]![0]!["Agent"]!["Goal"]!["NavigationVisited"] = new JsonArray(-1) })
        {
            var json = JsonNode.Parse(e.ExportJson())!.AsObject(); mutation(json);
            try { WorldEngine.ImportJson(json.ToJsonString()); throw new Exception("Corrupt new state accepted."); }
            catch (ArgumentException) { }
        }
    }
}
