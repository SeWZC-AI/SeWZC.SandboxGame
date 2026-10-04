using System.Reflection;
using SeWZC.WorldBox.Core;

internal static class SocietyBehaviorTests
{
    public static IEnumerable<(string Name, Action Run)> Cases() =>
    [
        ("construction and local research require materials and nearby work", ConstructionResearch),
        ("delivered civic reports preserve origins, deduplicate and change institutional decisions", InstitutionalWeights),
        ("culture changes independently of race and nationality through physical contact", CulturalExchange),
        ("magic uses trained talent and mana for actual local effects", MagicEffects),
        ("roads and operated connected signal towers affect physical communication", TransportNetwork),
        ("default societies build and research autonomously with deterministic saves", AutonomousDevelopment),
        ("large default world develops beyond founding facilities", LargeDefaultDevelopment),
        ("nearby work follows its selected facility and requires productive resources", SelectedFacilityWork),
        ("housing leaves materials for development and optional shortages do not block ready projects", DevelopmentBudget)
    ];

    private static WorldEngine FlatWorld(int population = 24)
    {
        var engine = WorldEngine.Create(1234, 64, 64, false);
        engine.State.NaturalDisasters = false;
        foreach (var tile in engine.State.Tiles) { tile.Terrain = TerrainType.Grass; tile.Fertility = 90; tile.ResourceAmount = 100; }
        engine.SpawnResidents(16, 32, RaceKind.Human, population);
        TestLand.ClaimAllTowns(engine);
        return engine;
    }

    private static void Check(bool valid, string message)
    {
        if (!valid) throw new InvalidOperationException(message);
    }

    private static void Place(Resident resident, int x, int y)
    {
        resident.X = resident.FromX = x; resident.Y = resident.FromY = y;
    }

    private static void Work(WorldEngine engine, Resident worker, Building facility)
    {
        Place(worker, facility.X, facility.Y);
        worker.Profession = Profession.Builder;
        worker.Agent.Goal = new AgentGoal { Kind = AgentGoalKind.Work, TargetX = facility.X, TargetY = facility.Y,
            StartedTick = engine.State.Tick, ReviewTick = engine.State.Tick + 100, PlayerDirected = true };
        while (!facility.IsCompleted)
        {
            engine.State.Tick++;
            Check(engine.TryWorkAtBuilding(worker), "A physically present adult could not construct the funded facility.");
        }
    }

    private static void ConstructionResearch()
    {
        var engine = FlatWorld(); var town = engine.State.Settlements.Single();
        engine.SetNationResources(town.NationId, 500, 500, 500, 100);
        var before = town.Resources.Wood;
        var id = engine.BuildFacility(town.Id, BuildingKind.Academy, 20, 32);
        var facility = engine.State.Society.Buildings.Single(b => b.Id == id);
        Check(town.Resources.Wood == before - WorldEngine.GetBuildingCost(BuildingKind.Academy).Wood, "Construction did not spend local materials.");
        Check(!facility.IsCompleted, "A new facility completed without any construction work.");
        var worker = engine.State.Residents.Last();
        Place(worker, 40, 32);
        Check(!engine.TryWorkAtBuilding(worker) && facility.ConstructionProgress == 0, "An absent worker remotely constructed the academy.");
        Work(engine, worker, facility);
        engine.StartResearch(town.Id, ResearchKind.Agriculture);
        var project = engine.State.Society.Research.Single();
        var initial = project.Progress;
        Place(worker, 40, 32); engine.State.Tick++;
        Check(!engine.TryWorkAtBuilding(worker) && project.Progress == initial, "Research progressed without a present researcher.");
        Place(worker, facility.X, facility.Y);
        worker.Agent.Goal.Kind = AgentGoalKind.Study;
        for (var i = 0; i < 15; i++) { engine.State.Tick++; Check(engine.TryWorkAtBuilding(worker), "Researcher did not advance the project."); }
        Check(project.Progress > 0 && !project.Completed.Contains(ResearchKind.Agriculture), "Research never accumulated intermediate progress.");
        var resumed = WorldEngine.ImportJson(engine.ExportJson());
        Check(resumed.ExportJson() == engine.ExportJson(), "Saving changed construction or in-progress research state.");
        for (var i = 0; i < 150 && !engine.HasResearch(town.Id, ResearchKind.Agriculture); i++)
        { engine.State.Tick++; engine.TryWorkAtBuilding(worker); }
        Check(engine.HasResearch(town.Id, ResearchKind.Agriculture), "Completed physical research did not unlock local knowledge.");
        Check(town.PublicKnowledge.Any(f => f.Kind == AgentFactKind.Research && f.Value == (int)ResearchKind.Agriculture), "Research produced no transmissible knowledge.");
        Check(worker.Agent.Memory.Any(f => f.Kind == AgentFactKind.Research), "The actual researcher did not learn their result.");
    }

