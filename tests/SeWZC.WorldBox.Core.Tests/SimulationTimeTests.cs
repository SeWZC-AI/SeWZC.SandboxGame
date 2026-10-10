namespace SeWZC.WorldBox.Core.Tests;

/// <summary>日历换算、日内作息和按日结算的边界。</summary>
public sealed class SimulationTimeTests
{
    /// <summary>离开工位走向维修目标后，活动立即表示移动，不沿用上一刻的劳动状态。</summary>
    [Fact]
    public void Worker_in_transit_does_not_keep_the_previous_working_activity()
    {
        var fixture = Prepare(12);
        var ground = fixture.Engine.Tiles[16 * 32 + 22];
        ground.Replace(ground.Value with { ClaimedSettlementId = fixture.Town.Value.Id, NationId = fixture.Town.Value.NationId });
        var buildingId = fixture.Engine.GrantFacility(fixture.Town.Value.Id, BuildingKind.Farm, 22, 16);
        fixture.Engine.Buildings.Single(b => b.Value.Id == buildingId).Replace(fixture.Engine.Buildings.Single(b => b.Value.Id == buildingId).Value with { Health = 10 });
        fixture.Resident.Replace(fixture.Resident.Value with { Profession = Profession.Builder });
        fixture.Resident.Replace(fixture.Resident.Value.WithInventory(fixture.Resident.Value.Inventory with { Stone = 1 }));
        fixture.Resident.Replace(fixture.Resident.Value.WithActivity(ResidentActivity.Working));
        fixture.Resident.Replace(fixture.Resident.Value.WithAgent(fixture.Resident.Value.Agent with
        {
            Goal = new AgentGoal
            {
                Kind = AgentGoalKind.Work,
                TargetEntityId = buildingId,
                TargetX = 22,
                TargetY = 16,
                ReviewTick = SimulationTime.TicksPerYear,
            },
        }));

        fixture.Engine.Step();

        Assert.Equal(fixture.Engine.State.Tick, fixture.Resident.Value.MoveStartedTick);
        Assert.Equal(ResidentActivity.Wandering, fixture.Resident.Value.Activity);
        Assert.Equal(10, fixture.Engine.Buildings.Single(b => b.Value.Id == buildingId).Value.Health);
    }

    /// <summary>夜间冰冻不能保留此前的劳动标记，也不能产生劳动量。</summary>
    [Fact]
    public void Frozen_worker_rests_instead_of_showing_night_work()
    {
        var fixture = Prepare(SimulationTime.SleepTick - 1);
        fixture.Resident.Replace(fixture.Resident.Value with { FrozenUntilTick = SimulationTime.SleepTick + 1 });
        fixture.Resident.Replace(fixture.Resident.Value.WithActivity(ResidentActivity.Working));

        fixture.Engine.Step();

        Assert.Equal(ResidentActivity.Resting, fixture.Resident.Value.Activity);
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
        fixture.Resident.Replace(fixture.Resident.Value with { FromX = 22, X = 22 });
        var daytime = fixture.Resident.Value.Agent.Goal;

        fixture.Engine.Step();

        Assert.Equal(AgentGoalKind.Sleep, fixture.Resident.Value.Agent.Goal.Kind);
        Assert.Equal(daytime, fixture.Resident.Value.Agent.DaytimeGoal);
        Assert.InRange(fixture.Resident.Value.X, 21, 22);
        Assert.NotEqual(ResidentActivity.Sleeping, fixture.Resident.Value.Activity);
        Assert.Equal(SimulationTime.ReturnHomeTick, fixture.Resident.Value.MoveStartedTick);
        Assert.Contains("正在返家", fixture.Engine.GetResidentActionSummary(fixture.ResidentId));
        Assert.DoesNotContain("到场后开始劳动", fixture.Engine.GetResidentActionSummary(fixture.ResidentId));
    }

