using System.Text.Json.Nodes;

namespace SeWZC.WorldBox.Core.Tests;

/// <summary>睡眠、体力、劳动年龄和强制昏迷的规则与实际入口。</summary>
public sealed class ResidentEnduranceTests
{
    /// <summary>普通人类清醒十六 tick 保持一半睡眠，再睡八 tick 回到满值。</summary>
    [Fact]
    public void Normal_human_balances_sixteen_awake_and_eight_sleep_ticks()
    {
        var person = new Resident { Age = 18, Activity = ResidentActivity.Working };
        for (var tick = 0; tick < 16; tick++)
            person = person.WithAgent(ResidentNeedsRules.Advance(person, 1));

        Assert.Equal(50, person.Agent.Sleep);
        Assert.Equal(1, ResidentNeedsRules.WorkEfficiency(person));
        Assert.Equal(1, ResidentNeedsRules.WorkEfficiency(person, true));
        person = person.WithActivity(ResidentActivity.Sleeping);
        for (var tick = 0; tick < 8; tick++)
            person = person.WithAgent(ResidentNeedsRules.Advance(person, 1));
        Assert.Equal(100, person.Agent.Sleep);
        Assert.Equal(8, SimulationTime.TicksPerDay - SimulationTime.SleepTick + SimulationTime.WakeTick);
    }

    /// <summary>睡眠恢复抵扣持续消耗，普通休息只恢复体力。</summary>
    [Theory]
    [InlineData(ResidentActivity.Sleeping, 1, 56.25, 43.75)]
    [InlineData(ResidentActivity.Resting, 1, 46.875, 43.75)]
    [InlineData(ResidentActivity.Sleeping, 2, 65.625, 37.5)]
    [InlineData(ResidentActivity.Sleeping, 0, 50, 50)]
    public void Recovery_accounts_for_sleep_consumption_and_quality(ResidentActivity activity, double quality, double sleep, double fatigue)
    {
        var person = new Resident { Age = 18, Activity = activity, Agent = new AgentState { Sleep = 50, Fatigue = 50 } };
        var after = ResidentNeedsRules.Advance(person, quality);
        Assert.Equal(sleep, after.Sleep);
        Assert.Equal(fatigue, after.Fatigue);
        Assert.Equal(50, person.Agent.Sleep);
    }

    /// <summary>睡眠与体力不足对两类劳动的降幅相反，并只在一半以下降效。</summary>
    [Theory]
    [InlineData(50, 50, 1, 1)]
    [InlineData(25, 0, .75, .5)]
    [InlineData(0, 0, .5, 0)]
    [InlineData(100, 75, .5, .75)]
    [InlineData(100, 100, 0, .5)]
    [InlineData(25, 75, .375, .375)]
    public void Sleep_and_stamina_reduce_different_work_types(double sleep, double fatigue, double physical, double mental)
    {
        var person = new Resident { Age = 18, Agent = new AgentState { Sleep = sleep, Fatigue = fatigue } };
        Assert.Equal(physical, ResidentNeedsRules.WorkEfficiency(person));
        Assert.Equal(mental, ResidentNeedsRules.WorkEfficiency(person, true));
    }

    /// <summary>刚达到劳动年龄时为两成效率，十八岁达到完整效率。</summary>
    [Theory]
    [InlineData(11.99, 0)]
    [InlineData(12, .2)]
    [InlineData(15, .6)]
    [InlineData(18, 1)]
    [InlineData(80, 1)]
    public void Work_efficiency_grows_with_age(double age, double expected)
    {
        Assert.Equal(expected, ResidentNeedsRules.AgeEfficiency(age), 10);
    }

    /// <summary>普通人类体力劳动十二 tick 消耗一半体力，脑力劳动消耗较少。</summary>
    [Fact]
    public void Physical_work_consumes_more_stamina_than_mental_work()
    {
        var person = new Resident { Age = 18 };
        var physical = ResidentNeedsRules.ExertionFatigue(person, ResidentNeedsRules.PhysicalWorkCostPerTick * 12);
        var mental = ResidentNeedsRules.ExertionFatigue(person, ResidentNeedsRules.PhysicalWorkCostPerTick * ResidentNeedsRules.MentalWorkCostRatio * 12);
        Assert.Equal(50, physical);
        Assert.Equal(10, mental, 10);
    }

