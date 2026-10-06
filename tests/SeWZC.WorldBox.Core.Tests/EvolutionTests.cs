using System.Text.Json.Nodes;
using SeWZC.WorldBox.Core;

internal static class EvolutionTests
{
    public static IEnumerable<(string Name, Action Run)> Cases =>
    [
        ("world rules preserve disabled values, reject invalid edits and affect survival", Rules),
        ("placement previews are read-only and gifts differ from funded construction", Placement),
        ("development explains missing materials and recovers when supplied", Development),
        ("autonomous war requires delivered contact and does not instantly inform the other capital", Diplomacy),
        ("alliance requires a delivered proposal and foreign military orders cannot recruit", Alliance),
        ("migration changes membership only after physical arrival", Migration),
        ("trait presets change the actual personality used by decisions", Traits),
    ];

    private static void Check(bool value, string message)
    {
        if (!value) throw new InvalidOperationException(message);
    }

    private static WorldEngine Flat(int size = 64, int population = 24)
    {
        var engine = WorldEngine.Create(223, size, size, false);
        foreach (var tile in engine.State.Tiles)
        {
            tile.Terrain = TerrainType.Grass;
            tile.Fertility = 80;
        }

        engine.State.NaturalDisasters = false;
        engine.State.Rules.Thirst = false;
        engine.SpawnResidents(14, 24, RaceKind.Human, population);
        TestLand.ClaimAllTowns(engine);
        return engine;
    }

    private static AgentFact Fact(WorldEngine engine, AgentFactKind kind, int subject, int x, int y, double value,
        int source)
    {
        return new AgentFact
        {
            Id = engine.State.NextId++,
            Kind = kind,
            SubjectId = subject,
            X = x,
            Y = y,
            Value = value,
            ObservedTick = engine.State.Tick,
            LearnedTick = engine.State.Tick,
            OriginResidentId = source,
            SourceResidentId = source,
            OriginProfession = Profession.Messenger,
            Text = "实际送达的测试报告",
        };
    }

    private static void Rules()
    {
        var engine = Flat();
        var rules = WorldRules.For(WorldPreset.Flourishing) with
        {
            Births = false,
            Aging = false,
            Hunger = false,
            Thirst = false,
            Disease = false,
            Construction = false,
            Research = false,
            Trade = false,
        };
        engine.ConfigureWorld(rules, false, false);
        var before = engine.ExportJson();
        try
        {
            engine.ConfigureWorld(rules with { DevelopmentRate = double.NaN }, true, true);
            throw new InvalidOperationException("Invalid rules accepted");
        }
        catch (ArgumentException)
        {
        }

        Check(engine.ExportJson() == before, "Invalid rules partially changed the world");
        var restored = WorldEngine.ImportJson(before);
        Check(restored.State.Rules == rules && !restored.State.NaturalDisasters && !restored.State.Society.MagicEnabled,
            "Disabled rules changed on load");
        var person = engine.State.Residents[0];
        var age = person.Age;
        person.Hunger = 100;
        engine.Step(60);
        Check(person.Age == age && person.Health == 100 && engine.State.Residents.Count == 24,
            "Disabled survival rules did not take effect");
        restored = WorldEngine.ImportJson(engine.ExportJson());
        engine.Step(120);
        restored.Step(120);
        Check(engine.ExportJson() == restored.ExportJson(), "Rule-driven world lost deterministic continuation");
        var old = JsonNode.Parse(before)!;
        old["FormatVersion"] = 2;
        try
        {
            WorldEngine.ImportJson(old.ToJsonString());
            throw new InvalidOperationException("Old format accepted");
        }
        catch (ArgumentException)
        {
        }
    }

    [UnitTest]
    private static void Placement()
    {
        var engine = Flat(32, 1);
        TestLand.ClearWildlife(engine);
        var town = engine.State.Settlements[0];
        town.Resources = new ResourceStock();
        var before = engine.ExportJson();
        Check(engine.FacilityPlacementError(town.Id, BuildingKind.Academy, town.X + 2, town.Y) is not null,
            "Cost missing from preview");
        Check(engine.FacilityPlacementError(town.Id, BuildingKind.Academy, town.X + 2, town.Y, true) is null,
            "Gift unexpectedly required resources");
        Check(engine.ExportJson() == before, "Preview mutated the world");
        try
        {
            engine.BuildFacility(town.Id, BuildingKind.Academy, town.X + 2, town.Y);
        }
        catch (InvalidOperationException)
        {
        }

        Check(engine.ExportJson() == before, "Failed placement mutated the world");
        var id = engine.GrantFacility(town.Id, BuildingKind.Academy, town.X + 2, town.Y);
        Check(engine.State.Society.Buildings.Single(b => b.Id == id).IsCompleted && town.Resources.Wood == 0,
            "Gift did not create a completed building without cost");
        engine.SetNationResources(town.NationId, 100, 100, 100, 100);
        id = engine.BuildFacility(town.Id, BuildingKind.Infirmary, town.X + 3, town.Y);
        Check(!engine.State.Society.Buildings.Single(b => b.Id == id).IsCompleted && town.Resources.Wood == 75,
            "Construction bypassed cost or labor");
        _ = WorldEngine.ImportJson(engine.ExportJson());
    }

