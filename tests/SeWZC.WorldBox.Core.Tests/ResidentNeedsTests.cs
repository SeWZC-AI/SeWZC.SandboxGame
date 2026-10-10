using SeWZC.WorldBox.Core.Runtime;

namespace SeWZC.WorldBox.Core.Tests;

/// <summary>每日需求的不可变转换与粮水消费次序。</summary>
public sealed class ResidentNeedsTests
{
    /// <summary>普通环境的成年人断粮五日或断水两日时耗尽储备并失去行动能力。</summary>
    [Theory]
    [InlineData(true, 5)]
    [InlineData(false, 2)]
    public void Deprivation_exhausts_a_normal_adult_at_the_requested_deadline(bool food, int days)
    {
        var before = new Resident { Age = 25 };
        var rules = new WorldRules { Aging = false, Hunger = food, Thirst = !food };
        var tile = new Tile { Terrain = TerrainType.Grass, NaturalWaterYield = .1 };

        var after = before.CalculateDay(rules, tile, days * SimulationTime.TicksPerDay, before.Profession,
            0, 0, true, elapsedDays: days).Apply(before);

        Assert.Equal(100, food ? after.Hunger : after.Thirst);
        Assert.Equal(ResidentActivity.Unconscious, after.Activity);
        Assert.False(ResidentNeedsRules.CanWork(after));
        Assert.Equal(0, before.Hunger);
        Assert.Equal(0, before.Thirst);
    }

    /// <summary>带着物资仍会逐渐饥渴，未到吃喝时刻不会每刻自动扣除背包。</summary>
    [Fact]
    public void Carried_supplies_do_not_suppress_the_growth_of_needs()
    {
        var before = new Resident { Age = 25, Inventory = new ResourceStock { Food = 1, Water = 1 } };
        var after = before.CalculateDay(new WorldRules { Aging = false }, new Tile { NaturalWaterYield = .1 },
            1, before.Profession, 0, 0, true, elapsedDays: 1d / SimulationTime.TicksPerDay).Apply(before);

        Assert.True(after.Hunger > 0);
        Assert.True(after.Thirst > 0);
        Assert.Equal(before.Inventory, after.Inventory);
    }

    /// <summary>未完成移动区段时不能边赶路边自动进食。</summary>
    [Fact]
    public void A_travelling_resident_waits_until_arrival_to_eat()
    {
        var before = new Resident
        {
            Age = 25, Hunger = 40, Thirst = 40, Activity = ResidentActivity.Wandering,
            MoveStartedTick = 1, MoveDurationTicks = 2, Inventory = new ResourceStock { Food = 1, Water = 1 },
        };
        var after = before.CalculateDay(new WorldRules { Aging = false }, new Tile { NaturalWaterYield = .1 },
            2, before.Profession, 0, 0, true, elapsedDays: 1d / SimulationTime.TicksPerDay).Apply(before);

        Assert.True(after.Hunger > before.Hunger);
        Assert.True(after.Thirst > before.Thirst);
        Assert.Equal(before.Inventory, after.Inventory);
        Assert.Equal(ResidentActivity.Wandering, after.Activity);
    }

    /// <summary>吃喝按实际物资缓解饥渴，保留工作目标并暂停本刻劳动。</summary>
    [Fact]
    public void A_meal_consumes_real_supplies_and_preserves_the_work_goal()
    {
        var before = new Resident
        {
            Age = 25, Hunger = 10, Thirst = 17, Activity = ResidentActivity.Working,
            Inventory = new ResourceStock { Food = 1, Water = 1, Wood = 2 },
            Agent = new AgentState { Goal = new AgentGoal { Kind = AgentGoalKind.Work, TargetX = 5, TargetY = 5 } },
        };
        var after = before.AdvanceNeeds(new WorldRules(), 1, new Tile { NaturalWaterYield = .1 }, 1d / 24);

        Assert.Equal(10d / 12, after.Hunger, 10);
        Assert.Equal(17 + 50d / 24 - 50d / 3, after.Thirst, 10);
        Assert.Equal(.98, after.Inventory.Food);
        Assert.Equal(1 - .0125 / 3, after.Inventory.Water, 10);
        Assert.Equal(2, after.Inventory.Wood);
        Assert.Equal(ResidentActivity.Eating, after.Activity);
        Assert.Equal(before.Agent.Goal, after.Agent.Goal);
        Assert.Equal(1, before.Inventory.Food);
    }

