using SeWZC.WorldBox.Core;

internal static class AgentBehaviorTests
{
    public static IEnumerable<(string Name, Action Run)> Cases =>
    [
        ("remote disasters are unknown without observation or delivery", RemoteKnowledge),
        ("oral facts preserve provenance and cannot jump twice in one tick", OralDelivery),
        ("oral queues preserve tied fact order, delivery order and independent snapshots", StableOralQueues),
        ("resource choices preserve score ties, visible radius and map edge behavior", ResourceChoiceBounds),
        ("local detours preserve direction ties across residents and save/resume", ReusableLocalDetours),
        ("harvested resources travel in a resident's physical inventory", PhysicalProduction),
        ("a trader carries cargo over distance and returns home", PhysicalTrade),
        ("fact age and confidence change a resident's chosen action", EvidenceQuality),
        ("a delivered petition changes the receiving institution's policy", DeliveredPetition),
    ];

    private static void RemoteKnowledge()
    {
        var engine = Flat();
        engine.SpawnResidents(10, 10, RaceKind.Human, 3);
        engine.SpawnResidents(50, 50, RaceKind.Elf, 3);
        HoldResidents(engine);
        var remoteTown = engine.State.Settlements[1];
        var locals = engine.State.Residents.Where(p => p.SettlementId == engine.State.Settlements[0].Id).ToArray();
        engine.TriggerDisaster(remoteTown.X, remoteTown.Y, DisasterKind.Fire, 4);
        engine.Step(10);
        Require(locals.All(p => p.Agent.Memory.All(f => f.Kind != AgentFactKind.Danger || f.X < 30)),
            "A resident learned of a distant fire without a physical communication path.");
        Require(engine.State.Residents.Where(p => p.SettlementId == remoteTown.Id)
                .Any(p => p.Agent.Memory.Any(f => f.Kind == AgentFactKind.Danger && f.OriginResidentId == p.Id)),
            "Nearby witnesses did not perceive the actual fire.");
    }

    private static void OralDelivery()
    {
        var engine = Flat();
        engine.SpawnResidents(10, 10, RaceKind.Human, 3);
        HoldResidents(engine);
        var people = engine.State.Residents.ToArray();
        PlaceAndHold(people[0], 10, 10);
        PlaceAndHold(people[1], 12, 10);
        PlaceAndHold(people[2], 14, 10);
        var fact = Fact(engine, people[0], AgentFactKind.ReliefRequest, engine.State.Settlements[0], 70);
        people[0].Agent.Memory.Add(fact);
        var queued = false;
        for (var i = 0; i < 20 && !queued; i++)
        {
            engine.Tick();
            queued = engine.State.PendingMessages.Any(m => m.SenderId == people[0].Id
                                                           && m.RecipientId == people[1].Id &&
                                                           m.Facts.Any(f => f.Id == fact.Id));
        }

        Require(queued, "The adjacent listener received no queued conversation.");
        Require(people[1].Agent.Memory.All(f => f.Id != fact.Id),
            "A queued oral message arrived during its sending tick.");
        engine.Tick();
        var heard = people[1].Agent.Memory.Single(f => f.Id == fact.Id);
        Require(heard.ObservedTick == fact.ObservedTick && heard.LearnedTick == engine.State.Tick
                                                        && heard.OriginResidentId == people[0].Id &&
                                                        heard.SourceResidentId == people[0].Id
                                                        && heard.OriginProfession == fact.OriginProfession &&
                                                        heard.Hops == 1,
            "The listener lost the original observation time, author, profession or source.");
        Require(people[2].Agent.Memory.All(f => f.Id != fact.Id), "A fact crossed two oral links in one tick.");
        Require(engine.State.PendingMessages.All(m => m.SenderId != people[1].Id || m.Facts.All(f => f.Id != fact.Id)),
            "A listener forwarded a fact in the same tick they learned it.");
        engine.Step(30);
        var relayed = people[2].Agent.Memory.FirstOrDefault(f => f.Id == fact.Id);
        Require(relayed is not null && relayed.ObservedTick == fact.ObservedTick
                                    && relayed.OriginResidentId == people[0].Id && relayed.Hops >= 2 &&
                                    relayed.Confidence < heard.Confidence,
            "The second oral hop did not preserve provenance and reduce source certainty.");
    }

