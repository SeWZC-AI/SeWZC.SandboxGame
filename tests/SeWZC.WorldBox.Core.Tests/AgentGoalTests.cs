namespace SeWZC.WorldBox.Core.Tests;

/// <summary>行动目标的纯导航与驻留进度转换检查。</summary>
public sealed class AgentGoalTests
{
    /// <summary>相同目标复用原认知，改变目标时保留原状态及其记忆。</summary>
    [Fact]
    public void Unchanged_goal_reuses_agent_state_and_changed_goal_preserves_the_input()
    {
        var goal = new AgentGoal { Kind = AgentGoalKind.Work };
        var before = new AgentState { Goal = goal, Memory = [new AgentFact()] };

        Assert.Same(before, before.WithGoal(goal));
        Assert.Same(before, before.WithGoal(goal with { }));
        var next = goal with { Kind = AgentGoalKind.ReturnHome };
        var after = before.WithGoal(next);

        Assert.Same(goal, before.Goal);
        Assert.Same(next, after.Goal);
        Assert.Equal(before.Memory, after.Memory);
    }

    /// <summary>需要等待的任务逐日到场登记，在三日后保留已完成的进度。</summary>
    [Theory]
    [InlineData(AgentGoalKind.ReturnHome)]
    [InlineData(AgentGoalKind.ClaimLand)]
    [InlineData(AgentGoalKind.FetchWater)]
    [InlineData(AgentGoalKind.DeliverMessage)]
    [InlineData(AgentGoalKind.Trade)]
    [InlineData(AgentGoalKind.Petition)]
    public void Residence_wait_completes_without_accumulating_unused_days(AgentGoalKind kind)
    {
        var goal = new AgentGoal { Kind = kind };

        var first = goal.Attend();
        var second = first.Attend();
        var completed = second.Attend();

        Assert.Equal(0, goal.WorkTicks);
        Assert.Equal(1, first.WorkTicks);
        Assert.Equal(2, second.WorkTicks);
        Assert.Equal(3, completed.WorkTicks);
        Assert.Equal(completed, completed.Attend());
    }

    /// <summary>普通劳动与休息不产生无用途的驻留计数。</summary>
    [Theory]
    [InlineData(AgentGoalKind.Gather)]
    [InlineData(AgentGoalKind.Work)]
    [InlineData(AgentGoalKind.Rest)]
    [InlineData(AgentGoalKind.Socialize)]
    [InlineData(AgentGoalKind.Explore)]
    public void Ordinary_actions_do_not_accumulate_residence(AgentGoalKind kind)
    {
        var goal = new AgentGoal { Kind = kind };

        Assert.Equal(goal, goal.Attend());
    }

    /// <summary>尚在路上或被冻结时不登记驻留，到达且解冻后才记第一日。</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Residence_starts_after_arrival_and_thaw(bool frozen)
    {
        var fixture = new WorldFixture();
        fixture.Engine.ConfigureWorld(fixture.Engine.State.Rules with
        {
            Births = false,
            Construction = false,
            Research = false,
            Expansion = false,
            Migration = false,
            Secession = false,
        }, false, false);
        fixture.Resident.Replace(fixture.Resident.Value with { X = fixture.Town.Value.X });
        fixture.Resident.Replace(fixture.Resident.Value with { Y = fixture.Town.Value.Y });
        fixture.Resident.Replace(fixture.Resident.Value with { FromX = fixture.Town.Value.X });
        fixture.Resident.Replace(fixture.Resident.Value with { FromY = fixture.Town.Value.Y });
        fixture.Resident.Replace(fixture.Resident.Value with { MoveDurationTicks = frozen ? 1 : 3 });
        fixture.Resident.Replace(fixture.Resident.Value with { MoveStartedTick = 0 });
        fixture.Resident.Replace(fixture.Resident.Value with { FrozenUntilTick = frozen ? 3 : 0 });
        fixture.Resident.Replace(fixture.Resident.Value.WithAgent(fixture.Resident.Value.Agent with
        {
            Goal = new AgentGoal
            {
                Kind = AgentGoalKind.ReturnHome,
                TargetX = fixture.Town.Value.X,
                TargetY = fixture.Town.Value.Y,
                TargetSettlementId = fixture.Town.Value.Id,
                PlayerDirected = true,
                ReviewTick = 100,
            },
        }));

        fixture.Engine.Step();
        Assert.Equal(0, fixture.Resident.Value.Agent.Goal.WorkTicks);

        fixture.Engine.Step(2);
        Assert.Equal(1, fixture.Resident.Value.Agent.Goal.WorkTicks);
    }

    /// <summary>普通自主移动同时保留出发点、路线、疲劳和途中状态，旧快照保持未出发。</summary>
    [Fact]
    public void Movement_preserves_navigation_and_waits_for_arrival()
    {
        var fixture = new WorldFixture();
        fixture.Engine.ConfigureWorld(fixture.Engine.State.Rules with
        {
            Births = false,
            Construction = false,
            Research = false,
            Expansion = false,
            Migration = false,
            Secession = false,
        }, false, false);
        fixture.Resident.Replace(fixture.Resident.Value with { X = 16 });
        fixture.Resident.Replace(fixture.Resident.Value with { Y = 16 });
        fixture.Resident.Replace(fixture.Resident.Value with { FromX = 16 });
        fixture.Resident.Replace(fixture.Resident.Value with { FromY = 16 });
        fixture.Resident.Replace(fixture.Resident.Value.WithAgent(fixture.Resident.Value.Agent with
        {
            Goal = new AgentGoal
            {
                Kind = AgentGoalKind.Explore,
                TargetX = 19,
                TargetY = 16,
                PlayerDirected = true,
                ReviewTick = 100,
            },
        }));
        var before = fixture.Engine.State.Residents[0];

        fixture.Engine.Step();
        var moved = fixture.Engine.State.Residents[0];

        Assert.Equal(16, before.X);
        Assert.Empty(before.Agent.Goal.NavigationVisited);
        Assert.Equal(0, before.Agent.Fatigue);
        Assert.Equal(17, moved.X);
        Assert.Equal(16, moved.Y);
        Assert.Equal(16, moved.FromX);
        Assert.Equal(16, moved.FromY);
        Assert.Equal(1, moved.MoveStartedTick);
        Assert.Equal(2, moved.MoveDurationTicks);
        Assert.Equal(2, moved.Agent.Goal.NavigationBestDistance);
        Assert.Empty(moved.Agent.Goal.NavigationVisited);
        Assert.Equal(.15, moved.Agent.Fatigue);
        Assert.Equal(ResidentActivity.Wandering, moved.Activity);

        fixture.Engine.Step();
        var waiting = fixture.Engine.State.Residents[0];
        Assert.Equal(moved.X, waiting.X);
        Assert.Equal(moved.Agent.Goal, waiting.Agent.Goal);
        Assert.Equal(moved.Agent.Fatigue, waiting.Agent.Fatigue);
    }

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
