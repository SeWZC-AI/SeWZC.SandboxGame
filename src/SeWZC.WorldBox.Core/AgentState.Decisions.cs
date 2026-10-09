namespace SeWZC.WorldBox.Core;

public sealed partial record AgentState
{
    // 决策只保留最近六条；新记录与容量淘汰合并为一次状态转换。
    internal AgentState RecordDecision(AgentDecision decision)
    {
        var decisions = Decisions.Add(decision);
        if (decisions.Count > 6)
            decisions = decisions.RemoveAt(0);
        return this with { Decisions = decisions };
    }
}