    private static void StableOralQueues()
    {
        var engine = Flat();
        engine.SpawnResidents(10, 10, RaceKind.Human);
        HoldResidents(engine);
        foreach (var person in engine.State.Residents)
        {
            PlaceAndHold(person, 24, 24);
            person.Agent.Memory.Clear();
        }

        var sender = engine.State.Residents[0];
        var home = engine.State.Settlements.Single();
        var sendingTick = 12 - sender.Id % 12;
        engine.State.Tick = sendingTick - 1;
        var neighbors = engine.State.Residents.Where(p => p.Id != sender.Id).OrderBy(p => p.Id).ToArray();
        var recipient = neighbors[(sendingTick / 12 + sender.Id) % neighbors.Length];
        var facts = Enumerable.Range(0, 4).Select(i =>
        {
            var fact = Fact(engine, sender, AgentFactKind.Personal, home, i);
            fact.X = 24 + i;
            fact.Y = 24;
            return fact;
        }).ToArray();
        sender.Agent.Memory.AddRange(facts);
        var firstDelivery = Fact(engine, sender, AgentFactKind.Personal, home, 10);
        var secondDelivery = Fact(engine, sender, AgentFactKind.Personal, home, 20);
        firstDelivery.X = secondDelivery.X = 40;
        firstDelivery.Y = secondDelivery.Y = 40;
        var futureFirst = new PendingMessage
        {
            SenderId = sender.Id, RecipientId = recipient.Id, DeliverTick = sendingTick + 5, Facts = [],
        };
        var futureSecond = new PendingMessage
        {
            SenderId = sender.Id, RecipientId = recipient.Id, DeliverTick = sendingTick + 6, Facts = [],
        };
        engine.State.PendingMessages.AddRange([
            futureFirst,
            new PendingMessage
            {
                SenderId = sender.Id, RecipientId = recipient.Id, DeliverTick = sendingTick, Facts = [firstDelivery],
            },
            futureSecond,
            new PendingMessage
            {
                SenderId = sender.Id, RecipientId = recipient.Id, DeliverTick = sendingTick, Facts = [secondDelivery],
            },
        ]);
        // Collection order must not change the resident-ID ordering used to choose a listener.
        engine.State.Residents.Reverse();
        engine.Tick();
        Require(ReferenceEquals(engine.State.PendingMessages[0], futureFirst)
                && ReferenceEquals(engine.State.PendingMessages[1], futureSecond),
            "Compaction reordered future deliveries.");
        Require(recipient.Agent.Memory.Any(f => f.Id == firstDelivery.Id)
                && recipient.Agent.Memory.All(f => f.Id != secondDelivery.Id),
            "Equal-age deliveries changed their original order.");
        var message =
            engine.State.PendingMessages.Single(m => m.SenderId == sender.Id && m.DeliverTick == sendingTick + 1);
        Require(message.RecipientId == recipient.Id, "Collection order changed the selected adjacent listener.");
        Require(message.Facts.Select(f => f.Id).SequenceEqual(facts.Take(3).Select(f => f.Id)),
            "Equal-priority facts changed their stable memory order.");
        Require(message.Facts.All(f => !ReferenceEquals(f, facts.Single(original => original.Id == f.Id))),
            "A pending message shares a mutable fact with the speaker.");
        engine.Tick();
        foreach (var sent in message.Facts)
        {
            var heard = recipient.Agent.Memory.Single(f => f.Id == sent.Id);
            Require(!ReferenceEquals(heard, sent) && !ReferenceEquals(heard, facts.Single(f => f.Id == sent.Id))
                                                  && heard.LearnedTick == sendingTick + 1 && heard.Hops == 1,
                "A delivered memory shares its mutable snapshot or changes its delivery metadata.");
        }

        var resumed = WorldEngine.ImportJson(engine.ExportJson());
        // Move an existing listener after buckets have been used, then rebuild from live positions.
        PlaceAndHold(neighbors[0], 50, 50);
        PlaceAndHold(resumed.State.Residents.Single(p => p.Id == neighbors[0].Id), 50, 50);
        engine.Step(12);
        resumed.Step(12);
        Require(engine.ExportJson() == resumed.ExportJson(),
            "Reused communication buffers changed continuation after a move and save.");
    }

