using SeWZC.WorldBox.Core;

internal static class DiplomacyKnowledgeTests
{
    public static IEnumerable<(string Name, Action Run)> Cases =>
    [
        ("foreign shortages cannot change another capital's accumulated attitude or choices", ForeignShortage),
        ("one-sided contact only updates the informed diplomatic attitude", OneSidedContact),
        ("war and alliance thresholds use the acting capital's own attitude", LocalThresholds),
        ("unreported foreign capital moves preserve declarations and military targets", UnknownCapitalMoves),
        ("peace notices change only the receiving attitude and ignore old deliveries", PeaceDelivery),
        ("asymmetric diplomatic attitudes resume exactly around evaluation ticks", AttitudeContinuation),
    ];

    private static void ForeignShortage()
    {
        var original = World((14, 24), (36, 3));
        var a = original.State.Settlements[0];
        var b = original.State.Settlements[1];
        original.State.Tick = 359;
        Contact(original, a, b);
        Contact(original, b, a);
        Opinions(original.State.Diplomacies.Single(), -54, -54);
        var supplied = WorldEngine.ImportJson(original.ExportJson());
        var hungry = WorldEngine.ImportJson(original.ExportJson());
        hungry.State.Settlements.Single(t => t.Id == b.Id).Resources.Food = 0;
        for (var evaluation = 0; evaluation < 4; evaluation++)
        {
            supplied.Step(evaluation == 0 ? 1 : 60);
            hungry.Step(evaluation == 0 ? 1 : 60);
            var local = hungry.State.Diplomacies.Single();
            Check(Opinion(local, a.NationId) == -50 + evaluation * 4
                  && Opinion(local, a.NationId) == Opinion(supplied.State.Diplomacies.Single(), a.NationId),
                "The other capital's undisclosed shortage contaminated the local attitude across ticks.");
            Check(
                local.Status == DiplomaticStatus.Neutral &&
                supplied.State.Diplomacies.Single().Status == DiplomaticStatus.Neutral,
                "A cooperative nation declared war because of another capital's private hardship.");
            var capital = hungry.State.Settlements.Single(t => t.Id == a.Id);
            Check(!capital.PublicKnowledge.Any(f => f.Kind == AgentFactKind.FoodSupply && f.SubjectId == b.Id)
                  && !capital.PublicKnowledge.Any(f => f.Kind == AgentFactKind.WarOrder),
                "The isolated fixture unexpectedly delivered foreign food news or issued a war order.");
            hungry = WorldEngine.ImportJson(hungry.ExportJson());
        }

        Check(
            Opinion(hungry.State.Diplomacies.Single(), b.NationId) !=
            Opinion(supplied.State.Diplomacies.Single(), b.NationId),
            "The foreign capital must still respond to its own local shortage.");
    }

    private static void OneSidedContact()
    {
        var engine = World((14, 24), (36, 3));
        engine.State.Rules.Wars = false;
        var a = engine.State.Settlements[0];
        var b = engine.State.Settlements[1];
        var relation = engine.State.Diplomacies.Single();
        Opinions(relation, 0, -25);
        engine.Step(60);
        Check(relation.FirstOpinion == 0 && relation.SecondOpinion == -25,
            "A nation assessed another country without any delivered contact.");
        Contact(engine, a, b);
        engine.Step(60);
        Check(Opinion(relation, a.NationId) == 4 && Opinion(relation, b.NationId) == -25,
            "A one-sided contact also updated the uninformed capital's attitude.");
        Contact(engine, b, a);
        engine.Step(60);
        Check(Opinion(relation, a.NationId) == 8 && Opinion(relation, b.NationId) == -21 && relation.Opinion == -7,
            "New contact inherited the pair average instead of continuing the capital's own attitude.");
        Check(WorldEngine.ImportJson(engine.ExportJson()).ExportJson() == engine.ExportJson(),
            "Asymmetric negative half-point display values did not round-trip.");
    }

