using System.Diagnostics;

namespace SeWZC.WorldBox.UI.Tests;

/// <summary>最近一秒活动统计的窗口边界和重置检查。</summary>
public sealed class RecentActivityCounterTests
{
    /// <summary>恰好一秒前的样本过期，更近的样本保留。</summary>
    [Fact]
    public void Count_excludes_samples_at_the_one_second_boundary()
    {
        var counter = new RecentActivityCounter();
        counter.Record(0);
        counter.Record(1);

        Assert.Equal(2, counter.Count(Stopwatch.Frequency - 1));
        Assert.Equal(1, counter.Count(Stopwatch.Frequency));
    }

    /// <summary>没有新活动时，旧样本仍会全部过期。</summary>
    [Fact]
    public void Idle_window_returns_zero()
    {
        var counter = new RecentActivityCounter();
        counter.Record(0);

        Assert.Equal(0, counter.Count(Stopwatch.Frequency * 2));
    }

    /// <summary>同一时间完成的多个活动分别计数。</summary>
    [Fact]
    public void Simultaneous_activities_are_counted_individually()
    {
        var counter = new RecentActivityCounter();
        counter.Record(0);
        counter.Record(0);
        counter.Record(0);

        Assert.Equal(3, counter.Count(0));
    }

    /// <summary>切换世界时清除旧样本，新活动重新计数。</summary>
    [Fact]
    public void Clear_discards_previous_activities()
    {
        var counter = new RecentActivityCounter();
        counter.Record(0);

        counter.Clear();
        Assert.Equal(0, counter.Count(1));
        counter.Record(1);
        Assert.Equal(1, counter.Count(1));
    }
}
