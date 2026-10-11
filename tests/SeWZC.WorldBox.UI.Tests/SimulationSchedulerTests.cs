using SeWZC.WorldBox.Core;
using SeWZC.WorldBox.UI.Services;

namespace SeWZC.WorldBox.UI.Tests;

/// <summary>模拟调度在等待、慢帧和不限速档位下的时间行为。</summary>
public sealed class SimulationSchedulerTests
{
    /// <summary>不足一刻时不提前推进，下一次调用使用积累时间。</summary>
    [Fact]
    public void Accumulated_time_advances_only_when_a_tick_is_due()
    {
        var engine = WorldEngine.Create(42, 32, 32, false);
        var scheduler = new SimulationScheduler();
        Assert.Equal(0, scheduler.Advance(engine, 1, .1));
        Assert.Equal(0, engine.State.Tick);
        Assert.Equal(1, scheduler.Advance(engine, 1, .1));
        Assert.Equal(1, engine.State.Tick);
        Assert.Equal(0, scheduler.TickFraction);
    }

    /// <summary>上一帧地图刷新很慢时仍推进首刻，不限速也会让出线程。</summary>
    [Theory]
    [InlineData(5, .25)]
    [InlineData(0, 0)]
    public void Expensive_map_refresh_keeps_progress_and_yields(int speed, double elapsed)
    {
        var engine = WorldEngine.Create(42, 32, 32, false);
        var scheduler = new SimulationScheduler { LastMapRefreshMilliseconds = 1000 };
        Assert.Equal(1, scheduler.Advance(engine, speed, elapsed));
        Assert.Equal(1, engine.State.Tick);
        Assert.InRange(scheduler.NextDelay(speed, false, 1000), 1, 50);
    }

    /// <summary>切换不限速档清除欠账，重新开始世界时同时清除速率统计。</summary>
    [Fact]
    public void Clearing_pending_time_does_not_replay_old_elapsed_time()
    {
        var engine = WorldEngine.Create(42, 32, 32, false);
        var scheduler = new SimulationScheduler();
        scheduler.Advance(engine, 1, .1);
        scheduler.ClearPendingTime();
        Assert.Equal(0, scheduler.Advance(engine, 1, .1));
        scheduler.Reset();
        Assert.Equal(0, scheduler.Accumulator);
        Assert.Equal(50, scheduler.NextDelay(1, true, 1000));
    }
}
