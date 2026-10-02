using SeWZC.WorldBox.Core;

internal static class SocietyRegressionTests
{
    public static IEnumerable<(string Name, Action Run)> Cases =>
    [
        ("destroyed academies are rebuilt without losing research or its material reserve", RebuildAcademy),
        ("both diplomatic capitals can act regardless of founding order", BothDiplomaticSides),
        ("mutual diplomatic contact contributes once and resumes deterministically", DiplomaticContinuation),
        ("overlapping settlements preserve the strongest actual local protection", ShieldOverlap),
        ("material previews and spending agree at fractional resource boundaries", FractionalMaterials)
    ];

    private static void Check(bool value, string message)
    {
        if (!value) throw new InvalidOperationException(message);
    }

    private static WorldEngine Flat(int population = 24, bool pair = false, bool reverseFounding = false, int otherX = 36)
    {
        var engine = WorldEngine.Create(223, 64, 64, false);
        foreach (var tile in engine.State.Tiles) { tile.Terrain = TerrainType.Grass; tile.Fertility = 80; tile.ResourceAmount = 100; }
        engine.ConfigureWorld(new WorldRules
        {
            Births = false, Aging = false, Hunger = false, Disease = false, Construction = false,
            Research = false, Expansion = false, Trade = false, Wars = true, Alliances = false,
            Peace = true, Migration = false, Secession = false, Conflict = 3
        }, false, false);
        if (pair && reverseFounding) engine.SpawnResidents(otherX, 24, RaceKind.Human, population);
        engine.SpawnResidents(14, 24, RaceKind.Human, population);
        if (pair && !reverseFounding) engine.SpawnResidents(otherX, 24, RaceKind.Human, population);
        foreach (var town in engine.State.Settlements)
        {
            town.Resources = new ResourceStock { Food = 1000, Wood = 1000, Stone = 1000, Ore = 1000 };
            foreach (var resident in engine.State.Residents.Where(r => r.SettlementId == town.Id)) Hold(resident, town.X, town.Y);
        }
        return engine;
    }

    private static void Hold(Resident resident, int x, int y)
    {
        resident.X = resident.FromX = x; resident.Y = resident.FromY = y;
        resident.Age = 24; resident.Health = 100; resident.Hunger = 0; resident.SicknessTicks = 0;
        resident.MagicTraining = 0; resident.Inventory = new ResourceStock { Food = 1.2 };
        resident.Agent.Goal = new AgentGoal { Kind = AgentGoalKind.Rest, TargetX = x, TargetY = y,
            PlayerDirected = true, ReviewTick = 10000, Reason = "Held society regression fixture" };
    }

    private static void RebuildAcademy()
    {
        var engine = Flat(36); var town = engine.State.Settlements.Single();
        engine.ConfigureWorld(engine.State.Rules with { Construction = true, Research = true }, false, false);
        var id = engine.GrantFacility(town.Id, BuildingKind.Academy, 18, 24);
        engine.StartResearch(town.Id, ResearchKind.Agriculture);
        var worker = engine.State.Residents[1]; Hold(worker, 18, 24);
        worker.Agent.Goal = new AgentGoal { Kind = AgentGoalKind.Study, TargetX = 18, TargetY = 24, TargetEntityId = id,
            PlayerDirected = true, ReviewTick = 10000 };
        engine.State.Tick++;
        Check(engine.TryWorkAtBuilding(worker), "The research fixture did not make actual progress.");
        var project = engine.State.Society.Research.Single(); var progress = project.Progress;
        Hold(worker, town.X, town.Y);
        engine.PaintTerrain(18, 24, TerrainType.Water, 0);
        // Housing could afford an expansion, but it must reserve the replacement school's materials.
        town.Resources.Wood = 50; town.Resources.Stone = 20; var housing = town.Housing;
        engine.Step(60);
        var replacement = engine.State.Society.Buildings.SingleOrDefault(b => b.Kind == BuildingKind.Academy);
        Check(replacement is not null && !replacement.IsCompleted, "Active research prevented rebuilding its destroyed academy.");
        Check(town.Housing == housing && town.Resources.Wood == 20 && town.Resources.Stone == 5,
            "Housing consumed the replacement academy's reserved materials.");
        Check(project.ActiveProject == ResearchKind.Agriculture && project.Progress == progress,
            "Rebuilding reset or remotely advanced the interrupted research.");
        Hold(worker, replacement!.X, replacement.Y);
        worker.Agent.Goal = new AgentGoal { Kind = AgentGoalKind.Work, TargetX = replacement.X, TargetY = replacement.Y,
            TargetEntityId = replacement.Id, PlayerDirected = true, ReviewTick = 10000 };
        while (!replacement.IsCompleted)
        {
            engine.State.Tick++;
            Check(engine.TryWorkAtBuilding(worker), "The replacement academy could not be constructed on site.");
        }
        var resumed = WorldEngine.ImportJson(engine.ExportJson());
        engine.Step(100); resumed.Step(100);
        Check(engine.HasResearch(town.Id, ResearchKind.Agriculture), "Research did not resume in the rebuilt academy.");
        Check(engine.ExportJson() == resumed.ExportJson(), "Rebuilt academy research diverged after saving.");
    }