    private static void LocalThresholds()
    {
        foreach (var war in new[] { true, false })
        {
            var engine = World((14, 24), (36, 3));
            engine.State.Rules.Wars = war;
            engine.State.Rules.Alliances = !war;
            var a = engine.State.Settlements[0];
            var b = engine.State.Settlements[1];
            if (war)
                engine.SetPolicy(a.NationId, PolicyKind.Defense);
            engine.State.Tick = 359;
            Contact(engine, a, b);
            Contact(engine, b, a);
            var relation = engine.State.Diplomacies.Single();
            Opinions(relation, war ? -54 : 54, war ? 100 : -100);
            if (war)
                relation.FirstEscalationTick = relation.SecondEscalationTick = 120;
            engine.Step();
            if (war)
            {
                Check(relation.Status == DiplomaticStatus.War && relation.Opinion > 0
                                                              && a.PublicKnowledge.Any(f =>
                                                                  f.Kind == AgentFactKind.WarOrder &&
                                                                  f.SubjectId == b.NationId),
                    "A positive displayed average overrode the acting capital's hostile assessment.");
            }
            else
            {
                Check(
                    relation.Status == DiplomaticStatus.Neutral && relation.Opinion < 0 &&
                    relation.AllianceOfferNationId == a.NationId,
                    "A negative displayed average prevented a locally supported alliance proposal.");
            }
        }
    }

    private static void UnknownCapitalMoves()
    {
        foreach (var mutual in new[] { false, true })
        {
            var original = World((8, 24), (30, 3), (50, 3));
            var a = original.State.Settlements[0];
            var b = original.State.Settlements[1];
            var c = original.State.Settlements[2];
            original.SetPolicy(a.NationId, PolicyKind.Defense);
            original.State.Tick = 359;
            Contact(original, a, b);
            Contact(original, a, c, 32);
            if (mutual)
            {
                Contact(original, b, a);
                Contact(original, c, a);
            }

            foreach (var relation in original.State.Diplomacies)
            {
                Opinions(relation, -54, -54);
                relation.FirstEscalationTick = relation.SecondEscalationTick = 120;
            }

            var unchanged = WorldEngine.ImportJson(original.ExportJson());
            var moved = WorldEngine.ImportJson(original.ExportJson());
            // 改变远方首都的地理排序；仍置于观察范围外，并保留已递送报告。
            var movedTown = moved.State.Settlements.Single(t => t.Id == b.Id);
            moved.State.Tiles[movedTown.Y * moved.State.Width + movedTown.X].SettlementId = 0;
            movedTown.X = 2;
            movedTown.Y = 50;
            var capitalTile = moved.State.Tiles[50 * moved.State.Width + 2];
            capitalTile.SettlementId = b.Id;
            capitalTile.NationId = b.NationId;
            moved.ReconcileSocietyTopology();
            moved = WorldEngine.ImportJson(moved.ExportJson());
            unchanged.Step();
            moved.Step();
            var expected = unchanged.State.Armies.Single(army => army.NationId == a.NationId);
            var actual = moved.State.Armies.Single(army => army.NationId == a.NationId);
            Check(actual.TargetNationId == expected.TargetNationId && actual.TargetNationId == c.NationId
                                                                   && actual.TargetX == 32 && actual.TargetY == 24,
                "An unreported foreign capital move changed which delivered report supplied the latest military order.");
            var expectedOrders = unchanged.State.Settlements.Single(t => t.Id == a.Id).PublicKnowledge
                .Where(f => f.Kind == AgentFactKind.WarOrder).OrderBy(f => f.Id).Select(f => (f.SubjectId, f.X, f.Y))
                .ToArray();
            var actualOrders = moved.State.Settlements.Single(t => t.Id == a.Id).PublicKnowledge
                .Where(f => f.Kind == AgentFactKind.WarOrder).OrderBy(f => f.Id).Select(f => (f.SubjectId, f.X, f.Y))
                .ToArray();
            Check(actualOrders.SequenceEqual(expectedOrders),
                "Unknown geography reordered the capital's declarations.");
        }
    }

