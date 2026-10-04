using System.Text.Json.Nodes;
using SeWZC.WorldBox.Core;

internal static class EcologyAndConflictTests
{
    public static IEnumerable<(string Name, Action Run)> Cases =>
    [
        ("settlements retain their paid tier and keep a real center through relocation", Centers),
        ("ecology grows toward habitat capacity, migrates and survives save resume", Ecology),
        ("multiple species share land, migrate into occupied habitats and persist", Coexistence),
        ("ecology animals in unsuitable or overpopulated land decline gradually", Decline),
        ("resource observation can reveal minerals without teaching residents", Resources),
        ("local scarcity escalates in stages and relief ends conflict", Conflicts),
        ("new ecology and conflict records reject corrupt saves", Persistence),
        ("deaths retain the specific fatal cause and survive save resume", Mortality),
        ("resident inspection distinguishes warehouse pickup, factory travel and blocked operation without simulating", Inspection),
        ("nearby farms do not hijack an actual forestry task", ForestryTask),
        ("a lone resident works instead of endlessly seeking unavailable conversation", LoneResident),
        ("messengers without foreign addresses explore an actual route and retain their destination while travelling", ExploreRoute)
    ];

    private static void ForestryTask()
    {
        var engine = Empty(); engine.SpawnResidents(16, 16, RaceKind.Human, 3);
        TestLand.ClaimAllTowns(engine);
        var town = engine.State.Settlements.Single(); engine.GrantFacility(town.Id, BuildingKind.Farm, 18, 16);
        var person = engine.State.Residents.First(p => p.Id != town.RepresentativeId);
        var tile = engine.State.Tiles[16 * engine.State.Width + 19]; tile.Terrain = TerrainType.Forest; tile.ResourceAmount = 100;
        engine.EditResident(person.Id, new ResidentEdit { X = 19, Y = 16, Age = 25, Profession = Profession.Lumberjack });
        person = engine.GetResident(person.Id)!;
        person.Agent.Goal = new AgentGoal { Kind = AgentGoalKind.Work, TargetX = 19, TargetY = 16, PlayerDirected = true,
            StartedTick = engine.State.Tick, ReviewTick = engine.State.Tick + 24, Reason = "到森林执行伐木任务" };
        var wood = person.Inventory.Wood;
        engine.Step();
        Require(person.Inventory.Wood > wood && tile.ResourceAmount < 100, $"A nearby farm stole the resource work target: {person.Profession}, {person.Activity}, {person.Agent.Goal.Kind}, wood={person.Inventory.Wood}");
    }

    private static WorldEngine Empty()
    {
        var engine = WorldEngine.Create(82, 32, 32, false);
        foreach (var tile in engine.State.Tiles)
        { tile.Terrain = TerrainType.Grass; tile.Fertility = 100; tile.ResourceAmount = 100; tile.Wildlife = WildlifeKind.None; tile.WildlifePopulation = 0; tile.OtherWildlife = default; tile.Plants = new PlantCoverage { Grass = .6 }; tile.NaturalWaterYield = .02; }
        engine.ConfigureWorld(new WorldRules { Births = false, Aging = false, Hunger = false, Thirst = false, Disease = false, Construction = false,
            Research = false, Expansion = false, Trade = false, Wars = false, Alliances = false, Migration = false, Secession = false }, false, false);
        return engine;
    }

