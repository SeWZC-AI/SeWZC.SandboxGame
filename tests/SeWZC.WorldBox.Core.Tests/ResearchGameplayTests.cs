using System.Text.Json.Nodes;
using SeWZC.WorldBox.Core;

internal static class ResearchGameplayTests
{
    public static IEnumerable<(string Name, Action Run)> Cases =>
    [
        ("civilization outcomes require actual research, operational facilities and real batches", Civilization),
        ("hospital consumes carried medicine and grants local recovery immunity", Hospital),
        ("library and survey office teach observed facts without omniscient knowledge", Knowledge),
        ("ranged combat requires delivered orders, ammo, sight and cooldown", Ranged),
        ("advanced magic gates freezing, chain targets and consumes personal protection", Magic),
        ("waygates move actual cargo and reject invalid destinations without charging", Gates),
        ("rails and repairs consume physical materials and respect local ownership", Infrastructure),
        ("new professions, consumables and combat state survive validated continuation", Persistence)
        ,("water stations share natural quotas and groves restore real vegetation", EcologyServices),
        ("specialists keep staffed services and trained veterans retain their roles", Specialists),
        ("alternative factories can commission one real batch despite existing stocks", Commissioning)
    ];

    private static (WorldEngine E, Settlement Town, Resident Person) World()
    {
        var e = WorldEngine.Create(451, 32, 32, false);
        foreach (var t in e.State.Tiles) { t.Terrain = TerrainType.Grass; t.Fertility = 100; t.NaturalWaterYield = .08; t.Plants = new() { Grass = .3 }; }
        e.SpawnResidents(8, 8, RaceKind.Human, 12); TestLand.ClaimAllTowns(e);
        e.ConfigureWorld(new WorldRules { Aging = false, Births = false, Hunger = false, Thirst = false, Disease = false, Construction = false,
            Research = false, Expansion = false, Trade = false, Wars = false, Alliances = false, Migration = false, Secession = false }, false, true);
        var town = e.State.Settlements.Single();
        foreach (var k in AdvancementRules.Resources) town.Resources.Set(k, 200);
        foreach (var r in ResearchRules.All) e.GrantReceivedResearch(town.Id, r.Kind);
        e.State.Tick = 100;
        var person = e.State.Residents.First(); person.Age = 30; person.MagicTalent = 80; person.MagicTraining = 30; person.Mana = 100;
        return (e, town, person);
    }

    private static Building Facility(WorldEngine e, Settlement town, BuildingKind kind)
    {
        var site = Enumerable.Range(0, e.State.Tiles.Length).Where(i => Math.Abs(i % 32 - town.X) + Math.Abs(i / 32 - town.Y) <= 7
            && e.FacilityPlacementError(town.Id, kind, i % 32, i / 32, gift: true) is null).First();
        var id = e.GrantFacility(town.Id, kind, site % 32, site / 32);
        return e.State.Society.Buildings.Single(b => b.Id == id);
    }
    private static void At(Resident p, int x, int y)
    { p.X = p.FromX = x; p.Y = p.FromY = y; p.MoveStartedTick = 0; p.MoveDurationTicks = 1; }
    private static void Work(Resident p, Building b)
    { At(p, b.X, b.Y); p.Agent.Goal = new() { Kind = AgentGoalKind.Work, TargetEntityId = b.Id, TargetX = b.X, TargetY = b.Y }; }
    private static void Check(bool valid, string message) { if (!valid) throw new Exception(message); }
    private static void Reject(Action action)
    { try { action(); } catch (InvalidOperationException) { return; } throw new Exception("Invalid action was accepted."); }

    [UnitTest]
    private static void Commissioning()
    {
        var (e, town, p) = World(); var lab = Facility(e, town, BuildingKind.AlchemyLab);
        p.Profession = Profession.Mage; p.Inventory = new(); At(p, lab.X, lab.Y);
        Check(town.Resources.Medicine >= 80 && e.TryGetLocalWorkTarget(p, out var x, out var y) && (x, y) == (lab.X, lab.Y),
            "Existing pharmacy stocks prevented commissioning the unlocked alchemy recipe");
        lab.ProductionBatches = 1;
        Check(!e.TryGetLocalWorkTarget(p, out x, out y) || (x, y) != (lab.X, lab.Y), "Commissioning bypassed the ongoing stock limit");
        lab.ProductionBatches = 0; town.Resources.Crystals = 0;
        Check(!e.TryGetLocalWorkTarget(p, out x, out y) || (x, y) != (lab.X, lab.Y), "Commissioning bypassed physical input requirements");
    }

