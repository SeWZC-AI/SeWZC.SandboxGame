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
        ("local conversations preserve ID-ranked recipients across neighborhood orders", ConversationRecipients),
        ("shared visible paths react immediately to fire bridges terrain and travel mode", VisiblePathChanges),
        ("shore fishers retain sources seven steps away beside reachable banks", FishingBoundary),
        ("successive hunters and fishers preserve sparse prey and switch species immediately", ExhaustedPrey),
        ("fishers borrow a real boat fish offshore and return catch through a saved journey", BoatFishing)
    ];

    private static void Check(bool condition, string message) { if (!condition) throw new Exception(message); }
    private static void ExhaustedPrey()
    {
        var e = Flat(); var person = e.State.Residents.Single(); var tile = e.State.Tiles[person.Y * 32 + person.X];
        person.Age = 20; person.MoveStartedTick = -100; person.MoveDurationTicks = 1;
        person.Agent.Goal = new() { Kind = AgentGoalKind.Hunt, TargetEntityId = person.Y * 32 + person.X + 1 };
        tile.Wildlife = WildlifeKind.Rabbit; tile.WildlifePopulation = .05; tile.OtherWildlife = new() { Deer = .05 };
        Check(e.TryHarvestWildlife(person) && tile.OtherWildlife.Deer is > 0 and < .05 && tile.WildlifePopulation == .05, "First hunter failed to preserve sparse deer");
        Check(e.TryHarvestWildlife(person) && tile.WildlifePopulation is > 0 and < .05 && !e.TryHarvestWildlife(person), "Next hunter reused sparse cached prey instead of switching species");
        tile = e.State.Tiles[person.Y * 32 + person.X + 1]; tile.Terrain = TerrainType.Water;
        tile.Wildlife = WildlifeKind.Fish; tile.WildlifePopulation = .05; tile.OtherWildlife = new() { GrassCarp = .05 };
        person.Agent.Goal = new() { Kind = AgentGoalKind.Fish, TargetEntityId = person.Y * 32 + person.X + 2 };
        Check(e.TryHarvestWildlife(person) && tile.OtherWildlife.GrassCarp is > 0 and < .05 && tile.WildlifePopulation == .05, "First fisher failed to preserve sparse carp");
        Check(e.TryHarvestWildlife(person) && tile.WildlifePopulation is > 0 and < .05 && !e.TryHarvestWildlife(person), "Next fisher reused sparse cached fish instead of switching species");
    }
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
            e.State.Tiles[16 * 32 + 16].Plants = profession == Profession.Lumberjack ? new() { Trees = 1 } : new() { Grass = 1 };
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
        e.State.Tiles[16 * 32 + 16].Plants = new() { Trees = 1 };
        e.State.Settlements.Single().Resources.Wood = 0;
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

    [UnitTest]
    private static void ConversationRecipients()
    {
        foreach (var size in new[] { 2, 7, 31, 128 })
        foreach (var order in new[] { 0, 1, 2 })
        {
            var e = Flat(size);
            e.State.Residents = order switch
            {
                1 => e.State.Residents.OrderByDescending(r => r.Id).ToList(),
                2 => e.State.Residents.OrderBy(r => unchecked((uint)r.Id * 2654435761u)).ToList(),
                _ => e.State.Residents.OrderBy(r => r.Id).ToList()
            };
            foreach (var person in e.State.Residents)
            {
                person.X = person.FromX = 12; person.Y = person.FromY = 16; person.Age = 30; person.Profession = Profession.Farmer;
                person.Agent.Goal.TargetX = 12; person.Agent.Goal.TargetY = 16;
                person.Agent.Memory.Add(new() { Id = e.State.NextId++, Kind = AgentFactKind.Personal, SubjectId = person.Id,
                    OriginResidentId = person.Id, SourceResidentId = person.Id, Text = "A remembered personal event", Confidence = 1 });
            }
            for (var tick = 1; tick <= 12; tick++)
            {
                e.State.Tick = tick - 1; e.State.PendingMessages.Clear();
                foreach (var person in e.State.Residents) person.Agent.LastConversationTick = -100;
                var expected = e.State.Residents.Where(r => (tick + r.Id) % 12 == 0).Select(sender =>
                {
                    var neighbors = e.State.Residents.Where(r => r.Id != sender.Id).OrderBy(r => r.Id).ToArray();
                    return (sender.Id, neighbors[(tick / 12 + sender.Id) % neighbors.Length].Id);
                }).ToArray();
                e.Step();
                var actual = e.State.PendingMessages.Where(m => m.TargetSettlementId == 0 && m.DeliverTick == tick + 1)
                    .Select(m => (m.SenderId, m.RecipientId)).ToArray();
                Check(expected.SequenceEqual(actual), $"Conversation recipients changed for size {size}, order {order}, tick {tick}");
            }
        }
    }

    [UnitTest]
    private static void VisiblePathChanges()
    {
        var e = Flat();
        foreach (var tile in e.State.Tiles) { tile.Terrain = TerrainType.DeepWater; tile.ResourceAmount = 0; }
        for (var x = 10; x <= 14; x++) e.State.Tiles[10 * 32 + x].Terrain = TerrainType.Grass;
        var target = 10 * 32 + 14; e.State.Tiles[target].ResourceAmount = 100; e.State.Tiles[target].Plants = new() { Grass = 1 };
        var barrier = e.State.Tiles[10 * 32 + 12];
        var first = new Resident { Id = 900, X = 10, Y = 10, Race = RaceKind.Human };
        var other = new Resident { Id = 901, X = 11, Y = 10, Race = RaceKind.Human };
        var query = typeof(WorldEngine).GetMethod("FindVisibleResourceSite", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;
        int Site(Resident person) => (int)query.Invoke(e, [person, Profession.Farmer])!;
        void Expect(int site, string reason)
        {
            Check(Site(first) == site, reason);
            _ = Site(other); // Evict the last-search stamp, then exercise the shared cache.
            Check(Site(new Resident { Id = 902, X = first.X, Y = first.Y, Race = first.Race, TravelMode = first.TravelMode }) == site, reason + " for another resident");
        }
        Expect(target, "Open corridor became unreachable");
        barrier.FireTicks = 2; Expect(-1, "New fire did not block cached route");
        barrier.FireTicks = 0; Expect(target, "Extinguished fire kept blocking route");
        barrier.Terrain = TerrainType.River; Expect(-1, "River retained dry-land route");
        barrier.Improvement = LandImprovement.Bridge; Expect(target, "Completed horizontal bridge was ignored");
        barrier.BridgeDirection = BridgeDirection.Vertical; Expect(-1, "Bridge reorientation retained old route");
        first.TravelMode = TravelMode.Boat; Expect(target, "Boat reused foot-only bridge restriction");
        first.TravelMode = TravelMode.Foot; barrier.Improvement = LandImprovement.None;
        barrier.Terrain = TerrainType.Mountain; Expect(-1, "Human reused an invalid mountain route");
        first.Race = RaceKind.Dwarf; Expect(target, "Dwarf reused human mountain restriction");
        first.Race = RaceKind.Human; barrier.Improvement = LandImprovement.MountainPass; Expect(target, "Completed mountain pass was ignored");
        e.State.Tiles = e.State.Tiles.Select(t => new Tile { Terrain = t.Terrain, ResourceAmount = t.ResourceAmount, Fertility = 100, Plants = t.Plants }).ToArray();
        Expect(-1, "Replacement grid reused an old mountain pass");
    }

    [UnitTest]
    private static void FishingBoundary()
    {
        var e = Flat(); var person = e.State.Residents.Single(); person.X = 10; person.Y = 10;
        var fish = e.State.Tiles[14 * 32 + 13]; // Euclidean visible, Manhattan distance seven.
        fish.Terrain = TerrainType.Water; fish.Wildlife = WildlifeKind.Fish; fish.WildlifePopulation = 5;
        var query = typeof(WorldEngine).GetMethod("FindHarvestableWildlife", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;
        var result = ((int Site, int Source, bool Fishing))query.Invoke(e, [person])!;
        Check(result.Fishing && result.Source == 14 * 32 + 13
            && Math.Abs(result.Site % 32 - 10) + Math.Abs(result.Site / 32 - 10) == 6, "Pruning removed a visible fish source beside a reachable bank");
    }
}