    /// <summary>种族与个人需求上限存在小幅差异。</summary>
    [Fact]
    public void Capacities_vary_by_race_and_person()
    {
        var human = new Resident();
        Assert.Equal(100, ResidentNeedsRules.SleepCapacity(human));
        Assert.Equal(100, ResidentNeedsRules.StaminaCapacity(human));
        Assert.Equal(105, ResidentNeedsRules.SleepCapacity(human with { Race = RaceKind.Elf }));
        Assert.Equal(105, ResidentNeedsRules.StaminaCapacity(human with { Race = RaceKind.Orc }));
        Assert.NotEqual(ResidentNeedsRules.SleepCapacity(human with { Id = 1 }), ResidentNeedsRules.SleepCapacity(human with { Id = 2 }));
        Assert.InRange(ResidentNeedsRules.StaminaCapacity(human with { Id = 1 }), 97.5, 102.5);
    }

    /// <summary>昏迷持续到两项需求均恢复至门槛，不被玩家探索指令打断。</summary>
    [Theory]
    [InlineData(0, 0)]
    [InlineData(100, 100)]
    public void Exhaustion_forces_unconsciousness_and_blocks_directed_movement(double sleep, double fatigue)
    {
        var fixture = DirectedWorld(25, AgentGoalKind.Explore, 20, 16);
        fixture.Resident.Replace(fixture.Resident.Value.WithAgent(fixture.Resident.Value.Agent with { Sleep = sleep, Fatigue = fatigue }));
        fixture.Engine.Step();
        Assert.Equal(16, fixture.Resident.Value.X);
        Assert.Equal(ResidentActivity.Unconscious, fixture.Resident.Value.Activity);
        Assert.True(ResidentNeedsRules.IsUnconscious(fixture.Resident.Value));
        Assert.True(fixture.Resident.Value.Agent.Sleep > 0);
        Assert.True(fixture.Resident.Value.Agent.Fatigue < 100);
    }

    /// <summary>持续消耗在本 tick 耗尽睡眠时即进入昏迷。</summary>
    [Fact]
    public void Sleep_exhaustion_marks_unconsciousness_in_the_same_tick()
    {
        var fixture = DirectedWorld(25, AgentGoalKind.Socialize, 16, 16);
        fixture.Resident.Replace(fixture.Resident.Value.WithAgent(fixture.Resident.Value.Agent with { Sleep = 1 }));
        fixture.Engine.Step();
        Assert.Equal(0, fixture.Resident.Value.Agent.Sleep);
        Assert.Equal(ResidentActivity.Unconscious, fixture.Resident.Value.Activity);
    }

    /// <summary>昏迷苏醒同时核对睡眠与体力，并包含恰好到达门槛的边界。</summary>
    [Theory]
    [InlineData(49.9, 0, true)]
    [InlineData(100, 50.1, true)]
    [InlineData(50, 50, false)]
    public void Waking_requires_both_needs_to_recover(double sleep, double fatigue, bool unconscious)
    {
        var person = new Resident { Activity = ResidentActivity.Unconscious, Agent = new AgentState { Sleep = sleep, Fatigue = fatigue } };
        Assert.Equal(unconscious, ResidentNeedsRules.IsUnconscious(person));
    }

    /// <summary>六岁以下不能通过玩家探索命令出门，六岁可以实际移动。</summary>
    [Theory]
    [InlineData(5.99, 16)]
    [InlineData(6, 18)]
    public void Outdoor_age_limit_applies_to_actual_movement(double age, int expectedX)
    {
        var fixture = DirectedWorld(age, AgentGoalKind.Explore, 20, 16);
        fixture.Engine.Step();
        Assert.Equal(expectedX, fixture.Resident.Value.X);
    }

    /// <summary>十二岁以下即使被指定采集也不会劳动或消耗体力。</summary>
    [Fact]
    public void Children_cannot_execute_directed_gathering()
    {
        var fixture = DirectedWorld(11.99, AgentGoalKind.Gather, 16, 16);
        var before = fixture.Engine.Tiles[16 * 32 + 16].Value.Harvested;
        fixture.Engine.Step();
        Assert.Equal(before, fixture.Engine.Tiles[16 * 32 + 16].Value.Harvested);
        Assert.Equal(0, fixture.Resident.Value.Agent.Fatigue);
        Assert.Equal(ResidentActivity.Resting, fixture.Resident.Value.Activity);
    }

