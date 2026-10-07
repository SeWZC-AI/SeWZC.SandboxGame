using SeWZC.WorldBox.Core;

namespace SeWZC.WorldBox.Core.Tests;

/// <summary>行动目标的纯导航状态转换检查。</summary>
public sealed class AgentGoalTests
{
    /// <summary>新目的地清除旧路线和受阻进度，但保留任务依据。</summary>
    [Fact]
    public void New_destination_starts_navigation_without_changing_the_old_goal()
    {
        var goal = new AgentGoal
        {
            Kind = AgentGoalKind.Work,
            TargetEntityId = 7,
            NavigationTarget = 5,
            NavigationVisited = [1, 2],
            NavigationBestDistance = 3,
            NavigationWithoutProgress = 8,
            NavigationRetryTick = 20,
        };

        var next = goal.BeginNavigation(9, 6);

        Assert.Equal(9, next.NavigationTarget);
        Assert.Equal(6, next.NavigationBestDistance);
        Assert.Empty(next.NavigationVisited);
        Assert.Equal(0, next.NavigationRetryTick);
        Assert.Equal(0, next.NavigationWithoutProgress);
        Assert.Equal(7, next.TargetEntityId);
        Assert.Equal<int>([1, 2], goal.NavigationVisited);
        Assert.Equal(20, goal.NavigationRetryTick);
        Assert.Equal(next, next.BeginNavigation(9, 5));
    }

    /// <summary>路径追加只生成新目标，重复访问不会增加记录。</summary>
    [Fact]
    public void Visiting_a_tile_preserves_previous_navigation_versions()
    {
        var goal = new AgentGoal { NavigationVisited = [1] };

        var next = goal.Visit(2);

        Assert.Equal<int>([1], goal.NavigationVisited);
        Assert.Equal<int>([1, 2], next.NavigationVisited);
        Assert.Equal(next, next.Visit(2));
    }

    /// <summary>受阻重试保留当前任务及最佳距离，清除路线与等待。</summary>
    [Fact]
    public void Retrying_navigation_preserves_the_mission()
    {
        var goal = new AgentGoal
        {
            Kind = AgentGoalKind.Trade,
            TargetSettlementId = 4,
            NavigationTarget = 7,
            NavigationVisited = [2],
            NavigationBestDistance = 3,
            NavigationWithoutProgress = 8,
            NavigationRetryTick = 20,
        };

        var next = goal.ResetNavigation();

        Assert.Equal(AgentGoalKind.Trade, next.Kind);
        Assert.Equal(4, next.TargetSettlementId);
        Assert.Equal(3, next.NavigationBestDistance);
        Assert.Equal(-1, next.NavigationTarget);
        Assert.Empty(next.NavigationVisited);
        Assert.Equal(0, next.NavigationWithoutProgress);
        Assert.Equal(0, next.NavigationRetryTick);
        Assert.Equal(7, goal.NavigationTarget);
    }
}