    /// <summary>空水背包的口渴居民先饮水，再实际装水，不被逐刻小口饮水锁在井边。</summary>
    [Fact]
    public void A_thirsty_resident_at_a_well_continues_collecting_water_after_drinking()
    {
        var fixture = new WorldFixture();
        fixture.Engine.ConfigureWorld(new WorldRules
        {
            Aging = false, Hunger = false, Births = false, Construction = false, Expansion = false, Research = false,
        }, false, false);
        fixture.Engine.SimulationTick = SimulationTime.WakeTick;
        fixture.AddWell(16, 17, .025);
        fixture.Town.Replace(fixture.Town.Value.WithResources(new ResourceStock()));
        fixture.Resident.Replace(fixture.Resident.Value.WithPosition(16, 17) with
        {
            Age = 25, Thirst = 20, Inventory = new ResourceStock(),
            Agent = new AgentState
            {
                Initialized = true, NextThinkTick = 100,
                Goal = new AgentGoal { Kind = AgentGoalKind.FetchWater, TargetX = 16, TargetY = 17,
                    TargetEntityId = 17 * 32 + 16 + 1, PlayerDirected = true, ReviewTick = 100 },
            },
        });

        fixture.Engine.Step(2);

        Assert.True(fixture.Resident.Value.Thirst < 20);
        Assert.True(fixture.Resident.Value.Inventory.Water > .1);
        Assert.NotEqual(ResidentActivity.Eating, fixture.Resident.Value.Activity);
        Assert.Equal(.15, fixture.Engine.State.Tiles[17 * 32 + 16].WaterDrawn, 10);
    }

    /// <summary>引擎中的进食刻停止现场劳动，并保留原工作目标。</summary>
    [Fact]
    public void Eating_suspends_the_actual_labor_action()
    {
        var fixture = new WorldFixture();
        fixture.Engine.ConfigureWorld(new WorldRules
        {
            Aging = false, Thirst = false, Births = false, Construction = false, Expansion = false,
            Research = false, ResourceRegeneration = false,
        }, false, false);
        fixture.Engine.SimulationTick = SimulationTime.WakeTick;
        fixture.Resident.Replace(fixture.Resident.Value.WithPosition(10, 10) with
        {
            Age = 25, Hunger = 10, Profession = Profession.Farmer,
            Inventory = new ResourceStock { Food = 1 },
            Agent = new AgentState
            {
                Initialized = true, NextThinkTick = 100,
                Goal = new AgentGoal { Kind = AgentGoalKind.Gather, TargetX = 10, TargetY = 10,
                    PlayerDirected = true, ReviewTick = 100 },
            },
        });
        var goal = fixture.Resident.Value.Agent.Goal;
        var plants = fixture.Engine.State.Tiles[10 * 32 + 10].Plants;

        fixture.Engine.Step();

        Assert.Equal(.98, fixture.Resident.Value.Inventory.Food, 10);
        Assert.Equal(ResidentActivity.Eating, fixture.Resident.Value.Activity);
        Assert.Equal(goal, fixture.Resident.Value.Agent.Goal);
        Assert.Equal(plants, fixture.Engine.State.Tiles[10 * 32 + 10].Plants);
    }

    /// <summary>军人采用同一日内饥渴规则，实际领取军队补给但不再每刻自动吃喝。</summary>
    [Fact]
    public void Soldiers_grow_hungry_and_thirsty_with_carried_supplies()
    {
        var fixture = new WorldFixture();
        fixture.Engine.ConfigureWorld(new WorldRules
        {
            Aging = false, Births = false, Construction = false, Expansion = false, Research = false,
        }, false, false);
        var armyId = fixture.Engine.NextId++;
        fixture.Engine.Armies.Add(new StateReference<Army>(new Army
        {
            Id = armyId, NationId = fixture.Town.Value.NationId, X = 10, Y = 10,
            Supplies = 1, WaterSupplies = 1,
        }));
        fixture.Resident.Replace(fixture.Resident.Value.WithPosition(10, 10) with
        {
            Age = 25, ArmyId = armyId, Inventory = new ResourceStock(),
            Agent = new AgentState { Initialized = true },
        });

        fixture.Engine.Step();

        Assert.True(fixture.Resident.Value.Hunger > 0);
        Assert.True(fixture.Resident.Value.Thirst > 0);
        Assert.Equal(.02, fixture.Resident.Value.Inventory.Food, 10);
        Assert.Equal(.0125 / 3, fixture.Resident.Value.Inventory.Water, 10);
        Assert.Equal(.98, fixture.Engine.State.Armies.Single().Supplies, 10);
    }

