namespace SeWZC.WorldBox.Core.Tests;

/// <summary>自主评估错峰与供水等待的即时边界。</summary>
public sealed class AgentCadenceTests
{
    /// <summary>返乡休息和等粮不能在非评估日延后已到期的复评，下一错峰日仍会重新决策。</summary>
    [Theory]
    [InlineData(AgentGoalKind.ReturnHome, ResidentActivity.Resting)]
    [InlineData(AgentGoalKind.Eat, ResidentActivity.Eating)]
    public void Arrived_waiting_goal_keeps_its_due_review_until_the_scheduled_day(
        AgentGoalKind kind, ResidentActivity activity)
    {
        var fixture = Prepare();
        var due = fixture.Engine.SimulationTick + 1;
        fixture.Resident.Replace(fixture.Resident.Value.WithActivity(activity));
        fixture.Resident.Replace(fixture.Resident.Value.WithInventory(new ResourceStock { Food = 1, Water = 1 }));
        fixture.Resident.Replace(fixture.Resident.Value.WithAgent(fixture.Resident.Value.Agent with { NextThinkTick = due }));
        fixture.Resident.Replace(fixture.Resident.Value.WithAgent(fixture.Resident.Value.Agent with
        {
            Goal = new AgentGoal
            {
                Kind = kind,
                TargetX = 16,
                TargetY = 16,
                WorkTicks = 2,
                ReviewTick = due,
            },
        }));

        fixture.Engine.Step();

        Assert.Empty(fixture.Resident.Value.Agent.Decisions);
        Assert.Equal(due, fixture.Resident.Value.Agent.NextThinkTick);

        fixture.Engine.Step(3);

        Assert.NotEmpty(fixture.Resident.Value.Agent.Decisions);
    }

    /// <summary>体力充足时仍持续消耗睡眠，原目标及其他认知保持不变。</summary>
    [Theory]
    [InlineData(AgentGoalKind.Rest, ResidentActivity.Resting)]
    [InlineData(AgentGoalKind.Socialize, ResidentActivity.Talking)]
    public void Recovered_resident_consumes_sleep_without_changing_the_goal(AgentGoalKind kind, ResidentActivity activity)
    {
        var fixture = Prepare();
        fixture.Resident.Replace(fixture.Resident.Value.WithActivity(activity));
        fixture.Resident.Replace(fixture.Resident.Value.WithAgent(fixture.Resident.Value.Agent with
        {
            Goal = new AgentGoal
            {
                Kind = kind,
                TargetX = 16,
                TargetY = 16,
                PlayerDirected = true,
                ReviewTick = 100,
            },
        }));
        var before = fixture.Resident.Value;

        fixture.Engine.Step();

        var after = fixture.Resident.Value;
        Assert.True(after.Agent.Sleep < before.Agent.Sleep);
        Assert.Equal(before.Agent with { Sleep = after.Agent.Sleep }, after.Agent);
        Assert.Same(before.Agent.Goal, after.Agent.Goal);
        Assert.True(fixture.Resident.Value.Age > before.Age);
        Assert.True(fixture.Resident.Value.Inventory.Food < before.Inventory.Food);
    }

