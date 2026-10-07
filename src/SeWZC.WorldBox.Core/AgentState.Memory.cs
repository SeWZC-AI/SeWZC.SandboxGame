using System.Runtime.InteropServices;

namespace SeWZC.WorldBox.Core;

public sealed partial record AgentState
{
    // 短记忆按原有顺序保留；替换与淘汰直接生成一个不可变数组。
    internal AgentState Remember(AgentFact fact, int homeId)
    {
        var replaced = -1;
        for (var index = 0; index < Memory.Length; index++)
        {
            var old = Memory[index];
            if (!fact.HasSameSubject(old)) continue;
            if (!fact.Supersedes(old)) return this;
            replaced = index;
            break;
        }

        var count = Memory.Length + (replaced < 0 ? 1 : 0);
        var forgotten = -1;
        if (count > 16)
        {
            var lowestPriority = long.MaxValue;
            for (var index = 0; index < Memory.Length; index++)
            {
                if (index == replaced) continue;
                var priority = Memory[index].RetentionPriority(homeId);
                if (forgotten >= 0 && priority >= lowestPriority) continue;
                forgotten = index;
                lowestPriority = priority;
            }
            if (fact.RetentionPriority(homeId) < lowestPriority) return this;
            count--;
        }

        var items = new AgentFact[count];
        var position = 0;
        for (var index = 0; index < Memory.Length; index++)
            if (index != replaced && index != forgotten) items[position++] = Memory[index];
        items[position] = fact;
        return this with { Memory = ImmutableCollectionsMarshal.AsImmutableArray(items) };
    }
}