    /// <summary>睡眠字段越界的编辑失败且不改变世界。</summary>
    [Theory]
    [InlineData(-1)]
    [InlineData(101)]
    [InlineData(double.NaN)]
    public void Invalid_sleep_edit_preserves_the_world(double sleep)
    {
        var fixture = new WorldFixture();
        var before = fixture.Engine.ExportJson();
        Assert.Throws<ArgumentException>(() => fixture.Engine.EditResident(fixture.ResidentId,
            new ResidentEdit { Agent = fixture.Resident.Value.Agent with { Sleep = sleep } }));
        Assert.Equal(before, fixture.Engine.ExportJson());
    }

    /// <summary>当前存档不能用缺失的睡眠值替代真实状态。</summary>
    [Fact]
    public void Save_requires_sleep_state()
    {
        var fixture = new WorldFixture();
        var json = JsonNode.Parse(fixture.Engine.ExportJson())!;
        json["Residents"]![0]!["Agent"]!.AsObject().Remove("Sleep");
        Assert.Throws<ArgumentException>(() => WorldEngine.ImportJson(json.ToJsonString()));
    }

    /// <summary>昏迷途中保存恢复后继续产生相同结果。</summary>
    [Fact]
    public void Unconscious_recovery_survives_save_and_resume()
    {
        var fixture = DirectedWorld(25, AgentGoalKind.Explore, 20, 16);
        fixture.Resident.Replace(fixture.Resident.Value.WithAgent(fixture.Resident.Value.Agent with { Sleep = 0, Fatigue = 100 }));
        fixture.Engine.Step();
        var loaded = WorldEngine.ImportJson(fixture.Engine.ExportJson());
        fixture.Engine.Step();
        loaded.Step();
        Assert.Equal(fixture.Engine.ExportJson(), loaded.ExportJson());
    }

    /// <summary>远途宿营晨起时，即使采集错峰尚未到期也要停止睡眠恢复。</summary>
    [Fact]
    public void Camping_gatherer_wakes_before_the_next_work_tick()
    {
        var fixture = DirectedWorld(25, AgentGoalKind.Gather, 24, 16);
        Assert.NotEqual(0, (SimulationTime.WakeTick + fixture.ResidentId) % 4);
        fixture.Engine.SimulationTick = SimulationTime.WakeTick - 1;
        fixture.Resident.Replace(fixture.Resident.Value with
        {
            X = 24, FromX = 24, Activity = ResidentActivity.Sleeping,
            Agent = fixture.Resident.Value.Agent with
            {
                Sleep = 60, NextThinkTick = 100,
                Goal = fixture.Resident.Value.Agent.Goal with { PlayerDirected = false },
            },
        });

        fixture.Engine.Step();

        Assert.Equal(ResidentActivity.Resting, fixture.Resident.Value.Activity);
        Assert.True(fixture.Resident.Value.Agent.Sleep < 60);
        Assert.Equal(AgentGoalKind.Gather, fixture.Resident.Value.Agent.Goal.Kind);
        Assert.Contains("正在休息", fixture.Engine.GetResidentActionSummary(fixture.ResidentId));
        Assert.DoesNotContain("正在采集", fixture.Engine.GetResidentActionSummary(fixture.ResidentId));
    }