    /// <summary>傍晚在家普通休息仅恢复体力，显示等待入睡且睡眠值仍消耗。</summary>
    [Fact]
    public void Evening_rest_shows_waiting_for_sleep_and_does_not_restore_sleep()
    {
        var fixture = Prepare(SimulationTime.ReturnHomeTick - 1);
        fixture.Resident.Replace(fixture.Resident.Value.WithAgent(fixture.Resident.Value.Agent with { Sleep = 60, Fatigue = 40 }));

        fixture.Engine.Step();

        Assert.Equal(ResidentActivity.Resting, fixture.Resident.Value.Activity);
        Assert.True(fixture.Resident.Value.Agent.Sleep < 60);
        Assert.True(fixture.Resident.Value.Agent.Fatigue < 40);
        Assert.Contains("等待入睡", fixture.Engine.GetResidentActionSummary(fixture.ResidentId));
        Assert.Contains("睡眠", fixture.Engine.GetResidentTaskSummary(fixture.ResidentId));
    }

    /// <summary>休息目标可因睡眠不足而实际补觉，显示须区分普通休息和睡眠。</summary>
    [Theory]
    [InlineData(80, ResidentActivity.Resting)]
    [InlineData(25, ResidentActivity.Sleeping)]
    public void Rest_goal_reports_the_actual_recovery_activity(double sleep, ResidentActivity activity)
    {
        var fixture = Prepare(SimulationTime.WakeTick);
        fixture.Resident.Replace(fixture.Resident.Value.WithAgent(fixture.Resident.Value.Agent with
        {
            Sleep = sleep,
            Goal = new AgentGoal
            {
                Kind = AgentGoalKind.Rest, TargetX = 16, TargetY = 16,
                PlayerDirected = true, ReviewTick = SimulationTime.TicksPerYear,
            },
        }));

        fixture.Engine.Step();

        Assert.Equal(activity, fixture.Resident.Value.Activity);
        Assert.Equal(AgentGoalKind.Rest, fixture.Resident.Value.Agent.Goal.Kind);
        if (activity == ResidentActivity.Sleeping)
        {
            Assert.True(fixture.Resident.Value.Agent.Sleep > sleep);
            Assert.Contains("正在睡眠", fixture.Engine.GetResidentActionSummary(fixture.ResidentId));
        }
        else
        {
            Assert.True(fixture.Resident.Value.Agent.Sleep < sleep);
            Assert.Contains("正在休息", fixture.Engine.GetResidentActionSummary(fixture.ResidentId));
            Assert.DoesNotContain("正在睡眠", fixture.Engine.GetResidentActionSummary(fixture.ResidentId));
        }
    }

    /// <summary>前往医疗点的居民夜间睡眠，说明须区分到场与途中宿营。</summary>
    [Theory]
    [InlineData(14, false)]
    [InlineData(15, false)]
    [InlineData(22, true)]
    public void Medical_sleep_summary_uses_the_actual_location(int x, bool camping)
    {
        var fixture = Prepare(SimulationTime.SleepTick - 1);
        var ground = fixture.Engine.Tiles[16 * 32 + 14];
        ground.Replace(ground.Value with { ClaimedSettlementId = fixture.Town.Value.Id, NationId = fixture.Town.Value.NationId });
        var clinicId = fixture.Engine.GrantFacility(fixture.Town.Value.Id, BuildingKind.Infirmary, 14, 16);
        fixture.Resident.Replace(fixture.Resident.Value with { FromX = x, X = x, Health = 50 });
        fixture.Resident.Replace(fixture.Resident.Value.WithAgent(fixture.Resident.Value.Agent with
        {
            Sleep = 50,
            Goal = new AgentGoal
            {
                Kind = AgentGoalKind.Rest, TargetEntityId = clinicId, TargetX = 14, TargetY = 16,
                ReviewTick = SimulationTime.TicksPerYear,
            },
        }));

        fixture.Engine.Step();

        Assert.Equal(ResidentActivity.Sleeping, fixture.Resident.Value.Activity);
        Assert.Contains(WorldEngine.BuildingName(BuildingKind.Infirmary), fixture.Engine.GetResidentTaskSummary(fixture.ResidentId));
        var summary = fixture.Engine.GetResidentActionSummary(fixture.ResidentId);
        Assert.Contains("正在睡眠", summary);
        Assert.Contains("伤病恢复", summary);
        if (camping)
        {
            Assert.Contains("途中宿营", summary);
            Assert.DoesNotContain("在" + WorldEngine.BuildingName(BuildingKind.Infirmary) + "休息", summary);
        }
        else
            Assert.Contains("在" + WorldEngine.BuildingName(BuildingKind.Infirmary) + "休息", summary);
    }