    private static void DeliverContact(WorldEngine engine, Settlement recipient, Settlement observed)
    {
        recipient.PublicKnowledge.Add(new AgentFact
        {
            Id = engine.State.NextId++, Kind = AgentFactKind.SettlementLocation, SubjectId = observed.Id,
            X = observed.X, Y = observed.Y, Value = observed.NationId, ObservedTick = engine.State.Tick,
            LearnedTick = engine.State.Tick, OriginResidentId = recipient.RepresentativeId,
            SourceResidentId = recipient.RepresentativeId, OriginProfession = Profession.Representative,
            Text = "A physically delivered neighboring settlement report"
        });
    }

    private static void BothDiplomaticSides()
    {
        foreach (var reverse in new[] { false, true })
        {
            var engine = Flat(pair: true, reverseFounding: reverse);
            var west = engine.State.Settlements.Single(t => t.X == 14);
            var east = engine.State.Settlements.Single(t => t.X == 36);
            engine.SetPolicy(east.NationId, PolicyKind.Defense);
            engine.State.Tick = 359;
            DeliverContact(engine, west, east); DeliverContact(engine, east, west);
            var relation = engine.State.Diplomacies.Single(); relation.FirstOpinion = relation.SecondOpinion = relation.Opinion = -54;
            engine.Step();
            Check(relation.Status == DiplomaticStatus.War && relation.Opinion == -58,
                "A capital's diplomatic assessment was skipped or counted twice because of founding order.");
            Check(east.PublicKnowledge.Any(f => f.Kind == AgentFactKind.DiplomaticNotice && f.SubjectId == east.NationId),
                "The pressured capital did not issue the declaration supported by its local assessment.");
            Check(!west.PublicKnowledge.Any(f => f.Kind == AgentFactKind.WarOrder), "The other capital learned the declaration without delivery.");

            var peace = Flat(pair: true, reverseFounding: reverse);
            west = peace.State.Settlements.Single(t => t.X == 14); east = peace.State.Settlements.Single(t => t.X == 36);
            peace.State.Tick = 359; east.Resources.Food = 1;
            DeliverContact(peace, west, east); DeliverContact(peace, east, west);
            relation = peace.State.Diplomacies.Single(); relation.Status = DiplomaticStatus.War;
            relation.FirstOpinion = relation.SecondOpinion = relation.Opinion = -100;
            var events = peace.State.Events.Count;
            peace.Step();
            Check(relation.Status == DiplomaticStatus.Neutral && relation.Opinion == -48
                && (relation.FirstNationId == east.NationId ? relation.FirstOpinion : relation.SecondOpinion) == 0
                && (relation.FirstNationId == west.NationId ? relation.FirstOpinion : relation.SecondOpinion) == -96,
                "The later capital could not end a war in response to its own exhausted supplies.");
            Check(peace.State.Events.Skip(events).Count(e => e.Kind is WorldEventKind.War or WorldEventKind.Diplomacy) == 1,
                "Diplomacy applied more than one transition to the pair in one tick.");
        }
    }