    /// <summary>睡眠期间需求继续增加，但普通饥渴不会让居民边睡边吃喝。</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Sleeping_increases_needs_without_consuming_carried_supplies(bool sick)
    {
        var before = new Resident
        {
            Age = 25, Hunger = 20, Thirst = 20, Activity = ResidentActivity.Sleeping,
            SicknessTicks = sick ? 24 : 0,
            Inventory = new ResourceStock { Food = 1, Water = 1 },
        };
        var after = before.CalculateDay(new WorldRules { Aging = false }, new Tile { NaturalWaterYield = .1 },
            1, before.Profession, 0, 0, true, elapsedDays: 1d / 24).Apply(before);

        Assert.True(after.Hunger > before.Hunger);
        Assert.True(after.Thirst > before.Thirst);
        Assert.Equal(before.Inventory, after.Inventory);
        Assert.Equal(ResidentActivity.Sleeping, after.Activity);
    }

    /// <summary>体力恢复使饥渴跨过危机阈值时，也须立即请求重排远期任务。</summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Recovery_crossing_a_nutrition_crisis_requests_an_immediate_review(bool food)
    {
        var fixture = new WorldFixture();
        fixture.Engine.ConfigureWorld(new WorldRules
        {
            Aging = false, Hunger = food, Thirst = !food, Births = false, Construction = false,
            Expansion = false, Research = false,
        }, false, false);
        fixture.Town.Replace(fixture.Town.Value.WithResources(new ResourceStock()));
        var house = fixture.Engine.GetResidentHome(fixture.ResidentId)!;
        fixture.Engine.Tiles[house.Y * 32 + house.X].Replace(new Tile { Terrain = TerrainType.Grass, NaturalWaterYield = .1 });
        fixture.Engine.SimulationTick = SimulationTime.WakeTick;
        fixture.Resident.Replace(fixture.Resident.Value.WithPosition(house.X, house.Y) with
        {
            Age = 25, Hunger = food ? 59 : 0, Thirst = food ? 0 : 77.7, IsInsideHome = true,
            Activity = ResidentActivity.Resting, Inventory = new ResourceStock(),
            Agent = new AgentState
            {
                Initialized = true, Fatigue = 50, NextThinkTick = 100,
                Goal = new AgentGoal { Kind = AgentGoalKind.Rest, TargetX = house.X, TargetY = house.Y,
                    TargetEntityId = house.Id, PlayerDirected = true, ReviewTick = 100 },
            },
        });

        fixture.Engine.Step();

        Assert.True(food ? fixture.Resident.Value.Hunger > 60 : fixture.Resident.Value.Thirst > 80);
        Assert.Equal(fixture.Engine.State.Tick, fixture.Resident.Value.Agent.NextThinkTick);
        Assert.Equal(fixture.Engine.State.Tick, fixture.Resident.Value.Agent.Goal.ReviewTick);
    }

    /// <summary>昏迷者不能自行吃喝，住宅内有人照料时才可实际喂食。</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void An_unconscious_resident_requires_assistance_to_consume_supplies(bool assisted)
    {
        var before = new Resident
        {
            Age = 25, Hunger = 100, Thirst = 100, Activity = ResidentActivity.Unconscious,
            Inventory = new ResourceStock { Food = 1, Water = 1 },
        };
        var after = before.AdvanceNeeds(new WorldRules(), 1, new Tile { NaturalWaterYield = .1 }, 1d / 24,
            assistedFeeding: assisted);

        Assert.Equal(assisted, after.Hunger < 100);
        Assert.Equal(assisted, after.Thirst < 100);
        Assert.Equal(assisted, after.Inventory.Food < 1);
        Assert.Equal(assisted, after.Inventory.Water < 1);
        Assert.Equal(ResidentActivity.Unconscious, after.Activity);
    }

    /// <summary>体力恢复带来的饥渴只按实际恢复量增加，满体力与零质量休息不产生额外需求。</summary>
    [Theory]
    [InlineData(0, 1, 0)]
    [InlineData(1, 1, 1)]
    [InlineData(50, 1, 6.25)]
    [InlineData(50, .5, 3.125)]
    [InlineData(50, 0, 0)]
    public void Stamina_recovery_charges_only_the_amount_actually_restored(double fatigue, double quality, double restored)
    {
        var before = new Resident
        {
            Age = 25, Hunger = 10, Thirst = 10, Activity = ResidentActivity.Resting,
            Agent = new AgentState { Fatigue = fatigue },
        };
        var after = before.AdvanceRecovery(new WorldRules(), 1, quality);

        Assert.Equal(10 + restored * .05, after.Hunger, 10);
        Assert.Equal(10 + restored * .1, after.Thirst, 10);
        Assert.Equal(fatigue - restored, after.Agent.Fatigue, 10);
        Assert.Equal(before.Inventory, after.Inventory);
    }