    /// <summary>白天赴医疗点是休养，不显示返家、劳动或工作人员受阻。</summary>
    [Theory]
    [InlineData(14, false)]
    [InlineData(22, true)]
    public void Medical_rest_summary_describes_recovery_instead_of_work(int x, bool travelling)
    {
        var fixture = Prepare(SimulationTime.WakeTick);
        var ground = fixture.Engine.Tiles[16 * 32 + 14];
        ground.Replace(ground.Value with { ClaimedSettlementId = fixture.Town.Value.Id, NationId = fixture.Town.Value.NationId });
        var clinicId = fixture.Engine.GrantFacility(fixture.Town.Value.Id, BuildingKind.Infirmary, 14, 16);
        fixture.Resident.Replace(fixture.Resident.Value with { FromX = x, X = x, Health = 50 });
        fixture.Resident.Replace(fixture.Resident.Value.WithAgent(fixture.Resident.Value.Agent with
        {
            Sleep = 100, Fatigue = 0, NextThinkTick = 0,
            Goal = new AgentGoal
            {
                Kind = AgentGoalKind.Rest, TargetEntityId = clinicId, TargetX = 14, TargetY = 16,
                ReviewTick = fixture.Engine.State.Tick,
            },
        }));

        fixture.Engine.Step();

        Assert.Equal(AgentGoalKind.Rest, fixture.Resident.Value.Agent.Goal.Kind);
        Assert.True(fixture.Resident.Value.Health < 70);
        var clinicName = WorldEngine.BuildingName(BuildingKind.Infirmary);
        Assert.Contains(clinicName, fixture.Engine.GetResidentTaskSummary(fixture.ResidentId));
        var summary = fixture.Engine.GetResidentActionSummary(fixture.ResidentId);
        Assert.Contains(travelling ? "前往" + clinicName + "休息" : "正在休息", summary);
        Assert.DoesNotContain("返回家园", summary);
        Assert.DoesNotContain("到场后开始劳动", summary);
        Assert.DoesNotContain("现场劳动受阻", summary);
        Assert.Contains("伤病恢复", summary);
    }

    /// <summary>玩家可指定家园外的休息或睡眠地点，说明不能误报返家或自主作息。</summary>
    [Theory]
    [InlineData(AgentGoalKind.Rest, false, false)]
    [InlineData(AgentGoalKind.Rest, true, false)]
    [InlineData(AgentGoalKind.Rest, false, true)]
    [InlineData(AgentGoalKind.Rest, true, true)]
    [InlineData(AgentGoalKind.Sleep, false, false)]
    [InlineData(AgentGoalKind.Sleep, true, false)]
    public void Directed_recovery_summary_respects_the_selected_place_and_schedule(AgentGoalKind kind, bool arrived, bool retargeted)
    {
        var fixture = Prepare(SimulationTime.WakeTick);
        var clinicId = 0;
        if (retargeted)
        {
            var ground = fixture.Engine.Tiles[16 * 32 + 14];
            ground.Replace(ground.Value with { ClaimedSettlementId = fixture.Town.Value.Id, NationId = fixture.Town.Value.NationId });
            clinicId = fixture.Engine.GrantFacility(fixture.Town.Value.Id, BuildingKind.Infirmary, 14, 16);
        }
        if (arrived)
            fixture.Resident.Replace(fixture.Resident.Value with { X = 24, FromX = 24 });
        fixture.Resident.Replace(fixture.Resident.Value.WithAgent(fixture.Resident.Value.Agent with
        {
            Sleep = 80, Fatigue = 40,
            Goal = new AgentGoal
            {
                Kind = kind, TargetX = 24, TargetY = 16, TargetSettlementId = fixture.Town.Value.Id, TargetEntityId = clinicId,
                PlayerDirected = true, ReviewTick = SimulationTime.TicksPerYear,
            },
        }));

        fixture.Engine.Step();

        var task = fixture.Engine.GetResidentTaskSummary(fixture.ResidentId);
        var summary = fixture.Engine.GetResidentActionSummary(fixture.ResidentId);
        Assert.DoesNotContain("返家", task + summary);
        Assert.DoesNotContain("返回家园", task + summary);
        Assert.DoesNotContain("清晨", summary);
        Assert.Contains("指定地点", task);
        if (retargeted)
            Assert.DoesNotContain("医疗休养", summary);
        if (!arrived)
            Assert.Contains("前往指定地点", summary);
        else
            Assert.Contains(kind == AgentGoalKind.Sleep ? "正在睡眠" : "正在休息", summary);
    }