    private static void InstitutionalWeights()
    {
        var engine = FlatWorld(); var town = engine.State.Settlements.Single(); var nation = engine.State.Nations.Single();
        var carrier = engine.State.Residents.Last(); carrier.Profession = Profession.Messenger; Place(carrier, town.X, town.Y);
        var receiver = typeof(WorldEngine).GetMethod("ReceiveSocietyReport", BindingFlags.NonPublic | BindingFlags.Instance)!;
        AgentFact Fact(AgentFactKind topic, Profession originProfession, double severity, int originId) => new()
        {
            Id = engine.State.NextId++, Kind = topic, SubjectId = town.Id, X = town.X, Y = town.Y, Value = severity,
            OriginResidentId = originId, OriginProfession = originProfession, SourceResidentId = carrier.Id,
            ObservedTick = engine.State.Tick, LearnedTick = engine.State.Tick, Text = "A delivered civic report"
        };
        var food = Fact(AgentFactKind.ReliefRequest, Profession.Farmer, 80, engine.State.Residents[1].Id);
        var danger = Fact(AgentFactKind.Danger, Profession.Soldier, 60, engine.State.Residents[2].Id);
        receiver.Invoke(engine, [town, carrier, food]); receiver.Invoke(engine, [town, carrier, food]);
        receiver.Invoke(engine, [town, carrier, danger]);
        Check(engine.State.Society.Reports.Count == 2, "Forwarding the same evidence counted as an additional civic voice.");
        Check(engine.State.Society.Reports.Single(r => r.FactId == food.Id).ReportedProfession == Profession.Farmer, "A messenger overwrote the original citizen's occupational perspective.");
        engine.SetInstitution(nation.Id, InstitutionKind.Council); engine.State.Tick = 30; engine.TickSociety();
        Check(engine.GetLocalPolicy(town.Id) == PolicyKind.FoodSecurity, "Equal civic authority did not prioritize the stronger weighted food petition.");
        engine.SetInstitution(nation.Id, InstitutionKind.Monarchy); engine.State.Tick = 60; engine.TickSociety();
        Check(engine.GetLocalPolicy(town.Id) == PolicyKind.Defense, "Changing institutional military authority had no effect on the same received evidence.");
        engine.SetPolicy(nation.Id, PolicyKind.Scholarship); engine.State.Tick = 90; engine.TickSociety();
        Check(engine.GetLocalPolicy(town.Id) == PolicyKind.Scholarship, "Autonomous deliberation overwrote a player policy override.");
        engine.SetPolicyAutonomy(nation.Id); engine.State.Tick = 120; engine.TickSociety();
        Check(engine.GetLocalPolicy(town.Id) == PolicyKind.Defense, "Restoring autonomy failed to resume deliberation.");
        Check(engine.State.Society.Policies.Single().EvidenceFactId == danger.Id, "Policy explanation cited evidence from a different winning issue.");
    }

