using SeWZC.WorldBox.Core;

namespace SeWZC.WorldBox.Core.Tests;

/// <summary>居民基础生命状态的纯转换及伤害次序。</summary>
public sealed class ResidentVitalsTests
{
    /// <summary>新发疫病及时请求自主复评，不等远期工作安排到期。</summary>
    [Fact]
    public void New_illness_requests_a_recovery_review()
    {
        var before = new Resident
        {
            Age = 25, Inventory = new ResourceStock { Food = 1, Water = 1 },
            Agent = new AgentState { NextThinkTick = 100, Goal = new AgentGoal { ReviewTick = 100 } },
        };

        var after = before.AdvanceDay(new WorldRules(), new Tile(), 7, Profession.Farmer, 80, 0, true);

        Assert.Equal(80, after.SicknessTicks);
        Assert.Equal(7, after.Agent.NextThinkTick);
        Assert.Equal(7, after.Agent.Goal.ReviewTick);
        Assert.Equal(100, before.Agent.NextThinkTick);
    }

    /// <summary>现场取得的日常饮水直接进入结算，不凭空留下额外瓶装库存。</summary>
    [Fact]
    public void Delivered_water_is_consumed_by_the_day_transition()
    {
        var before = new Resident { Age = 20, Thirst = 10, Inventory = new ResourceStock { Food = 1 } };

        var after = before.AdvanceDay(new WorldRules(), new Tile(), 1, Profession.Farmer, 0, 0, true,
            deliveredWater: .025);

        Assert.Equal(7, after.Thirst);
        Assert.Equal(0, after.Inventory.Water);
        Assert.Equal(10, before.Thirst);
        Assert.Equal(0, before.Inventory.Water);
    }
    /// <summary>合并日结算仍包含年龄、疫病、魔力和真实粮水消费，保留源值。</summary>
    [Fact]
    public void Combined_day_matches_body_then_needs_for_a_living_adult()
    {
        var before = new Resident { Age = 20, Health = 60, SicknessTicks = 2,
            Inventory = new ResourceStock { Food = 1, Water = 1 } };
        var rules = new WorldRules();
        var tile = new Tile();

        var after = before.AdvanceDay(rules, tile, 1, Profession.Farmer, 0, .1, true);
        var expected = before.AdvanceVitals(rules, tile, 1, Profession.Farmer, 0, .1).AdvanceNeeds(rules, 1);

        Assert.Equal(expected, after);
        Assert.Equal(20, before.Age);
        Assert.Equal(1, before.Inventory.Food);
        Assert.Equal(1, before.Inventory.Water);
    }

    /// <summary>身体伤害当天致死时不再消耗已携带粮水。</summary>
    [Fact]
    public void Lethal_fire_prevents_consuming_supplies()
    {
        var before = new Resident { Age = 20, Health = 1,
            Inventory = new ResourceStock { Food = 1, Water = 1 } };

        var after = before.AdvanceDay(new WorldRules(), new Tile { FireTicks = 1 }, 2,
            Profession.Farmer, 0, .1, true);

        Assert.Equal(DeathCause.Fire, after.DeathCause);
        Assert.Equal(2, after.DeathTick);
        Assert.Equal(before.Inventory, after.Inventory);
    }
    /// <summary>老龄、火灾与疾病连续伤害保留首先致死的原因。</summary>
    [Fact]
    public void Vitals_preserve_the_first_lethal_damage_and_the_source()
    {
        var before = new Resident { Age = 999, Health = .4, SicknessTicks = 1 };

        var after = before.AdvanceVitals(new WorldRules(), new Tile { FireTicks = 1 }, 10, before.Profession, 0);

        Assert.Equal(.4, before.Health);
        Assert.Equal(1, before.SicknessTicks);
        Assert.Equal(0, after.Health);
        Assert.Equal(DeathCause.OldAge, after.DeathCause);
        Assert.Equal(10, after.DeathTick);
        Assert.Equal(190, after.DiseaseImmuneUntilTick);
        Assert.Equal(before.Activity, after.Activity);
    }

    /// <summary>传入新的感染时记录病程并进入生病活动，旧居民保持健康。</summary>
    [Fact]
    public void Infection_is_an_explicit_input_to_the_transition()
    {
        var before = new Resident { Age = 20, Profession = Profession.Farmer };

        var after = before.AdvanceVitals(new WorldRules(), new Tile(), 12, before.Profession, 80);

        Assert.Equal(0, before.SicknessTicks);
        Assert.Equal(80, after.SicknessTicks);
        Assert.Equal(ResidentActivity.Sick, after.Activity);
    }

    /// <summary>关闭衰老不增加年龄，传入的职业仍用于成年转换。</summary>
    [Fact]
    public void Disabled_aging_preserves_age()
    {
        var before = new Resident { Age = 14, Health = 50, Profession = Profession.Child };

        var after = before.AdvanceVitals(new WorldRules { Aging = false }, new Tile(), 1, Profession.Builder, 0);

        Assert.Equal(14, after.Age);
        Assert.Equal(50.15, after.Health);
        Assert.Equal(Profession.Builder, after.Profession);
        Assert.Equal(Profession.Child, before.Profession);
    }

    /// <summary>身体和需求均未变化时，实际抵达仍须记入熟路，并保留输入快照。</summary>
    [Fact]
    public void Arrival_is_remembered_when_vitals_and_needs_do_not_change()
    {
        var before = new Resident { Age = 20, Health = 100, Mana = 100 };
        var rules = new WorldRules { Aging = false, Hunger = false, Thirst = false, Disease = false };

        var after = before.AdvanceDay(rules, new Tile(), 1, before.Profession, 0, 0, true, 0, arrivedTile: 10);

        Assert.Empty(before.Agent.FamiliarTiles);
        Assert.Equal<int>([10], after.Agent.FamiliarTiles);
        Assert.Equal(before with { Agent = after.Agent }, after);
    }
}
