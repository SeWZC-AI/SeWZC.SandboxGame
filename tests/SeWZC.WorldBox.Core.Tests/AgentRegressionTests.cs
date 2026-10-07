using SeWZC.WorldBox.Core;

internal static class AgentRegressionTests
{
    public static IEnumerable<(string Name, Action Run)> Cases =>
    [
        ("frontline orders wait for delivery to the commander", FrontlineOrderDelivery),
        ("command succession cannot roll back an accepted order", CommandSuccession),
        ("same-day commands follow their IDs across delayed delivery and saves", SameDayOrderDelivery),
        ("miners use adjacent mountain deposits after local ore is exhausted", ExhaustedMiningSites),
    ];

    private static void FrontlineOrderDelivery()
    {
        var engine = Flat();
        engine.SpawnResidents(10, 24, RaceKind.Human, 8);
        engine.SpawnResidents(50, 24, RaceKind.Elf, 3);
        var home = engine.State.Settlements[0];
        var enemy = engine.State.Settlements[1];
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
        Check(commander.Agent.Memory.Any(f => f.Kind == AgentFactKind.WarOrder && f.SubjectId == enemy.NationId
                                                                               && f.Id == army.LastOrderFactId)
              && army.LastOrderTick == 29,
            "Recruitment did not give the commander the local war order and its cursor.");

        Hold(commander, 32, 24);
        army.X = army.FromX = commander.X;
        army.Y = army.FromY = commander.Y;
        army.Gathering = false;
        engine.SetDiplomacy(home.NationId, enemy.NationId, DiplomaticStatus.Neutral);
        var peace =
            carrier.Agent.Memory.Single(f => f.Kind == AgentFactKind.PeaceOrder && f.SubjectId == enemy.NationId);
        Check(!commander.Agent.Memory.Any(f => f.Id == peace.Id),
            "The fixture informed the distant commander directly.");
        engine.Step();
        Check(!army.Retreating && army.KnownDiplomacy == DiplomaticStatus.War && army.LastOrderTick == 29,
            "A soldier still at the capital changed frontline orders without delivering them.");

        // 将传令者移到前线，检查交谈队列能否递送军令。
        Hold(carrier, commander.X - 1, commander.Y);
        PendingMessage? delivery = null;
        for (var tick = 0; tick < 12 && delivery is null; tick++)
        {
            engine.Step();
            Check(!army.Retreating && commander.Agent.Memory.All(f => f.Id != peace.Id),
                "The commander acted before the local conversation was delivered.");
            delivery = engine.State.PendingMessages.FirstOrDefault(m => m.SenderId == carrier.Id
                                                                        && m.RecipientId == commander.Id &&
                                                                        m.Facts.Any(f => f.Id == peace.Id));
        }

        Check(delivery is not null, "The nearby soldier never queued the peace order for the commander.");
        engine.Step();
        var received = commander.Agent.Memory.Single(f => f.Id == peace.Id);
        Check(received.SourceResidentId == carrier.Id && received.LearnedTick == engine.State.Tick
                                                      && received.ObservedTick == peace.ObservedTick && army.Retreating
                                                      && army.KnownDiplomacy == DiplomaticStatus.Neutral &&
                                                      army.LastOrderTick == peace.ObservedTick
                                                      && army.LastOrderFactId == peace.Id,
            "Actual delivery did not preserve provenance and change the commander's orders.");

        // 同时观察到的冲突军令仍按 ID 顺序处理。
        var olderPeace = Order(engine, commander, enemy.NationId, AgentFactKind.PeaceOrder);
        var latestWar = Order(engine, commander, enemy.NationId, AgentFactKind.WarOrder);
        commander.Agent.Memory.AddRange([olderPeace, latestWar]);
        engine.Step();
        Check(!army.Retreating && army.KnownDiplomacy == DiplomaticStatus.War
                               && army.LastOrderTick == latestWar.ObservedTick && army.LastOrderFactId == latestWar.Id,
            "Equal-time orders stopped preferring the latest fact ID.");
    }

