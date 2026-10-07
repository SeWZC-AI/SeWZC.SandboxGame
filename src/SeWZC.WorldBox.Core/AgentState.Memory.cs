namespace SeWZC.WorldBox.Core;

public sealed partial record AgentState
{
    // 替换与容量淘汰合成一次认知转换，保持议题规则和同优先级时的原有顺序。
    internal AgentState Remember(AgentFact fact, int homeId)
    {
        var memory = Memory;
        for (var i = 0; i < memory.Count; i++)
        {
            var old = memory[i];
            if (!fact.HasSameSubject(old)) continue;
            if (!fact.Supersedes(old)) return this;
            memory = memory.RemoveAt(i);
            break;
        }

        memory = memory.Add(fact);
        if (memory.Count > 16)
        {
            var forgotten = 0;
            var lowestPriority = memory[0].RetentionPriority(homeId);
            for (var i = 1; i < memory.Count; i++)
            {
                var priority = memory[i].RetentionPriority(homeId);
                if (priority >= lowestPriority) continue;
                forgotten = i;
                lowestPriority = priority;
            }
            memory = memory.RemoveAt(forgotten);
        }
        return this with { Memory = memory };
    }
}
