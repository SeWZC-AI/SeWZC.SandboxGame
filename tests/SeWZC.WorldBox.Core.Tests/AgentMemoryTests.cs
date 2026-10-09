namespace SeWZC.WorldBox.Core.Tests;

/// <summary>认知记忆的不可变替换、淘汰与显式提交。</summary>
public sealed class AgentMemoryTests
{
    /// <summary>新观察替换同一主体的旧观察，并在新记忆末尾保留来源。</summary>
    [Fact]
    public void Newer_observation_replaces_the_subject_without_changing_the_source()
    {
        var old = new AgentFact { Kind = AgentFactKind.FoodSupply, SubjectId = 4, ObservedTick = 1 };
        var address = new AgentFact { Kind = AgentFactKind.SettlementLocation, SubjectId = 4 };
        var before = new AgentState { Memory = [old, address] };
        var observed = old with { Id = 7, ObservedTick = 2, OriginResidentId = 9 };

        var after = before.Remember(observed, 4);

        Assert.Equal<AgentFact>([old, address], before.Memory);
        Assert.Equal<AgentFact>([address, observed], after.Memory);
        Assert.Equal(9, after.Memory[1].OriginResidentId);
        Assert.Equal(1, before.Memory[0].ObservedTick);
    }

    /// <summary>仅获知时间较新不能覆盖观察更新且更可靠的现有记忆。</summary>
    [Fact]
    public void Relayed_old_observation_does_not_replace_current_knowledge()
    {
        var current = new AgentFact { ObservedTick = 2, Confidence = 1 };
        var before = new AgentState { Memory = [current] };

        var after = before.Remember(current with { ObservedTick = 1, LearnedTick = 9 }, 4);

        Assert.Same(before, after);
    }

    /// <summary>容量已满时保留家园地址，并按原顺序淘汰最早的同优先级记忆。</summary>
    [Fact]
    public void Capacity_preserves_home_address_and_evicts_the_first_lowest_priority()
    {
        var home = new AgentFact { Kind = AgentFactKind.SettlementLocation, SubjectId = 4 };
        var other = Enumerable.Range(10, 15).Select(id => new AgentFact
        {
            Kind = AgentFactKind.Personal, SubjectId = id, LearnedTick = 1,
        }).ToArray();
        var before = new AgentState { Memory = [home, .. other] };
        var policy = new AgentFact { Kind = AgentFactKind.Policy, LearnedTick = 1 };

        var after = before.Remember(policy, 4);

        Assert.Equal(16, before.Memory.Length);
        Assert.Equal<AgentFact>([home, .. other.Skip(1), policy], after.Memory);
        Assert.Same(other[0], before.Memory[1]);
    }

    /// <summary>记忆转换在提交前不修改居民，后续转换保留此前的世界快照。</summary>
    [Fact]
    public void Memory_transition_is_local_until_explicitly_committed()
    {
        var fixture = new WorldFixture();
        var agent = fixture.Resident.Agent;
        var initial = agent.Memory;
        var observed = new AgentFact { SubjectId = 4 };
        var received = new AgentFact { SubjectId = 5 };

        var next = agent.Remember(observed, fixture.Town.Id);
        Assert.Same(agent, fixture.Resident.Agent);
        fixture.Resident.Agent = next;
        var before = fixture.Engine.State;
        fixture.Resident.Agent = next.Remember(received, fixture.Town.Id);

        Assert.Equal<AgentFact>([.. initial, observed], before.Residents[0].Agent.Memory);
        Assert.Equal<AgentFact>([.. initial, observed, received], fixture.Engine.State.Residents[0].Agent.Memory);
        Assert.Equal(initial, agent.Memory);
    }
}