    /// <summary>一方昏迷后停止现场争夺；双方昏迷也不能新建争执。</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Unconscious_residents_cannot_participate_in_local_conflicts(bool existingViolence)
    {
        var fixture = DirectedWorld(25, AgentGoalKind.Rest, 16, 16);
        fixture.Engine.SpawnResidents(16, 16, RaceKind.Human, 1);
        fixture.Engine.ConfigureWorld(fixture.Engine.State.Rules with { Wars = true }, false, false);
        fixture.Engine.SimulationTick = 11;
        foreach (var person in fixture.Engine.Residents)
            person.Replace(person.Value with
            {
                Age = 25, X = 16, Y = 16, Hunger = 60, Inventory = new ResourceStock(), FrozenUntilTick = 100,
                Agent = new AgentState
                {
                    Initialized = true, Sleep = existingViolence && person.Value.Id != fixture.ResidentId ? 100 : 0,
                    NextThinkTick = 100, Goal = new AgentGoal { ReviewTick = 100 },
                    Memory = [new AgentFact
                    {
                        Kind = AgentFactKind.FoodSupply, SubjectId = fixture.Town.Value.Id,
                        Confidence = 1, ObservedTick = 1, LearnedTick = 1,
                    }],
                },
            });
        if (existingViolence)
            fixture.Engine.Conflicts = [new LocalConflict
            {
                Id = fixture.Engine.NextId++, FirstResidentId = fixture.ResidentId,
                SecondResidentId = fixture.Engine.Residents.Single(r => r.Value.Id != fixture.ResidentId).Value.Id,
                SettlementId = fixture.Town.Value.Id, X = 16, Y = 16, Tension = 80,
                Stage = ConflictStage.Violence,
            }];

        fixture.Engine.Step();

        Assert.All(fixture.Engine.Residents, person => Assert.Equal(100, person.Value.Health));
        if (existingViolence)
            Assert.Equal(68, Assert.Single(fixture.Engine.State.Conflicts).Tension);
        else
            Assert.Empty(fixture.Engine.State.Conflicts);
    }

    /// <summary>已运营的信号路径允许清醒信使转述，但昏迷信使不能主动发信。</summary>
    [Theory]
    [InlineData(100, true)]
    [InlineData(0, false)]
    public void Message_relay_requires_a_conscious_messenger(double sleep, bool sends)
    {
        var fixture = DirectedWorld(25, AgentGoalKind.Rest, 16, 16);
        fixture.Engine.SpawnResidents(0, 16, RaceKind.Human, 1);
        var destination = fixture.Engine.Settlements.Single(t => t.Value.Id != fixture.Town.Value.Id);
        destination.Replace(destination.Value with { NationId = fixture.Town.Value.NationId });
        var recipient = fixture.Engine.Residents.Single(p => p.Value.SettlementId == destination.Value.Id);
        recipient.Replace(recipient.Value with { X = 0, Y = 16, NationId = fixture.Town.Value.NationId });
        GrantResearch(fixture, Advancement.SignalNetwork);
        GrantResearch(fixture, Advancement.Electrification);
        fixture.Engine.Buildings.RemoveAll(b => b.Value.X == 15 && b.Value.Y == 16);
        var ground = fixture.Engine.Tiles[16 * 32 + 15];
        ground.Replace(ground.Value with
        {
            NationId = fixture.Town.Value.NationId, ClaimedSettlementId = fixture.Town.Value.Id,
        });
        var towerId = fixture.Engine.GrantFacility(fixture.Town.Value.Id, BuildingKind.SignalTower, 15, 16);
        fixture.Engine.SimulationTick = 24 - fixture.ResidentId % 24 - 1;
        var tower = fixture.Engine.Buildings.Single(b => b.Value.Id == towerId);
        tower.Replace(tower.Value with
        {
            Level = 2, LastWorkedTick = fixture.Engine.SimulationTick, Workers = [fixture.ResidentId],
        });
        fixture.Resident.Replace(fixture.Resident.Value with
        {
            Profession = Profession.Messenger,
            Agent = fixture.Resident.Value.Agent with
            {
                Sleep = sleep,
                Memory = [new AgentFact
                {
                    Id = fixture.Engine.NextId++, Kind = AgentFactKind.SettlementLocation,
                    SubjectId = destination.Value.Id, X = 0, Y = 16, ObservedTick = 1, LearnedTick = 1,
                    OriginResidentId = fixture.ResidentId, SourceResidentId = fixture.ResidentId, Confidence = 1,
                }],
            },
        });
        Assert.True(fixture.Engine.CanRelayInformation(fixture.Town.Value.Id, destination.Value.Id, out _));

        fixture.Engine.Step();

        Assert.Equal(sends, fixture.Engine.State.PendingMessages.Any(m => m.SenderId == fixture.ResidentId));
    }