    [UnitTest]
    private static void Centers()
    {
        var engine = Empty(); engine.SpawnResidents(16, 16, RaceKind.Human, 1);
        TestLand.ClaimAllTowns(engine);
        var town = engine.State.Settlements.Single(); Require(town.Name.EndsWith('村'), "Small settlement is not a village");
        engine.SpawnResidents(16, 16, RaceKind.Human, 59); Require(town.Name.EndsWith('村'), "Population bypassed paid town expansion");
        TestLand.ClaimAllTowns(engine);
        engine.SpawnResidents(16, 16, RaceKind.Human, 100); Require(town.Name.EndsWith('村'), "Population bypassed paid city expansion");
        TestLand.ClaimAllTowns(engine);
        var center = engine.State.Society.Buildings.Single(b => b.Kind == BuildingKind.TownCenter && b.SettlementId == town.Id);
        engine.PaintTerrain(16, 16, TerrainType.Water, 0);
        Require(center.X == town.X && center.Y == town.Y && engine.State.Society.Buildings.Contains(center), "Relocated settlement lost its real center");
        center.Health = 0; var ground = engine.State.Tiles[town.Y * engine.State.Width + town.X]; ground.FireTicks = 5;
        engine.State.Rules.Construction = true; town.Resources.Wood = 20; town.Resources.Stone = 10;
        engine.Step(); Require(center.Health == 0 && town.Resources.Wood == 20, "Burning ruins spent rebuilding materials before the fire ended");
        ground.FireTicks = 0; engine.Step();
        Require(center.Health > 0 && center.ConstructionProgress < center.ConstructionRequired && town.Resources.Wood == 8,
            "Destroyed center did not start a paid construction project after the fire ended");
        _ = WorldEngine.ImportJson(engine.ExportJson());
    }

    private static void Ecology()
    {
        var engine = Empty(); var source = engine.State.Tiles[16 * 32 + 16];
        source.Wildlife = WildlifeKind.Rabbit; source.WildlifePopulation = 1;
        engine.Step(12);
        Require(engine.State.Tiles.Sum(t => t.AnimalPopulation(WildlifeKind.Rabbit)) > 1 && engine.State.Tiles[16 * 32 + 17].WildlifePopulation > 0, "Animals did not reproduce and migrate");
        var restored = WorldEngine.ImportJson(engine.ExportJson()); engine.Step(36); restored.Step(36);
        Require(engine.ExportJson() == restored.ExportJson(), "Ecology diverged after resume");
        foreach (var tile in engine.State.Tiles) { tile.Wildlife = WildlifeKind.Rabbit; tile.WildlifePopulation = 1; }
        engine.Step(3600);
        Require(source.WildlifePopulation > 11.5 && source.WildlifePopulation <= WorldEngine.WildlifeCapacity(source, source.Wildlife) + .001,
            $"Logistic growth did not approach habitat capacity: source {source.WildlifePopulation}, capacity {WorldEngine.WildlifeCapacity(source, source.Wildlife)}, mean {engine.State.Tiles.Average(t => t.AnimalPopulation(WildlifeKind.Rabbit))}");
    }

    private static void Coexistence()
    {
        var engine = Empty(); var source = engine.State.Tiles[16 * 32 + 16]; var target = engine.State.Tiles[16 * 32 + 17];
        source.Terrain = target.Terrain = TerrainType.Forest; source.Plants = target.Plants = new PlantCoverage { Trees = .7, Shrubs = .3 };
        source.Wildlife = WildlifeKind.Deer; source.WildlifePopulation = 2;
        source.OtherWildlife = new WildlifePopulations { Boar = 1, Wolf = .5 };
        target.Wildlife = WildlifeKind.Boar; target.WildlifePopulation = 1;
        engine.Step(12);
        Require(engine.State.Tiles.Sum(t => t.AnimalPopulation(WildlifeKind.Deer)) > 2 && engine.State.Tiles.Sum(t => t.AnimalPopulation(WildlifeKind.Boar)) > 2, "Coexisting species failed to grow");
        Require(target.AnimalPopulation(WildlifeKind.Deer) > 0 && target.AnimalPopulation(WildlifeKind.Boar) > 0, "Migration displaced the other species");
        var summary = engine.GetTileProductionSummary(16, 16);
        Require(summary.Contains("鹿") && summary.Contains("野猪") && summary.Contains("乔木") && summary.Contains("灌木"), "Detailed ecology omits existing resources");
        var resumed = WorldEngine.ImportJson(engine.ExportJson()); engine.Step(120); resumed.Step(120);
        Require(engine.ExportJson() == resumed.ExportJson(), "Multispecies ecology failed deterministic resume");
        source.Terrain = TerrainType.Desert; var before = source.AnimalPopulation(WildlifeKind.Boar);
        engine.Step(6); Require(source.AnimalPopulation(WildlifeKind.Boar) < before, "Unsuitable species did not decline");
        var bad = JsonNode.Parse(engine.ExportJson())!;
        bad["Tiles"]![16 * 32 + 16]!["OtherWildlife"] = new JsonObject { ["Boar"] = -1 };
        try { WorldEngine.ImportJson(bad.ToJsonString()); throw new Exception("Corrupt secondary population accepted"); } catch (ArgumentException) { }
        var legacy = JsonNode.Parse(engine.ExportJson())!;
        foreach (var t in legacy["Tiles"]!.AsArray()) t!.AsObject().Remove("OtherWildlife");
        _ = WorldEngine.ImportJson(legacy.ToJsonString());
        var depleted = new Tile { Terrain = TerrainType.Forest, ResourceAmount = 0, Fertility = 100 };
        Require(!PlantResources.At(depleted).Any(), "Depleted land invents available plants");
    }