    private static void CommandSuccession()
    {
        var (engine, army, home, enemy, soldiers) = ArmyFixture();
        var commander = soldiers[0];
        var successor = soldiers[1];
        var carrier = soldiers[2];
        FreezeAt(engine, commander, 32, 24);
        army.X = army.FromX = commander.X;
        army.Y = army.FromY = commander.Y;
        army.Gathering = false;
        FreezeAt(engine, successor, home.X, home.Y);
        FreezeAt(engine, carrier, home.X, home.Y);
        FreezeAt(engine, soldiers[3], 42, 40);
        engine.SetDiplomacy(home.NationId, enemy.NationId, DiplomaticStatus.Neutral);
        var oldPeace =
            successor.Agent.Memory.Single(f => f.Kind == AgentFactKind.PeaceOrder && f.SubjectId == enemy.NationId);
        engine.Step();
        FreezeAt(engine, successor, 26, 24);
        engine.SetDiplomacy(home.NationId, enemy.NationId, DiplomaticStatus.War);
        var newWar =
            carrier.Agent.Memory.Single(f => f.Kind == AgentFactKind.WarOrder && f.SubjectId == enemy.NationId);
        Check(newWar.ObservedTick > oldPeace.ObservedTick, "Succession fixture requires a newer accepted war order.");
        DeliverByConversation(engine, army, commander, carrier, newWar);
        Check(successor.Agent.Memory.All(f => f.Id != newWar.Id),
            "The separated successor should still know only the older order.");

        // 指挥官死亡后选择下一名存活士兵，保留军令游标。
        commander.Age = 91;
        commander.Health = .1;
        engine.ConfigureWorld(engine.State.Rules with { Aging = true }, false, false);
        var resumed = WorldEngine.ImportJson(engine.ExportJson());
        engine.Step();
        resumed.Step();
        Check(engine.State.ArchivedResidents.Any(r => r.Id == commander.Id) && army.CommanderId == successor.Id,
            "The commander did not die and pass command to the surviving soldier.");
        Check(army.LastOrderTick == newWar.ObservedTick && army.LastOrderFactId == newWar.Id
                                                        && army.KnownDiplomacy == DiplomaticStatus.War &&
                                                        !army.Retreating,
            "A successor's old peace order rolled back the army's accepted command.");
        Check(engine.ExportJson() == resumed.ExportJson(),
            "Command succession diverged after saving before the death.");
    }

