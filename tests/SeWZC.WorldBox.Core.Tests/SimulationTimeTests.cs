namespace SeWZC.WorldBox.Core.Tests;

/// <summary>日历换算、日内作息和按日结算的边界。</summary>
public sealed class SimulationTimeTests
{
    /// <summary>离开工位走向维修目标后，活动立即表示移动，不沿用上一刻的劳动状态。</summary>
    [Fact]
    public void Worker_in_transit_does_not_keep_the_previous_working_activity()
    {
        var fixture = Prepare(12);
        var ground = fixture.Engine.Current.Tiles[16 * 32 + 22];
        ground.Replace(ground.Value with { ClaimedSettlementId = fixture.Town.Id, NationId = fixture.Town.NationId });
        var buildingId = fixture.Engine.GrantFacility(fixture.Town.Id, BuildingKind.Farm, 22, 16);
        fixture.Engine.Current.Buildings.Single(b => b.Value.Id == buildingId).Replace(fixture.Engine.Current.Buildings.Single(b => b.Value.Id == buildingId).Value with { Health = 10 });
        fixture.Resident.Profession = Profession.Builder;
        fixture.Resident.Inventory = fixture.Resident.Inventory with { Stone = 1 };
        fixture.Resident.Activity = ResidentActivity.Working;
        fixture.Resident.Agent = fixture.Resident.Agent with
        {
            Goal = new AgentGoal
            {
                Kind = AgentGoalKind.Work,
                TargetEntityId = buildingId,
                TargetX = 22,
                TargetY = 16,
                ReviewTick = SimulationTime.TicksPerYear,
            },
        };

        fixture.Engine.Step();

        Assert.Equal(fixture.Engine.State.Tick, fixture.Resident.MoveStartedTick);
        Assert.Equal(ResidentActivity.Wandering, fixture.Resident.Activity);
        Assert.Equal(10, fixture.Engine.Current.Buildings.Single(b => b.Value.Id == buildingId).Value.Health);
    }

    /// <summary>夜间冰冻不能保留此前的劳动标记，也不能产生劳动量。</summary>
    [Fact]
    public void Frozen_worker_rests_instead_of_showing_night_work()
    {
        var fixture = Prepare(SimulationTime.SleepTick - 1);
        fixture.Resident.FrozenUntilTick = SimulationTime.SleepTick + 1;
        fixture.Resident.Activity = ResidentActivity.Working;

        fixture.Engine.Step();

        Assert.Equal(ResidentActivity.Resting, fixture.Resident.Activity);
    }

    /// <summary>月份、日期和日内步序在各单位边界同时进位。</summary>
    [Theory]
    [InlineData(0, 1, 1, 1, 0)]
    [InlineData(SimulationTime.TicksPerDay - 1, 1, 1, 1, SimulationTime.TicksPerDay - 1)]
    [InlineData(SimulationTime.TicksPerDay, 1, 1, 2, 0)]
    [InlineData(SimulationTime.TicksPerMonth - 1, 1, 1, SimulationTime.DaysPerMonth, SimulationTime.TicksPerDay - 1)]
    [InlineData(SimulationTime.TicksPerMonth, 1, 2, 1, 0)]
    [InlineData(SimulationTime.TicksPerYear - 1, 1, SimulationTime.MonthsPerYear, SimulationTime.DaysPerMonth,
        SimulationTime.TicksPerDay - 1)]
    [InlineData(SimulationTime.TicksPerYear, 2, 1, 1, 0)]
    public void Calendar_carries_at_day_month_and_year_boundaries(long tick, int year, int month, int day, int time)
    {
        var state = new WorldState { Width = 0, Height = 0, Tiles = [], Tick = tick };

        Assert.Equal(year, state.Year);
        Assert.Equal(month, state.Month);
        Assert.Equal(day, state.Day);
        Assert.Equal(time, state.TickOfDay);
    }

    /// <summary>傍晚暂存工作目标并沿真实路径返家，不能直接出现在家园。</summary>
    [Fact]
    public void Evening_stores_daytime_task_and_walks_home()
    {
        var fixture = Prepare(SimulationTime.ReturnHomeTick - 1);
        fixture.Resident.X = fixture.Resident.FromX = 22;
        var daytime = fixture.Resident.Agent.Goal;

        fixture.Engine.Step();

        Assert.Equal(AgentGoalKind.Sleep, fixture.Resident.Agent.Goal.Kind);
        Assert.Equal(daytime, fixture.Resident.Agent.DaytimeGoal);
        Assert.InRange(fixture.Resident.X, 21, 22);
        Assert.NotEqual(ResidentActivity.Sleeping, fixture.Resident.Activity);
        Assert.Equal(SimulationTime.ReturnHomeTick, fixture.Resident.MoveStartedTick);
    }