    private static void ResourceChoiceBounds()
    {
        void Choose(int x, int y, (int X, int Y, byte Fertility)[] resources, AgentGoalKind kind, int targetX,
            int targetY)
        {
            var engine = Flat();
            engine.State.Rules.Hunger = engine.State.Rules.Thirst = false;
            engine.SpawnResidents(10, 10, RaceKind.Human, 1);
            engine.State.Society.Buildings.Clear();
            foreach (var tile in engine.State.Tiles)
            {
                tile.Terrain = TerrainType.Floodplain;
                tile.Fertility = 0;
                tile.Wildlife = WildlifeKind.None;
                tile.WildlifePopulation = 0;
                tile.OtherWildlife = default;
                tile.Plants = new PlantCoverage { Grass = 1 };
            }

            foreach (var site in resources)
                engine.State.Tiles[site.Y * engine.State.Width + site.X].Fertility = site.Fertility;
            var person = engine.State.Residents.Single();
            person.Profession = Profession.Farmer;
            person.Age = 60;
            person.Inventory.Food = 1.2;
            person.Agent.Personality.Diligence = 1;
            person.X = person.FromX = x;
            person.Y = person.FromY = y;
            person.Agent.Goal = new AgentGoal();
            person.Agent.NextThinkTick = engine.State.Tick;
            person.Agent.Fatigue = person.Agent.SocialNeed = 0;
            engine.Tick();
            Require(
                person.Agent.Goal.Kind == kind && person.Agent.Goal.TargetX == targetX &&
                person.Agent.Goal.TargetY == targetY,
                $"Resource choice changed: {person.Agent.Goal.Kind} at {person.Agent.Goal.TargetX},{person.Agent.Goal.TargetY}; expected {kind} at {targetX},{targetY}.");
        }

        // Both sources are outside town land: half yield preserves a tie over two steps.
        Choose(20, 20, [(20, 20, 50), (20, 18, 100)], AgentGoalKind.Gather, 20, 18);
        Choose(0, 0, [(1, 0, 100), (0, 1, 100)], AgentGoalKind.Gather, 1, 0);
        // (6,1) is outside the radius-six circle even though its Manhattan distance is seven.
        Choose(20, 20, [(26, 21, 100)], AgentGoalKind.ReturnHome, 10, 10);
    }

    private static void PhysicalProduction()
    {
        var engine = Flat();
        engine.SpawnResidents(10, 10, RaceKind.Human, 1);
        var person = engine.State.Residents.Single();
        var home = engine.State.Settlements.Single();
        home.Resources.Wood = home.Resources.Stone = home.Resources.Ore = 0;
        person.Profession = Profession.Lumberjack;
        person.Age = 60;
        person.Inventory.Food = 10;
        var resource = engine.State.Tiles[20 * engine.State.Width + 28];
        resource.Terrain = TerrainType.Forest;
        resource.Plants = new PlantCoverage { Trees = 1 };
        PlaceAndHold(person, 28, 20, AgentGoalKind.Work);
        var naturalStock = resource.ResourceAmount;
        engine.Tick();
        var carried = person.Inventory.Wood;
        Require(carried > 0 && Math.Abs(naturalStock - resource.ResourceAmount - carried) < 0.000001,
            "Harvest did not transfer the actual local deposit into personal inventory.");
        Require(home.Resources.Wood == 0, "Remote production appeared directly in the village warehouse.");
        person.Agent.Goal = new AgentGoal
        {
            Kind = AgentGoalKind.ReturnHome,
            TargetX = home.X,
            TargetY = home.Y,
            TargetSettlementId = home.Id,
            StartedTick = engine.State.Tick,
            ReviewTick = 1000,
            PlayerDirected = true,
        };
        for (var step = 0;
             step < 100 && (Distance(person, home) > 1 ||
                            engine.State.Tick - person.MoveStartedTick < person.MoveDurationTicks);
             step++)
        {
            Require(home.Resources.Wood == 0, "Wood arrived before its carrier.");
            engine.Tick();
        }

        engine.Tick();
        Require(Distance(person, home) <= 1 && Math.Abs(home.Resources.Wood - carried) < 0.000001
                                            && person.Inventory.Wood == 0,
            "The carrier failed to deposit exactly the harvested wood after reaching home.");
    }

    public static (WorldEngine Engine, Resident Trader, Settlement Source, Settlement Destination) TradeWorld()
    {
        var engine = Flat();
        engine.SpawnResidents(12, 32, RaceKind.Human, 3);
        engine.SpawnResidents(44, 32, RaceKind.Elf, 3);
        HoldResidents(engine);
        var source = engine.State.Settlements[0];
        var destination = engine.State.Settlements[1];
        source.Resources.Food = 2000;
        destination.Resources.Food = 10;
        destination.Resources.Wood = 100;
        var trader = engine.State.Residents.First(p => p.SettlementId == source.Id);
        PrepareTraderKnowledge(engine, trader, source, destination, 1, engine.State.Tick);
        return (engine, trader, source, destination);
    }