    /// <summary>祈雨的干旱与火势抵扣量都随脑力劳动效率缩减。</summary>
    [Theory]
    [InlineData(18, 100, 1)]
    [InlineData(18, 25, .5)]
    [InlineData(12, 100, .2)]
    public void Rain_effect_scales_with_sleep_and_age(double age, double sleep, double efficiency)
    {
        var fixture = DirectedWorld(age, AgentGoalKind.Rest, 16, 16);
        GrantResearch(fixture, Advancement.NatureBinding);
        fixture.Resident.Replace(fixture.Resident.Value with
        {
            MagicTalent = 60, MagicTraining = 20, Mana = 100,
            Agent = fixture.Resident.Value.Agent with { Sleep = sleep },
        });
        var tile = fixture.Engine.Tiles[16 * 32 + 16];
        var drought = 4 * SimulationTime.TicksPerMonth;
        tile.Replace(tile.Value with { DroughtTicks = drought, FireTicks = 20 });

        fixture.Engine.CastSpell(fixture.ResidentId, SpellKind.RainCall, 16, 16);

        Assert.Equal(drought - (int)(2 * SimulationTime.TicksPerMonth * efficiency), tile.Value.DroughtTicks);
        Assert.Equal(20 - (int)(8 * efficiency), tile.Value.FireTicks);
    }

    /// <summary>治疗的减病时间与冰箭冻结时间也遵循施法者脑力劳动效率。</summary>
    [Theory]
    [InlineData(SpellKind.Heal, 18, 100, 1)]
    [InlineData(SpellKind.Heal, 18, 25, .5)]
    [InlineData(SpellKind.Heal, 12, 100, .2)]
    [InlineData(SpellKind.FrostBolt, 18, 100, 1)]
    [InlineData(SpellKind.FrostBolt, 18, 25, .5)]
    [InlineData(SpellKind.FrostBolt, 12, 100, .2)]
    public void Spell_durations_scale_with_sleep_and_age(SpellKind spell, double age, double sleep, double efficiency)
    {
        var fixture = DirectedWorld(age, AgentGoalKind.Rest, 16, 16);
        fixture.Engine.SpawnResidents(spell == SpellKind.Heal ? 16 : 0, 16, RaceKind.Human, 1);
        var target = fixture.Engine.Residents.Single(p => p.Value.Id != fixture.ResidentId);
        target.Replace(target.Value with { X = 16, Y = 16, Health = 50, SicknessTicks = 100 });
        if (spell == SpellKind.FrostBolt)
            GrantResearch(fixture, Advancement.Elementalism);
        fixture.Resident.Replace(fixture.Resident.Value with
        {
            MagicTalent = 60, MagicTraining = 20, Mana = 100,
            Agent = fixture.Resident.Value.Agent with
            {
                Sleep = sleep,
                Memory = [new AgentFact { Kind = AgentFactKind.WarOrder, SubjectId = target.Value.NationId }],
            },
        });

        fixture.Engine.CastSpell(fixture.ResidentId, spell, 16, 16);

        if (spell == SpellKind.Heal)
            Assert.Equal(100 - (int)(SimulationTime.TicksPerDay * efficiency), target.Value.SicknessTicks);
        else
            Assert.Equal(fixture.Engine.SimulationTick + (int)(6 * efficiency), target.Value.FrozenUntilTick);
    }

    private static void GrantResearch(WorldFixture fixture, Advancement research)
    {
        foreach (var prerequisite in research.Prerequisites)
            GrantResearch(fixture, prerequisite);
        fixture.Engine.GrantReceivedResearch(fixture.Town.Value.Id, research);
    }

    private static WorldFixture DirectedWorld(double age, AgentGoalKind kind, int x, int y)
    {
        var fixture = new WorldFixture();
        fixture.Engine.ConfigureWorld(new WorldRules
        {
            Aging = false, Hunger = false, Thirst = false, Disease = false, Births = false,
            Construction = false, Expansion = false, Research = false, Migration = false, Secession = false, Wars = false,
        }, false, false);
        fixture.Engine.SimulationTick = SimulationTime.WakeTick;
        fixture.Resident.Replace(fixture.Resident.Value with
        {
            Age = age, Profession = Profession.Farmer, X = 16, Y = 16, FromX = 16, FromY = 16,
            Agent = new AgentState
            {
                Initialized = true, Goal = new AgentGoal
                {
                    Kind = kind, TargetX = x, TargetY = y, PlayerDirected = true, ReviewTick = 100,
                },
            },
        });
        return fixture;
    }
}