    /// <summary>恢复消耗也可能使饮食储备耗尽，在本刻立即昏迷。</summary>
    [Fact]
    public void Recovery_can_exhaust_nutrition_and_force_unconsciousness()
    {
        var before = new Resident
        {
            Age = 25, Thirst = 99.9, Activity = ResidentActivity.Resting,
            Agent = new AgentState { Fatigue = 50 },
        };
        var after = before.AdvanceRecovery(new WorldRules(), 1, 1);

        Assert.Equal(100, after.Thirst);
        Assert.Equal(ResidentActivity.Unconscious, after.Activity);
        Assert.False(ResidentNeedsRules.CanWork(after));
    }


    /// <summary>环境供水减缓每位居民口渴，不共享打水额度、不产出库存，也不修改旧快照。</summary>
    [Fact]
    public void Natural_water_reduces_thirst_without_drawing_a_shared_quota()
    {
        var fixture = new WorldFixture();
        fixture.Engine.ConfigureWorld(fixture.Engine.State.Rules with { Hunger = false }, false, true);
        fixture.Town.Replace(fixture.Town.Value.WithResources(new ResourceStock()));
        fixture.Resident.Replace(fixture.Resident.Value with
        {
            Age = 20,
            X = 16,
            Y = 16,
            FromX = 16,
            FromY = 16,
            Inventory = new ResourceStock(),
            Thirst = 10,
            FrozenUntilTick = 10,
        });
        fixture.Engine.Residents.Add(new StateReference<Resident>(fixture.Resident.Value with { Id = 900, Name = "第二位居民" }));
        var source = fixture.Engine.Tiles[16 * 32 + 16];
        source.Replace(source.Value.WithNaturalWaterYield(.04));
        source.Replace(source.Value.WithDroughtTicks(0));
        var before = fixture.Engine.State;

        fixture.Engine.Step();

        var after = fixture.Engine.State;
        Assert.Equal(10 + 50d / SimulationTime.TicksPerDay, after.Residents[0].Thirst, 8);
        Assert.Equal(10 + 50d / SimulationTime.TicksPerDay, after.Residents[1].Thirst, 8);
        Assert.All(after.Residents, person => Assert.Equal(0, person.Inventory.Water));
        Assert.Equal(before.Tiles[16 * 32 + 16].WaterDrawn, after.Tiles[16 * 32 + 16].WaterDrawn);
        Assert.Equal(before.Tiles[16 * 32 + 16].WaterDrawTick, after.Tiles[16 * 32 + 16].WaterDrawTick);
        Assert.Equal(10, before.Residents[0].Thirst);
        Assert.NotEqual(1, before.Tiles[16 * 32 + 16].WaterDrawTick);
    }

    /// <summary>环境最多抵扣一半饮水需求，干旱降低抵扣；儿童和成人均仍须饮水。</summary>
    [Theory]
    [InlineData(20, 0, 0, 100)]
    [InlineData(20, .01, 0, 60)]
    [InlineData(20, .04, 0, 50)]
    [InlineData(20, .04, 10, 68)]
    [InlineData(10, .04, 0, 50)]
    public void Environmental_water_offsets_only_part_of_thirst_growth(
        int age, double naturalWater, int drought, double thirst)
    {
        var before = new Resident { Age = age };
        var tile = new Tile { Terrain = TerrainType.Grass, NaturalWaterYield = naturalWater, DroughtTicks = drought };

        var after = before.AdvanceDay(new WorldRules { Aging = false, Hunger = false }, tile, 1,
            before.Profession, 0, 0, true);

        Assert.Equal(thirst, after.Thirst, 8);
        Assert.Equal(0, after.Inventory.Water);
        Assert.Equal(0, before.Thirst);
    }

