namespace SeWZC.WorldBox.Core.Tests;

/// <summary>任务状态转换的消息选择、容量和输入隔离。</summary>
public sealed class AgentMissionTests
{
    /// <summary>开始递送优先选择紧急消息，同级消息按观察时间排序并保留原记忆。</summary>
    [Fact]
    public void Delivery_selects_up_to_eight_messages_without_changing_input()
    {
        var routine = Enumerable.Range(1, 9).Select(tick => new AgentFact
        {
            Kind = AgentFactKind.FoodSupply,
            ObservedTick = tick,
        }).ToArray();
        var urgent = new AgentFact { Kind = AgentFactKind.WarOrder, ObservedTick = 0 };
        var carried = new AgentFact { Kind = AgentFactKind.Personal };
        var before = new AgentState
        {
            Memory = [.. routine, urgent],
            CarriedMessages = [carried],
            Goal = new AgentGoal { Kind = AgentGoalKind.DeliverMessage, TargetSettlementId = 8 },
        };

        var after = before.BeginMission(4, 12);

        Assert.Equal<AgentFact>([urgent, .. routine.Reverse().Take(7)], after.CarriedMessages);
        Assert.Equal(8, after.DestinationSettlementId);
        Assert.Equal(4, after.MissionOriginSettlementId);
        Assert.Equal(12, after.MissionStartedTick);
        Assert.Equal(before.Memory, after.Memory);
        Assert.Same(carried, Assert.Single(before.CarriedMessages));
        Assert.Equal(0, before.DestinationSettlementId);
    }

    /// <summary>贸易先清空携带消息，到场装货后才选择新消息。</summary>
    [Fact]
    public void Trade_starts_with_empty_messages_and_preserves_the_old_mission()
    {
        var fact = new AgentFact { Kind = AgentFactKind.FoodSupply };
        var before = new AgentState
        {
            Memory = [fact],
            CarriedMessages = [fact],
            Goal = new AgentGoal { Kind = AgentGoalKind.Trade, TargetSettlementId = 8 },
        };

        var after = before.BeginMission(4, 12);

        Assert.Empty(after.CarriedMessages);
        Assert.Same(fact, Assert.Single(before.CarriedMessages));
        Assert.Equal(before.Memory, after.Memory);
    }

    /// <summary>结束任务一次转换返乡状态并淘汰最早决策，原认知仍可独立读取。</summary>
    [Fact]
    public void Completion_clears_the_mission_and_retains_the_latest_six_decisions()
    {
        var history = Enumerable.Range(1, 6).Select(tick => new AgentDecision { Tick = tick }).ToArray();
        var fact = new AgentFact();
        var before = new AgentState
        {
            Decisions = [.. history],
            Memory = [fact],
            CarriedMessages = [fact],
            DestinationSettlementId = 8,
            Goal = new AgentGoal { Kind = AgentGoalKind.DeliverMessage },
        };
        var returning = new AgentGoal { Kind = AgentGoalKind.ReturnHome, TargetSettlementId = 4 };

        var after = before.FinishMission(returning, "已递送", 12, 9);

        Assert.Equal(history.Skip(1), after.Decisions.Take(5));
        Assert.Equal(6, after.Decisions.Count);
        Assert.Equal("已递送", after.Decisions[^1].Reason);
        Assert.Equal(9, after.Decisions[^1].SourceResidentId);
        Assert.Same(returning, after.Goal);
        Assert.Equal(0, after.DestinationSettlementId);
        Assert.Empty(after.CarriedMessages);
        Assert.Equal(102, after.MissionRetryTick);
        Assert.Equal(24, after.NextThinkTick);
        Assert.Equal(before.Memory, after.Memory);
        Assert.Equal(history, before.Decisions);
        Assert.Equal(8, before.DestinationSettlementId);
        Assert.Same(fact, Assert.Single(before.CarriedMessages));
    }
}