    /// <summary>夜间在家睡眠仍按日内份额消耗口粮和衰老，晨起清除暂存目标。</summary>
    [Fact]
    public void Sleeping_consumes_fractional_needs_and_ends_at_morning()
    {
        var fixture = Prepare(SimulationTime.SleepTick - 1);
        var before = fixture.Resident.Value;
        var foodBefore = before.Inventory.Food + fixture.Town.Resources.Food;

        fixture.Engine.Step();

        Assert.Equal(ResidentActivity.Sleeping, fixture.Resident.Activity);
        Assert.Equal(before.Age + 1d / SimulationTime.TicksPerYear, fixture.Resident.Age, 10);
        Assert.Equal(foodBefore - WorldEngine.FoodUse(before) / SimulationTime.TicksPerDay,
            fixture.Resident.Inventory.Food + fixture.Town.Resources.Food, 10);
        var restored = WorldEngine.ImportJson(fixture.Engine.ExportJson());
        var untilMorning = SimulationTime.TicksPerDay - SimulationTime.SleepTick + SimulationTime.WakeTick;
        fixture.Engine.Step(untilMorning);
        restored.Step(untilMorning);

        Assert.Null(fixture.Resident.Agent.DaytimeGoal);
        Assert.NotEqual(AgentGoalKind.Sleep, fixture.Resident.Agent.Goal.Kind);
        Assert.NotEqual(ResidentActivity.Sleeping, fixture.Resident.Activity);
        Assert.Equal(fixture.Engine.ExportJson(), restored.ExportJson());
    }

    /// <summary>所在地起火立即打断睡眠，不能等到晨起才处理。</summary>
    [Fact]
    public void Fire_interrupts_sleep_immediately()
    {
        var fixture = Prepare(SimulationTime.SleepTick - 1);
        fixture.Engine.Step();
        fixture.Engine.Current.Tiles[16 * 32 + 16].FireTicks = SimulationTime.TicksPerDay;

        fixture.Engine.Step();

        Assert.NotEqual(AgentGoalKind.Sleep, fixture.Resident.Agent.Goal.Kind);
        Assert.Null(fixture.Resident.Agent.DaytimeGoal);
        Assert.NotEqual(ResidentActivity.Sleeping, fixture.Resident.Activity);
    }

    /// <summary>迁居者在途中休息，保存目的地与货物，不能夜间折返家园。</summary>
    [Fact]
    public void Journey_sleeps_in_place_without_losing_its_goal()
    {
        var fixture = Prepare(SimulationTime.SleepTick - 1);
        var goal = new AgentGoal
        {
            Kind = AgentGoalKind.Migrate,
            TargetX = 24,
            TargetY = 16,
            TargetSettlementId = fixture.Town.Id,
            ReviewTick = SimulationTime.TicksPerYear,
        };
        fixture.Resident.X = fixture.Resident.FromX = 22;
        fixture.Resident.Agent = fixture.Resident.Agent with { Goal = goal };

        fixture.Engine.Step();

        Assert.Equal(ResidentActivity.Sleeping, fixture.Resident.Activity);
        Assert.Equal(goal, fixture.Resident.Agent.Goal);
        Assert.Equal(22, fixture.Resident.X);
        Assert.Null(fixture.Resident.Agent.DaytimeGoal);
    }

    /// <summary>远处劳动点的居民夜间宿营，次日继续劳动，避免每天往返占满整个白天。</summary>
    [Theory]
    [InlineData(AgentGoalKind.Gather)]
    [InlineData(AgentGoalKind.Work)]
    public void Distant_workers_camp_without_repeating_the_daily_commute(AgentGoalKind kind)
    {
        var fixture = Prepare(SimulationTime.SleepTick - 1);
        fixture.Resident.X = fixture.Resident.FromX = 22;
        var goal = new AgentGoal { Kind = kind, TargetX = 22, TargetY = 16, ReviewTick = SimulationTime.TicksPerYear };
        fixture.Resident.Agent = fixture.Resident.Agent with { Goal = goal };

        fixture.Engine.Step();

        Assert.Equal(ResidentActivity.Sleeping, fixture.Resident.Activity);
        Assert.Equal(goal, fixture.Resident.Agent.Goal);
        Assert.Equal(22, fixture.Resident.X);
        Assert.Null(fixture.Resident.Agent.DaytimeGoal);
    }

    /// <summary>远处取水在傍晚到场后仍可完成，不被返家规则截断成每日空走。</summary>
    [Fact]
    public void Water_trip_can_collect_after_arriving_in_the_evening()
    {
        var fixture = Prepare(SimulationTime.ReturnHomeTick - 1);
        fixture.Engine.Current.Tiles[16 * 32 + 23].Terrain = TerrainType.River;
        fixture.Resident.X = fixture.Resident.FromX = 22;
        fixture.Resident.Inventory = new ResourceStock { Food = 1 };
        fixture.Resident.Agent = fixture.Resident.Agent with
        {
            Goal = new AgentGoal
            {
                Kind = AgentGoalKind.FetchWater,
                TargetX = 22,
                TargetY = 16,
                TargetEntityId = 16 * 32 + 23 + 1,
                ReviewTick = SimulationTime.TicksPerYear,
            },
        };

        fixture.Engine.Step();

        Assert.True(fixture.Resident.Inventory.Water > .3);
        Assert.NotEqual(AgentGoalKind.Sleep, fixture.Resident.Agent.Goal.Kind);
        Assert.Null(fixture.Resident.Agent.DaytimeGoal);
    }

