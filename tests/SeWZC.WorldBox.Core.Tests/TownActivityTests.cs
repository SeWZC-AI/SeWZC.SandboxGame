using SeWZC.WorldBox.Core;
using System.Text.Json.Nodes;

internal static class TownActivityTests
{
    public static IEnumerable<(string Name, Action Run)> Cases =>
    [
        ("town buildings require registered land while public roads and bridges keep ownership", BuildingClaims),
        ("territory edits extend connected towns and terrain cuts release detached claims", ConnectedClaims),
        ("terrain harvesting differentiates forests food and mineral composition", TerrainHarvest),
        ("local workforce keeps messengers scarce and representatives local", Workforce),
        ("workers finish useful tasks and rest before choosing another destination", TaskCommitment),
        ("fishers borrow a real boat fish offshore and return catch through a saved journey", BoatFishing)
    ];

    private static void Check(bool condition, string message) { if (!condition) throw new Exception(message); }
    private static WorldEngine Flat(int population = 1)
    {
        var e = WorldEngine.Create(123, 32, 32, false);
        foreach (var tile in e.State.Tiles)
        {
            tile.Terrain = TerrainType.Grass; tile.Fertility = 100; tile.ResourceAmount = 100;
            tile.Wildlife = WildlifeKind.None; tile.WildlifePopulation = 0; tile.OtherWildlife = default;
            tile.Deposit = null; tile.DepositAmount = 0; tile.NaturalWaterYield = .03;
        }
        e.ConfigureWorld(new WorldRules { Aging = false, Births = false, Hunger = false, Thirst = false, Disease = false,
            Construction = false, Research = false, Expansion = false, Migration = false, Trade = false, Wars = false,
            Peace = false, Alliances = false, Secession = false, ResourceRegeneration = false, Conflict = 0 }, false, false);
        e.SpawnResidents(12, 16, RaceKind.Human, population);
        foreach (var person in e.State.Residents)
        { person.Agent.Goal = new() { Kind = AgentGoalKind.Rest, TargetX = person.X, TargetY = person.Y, PlayerDirected = true, ReviewTick = 10000 }; }
        return e;
    }

    [UnitTest]
    private static void BuildingClaims()
    {
        var e = Flat(); var town = e.State.Settlements.Single();
        var before = e.ExportJson();
        Check(e.FacilityPlacementError(town.Id, BuildingKind.Housing, 16, 16, true)?.Contains("占领") == true, "Gift bypassed land registration");
        try { e.GrantFacility(town.Id, BuildingKind.Housing, 16, 16); throw new Exception("Unclaimed building accepted"); } catch (InvalidOperationException) { }
        Check(before == e.ExportJson(), "Rejected construction mutated the world");
        e.TransferTerritory(12, 16, town.NationId, 5);
        e.GrantFacility(town.Id, BuildingKind.Housing, 16, 16);
        e.State.Tiles[16 * 32 + 18].Terrain = TerrainType.River;
        e.GrantFacility(town.Id, BuildingKind.Bridge, 18, 16, BridgeDirection.Horizontal);
        Check(e.State.Tiles[16 * 32 + 18].ClaimedSettlementId == 0, "Bridge annexed unclaimed water");
        e.BuildRoad(town.Id, 19, 16, 0);
        Check(e.State.Tiles[16 * 32 + 19].NationId == 0, "Road annexed its site");
    }

    [UnitTest]
    private static void ConnectedClaims()
    {
        var e = Flat(); var town = e.State.Settlements.Single();
        e.TransferTerritory(25, 25, town.NationId, 1);
        Check(e.State.Tiles[25 * 32 + 25].NationId == 0, "Remote brush created an enclave");
        var corrupt = JsonNode.Parse(e.ExportJson())!;
        corrupt["Tiles"]![25 * 32 + 25]!["NationId"] = town.NationId;
        corrupt["Tiles"]![25 * 32 + 25]!["ClaimedSettlementId"] = town.Id;
        try { _ = WorldEngine.ImportJson(corrupt.ToJsonString()); throw new Exception("Detached claim accepted from save"); }
        catch (ArgumentException) { }
        e.TransferTerritory(12, 16, town.NationId, 6);
        Check(e.State.Tiles[16 * 32 + 17].ClaimedSettlementId == town.Id, "Connected brush did not extend town");
        for (var y = 10; y <= 22; y++) e.PaintTerrain(15, y, TerrainType.DeepWater, 0);
        Check(e.State.Tiles[16 * 32 + 17].ClaimedSettlementId == 0 && e.State.Tiles[16 * 32 + 17].NationId == 0, "Disconnected land kept ownership");
        var resumed = WorldEngine.ImportJson(e.ExportJson()); e.Step(3); resumed.Step(3);
        Check(e.ExportJson() == resumed.ExportJson(), "Connectivity changed save continuation");
    }