    private static void Development()
    {
        var engine = Flat();
        var town = engine.State.Settlements[0];
        town.Resources = new ResourceStock { Food = 1000 };
        foreach (var resident in engine.State.Residents)
        {
            resident.Age = 22;
            resident.Inventory.Food = 100;
            resident.Agent.Goal = new AgentGoal
            {
                Kind = AgentGoalKind.Rest,
                TargetX = town.X,
                TargetY = town.Y,
                PlayerDirected = true,
                ReviewTick = 1000,
            };
        }

        var before = engine.ExportJson();
        _ = engine.GetDevelopment(town.Id);
        Check(before == engine.ExportJson(), "Development inspection mutated state");
        engine.Step(60);
        var summary = engine.GetDevelopment(town.Id);
        Check(summary.Blocker.Contains("缺"), "Blocked development has no concrete missing resource");
        engine.SetNationResources(town.NationId, 1000, 1000, 1000, 1000);
        foreach (var resident in engine.State.Residents)
        {
            resident.Agent.Goal.PlayerDirected = false;
            resident.Agent.NextThinkTick = engine.State.Tick;
        }

        engine.Step(360);
        Check(engine.State.Society.Buildings.Any(b => b.Kind == BuildingKind.Academy && b.IsCompleted),
            "Development did not recover after supplying missing resources");
    }

    private static void Diplomacy()
    {
        var engine = Flat();
        engine.SpawnResidents(36, 24, RaceKind.Orc, 24);
        engine.ConfigureWorld(
            new WorldRules
            {
                Births = false,
                Aging = false,
                Hunger = false,
                Thirst = false,
                Construction = false,
                Research = false,
                Expansion = false,
                Conflict = 3,
            }, false, false);
        foreach (var resident in engine.State.Residents)
        {
            resident.Age = 24;
            resident.Agent.Goal = new AgentGoal
            {
                Kind = AgentGoalKind.Rest,
                TargetX = resident.X,
                TargetY = resident.Y,
                PlayerDirected = true,
                ReviewTick = 3000,
            };
        }

        engine.Step(420);
        Check(engine.State.Diplomacies.All(d => d.Status == DiplomaticStatus.Neutral),
            "Nations declared war without delivered information");
        var a = engine.State.Settlements[0];
        var b = engine.State.Settlements[1];
        a.PublicKnowledge.Add(Fact(engine, AgentFactKind.SettlementLocation, b.Id, b.X, b.Y, b.NationId,
            a.RepresentativeId));
        var relation = engine.State.Diplomacies.Single();
        relation.FirstOpinion = relation.SecondOpinion = relation.Opinion = -90;
        a.Resources.Food = 80;
        engine.Step(60);
        Check(relation.Status == DiplomaticStatus.Neutral && relation.FirstEscalationTick > 0,
            "New dispute immediately became war");
        engine.Step(180);
        Check(relation.Status == DiplomaticStatus.War && relation.Reason.Length > 0,
            "Delivered contact and hostility did not create autonomous war");
        Check(!b.PublicKnowledge.Any(f => f.Kind == AgentFactKind.WarOrder),
            "Other capital learned the war declaration without delivery");
        var restored = WorldEngine.ImportJson(engine.ExportJson());
        engine.Step(120);
        restored.Step(120);
        Check(engine.ExportJson() == restored.ExportJson(), "Diplomatic state did not resume deterministically");
    }

