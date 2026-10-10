namespace SeWZC.WorldBox.Core.Tests;

/// <summary>局部冲突与存活居民索引的衔接。</summary>
public sealed class LocalConflictTests
{
    /// <summary>参与者已死亡归档时，冲突剔除旧编号并降低紧张程度，不能继续使用旧引用产生压力。</summary>
    [Fact]
    public void An_archived_participant_is_removed_and_tension_falls()
    {
        var fixture = new WorldFixture();
        fixture.Engine.SpawnResidents(16, 16, RaceKind.Human, 1);
        fixture.Engine.ConfigureWorld(new WorldRules
        {
            Aging = false, Hunger = true, Thirst = false, Disease = false, Births = false,
            Construction = false, Expansion = false, Research = false, Migration = false, Secession = false,
        }, false, false);
        foreach (var person in fixture.Engine.Residents)
            person.Replace(person.Value with
            {
                Age = 25, Hunger = 50, Inventory = new ResourceStock(), FrozenUntilTick = 100,
                Agent = new AgentState
                {
                    Initialized = true, NextThinkTick = 100, Goal = new AgentGoal { ReviewTick = 100 },
                    Memory = [new AgentFact
                    {
                        Kind = AgentFactKind.FoodSupply, SubjectId = fixture.Town.Value.Id,
                        Confidence = 1, ObservedTick = 1, LearnedTick = 1,
                    }],
                },
            });
        var first = fixture.Engine.Residents[0];
        var second = fixture.Engine.Residents[1];
        fixture.Engine.Conflicts = [new LocalConflict
        {
            Id = fixture.Engine.NextId++, FirstResidentId = first.Value.Id, SecondResidentId = second.Value.Id,
            SettlementId = fixture.Town.Value.Id, X = 16, Y = 16, Tension = 20,
            Participants = [first.Value.Id, second.Value.Id],
        }];
        fixture.Engine.SimulationTick = 11;
        fixture.Engine.Step();
        var before = fixture.Engine.State;
        Assert.Equal(24, Assert.Single(before.Conflicts).Tension);
        first.Replace(first.Value with { Health = 0 });
        fixture.Engine.SimulationTick = 23;

        fixture.Engine.Step();

        var conflict = Assert.Single(fixture.Engine.State.Conflicts);
        Assert.Equal(12, conflict.Tension);
        Assert.DoesNotContain(first.Value.Id, conflict.Participants);
        Assert.Equal(second.Value.Id, Assert.Single(conflict.Participants));
        Assert.Equal(first.Value.Id, Assert.Single(fixture.Engine.State.ArchivedResidents).Id);
        Assert.Equal(ConflictStage.Dispute, Assert.Single(before.Conflicts).Stage);
        Assert.Equal(100, before.Residents[0].Health);
    }
}
