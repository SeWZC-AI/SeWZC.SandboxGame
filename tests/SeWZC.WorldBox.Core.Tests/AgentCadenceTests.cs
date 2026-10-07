using SeWZC.WorldBox.Core;

namespace SeWZC.WorldBox.Core.Tests;

/// <summary>自主评估错峰与供水等待的即时边界。</summary>
public sealed class AgentCadenceTests
{
    /// <summary>精力已恢复的到场休息仍结算身体需求，但复用未改变的心智快照。</summary>
    [Theory]
    [InlineData(AgentGoalKind.Rest, ResidentActivity.Resting)]
    [InlineData(AgentGoalKind.Socialize, ResidentActivity.Talking)]
    public void Recovered_resident_reuses_unchanged_agent_state(AgentGoalKind kind, ResidentActivity activity)
    {
        var fixture = Prepare();
        fixture.Resident.Activity = activity;
        fixture.Resident.Agent.Goal = new AgentGoal
        {
            Kind = kind, TargetX = 16, TargetY = 16, PlayerDirected = true, ReviewTick = 100,
        };
        var before = fixture.Resident.Value;

        fixture.Engine.Step();

        Assert.Same(before.Agent, fixture.Resident.Value.Agent);
        Assert.True(fixture.Resident.Age > before.Age);
        Assert.True(fixture.Resident.Inventory.Food < before.Inventory.Food);
    }

    /// <summary>同格多人交谈只选择其他居民，且新消息必须等到下一日递送。</summary>
    [Fact]
    public void Crowded_cell_conversation_excludes_sender_and_delays_delivery()
    {
        var fixture = Prepare();
        fixture.Engine.SpawnResidents(16, 16, RaceKind.Human, 2);
        fixture.Engine.Current.Tick = 11;
        foreach (var person in fixture.Engine.Current.Residents)
            person.Replace(person.Value with
            {
                X = 16, Y = 16, FromX = 16, FromY = 16,
                Inventory = new ResourceStock { Food = 10, Water = 10 },
                Agent = person.Agent.Value with
                {
                    Initialized = true, NextThinkTick = 100,
                    Goal = new AgentGoal { Kind = AgentGoalKind.Rest, PlayerDirected = true, ReviewTick = 100 },
                },
            });
        fixture.Resident.Agent.Memory.Add(new AgentFact
        {
            Id = 90_001, Kind = AgentFactKind.SettlementLocation, SubjectId = fixture.Town.Id,
            X = 16, Y = 16, ObservedTick = 1, LearnedTick = 1,
            OriginResidentId = fixture.ResidentId, SourceResidentId = fixture.ResidentId,
            Confidence = 1, Text = "亲眼见到家园",
        });

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
        fixture.Town.Resources = new ResourceStock();
        fixture.Resident.Inventory = new ResourceStock { Water = 10 };
        fixture.Resident.Hunger = 90;
        fixture.Resident.Agent.NextThinkTick = fixture.Engine.Current.Tick + 4;
        fixture.Resident.Agent.Goal = new AgentGoal
        {
            Kind = AgentGoalKind.Gather, TargetX = 16, TargetY = 16,
            ReviewTick = fixture.Engine.Current.Tick + 4,
        };
        fixture.Resident.FrozenUntilTick = fixture.Engine.Current.Tick + 2;

        fixture.Engine.Step();

        Assert.Empty(fixture.Resident.Agent.Decisions);
        Assert.Equal(AgentGoalKind.Gather, fixture.Resident.Agent.Goal.Kind);
        Assert.True(fixture.Resident.Hunger > 90);
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
        foreach (var tile in fixture.Engine.Current.Tiles) tile.NaturalWaterYield = 0;
        fixture.Town.Resources = new ResourceStock { Food = 40 };
        fixture.Resident.Inventory = new ResourceStock { Food = 10 };
        fixture.Resident.Thirst = 90;
        fixture.Resident.Agent.NextThinkTick = 100;
        fixture.Resident.FrozenUntilTick = fixture.Engine.Current.Tick + 2;
        var source = 17 * 32 + 16;
        fixture.Engine.Current.Tiles[source].NaturalWaterYield = 1;

        fixture.Engine.Step();

        Assert.Equal(AgentGoalKind.FetchWater, fixture.Resident.Agent.Goal.Kind);
        Assert.Equal(source + 1, fixture.Resident.Agent.Goal.TargetEntityId);
        Assert.Equal(16, fixture.Resident.X);
        Assert.Equal(16, fixture.Resident.Y);
    }

    /// <summary>每日额度耗尽时保留会恢复供水的已知水源，不转向或反复请求评估。</summary>
    [Fact]
    public void Exhausted_daily_water_quota_preserves_the_known_source()
    {
        var fixture = new WorldFixture();
        fixture.Engine.Current.Tick = 1;
        fixture.Resident.X = 16;
        fixture.Resident.Y = 16;
        fixture.Resident.Inventory = new ResourceStock();
        fixture.Resident.Agent.ExplorationHeading = 2;
        fixture.Resident.Agent.NextThinkTick = 37;
        var source = 16 * 32 + 16;
        fixture.Resident.Agent.Goal = new AgentGoal
        {
            Kind = AgentGoalKind.FetchWater, TargetX = 16, TargetY = 16,
            TargetEntityId = source + 1, WorkTicks = 3,
        };
        var tile = fixture.Engine.Current.Tiles[source];
        tile.Replace(tile.Value with { NaturalWaterYield = .1, WaterDrawTick = 1, WaterDrawn = .1 });

        Assert.False(fixture.Engine.TryFetchWater(fixture.Resident.Value));

        Assert.Equal(2, fixture.Resident.Agent.ExplorationHeading);
        Assert.Equal(37, fixture.Resident.Agent.NextThinkTick);
        Assert.Equal(.1, tile.WaterDrawn);
        Assert.Equal(0, fixture.Resident.Inventory.Water);
    }

    private static WorldFixture Prepare()
    {
        var fixture = new WorldFixture();
        fixture.Engine.ConfigureWorld(fixture.Engine.State.Rules with
        {
            Births = false, Construction = false, Expansion = false,
            Wars = false, Secession = false, Migration = false,
        }, false, false);
        fixture.Engine.Current.Tick = 8 + (4 - fixture.ResidentId % 4) % 4;
        fixture.Resident.Replace(fixture.Resident.Value with
        {
            X = 16, Y = 16, FromX = 16, FromY = 16,
            Inventory = new ResourceStock { Food = 10, Water = 10 },
            Agent = fixture.Resident.Agent.Value with { Initialized = true, NextThinkTick = 0, Goal = new AgentGoal() },
        });
        return fixture;
    }
}
