using SeWZC.WorldBox.Core;

internal static class AgentRegressionTests
{
    public static IEnumerable<(string Name, Action Run)> Cases =>
    [
        ("frontline orders wait for delivery to the commander", FrontlineOrderDelivery),
        ("miners use adjacent mountain deposits after local ore is exhausted", ExhaustedMiningSites)
    ];

    private static void FrontlineOrderDelivery()
    {
        var engine = Flat();
        engine.SpawnResidents(10, 24, RaceKind.Human, 8);
        engine.SpawnResidents(50, 24, RaceKind.Elf, 3);
        var home = engine.State.Settlements[0]; var enemy = engine.State.Settlements[1];
        foreach (var person in engine.State.Residents)
        {
            var town = engine.State.Settlements.Single(s => s.Id == person.SettlementId);
            Hold(person, town.X, town.Y);
        }
        engine.State.Tick = 29;
        engine.SetDiplomacy(home.NationId, enemy.NationId, DiplomaticStatus.War);
        engine.Step();
        var army = engine.State.Armies.Single(a => a.NationId == home.NationId);
        var soldiers = engine.State.Residents.Where(r => r.ArmyId == army.Id).ToArray();
        var commander = soldiers.Single(r => r.Id == army.CommanderId);
        var carrier = soldiers.First(r => r.Id != commander.Id);
        Check(commander.Agent.Memory.Any(f => f.Kind == AgentFactKind.WarOrder && f.SubjectId == enemy.NationId)
            && army.LastOrderTick == 29, "Recruitment did not give the commander the local war order.");

        Hold(commander, 32, 24);
        army.X = army.FromX = commander.X; army.Y = army.FromY = commander.Y;
        army.Gathering = false;
        engine.SetDiplomacy(home.NationId, enemy.NationId, DiplomaticStatus.Neutral);
        var peace = carrier.Agent.Memory.Single(f => f.Kind == AgentFactKind.PeaceOrder && f.SubjectId == enemy.NationId);
        Check(!commander.Agent.Memory.Any(f => f.Id == peace.Id), "The fixture informed the distant commander directly.");
        engine.Step();
        Check(!army.Retreating && army.KnownDiplomacy == DiplomaticStatus.War && army.LastOrderTick == 29,
            "A soldier still at the capital changed frontline orders without delivering them.");

        // Bring the carrier to the front; the ordinary conversation queue must still deliver the order.
        Hold(carrier, commander.X - 1, commander.Y);
        PendingMessage? delivery = null;
        for (var tick = 0; tick < 12 && delivery is null; tick++)
        {
            engine.Step();
            Check(!army.Retreating && commander.Agent.Memory.All(f => f.Id != peace.Id),
                "The commander acted before the local conversation was delivered.");
            delivery = engine.State.PendingMessages.FirstOrDefault(m => m.SenderId == carrier.Id
                && m.RecipientId == commander.Id && m.Facts.Any(f => f.Id == peace.Id));
        }
        Check(delivery is not null, "The nearby soldier never queued the peace order for the commander.");
        engine.Step();
        var received = commander.Agent.Memory.Single(f => f.Id == peace.Id);
        Check(received.SourceResidentId == carrier.Id && received.LearnedTick == engine.State.Tick
            && received.ObservedTick == peace.ObservedTick && army.Retreating
            && army.KnownDiplomacy == DiplomaticStatus.Neutral && army.LastOrderTick == peace.ObservedTick,
            "Actual delivery did not preserve provenance and change the commander's orders.");

        // Equal-time contradictory orders retain the existing stable ID ordering.
        var olderPeace = Order(engine, commander, enemy.NationId, AgentFactKind.PeaceOrder);
        var latestWar = Order(engine, commander, enemy.NationId, AgentFactKind.WarOrder);
        commander.Agent.Memory.AddRange([olderPeace, latestWar]);
        engine.Step();
        Check(!army.Retreating && army.KnownDiplomacy == DiplomaticStatus.War
            && army.LastOrderTick == latestWar.ObservedTick, "Equal-time orders stopped preferring the latest fact ID.");
    }

    private static AgentFact Order(WorldEngine engine, Resident commander, int enemyId, AgentFactKind kind) => new()
    {
        Id = engine.State.NextId++, Kind = kind, SubjectId = enemyId, TargetNationId = commander.NationId,
        X = commander.X, Y = commander.Y, ObservedTick = engine.State.Tick, LearnedTick = engine.State.Tick,
        OriginResidentId = commander.Id, SourceResidentId = commander.Id, OriginProfession = commander.Profession
    };

    private static void ExhaustedMiningSites()
    {
        foreach (var terrain in new[] { TerrainType.Hills, TerrainType.Snow, TerrainType.Grass })
        foreach (var localStock in terrain == TerrainType.Grass ? new[] { 0d } : new[] { 0d, 0.1 })
        {
            var engine = Flat();
            engine.SpawnResidents(10, 24, RaceKind.Human, 1);
            engine.State.Society.Buildings.Clear();
            var miner = engine.State.Residents.Single();
            Hold(miner, 20, 20);
            miner.Profession = Profession.Miner;
            miner.Agent.Personality.Diligence = 1;
            miner.Agent.Goal = new AgentGoal(); miner.Agent.NextThinkTick = engine.State.Tick;
            var site = engine.State.Tiles[20 * engine.State.Width + 20];
            site.Terrain = terrain; site.ResourceAmount = localStock;
            var mountain = engine.State.Tiles[20 * engine.State.Width + 21];
            mountain.Terrain = TerrainType.Mountain; mountain.ResourceAmount = 100;

            engine.Step();
            Check(miner.Agent.Goal.Kind == AgentGoalKind.Work && miner.X == 20 && miner.Y == 20,
                "The miner did not choose the visible adjacent deposit from a walkable work site.");
            if (localStock > 0)
                Check(site.ResourceAmount == 0 && mountain.ResourceAmount == 100
                    && Math.Abs(miner.Inventory.Stone + miner.Inventory.Ore - localStock) < 0.000001,
                    "The miner skipped the remaining local deposit before mining the adjacent mountain.");
            else Check(mountain.ResourceAmount < 100, "An exhausted work site prevented mining the stocked adjacent mountain.");

            engine.Step(5);
            var carried = miner.Inventory.Stone + miner.Inventory.Ore;
            Check(mountain.ResourceAmount < 100 && carried > localStock
                && Math.Abs(100 + localStock - site.ResourceAmount - mountain.ResourceAmount - carried) < 0.000001,
                "Mining after depletion did not transfer actual deposit stock into personal inventory.");
        }
    }

    private static WorldEngine Flat()
    {
        var engine = WorldEngine.Create(223, 64, 64, false);
        foreach (var tile in engine.State.Tiles) { tile.Terrain = TerrainType.Grass; tile.Fertility = 0; }
        engine.ConfigureWorld(new WorldRules { Births = false, Aging = false, Hunger = false, Disease = false,
            Construction = false, Research = false, Expansion = false, Trade = false, Wars = false,
            Alliances = false, Peace = false, Migration = false, Secession = false }, false, false);
        return engine;
    }

    private static void Hold(Resident person, int x, int y)
    {
        person.Age = 25; person.Inventory.Food = 1.2;
        person.X = person.FromX = x; person.Y = person.FromY = y;
        person.Agent.Goal = new AgentGoal { Kind = AgentGoalKind.Rest, TargetX = x, TargetY = y,
            PlayerDirected = true, ReviewTick = 10000 };
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
