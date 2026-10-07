using SeWZC.WorldBox.Core;

internal static class TradeRegressionTests
{
    public static IEnumerable<(string Name, Action Run)> Cases =>
    [
        ("trade loading preserves local food after stale surplus reports", LocalReserve),
        ("trade exchanges only the food covered by actual payment and returns leftovers", PartialPayment),
        ("trade without payment keeps cargo and produces no diplomatic success", NoPayment),
        ("trade respects warehouse and carrier capacity without losing goods", Capacity),
        ("trade choices exclude enemies actually known to the merchant", KnownEnemy),
        ("trade cancellation waits for delivered hostility and keeps cargo", DeliveredHostility),
    ];

    private static (WorldEngine Engine, Resident Trader, Settlement Source, Settlement Destination) Fixture()
    {
        var fixture = AgentBehaviorTests.TradeWorld();
        fixture.Engine.ConfigureWorld(new WorldRules
        {
            Births = false,
            Aging = false,
            Hunger = false,
            Thirst = false,
            Disease = false,
            Construction = false,
            Research = false,
            Expansion = false,
            Wars = false,
            Alliances = false,
            Peace = false,
            Migration = false,
            Secession = false,
        }, false, false);
        foreach (var person in fixture.Engine.State.Residents)
        {
            person.Inventory.Food = 1.2;
            if (person == fixture.Trader)
                continue;
            var home = fixture.Engine.State.Settlements.Single(t => t.Id == person.SettlementId);
            person.X = person.FromX = person.Agent.Goal.TargetX = home.X;
            person.Y = person.FromY = person.Agent.Goal.TargetY = home.Y;
        }

        return fixture;
    }

    private static void LocalReserve()
    {
        foreach (var (food, population) in new[] { (10d, 3), (15d, 3), (27d, 24) })
        {
            var (engine, trader, source, destination) = Fixture();
            if (population > source.Population)
            {
                var previousIds = engine.State.Residents.Select(r => r.Id).ToHashSet();
                engine.SpawnResidents(source.X, source.Y, RaceKind.Human, population - source.Population);
                foreach (var person in engine.State.Residents.Where(r => !previousIds.Contains(r.Id)))
                {
                    person.Inventory.Food = 1.2;
                    person.Agent.Goal = new AgentGoal
                    {
                        Kind = AgentGoalKind.Rest,
                        TargetX = person.X,
                        TargetY = person.Y,
                        PlayerDirected = true,
                        ReviewTick = 1000,
                    };
                }
            }

            // 依据旧余粮报告出发，到达时仓库已耗尽。
            source.Resources.Food = food;
            trader.Agent.DestinationSettlementId = destination.Id;
            trader.Agent.MissionOriginSettlementId = source.Id;
            trader.Agent.Goal = new AgentGoal
            {
                Kind = AgentGoalKind.Trade,
                TargetSettlementId = destination.Id,
                TargetX = source.X,
                TargetY = source.Y,
                PlayerDirected = true,
                ReviewTick = 1000,
            };
            var total = Totals(engine);
            engine.Step();
            Check(source.Resources.Food >= Math.Min(food, Math.Max(12, population)),
                "A merchant loaded local subsistence food using stale surplus knowledge.");
            CheckTotals(engine, total);
            if (food == 10)
            {
                Check(trader.Agent.Goal.Kind == AgentGoalKind.ReturnHome && trader.Agent.Goal.Reason.Contains("余粮"),
                    "No-surplus cancellation lacks an observable reason.");
            }
        }
    }

    private static (WorldEngine Engine, Resident Trader, Settlement Source, Settlement Destination) AtExchange()
    {
        var fixture = Fixture();
        for (var tick = 0; tick < 200 && fixture.Trader.Agent.Goal.WorkTicks < 2; tick++)
            fixture.Engine.Step();
        Check(fixture.Trader.Agent.Goal.Kind == AgentGoalKind.Trade && fixture.Trader.Agent.Goal.WorkTicks == 2
                                                                    && Distance(fixture.Trader, fixture.Destination) <=
                                                                    1,
            "Merchant did not physically reach the exchange.");
        return fixture;
    }

    private static void PartialPayment()
    {
        var (engine, trader, source, destination) = AtExchange();
        destination.Resources.Wood = .8;
        var beforeFood = trader.Inventory.Food;
        var total = Totals(engine);
        var restored = WorldEngine.ImportJson(engine.ExportJson());
        engine.Step();
        restored.Step();
        Check(engine.ExportJson() == restored.ExportJson(), "An exchange resumed differently after save/load.");
        Check(Math.Abs(beforeFood - trader.Inventory.Food - 2) < 1e-8 && Math.Abs(trader.Inventory.Wood - .8) < 1e-8,
            "A partial payment bought more food than it paid for.");
        CheckTotals(engine, total);
        Check(source.PublicKnowledge.All(f => f.Kind != AgentFactKind.TradeExchange),
            "Trade success reached the source before its carrier.");
        var sourceFood = source.Resources.Food;
        var sourceWood = source.Resources.Wood;
        for (var tick = 0;
             tick < 150 && !(Distance(trader, source) <= 1 && trader.Inventory.Wood == 0);
             tick++)
            engine.Step();
        Check(source.Resources.Food > sourceFood && Math.Abs(source.Resources.Wood - sourceWood - .8) < 1e-8,
            "The merchant failed to bring unused cargo and payment back to the warehouse.");
        CheckTotals(engine, total);
    }