    private static void PhysicalTrade()
    {
        var (engine, trader, source, destination) = TradeWorld();
        engine.Step(12);
        Require(trader.Agent.Goal.Kind == AgentGoalKind.Trade && trader.Inventory.Food > 4
                                                              && trader.Agent.DestinationSettlementId == destination.Id,
            "Loading a trader's cargo caused the delivery mission to be interrupted.");
        Require(engine.State.Events.All(e => e.Kind != WorldEventKind.Trade),
            "Goods arrived before a distant trader could walk there.");
        var delivered = false;
        var returned = false;
        for (var step = 0; step < 220; step++)
        {
            engine.Tick();
            if (!delivered && engine.State.Events.Any(e => e.Kind == WorldEventKind.Trade))
            {
                Require(Distance(trader, destination) <= 1,
                    "A trade event happened while the carrier was away from the recipient.");
                Require(trader.Inventory.Wood > 0,
                    "The delivered food was not exchanged for physically carried payment.");
                delivered = true;
            }

            if (delivered && Distance(trader, source) <= 1 && trader.Inventory.Wood == 0)
            {
                returned = true;
                break;
            }
        }

        Require(delivered && returned, "The merchant did not complete the delivery and physical return journey.");
    }

    private static void EvidenceQuality()
    {
        static AgentGoalKind Goal(double confidence, long observed)
        {
            var (engine, trader, source, destination) = TradeWorld();
            engine.State.Tick = 240;
            PrepareTraderKnowledge(engine, trader, source, destination, confidence, observed);
            engine.Tick();
            return trader.Agent.Goal.Kind;
        }

        Require(Goal(1, 240) == AgentGoalKind.Trade,
            "Reliable fresh supply observations did not support a trade mission.");
        Require(Goal(0.1, 240) != AgentGoalKind.Trade, "Lowering confidence did not change the trade decision.");
        Require(Goal(1, 0) != AgentGoalKind.Trade, "An obsolete supply observation was treated as fresh knowledge.");
    }

    private static void DeliveredPetition()
    {
        var (engine, representative, source, capital) = TradeWorld();
        engine.TransferTerritory(source.X, source.Y, capital.NationId, 0);
        representative.Profession = Profession.Representative;
        source.Resources.Food = 0;
        capital.Resources.Food = 2000;
        representative.Inventory.Food = 20;
        representative.Agent.Goal = new AgentGoal();
        representative.Agent.NextThinkTick = engine.State.Tick;
        representative.Agent.Memory.Clear();
        representative.Agent.Memory.Add(Fact(engine, representative, AgentFactKind.SettlementLocation, source,
            source.NationId));
        representative.Agent.Memory.Add(Fact(engine, representative, AgentFactKind.SettlementLocation, capital,
            capital.NationId));
        var witness = engine.State.Residents.First(p => p.Id != representative.Id && p.SettlementId == source.Id);
        witness.Hunger = 70;
        var request = Fact(engine, witness, AgentFactKind.ReliefRequest, source, witness.Hunger);
        representative.Agent.Memory.Add(request);
        engine.Step(20);
        Require(engine.State.Society.Reports.All(r => r.RecipientSettlementId != capital.Id || r.FactId != request.Id),
            "A distant institution received a petition before its representative arrived.");
        var arrived = false;
        for (var step = 0; step < 150; step++)
        {
            engine.Tick();
            var report = engine.State.Society.Reports.FirstOrDefault(r =>
                r.RecipientSettlementId == capital.Id && r.FactId == request.Id);
            if (report is null) continue;
            Require(report.ObservedTick == request.ObservedTick && report.ReceivedTick > report.ObservedTick,
                "Delivered opinion lost its original observation time.");
            arrived = true;
            break;
        }

        Require(arrived, "The representative never delivered the physical petition.");
        engine.Step(31);
        Require(engine.GetLocalPolicy(capital.Id) == PolicyKind.FoodSecurity,
            "The receiving institution failed to act on the delivered hunger report.");
    }

    private static void PrepareTraderKnowledge(WorldEngine engine, Resident person, Settlement source,
        Settlement destination, double confidence, long observed)
    {
        person.Profession = Profession.Trader;
        person.Age = 60;
        person.X = person.FromX = source.X;
        person.Y = person.FromY = source.Y;
        person.Agent.Goal = new AgentGoal();
        person.Agent.NextThinkTick = engine.State.Tick;
        person.Agent.MissionRetryTick = 0;
        person.Agent.DestinationSettlementId = 0;
        person.Agent.Personality.Ambition = 1;
        person.Agent.Fatigue = person.Agent.SocialNeed = 0;
        person.Inventory.Food = 1.2;
        person.Agent.Memory.Clear();
        person.Agent.Memory.Add(Fact(engine, person, AgentFactKind.SettlementLocation, source, source.NationId));
        person.Agent.Memory.Add(Fact(engine, person, AgentFactKind.FoodSupply, source, source.Resources.Food));
        person.Agent.Memory.Add(Fact(engine, person, AgentFactKind.SettlementLocation, destination,
            destination.NationId));
        var food = Fact(engine, person, AgentFactKind.FoodSupply, destination, destination.Resources.Food);
        food.Confidence = confidence;
        food.ObservedTick = observed;
        food.LearnedTick = observed;
        person.Agent.Memory.Add(food);
    }