    private static void Alliance()
    {
        var engine = Flat();
        engine.SpawnResidents(36, 24, RaceKind.Elf, 24);
        engine.ConfigureWorld(
            new WorldRules
            {
                Births = false,
                Aging = false,
                Hunger = false,
                Thirst = false,
                Construction = false,
                Research = false,
                Expansion = false,
                Wars = false,
            }, false, false);
        foreach (var resident in engine.State.Residents)
            resident.Agent.Goal = new AgentGoal
            {
                Kind = AgentGoalKind.Rest,
                TargetX = resident.X,
                TargetY = resident.Y,
                PlayerDirected = true,
                ReviewTick = 3000,
            };
        var a = engine.State.Settlements[0];
        var b = engine.State.Settlements[1];
        a.PublicKnowledge.Add(Fact(engine, AgentFactKind.SettlementLocation, b.Id, b.X, b.Y, b.NationId,
            a.RepresentativeId));
        b.PublicKnowledge.Add(Fact(engine, AgentFactKind.SettlementLocation, a.Id, a.X, a.Y, a.NationId,
            b.RepresentativeId));
        var foreignOrder = Fact(engine, AgentFactKind.WarOrder, a.NationId, a.X, a.Y, a.Id, b.RepresentativeId);
        foreignOrder.TargetNationId = b.NationId;
        a.PublicKnowledge.Add(foreignOrder);
        a.Resources.Food = b.Resources.Food = 1000;
        var initialRelation = engine.State.Diplomacies.Single();
        initialRelation.FirstOpinion = initialRelation.SecondOpinion = initialRelation.Opinion = 80;
        engine.Step(420);
        Check(engine.State.Armies.Count == 0, "Foreign orders recruited a self-targeting army");
        var relation = engine.State.Diplomacies.Single();
        Check(relation.Status == DiplomaticStatus.Neutral && relation.AllianceOfferNationId == a.NationId,
            "Alliance completed before delivery");
        var carrier = engine.State.Residents.First(r => r.SettlementId == a.Id);
        carrier.Age = 24;
        carrier.Profession = Profession.Messenger;
        carrier.Inventory.Food = 100;
        carrier.X = a.X;
        carrier.Y = a.Y;
        carrier.FromX = a.X;
        carrier.FromY = a.Y;
        carrier.Agent.Goal = new AgentGoal
        {
            Kind = AgentGoalKind.DeliverMessage,
            TargetSettlementId = b.Id,
            TargetX = b.X,
            TargetY = b.Y,
            StartedTick = engine.State.Tick,
        };
        carrier.Agent.Memory.Add(Fact(engine, AgentFactKind.SettlementLocation, b.Id, b.X, b.Y, b.NationId,
            carrier.Id));
        carrier.Agent.CarriedMessages = [a.PublicKnowledge.Single(f => f.Kind == AgentFactKind.DiplomaticNotice)];
        carrier.Agent.DestinationSettlementId = b.Id;
        carrier.Agent.MissionOriginSettlementId = a.Id;
        carrier.Agent.MissionStartedTick = engine.State.Tick;
        carrier.Agent.NextThinkTick = engine.State.Tick + 6;
        engine.Step();
        Check(relation.Status == DiplomaticStatus.Neutral && Math.Abs(carrier.X - a.X) <= 1, "Proposal teleported");
        engine.Step(90);
        Check(relation.Status == DiplomaticStatus.Allied, "Actually delivered proposal was not accepted");
        _ = WorldEngine.ImportJson(engine.ExportJson());
    }

    private static void Migration()
    {
        var engine = Flat();
        engine.SpawnResidents(36, 24, RaceKind.Elf);
        var a = engine.State.Settlements[0];
        var b = engine.State.Settlements[1];
        engine.ConfigureWorld(
            new WorldRules
            {
                Births = false,
                Wars = false,
                Expansion = false,
                Construction = false,
                Research = false,
            },
            false, false);
        b.Resources.Food = 1000;
        var person = engine.State.Residents[0];
        person.Age = 20;
        person.Inventory.Food = 50;
        person.Agent.Goal = new AgentGoal
        {
            Kind = AgentGoalKind.Migrate,
            TargetX = b.X,
            TargetY = b.Y,
            TargetSettlementId = b.Id,
            ReviewTick = 360,
            Reason = "已收到目标粮情，步行迁居",
        };
        person.Agent.NextThinkTick = 300;
        var oldX = person.X;
        engine.Step();
        Check(person.SettlementId == a.Id && Math.Abs(person.X - oldX) <= 1,
            "Migration teleported or changed membership before arrival");
        engine.Step(100);
        Check(person.SettlementId == b.Id && person.NationId == b.NationId,
            "Migrant did not join the physically reached settlement");
        _ = WorldEngine.ImportJson(engine.ExportJson());
    }

    [UnitTest]
    private static void Traits()
    {
        var engine = Flat(32, 1);
        var id = engine.State.Residents[0].Id;
        engine.EditResident(id, new ResidentEdit { Trait = "勇敢" });
        Check(engine.GetResident(id)!.Agent.Personality.Courage == .9, "Trait preset changed only its label");
    }
}