    /// <summary>同格多人交谈只选择其他居民，且新消息必须等到下一日递送。</summary>
    [Fact]
    public void Crowded_cell_conversation_excludes_sender_and_delays_delivery()
    {
        var fixture = Prepare();
        fixture.Engine.SpawnResidents(16, 16, RaceKind.Human, 2);
        fixture.Engine.SimulationTick = 11;
        foreach (var person in fixture.Engine.Residents)
            person.Replace(person.Value with
            {
                X = 16,
                Y = 16,
                FromX = 16,
                FromY = 16,
                Inventory = new ResourceStock { Food = 10, Water = 10 },
                Agent = person.Value.Agent with
                {
                    Initialized = true,
                    NextThinkTick = 100,
                    Goal = new AgentGoal { Kind = AgentGoalKind.Rest, PlayerDirected = true, ReviewTick = 100 },
                },
            });
        fixture.Resident.Replace(fixture.Resident.Value.WithAgent(fixture.Resident.Value.Agent with
        {
            Memory = fixture.Resident.Value.Agent.Memory.Add(new AgentFact
            {
                Id = 90_001,
                Kind = AgentFactKind.SettlementLocation,
                SubjectId = fixture.Town.Value.Id,
                X = 16,
                Y = 16,
                ObservedTick = 1,
                LearnedTick = 1,
                OriginResidentId = fixture.ResidentId,
                SourceResidentId = fixture.ResidentId,
                Confidence = 1,
                Text = "亲眼见到家园",
            }),
        }));

        fixture.Engine.Step();

        var message = Assert.Single(fixture.Engine.State.PendingMessages);
        Assert.Equal(fixture.ResidentId, message.SenderId);
        Assert.NotEqual(message.SenderId, message.RecipientId);
        Assert.Equal(13, message.DeliverTick);
        var fact = Assert.Single(message.Facts, fact => fact.Id == 90_001);
        Assert.Equal(1, fact.ObservedTick);
        Assert.Equal(fixture.ResidentId, fact.OriginResidentId);
        Assert.DoesNotContain(fixture.Engine.State.Residents.Single(p => p.Id == message.RecipientId).Agent.Memory,
            remembered => remembered.Id == fact.Id);
    }

    /// <summary>持续缺粮时保留刚评估的寻粮目标，身体需求仍结算但不每天重新决策。</summary>
    [Fact]
    public void Ongoing_crisis_preserves_a_recent_survival_review()
    {
        var fixture = Prepare();
        fixture.Town.Replace(fixture.Town.Value.WithResources(new ResourceStock()));
        fixture.Resident.Replace(fixture.Resident.Value.WithInventory(new ResourceStock { Water = 10 }));
        fixture.Resident.Replace(fixture.Resident.Value.WithHunger(90));
        fixture.Resident.Replace(fixture.Resident.Value.WithAgent(fixture.Resident.Value.Agent with { NextThinkTick = fixture.Engine.SimulationTick + 4 }));
        fixture.Resident.Replace(fixture.Resident.Value.WithAgent(fixture.Resident.Value.Agent with
        {
            Goal = new AgentGoal
            {
                Kind = AgentGoalKind.Gather, TargetX = 16, TargetY = 16, ReviewTick = fixture.Engine.SimulationTick + 4,
            },
        }));
        fixture.Resident.Replace(fixture.Resident.Value with { FrozenUntilTick = fixture.Engine.SimulationTick + 2 });

        fixture.Engine.Step();

        Assert.Empty(fixture.Resident.Value.Agent.Decisions);
        Assert.Equal(AgentGoalKind.Gather, fixture.Resident.Value.Agent.Goal.Kind);
        Assert.True(fixture.Resident.Value.Hunger > 90);
    }

    /// <summary>紧急判断立即执行，首次复评对齐个人四日错峰，保存恢复保留后续安排。</summary>
    [Fact]
    public void Survival_reviews_are_spread_without_delaying_the_initial_emergency()
    {
        var fixture = Prepare();
        fixture.Engine.SpawnResidents(16, 16, RaceKind.Human, 5);
        fixture.Town.Replace(fixture.Town.Value.WithResources(new ResourceStock()));
        foreach (var person in fixture.Engine.Residents)
            person.Replace(person.Value with
            {
                Hunger = 90,
                Inventory = new ResourceStock { Water = 10 },
                FrozenUntilTick = fixture.Engine.SimulationTick + 6,
                Agent = person.Value.Agent with { Initialized = true, NextThinkTick = 100, Goal = new AgentGoal() },
            });

        fixture.Engine.Step();

        var intervals = fixture.Engine.State.Residents.Select(person =>
        {
            Assert.Equal(fixture.Engine.State.Tick, person.Agent.Goal.StartedTick);
            Assert.Equal(person.Agent.NextThinkTick, person.Agent.Goal.ReviewTick);
            Assert.Equal(0, (person.Agent.NextThinkTick + person.Id) % 4);
            return person.Agent.NextThinkTick - fixture.Engine.State.Tick;
        }).Distinct().Order().ToArray();
        Assert.Equal([3, 4, 5, 6], intervals);
        var restored = WorldEngine.ImportJson(fixture.Engine.ExportJson());
        Assert.Equal(fixture.Engine.State.Residents.Select(person => person.Agent.NextThinkTick),
            restored.State.Residents.Select(person => person.Agent.NextThinkTick));
        var due = fixture.Resident.Value.Agent.NextThinkTick;
        var days = (int)(due - fixture.Engine.State.Tick);
        fixture.Engine.Step(days);
        restored.Step(days);
        Assert.InRange(fixture.Resident.Value.Agent.NextThinkTick - due, 4, 24);
        Assert.Equal(fixture.Engine.ExportJson(), restored.ExportJson());
    }