    private static void SameDayOrderDelivery()
    {
        foreach (var first in new[] { DiplomaticStatus.Neutral, DiplomaticStatus.War })
        {
            var (engine, army, home, enemy, soldiers) = ArmyFixture();
            var commander = soldiers[0];
            FreezeAt(engine, commander, 32, 24);
            army.X = army.FromX = commander.X;
            army.Y = army.FromY = commander.Y;
            army.Gathering = false;
            for (var i = 1; i < soldiers.Length; i++)
                FreezeAt(engine, soldiers[i], 40 + (i - 1) * 8, 40);
            var opposite = first == DiplomaticStatus.War ? DiplomaticStatus.Neutral : DiplomaticStatus.War;
            var statuses = new[] { first, opposite, first };
            var orders = new AgentFact[statuses.Length];
            for (var i = 0; i < statuses.Length; i++)
            {
                var carrier = soldiers[i + 1];
                FreezeAt(engine, carrier, home.X, home.Y);
                engine.SetDiplomacy(home.NationId, enemy.NationId, statuses[i]);
                orders[i] = home.PublicKnowledge.Single(f => f.SubjectId == enemy.NationId
                                                             && f.Kind is AgentFactKind.WarOrder
                                                                 or AgentFactKind.PeaceOrder);
                Check(carrier.Agent.Memory.Any(f => f.Id == orders[i].Id),
                    "The carrier did not hear its order at the capital.");
                FreezeAt(engine, carrier, 40 + i * 8, 40);
            }

            Check(orders.Select(order => order.ObservedTick).Distinct().Count() == 1
                  && orders[0].Id < orders[1].Id && orders[1].Id < orders[2].Id,
                "The same-day orders did not retain their issuance order.");
            for (var i = 0; i < orders.Length; i++)
            {
                if (i > 0)
                    FreezeAt(engine, soldiers[i], 40 + (i - 1) * 8, 40);
                DeliverByConversation(engine, army, commander, soldiers[i + 1], orders[i]);
                Check(army.LastOrderFactId == orders[i].Id && army.LastOrderTick == orders[i].ObservedTick
                                                           && army.KnownDiplomacy == statuses[i] &&
                                                           army.Retreating == (statuses[i] != DiplomaticStatus.War),
                    "A later same-day order failed to supersede the earlier accepted order.");
                if (statuses[i] == DiplomaticStatus.War)
                {
                    Check(army.CampaignEventId == orders[i].CampaignEventId && army.Objective == orders[i].WarObjective,
                        "The new military order lost its campaign provenance or objective.");
                }
            }

            // 用新 ID 重复第一种军令，检查记忆合并。
            Check(commander.Agent.Memory.Any(f => f.Id == orders[2].Id)
                  && commander.Agent.Memory.All(f => f.Id != orders[0].Id),
                "An older same-kind order prevented retaining the newer relayed command.");
            var afterDelivery = WorldEngine.ImportJson(engine.ExportJson());
            engine.Step(3);
            afterDelivery.Step(3);
            Check(engine.ExportJson() == afterDelivery.ExportJson(),
                "The complete accepted command cursor was not preserved after delivery.");
        }
    }

    private static (WorldEngine Engine, Army Army, Settlement Home, Settlement Enemy, Resident[] Soldiers) ArmyFixture()
    {
        var engine = Flat();
        engine.SpawnResidents(10, 24, RaceKind.Human, 8);
        engine.SpawnResidents(50, 24, RaceKind.Elf, 3);
        foreach (var resident in engine.State.Residents)
        {
            var town = engine.State.Settlements.Single(t => t.Id == resident.SettlementId);
            Hold(resident, town.X, town.Y);
        }

        var home = engine.State.Settlements[0];
        var enemy = engine.State.Settlements[1];
        engine.State.Tick = 29;
        engine.SetDiplomacy(home.NationId, enemy.NationId, DiplomaticStatus.War);
        engine.Step();
        var army = engine.State.Armies.Single(a => a.NationId == home.NationId);
        var soldiers = engine.State.Residents.Where(r => r.ArmyId == army.Id).ToArray();
        Check(soldiers.Length == 4 && soldiers[0].Id == army.CommanderId,
            "The fixture must recruit a commander and three soldiers normally.");
        return (engine, army, home, enemy, soldiers);
    }

    private static void FreezeAt(WorldEngine engine, Resident resident, int x, int y)
    {
        Hold(resident, x, y);
        resident.MoveStartedTick = engine.State.Tick;
        resident.MoveDurationTicks = 100;
    }