    private static void PeaceDelivery()
    {
        var engine = World((14, 24), (36, 3));
        engine.State.Rules.Wars = false;
        engine.State.Rules.Peace = true;
        var a = engine.State.Settlements[0];
        var b = engine.State.Settlements[1];
        engine.State.Tick = 359;
        a.Resources.Food = 1;
        Contact(engine, a, b);
        Contact(engine, b, a);
        var relation = engine.State.Diplomacies.Single();
        relation.Status = DiplomaticStatus.War;
        Opinions(relation, -60, -80);
        engine.Step();
        Check(relation.Status == DiplomaticStatus.Neutral && Opinion(relation, a.NationId) == 0
                                                          && Opinion(relation, b.NationId) == -76,
            "A local ceasefire immediately reset the other capital's undelivered attitude.");
        var notice =
            a.PublicKnowledge.Single(f => f.Kind == AgentFactKind.DiplomaticNotice && f.TargetNationId == b.NationId);
        var carrier = engine.State.Residents.First(r => r.SettlementId == a.Id);
        Carry(engine, carrier, b, notice);
        engine.Step();
        Check(Opinion(relation, b.NationId) == -76 && carrier.X < b.X - 1,
            "A ceasefire changed the remote attitude before its courier arrived.");
        for (var i = 0; i < 90 && !HasOrder(b, a.NationId, notice.ObservedTick); i++)
            engine.Step();
        Check(
            HasOrder(b, a.NationId, notice.ObservedTick) &&
            Opinion(relation, b.NationId) == (engine.State.Tick % 60 == 0 ? 4 : 0),
            "Actual ceasefire delivery did not reset only the receiving capital's attitude.");
        var localA = Opinion(relation, a.NationId);
        SetOpinion(relation, b.NationId, -17);
        Carry(engine, carrier, b, notice);
        engine.Step(4);
        Check(Opinion(relation, b.NationId) == -17 && Opinion(relation, a.NationId) == localA,
            "A duplicate delivered ceasefire reset later local assessments.");
        var newerWar = new AgentFact
        {
            Id = engine.State.NextId++,
            Kind = AgentFactKind.WarOrder,
            SubjectId = a.NationId,
            TargetNationId = b.NationId,
            X = a.X,
            Y = a.Y,
            ObservedTick = engine.State.Tick,
            LearnedTick = engine.State.Tick,
            Text = "A newer locally received war order",
        };
        b.PublicKnowledge.Add(newerWar);
        SetOpinion(relation, b.NationId, -33);
        Carry(engine, carrier, b, notice);
        engine.Step(4);
        Check(Opinion(relation, b.NationId) == -33,
            "An older ceasefire arriving after a newer military order reset the current attitude.");
        _ = WorldEngine.ImportJson(engine.ExportJson());
    }

    private static bool HasOrder(Settlement town, int nationId, long tick)
    {
        return town.PublicKnowledge
            .Any(f => f.Kind == AgentFactKind.PeaceOrder && f.SubjectId == nationId && f.ObservedTick == tick);
    }

    private static void Carry(WorldEngine engine, Resident carrier, Settlement destination, AgentFact fact)
    {
        carrier.Profession = Profession.Messenger;
        carrier.Agent.Goal = new AgentGoal
        {
            Kind = AgentGoalKind.DeliverMessage,
            TargetSettlementId = destination.Id,
            TargetX = destination.X,
            TargetY = destination.Y,
            StartedTick = engine.State.Tick,
            PlayerDirected = true,
            ReviewTick = engine.State.Tick + 240,
        };
        carrier.Agent.CarriedMessages = [fact];
        carrier.Agent.DestinationSettlementId = destination.Id;
        carrier.Agent.MissionOriginSettlementId = carrier.SettlementId;
        carrier.Agent.MissionStartedTick = engine.State.Tick;
        carrier.Agent.NextThinkTick = engine.State.Tick + 6;
    }