    [UnitTest]
    private static void Decline()
    {
        var engine = Empty(); var tile = engine.State.Tiles[16 * 32 + 16];
        tile.Terrain = TerrainType.Desert; tile.Wildlife = WildlifeKind.Deer; tile.WildlifePopulation = 50;
        engine.Step(6); Require(tile.WildlifePopulation is > 0 and < 50, "Unsuitable habitat did not gradually lose animals");
        tile.Terrain = TerrainType.Forest; tile.ResourceAmount = 100; var before = tile.WildlifePopulation;
        engine.Step(6); Require(tile.WildlifePopulation > 0 && tile.WildlifePopulation < before, "Excess animals survived without capacity pressure");
    }

    [UnitTest]
    private static void Resources()
    {
        var engine = Empty(); engine.SpawnResidents(16, 16, RaceKind.Human, 1);
        TestLand.ClaimAllTowns(engine);
        var tile = engine.State.Tiles[17 * 32 + 17]; tile.Deposit = ResourceKind.Oil; tile.DepositAmount = 120;
        var before = engine.ExportJson();
        Require(engine.GetTileProductionSummary(17, 17, ResourceVisibility.All).Contains("石油矿藏"), "All resources hides undiscovered oil");
        Require(!engine.IsDepositVisible(tile, ResourceVisibility.Researched), "Unknown research reveals oil by default");
        Require(engine.ExportJson() == before && !tile.DepositDiscovered, "Player inspection taught residents or changed the world");
        engine.GrantReceivedResearch(engine.State.Settlements[0].Id, ResearchKind.Electrification);
        Require(engine.IsDepositVisible(tile, ResourceVisibility.Researched), "Latest researched stage did not reveal oil");
    }