    private static void NoPayment()
    {
        var (engine, trader, _, destination) = AtExchange();
        destination.Resources.Wood = 0;
        var food = trader.Inventory.Food;
        var total = Totals(engine);
        engine.Step();
        Check(trader.Inventory.Food == food, "An insolvent destination received unpaid cargo.");
        Check(!engine.State.Events.Any(e => e.Kind == WorldEventKind.Trade)
              && !destination.PublicKnowledge.Any(f => f.Kind == AgentFactKind.TradeExchange)
              && !trader.Agent.Memory.Any(f => f.Kind == AgentFactKind.TradeExchange),
            "A failed exchange created diplomatic success evidence.");
        Check(trader.Agent.Goal.Kind == AgentGoalKind.ReturnHome && trader.Agent.Goal.Reason.Contains("木材"),
            "The merchant did not explain why it returned with its cargo.");
        CheckTotals(engine, total);
    }

    private static void Capacity()
    {
        foreach (var fullWarehouse in new[] { false, true })
        foreach (var freeSpace in new[] { 0d, 1d })
        {
            var (engine, trader, _, destination) = AtExchange();
            if (fullWarehouse)
                destination.Resources.Food = 1_000_000 - freeSpace;
            else
                trader.Inventory.Wood = 1_000_000 - freeSpace * .4;
            var food = trader.Inventory.Food;
            var wood = trader.Inventory.Wood;
            var total = Totals(engine);
            engine.Step();
            Check(Math.Abs(food - trader.Inventory.Food - freeSpace) < 1e-7
                  && Math.Abs(trader.Inventory.Wood - wood - freeSpace * .4) < 1e-7,
                "The exchange exceeded actual receiving capacity.");
            if (freeSpace == 0)
            {
                Check(!engine.State.Events.Any(e => e.Kind == WorldEventKind.Trade),
                    "A full receiver still generated a successful exchange.");
            }

            CheckTotals(engine, total);
            _ = WorldEngine.ImportJson(engine.ExportJson());
        }
    }

    private static void KnownEnemy()
    {
        var (engine, trader, source, destination) = Fixture();
        engine.SetDiplomacy(source.NationId, destination.NationId, DiplomaticStatus.War);
        Check(trader.Agent.Memory.Any(f => f.Kind == AgentFactKind.WarOrder),
            "Merchant did not hear the local declaration.");
        engine.Step();
        Check(trader.Agent.Goal.Kind != AgentGoalKind.Trade,
            "A merchant deliberately started trading with a known enemy.");
    }

    private static void DeliveredHostility()
    {
        var (engine, trader, source, destination) = Fixture();
        engine.Step(20);
        Check(Distance(trader, source) > 4 && trader.Agent.Goal.Kind == AgentGoalKind.Trade,
            "Merchant has not departed.");
        engine.SetDiplomacy(source.NationId, destination.NationId, DiplomaticStatus.War);
        engine.Step();
        Check(trader.Agent.Goal.Kind == AgentGoalKind.Trade,
            "Remote diplomacy changed the uninformed merchant's mission.");
        var order = source.PublicKnowledge.Single(f =>
            f.Kind == AgentFactKind.WarOrder && f.SubjectId == destination.NationId);
        var sender = engine.State.Residents.First(r => r.Id != trader.Id && r.SettlementId == source.Id);
        sender.X = sender.FromX = trader.X;
        sender.Y = sender.FromY = trader.Y;
        sender.Agent.Goal.TargetX = sender.X;
        sender.Agent.Goal.TargetY = sender.Y;
        // 通过交谈队列递送信息。
        var learned = false;
        for (var tick = 0; tick < 12 && !learned; tick++)
        {
            sender.X = sender.FromX = trader.X;
            sender.Y = sender.FromY = trader.Y;
            sender.Agent.Goal.TargetX = sender.X;
            sender.Agent.Goal.TargetY = sender.Y;
            engine.Step();
            learned = trader.Agent.Memory.Any(f => f.Id == order.Id);
        }

        Check(learned, "The traveling merchant did not receive the nearby messenger's order.");
        var cargo = trader.Inventory.Food;
        var restored = WorldEngine.ImportJson(engine.ExportJson());
        engine.Step();
        restored.Step();
        Check(engine.ExportJson() == restored.ExportJson(), "Delivered hostility changed save continuation.");
        Check(trader.Agent.Goal.Kind == AgentGoalKind.ReturnHome && trader.Inventory.Food == cargo
                                                                 && trader.Agent.Goal.Reason.Contains("敌对"),
            "Delivered hostility did not stop the mission with cargo intact.");
        Check(!engine.State.Events.Any(e => e.Kind == WorldEventKind.Trade), "Aborted trade created a delivery event.");
    }

    private static int Distance(Resident person, Settlement town)
    {
        return Math.Abs(person.X - town.X) + Math.Abs(person.Y - town.Y);
    }

    private static (double Food, double Wood) Totals(WorldEngine engine)
    {
        return (engine.State.Settlements.Sum(s => s.Resources.Food) + engine.State.Residents.Sum(r => r.Inventory.Food),
            engine.State.Settlements.Sum(s => s.Resources.Wood) + engine.State.Residents.Sum(r => r.Inventory.Wood));
    }

    private static void CheckTotals(WorldEngine engine, (double Food, double Wood) before)
    {
        var after = Totals(engine);
        Check(Math.Abs(after.Food - before.Food) < 1e-7 && Math.Abs(after.Wood - before.Wood) < 1e-7,
            "Trade created or destroyed food/wood instead of transferring actual inventory.");
    }

    private static void Check(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }
}