    private static void AttitudeContinuation()
    {
        foreach (var saveTick in new[] { 59, 60, 61 })
        {
            var engine = World((14, 24), (36, 3));
            engine.State.Rules.Wars = false;
            var a = engine.State.Settlements[0];
            var b = engine.State.Settlements[1];
            Opinions(engine.State.Diplomacies.Single(), 0, -73);
            Contact(engine, a, b);
            engine.Step(saveTick);
            var saved = engine.ExportJson();
            var resumed = WorldEngine.ImportJson(saved);
            Check(saved == resumed.ExportJson(),
                "The directional attitudes changed when loaded at an evaluation boundary.");
            for (var i = 0; i < 121; i++)
            {
                engine.Step();
                resumed.Step();
                Check(engine.ExportJson() == resumed.ExportJson(),
                    "Directional attitudes diverged after a save around an evaluation tick.");
            }

            Check(Opinion(engine.State.Diplomacies.Single(), b.NationId) == -73,
                "Loading or another capital's evaluations overwrote an uninformed attitude.");
        }
    }

    private static WorldEngine World(params (int X, int Population)[] locations)
    {
        var engine = WorldEngine.Create(223, 64, 64, false);
        foreach (var tile in engine.State.Tiles)
        {
            tile.Terrain = TerrainType.Grass;
            tile.Fertility = 80;
            tile.ResourceAmount = 100;
        }

        engine.ConfigureWorld(new WorldRules
        {
            Births = false,
            Aging = false,
            Hunger = false,
            Thirst = false,
            Disease = false,
            Construction = false,
            Research = false,
            Expansion = false,
            Trade = false,
            Wars = true,
            Alliances = false,
            Peace = false,
            Migration = false,
            Secession = false,
            Conflict = 3,
        }, false, false);
        foreach (var (x, population) in locations)
            engine.SpawnResidents(x, 24, RaceKind.Human, population);
        foreach (var culture in engine.State.Society.Cultures)
            culture.Cooperation = .8;
        foreach (var town in engine.State.Settlements)
        {
            town.Resources = new ResourceStock { Food = 1000, Wood = 1000, Stone = 1000, Ore = 1000 };
            foreach (var resident in engine.State.Residents.Where(r => r.SettlementId == town.Id))
            {
                resident.X = resident.FromX = town.X;
                resident.Y = resident.FromY = town.Y;
                resident.Age = 24;
                resident.Health = 100;
                resident.Hunger = 0;
                resident.SicknessTicks = 0;
                resident.Inventory = new ResourceStock { Food = 1.2 };
                resident.Agent.Goal = new AgentGoal
                {
                    Kind = AgentGoalKind.Rest,
                    TargetX = town.X,
                    TargetY = town.Y,
                    PlayerDirected = true,
                    ReviewTick = 10000,
                };
            }
        }

        return engine;
    }

    private static void Contact(WorldEngine engine, Settlement recipient, Settlement observed, int? knownX = null)
    {
        recipient.PublicKnowledge.Add(new AgentFact
        {
            Id = engine.State.NextId++,
            Kind = AgentFactKind.SettlementLocation,
            SubjectId = observed.Id,
            X = knownX ?? observed.X,
            Y = observed.Y,
            Value = observed.NationId,
            ObservedTick = engine.State.Tick,
            LearnedTick = engine.State.Tick,
            OriginResidentId = recipient.RepresentativeId,
            SourceResidentId = recipient.RepresentativeId,
            OriginProfession = Profession.Representative,
            Text = "Previously delivered contact",
        });
    }

    private static int Opinion(DiplomaticRelation relation, int nationId)
    {
        return nationId == relation.FirstNationId ? relation.FirstOpinion : relation.SecondOpinion;
    }

    private static void SetOpinion(DiplomaticRelation relation, int nationId, int value)
    {
        Opinions(relation, nationId == relation.FirstNationId ? value : relation.FirstOpinion,
            nationId == relation.SecondNationId ? value : relation.SecondOpinion);
    }

    private static void Opinions(DiplomaticRelation relation, int first, int second)
    {
        relation.FirstOpinion = first;
        relation.SecondOpinion = second;
        relation.Opinion = (int)Math.Round((first + second) / 2d, MidpointRounding.AwayFromZero);
    }

    private static void Check(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }
}
