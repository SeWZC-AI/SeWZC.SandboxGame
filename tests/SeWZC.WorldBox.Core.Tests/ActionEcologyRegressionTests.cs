using System.Reflection;
using System.Text.Json.Nodes;
using SeWZC.WorldBox.Core;

internal static class ActionEcologyRegressionTests
{
    public static IEnumerable<(string Name, Action Run)> Cases =>
    [
        ("full food webs remain at equilibrium at every carrying capacity", Equilibrium),
        ("prey shortages kill predators and extreme overcrowding can exhaust prey", Starvation),
        ("shared predator budgets retain a mixed food web across long revisits", Diversity),
        ("wildlife presentation selects actual dominant species without enum bias", WildlifeDisplay),
        ("full local stores stop needless gathering and real shortages restart work", SupplyDemand),
        ("factory carriers keep loaded inputs through adjacent home tiles", Cargo),
        ("pastures and aquaculture capture feed breed and preserve real stocks", Husbandry),
        ("essential construction proceeds during research and optional defenses wait for demand", Planning),
        ("ordinary buildings admit workers from all four sides", Orientation),
        ("herds reject corrupt saves and continue deterministically", Persistence),
        ("low-density hunting slows down and chooses productive visible prey", SustainableHunting),
        ("natural plants survive repeated collection and gatherers choose fuller sites", SustainablePlants),
        ("migration spends a small bounded share while preserving population", SlowMigration),
        ("inhabited worlds retain animal diversity and plants through autonomous collection", InhabitedDiversity)
    ];
    private static void Check(bool value, string message) { if (!value) throw new Exception(message); }
    [UnitTest]
    private static void SustainableHunting()
    {
        var (e, t, p) = World(); p.X = p.FromX = 24; p.Y = p.FromY = 24;
        p.MoveStartedTick = -100; p.MoveDurationTicks = 1; p.Inventory.Food = 0;
        var source = 24 * 32 + 24; var tile = e.State.Tiles[source];
        tile.Plants = new() { Grass = 1 }; tile.Wildlife = WildlifeKind.Deer;
        p.Agent.Goal = new() { Kind = AgentGoalKind.Hunt, TargetEntityId = source + 1 };
        tile.WildlifePopulation = .06; Check(e.TryHarvestWildlife(p), "Sparse prey could not be harvested at a reduced rate");
        var sparse = .06 - tile.WildlifePopulation;
        tile.WildlifePopulation = 1; Check(e.TryHarvestWildlife(p), "Dense prey could not be harvested");
        Check(1 - tile.WildlifePopulation > sparse * 4 && tile.WildlifePopulation > 0, "Sparse hunting did not slow down");
        tile.WildlifePopulation = .06;
        for (var i = 0; i < 500; i++) e.TryHarvestWildlife(p);
        Check(tile.WildlifePopulation > 0, "Repeated hunters exhausted the final breeding population");
        var better = e.State.Tiles[24 * 32 + 27]; better.Plants = new() { Grass = 1 }; better.Wildlife = WildlifeKind.Deer; better.WildlifePopulation = 2;
        var query = typeof(WorldEngine).GetMethod("FindHarvestableWildlife", BindingFlags.Instance | BindingFlags.NonPublic)!;
        var found = ((int Site, int Source, bool Fishing))query.Invoke(e, [p])!;
        Check(found.Source == 24 * 32 + 27, "Hunter stayed on sparse nearby prey instead of a productive visible site");
        foreach (var resident in e.State.Residents) resident.Race = RaceKind.Orc;
        var habitat = e.State.Tiles[19 * 32 + 19];
        habitat.Terrain = TerrainType.Forest; habitat.ResourceAmount = 100; habitat.Plants = new() { Trees = .7, Shrubs = .3 }; habitat.Wildlife = WildlifeKind.Deer; habitat.WildlifePopulation = 2;
        e.GrantFacility(t.Id, BuildingKind.HuntingCamp, 19, 19);
        Check(habitat.Terrain == TerrainType.Forest && habitat.ResourceAmount == 100 && habitat.Plants.Trees == .7,
            "A hunting camp destroyed its own wildlife habitat");
    }
    [UnitTest]
    private static void SustainablePlants()
    {
        var (e, t, p) = World(); p.X = p.FromX = 24; p.Y = p.FromY = 24; p.Profession = Profession.Farmer;
        var tile = e.State.Tiles[24 * 32 + 24]; tile.Plants = new() { Grass = 1 }; tile.ResourceAmount = .1;
        var gather = typeof(WorldEngine).GetMethod("GatherActualResources", BindingFlags.Instance | BindingFlags.NonPublic)!;
        for (var i = 0; i < 500; i++) gather.Invoke(e, [p, Profession.Farmer]);
        Check(tile.ResourceAmount > 0 && tile.Plants.Grass == 1 && PlantResources.At(tile).Single().Quantity > 0, "Collection cleared scarce plants or their displayed quantity");
        foreach (var ground in e.State.Tiles) ground.ResourceAmount = 1;
        var rich = 24 * 32 + 27; e.State.Tiles[rich].ResourceAmount = 100;
        var query = typeof(WorldEngine).GetMethod("FindVisibleResourceSite", BindingFlags.Instance | BindingFlags.NonPublic)!;
        Check((int)query.Invoke(e, [p, Profession.Farmer])! == rich, "Gatherer stayed on scarce plants instead of the fuller visible site");
        tile.ResourceAmount = 45; tile.Plants = new() { Grass = 1 };
        var before = e.ExportJson(); var detail = e.GetTileProductionSummary(24, 24);
        Check(detail.Contains("草本 45") && detail.Contains("草籽与嫩叶") && !detail.Contains("野生食物") && !detail.Contains("可采储量"), "Inspection omitted concrete plant quantities and products");
        Check(e.ExportJson() == before, "Plant inspection changed the world");
        tile.ResourceAmount = 100; tile.Plants = new() { Trees = .7, Shrubs = .3 };
        var treeStock = PlantResources.At(tile).Single(s => s.Kind == PlantKind.Trees).Quantity;
        gather.Invoke(e, [p, Profession.Farmer]);
        Check(Math.Abs(PlantResources.At(tile).Single(s => s.Kind == PlantKind.Trees).Quantity - treeStock) < 1e-9, "Food collection spent tree stock instead of edible plants");
    }
    [UnitTest]
    private static void SlowMigration()
    {
        var e = WorldEngine.Create(42, 32, 32, false); TestLand.ClearWildlife(e);
        foreach (var tile in e.State.Tiles) { tile.Terrain = TerrainType.Grass; tile.Fertility = 100; tile.ResourceAmount = 100; tile.NaturalWaterYield = .1; tile.Plants = new() { Grass = 1 }; }
        var source = e.State.Tiles[0]; source.Wildlife = WildlifeKind.Deer;
        var initial = source.WildlifePopulation = AnimalRules.EnvironmentalCapacity(source, WildlifeKind.Deer);
        e.State.Tick = 1; typeof(WorldEngine).GetMethod("TickWildlife", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(e, null);
        var migrated = e.State.Tiles[1].AnimalPopulation(WildlifeKind.Deer) + e.State.Tiles[32].AnimalPopulation(WildlifeKind.Deer);
        Check(migrated > 0 && migrated <= initial * .021 && source.WildlifePopulation >= initial * .979, "Animals dispersed too much in a single natural review");
        Check(Math.Abs(initial - source.WildlifePopulation - migrated) < 1e-9, "Migration created or lost animals");
    }
    [LongRunningTest]
    private static void InhabitedDiversity()
    {
        foreach (var seed in new[] { 42, 73921 })
        {
            var e = WorldEngine.Create(seed, 128, 128); e.State.NaturalDisasters = false;
            e.ConfigureWorld(e.State.Rules with { Wars = false, Conflict = 0 }, false, true);
            e.Step(6000);
            Check(AnimalRules.Species.All(k => e.State.Tiles.Sum(t => t.AnimalPopulation(k)) > 1), $"Residents exhausted a wildlife species at seed {seed}");
            Check(e.State.Tiles.Sum(t => PlantResources.At(t).Sum(p => p.Quantity)) > 1000, "Autonomous collection exhausted vegetation");
            var resumed = WorldEngine.ImportJson(e.ExportJson()); e.Step(24); resumed.Step(24);
            Check(e.ExportJson() == resumed.ExportJson(), "Sustainable collection lost deterministic continuation");
        }
    }
    private static (WorldEngine E, Settlement T, Resident P) World()
    {
        var e = WorldEngine.Create(42, 32, 32, false);
        foreach (var tile in e.State.Tiles) { tile.Terrain = TerrainType.Grass; tile.ResourceAmount = 100; tile.Fertility = 100; tile.NaturalWaterYield = .1; tile.Plants = new() { Grass = 1 }; }
        TestLand.ClearWildlife(e); e.SpawnResidents(16, 16, RaceKind.Human, 12); TestLand.ClaimAllTowns(e);
        e.ConfigureWorld(new WorldRules { Births = false, Aging = false, Hunger = false, Thirst = false, Disease = false,
            Construction = false, Research = false, Expansion = false, Trade = false, Wars = false, Migration = false, Alliances = false, Secession = false }, false, true);
        e.State.Tick = 120;
        var t = e.State.Settlements.Single(); foreach (var k in AdvancementRules.Resources) t.Resources.Set(k, 200);
        foreach (var p in e.State.Residents) { p.Age = 30; p.Agent.Initialized = true; p.Agent.NextThinkTick = 500; }
        return (e, t, e.State.Residents[0]);
    }
    private static void Work(Resident p, Building b, long tick)
    {
        p.Agent.Goal = new() { Kind = AgentGoalKind.Work, TargetEntityId = b.Id, TargetX = b.X, TargetY = b.Y,
            PlayerDirected = true, StartedTick = tick, ReviewTick = tick + 200 };
        p.Agent.NextThinkTick = tick + 200;
    }
    [UnitTest]
    private static void WildlifeDisplay()
    {
        var tile = new Tile { Wildlife = WildlifeKind.Rabbit, WildlifePopulation = .1,
            OtherWildlife = new() { Waterfowl = .8, Goat = .7, Deer = .2 } };
        Check(WorldEngine.VisibleWildlife(tile, 0) == WildlifeKind.Waterfowl && WorldEngine.VisibleWildlife(tile, 2) == WildlifeKind.Goat,
            "Earlier enum species obscured more abundant species");
    }
    private static void SetPopulation(Tile tile, WildlifeKind kind, double population)
    { var others = tile.OtherWildlife; others.Set(kind, population); tile.OtherWildlife = others; }
    [UnitTest]
    private static void Equilibrium()
    {
        var e = WorldEngine.Create(42, 32, 32, false);
        var tick = typeof(WorldEngine).GetMethod("TickWildlife", BindingFlags.Instance | BindingFlags.NonPublic)!;
        foreach (var terrain in new[] { TerrainType.Grass, TerrainType.Forest, TerrainType.Snow, TerrainType.Desert, TerrainType.Lake, TerrainType.Water })
        {
            foreach (var tile in e.State.Tiles)
            {
                tile.Terrain = terrain; tile.Fertility = 100; tile.ResourceAmount = 100; tile.NaturalWaterYield = .1;
                tile.Plants = new() { Grass = .7, Trees = .3 }; tile.Wildlife = WildlifeKind.None; tile.WildlifePopulation = 0; tile.OtherWildlife = default;
                foreach (var kind in AnimalRules.Species.Where(k => AnimalRules.For(k).Diet == AnimalDiet.Herbivore)) SetPopulation(tile, kind, WorldEngine.WildlifeCapacity(tile, kind));
                foreach (var kind in AnimalRules.Species.Where(k => AnimalRules.For(k).Diet == AnimalDiet.Carnivore)) SetPopulation(tile, kind, WorldEngine.WildlifeCapacity(tile, kind));
            }
            var expected = AnimalRules.Species.Select(k => e.State.Tiles[0].AnimalPopulation(k)).ToArray();
            for (var day = 0; day < 18; day++) { e.State.Tick++; tick.Invoke(e, null); }
            foreach (var tile in e.State.Tiles)
                for (var k = 0; k < expected.Length; k++)
                    Check(Math.Abs(tile.AnimalPopulation(AnimalRules.Species[k]) - expected[k]) < 1e-9, $"Capacity failed equilibrium: {terrain}, {AnimalRules.Species[k]}");
        }
    }
    [UnitTest]
    private static void Starvation()
    {
        var e = WorldEngine.Create(42, 32, 32, false);
        var tick = typeof(WorldEngine).GetMethod("TickWildlife", BindingFlags.Instance | BindingFlags.NonPublic)!;
        void Fill(double predators)
        {
            foreach (var tile in e.State.Tiles)
            {
                tile.Terrain = TerrainType.Grass; tile.Fertility = 100; tile.ResourceAmount = 100; tile.NaturalWaterYield = .1;
                tile.Plants = new() { Grass = 1 }; tile.Wildlife = WildlifeKind.Deer; tile.WildlifePopulation = .01;
                tile.OtherWildlife = new() { Wolf = predators };
            }
        }
        var previous = .01;
        foreach (var pressure in new[] { .001, .01, .1, .2, .4, .8, 2, 10, 50, 200, 1000d })
        {
            Fill(pressure); e.State.Tick = 1; tick.Invoke(e, null);
            var prey = e.State.Tiles[0].AnimalPopulation(WildlifeKind.Deer);
            Check(prey <= previous + .0003, "Increasing pressure increased prey survival"); previous = prey;
            Fill(pressure * 1.00001); e.State.Tick = 1; tick.Invoke(e, null);
            Check(Math.Abs(prey - e.State.Tiles[0].AnimalPopulation(WildlifeKind.Deer)) < .000001,
                "Predation jumped discontinuously at a pressure threshold");
        }
        Fill(.4); e.State.Tick = 1; tick.Invoke(e, null);
        var tested = e.State.Tiles[0];
        Check(tested.AnimalPopulation(WildlifeKind.Wolf) < .12 && tested.AnimalPopulation(WildlifeKind.Deer) > .001,
            "Food shortage destroyed prey instead of starving predators");
        Fill(1000); e.State.Tick = 1; tick.Invoke(e, null);
        Check(tested.AnimalPopulation(WildlifeKind.Deer) == 0 && tested.AnimalPopulation(WildlifeKind.Wolf) < 1,
            "An artificial prey floor blocked extreme overcrowding or predators survived famine");
    }
    [LongRunningTest]
    private static void Diversity()
    {
        foreach (var seed in new[] { 42, 73921 })
        foreach (var size in new[] { 128, 256 })
        {
            var e = WorldEngine.Create(seed, size, size, false); e.ConfigureWorld(new WorldRules { ResourceRegeneration = false }, false, true);
            e.Step(6000);
            Check(AnimalRules.Species.All(k => e.State.Tiles.Sum(t => t.AnimalPopulation(k)) > 1), $"Food-web extinction at seed {seed}, size {size}");
            foreach (var k in new[] { WildlifeKind.Deer, WildlifeKind.Boar, WildlifeKind.Goat, WildlifeKind.Bison })
                Check(e.State.Tiles.Count(t => t.AnimalPopulation(k) >= .08) > 20, $"Only rabbits remained visible: {k}");
        }
    }
    private static void SupplyDemand()
    {
        var (e, t, p) = World(); p.Profession = Profession.Farmer; p.Inventory.Food = 2;
        p.X = p.FromX = t.X; p.Y = p.FromY = t.Y;
        var choose = typeof(WorldEngine).GetMethod("ChooseAgentGoal", BindingFlags.Instance | BindingFlags.NonPublic)!;
        choose.Invoke(e, [p, t, false]);
        Check(p.Agent.Goal.Kind != AgentGoalKind.Gather && (p.Agent.Goal.Kind != AgentGoalKind.Work
            || e.State.Society.Buildings.FirstOrDefault(b => b.Id == p.Agent.Goal.TargetEntityId)?.Kind != BuildingKind.Workshop),
            "Well-supplied resident continued useless supply shuttling");
        t.Resources.Food = 0; t.Resources.Wood = 0;
        p.Agent.Goal = new() { Kind = AgentGoalKind.Idle }; choose.Invoke(e, [p, t, false]);
        Check(p.Agent.Goal.Kind is AgentGoalKind.Gather or AgentGoalKind.Work, "Actual shortage failed to restart useful work");
    }
    private static void Cargo()
    {
        var (e, t, p) = World();
        foreach (var k in new[] { ResearchKind.Agriculture, ResearchKind.Logistics, ResearchKind.Industry }) e.GrantReceivedResearch(t.Id, k);
        var id = e.GrantFacility(t.Id, BuildingKind.Foundry, 19, 16); var b = e.State.Society.Buildings.Single(b => b.Id == id);
        foreach (var other in e.State.Residents.Skip(1)) other.Agent.NextThinkTick = 10000;
        p.X = p.FromX = 17; p.Y = p.FromY = 16; p.Inventory = new() { Food = 2, Water = 2, Ore = 8, Coal = 4 };
        t.Resources.Alloy = 0; Work(p, b, e.State.Tick); e.Step();
        Check(p.Inventory.Ore == 8 && p.Inventory.Coal == 4, "Passing the home apron unloaded job cargo");
        e.Step(28); Check(b.ProductionBatches >= 4 && t.Resources.Alloy >= 4, "Carrier failed to complete and deliver its real multi-batch load");
    }
    private static void Husbandry()
    {
        var (e, t, p) = World();
        foreach (var k in new[] { ResearchKind.Agriculture, ResearchKind.Logistics, ResearchKind.Industry }) e.GrantReceivedResearch(t.Id, k);
        var source = e.State.Tiles[16 * 32 + 19]; source.OtherWildlife = new() { Goat = 2 };
        var id = e.GrantFacility(t.Id, BuildingKind.Pasture, 19, 16); var b = e.State.Society.Buildings.Single(b => b.Id == id);
        p.X = p.FromX = b.X; p.Y = p.FromY = b.Y; p.Profession = Profession.Farmer; p.Inventory = new() { Food = 2, Water = 2 }; Work(p, b, e.State.Tick);
        Check(e.TryWorkAtBuilding(p) && b.LivestockPopulation == 1 && source.AnimalPopulation(WildlifeKind.Goat) == 1, "Pasture created free livestock");
        e.State.Tick++; var herd = b.LivestockPopulation; var food = p.Inventory.Food;
        Check(e.TryWorkAtBuilding(p) && b.LivestockPopulation > herd && p.Inventory.Food < food, "Feeding failed to grow the real herd");
        e.State.Tick++; p.Inventory.Water = 0; Check(!e.TryWorkAtBuilding(p), "Pasture bred without delivered water");
        var water = e.State.Tiles[19 * 32 + 19]; water.Terrain = TerrainType.Lake; water.OtherWildlife = new() { Fish = 2 };
        id = e.GrantFacility(t.Id, BuildingKind.Aquaculture, 19, 18); b = e.State.Society.Buildings.Single(b => b.Id == id);
        p.X = p.FromX = b.X; p.Y = p.FromY = b.Y; p.Profession = Profession.Fisher; p.Inventory = new() { Food = 2, Water = 2 }; Work(p, b, e.State.Tick);
        Check(e.TryWorkAtBuilding(p) && b.LivestockKind == WildlifeKind.Fish && water.AnimalPopulation(WildlifeKind.Fish) == 1, "Aquaculture failed to capture from its real neighboring water");
        b.LivestockPopulation = 4; e.State.Tick++; food = p.Inventory.Food;
        Check(e.TryWorkAtBuilding(p) && b.ProductionBatches == 1 && p.Inventory.Food > food && t.Resources.Food == 200, "Harvest bypassed breeding stock or physical delivery");
    }
    private static void Planning()
    {
        var (e, t, p) = World();
        e.ConfigureWorld(e.State.Rules with { Construction = true, Research = true, Thirst = true }, false, true);
        foreach (var k in new[] { ResearchKind.Agriculture, ResearchKind.Logistics, ResearchKind.Industry, ResearchKind.Ballistics }) e.GrantReceivedResearch(t.Id, k);
        e.GrantFacility(t.Id, BuildingKind.Academy, 18, 18);
        var research = e.State.Society.Research.Single(); research.ActiveProject = ResearchKind.ScientificMethod; research.RequiredProgress = 100;
        t.Resources.Water = 0;
        var pending = e.State.Society.Buildings.First(b => b.Kind == BuildingKind.Workshop); pending.ConstructionProgress = 0;
        var method = typeof(WorldEngine).GetMethod("PlanLocalDevelopment", BindingFlags.Instance | BindingFlags.NonPublic)!;
        method.Invoke(e, [t]);
        Check(e.State.Society.Buildings.Any(b => b.Kind == BuildingKind.Well), "Active research or an unrelated site prevented essential water construction");
        Check(!e.State.Society.Buildings.Any(b => b.Kind == BuildingKind.Arsenal), "No-defense town built an idle arsenal");
        e.Step(120);
        Check(e.State.Society.Research.Single().Progress > 0 || e.HasResearch(t.Id, ResearchKind.ScientificMethod), "Useful research never gained an actual worker");
    }
    [UnitTest]
    private static void Orientation()
    {
        var (e, t, p) = World(); var id = e.GrantFacility(t.Id, BuildingKind.Farm, 20, 20); var b = e.State.Society.Buildings.Single(b => b.Id == id);
        foreach (var (x, y) in new[] { (19, 20), (21, 20), (20, 19), (20, 21) })
        { Check(e.CanTraverseStep(x, y, 20, 20, TravelMode.Foot), "Ordinary facility required a facing direction"); }
    }
    [UnitTest]
    private static void Persistence()
    {
        var (e, t, p) = World(); var id = e.GrantFacility(t.Id, BuildingKind.Pasture, 19, 16); var b = e.State.Society.Buildings.Single(b => b.Id == id);
        b.LivestockKind = WildlifeKind.Goat; b.LivestockPopulation = 3;
        var save = e.ExportJson(); var resumed = WorldEngine.ImportJson(save); e.Step(12); resumed.Step(12);
        Check(e.ExportJson() == resumed.ExportJson(), "Herd continuation was not deterministic");
        var bad = JsonNode.Parse(save)!; var node = bad["Society"]!["Buildings"]!.AsArray().Single(n => n!["Id"]!.GetValue<int>() == id)!;
        node["LivestockKind"] = (int)WildlifeKind.Wolf;
        try { WorldEngine.ImportJson(bad.ToJsonString()); throw new Exception("Predatory livestock accepted"); } catch (ArgumentException) { }
        node["LivestockKind"] = (int)WildlifeKind.Goat; node["LivestockPopulation"] = 100;
        try { WorldEngine.ImportJson(bad.ToJsonString()); throw new Exception("Overcapacity herd accepted"); } catch (ArgumentException) { }
    }
}