    [UnitTest]
    private static void Specialists()
    {
        var (e, town, person) = World(); var library = Facility(e, town, BuildingKind.Library);
        var construction = Facility(e, town, BuildingKind.Farm); construction.ConstructionProgress = 0;
        person.Profession = Profession.Archivist; At(person, library.X, library.Y);
        Check(e.TryGetLocalWorkTarget(person, out var x, out var y) && (x, y) == (library.X, library.Y), "Active specialist service was abandoned for construction");
        person.Agent.Goal = new() { Kind = AgentGoalKind.Work, TargetEntityId = construction.Id, TargetX = construction.X, TargetY = construction.Y };
        var kept = (bool)typeof(WorldEngine).GetMethod("ProductiveGoalContinues", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.Invoke(e, [person, town])!;
        Check(!kept, "An old construction goal prevented a specialist from returning to an active service");
        var army = new Army { Id = e.State.NextId++, NationId = town.NationId, X = town.X, Y = town.Y };
        e.State.Armies.Add(army);
        var mage = e.State.Residents[1]; person.Profession = Profession.Ranger; mage.Profession = Profession.Battlemage;
        person.ArmyId = mage.ArmyId = army.Id;
        typeof(WorldEngine).GetMethod("DisbandArmy", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.Invoke(e, [army]);
        Check(person.ArmyId == 0 && mage.ArmyId == 0 && person.Profession == Profession.Ranger && mage.Profession == Profession.Battlemage,
            "Demobilization erased trained combat professions");
    }

    [UnitTest]
    private static void Civilization()
    {
        var (e, town, person) = World();
        Check(ResearchRules.All.Count == 40 && ResearchRules.Route(false).Count == 26 && ResearchRules.Route(true).Count == 25, "Actual research routes are incomplete");
        Check(!Enum.IsDefined((ResearchKind)18) && !Enum.IsDefined((ResearchKind)23), "Fake empire knowledge remains valid");
        Check(!e.GetCivilizationProgress(town.Id, false).Achieved, "Knowledge alone magically created an empire");
        var facilities = ResearchRules.Route(false).SelectMany(k => ResearchRules.For(k).UnlockedBuildings).Distinct().Select(k => Facility(e, town, k)).ToArray();
        Check(e.GetCivilizationProgress(town.Id, false).UnprovenProduction.Count > 0 && !e.GetCivilizationProgress(town.Id, false).Achieved, "Unworked factories qualified");
        foreach (var b in facilities)
        {
            var recipe = AdvancementRules.For(b.Kind); if (recipe is null) continue;
            person.Inventory = recipe.Input.Copy(); person.Mana = 100; Work(person, b);
            Check(e.TryWorkAtBuilding(person) && b.ProductionBatches == 1, "Factory did not physically produce: " + b.Kind);
        }
        Check(e.GetCivilizationProgress(town.Id, false).Achieved && e.GetAdvancementStage(town.Id).Contains("科技帝国"), "Actual completed civilization did not qualify");
        facilities[0].Enabled = false; Check(!e.GetCivilizationProgress(town.Id, false).Achieved, "Disabled infrastructure still qualified");
        facilities[0].Enabled = true; e.State.Society.Research.Single().Completed.Remove(ResearchKind.Medicine);
        Check(!e.GetCivilizationProgress(town.Id, false).Achieved, "Incomplete route still qualified");
    }

    [UnitTest]
    private static void Hospital()
    {
        var (e, town, person) = World(); var b = Facility(e, town, BuildingKind.Hospital);
        person.Profession = Profession.Physician; person.Inventory.Medicine = 1; Work(person, b);
        var patient = e.State.Residents[1]; At(patient, b.X, b.Y); patient.Health = 40; patient.SicknessTicks = 20;
        var warehouse = town.Resources.Medicine;
        Check(e.TryWorkAtBuilding(person), "Doctor could not treat an actual patient");
        Check(person.Inventory.Medicine == .75 && town.Resources.Medicine == warehouse && patient.Health > 40
            && patient.SicknessTicks == 16 && patient.DiseaseImmuneUntilTick == 220 && b.ServiceActions == 1, "Treatment bypassed physical medicine or actual patient");
        e.State.Tick++; e.State.Society.Research.Single().Completed.Remove(ResearchKind.Sanitation);
        Check(!e.TryWorkAtBuilding(person), "Gifted hospital operated without local knowledge");
    }

    [UnitTest]
    private static void Knowledge()
    {
        var (e, town, person) = World(); var library = Facility(e, town, BuildingKind.Library);
        person.Profession = Profession.Archivist; Work(person, library);
        var pupil = e.State.Residents[1]; pupil.Agent.Memory.Clear(); At(pupil, library.X, library.Y);
        var before = e.State.Society.Research.Single().Completed.ToArray();
        Check(e.TryWorkAtBuilding(person) && pupil.Agent.Memory.Any(f => f.Kind == AgentFactKind.Research), "Library did not teach actual local knowledge");
        Check(before.SequenceEqual(e.State.Society.Research.Single().Completed), "Reading invented a completed project");
        var office = Facility(e, town, BuildingKind.SurveyOffice); person.Profession = Profession.Surveyor; Work(person, office); person.Agent.Memory.Clear();
        Check(e.TryWorkAtBuilding(person) && person.Agent.Memory.Any(f => f.Kind == AgentFactKind.WaterSource && f.ObservedTick == e.State.Tick), "Survey generated no located, dated observation");
        Check(person.Agent.Memory.Where(f => f.Kind == AgentFactKind.WaterSource).All(f => Math.Abs(f.X - office.X) + Math.Abs(f.Y - office.Y) <= 6), "Survey observed beyond its real range");
    }

    private static Resident Enemy(WorldEngine e, Resident person)
    {
        e.SpawnResidents(24, 24, RaceKind.Orc, 4); var enemy = e.State.Residents.First(r => r.NationId != person.NationId);
        At(person, 8, 8); At(enemy, 11, 8);
        return enemy;
    }
    private static void Order(WorldEngine e, Resident person, Resident enemy) => person.Agent.Memory.Add(new AgentFact
    { Kind = AgentFactKind.WarOrder, SubjectId = enemy.NationId, X = enemy.X, Y = enemy.Y, ObservedTick = e.State.Tick, LearnedTick = e.State.Tick, Text = "本国送达的军令" });

    [UnitTest]
    private static void Ranged()
    {
        var (e, _, p) = World(); var enemy = Enemy(e, p); p.Profession = Profession.Ranger; p.Inventory.Ammunition = 2;
        Reject(() => e.RangedAttack(p.Id, enemy.Id)); Check(p.Inventory.Ammunition == 2, "Unknown hostility consumed ammo");
        Order(e, p, enemy); e.State.Tiles[8 * 32 + 10].Terrain = TerrainType.Mountain;
        Reject(() => e.RangedAttack(p.Id, enemy.Id)); Check(enemy.Health == 100, "A shot passed through mountains");
        e.State.Tiles[8 * 32 + 10].Terrain = TerrainType.Grass; e.RangedAttack(p.Id, enemy.Id);
        Check(enemy.Health < 100 && p.Inventory.Ammunition == 1, "Successful shooting did not consume ammo");
        Reject(() => e.RangedAttack(p.Id, enemy.Id)); Check(p.Inventory.Ammunition == 1, "Cooldown charged twice");
    }

    [UnitTest]
    private static void Magic()
    {
        var (e, town, p) = World(); var enemy = Enemy(e, p); Order(e, p, enemy);
        var research = e.State.Society.Research.First(r => r.SettlementId == town.Id); research.Completed.Remove(ResearchKind.Elementalism);
        Check(!e.TryCastSpell(p.Id, SpellKind.FrostBolt, enemy.X, enemy.Y) && p.Mana == 100, "Locked spell spent mana");
        e.GrantReceivedResearch(town.Id, ResearchKind.Elementalism); e.CastSpell(p.Id, SpellKind.FrostBolt, enemy.X, enemy.Y);
        Check(enemy.FrozenUntilTick == e.State.Tick + 6 && p.Mana == 76, "Frost did not freeze or charge");
        enemy.Agent.Goal = new() { Kind = AgentGoalKind.Explore, TargetX = 13, TargetY = 8, PlayerDirected = true, ReviewTick = 150 };
        var location = (enemy.X, enemy.Y); e.Step(2); Check((enemy.X, enemy.Y) == location, "Frozen enemy moved");
        e.CastSpell(p.Id, SpellKind.RuneWard, p.X, p.Y); var ward = p.PersonalWard;
        Check(ward > 0 && e.TryAbsorbShieldDamage(p, 10) == 0 && p.PersonalWard == ward - 10, "Personal shield did not consume protection");
        p.PersonalWard = 0; p.Armor = 30; var remaining = e.TryAbsorbShieldDamage(p, 20);
        Check(remaining < 20 && p.Armor < 30, "Physical armor did not wear");
        var enemies = e.State.Residents.Where(r => r.NationId == enemy.NationId).OrderBy(r => r.Id).ToArray();
        foreach (var r in enemies) { At(r, 10, 8); r.Health = 100; r.PersonalWard = r.Armor = 0; }
        p.Mana = 100; e.CastSpell(p.Id, SpellKind.ChainLightning, 10, 8);
        Check(enemies.Count(r => r.Health < 100) == 3 && p.Mana == 64, "Chain lightning struck too many targets or charged incorrectly");
        e.TriggerDisaster(8, 8, DisasterKind.Drought, 1); var drought = e.State.Tiles[8 * 32 + 8].DroughtTicks;
        e.CastSpell(p.Id, SpellKind.RainCall, 8, 8);
        Check(e.State.Tiles[8 * 32 + 8].DroughtTicks == Math.Max(0, drought - 60) && p.Mana == 36, "Rain was cosmetic or free");
    }

    [UnitTest]
    private static void Gates()
    {
        var (e, town, p) = World(); var source = Facility(e, town, BuildingKind.Waygate); var target = Facility(e, town, BuildingKind.Waygate);
        At(p, source.X, source.Y); p.Inventory.Crystals = 5; p.Inventory.Ore = 7; p.Inventory.Water = 3;
        var cargo = p.Inventory.Copy(); e.TravelByWaygate(p.Id, target.Id);
        Check((p.X, p.Y) == (target.X, target.Y) && p.Mana == 70 && p.Inventory.Crystals == cargo.Crystals - 2
            && p.Inventory.Ore == 7 && p.Inventory.Water == 3 && source.ServiceActions == 1 && target.ServiceActions == 1, "Teleport invented, lost or remote-delivered cargo");
        var json = e.ExportJson(); target.Enabled = false; var blocked = e.ExportJson();
        Reject(() => e.TravelByWaygate(p.Id, source.Id)); Check(e.ExportJson() == blocked, "Rejected gate travel mutated the world");
        target.Enabled = true; Check(e.ExportJson() == json, "Checking gates changed physical state");
    }

    [UnitTest]
    private static void Infrastructure()
    {
        var (e, town, p) = World(); var tile = e.State.Tiles[8 * 32 + 8]; tile.RoadLevel = 1;
        var alloy = town.Resources.Alloy; var stone = town.Resources.Stone; var oldCost = e.GetTerrainMoveCost(8, 8);
        e.BuildRail(town.Id, 8, 8, 0);
        Check(tile.RoadLevel == 2 && town.Resources.Alloy == alloy - .5 && town.Resources.Stone == stone - 1
            && e.GetTerrainMoveCost(8, 8) < oldCost, "Rail was cosmetic or material-free");
        var b = Facility(e, town, BuildingKind.Hospital); b.Health = 40; p.Profession = Profession.Engineer; p.Inventory.Stone = 1; Work(p, b);
        e.RepairBuilding(p.Id, b.Id); Check(b.Health == 50 && p.Inventory.Stone == .5, "Repair did not consume actual carried stone");
    }

    [UnitTest]
    private static void Persistence()
    {
        var (e, town, p) = World(); e.AssignResearchProfession(p.Id, Profession.Engineer);
        p.Armor = 17; p.PersonalWard = 24; p.FrozenUntilTick = e.State.Tick + 6; p.Inventory.Tools = 3; p.Inventory.Medicine = 4; p.Inventory.Ammunition = 5;
        var json = e.ExportJson(); var resumed = WorldEngine.ImportJson(json);
        Check(resumed.ExportJson() == json, "Current-format research gameplay did not round trip");
        var bad = JsonNode.Parse(json)!; bad["Residents"]![0]!["FrozenUntilTick"] = 10000;
        try { WorldEngine.ImportJson(bad.ToJsonString()); throw new Exception("Invalid combat state accepted"); } catch (ArgumentException) { }
        e.Step(12); resumed.Step(12); Check(e.ExportJson() == resumed.ExportJson(), "Research gameplay resumed nondeterministically");
    }

    [UnitTest]
    private static void EcologyServices()
    {
        var (e, town, p) = World(); var station = Facility(e, town, BuildingKind.Reservoir);
        p.Inventory.Water = 0; Work(p, station); var stock = town.Resources.Water;
        Check(e.TryWorkAtBuilding(p) && Math.Abs(p.Inventory.Water - .08) < 1e-9 && town.Resources.Water == stock, "Station invented warehouse water");
        var other = e.State.Residents[1]; other.Inventory.Water = 0; Work(other, station);
        Check(!e.TryWorkAtBuilding(other) && other.Inventory.Water == 0, "Two station workers duplicated one natural quota");
        var grove = Facility(e, town, BuildingKind.GroveSanctuary); p.Profession = Profession.Gardener; p.Inventory.Water = 2; p.Mana = 100; Work(p, grove);
        var trees = e.State.Tiles.Sum(t => t.Plants.Trees);
        Check(e.TryWorkAtBuilding(p) && e.State.Tiles.Sum(t => t.Plants.Trees) > trees && p.Inventory.Water == 1.75 && p.Mana == 98,
            "Grove changed no vegetation or ignored physical inputs");
        Check(e.State.Tiles.All(t => t.Plants.Total <= 1.000001), "Plant coverage exceeded real tile capacity");
    }
}