    /// <summary>寻水勘察在夜间宿营，保留目标，使次日能继续推进而非原路重走。</summary>
    [Fact]
    public void Water_survey_camps_without_restarting_the_route()
    {
        var fixture = Prepare(SimulationTime.SleepTick - 1);
        fixture.Resident.X = fixture.Resident.FromX = 22;
        var goal = new AgentGoal { Kind = AgentGoalKind.FetchWater, TargetX = 24, TargetY = 16 };
        fixture.Resident.Agent = fixture.Resident.Agent with { Goal = goal };

        fixture.Engine.Step();

        Assert.Equal(ResidentActivity.Sleeping, fixture.Resident.Activity);
        Assert.Equal(goal, fixture.Resident.Agent.Goal);
        Assert.Equal(22, fixture.Resident.X);
    }

    /// <summary>返程跨过晨起时仍先走到家，不能立即转身重走昨天的路。</summary>
    [Fact]
    public void Unfinished_return_home_continues_after_dawn()
    {
        var fixture = Prepare(SimulationTime.WakeTick - 1);
        fixture.Resident.X = fixture.Resident.FromX = 22;
        fixture.Resident.Agent = fixture.Resident.Agent with
        {
            DaytimeGoal = fixture.Resident.Agent.Goal,
            Goal = new AgentGoal { Kind = AgentGoalKind.Sleep, TargetX = 16, TargetY = 16 },
        };

        fixture.Engine.Step();

        Assert.Equal(AgentGoalKind.Sleep, fixture.Resident.Agent.Goal.Kind);
        Assert.NotNull(fixture.Resident.Agent.DaytimeGoal);
        Assert.InRange(fixture.Resident.X, 21, 22);
        Assert.NotEqual(ResidentActivity.Sleeping, fixture.Resident.Activity);
    }

    /// <summary>玩家仍在指挥时可覆盖作息；命令到期后恢复自主睡眠。</summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Player_order_overrides_routine_only_until_its_deadline(bool active)
    {
        var fixture = Prepare(SimulationTime.SleepTick - 1);
        fixture.Resident.Agent = fixture.Resident.Agent with
        {
            Goal = fixture.Resident.Agent.Goal with
            {
                PlayerDirected = true, ReviewTick = SimulationTime.SleepTick + (active ? 1 : 0),
            },
        };

        fixture.Engine.Step();

        Assert.Equal(!active, fixture.Resident.Activity == ResidentActivity.Sleeping);
    }

    /// <summary>水井的共享额度日内不重置，下一日才恢复。</summary>
    [Fact]
    public void Well_quota_resets_at_day_boundary_instead_of_each_tick()
    {
        var fixture = Prepare(SimulationTime.WakeTick);
        fixture.AddWell(16, 17, .025);
        fixture.Resident.X = fixture.Resident.FromX = 16;
        fixture.Resident.Y = fixture.Resident.FromY = 17;
        fixture.Resident.Inventory = new ResourceStock();
        fixture.Resident.Agent = fixture.Resident.Agent with
        {
            Goal = new AgentGoal
            {
                Kind = AgentGoalKind.FetchWater,
                TargetX = 16,
                TargetY = 17,
                TargetEntityId = 17 * 32 + 16 + 1,
                ReviewTick = SimulationTime.TicksPerYear,
            },
        };
        Assert.True(fixture.Engine.TryFetchWater(fixture.Resident.Value));
        fixture.Engine.Current.Tick++;

        Assert.False(fixture.Engine.TryFetchWater(fixture.Resident.Value));
        Assert.Equal(.15, fixture.Resident.Inventory.Water, 8);
        fixture.Engine.Current.Tick = SimulationTime.TicksPerDay;
        Assert.True(fixture.Engine.TryFetchWater(fixture.Resident.Value));
        Assert.Equal(.3, fixture.Resident.Inventory.Water, 8);
    }

    private static WorldFixture Prepare(int tick)
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
            Research = false,
            Disease = false,
        }, false, false);
        fixture.Engine.Current.Tick = tick;
        fixture.Resident.Replace(fixture.Resident.Value with
        {
            Age = 25,
            X = 16,
            Y = 16,
            FromX = 16,
            FromY = 16,
            MoveStartedTick = 0,
            MoveDurationTicks = 1,
            Inventory = new ResourceStock { Food = 10, Water = 10 },
            Agent = fixture.Resident.Agent with
            {
                Initialized = true,
                NextThinkTick = SimulationTime.TicksPerYear,
                Goal = new AgentGoal
                {
                    Kind = AgentGoalKind.Socialize,
                    TargetX = 16,
                    TargetY = 16,
                    ReviewTick = SimulationTime.TicksPerYear,
                },
            },
        });
        return fixture;
    }
}
