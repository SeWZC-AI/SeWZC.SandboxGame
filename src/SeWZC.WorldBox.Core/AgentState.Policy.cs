namespace SeWZC.WorldBox.Core;

public sealed partial record AgentState
{
    /// <summary>按已收到且仍可信的家园政策计算远处采集倍率。</summary>
    /// <param name="settlementId">家园聚落的稳定 ID。</param>
    /// <param name="tick">评估消息可信度的模拟 tick 序。</param>
    internal double FoodPolicyMultiplier(int settlementId, long tick)
    {
        AgentFact? instruction = null;
        foreach (var fact in Memory)
            if (fact.Kind == AgentFactKind.Policy && fact.SubjectId == settlementId &&
                fact.ReliabilityAt(tick) >= 0.5
                && (instruction is null || fact.ObservedTick > instruction.ObservedTick))
                instruction = fact;
        return instruction?.Value == (int)PolicyKind.FoodSecurity ? 1.25 : 1;
    }
}
