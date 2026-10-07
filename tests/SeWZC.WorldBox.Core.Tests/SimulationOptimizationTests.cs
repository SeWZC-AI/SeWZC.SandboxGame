using System.Reflection;
using SeWZC.WorldBox.Core;

internal static class SimulationOptimizationTests
{
    public static IEnumerable<(string Name, Action Run)> Cases =>
    [
        ("town bonuses suspend below three-radius area and expansion follows half the maximum radius",
            TerritoryThresholds),
        ("gathering slows outside the worker's own territory and debits the real source", Gathering),
        ("automatic construction rejects useless and unreachable sites and preserves its reasons", Construction),
        ("automatic bridges reject an existing land detour and an isolated far bank", BridgePurpose),
        ("builders leave routine production to register the area needed for town upgrades", UpgradeClaimPriority),
        ("resident descriptions distinguish assigned work from travel and blocked labor", TaskDescriptions),
        ("bounded navigation never re-enters an already visited tile", Navigation),
        ("large-map food webs survive long revisit intervals and deterministic resume", FoodWeb),
    ];

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
    }

    private static object? Invoke(WorldEngine e, string method, params object[] args)
    {
        return typeof(WorldEngine).GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(e, args);
    }

    private static WorldEngine Flat(int population = 8)
    {
        var e = WorldEngine.Create(42, 32, 32, false);
        foreach (var t in e.State.Tiles)
        {
            t.Terrain = TerrainType.Grass;
            t.Fertility = 80;
            t.ResourceAmount = 100;
            t.NaturalWaterYield = .03;
            t.Wildlife = WildlifeKind.None;
            t.WildlifePopulation = 0;
            t.OtherWildlife = default;
        }

        e.ConfigureWorld(new WorldRules
        {
            Aging = false,
            Births = false,
            Hunger = false,
            Thirst = false,
            Disease = false,
            Construction = false,
            Research = false,
            Expansion = false,
            Trade = false,
            Wars = false,
            Alliances = false,
            Peace = false,
            Migration = false,
            Secession = false,
            ResourceRegeneration = false,
        }, false, false);
        e.SpawnResidents(12, 16, RaceKind.Human, population);
        foreach (var person in e.State.Residents)
        {
            person.X = person.FromX = 12;
            person.Y = person.FromY = 16;
            person.Age = 25;
            person.Agent.Goal = new AgentGoal
            {
                Kind = AgentGoalKind.Rest,
                TargetX = 12,
                TargetY = 16,
                PlayerDirected = true,
                ReviewTick = 1000,
            };
        }

        e.State.Society.Buildings.RemoveAll(b => b.Kind != BuildingKind.TownCenter);
        foreach (var t in e.State.Tiles)
        {
            t.NationId = 0;
            t.ClaimedSettlementId = 0;
        }

        Claim(e, 1);
        return e;
    }

    private static void Claim(WorldEngine e, int area)
    {
        var town = e.State.Settlements.Single();
        foreach (var i in Enumerable.Range(0, e.State.Tiles.Length)
                     .OrderBy(i => Math.Abs(i % 32 - town.X) + Math.Abs(i / 32 - town.Y)).ThenBy(i => i).Take(area))
        {
            e.State.Tiles[i].NationId = town.NationId;
            e.State.Tiles[i].ClaimedSettlementId = town.Id;
        }
    }

    [UnitTest]
    private static void TerritoryThresholds()
    {
        var e = Flat();
        var town = e.State.Settlements.Single();
        town.Tier = SettlementTier.City;
        var center = e.State.Society.Buildings.Single();
        e.UpgradeBuilding(center.Id, true);
        e.UpgradeBuilding(center.Id, true);
        Claim(e, 24);
        var inactiveSpeed = e.MessageTravelMultiplier(town.X, town.Y, town.NationId);
        Check(
            !e.IsSettlementActive(town.Id) && !e.GetResidentEffects(e.State.Residents[0].Id).Any(f => f.Name == "家园休息"),
            "Unoccupied center retained its tier or center bonus.");
        Claim(e, 25);
        Check(e.IsSettlementActive(town.Id) && e.MessageTravelMultiplier(town.X, town.Y, town.NationId) > inactiveSpeed,
            "Three-radius equivalent area did not enable bonuses.");
        var edge = e.State.Tiles.Last(t => t.ClaimedSettlementId == town.Id);
        edge.NationId = edge.ClaimedSettlementId = 0;
        Check(!e.IsSettlementActive(town.Id), "Area cache ignored a lost claim.");
        Claim(e, 100);
        town.Tier = SettlementTier.Town;
        town.Population = 160;
        town.MaxClaimRadius = 17;
        town.Resources = new ResourceStock { Food = 1000, Wood = 1000, Stone = 1000, Ore = 1000 };
        Check(
            e.GetSettlementExpansionArea(town.Id) == 181 &&
            e.SettlementExpansionError(town.Id)?.Contains("独占陆地") == true,
            "Fixed 100-plot city requirement bypassed half-radius area.");
        Claim(e, 181);
        Check(e.SettlementExpansionError(town.Id) is null, "Equivalent half-radius area was rejected.");
    }

    [UnitTest]
    private static void Gathering()
    {
        var e = Flat(1);
        var person = e.State.Residents.Single();
        person.X = 16;
        person.Y = 16;
        var source = e.State.Tiles[16 * 32 + 16];
        source.Terrain = TerrainType.Forest;
        source.Plants = new PlantCoverage { Trees = 1 };
        Invoke(e, "GatherActualResources", person, Profession.Lumberjack);
        var outside = person.Inventory.Wood;
        var remaining = source.ResourceAmount;
        Claim(e, 61);
        Invoke(e, "GatherActualResources", person, Profession.Lumberjack);
        Check(Math.Abs(person.Inventory.Wood - outside * 3) < 1e-9 && source.ResourceAmount < remaining,
            "Outside harvesting was not half speed or failed to spend source stock.");
        source.Terrain = TerrainType.River;
        source.Wildlife = WildlifeKind.Fish;
        source.WildlifePopulation = 10;
        person.X = 15;
        person.Y = 16;
        person.Agent.Goal = new AgentGoal
        {
            Kind = AgentGoalKind.Fish, TargetEntityId = 16 * 32 + 17, TargetX = 15, TargetY = 16,
        };
        e.State.Tick++;
        source.NationId = source.ClaimedSettlementId = 0;
        var food = person.Inventory.Food;
        Check(e.TryHarvestWildlife(person), "Outside shore fishing failed.");
        var fishOutside = person.Inventory.Food - food;
        var town = e.State.Settlements.Single();
        source.NationId = town.NationId;
        source.ClaimedSettlementId = town.Id;
        food = person.Inventory.Food;
        Check(e.TryHarvestWildlife(person), "Inside shore fishing failed.");
        Check(Math.Abs(person.Inventory.Food - food - fishOutside * 2) < 1e-9,
            "Fishing penalty used the bank instead of the actual resource source.");
    }

    [UnitTest]
    private static void Construction()
    {
        var e = Flat();
        var town = e.State.Settlements.Single();
        Claim(e, 181);
        e.State.Rules.Construction = true;
        town.Resources = new ResourceStock { Food = 0, Wood = 1000, Stone = 1000 };
        foreach (var t in e.State.Tiles) t.Fertility = 0;
        for (var y = 0; y < 32; y++)
            e.State.Tiles[y * 32 + 13].Terrain = y == 25 ? TerrainType.Grass : TerrainType.River;
        e.State.Tiles[16 * 32 + 15].Fertility = 100;
        Invoke(e, "PlanLocalDevelopment", town);
        Check(e.State.Society.Buildings.All(b => b.Kind != BuildingKind.Farm),
            "Planner built on useless land or across a visible impassable barrier.");
        e.State.Tiles[18 * 32 + 12].Fertility = 80;
        Invoke(e, "PlanLocalDevelopment", town);
        var farm = e.State.Society.Buildings.Single(b => b.Kind == BuildingKind.Farm);
        Check(farm.X == 12 && farm.Y == 18 && farm.PlanningReason.Contains("粮食") && farm.SiteReason.Contains("肥力 80"),
            "Useful site or concrete planning evidence was missing.");
        // 导出世界前核对编辑河流后的领地连通性。
        e.ReconcileSocietyTopology();
        var resumed = WorldEngine.ImportJson(e.ExportJson());
        Check(resumed.State.Society.Buildings.Single(b => b.Id == farm.Id).PlanningReason == farm.PlanningReason,
            "Construction reason was lost in saves.");
    }

    [UnitTest]
    private static void BridgePurpose()
    {
        foreach (var landDetour in new[] { true, false })
        {
            var e = Flat(1);
            var town = e.State.Settlements.Single();
            Claim(e, 181);
            town.Resources = new ResourceStock { Food = 1000, Wood = 1000, Stone = 1000 };
            e.GrantReceivedResearch(town.Id, Advancement.Logistics);
            e.State.Rules.Construction = true;
            var person = e.State.Residents.Single();
            var farm = e.GrantFacility(town.Id, BuildingKind.Farm, 18, 16);
            for (var y = 0; y < 32; y++)
                e.State.Tiles[y * 32 + 13].Terrain = landDetour && y == 17 ? TerrainType.Grass : TerrainType.River;
            if (!landDetour)
            {
                foreach (var (x, y) in new[] { (14, 15), (14, 17), (15, 16) })
                    e.State.Tiles[y * 32 + x].Terrain = TerrainType.Mountain;
            }

            person.Agent.Goal = new AgentGoal
            {
                Kind = AgentGoalKind.Work,
                TargetX = 18,
                TargetY = 16,
                TargetEntityId = farm,
                PlayerDirected = true,
                ReviewTick = 1000,
            };
            e.Step();
            Check(e.State.Society.Buildings.All(b => b.Kind != BuildingKind.Bridge),
                landDetour
                    ? "A usable visible detour still caused a bridge."
                    : "An isolated far bank caused a bridge unrelated to the actual task.");
        }
    }

    [UnitTest]
    private static void UpgradeClaimPriority()
    {
        var e = Flat(60);
        var town = e.State.Settlements.Single();
        Claim(e, 25);
        town.MaxClaimRadius = 10;
        var farm = e.GrantFacility(town.Id, BuildingKind.Farm, 12, 18);
        var builder = e.State.Residents[0];
        builder.Profession = Profession.Builder;
        builder.Inventory.Food = 3;
        builder.Inventory.Water = 2;
        builder.Agent.NextThinkTick = e.State.Tick;
        builder.Agent.Goal = new AgentGoal
        {
            Kind = AgentGoalKind.Work, TargetX = 12, TargetY = 18, TargetEntityId = farm,
        };
        e.State.Rules.Expansion = true;
        e.Step();
        Check(builder.Agent.Goal.Kind == AgentGoalKind.ClaimLand && builder.Agent.Goal.Reason.Contains("升级面积"),
            $"Builder chose {builder.Agent.Goal.Kind}: {builder.Agent.Goal.Reason}; profession {builder.Profession}, population {town.Population}, area {e.GetSettlementArea(town.Id)}.");
    }

    [UnitTest]
    private static void TaskDescriptions()
    {
        var e = Flat(1);
        var person = e.State.Residents.Single();
        person.Profession = Profession.Farmer;
        person.Agent.Goal = new AgentGoal
        {
            Kind = AgentGoalKind.Gather, TargetX = 13, TargetY = 16, Reason = "亲眼看见可食资源",
        };
        var before = e.ExportJson();
        var summary = e.GetResidentActionSummary(person.Id);
        Check(
            summary.Contains("当前任务：采集浆果、草籽或嫩叶") && !summary.Contains("野生食物") && summary.Contains("当前劳作：正在前往") &&
            !summary.Contains("任务地点："),
            "Adjacent exact-site work omitted its plant products or was described as already happening.");
        Check(e.ExportJson() == before, "Explaining a task changed the world.");
    }

    private static void Navigation()
    {
        var e = Flat(1);
        var person = e.State.Residents.Single();
        for (var y = 0; y < 32; y++) e.State.Tiles[y * 32 + 13].Terrain = TerrainType.River;
        person.Agent.Goal = new AgentGoal
        {
            Kind = AgentGoalKind.Explore,
            TargetX = 16,
            TargetY = 16,
            PlayerDirected = true,
            ReviewTick = 1000,
        };
        var positions = new HashSet<(int, int)> { (person.X, person.Y) };
        for (var i = 0; i < 160; i++)
        {
            var previous = (person.X, person.Y);
            e.Step();
            if (previous != (person.X, person.Y))
                Check(positions.Add((person.X, person.Y)), "Navigation loop re-entered a visited tile.");
        }

        Check(
            person.Agent.Goal.NavigationRetryTick > 0 &&
            e.State.Society.Buildings.All(b => b.Kind != BuildingKind.Bridge),
            "Blocked exploration never stopped or invented a bridge.");
    }

    [LongRunningTest]
    private static void FoodWeb()
    {
        foreach (var (size, seed) in new[] { (128, 73), (256, 42), (256, 451) })
        {
            var e = WorldEngine.Create(seed, size, size, false);
            e.ConfigureWorld(
                new WorldRules
                {
                    ResourceRegeneration = false, Construction = false, Expansion = false, Births = false,
                }, false,
                false);
            var initial = e.State.Tiles.Sum(t => AnimalRules.Species.Sum(k => t.AnimalPopulation(k)));
            e.Step(6000);
            Check(e.State.Tiles.Sum(t => AnimalRules.Species.Sum(k => t.AnimalPopulation(k))) > initial * .1,
                $"Food web collapsed without residents: {size}, {seed}");
            Check(e.State.Tiles.Count(t => AnimalRules.Species.Any(k => t.AnimalPopulation(k) >= .5)) > size * size / 4,
                $"Animals became invisible across most of the map: {size}, {seed}");
            var resumed = WorldEngine.ImportJson(e.ExportJson());
            e.Step(128);
            resumed.Step(128);
            Check(e.ExportJson() == resumed.ExportJson(), "Ecology resumed with different population or random state.");
        }
    }
}
