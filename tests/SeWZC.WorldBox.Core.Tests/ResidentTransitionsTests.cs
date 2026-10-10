namespace SeWZC.WorldBox.Core.Tests;

/// <summary>居民动作的纯转换及其快照边界。</summary>
public sealed class ResidentTransitionsTests
{
    /// <summary>一次动作共同转换认知、库存和活动，保留身体及真实移动区段。</summary>
    [Fact]
    public void An_action_preserves_its_source_and_unrelated_state()
    {
        var initial = new Resident
        {
            Id = 1, Age = 42, Health = 85, Hunger = 12, X = 3, Y = 4,
            FromX = 2, FromY = 4, MoveStartedTick = 10, MoveDurationTicks = 3,
            Inventory = new ResourceStock { Food = 5, Water = 2 },
        };
        var agent = initial.Agent with { Fatigue = 4 };
        var inventory = initial.Inventory with { Food = 6 };

        var result = initial.WithAction(inventory, agent, ResidentActivity.Working);

        Assert.Equal(initial with { Agent = agent, Inventory = inventory, Activity = ResidentActivity.Working }, result);
        Assert.Equal(5, initial.Inventory.Food);
        Assert.Equal(ResidentActivity.Wandering, initial.Activity);
        Assert.NotSame(agent, initial.Agent);
        Assert.Same(result, result.WithAction(inventory, agent, ResidentActivity.Working));
    }

    /// <summary>新移动区段从当前坐标开始，后续位置转换不修改已取得的区段。</summary>
    [Fact]
    public void Movement_returns_a_new_segment_without_changing_previous_snapshots()
    {
        var initial = new Resident { X = 3, Y = 4, TravelMode = TravelMode.Boat, Age = 42 };
        var moved = initial.BeginMove(5, 6, 10, 3, ResidentActivity.Wandering, initial.Agent);
        var positioned = moved.WithPosition(7, 8);

        Assert.Equal((3, 4, 3, 4, 0L), (initial.X, initial.Y, moved.FromX, moved.FromY, initial.MoveStartedTick));
        Assert.Equal((5, 6, 10L, 3), (moved.X, moved.Y, moved.MoveStartedTick, moved.MoveDurationTicks));
        Assert.Equal((7, 8, 7, 8), (positioned.X, positioned.Y, positioned.FromX, positioned.FromY));
        Assert.Equal(TravelMode.Boat, positioned.TravelMode);
        Assert.Equal(42, positioned.Age);
    }
}