    private static void Conflicts()
    {
        var engine = Empty(); engine.SpawnResidents(16, 16, RaceKind.Human, 4);
        TestLand.ClaimAllTowns(engine);
        engine.State.Rules.Wars = true; engine.State.Rules.Conflict = 3; engine.State.Rules.Hunger = true;
        var town = engine.State.Settlements.Single(); town.Resources.Food = 0;
        foreach (var person in engine.State.Residents)
        {
            person.X = person.FromX = 16; person.Y = person.FromY = 16; person.Age = 25; person.Hunger = 50; person.Inventory.Food = 0;
            person.Agent.Goal = new AgentGoal { Kind = AgentGoalKind.Rest, TargetX = 16, TargetY = 16, ReviewTick = 1000, PlayerDirected = true };
        }
        void Advance(WorldEngine world, int ticks)
        { for (var i = 0; i < ticks; i += 12) { foreach (var person in world.State.Residents) { person.Hunger = 50; person.Health = 100; } world.Step(12); } }
        Advance(engine, 12); var conflict = engine.State.Conflicts.Single();
        Require(conflict.Stage == ConflictStage.Dispute && conflict.Scope == ConflictScope.Individual, "Scarcity immediately caused a major conflict");
        Advance(engine, 36); Require(conflict.Stage == ConflictStage.Confrontation, "Persistent scarcity did not create confrontation");
        var resumed = WorldEngine.ImportJson(engine.ExportJson()); Advance(engine, 60); Advance(resumed, 60);
        Require(engine.ExportJson() == resumed.ExportJson() && conflict.Stage == ConflictStage.Violence, "Conflict escalation or resume failed");
        town.Resources.Food = 1000; foreach (var person in engine.State.Residents) { person.Hunger = 0; person.Inventory.Food = 2; }
        engine.Step(120); Require(conflict.Stage == ConflictStage.Resolved, "Food relief did not resolve conflict");
    }

    [UnitTest]
    private static void Persistence()
    {
        var engine = Empty(); var json = JsonNode.Parse(engine.ExportJson())!;
        json["Tiles"]![0]!["WildlifePopulation"] = -1;
        try { WorldEngine.ImportJson(json.ToJsonString()); throw new Exception("Invalid animal population accepted"); } catch (ArgumentException) { }
        json = JsonNode.Parse(engine.ExportJson())!; json["Conflicts"] = null;
        try { WorldEngine.ImportJson(json.ToJsonString()); throw new Exception("Null conflicts accepted"); } catch (ArgumentException) { }
    }

    [UnitTest]
    private static void ExploreRoute()
    {
        var engine = Empty(); engine.SpawnResidents(16, 16, RaceKind.Human, 1);
        TestLand.ClaimAllTowns(engine);
        var person = engine.State.Residents.Single(); var home = engine.State.Settlements.Single();
        person.Age = 25; person.Profession = Profession.Messenger; person.Inventory.Food = 6;
        person.X = person.FromX = home.X; person.Y = person.FromY = home.Y;
        engine.Step();
        var goal = person.Agent.Goal;
        var started = goal.StartedTick; var destination = (goal.TargetX, goal.TargetY);
        Require(goal.Kind == AgentGoalKind.Explore && (person.X != home.X || person.Y != home.Y), "Messenger did not begin physical exploration");
        Require(engine.GetResidentActionSummary(person.Id).Contains("发现聚落后记下实际位置"), "Route exploration was described as gathering materials");
        engine.Step(4);
        Require(person.Agent.Goal.StartedTick == started && (person.Agent.Goal.TargetX, person.Agent.Goal.TargetY) == destination,
            "Moving explorer repeatedly discarded an unfinished destination");
        var restored = WorldEngine.ImportJson(engine.ExportJson()); engine.Step(24); restored.Step(24);
        Require(engine.ExportJson() == restored.ExportJson() && person.Agent.Goal.Kind != AgentGoalKind.Idle, "Exploration did not continue deterministically after resume");
    }

    [UnitTest]
    private static void LoneResident()
    {
        var engine = Empty(); engine.SpawnResidents(16, 16, RaceKind.Human, 1);
        TestLand.ClaimAllTowns(engine);
        var town = engine.State.Settlements.Single(); var person = engine.State.Residents.Single();
        engine.GrantFacility(town.Id, BuildingKind.Farm, 17, 16);
        person.Age = 25; person.Profession = Profession.Farmer; person.Inventory.Food = 2;
        person.X = person.FromX = town.X; person.Y = person.FromY = town.Y;
        person.Agent.SocialNeed = 100; person.Agent.Personality.Sociability = 1;
        engine.Step(); Require(person.Agent.Goal.Kind == AgentGoalKind.Work, "A lone worker chose conversation with nobody over available work");
        var farm = engine.State.Society.Buildings.Single(b => b.Id == person.Agent.Goal.TargetEntityId);
        Require(farm.Kind == BuildingKind.Farm, "Lone farmer did not select an actual farm");
        engine.Step(5); Require(farm.LastWorkedTick > 0, "Lone resident did not reach productive work");
    }