    [UnitTest]
    private static void TerrainHarvest()
    {
        ResourceStock Harvest(TerrainType terrain, Profession profession)
        {
            var e = Flat(); var person = e.State.Residents.Single(); person.Race = RaceKind.Elf;
            e.State.Tiles[16 * 32 + 16].Terrain = terrain;
            person.X = person.FromX = 16; person.Y = person.FromY = 16; person.Profession = profession;
            person.Agent.Goal = new() { Kind = profession == Profession.Farmer ? AgentGoalKind.Gather : AgentGoalKind.Work,
                TargetX = 16, TargetY = 16, PlayerDirected = true, ReviewTick = 10000 };
            e.Step(8); return person.Inventory;
        }
        Check(Harvest(TerrainType.Forest, Profession.Lumberjack).Wood > Harvest(TerrainType.Woodland, Profession.Lumberjack).Wood, "Forest and woodland have identical logging");
        Check(Harvest(TerrainType.Rainforest, Profession.Lumberjack).Wood > Harvest(TerrainType.Forest, Profession.Lumberjack).Wood, "Rainforest yields no more wood");
        Check(Harvest(TerrainType.Meadow, Profession.Farmer).Food > Harvest(TerrainType.Grass, Profession.Farmer).Food, "Food yields were flattened");
        var sand = Harvest(TerrainType.Sand, Profession.Miner); var hills = Harvest(TerrainType.Hills, Profession.Miner);
        Check(hills.Stone + hills.Ore > sand.Stone + sand.Ore && hills.Ore > sand.Ore, "Minerals ignore terrain");
    }

    [UnitTest]
    private static void Workforce()
    {
        var e = Flat(120); var town = e.State.Settlements.Single();
        foreach (var person in e.State.Residents) { person.Profession = Profession.Messenger; person.Agent.Goal.PlayerDirected = false; person.Agent.JobChangedTick = 0; }
        e.State.Tick = 149; e.Step();
        Check(e.State.Residents.Count(r => r.Profession == Profession.Messenger) <= 2, "Town kept excessive messengers");
        Check(e.State.Residents.Single(r => r.Id == town.RepresentativeId).Agent.Goal.Kind is not (AgentGoalKind.Explore or AgentGoalKind.DeliverMessage), "Representative left without local business");
    }

    [UnitTest]
    private static void TaskCommitment()
    {
        var e = Flat(); var person = e.State.Residents.Single();
        person.Profession = Profession.Lumberjack; person.Inventory.Food = 2; person.Inventory.Water = 2;
        person.Agent.Goal = new() { Kind = AgentGoalKind.Rest, TargetX = 12, TargetY = 16, StartedTick = 0 };
        person.X = person.FromX = 12; person.Y = person.FromY = 16; person.Agent.Fatigue = 50; person.Agent.NextThinkTick = 0;
        e.Step(8); Check(person.Agent.Goal.Kind == AgentGoalKind.Rest && person.Agent.Fatigue < 35, "Resident departed before completing rest");
        e.State.Tiles[16 * 32 + 16].Terrain = TerrainType.Forest;
        person.Agent.Goal = new() { Kind = AgentGoalKind.Work, TargetX = 16, TargetY = 16 };
        person.Agent.Fatigue = 0; person.Agent.NextThinkTick = e.State.Tick;
        e.Step(12); Check(person.Agent.Goal.TargetX == 16 && person.Inventory.Wood > 0, "Useful logging goal was abandoned during travel");
    }

    private static void BoatFishing()
    {
        var e = Flat(); var town = e.State.Settlements.Single(); var person = e.State.Residents.Single();
        for (var y = 13; y <= 19; y++) for (var x = 14; x <= 19; x++) e.State.Tiles[y * 32 + x].Terrain = TerrainType.Water;
        var fish = e.State.Tiles[16 * 32 + 16]; fish.Wildlife = WildlifeKind.Fish; fish.WildlifePopulation = 12;
        e.GrantReceivedResearch(town.Id, ResearchKind.Logistics); town.Resources.Boats = 1;
        person.X = person.FromX = town.X; person.Y = person.FromY = town.Y;
        person.Profession = Profession.Fisher; person.Inventory.Food = 1.2; person.Inventory.Water = 1;
        person.Agent.Goal = new() { Kind = AgentGoalKind.Idle }; person.Agent.NextThinkTick = 0;
        e.Step(8); Check(person.TravelMode == TravelMode.Boat && town.Resources.Boats == 0 && person.Inventory.Boats == 1, "Fisher did not borrow an actual boat");
        var resumed = WorldEngine.ImportJson(e.ExportJson());
        var fishedOffshore = false; var returned = false; var initialFood = town.Resources.Food;
        for (var day = 0; day < 180; day++)
        {
            e.Step(); resumed.Step();
            fishedOffshore |= person.X == 16 && person.Y == 16 && person.Activity == ResidentActivity.Working;
            returned |= fishedOffshore && town.Resources.Boats == 1 && town.Resources.Food > initialFood;
        }
        Check(fishedOffshore && returned && fish.WildlifePopulation < 12, $"Fishing failed: offshore={fishedOffshore}, returned={returned}, fish={fish.WildlifePopulation}, profession={person.Profession}, xy={person.X},{person.Y}, goal={person.Agent.Goal.Kind}, boats={town.Resources.Boats}/{person.Inventory.Boats}, food={town.Resources.Food}/{initialFood}/{person.Inventory.Food}");
        Check(e.ExportJson() == resumed.ExportJson(), "Boat fishing diverged after saving at sea");
    }
}