    /// <summary>水井日额度只含实际井水，日常饮用按居民顺序共享剩余额度，旧快照保持原值。</summary>
    [Fact]
    public void Daily_drinking_shares_only_the_well_quota_in_resident_order()
    {
        var fixture = new WorldFixture();
        fixture.Engine.ConfigureWorld(fixture.Engine.State.Rules with { Hunger = false }, false, true);
        fixture.Town.Replace(fixture.Town.Value.WithResources(new ResourceStock()));
        fixture.AddWell(16, 17, .025);
        fixture.Resident.Replace(fixture.Resident.Value with
        {
            Age = 20,
            X = 16,
            Y = 17,
            FromX = 16,
            FromY = 17,
            Inventory = new ResourceStock(),
            Thirst = 20,
            FrozenUntilTick = 10,
        });
        fixture.Engine.Residents.Add(new StateReference<Resident>(fixture.Resident.Value with { Id = 900, Name = "第二位居民" }));
        var source = fixture.Engine.Tiles[17 * 32 + 16];
        source.Replace(source.Value.WithWaterDrawTick(1));
        source.Replace(source.Value.WithWaterDrawn(.15 - .0125 / SimulationTime.TicksPerDay));
        var before = fixture.Engine.State;

        fixture.Engine.Step();

        var after = fixture.Engine.State;
        Assert.Equal(20, after.Residents[0].Thirst, 8);
        Assert.Equal(20 + 50d / SimulationTime.TicksPerDay, after.Residents[1].Thirst, 8);
        Assert.Equal(.15, after.Tiles[17 * 32 + 16].WaterDrawn, 8);
        Assert.Equal(0, fixture.Engine.AvailableWater(16, 17), 8);
        Assert.Equal(.15 - .0125 / SimulationTime.TicksPerDay, before.Tiles[17 * 32 + 16].WaterDrawn);
        Assert.Equal(20, before.Residents[0].Thirst);
    }

    /// <summary>首次跨过严重饥饿阈值时立即请求复评，不能被原有远期安排延后。</summary>
    [Fact]
    public void Crossing_critical_hunger_requests_an_immediate_review()
    {
        var before = new Resident
        {
            Age = 20,
            Hunger = 59.9,
            Inventory = new ResourceStock { Water = 1 },
            Agent = new AgentState { NextThinkTick = 100, Goal = new AgentGoal { ReviewTick = 100 } },
        };

        var after = before.AdvanceNeeds(new WorldRules(), 3);

        Assert.True(after.Hunger > 60);
        Assert.Equal(3, after.Agent.NextThinkTick);
        Assert.Equal(3, after.Agent.Goal.ReviewTick);
        Assert.Equal(100, before.Agent.NextThinkTick);
        Assert.Equal(100, before.Agent.Goal.ReviewTick);
    }

    /// <summary>脱水致死后不再消耗粮食，旧居民和库存保持原值。</summary>
    [Fact]
    public void Lethal_dehydration_prevents_eating()
    {
        var before = new Resident
        {
            Age = 20,
            Health = .2,
            Thirst = 96,
            Hunger = 90,
            Inventory = new ResourceStock { Food = 1 },
        };

        var after = before.AdvanceNeeds(new WorldRules(), 12);

        Assert.Equal(.2, before.Health);
        Assert.Equal(96, before.Thirst);
        Assert.Equal(1, after.Inventory.Food);
        Assert.Equal(90, after.Hunger);
        Assert.Equal(0, after.Health);
        Assert.Equal(DeathCause.Dehydration, after.DeathCause);
        Assert.Equal(12, after.DeathTick);
        Assert.Equal(.07, after.Agent.SocialNeed);
        Assert.Equal(0, before.Agent.SocialNeed);
    }

    /// <summary>关闭需求规则不消费粮水，并清除已有饥渴。</summary>
    [Fact]
    public void Disabled_needs_preserve_inventory()
    {
        var before = new Resident { Thirst = 80, Hunger = 7, Inventory = new ResourceStock { Food = 1, Water = 1 } };

        var after = before.AdvanceNeeds(new WorldRules { Hunger = false, Thirst = false }, 1);

        Assert.Equal(before.Inventory, after.Inventory);
        Assert.Equal(0, after.Thirst);
        Assert.Equal(0, after.Hunger);
        Assert.Equal(before.Health, after.Health);
        Assert.Equal(.07, after.Agent.SocialNeed);
    }

    /// <summary>正常饮水后饥饿致死的记录归于当天的饥饿伤害。</summary>
    [Fact]
    public void Starvation_follows_water_consumption()
    {
        var before = new Resident
        {
            Age = 20,
            Health = .3,
            Hunger = 99.5,
            Thirst = 20,
            Inventory = new ResourceStock { Water = 1 },
        };

        var after = before.AdvanceNeeds(new WorldRules(), 15, new Tile { NaturalWaterYield = .1 },
            1d / SimulationTime.TicksPerDay);

        Assert.Equal(1 - .0125 / 3, after.Inventory.Water, 10);
        Assert.Equal(20 + 50d / 24 - 50d / 3, after.Thirst, 10);
        Assert.Equal(0, after.Health);
        Assert.Equal(DeathCause.Starvation, after.DeathCause);
        Assert.Equal(15, after.DeathTick);
        Assert.Equal(1, before.Inventory.Water);
    }
}
