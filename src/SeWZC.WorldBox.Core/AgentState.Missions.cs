using System.Collections.Immutable;

namespace SeWZC.WorldBox.Core;

public sealed partial record AgentState
{
    // 开始任务只选择已有消息，不领取物资或改写外部状态。
    internal AgentState BeginMission(int homeId, long tick)
    {
        var messages = Goal.Kind == AgentGoalKind.Trade
            ? ImmutableList<AgentFact>.Empty
            : Memory.OrderByDescending(f =>
                    f.Kind is AgentFactKind.ReliefRequest or AgentFactKind.WarOrder or AgentFactKind.PeaceOrder
                        or AgentFactKind.DiplomaticNotice or AgentFactKind.WarReport
                        ? 1
                        : 0)
                .ThenByDescending(f => f.ObservedTick).Take(8).ToImmutableList();
        return this with
        {
            DestinationSettlementId = Goal.TargetSettlementId,
            MissionOriginSettlementId = homeId,
            MissionStartedTick = tick,
            CarriedMessages = messages,
        };
    }

    // 任务结束保留原始记忆及随身货物，只更新行动、消息和决策。
    internal AgentState FinishMission(AgentGoal nextGoal, string reason, long tick, int residentId)
    {
        return RecordDecision(new AgentDecision
            {
                Tick = tick,
                Goal = AgentGoalKind.ReturnHome,
                Reason = reason,
                Score = 80,
                KnowledgeObservedTick = tick,
                SourceResidentId = residentId,
            }) with
            {
                DestinationSettlementId = 0,
                CarriedMessages = [],
                MissionRetryTick = tick + 90,
                Goal = nextGoal,
                NextThinkTick = tick + 12,
            };
    }
}