    private static void DiplomaticContinuation()
    {
        var engine = Flat(pair: true);
        engine.ConfigureWorld(engine.State.Rules with { Wars = false, Alliances = true }, false, false);
        var west = engine.State.Settlements[0]; var east = engine.State.Settlements[1];
        engine.State.Tick = 359;
        DeliverContact(engine, west, east); DeliverContact(engine, east, west);
        var relation = engine.State.Diplomacies.Single(); relation.FirstOpinion = relation.SecondOpinion = relation.Opinion = 48;
        var resumed = WorldEngine.ImportJson(engine.ExportJson());
        engine.Step(); resumed.Step();
        Check(relation.Opinion == 52 && relation.AllianceOfferNationId == 0,
            "Mutual cooperation counted twice and issued an alliance offer too early.");
        engine.Step(60); resumed.Step(60);
        Check(relation.Status == DiplomaticStatus.Neutral && relation.AllianceOfferNationId == west.NationId,
            "A fresh alliance proposal was duplicated or accepted before actual delivery.");
        Check(engine.ExportJson() == resumed.ExportJson(), "Diplomatic assessment diverged after saving before its tick.");
        Check(engine.State.Settlements.Sum(t => t.PublicKnowledge.Count(f => f.Kind == AgentFactKind.DiplomaticNotice)) == 1,
            "Both sides generated competing offers in the same evaluation.");
    }

    private static void ShieldOverlap()
    {
        var engine = Flat(pair: true, otherX: 24);
        var west = engine.State.Settlements[0]; var east = engine.State.Settlements[1];
        engine.TransferTerritory(east.X, east.Y, west.NationId, 0);
        var caster = engine.State.Residents.First(r => r.SettlementId == east.Id);
        caster.MagicTraining = 30; caster.MagicTalent = 60; caster.Mana = 100;
        engine.CastSpell(caster.Id, SpellKind.Shield, east.X, east.Y);
        var victim = engine.State.Residents.First(r => r.SettlementId == west.Id);
        Hold(victim, 19, 24);
        Check(engine.TryAbsorbShieldDamage(victim, 10) == 6, "An unshielded neighboring settlement hid the active shield.");
        engine.State.Settlements.Reverse();
        Check(engine.TryAbsorbShieldDamage(victim, 10) == 6, "Shield coverage depends on settlement list order.");
        west.ShieldTicks = east.ShieldTicks;
        Check(engine.TryAbsorbShieldDamage(victim, 10) == 6, "Overlapping shields stacked their reductions.");
        engine.SetPolicy(west.NationId, PolicyKind.Defense);
        Check(Math.Abs(engine.TryAbsorbShieldDamage(victim, 10) - 5.28) < 0.000001,
            "The strongest local defense-and-shield combination was lost.");
        Hold(victim, 30, 24);
        Check(engine.TryAbsorbShieldDamage(victim, 10) == 10, "Protection extended beyond the actual local radius.");
    }

    [UnitTest]
    private static void FractionalMaterials()
    {
        var engine = Flat(); var town = engine.State.Settlements.Single();
        engine.ConfigureWorld(engine.State.Rules with { Construction = true }, false, false);
        town.Resources.Wood = 30 - 0.0000001; town.Resources.Stone = 15;
        Check(engine.FacilityPlacementError(town.Id, BuildingKind.Academy, 18, 24) is null,
            "The fixture did not exercise an accepted roundoff-sized shortage.");
        engine.Step(60);
        Check(engine.State.Society.Buildings.Any(b => b.Kind == BuildingKind.Academy) && town.Resources.Wood == 0 && town.Resources.Stone == 0,
            "Autonomous construction rejected an accepted cost or left a negative inventory.");
        _ = WorldEngine.ImportJson(engine.ExportJson());

        var research = Flat(); town = research.State.Settlements.Single();
        research.GrantFacility(town.Id, BuildingKind.Academy, 18, 24);
        town.Resources.Food = 20 - 0.0000001; town.Resources.Wood = 15 - 0.0000001;
        research.StartResearch(town.Id, ResearchKind.Agriculture);
        Check(town.Resources.Food == 0 && town.Resources.Wood == 0, "Research spending uses a different material tolerance.");

        var insufficient = Flat(); town = insufficient.State.Settlements.Single();
        town.Resources.Wood = 30 - 0.00001; town.Resources.Stone = 15;
        var before = insufficient.ExportJson();
        Check(insufficient.FacilityPlacementError(town.Id, BuildingKind.Academy, 18, 24) is not null,
            "A material shortage larger than roundoff passed the preview.");
        try { insufficient.BuildFacility(town.Id, BuildingKind.Academy, 18, 24); throw new Exception("An actual material shortage was accepted."); }
        catch (InvalidOperationException) { }
        Check(insufficient.ExportJson() == before, "Rejected spending partially changed the world.");
    }
}
