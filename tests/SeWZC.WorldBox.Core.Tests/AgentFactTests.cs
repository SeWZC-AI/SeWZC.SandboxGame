namespace SeWZC.WorldBox.Core.Tests;

/// <summary>信息快照的时效与议题主体边界检查。</summary>
public sealed class AgentFactTests
{
    /// <summary>观察时间决定衰减，转述时更新获知时间不能恢复信息时效。</summary>
    [Theory]
    [InlineData(AgentFactKind.FoodSupply, 180)]
    [InlineData(AgentFactKind.Danger, 24)]
    [InlineData(AgentFactKind.SettlementLocation, 1200)]
    [InlineData(AgentFactKind.ReliefRequest, 180)]
    [InlineData(AgentFactKind.Research, 600)]
    public void Relaying_does_not_reset_original_observation_lifetime(AgentFactKind kind, long lifetime)
    {
        var observed = new AgentFact { Kind = kind, ObservedTick = 12, LearnedTick = 12, Confidence = 0.8 };
        var relayed = observed with { LearnedTick = 12 + lifetime / 2, SourceResidentId = 99 };

        Assert.Equal(0.4, relayed.ReliabilityAt(12 + lifetime / 2), 10);
        Assert.Equal(0, relayed.ReliabilityAt(12 + lifetime));
        Assert.Equal(12, observed.LearnedTick);
        Assert.Equal(0, observed.SourceResidentId);
    }

    /// <summary>地点性消息不合并不同位置，同一聚落的其他观察按主体合并。</summary>
    [Theory]
    [InlineData(AgentFactKind.Danger, false)]
    [InlineData(AgentFactKind.Personal, false)]
    [InlineData(AgentFactKind.FoodSupply, true)]
    [InlineData(AgentFactKind.SettlementLocation, true)]
    public void Subject_matching_obeys_topic_location_semantics(AgentFactKind kind, bool sameSubject)
    {
        var first = new AgentFact { Kind = kind, SubjectId = 5, X = 1, Y = 2 };

        Assert.Equal(sameSubject, first.HasSameSubject(first with { X = 3 }));
        Assert.False(first.HasSameSubject(first with { TargetNationId = 8 }));
    }
}