    /// <summary>暂停时指定恢复目标只安排下一步，不能把当前劳动显示为已开始休息。</summary>
    [Theory]
    [InlineData(AgentGoalKind.Rest, 16, 16)]
    [InlineData(AgentGoalKind.Rest, 24, 24)]
    [InlineData(AgentGoalKind.Rest, 16, 24)]
    [InlineData(AgentGoalKind.Sleep, 16, 16)]
    [InlineData(AgentGoalKind.Sleep, 24, 24)]
    [InlineData(AgentGoalKind.Sleep, 16, 24)]
    public void Recovery_summary_does_not_claim_rest_started_before_the_next_step(AgentGoalKind kind, int x, int targetX)
    {
        var fixture = Prepare(SimulationTime.WakeTick);
        fixture.Resident.Replace(fixture.Resident.Value with
        {
            X = x, FromX = x, Activity = ResidentActivity.Working,
            Agent = fixture.Resident.Value.Agent with
            {
                Goal = new AgentGoal
                {
                    Kind = kind, TargetX = targetX, TargetY = 16,
                    PlayerDirected = true, ReviewTick = SimulationTime.TicksPerYear,
                },
            },
        });
        var before = fixture.Resident.Value;

        var summary = fixture.Engine.GetResidentActionSummary(fixture.ResidentId);

        Assert.Contains(kind == AgentGoalKind.Sleep ? "已安排睡眠" : "已安排休息", summary);
        Assert.DoesNotContain("正在家园休息", summary);
        Assert.DoesNotContain("正在休息恢复体力", summary);
        Assert.DoesNotContain("正在前往", summary);
        Assert.DoesNotContain("正在返家", summary);
        Assert.Equal(before, fixture.Resident.Value);
    }

    /// <summary>指定睡眠到期后重新按自主作息返家，说明不能沿用旧地点与安排。</summary>
    [Theory]
    [InlineData(0, false)]
    [InlineData(1, true)]
    public void Sleep_summary_uses_only_the_active_player_schedule(int remaining, bool directed)
    {
        var fixture = Prepare(SimulationTime.SleepTick - 1);
        fixture.Resident.Replace(fixture.Resident.Value with { X = 22, FromX = 22 });
        fixture.Resident.Replace(fixture.Resident.Value.WithAgent(fixture.Resident.Value.Agent with
        {
            Goal = new AgentGoal
            {
                Kind = AgentGoalKind.Sleep, TargetX = 24, TargetY = 16,
                PlayerDirected = true, ReviewTick = SimulationTime.SleepTick + remaining,
            },
        }));

        fixture.Engine.Step();

        var summary = fixture.Engine.GetResidentActionSummary(fixture.ResidentId);
        if (directed)
        {
            Assert.True(fixture.Resident.Value.X > 22);
            Assert.Contains("前往指定地点", summary);
            Assert.DoesNotContain("清晨", summary);
        }
        else
        {
            Assert.True(fixture.Resident.Value.X < 22);
            Assert.Contains("正在返家", summary);
            Assert.Contains("清晨", summary);
            Assert.DoesNotContain("指定地点", summary);
            Assert.DoesNotContain("玩家安排", summary);
        }
    }

    /// <summary>指定睡眠在移动中到期时，先完成区段，实际转向后才显示返家。</summary>
    [Fact]
    public void Expired_sleep_summary_waits_for_the_current_movement_before_reporting_return_home()
    {
        var fixture = Prepare(SimulationTime.SleepTick - 1);
        fixture.Resident.Replace(fixture.Resident.Value with
        {
            X = 23, FromX = 22, MoveStartedTick = SimulationTime.SleepTick - 1, MoveDurationTicks = 2,
            Activity = ResidentActivity.Wandering,
            Agent = fixture.Resident.Value.Agent with
            {
                Goal = new AgentGoal
                {
                    Kind = AgentGoalKind.Sleep, TargetX = 24, TargetY = 16,
                    PlayerDirected = true, ReviewTick = SimulationTime.SleepTick,
                },
            },
        });

        fixture.Engine.Step();

        Assert.Equal(23, fixture.Resident.Value.X);
        var summary = fixture.Engine.GetResidentActionSummary(fixture.ResidentId);
        Assert.Contains("正在完成当前移动", summary);
        Assert.DoesNotContain("正在返家", summary);

        fixture.Engine.Step();

        Assert.True(fixture.Resident.Value.X < 23);
        Assert.Contains("正在返家", fixture.Engine.GetResidentActionSummary(fixture.ResidentId));
    }