    private static void ReusableLocalDetours()
    {
        var engine = Flat();
        engine.SpawnResidents(10, 10, RaceKind.Human, 3);
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
            Wars = false,
            Migration = false,
        }, false, false);
        HoldResidents(engine);
        var travelers = engine.State.Residents.Take(2).ToArray();
        foreach (var traveler in travelers)
        {
            PlaceAndHold(traveler, 16, 20, AgentGoalKind.Flee);
            traveler.Agent.Goal.TargetX = 24;
            traveler.Agent.Goal.TargetY = 20;
        }

        for (var y = 19; y <= 21; y++) engine.State.Tiles[y * engine.State.Width + 17].Terrain = TerrainType.DeepWater;
        engine.Step();
        Require(travelers.All(r => r.X == 16 && r.Y == 21),
            "Equal-length local detours changed the original direction tie.");
        var resumed = WorldEngine.ImportJson(engine.ExportJson());
        for (var i = 0; i < 24; i++)
        {
            engine.Step();
            resumed.Step();
            Require(travelers[0].X == travelers[1].X && travelers[0].Y == travelers[1].Y,
                "One resident's search marks changed the following resident's route.");
        }

        Require(travelers.All(r => r.X == 24 && r.Y == 20),
            "Residents failed to walk around a visible three-cell barrier.");
        Require(engine.ExportJson() == resumed.ExportJson(),
            "Unsaved navigation buffers changed subsequent simulation.");

        var blocked = Flat();
        blocked.SpawnResidents(10, 10, RaceKind.Human, 3);
        blocked.ConfigureWorld(engine.State.Rules, false, false);
        HoldResidents(blocked);
        var travelerAtEdge = blocked.State.Residents[0];
        PlaceAndHold(travelerAtEdge, 0, 20, AgentGoalKind.Flee);
        travelerAtEdge.Agent.Goal.TargetX = 8;
        travelerAtEdge.Agent.Goal.TargetY = 20;
        for (var y = 0; y < blocked.State.Height; y++)
            blocked.State.Tiles[y * blocked.State.Width + 1].Terrain = TerrainType.DeepWater;
        blocked.Step();
        Require(travelerAtEdge.X == 0 && travelerAtEdge.Y == 21, "The map-edge fallback changed a tied direction.");
    }

    private static WorldEngine Flat()
    {
        var engine = WorldEngine.Create(223, 64, 64, false);
        engine.State.NaturalDisasters = false;
        foreach (var tile in engine.State.Tiles)
        {
            tile.Terrain = TerrainType.Grass;
            tile.Fertility = 80;
            tile.ResourceAmount = 100;
        }

        return engine;
    }

    private static void HoldResidents(WorldEngine engine)
    {
        foreach (var person in engine.State.Residents)
        {
            person.Age = 60;
            person.Inventory.Food = 20;
            PlaceAndHold(person, person.X, person.Y);
        }
    }

    private static void PlaceAndHold(Resident person, int x, int y, AgentGoalKind kind = AgentGoalKind.Rest)
    {
        person.X = person.FromX = x;
        person.Y = person.FromY = y;
        person.Agent.Goal = new AgentGoal
        {
            Kind = kind,
            TargetX = x,
            TargetY = y,
            ReviewTick = 1000,
            PlayerDirected = true,
        };
    }

    private static AgentFact Fact(WorldEngine engine, Resident observer, AgentFactKind kind, Settlement subject,
        double value)
    {
        return new AgentFact
        {
            Id = engine.State.NextId++,
            Kind = kind,
            SubjectId = subject.Id,
            X = subject.X,
            Y = subject.Y,
            Value = value,
            ObservedTick = engine.State.Tick,
            LearnedTick = engine.State.Tick,
            OriginResidentId = observer.Id,
            SourceResidentId = observer.Id,
            OriginProfession = observer.Profession,
            Confidence = 1,
            Text = "Controlled prior observation",
        };
    }

    private static int Distance(Resident resident, Settlement town)
    {
        return Math.Abs(resident.X - town.X) + Math.Abs(resident.Y - town.Y);
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