    /// <summary>已在有效采食且携水够用时，不将普通地块不能打水误判为新危机。</summary>
    [Theory]
    [InlineData(100, true)]
    [InlineData(5, false)]
    public void Productive_food_task_continues_with_carried_water(byte fertility, bool productive)
    {
        var fixture = Prepare();
        fixture.Town.Replace(fixture.Town.Value.WithResources(new ResourceStock()));
        fixture.Resident.Replace(fixture.Resident.Value.WithInventory(new ResourceStock { Water = 1 }));
        fixture.Resident.Replace(fixture.Resident.Value.WithHunger(90));
        var tile = fixture.Engine.Tiles[16 * 32 + 16];
        tile.Replace(tile.Value with
        {
            NaturalWaterYield = .025,
            DroughtTicks = 0,
            ResourceAmount = 100,
            Fertility = fertility,
            Plants = new PlantCoverage { Grass = 1 },
        });
        fixture.Resident.Replace(fixture.Resident.Value.WithAgent(fixture.Resident.Value.Agent with
        {
            Goal = new AgentGoal
            {
                Kind = AgentGoalKind.Gather, TargetX = 16, TargetY = 16, StartedTick = fixture.Engine.SimulationTick - 4,
            },
        }));

        fixture.Engine.Step();

        Assert.InRange(fixture.Resident.Value.Thirst, 0, ResidentNeedsRules.DrinkThreshold);
        Assert.Equal(0, fixture.Engine.AvailableWater(16, 16), 8);
        if (productive)
        {
            Assert.Equal(AgentGoalKind.Gather, fixture.Resident.Value.Agent.Goal.Kind);
            Assert.Empty(fixture.Resident.Value.Agent.Decisions);
            Assert.Equal(fixture.Engine.SimulationTick + 24, fixture.Resident.Value.Agent.NextThinkTick);
        }
        else
        {
            Assert.NotEmpty(fixture.Resident.Value.Agent.Decisions);
            Assert.NotEqual(fixture.Engine.SimulationTick + 24, fixture.Resident.Value.Agent.NextThinkTick);
        }
    }

    /// <summary>到场等粮的紧急任务也按个人日期复评，现场查看粮仓不能重新聚集评估日期。</summary>
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void Arrived_food_crisis_keeps_the_personal_review_phase(int offset)
    {
        var fixture = Prepare();
        fixture.Engine.SimulationTick += offset;
        fixture.Town.Replace(fixture.Town.Value.WithResources(new ResourceStock()));
        fixture.Resident.Replace(fixture.Resident.Value.WithInventory(new ResourceStock { Water = 10 }));
        fixture.Resident.Replace(fixture.Resident.Value.WithHunger(90));
        fixture.Resident.Replace(fixture.Resident.Value.WithAgent(fixture.Resident.Value.Agent with { NextThinkTick = 100 }));
        fixture.Resident.Replace(fixture.Resident.Value.WithAgent(fixture.Resident.Value.Agent with
        {
            Goal = new AgentGoal
            {
                Kind = AgentGoalKind.Eat, TargetX = 16, TargetY = 16, ReviewTick = 100,
            },
        }));

        fixture.Engine.Step();

        Assert.Equal(ResidentActivity.Eating, fixture.Resident.Value.Activity);
        Assert.InRange(fixture.Resident.Value.Agent.NextThinkTick - fixture.Engine.State.Tick, 3, 6);
        Assert.Equal(0, (fixture.Resident.Value.Agent.NextThinkTick + fixture.ResidentId) % 4);
        Assert.Equal(fixture.Resident.Value.Agent.NextThinkTick, fixture.Resident.Value.Agent.Goal.ReviewTick);
    }