    /// <summary>被冻结的居民尚未抵达指定休息点时，不能显示已在指定地点休息。</summary>
    [Fact]
    public void Recovery_summary_does_not_claim_arrival_while_frozen_in_transit()
    {
        var fixture = Prepare(SimulationTime.SleepTick - 1);
        fixture.Resident.Replace(fixture.Resident.Value with
        {
            X = 22, FromX = 22, FrozenUntilTick = SimulationTime.SleepTick + 1,
        });
        fixture.Resident.Replace(fixture.Resident.Value.WithAgent(fixture.Resident.Value.Agent with
        {
            Goal = new AgentGoal
            {
                Kind = AgentGoalKind.Rest, TargetX = 24, TargetY = 16,
                PlayerDirected = true, ReviewTick = SimulationTime.SleepTick,
            },
        }));

        fixture.Engine.Step();

        Assert.Equal(ResidentActivity.Resting, fixture.Resident.Value.Activity);
        var summary = fixture.Engine.GetResidentActionSummary(fixture.ResidentId);
        Assert.Contains("原地休息", summary);
        Assert.DoesNotContain("在指定地点休息", summary);
    }

    /// <summary>夜间在家睡眠仍按日内份额消耗口粮和衰老，晨起清除暂存目标。</summary>
    [Fact]
    public void Sleeping_consumes_fractional_needs_and_ends_at_morning()
    {
        var fixture = Prepare(SimulationTime.SleepTick - 1);
        var before = fixture.Resident.Value;
        var foodBefore = before.Inventory.Food + fixture.Town.Value.Resources.Food;

        fixture.Engine.Step();

        Assert.Equal(ResidentActivity.Sleeping, fixture.Resident.Value.Activity);
        Assert.Contains("睡眠", fixture.Engine.GetResidentTaskSummary(fixture.ResidentId));
        Assert.Contains("正在睡眠", fixture.Engine.GetResidentActionSummary(fixture.ResidentId));
        Assert.Contains("在家", fixture.Engine.GetResidentActionSummary(fixture.ResidentId));
        Assert.Equal(before.Age + 1d / SimulationTime.TicksPerYear, fixture.Resident.Value.Age, 10);
        Assert.Equal(foodBefore - WorldEngine.FoodUse(before) / SimulationTime.TicksPerDay,
            fixture.Resident.Value.Inventory.Food + fixture.Town.Value.Resources.Food, 10);
        var restored = WorldEngine.ImportJson(fixture.Engine.ExportJson());
        var untilMorning = SimulationTime.TicksPerDay - SimulationTime.SleepTick + SimulationTime.WakeTick;
        fixture.Engine.Step(untilMorning);
        restored.Step(untilMorning);

        Assert.Null(fixture.Resident.Value.Agent.DaytimeGoal);
        Assert.NotEqual(AgentGoalKind.Sleep, fixture.Resident.Value.Agent.Goal.Kind);
        Assert.NotEqual(ResidentActivity.Sleeping, fixture.Resident.Value.Activity);
        Assert.Equal(fixture.Engine.ExportJson(), restored.ExportJson());
    }

    /// <summary>所在地起火立即打断睡眠，不能等到晨起才处理。</summary>
    [Fact]
    public void Fire_interrupts_sleep_immediately()
    {
        var fixture = Prepare(SimulationTime.SleepTick - 1);
        fixture.Engine.Step();
        fixture.Engine.Tiles[16 * 32 + 16].Replace(fixture.Engine.Tiles[16 * 32 + 16].Value.WithFireTicks(SimulationTime.TicksPerDay));

        fixture.Engine.Step();

        Assert.NotEqual(AgentGoalKind.Sleep, fixture.Resident.Value.Agent.Goal.Kind);
        Assert.Null(fixture.Resident.Value.Agent.DaytimeGoal);
        Assert.NotEqual(ResidentActivity.Sleeping, fixture.Resident.Value.Activity);
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
            TargetSettlementId = fixture.Town.Value.Id,
            ReviewTick = SimulationTime.TicksPerYear,
        };
        fixture.Resident.Replace(fixture.Resident.Value with { FromX = 22, X = 22 });
        fixture.Resident.Replace(fixture.Resident.Value.WithAgent(fixture.Resident.Value.Agent with { Goal = goal }));

