using SeWZC.WorldBox.Core;

namespace SeWZC.WorldBox.Core.Tests;

/// <summary>每日需求的不可变转换与粮水消费次序。</summary>
public sealed class ResidentNeedsTests
{
    /// <summary>首次跨过严重饥饿阈值时立即请求复评，不能被原有远期安排延后。</summary>
    [Fact]
    public void Crossing_critical_hunger_requests_an_immediate_review()
    {
        var before = new Resident { Age = 20, Hunger = 59.9, Inventory = new ResourceStock { Water = 1 },
            Agent = new AgentState { NextThinkTick = 100, Goal = new AgentGoal { ReviewTick = 100 } } };

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
        var before = new Resident { Age = 20, Health = .2, Thirst = 96, Hunger = 90,
            Inventory = new ResourceStock { Food = 1 } };

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

    /// <summary>关闭需求规则不消费粮水，已有饥渴仍按原规则恢复。</summary>
    [Fact]
    public void Disabled_needs_preserve_inventory()
    {
        var before = new Resident { Thirst = 80, Hunger = 7,
            Inventory = new ResourceStock { Food = 1, Water = 1 } };

        var after = before.AdvanceNeeds(new WorldRules { Hunger = false, Thirst = false }, 1);

        Assert.Equal(before.Inventory, after.Inventory);
        Assert.Equal(0, after.Thirst);
        Assert.Equal(4, after.Hunger);
        Assert.Equal(before.Health, after.Health);
        Assert.Equal(.07, after.Agent.SocialNeed);
    }

    /// <summary>正常饮水后饥饿致死的记录归于当天的饥饿伤害。</summary>
    [Fact]
    public void Starvation_follows_water_consumption()
    {
        var before = new Resident { Age = 20, Health = .3, Hunger = 81, Thirst = 96,
            Inventory = new ResourceStock { Water = 1 } };

        var after = before.AdvanceNeeds(new WorldRules(), 15);

        Assert.Equal(.975, after.Inventory.Water);
        Assert.Equal(93, after.Thirst);
        Assert.Equal(0, after.Health);
        Assert.Equal(DeathCause.Starvation, after.DeathCause);
        Assert.Equal(15, after.DeathTick);
        Assert.Equal(1, before.Inventory.Water);
    }
}
