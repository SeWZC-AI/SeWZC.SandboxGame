namespace SeWZC.WorldBox.Core.Tests;

/// <summary>项目观测的不可变状态转换检查。</summary>
public sealed class ProjectObservationTests
{
    /// <summary>登记劳动者保留原记录，并且不会重复登记。</summary>
    [Fact]
    public void Adding_contributor_preserves_the_previous_observation()
    {
        var original = new ProjectObservation();

        var changed = original.AddContributor(5);

        Assert.Empty(original.Contributors);
        Assert.Equal(5, Assert.Single(changed.Contributors));
        Assert.Same(changed, changed.AddContributor(5));
    }

    /// <summary>未到四日采样间隔时保留现有记录。</summary>
    [Fact]
    public void Observing_before_interval_returns_the_previous_observation()
    {
        var original = new ProjectObservation().Observe(12, 1, 3);

        var changed = original.Observe(15, 1, 4);

        Assert.Same(original, changed);
        Assert.Equal(3, Assert.Single(changed.Samples).Progress);
    }

    /// <summary>倍率改变时，新快照重新采样，旧快照仍保留原记录。</summary>
    [Fact]
    public void Changing_rate_restarts_samples_without_mutating_old_observation()
    {
        var original = new ProjectObservation().Observe(12, 1, 3);

        var changed = original.Observe(13, 2, 5);

        Assert.Equal(12, Assert.Single(original.Samples).Tick);
        Assert.Equal(1, original.DevelopmentRate);
        Assert.Equal(13, Assert.Single(changed.Samples).Tick);
        Assert.Equal(2, changed.DevelopmentRate);
    }

    /// <summary>第八条采样替换最旧样本，先前快照不被淘汰影响。</summary>
    [Fact]
    public void Eighth_sample_evicts_the_oldest_from_the_new_snapshot()
    {
        var original = new ProjectObservation
        {
            Samples =
            [
                new ProgressSample { Tick = 0 }, new ProgressSample { Tick = 4 }, new ProgressSample { Tick = 8 },
                new ProgressSample { Tick = 12 },
                new ProgressSample { Tick = 16 }, new ProgressSample { Tick = 20 }, new ProgressSample { Tick = 24 },
            ],
        };

        var changed = original.Observe(28, 1, 5);

        Assert.Equal(7, changed.Samples.Length);
        Assert.Equal(4, changed.Samples[0].Tick);
        Assert.Equal(28, changed.Samples[^1].Tick);
        Assert.Equal(0, original.Samples[0].Tick);
    }
}