    /// <summary>非评估日的普通空闲居民继续结算需求，保留尚未评估的目标。</summary>
    [Fact]
    public void Ordinary_idle_waits_for_its_review_day()
    {
        var fixture = Prepare();
        var before = fixture.Engine.State.Residents[0];

        fixture.Engine.Step();

        var after = fixture.Engine.State.Residents[0];
        Assert.Equal(AgentGoalKind.Idle, after.Agent.Goal.Kind);
        Assert.True(after.Age > before.Age);
        Assert.True(after.Inventory.Food < before.Inventory.Food);
    }

    /// <summary>严重口渴立即选择实际可达水源，不等待普通评估日。</summary>
    [Fact]
    public void Critical_thirst_overrides_the_review_schedule()
    {
        var fixture = Prepare();
        foreach (var tile in fixture.Engine.Tiles)
            tile.Replace(tile.Value.WithNaturalWaterYield(0));
        fixture.Town.Replace(fixture.Town.Value.WithResources(new ResourceStock { Food = 40 }));
        fixture.Resident.Replace(fixture.Resident.Value.WithInventory(new ResourceStock { Food = 10 }));
        fixture.Resident.Replace(fixture.Resident.Value.WithThirst(90));
        fixture.Resident.Replace(fixture.Resident.Value.WithAgent(fixture.Resident.Value.Agent with { NextThinkTick = 100 }));
        fixture.Resident.Replace(fixture.Resident.Value with { FrozenUntilTick = fixture.Engine.SimulationTick + 2 });
        var source = 17 * 32 + 16;
        fixture.AddWell(16, 17, .1);

        fixture.Engine.Step();

        Assert.Equal(AgentGoalKind.FetchWater, fixture.Resident.Value.Agent.Goal.Kind);
        Assert.Equal(source + 1, fixture.Resident.Value.Agent.Goal.TargetEntityId);
        Assert.Equal(16, fixture.Resident.Value.X);
        Assert.Equal(16, fixture.Resident.Value.Y);
    }

    /// <summary>每日额度耗尽时保留会恢复供水的已知水源，不转向或反复请求评估。</summary>
    [Fact]
    public void Exhausted_daily_water_quota_preserves_the_known_source()
    {
        var fixture = new WorldFixture();
        fixture.Engine.SimulationTick = 1;
        fixture.Resident.Replace(fixture.Resident.Value with { X = 16 });
        fixture.Resident.Replace(fixture.Resident.Value with { Y = 17 });
        fixture.Resident.Replace(fixture.Resident.Value.WithInventory(new ResourceStock()));
        fixture.Resident.Replace(fixture.Resident.Value.WithAgent(fixture.Resident.Value.Agent with { ExplorationHeading = 2 }));
        fixture.Resident.Replace(fixture.Resident.Value.WithAgent(fixture.Resident.Value.Agent with { NextThinkTick = 37 }));
        var source = 17 * 32 + 16;
        fixture.Resident.Replace(fixture.Resident.Value.WithAgent(fixture.Resident.Value.Agent with
        {
            Goal = new AgentGoal
            {
                Kind = AgentGoalKind.FetchWater,
                TargetX = 16,
                TargetY = 17,
                TargetEntityId = source + 1,
                WorkTicks = 3,
            },
        }));
        var tile = fixture.Engine.Tiles[source];
        fixture.AddWell(16, 17, .025);
        tile.Replace(tile.Value with { WaterDrawTick = 1, WaterDrawn = fixture.Engine.GetDailyWaterCapacity(16, 17) });

        Assert.False(fixture.Engine.TryFetchWater(fixture.Resident.Value));

        Assert.Equal(2, fixture.Resident.Value.Agent.ExplorationHeading);
        Assert.Equal(37, fixture.Resident.Value.Agent.NextThinkTick);
        Assert.Equal(.15, tile.Value.WaterDrawn, 8);
        Assert.Equal(0, fixture.Resident.Value.Inventory.Water);
    }