        fixture.Engine.Step();

        Assert.Equal(ResidentActivity.Sleeping, fixture.Resident.Value.Activity);
        Assert.Equal(goal, fixture.Resident.Value.Agent.Goal);
        Assert.Equal(22, fixture.Resident.Value.X);
        Assert.Null(fixture.Resident.Value.Agent.DaytimeGoal);
    }

    /// <summary>远处劳动点的居民夜间宿营，次日继续劳动，避免每天往返占满整个白天。</summary>
    [Theory]
    [InlineData(AgentGoalKind.Gather)]
    [InlineData(AgentGoalKind.Work)]
    public void Distant_workers_camp_without_repeating_the_daily_commute(AgentGoalKind kind)
    {
        var fixture = Prepare(SimulationTime.SleepTick - 1);
        fixture.Resident.Replace(fixture.Resident.Value with { FromX = 22, X = 22 });
        var goal = new AgentGoal { Kind = kind, TargetX = 22, TargetY = 16, ReviewTick = SimulationTime.TicksPerYear };
        fixture.Resident.Replace(fixture.Resident.Value.WithAgent(fixture.Resident.Value.Agent with { Goal = goal }));
        fixture.Resident.Replace(fixture.Resident.Value.WithAgent(fixture.Resident.Value.Agent with { Sleep = 50 }));

        fixture.Engine.Step();

        Assert.Equal(ResidentActivity.Sleeping, fixture.Resident.Value.Activity);
        Assert.Equal(goal, fixture.Resident.Value.Agent.Goal);
        Assert.Equal(22, fixture.Resident.Value.X);
        Assert.Null(fixture.Resident.Value.Agent.DaytimeGoal);
        Assert.True(fixture.Resident.Value.Agent.Sleep > 50);
        Assert.Contains("正在睡眠", fixture.Engine.GetResidentActionSummary(fixture.ResidentId));
        Assert.Contains("途中宿营", fixture.Engine.GetResidentActionSummary(fixture.ResidentId));
        Assert.DoesNotContain("当前劳作：正在采", fixture.Engine.GetResidentActionSummary(fixture.ResidentId));
    }

    /// <summary>远处取水在傍晚到场后仍可完成，不被返家规则截断成每日空走。</summary>
    [Fact]
    public void Water_trip_can_collect_after_arriving_in_the_evening()
    {
        var fixture = Prepare(SimulationTime.ReturnHomeTick - 1);
        fixture.Engine.Tiles[16 * 32 + 23].Replace(fixture.Engine.Tiles[16 * 32 + 23].Value.WithTerrain(TerrainType.River));
        fixture.Resident.Replace(fixture.Resident.Value with { FromX = 22, X = 22 });
        fixture.Resident.Replace(fixture.Resident.Value.WithInventory(new ResourceStock { Food = 1 }));
        fixture.Resident.Replace(fixture.Resident.Value.WithAgent(fixture.Resident.Value.Agent with
        {
            Goal = new AgentGoal
            {
                Kind = AgentGoalKind.FetchWater,
                TargetX = 22,
                TargetY = 16,
                TargetEntityId = 16 * 32 + 23 + 1,
                ReviewTick = SimulationTime.TicksPerYear,
            },
        }));

        fixture.Engine.Step();

        Assert.True(fixture.Resident.Value.Inventory.Water > .3);
        Assert.NotEqual(AgentGoalKind.Sleep, fixture.Resident.Value.Agent.Goal.Kind);
        Assert.Null(fixture.Resident.Value.Agent.DaytimeGoal);
    }

    /// <summary>寻水勘察在夜间宿营，保留目标，使次日能继续推进而非原路重走。</summary>
    [Fact]
    public void Water_survey_camps_without_restarting_the_route()
    {
        var fixture = Prepare(SimulationTime.SleepTick - 1);
        fixture.Resident.Replace(fixture.Resident.Value with { FromX = 22, X = 22 });
        var goal = new AgentGoal { Kind = AgentGoalKind.FetchWater, TargetX = 24, TargetY = 16 };
        fixture.Resident.Replace(fixture.Resident.Value.WithAgent(fixture.Resident.Value.Agent with { Goal = goal }));

        fixture.Engine.Step();

        Assert.Equal(ResidentActivity.Sleeping, fixture.Resident.Value.Activity);
        Assert.Equal(goal, fixture.Resident.Value.Agent.Goal);
        Assert.Equal(22, fixture.Resident.Value.X);
    }

    /// <summary>返程跨过晨起时仍先走到家，不能立即转身重走昨天的路。</summary>
    [Fact]
    public void Unfinished_return_home_continues_after_dawn()
    {
        var fixture = Prepare(SimulationTime.WakeTick - 1);
        fixture.Resident.Replace(fixture.Resident.Value with { FromX = 22, X = 22 });
        fixture.Resident.Replace(fixture.Resident.Value.WithAgent(fixture.Resident.Value.Agent with
        {
            DaytimeGoal = fixture.Resident.Value.Agent.Goal,
            Goal = new AgentGoal { Kind = AgentGoalKind.Sleep, TargetX = 16, TargetY = 16 },
        }));

        fixture.Engine.Step();

        Assert.Equal(AgentGoalKind.Sleep, fixture.Resident.Value.Agent.Goal.Kind);
        Assert.NotNull(fixture.Resident.Value.Agent.DaytimeGoal);
        Assert.InRange(fixture.Resident.Value.X, 21, 22);
        Assert.NotEqual(ResidentActivity.Sleeping, fixture.Resident.Value.Activity);
        var summary = fixture.Engine.GetResidentActionSummary(fixture.ResidentId);
        Assert.Contains("恢复白天活动", summary);
        Assert.DoesNotContain("按作息入睡", summary);
        Assert.DoesNotContain("清晨醒来", summary);
        Assert.DoesNotContain("休息与睡眠", summary);
    }

    /// <summary>玩家仍在指挥时可覆盖作息；命令到期后恢复自主睡眠。</summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Player_order_overrides_routine_only_until_its_deadline(bool active)
    {
        var fixture = Prepare(SimulationTime.SleepTick - 1);
        fixture.Resident.Replace(fixture.Resident.Value.WithAgent(fixture.Resident.Value.Agent with
        {
            Goal = fixture.Resident.Value.Agent.Goal with
            {
                PlayerDirected = true, ReviewTick = SimulationTime.SleepTick + (active ? 1 : 0),
            },
        }));

        fixture.Engine.Step();

        Assert.Equal(!active, fixture.Resident.Value.Activity == ResidentActivity.Sleeping);
    }

    /// <summary>水井的共享额度日内不重置，下一日才恢复。</summary>
    [Fact]
    public void Well_quota_resets_at_day_boundary_instead_of_each_tick()
    {
        var fixture = Prepare(SimulationTime.WakeTick);
        fixture.AddWell(16, 17, .025);
        fixture.Resident.Replace(fixture.Resident.Value with { FromX = 16, X = 16 });
        fixture.Resident.Replace(fixture.Resident.Value with { FromY = 17, Y = 17 });
        fixture.Resident.Replace(fixture.Resident.Value.WithInventory(new ResourceStock()));
        fixture.Resident.Replace(fixture.Resident.Value.WithAgent(fixture.Resident.Value.Agent with
        {
            Goal = new AgentGoal
            {
                Kind = AgentGoalKind.FetchWater,
                TargetX = 16,
                TargetY = 17,
                TargetEntityId = 17 * 32 + 16 + 1,
                ReviewTick = SimulationTime.TicksPerYear,
            },
        }));
        Assert.True(fixture.Engine.TryFetchWater(fixture.Resident.Value));
        fixture.Engine.SimulationTick++;

        Assert.False(fixture.Engine.TryFetchWater(fixture.Resident.Value));
        Assert.Equal(.15, fixture.Resident.Value.Inventory.Water, 8);
        fixture.Engine.SimulationTick = SimulationTime.TicksPerDay;
        Assert.True(fixture.Engine.TryFetchWater(fixture.Resident.Value));
        Assert.Equal(.3, fixture.Resident.Value.Inventory.Water, 8);
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
        fixture.Engine.SimulationTick = tick;
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
            Agent = fixture.Resident.Value.Agent with
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