    [UnitTest]
    private static void CulturalExchange()
    {
        var engine = FlatWorld(); var first = engine.State.Residents[1]; var second = engine.State.Residents[2];
        var race = first.Race; var nationality = first.NationId;
        engine.SetResidentCulture(first.Id, 2); engine.SetResidentCulture(second.Id, 3);
        first.Agent.Personality.Sociability = 1; second.Agent.Personality.Sociability = 0;
        Place(first, 16, 32); Place(second, 40, 32);
        for (var i = 0; i < 12; i++) { engine.State.Tick += 12; engine.ExchangeCulture(first, second); }
        Check(first.CultureId == 2, "Culture changed through an impossible distant conversation.");
        Place(second, 17, 32);
        for (var i = 0; i < 12 && first.CultureId != 3; i++) { engine.State.Tick += 12; engine.ExchangeCulture(first, second); }
        Check(first.CultureId == 3, "Repeated in-person exposure did not affect cultural identity.");
        Check(first.Race == race && first.NationId == nationality, "Cultural exchange changed biological race or political nationality.");
        engine.SetNationCulture(nationality, 4);
        Check(first.CultureId == 3, "Editing national culture forcibly rewrote a resident's identity.");
        engine.SetCultureValues(3, 0.8, 0.9, 0.7);
        Check(engine.State.Society.Cultures.Single(c => c.Id == 3).Innovation == 0.9, "Culture values were not editable.");
        _ = WorldEngine.ImportJson(engine.ExportJson());
    }

    private static void MagicEffects()
    {
        var engine = FlatWorld(); var town = engine.State.Settlements.Single();
        var caster = engine.State.Residents[1]; var patient = engine.State.Residents[2];
        Place(caster, town.X, town.Y); Place(patient, town.X, town.Y);
        caster.MagicTalent = 60; caster.MagicTraining = 20; caster.Mana = 100; caster.Profession = Profession.Mage;
        patient.Health = 30; patient.SicknessTicks = 20;
        engine.CastSpell(caster.Id, SpellKind.Heal, patient.X, patient.Y);
        Check(patient.Health > 30 && patient.SicknessTicks < 20 && caster.Mana < 100, "Healing did not spend mana for an actual health effect.");
        var mana = caster.Mana;
        Check(!engine.TryCastSpell(caster.Id, SpellKind.Heal, 50, 50) && caster.Mana == mana, "An out-of-range spell succeeded or charged mana.");
        engine.CastSpell(caster.Id, SpellKind.HarvestBlessing, town.X, town.Y);
        engine.CastSpell(caster.Id, SpellKind.Shield, town.X, town.Y);
        Check(town.FertilityBoostTicks > 0 && town.ShieldTicks > 0, "Magic did not establish real production and defensive effects.");
        Check(engine.TryAbsorbShieldDamage(patient, 10) < 10, "A local shield failed to reduce incoming damage.");
        engine.State.Society.MagicEnabled = false; caster.Mana = 100;
        Check(engine.TryCastSpell(caster.Id, SpellKind.Heal, patient.X, patient.Y), "Closing new magic development destroyed an already trained ability.");
        foreach (var resident in engine.State.Residents) resident.Health = 100;
        town.FertilityBoostTicks = 0; town.ShieldTicks = 0; caster.Mana = 100;
        caster.Agent.Goal = new AgentGoal { TargetX = caster.X, TargetY = caster.Y };
        engine.SetPolicy(town.NationId, PolicyKind.FoodSecurity);
        engine.State.Tick += 12 - (engine.State.Tick + caster.Id) % 12;
        engine.TickSociety();
        Check(town.FertilityBoostTicks > 0 && caster.Mana < 100, "A trained mage never autonomously answered a known local food policy.");
        Check(caster.Agent.Decisions.Any(d => d.Reason.Contains("丰饶")), "Automatic magic lacked an inspectable actual decision.");
        _ = WorldEngine.ImportJson(engine.ExportJson());
    }