    private static void DeliverByConversation(WorldEngine engine, Army army, Resident commander, Resident carrier,
        AgentFact order)
    {
        Check(commander.Agent.Memory.All(f => f.Id != order.Id),
            "The commander already knows the order before its carrier arrives.");
        FreezeAt(engine, carrier, commander.X - 1, commander.Y);
        var before = (army.LastOrderTick, army.LastOrderFactId);
        PendingMessage? pending = null;
        for (var tick = 0; tick < 12 && pending is null; tick++)
        {
            engine.Step();
            Check((army.LastOrderTick, army.LastOrderFactId) == before,
                "The army adopted an order before actual local delivery.");
            pending = engine.State.PendingMessages.FirstOrDefault(message => message.SenderId == carrier.Id
                                                                             && message.RecipientId == commander.Id &&
                                                                             message.Facts.Any(fact =>
                                                                                 fact.Id == order.Id));
        }

        Check(pending is not null, "The neighboring carrier never queued the command for delivery.");
        var resumed = WorldEngine.ImportJson(engine.ExportJson());
        engine.Step();
        resumed.Step();
        var received = commander.Agent.Memory.Single(f => f.Id == order.Id);
        Check(received.SourceResidentId == carrier.Id && received.ObservedTick == order.ObservedTick
                                                      && received.LearnedTick == engine.State.Tick &&
                                                      army.LastOrderFactId == order.Id,
            "The commander did not adopt the real delivered command with its original provenance.");
        Check(engine.ExportJson() == resumed.ExportJson(),
            "A pending command or accepted cursor diverged across save/load.");
    }

    private static AgentFact Order(WorldEngine engine, Resident commander, int enemyId, AgentFactKind kind)
    {
        return new AgentFact
        {
            Id = engine.State.NextId++,
            Kind = kind,
            SubjectId = enemyId,
            TargetNationId = commander.NationId,
            X = commander.X,
            Y = commander.Y,
            ObservedTick = engine.State.Tick,
            LearnedTick = engine.State.Tick,
            OriginResidentId = commander.Id,
            SourceResidentId = commander.Id,
            OriginProfession = commander.Profession,
        };
    }

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
            miner.Agent.Goal = new AgentGoal();
            miner.Agent.NextThinkTick = engine.State.Tick;
            var site = engine.State.Tiles[20 * engine.State.Width + 20];
            site.Terrain = terrain;
            site.ResourceAmount = localStock;
            var mountain = engine.State.Tiles[20 * engine.State.Width + 21];
            mountain.Terrain = TerrainType.Mountain;
            mountain.ResourceAmount = 100;

            engine.Step();
            Check(miner.Agent.Goal.Kind == AgentGoalKind.Work && miner.X == 20 && miner.Y == 20,
                "The miner did not choose the visible adjacent deposit from a walkable work site.");
            if (localStock > 0)
            {
                Check(site.ResourceAmount < localStock && mountain.ResourceAmount == 100
                                                       && Math.Abs(miner.Inventory.Stone + miner.Inventory.Ore -
                                                                   (localStock - site.ResourceAmount)) < 0.000001,
                    "The miner skipped the remaining local deposit before mining the adjacent mountain.");
            }
            else
            {
                Check(mountain.ResourceAmount < 100,
                    "An exhausted work site prevented mining the stocked adjacent mountain.");
            }

            engine.Step(5);
            var carried = miner.Inventory.Stone + miner.Inventory.Ore;
            Check(mountain.ResourceAmount < 100 && carried > localStock
                                                && Math.Abs(100 + localStock - site.ResourceAmount -
                                                            mountain.ResourceAmount - carried) < 0.000001,
                "Mining after depletion did not transfer actual deposit stock into personal inventory.");
        }
    }

    private static WorldEngine Flat()
    {
        var engine = WorldEngine.Create(223, 64, 64, false);
        foreach (var tile in engine.State.Tiles)
        {
            tile.Terrain = TerrainType.Grass;
            tile.Fertility = 0;
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
            Wars = false,
            Alliances = false,
            Peace = false,
            Migration = false,
            Secession = false,
        }, false, false);
        return engine;
    }

    private static void Hold(Resident person, int x, int y)
    {
        person.Age = 25;
        person.Inventory.Food = 1.2;
        person.X = person.FromX = x;
        person.Y = person.FromY = y;
        person.Agent.Goal = new AgentGoal
        {
            Kind = AgentGoalKind.Rest,
            TargetX = x,
            TargetY = y,
            PlayerDirected = true,
            ReviewTick = 10000,
        };
    }

    private static void Check(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }
}