    /// <summary>补个人储备与公共运水的任务在装满后结束，后者需实际返仓。</summary>
    [Theory]
    [InlineData(101, .75, AgentGoalKind.Idle)]
    [InlineData(100, 3.75, AgentGoalKind.ReturnHome)]
    public void Water_collection_finishes_at_the_respective_carry_target(
        int id, double target, AgentGoalKind completedGoal)
    {
        var fixture = new WorldFixture();
        fixture.Engine.NextId = id;
        fixture.Engine.SpawnResidents(16, 16, RaceKind.Human, 1);
        var person = fixture.Engine.RequireResident(id);
        fixture.Engine.SimulationTick = 1;
        person.Replace(person.Value with
        {
            X = 16,
            Y = 17,
            FromX = 16,
            FromY = 17,
            Inventory = new ResourceStock { Water = target - .1 },
            Agent = person.Value.Agent with
            {
                Goal = new AgentGoal
                {
                    Kind = AgentGoalKind.FetchWater,
                    TargetX = 16,
                    TargetY = 17,
                    TargetEntityId = 17 * 32 + 16 + 1,
                    ReviewTick = 40,
                },
                NextThinkTick = 40,
            },
        });
        fixture.AddWell(16, 17, .1);
        var before = fixture.Engine.State;

        Assert.True(fixture.Engine.TryFetchWater(person.Value));

        Assert.Equal(target, person.Value.Inventory.Water, 8);
        Assert.Equal(completedGoal, person.Value.Agent.Goal.Kind);
        Assert.Equal(1, person.Value.Agent.NextThinkTick);
        Assert.Equal(.1, fixture.Engine.Tiles[17 * 32 + 16].Value.WaterDrawn, 8);
        Assert.Equal(target - .1, before.Residents.Single(resident => resident.Id == id).Inventory.Water, 8);
        Assert.Equal(AgentGoalKind.FetchWater, before.Residents.Single(resident => resident.Id == id).Agent.Goal.Kind);
    }

    /// <summary>到场取水逐日结算，不受普通生产的四日轮班限制；在途仍不能取水。</summary>
    [Theory]
    [InlineData(0, true)]
    [InlineData(1, false)]
    public void Water_collection_runs_daily_after_arrival(int movementStarted, bool draws)
    {
        var fixture = Prepare();
        fixture.Engine.SimulationTick = 8 + (4 - fixture.ResidentId % 4) % 4;
        fixture.Town.Replace(fixture.Town.Value.WithResources(new ResourceStock()));
        fixture.Resident.Replace(fixture.Resident.Value with
        {
            X = 16,
            Y = 17,
            FromX = 16,
            FromY = 17,
            Age = 20,
            MoveStartedTick = movementStarted == 0 ? 0 : fixture.Engine.SimulationTick,
            MoveDurationTicks = movementStarted == 0 ? 1 : 3,
            Inventory = new ResourceStock { Food = 10, Water = .1 },
            Agent = fixture.Resident.Value.Agent with
            {
                NextThinkTick = 40,
                Goal = new AgentGoal
                {
                    Kind = AgentGoalKind.FetchWater,
                    TargetX = 16,
                    TargetY = 17,
                    TargetEntityId = 17 * 32 + 16 + 1,
                    ReviewTick = 40,
                },
            },
        });
        fixture.AddWell(16, 17, .03);

        fixture.Engine.Step();

        Assert.Equal(.1 + (draws ? .3 : 0), fixture.Resident.Value.Inventory.Water, 8);
        Assert.Equal(draws ? .3 : 0, fixture.Engine.Tiles[17 * 32 + 16].Value.WaterDrawn, 8);
    }

    private static WorldFixture Prepare()
    {
        var fixture = new WorldFixture();
        fixture.Engine.ConfigureWorld(fixture.Engine.State.Rules with
        {
            Births = false,
            Construction = false,
            Expansion = false,
            Wars = false,
            Secession = false,
            Migration = false,
        }, false, false);
        fixture.Engine.SimulationTick = 8 + (4 - fixture.ResidentId % 4) % 4;
        fixture.Resident.Replace(fixture.Resident.Value with
        {
            X = 16,
            Y = 16,
            FromX = 16,
            FromY = 16,
            Inventory = new ResourceStock { Food = 10, Water = 10 },
            Agent = fixture.Resident.Value.Agent with { Initialized = true, NextThinkTick = 0, Goal = new AgentGoal() },
        });
        return fixture;
    }
}
