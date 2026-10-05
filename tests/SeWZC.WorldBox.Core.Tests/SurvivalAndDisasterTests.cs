using System.Text.Json.Nodes;
using SeWZC.WorldBox.Core;

internal static class SurvivalAndDisasterTests
{
    public static IEnumerable<(string Name, Action Run)> Cases =>
    [
        ("wetlands provide shared local water without storing unused daily yield", WetlandWater),
        ("hungry residents eat before damage and cannot hoard others' daily meals", SharedMeals),
        ("founding families stay on their camp's connected shore with equal rations", ConnectedSpawn),
        ("water searches can be interrupted for reachable food", FoodInterruptsWater),
        ("fire starts locally, respects wet ground and burns buildings by combustibility", Fire),
        ("actual firefighting consumes water gradually and resumes with its daily limit", Firefighting),
        ("plague starts with few cases, spreads by contact and grants temporary recovery immunity", Plague),
        ("six generated worlds survive their first five years without supply deaths", OpeningSurvival),
    ];

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
    }

    private static WorldEngine Flat(int people = 4)
    {
        var e = WorldEngine.Create(42, 32, 32, false);
        foreach (var t in e.State.Tiles)
        {
            t.Terrain = TerrainType.Grass;
            t.Fertility = 80;
            t.ResourceAmount = 100;
            t.NaturalWaterYield = .001;
            t.Plants = new PlantCoverage { Grass = .6 };
            t.Wildlife = WildlifeKind.None;
            t.WildlifePopulation = 0;
            t.OtherWildlife = default;
        }

        e.ConfigureWorld(new WorldRules
        {
            Aging = false, Births = false, Expansion = false, Construction = false,
            Research = false, Migration = false, Secession = false, Trade = false, Wars = false, Peace = false,
            Alliances = false, Hunger = false, Thirst = false, Disease = false, ResourceRegeneration = false,
        }, false, false);
        e.State.Tick = 1;
        e.SpawnResidents(16, 16, RaceKind.Human, people);
        foreach (var r in e.State.Residents) Hold(e, r, AgentGoalKind.Rest, 16, 16);
        return e;
    }

    private static void Hold(WorldEngine e, Resident r, AgentGoalKind kind, int x, int y, int source = 0)
    {
        r.X = r.FromX = x;
        r.Y = r.FromY = y;
        r.Age = 25;
        r.MoveStartedTick = e.State.Tick - 1;
        r.MoveDurationTicks = 1;
        r.Agent.Goal = new AgentGoal
        {
            Kind = kind, TargetX = x, TargetY = y, TargetEntityId = source,
            PlayerDirected = true, StartedTick = e.State.Tick, ReviewTick = e.State.Tick + 1000,
        };
    }

    [UnitTest]
    private static void WetlandWater()
    {
        var e = Flat(3);
        var index = 20 * 32 + 20;
        var wet = e.State.Tiles[index];
        wet.Terrain = TerrainType.Wetland;
        wet.Rainfall = wet.NaturalWaterYield = 1;
        Check(e.AvailableWater(20, 20) == 1, "Wetland retained only a trace water yield.");
        foreach (var r in e.State.Residents)
        {
            r.Inventory.Water = 0;
            Hold(e, r, AgentGoalKind.FetchWater, 20, 20, index + 1);
            e.TryFetchWater(r);
        }

        Check(e.State.Residents.Sum(r => r.Inventory.Water) == 1 && e.AvailableWater(20, 20) == 0,
            "Wetland carriers exceeded the shared daily allowance.");
        e.State.Tick = 120;
        Check(e.AvailableWater(20, 20) == 1, "Unused wetland water accumulated or did not renew.");
        wet.DroughtTicks = 10;
        Check(e.AvailableWater(20, 20) == .2, "Drought did not reduce wetland water.");
        var away = e.State.Residents[0];
        Hold(e, away, AgentGoalKind.FetchWater, 19, 20, index + 1);
        Check(!e.TryFetchWater(away), "Wetland water was taken without arriving on its tile.");
    }

    private static void SharedMeals()
    {
        var e = Flat(12);
        e.State.Rules.Hunger = true;
        var town = e.State.Settlements.Single();
        town.Resources.Food = e.State.Residents.Sum(WorldEngine.FoodUse);
        foreach (var r in e.State.Residents)
        {
            r.Inventory.Food = 0;
            r.Hunger = 82;
            r.Health = .1;
        }

        e.Step();
        Check(e.State.Residents.Count == 12 && e.State.Residents.All(r => r.Hunger < 80),
            "List order hoarded shared meals or starvation damage killed a resident before eating.");
        Check(town.Resources.Food < .000001,
            "Meals were not physically deducted from the warehouse.");
        var fatal = Flat(1);
        var person = fatal.State.Residents.Single();
        var home = fatal.State.Settlements.Single();
        fatal.State.Rules.Hunger = true;
        home.Resources.Food = 10;
        fatal.State.Society.Policies.Single().Kind = PolicyKind.PublicHealth;
        fatal.State.Society.Policies.Single().PlayerOverride = true;
        Hold(fatal, person, AgentGoalKind.Rest, home.X + 2, home.Y);
        person.Inventory.Food = 0;
        person.Hunger = 100;
        person.Health = .1;
        fatal.Step();
        Check(fatal.State.Residents.Count == 0 && fatal.State.ArchivedResidents.Single().DeathCause ==
                                               DeathCause.Starvation
                                               && fatal.State.ArchivedResidents.Single().Health == 0,
            "A resident dead before reaching food was revived by the later daily healthcare phase.");
    }

    private static void ConnectedSpawn()
    {
        var e = Flat(1);
        for (var y = 0; y < 32; y++) e.PaintTerrain(18, y, TerrainType.River, 0);
        e.State.Rules.Hunger = e.State.Rules.Thirst = true;
        e.State.Settlements.Single().Resources.Food = e.State.Settlements.Single().Resources.Water = 100;
        e.SpawnResidents(16, 16, RaceKind.Human, 30);
        Check(e.State.Residents.All(r => r.X < 18), "A founding resident appeared on an inaccessible shore.");
        Check(e.State.Residents.Skip(1).All(r => r.Inventory.Food == 1 && r.Inventory.Water == .75),
            "Starting rations depended on profession or initial target coordinates.");
    }

    private static void FoodInterruptsWater()
    {
        var e = Flat(1);
        var r = e.State.Residents.Single();
        e.State.Rules.Hunger = e.State.Rules.Thirst = true;
        r.Hunger = 65;
        r.Inventory.Food = r.Inventory.Water = 0;
        Hold(e, r, AgentGoalKind.FetchWater, 24, 16);
        r.Agent.Goal.TargetX = 30;
        r.Agent.Goal.PlayerDirected = false;
        r.Agent.NextThinkTick = 0;
        r.Profession = Profession.Scholar;
        e.Step();
        Check(r.Agent.Goal.Kind == AgentGoalKind.Gather && r.Inventory.Food > WorldEngine.FoodUse(r),
            "A hungry water seeker stayed committed while reachable food was present.");
        var e2 = Flat(1);
        var p = e2.State.Residents.Single();
        for (var y = 0; y < 32; y++) e2.PaintTerrain(18, y, TerrainType.River, 0);
        foreach (var t in e2.State.Tiles)
            if (t.Terrain != TerrainType.River)
                t.Fertility = 30;
        e2.State.Tiles[16 * 32 + 19].Fertility = 100;
        e2.State.Settlements.Single().Resources.Food = 0;
        e2.State.Rules.Hunger = true;
        Hold(e2, p, AgentGoalKind.Idle, 17, 16);
        p.Hunger = 65;
        p.Inventory.Food = 0;
        p.Agent.Goal.PlayerDirected = false;
        p.Agent.NextThinkTick = 0;
        e2.Step();
        Check(p.Agent.Goal.Kind == AgentGoalKind.Gather && p.Agent.Goal.TargetX < 18,
            "A resident chose visible food across an impassable river.");
    }

    private static void Fire()
    {
        var e = Flat();
        e.PaintTerrain(24, 24, TerrainType.Wetland, 3);
        e.TriggerDisaster(24, 24, DisasterKind.Fire, 2);
        Check(e.State.Tiles.All(t => t.FireTicks == 0), "Ordinary wetland ignited without fuel.");
        e.TriggerDisaster(4, 4, DisasterKind.Fire, 8);
        Check(e.State.Tiles.Count(t => t.FireTicks > 0) == 1, "A local fire instantly ignited its entire brush area.");
        e.Step(7);
        Check(e.State.Tiles.Count(t => t.FireTicks > 0) == 1, "Fire spread before the slow propagation interval.");
        e.State.Rules.FireSpread = false;
        e.Step(25);
        Check(e.State.Tiles[4 * 32 + 4].FireTicks > 0, "A fire disappeared in a few days without firefighting.");
        var town = e.State.Settlements.Single();
        TestLand.ClaimAllTowns(e);
        var farmId = e.GrantFacility(town.Id, BuildingKind.Farm, 20, 20);
        var academyId = e.GrantFacility(town.Id, BuildingKind.Academy, 21, 19);
        var farm = e.State.Society.Buildings.Single(b => b.Id == farmId);
        var academy = e.State.Society.Buildings.Single(b => b.Id == academyId);
        e.PaintTerrain(20, 20, TerrainType.Wetland, 0);
        e.TriggerDisaster(20, 20, DisasterKind.Fire, 1);
        Check(e.State.Tiles[20 * 32 + 20].FireTicks > 0, "A wooden structure could not burn on wet ground.");
        e.TriggerDisaster(21, 19, DisasterKind.Fire, 1);
        e.Step();
        Check(
            farm.Health < academy.Health &&
            WorldEngine.BuildingFlammability(farm) > WorldEngine.BuildingFlammability(academy),
            "Wood and stone facilities sustained identical fire damage.");
    }

    private static void Firefighting()
    {
        var e = Flat(6);
        var index = 20 * 32 + 20;
        e.TriggerDisaster(20, 20, DisasterKind.Fire, 1);
        var before = e.State.Tiles[index].FireTicks;
        var fighter = e.State.Residents[0];
        Hold(e, fighter, AgentGoalKind.ExtinguishFire, 10, 20);
        fighter.Agent.Goal.TargetX = 20;
        fighter.Inventory.Water = 1;
        Check(!e.TryExtinguishFire(fighter), "A remote resident extinguished fire.");
        foreach (var r in e.State.Residents)
        {
            Hold(e, r, AgentGoalKind.ExtinguishFire, 19, 20);
            r.Agent.Goal.TargetX = 20;
            r.Inventory.Water = 1;
            e.TryExtinguishFire(r);
        }

        Check(
            e.State.Tiles[index].FireTicks == before - 2 &&
            Math.Abs(e.State.Residents.Sum(r => r.Inventory.Water) - 5.9) < 1e-9,
            "Many firefighters instantly extinguished the tile or failed to consume water.");
        var resumed = WorldEngine.ImportJson(e.ExportJson());
        Check(!resumed.TryExtinguishFire(resumed.State.Residents[0]),
            "Loading reset today's shared suppression limit.");
        e.Step(3);
        resumed.Step(3);
        Check(e.ExportJson() == resumed.ExportJson(), "Firefighting diverged after resuming.");
        var invalid = JsonNode.Parse(e.ExportJson())!;
        invalid["Tiles"]![index]!["FireSuppressed"] = 3;
        try
        {
            WorldEngine.ImportJson(invalid.ToJsonString());
            throw new Exception("Invalid suppression allowance accepted.");
        }
        catch (ArgumentException)
        {
        }
    }

    private static void Plague()
    {
        var e = Flat(36);
        e.State.Rules.Disease = true;
        foreach (var r in e.State.Residents.Skip(18)) Hold(e, r, AgentGoalKind.Rest, 28, 28);
        e.TriggerDisaster(16, 16, DisasterKind.Plague, 8);
        Check(e.State.Residents.Count(r => r.SicknessTicks > 0) == 1, "Plague instantly infected an entire town.");
        var patient = e.State.Residents.First(r => r.SicknessTicks > 0);
        e.Step(48);
        Check(e.State.Residents.Skip(18).All(r => r.SicknessTicks == 0), "Plague spread without physical contact.");
        Check(e.State.Residents.Take(18).Count(r => r.SicknessTicks > 0) is > 1 and < 18,
            "Contact spread was absent or instantaneous.");
        patient.SicknessTicks = 1;
        e.Step();
        var immuneUntil = patient.DiseaseImmuneUntilTick;
        Check(immuneUntil == e.State.Tick + 180, "Recovered patient received no temporary immunity.");
        e.Step(24);
        Check(patient.SicknessTicks == 0, "A recovering patient was immediately reinfected.");
        var resumed = WorldEngine.ImportJson(e.ExportJson());
        e.Step(12);
        resumed.Step(12);
        Check(e.ExportJson() == resumed.ExportJson(), "Disease immunity or contact spread diverged after loading.");
        var invalid = JsonNode.Parse(e.ExportJson())!;
        invalid["Residents"]![0]!["DiseaseImmuneUntilTick"] = e.State.Tick + 181;
        try
        {
            WorldEngine.ImportJson(invalid.ToJsonString());
            throw new Exception("Invalid disease immunity accepted.");
        }
        catch (ArgumentException)
        {
        }
    }

    [LongRunningTest]
    private static void OpeningSurvival()
    {
        foreach (var seed in new[] { 42, 73921, 451 })
        foreach (var size in new[] { 128, 256 })
        {
            var e = WorldEngine.Create(seed, size, size);
            var deaths = new HashSet<int>();
            for (var day = 0; day < 600; day++)
            {
                e.Step();
                foreach (var r in e.State.ArchivedResidents)
                    if (r.DeathCause is DeathCause.Starvation or DeathCause.Dehydration)
                        deaths.Add(r.Id);
            }

            Check(deaths.Count == 0,
                $"Seed {seed}, size {size}: {deaths.Count} supply deaths in the first five years.");
            Check(e.State.Residents.Count >= 144, $"Seed {seed}, size {size}: founding population failed to survive.");
        }
    }
}