    private static void TransportNetwork()
    {
        var engine = FlatWorld(); var from = engine.State.Settlements.Single();
        engine.SpawnResidents(52, 32, RaceKind.Elf, 16);
        var to = engine.State.Settlements.Last(); engine.TransferTerritory(to.X, to.Y, from.NationId, 0);
        engine.SetNationResources(from.NationId, 2000, 2000, 2000, 1000, 1000, 1000);
        engine.GrantReceivedResearch(from.Id, ResearchKind.Electrification);
        engine.GrantReceivedResearch(to.Id, ResearchKind.Electrification);
        var originalCost = engine.GetTerrainMoveCost(17, 33); var stone = from.Resources.Stone;
        engine.BuildRoad(from.Id, 17, 33, 0);
        Check(engine.GetTerrainMoveCost(17, 33) < originalCost && from.Resources.Stone < stone, "Roads did not consume materials and improve physical travel.");
        engine.GrantReceivedResearch(from.Id, ResearchKind.SignalNetwork);
        engine.GrantReceivedResearch(to.Id, ResearchKind.SignalNetwork);
        Check(!engine.CanRelayInformation(from.Id, to.Id, out _), "Technology alone enabled a global information broadcast.");
        TestLand.ClaimAllTowns(engine, 9);
        var firstTowerId = engine.BuildFacility(from.Id, BuildingKind.SignalTower, 24, 32);
        var secondTowerId = engine.BuildFacility(to.Id, BuildingKind.SignalTower, 44, 32);
        var a = engine.State.Society.Buildings.Single(building => building.Id == firstTowerId);
        var b = engine.State.Society.Buildings.Single(building => building.Id == secondTowerId);
        var workerA = engine.State.Residents.First(r => r.SettlementId == from.Id && r.Id != from.RepresentativeId);
        var workerB = engine.State.Residents.First(r => r.SettlementId == to.Id && r.Id != to.RepresentativeId);
        Work(engine, workerA, a); Work(engine, workerB, b);
        engine.State.Tick++; engine.TryWorkAtBuilding(workerA); engine.TryWorkAtBuilding(workerB);
        Check(engine.CanRelayInformation(from.Id, to.Id, out var ticks) && ticks > 0, "A staffed connected tower network did not provide bounded-delay communication.");
        Place(workerB, to.X, to.Y);
        Check(!engine.CanRelayInformation(from.Id, to.Id, out _), "An abandoned relay continued operating without a present worker.");
        Place(workerB, b.X, b.Y); engine.State.Tick++; engine.TryWorkAtBuilding(workerB); engine.TryWorkAtBuilding(workerA);
        engine.PaintTerrain(34, 32, TerrainType.Mountain, 0);
        Check(!engine.CanRelayInformation(from.Id, to.Id, out _), "A mountain severing relay visibility left the network connected.");
    }

    private static void AutonomousDevelopment()
    {
        var engine = WorldEngine.Create(42, 64, 64); engine.State.NaturalDisasters = false;
        var foundingBuildings = engine.State.Society.Buildings.Count;
        engine.Step(180);
        var resumed = WorldEngine.ImportJson(engine.ExportJson());
        engine.Step(180); resumed.Step(180);
        Check(engine.ExportJson() == resumed.ExportJson(), "Society, research or communication diverged after mid-development saving.");
        Check(engine.State.Society.Buildings.Count > foundingBuildings && engine.State.Society.Buildings.Any(b => b.Kind == BuildingKind.Academy && b.IsCompleted),
            "Default inhabitants never autonomously funded and physically constructed a school.");
        Check(engine.State.Society.Research.Any(r => r.Completed.Count > 0), "Default institutions required player research clicks to discover anything.");
    }

    [LongRunningTest]
    private static void LargeDefaultDevelopment()
    {
        var engine = WorldEngine.Create(73921, 256, 256);
        engine.State.NaturalDisasters = false;
        engine.Step(1200);
        Check(engine.State.Society.Buildings.Any(b => b.Kind == BuildingKind.Academy && b.IsCompleted),
            "The application's default world still has no completed school after ten simulated years.");
        Check(engine.State.Society.Research.Any(r => r.Completed.Contains(ResearchKind.Agriculture)),
            "The application's default world never completes its first local research project.");
        Check(engine.State.Society.Research.Any(r => r.Completed.Contains(ResearchKind.Logistics)),
            "An unaffordable optional project blocked every settlement's basic transport research.");
        var resumed = WorldEngine.ImportJson(engine.ExportJson());
        engine.Step(120); resumed.Step(120);
        Check(engine.ExportJson() == resumed.ExportJson(), "Large-world development diverged after saving and resuming.");
    }