    [UnitTest]
    private static void Inspection()
    {
        var engine = Empty(); engine.SpawnResidents(16, 16, RaceKind.Human, 1);
        TestLand.ClaimAllTowns(engine);
        var town = engine.State.Settlements.Single(); var person = engine.State.Residents.Single(); person.Age = 25;
        foreach (var knowledge in new[] { ResearchKind.Agriculture, ResearchKind.Logistics, ResearchKind.Industry }) engine.GrantReceivedResearch(town.Id, knowledge);
        foreach (var kind in AdvancementRules.Resources) town.Resources.Set(kind, 100);
        town.Resources.Alloy = 0; // The assigned foundry has an actual unmet output demand.
        var factory = engine.GrantFacility(town.Id, BuildingKind.Foundry, 20, 16);
        person.X = person.FromX = 18; person.Agent.Goal = new AgentGoal { Kind = AgentGoalKind.Work, TargetEntityId = factory, TargetX = 20, TargetY = 16 };
        var before = engine.ExportJson(); Require(engine.GetResidentActionSummary(person.Id).Contains("仓库取料"), "Factory work described the wrong current destination");
        Require(engine.ExportJson() == before, "Action inspection changed the world");
        var recipe = AdvancementRules.For(BuildingKind.Foundry)!;
        foreach (var kind in AdvancementRules.Resources) person.Inventory.Set(kind, recipe.Input.Get(kind));
        Require(engine.GetResidentActionSummary(person.Id).Contains("携带原料前往"), "Loaded worker was still described as fetching inputs");
        engine.SetBuildingEnabled(factory, false);
        Require(engine.GetResidentActionSummary(person.Id).Contains("已停用"), "Stopped production was described as ongoing labor");
    }

    [UnitTest]
    private static void Mortality()
    {
        foreach (var cause in new[] { DeathCause.Fire, DeathCause.Disease, DeathCause.Starvation, DeathCause.OldAge, DeathCause.Meteor, DeathCause.PlayerIntervention })
        {
            var engine = Empty(); engine.SpawnResidents(16, 16, RaceKind.Human, 1); var person = engine.State.Residents[0];
        TestLand.ClaimAllTowns(engine);
            person.X = person.FromX = 16; person.Y = person.FromY = 16; person.Age = 25; person.Health = .1;
            switch (cause)
            {
                case DeathCause.Fire: engine.State.Tiles[16 * 32 + 16].FireTicks = 10; break;
                case DeathCause.Disease: engine.State.Rules.Disease = true; person.SicknessTicks = 5; break;
                case DeathCause.Starvation: engine.State.Rules.Hunger = true; person.Hunger = 90; break;
                case DeathCause.OldAge: engine.State.Rules.Aging = true; person.Age = 95; break;
                case DeathCause.Meteor: engine.TriggerDisaster(16, 16, DisasterKind.Meteor, 2); break;
                case DeathCause.PlayerIntervention: engine.EditResident(person.Id, new ResidentEdit { Health = 0 }); break;
            }
            engine.Step(); var deceased = engine.State.ArchivedResidents.Single(r => r.Id == person.Id);
            Require(deceased.DeathCause == cause && deceased.History.Any(h => h.Text.Contains(WorldEngine.DeathCauseName(cause))), "Missing precise fatal cause: " + cause);
            Require(WorldEngine.ImportJson(engine.ExportJson()).State.ArchivedResidents.Single(r => r.Id == person.Id).DeathCause == cause, "Death cause was lost on import");
        }
    }

    private static void Require(bool condition, string message) { if (!condition) throw new Exception(message); }
}
