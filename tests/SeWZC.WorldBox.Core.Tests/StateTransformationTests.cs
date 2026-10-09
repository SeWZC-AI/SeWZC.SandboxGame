namespace SeWZC.WorldBox.Core.Tests;

/// <summary>嵌套状态转换保留输入及先前返回值，不通过包装引用回写世界。</summary>
public sealed class StateTransformationTests
{
    /// <summary>启动及多人贡献研究时，每个保留版本的进度和参与者独立。</summary>
    [Fact]
    public void Research_versions_preserve_progress_and_contributors()
    {
        var original = new SettlementResearch { SettlementId = 7 };
        var started = original.Begin(Advancement.Agriculture, 10, 12, 1);
        var first = started.AddWork(20, 3);
        var second = first.AddWork(21, 4);
        var learned = second.Learn(Advancement.Agriculture);

        Assert.Null(original.ActiveProject);
        Assert.Empty(original.Observation.Samples);
        Assert.Equal(0, started.Progress);
        Assert.Empty(started.Observation.Contributors);
        Assert.Equal(3, first.Progress);
        Assert.Equal([20], first.Observation.Contributors.ToArray());
        Assert.Equal(7, second.Progress);
        Assert.Equal([20, 21], second.Observation.Contributors.ToArray());
        Assert.Empty(second.Completed);
        Assert.Contains(Advancement.Agriculture, learned.Completed);
        Assert.Same(learned, learned.Learn(Advancement.Agriculture));
    }

    /// <summary>单方态度变化保留另一方态度，限制范围并按远离零的方向舍入展示均值。</summary>
    [Theory]
    [InlineData(1, 200, 100, -3, 49)]
    [InlineData(2, -200, 4, -100, -48)]
    [InlineData(1, 0, 0, -3, -2)]
    public void Diplomacy_changes_one_side_without_changing_the_input(int nationId, int opinion,
        int first, int second, int mean)
    {
        var original = new DiplomaticRelation
        {
            FirstNationId = 1,
            SecondNationId = 2,
            FirstOpinion = 4,
            SecondOpinion = -3,
            Opinion = 1,
        };

        var changed = original.WithLocalOpinion(nationId, opinion);

        Assert.Equal(4, original.FirstOpinion);
        Assert.Equal(-3, original.SecondOpinion);
        Assert.Equal(1, original.Opinion);
        Assert.Equal(first, changed.FirstOpinion);
        Assert.Equal(second, changed.SecondOpinion);
        Assert.Equal(mean, changed.Opinion);
    }

    /// <summary>文化接触间隔不满足时复用输入，后续累计保留以前的接触记录。</summary>
    [Fact]
    public void Cultural_contact_reuses_early_observations_and_preserves_previous_exposure()
    {
        var original = new CulturalContact { ResidentId = 1, CultureId = 2, Exposure = 3, LastContactTick = 12 };

        Assert.Same(original, original.Observe(23, 0.75));
        var changed = original.Observe(24, 0.75);

        Assert.Equal(3, original.Exposure);
        Assert.Equal(12, original.LastContactTick);
        Assert.Equal(4.25, changed.Exposure);
        Assert.Equal(24, changed.LastContactTick);
    }

    /// <summary>政策命令显式提交新的制度及本地政策，旧 Society 及其嵌套值保持不变。</summary>
    [Fact]
    public void Policy_commands_preserve_retained_society_values()
    {
        var fixture = new WorldFixture();
        var original = fixture.Engine.Current.Society;

        fixture.Engine.SetPolicy(fixture.Town.Value.NationId, PolicyKind.Defense);
        var directed = fixture.Engine.Current.Society;
        fixture.Engine.SetPolicyAutonomy(fixture.Town.Value.NationId);
        var autonomous = fixture.Engine.Current.Society;

        Assert.Null(original.Institutions.Single().PlayerPolicy);
        Assert.False(original.Policies.Single().PlayerOverride);
        Assert.Equal(PolicyKind.Defense, directed.Institutions.Single().PlayerPolicy);
        Assert.True(directed.Policies.Single().PlayerOverride);
        Assert.Null(autonomous.Institutions.Single().PlayerPolicy);
        Assert.False(autonomous.Policies.Single().PlayerOverride);
        Assert.Equal(PolicyKind.Defense, autonomous.Policies.Single().Kind);
    }
}