    [UnitTest]
    private static void SelectedFacilityWork()
    {
        var engine = FlatWorld();
        var town = engine.State.Settlements.Single();
        var workshop = engine.State.Society.Buildings.Single(b => b.Kind == BuildingKind.Workshop);
        var worker = engine.State.Residents.Last();
        Place(worker, workshop.X, workshop.Y);
        worker.Profession = Profession.Builder;
        // Equal priority used to send this worker to the adjacent farm instead of the selected workshop.
        worker.Agent.Goal = new AgentGoal { Kind = AgentGoalKind.Work, TargetX = workshop.X, TargetY = workshop.Y,
            TargetEntityId = workshop.Id, StartedTick = engine.State.Tick, ReviewTick = engine.State.Tick + 100 };
        var before = worker.Inventory.Wood;
        Check(engine.TryWorkAtBuilding(worker) && worker.Inventory.Wood > before,
            "Working at the selected workshop produced food at a different facility instead of wood.");
        worker.Profession = Profession.Lumberjack;
        foreach (var tile in engine.State.Tiles) tile.Terrain = TerrainType.Desert;
        var resources = engine.State.Tiles.Sum(t => t.ResourceAmount);
        engine.State.Tick++;
        Check(!engine.TryWorkAtBuilding(worker) && engine.State.Tiles.Sum(t => t.ResourceAmount) == resources,
            "A workshop consumed terrain resources and claimed work despite having no usable wood source.");
    }

    private static void DevelopmentBudget()
    {
        var engine = FlatWorld(36);
        var town = engine.State.Settlements.Single();
        engine.SetNationResources(town.NationId, 500, 50, 20, 0);
        foreach (var resident in engine.State.Residents)
        {
            Place(resident, town.X, town.Y);
            resident.Agent.Goal = new AgentGoal { Kind = AgentGoalKind.Rest, TargetX = town.X, TargetY = town.Y,
                StartedTick = engine.State.Tick, ReviewTick = 100, PlayerDirected = true };
        }
        var housing = town.Housing;
        engine.Step(12);
        Check(town.Housing == housing && town.Resources.Wood >= 30 && town.Resources.Stone >= 15,
            "Housing spent materials needed to build the town's first school.");
        engine.State.Tick += (60 - (engine.State.Tick + town.Id) % 60) % 60;
        engine.TickSociety();
        var academy = engine.State.Society.Buildings.Single(b => b.Kind == BuildingKind.Academy);
        Work(engine, engine.State.Residents.First(r => r.Age >= 16 && r.Id != town.RepresentativeId), academy);
        engine.GrantReceivedResearch(town.Id, ResearchKind.Agriculture);
        foreach (var resident in engine.State.Residents) resident.MagicTalent = 60;
        engine.SetNationResources(town.NationId, 500, 100, 20, 0);
        engine.State.Tick += (60 - (engine.State.Tick + town.Id) % 60) % 60;
        engine.TickSociety();
        Check(engine.State.Society.Research.Single().ActiveProject == ResearchKind.Logistics,
            "Lacking ore for optional magic prevented funded transport research from starting.");
        engine.GrantReceivedResearch(town.Id, ResearchKind.Logistics);
        engine.GrantReceivedResearch(town.Id, ResearchKind.ArcaneArts);
        foreach (var r in ResearchRules.All.Where(r => r.Branch == "民生与资源")) engine.GrantReceivedResearch(town.Id, r.Kind);
        engine.SetNationResources(town.NationId, 500, 100, 15, 0);
        engine.State.Tick += 60;
        engine.TickSociety();
        Check(engine.State.Society.Buildings.Any(b => b.Kind == BuildingKind.Waystation),
            "An unaffordable arcane sanctum prevented the town from building its funded waystation.");
    }
}
